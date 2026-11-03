// Inference-only MLP policy. MUST stay compilable for net35 / Unity 5.6 Mono (linked into the mod):
// no Span, no System.Numerics, no LINQ, no ValueTuple, no file-scoped namespaces, explicit usings.
// Zero allocations per Forward/Argmax/Sample call: the caller owns every buffer.
//
// .btcpol format (little endian):
//   bytes  "BTCP"
//   uint   version (=1)
//   uint   schemaHash  (FNV-1a of the caller's observation/action layout string)
//   int    inputSize
//   int    numLayers
//   per layer: int outSize, byte activation (0 none, 1 tanh, 2 relu)
//   int    numHeads, int[numHeads] headSizes
//   byte   hasValue (1 = last output after the logits is a state value)
//   per layer: float[out*in] weights (row-major [out][in]), float[out] bias
// Last layer output size == sum(headSizes) + (hasValue ? 1 : 0).
// Optional trailer after the weights (absent in old files; old readers ignore trailing bytes): "META" + float decisionSec.
using System;
using System.IO;

namespace Btc.Shared
{
    public sealed class MlpPolicy
    {
        public const uint FormatVersion = 1;
        public const byte ActNone = 0, ActTanh = 1, ActRelu = 2;

        public uint Version = FormatVersion;
        public uint SchemaHash;
        public int InputSize;
        public int[] LayerOut;     // per layer output size
        public byte[] LayerAct;    // per layer activation
        public float[][] Weights;  // per layer [out*in], row-major
        public float[][] Biases;   // per layer [out]
        public int[] HeadSizes;
        public bool HasValue;
        /// <summary>Decision period (seconds) the policy was trained with; 0 = not stored (controllers fall back to 0.1). Stored in the optional META trailer.</summary>
        public float DecisionSec;

        public int NumLayers { get { return LayerOut.Length; } }
        public int OutputSize { get { return LayerOut[LayerOut.Length - 1]; } }
        public int LogitCount { get { int s = 0; for (int i = 0; i < HeadSizes.Length; i++) s += HeadSizes[i]; return s; } }

        public int MaxWidth
        {
            get
            {
                int m = InputSize;
                for (int i = 0; i < LayerOut.Length; i++) if (LayerOut[i] > m) m = LayerOut[i];
                return m;
            }
        }

        /// <summary>Scratch buffer length Forward needs.</summary>
        public int ScratchSize { get { return 2 * MaxWidth; } }

        public static uint Fnv1a(string s)
        {
            uint h = 2166136261u;
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                h ^= (byte)(c & 0xFF); h *= 16777619u;
                h ^= (byte)(c >> 8); h *= 16777619u;
            }
            return h;
        }

        /// <summary>Forward pass. obs[InputSize] -> output[OutputSize]; scratch[ScratchSize].</summary>
        public void Forward(float[] obs, float[] output, float[] scratch)
        {
            int half = MaxWidth;
            float[] src = obs; int srcOff = 0; int nIn = InputSize;
            int L = LayerOut.Length;
            for (int l = 0; l < L; l++)
            {
                int nOut = LayerOut[l];
                float[] dst; int dstOff;
                if (l == L - 1) { dst = output; dstOff = 0; }
                else { dst = scratch; dstOff = (l & 1) == 0 ? 0 : half; }
                float[] w = Weights[l]; float[] b = Biases[l];
                byte act = LayerAct[l];
                for (int o = 0; o < nOut; o++)
                {
                    float s = 0f;
                    int wo = o * nIn;
                    for (int i = 0; i < nIn; i++) s += src[srcOff + i] * w[wo + i];
                    s += b[o];
                    if (act == ActTanh) s = (float)Math.Tanh(s);
                    else if (act == ActRelu) { if (s < 0f) s = 0f; }
                    dst[dstOff + o] = s;
                }
                src = dst; srcOff = dstOff; nIn = nOut;
            }
        }

        /// <summary>Value output (only if HasValue), read from a Forward output buffer.</summary>
        public float Value(float[] output) { return HasValue ? output[LogitCount] : 0f; }

        /// <summary>Greedy action per head. mask: 1 = allowed, aligned with logits; null = all allowed.</summary>
        public void ArgmaxPerHead(float[] logits, byte[] mask, int[] actions)
        {
            int off = 0;
            for (int h = 0; h < HeadSizes.Length; h++)
            {
                int n = HeadSizes[h];
                int best = 0; float bv = float.NegativeInfinity; bool any = false;
                for (int j = 0; j < n; j++)
                {
                    if (mask != null && mask[off + j] == 0) continue;
                    float v = logits[off + j];
                    if (!any || v > bv) { bv = v; best = j; any = true; }
                }
                actions[h] = best;
                off += n;
            }
        }

        /// <summary>Sample one action per head; uniforms[h] in [0,1) supplied by the caller (deterministic).</summary>
        public void SampleWithRng(float[] logits, byte[] mask, float[] uniforms, int[] actions)
        {
            int off = 0;
            for (int h = 0; h < HeadSizes.Length; h++)
            {
                int n = HeadSizes[h];
                float mx = float.NegativeInfinity; int first = -1;
                for (int j = 0; j < n; j++)
                {
                    if (mask != null && mask[off + j] == 0) continue;
                    if (first < 0) first = j;
                    if (logits[off + j] > mx) mx = logits[off + j];
                }
                if (first < 0) { actions[h] = 0; off += n; continue; }
                double sum = 0;
                for (int j = 0; j < n; j++)
                {
                    if (mask != null && mask[off + j] == 0) continue;
                    sum += Math.Exp(logits[off + j] - mx);
                }
                double u = uniforms[h] * sum, c = 0;
                int pick = -1, last = first;
                for (int j = 0; j < n; j++)
                {
                    if (mask != null && mask[off + j] == 0) continue;
                    last = j;
                    c += Math.Exp(logits[off + j] - mx);
                    if (u < c) { pick = j; break; }
                }
                actions[h] = pick < 0 ? last : pick;
                off += n;
            }
        }

        public byte[] Save()
        {
            MemoryStream ms = new MemoryStream();
            BinaryWriter w = new BinaryWriter(ms);
            w.Write((byte)'B'); w.Write((byte)'T'); w.Write((byte)'C'); w.Write((byte)'P');
            w.Write(Version);
            w.Write(SchemaHash);
            w.Write(InputSize);
            w.Write(LayerOut.Length);
            for (int l = 0; l < LayerOut.Length; l++) { w.Write(LayerOut[l]); w.Write(LayerAct[l]); }
            w.Write(HeadSizes.Length);
            for (int h = 0; h < HeadSizes.Length; h++) w.Write(HeadSizes[h]);
            w.Write((byte)(HasValue ? 1 : 0));
            for (int l = 0; l < LayerOut.Length; l++)
            {
                float[] W = Weights[l]; for (int i = 0; i < W.Length; i++) w.Write(W[i]);
                float[] B = Biases[l]; for (int i = 0; i < B.Length; i++) w.Write(B[i]);
            }
            if (DecisionSec > 0f) { w.Write((byte)'M'); w.Write((byte)'E'); w.Write((byte)'T'); w.Write((byte)'A'); w.Write(DecisionSec); }
            w.Flush();
            return ms.ToArray();
        }

        public static MlpPolicy Load(byte[] data)
        {
            BinaryReader r = new BinaryReader(new MemoryStream(data));
            if (r.ReadByte() != 'B' || r.ReadByte() != 'T' || r.ReadByte() != 'C' || r.ReadByte() != 'P')
                throw new InvalidDataException("not a .btcpol file");
            MlpPolicy p = new MlpPolicy();
            p.Version = r.ReadUInt32();
            if (p.Version != FormatVersion) throw new InvalidDataException("unsupported .btcpol version " + p.Version);
            p.SchemaHash = r.ReadUInt32();
            p.InputSize = r.ReadInt32();
            int L = r.ReadInt32();
            if (L <= 0 || L > 64) throw new InvalidDataException("bad layer count");
            p.LayerOut = new int[L]; p.LayerAct = new byte[L];
            for (int l = 0; l < L; l++) { p.LayerOut[l] = r.ReadInt32(); p.LayerAct[l] = r.ReadByte(); }
            int H = r.ReadInt32();
            p.HeadSizes = new int[H];
            for (int h = 0; h < H; h++) p.HeadSizes[h] = r.ReadInt32();
            p.HasValue = r.ReadByte() != 0;
            if (p.LogitCount + (p.HasValue ? 1 : 0) != p.OutputSize) throw new InvalidDataException("head sizes do not match output size");
            p.Weights = new float[L][]; p.Biases = new float[L][];
            int nIn = p.InputSize;
            for (int l = 0; l < L; l++)
            {
                int nOut = p.LayerOut[l];
                float[] W = new float[nOut * nIn];
                for (int i = 0; i < W.Length; i++) W[i] = r.ReadSingle();
                float[] B = new float[nOut];
                for (int i = 0; i < B.Length; i++) B[i] = r.ReadSingle();
                p.Weights[l] = W; p.Biases[l] = B;
                nIn = nOut;
            }
            if (data.Length - r.BaseStream.Position >= 8 && r.ReadByte() == (byte)'M' && r.ReadByte() == (byte)'E' && r.ReadByte() == (byte)'T' && r.ReadByte() == (byte)'A')
            {
                float d = r.ReadSingle();
                if (d > 0f && d < 10f) p.DecisionSec = d;
            }
            return p;
        }
    }
}
