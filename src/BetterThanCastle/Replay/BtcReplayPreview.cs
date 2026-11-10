using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using UnityEngine;

namespace BetterThanCastle
{
    /// <summary>
    /// Map thumbnails for the replay list. The replay file ends with the world snapshot (a zip written by the game's lzip); its Heightmap.png is read
    /// straight from the zip on a worker thread (lzip.entry2Buffer, nothing is extracted), turned into a small coloured relief on the main thread and
    /// cached as a PNG under &lt;BTC folder&gt;/previews. One job at a time, a couple of cache loads per frame, nothing blocks the menu.
    /// </summary>
    internal static class BtcReplayPreview
    {
        private const int Size = 256;
        private static readonly Dictionary<string, Texture2D> Tex = new Dictionary<string, Texture2D>();
        private static readonly HashSet<string> Failed = new HashSet<string>();
        private static readonly Queue<string> Wanted = new Queue<string>();
        private static readonly HashSet<string> Queued = new HashSet<string>();
        private static volatile bool _busy;
        private static volatile string _doneFile;
        private static volatile byte[] _doneBytes;
        private static int _frame, _loadsThisFrame;

        private static string Dir { get { return Path.Combine(BtcPaths.Root, "previews"); } }

        private static string CacheFile(BtcReplay.Data d)
        {
            string name = Path.GetFileNameWithoutExtension(d.FilePath);
            char[] bad = Path.GetInvalidFileNameChars();
            for (int i = 0; i < bad.Length; i++) name = name.Replace(bad[i], '_');
            if (name.Length > 60) name = name.Substring(0, 60);
            return Path.Combine(Dir, name + "_" + d.FileBytes.ToString("x") + "_p256.png");
        }

        /// <summary>The thumbnail or null (not ready, no snapshot, or failed). Call every frame the card is visible.</summary>
        internal static Texture2D Get(BtcReplay.Data d)
        {
            if (d == null || d.Error != null || !d.Complete || d.SnapBytes <= 0) return null;
            Pump();
            Texture2D t;
            if (Tex.TryGetValue(d.FilePath, out t)) return t;
            if (Failed.Contains(d.FilePath)) return null;
            if (_frame != Time.frameCount) { _frame = Time.frameCount; _loadsThisFrame = 0; }
            string cf = CacheFile(d);
            try
            {
                if (File.Exists(cf))
                {
                    if (_loadsThisFrame >= 2) return null;
                    _loadsThisFrame++;
                    t = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                    t.hideFlags = HideFlags.HideAndDontSave;
                    if (t.LoadImage(File.ReadAllBytes(cf))) { t.filterMode = FilterMode.Bilinear; Tex[d.FilePath] = t; return t; }
                    UnityEngine.Object.Destroy(t);
                    File.Delete(cf);
                }
            }
            catch (Exception ex) { BtcLog.Warn("ReplayPreview", "cache read: " + ex.Message); }
            if (Queued.Add(d.FilePath)) { Wanted.Enqueue(d.FilePath); _wantedData[d.FilePath] = d; }
            return null;
        }

        private static readonly Dictionary<string, BtcReplay.Data> _wantedData = new Dictionary<string, BtcReplay.Data>();

        /// <summary>Called by Get: finishes a job (main thread) and starts the next one.</summary>
        private static void Pump()
        {
            string done = _doneFile;
            if (done != null)
            {
                byte[] png = _doneBytes;
                _doneFile = null; _doneBytes = null;
                BtcReplay.Data d;
                _wantedData.TryGetValue(done, out d);
                Texture2D t = png != null && png.Length > 0 && d != null ? Build(png, d) : null;
                if (t != null) Tex[done] = t; else Failed.Add(done);
                _busy = false;
            }
            if (_busy || Wanted.Count == 0) return;
            string next = Wanted.Dequeue();
            BtcReplay.Data nd;
            if (!_wantedData.TryGetValue(next, out nd)) return;
            _busy = true;
            string file = next;
            long snap = nd.SnapBytes;
            Thread th = new Thread(delegate () { Job(file, snap); });
            th.IsBackground = true;
            th.Start();
        }

        private static void Job(string file, long snapBytes)
        {
            byte[] png = null;
            string zip = Path.Combine(Path.GetTempPath(), "btc-prev-" + Guid.NewGuid().ToString("N") + ".zip");
            try
            {
                using (FileStream fs = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                    byte[] foot = new byte[28];
                    fs.Seek(fs.Length - 28, SeekOrigin.Begin);
                    if (fs.Read(foot, 0, 28) == 28 && foot[0] == (byte)'B' && foot[1] == (byte)'T' && foot[2] == (byte)'C' && foot[3] == (byte)'E')
                    {
                        int len = BitConverter.ToInt32(foot, 24);
                        if (len > 0 && len < fs.Length)
                        {
                            byte[] blob = new byte[len];
                            fs.Seek(fs.Length - 28 - len, SeekOrigin.Begin);
                            int got = 0;
                            while (got < len) { int r = fs.Read(blob, got, len - got); if (r <= 0) break; got += r; }
                            if (got == len) File.WriteAllBytes(zip, blob);
                        }
                    }
                }
                if (File.Exists(zip)) png = lzip.entry2Buffer(zip, "Heightmap.png", null);
            }
            catch (Exception ex) { BtcLog.Warn("ReplayPreview", "job: " + ex.Message); }
            try { if (File.Exists(zip)) File.Delete(zip); } catch { }
            _doneBytes = png;
            _doneFile = file;
        }

        /// <summary>Heightmap PNG -> small coloured relief, saved to the cache.</summary>
        private static Texture2D Build(byte[] png, BtcReplay.Data d)
        {
            Texture2D src = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            try
            {
                if (!src.LoadImage(png) || src.width < 4 || src.height < 4) return null;
                Color32[] px = src.GetPixels32();
                int sw = src.width, sh = src.height;
                float[] h = new float[Size * Size];
                float lo = 1e9f, hi = -1e9f;
                for (int y = 0; y < Size; y++)
                    for (int x = 0; x < Size; x++)
                    {
                        int x0 = x * sw / Size, x1 = Mathf.Max(x0 + 1, (x + 1) * sw / Size), y0 = y * sh / Size, y1 = Mathf.Max(y0 + 1, (y + 1) * sh / Size);
                        float sum = 0f; int n = 0;
                        for (int yy = y0; yy < y1 && yy < sh; yy += Mathf.Max(1, (y1 - y0) / 4))
                            for (int xx = x0; xx < x1 && xx < sw; xx += Mathf.Max(1, (x1 - x0) / 4)) { sum += px[yy * sw + xx].r; n++; }
                        float v = n > 0 ? sum / n : 0f;
                        h[y * Size + x] = v;
                        if (v < lo) lo = v;
                        if (v > hi) hi = v;
                    }
                float span = Mathf.Max(1f, hi - lo);
                Color32[] o = new Color32[Size * Size];
                for (int y = 0; y < Size; y++)
                    for (int x = 0; x < Size; x++)
                    {
                        float v = (h[y * Size + x] - lo) / span;
                        // light from the upper left: the slope against the neighbour gives the relief
                        float dx = h[y * Size + Mathf.Min(Size - 1, x + 1)] - h[y * Size + Mathf.Max(0, x - 1)];
                        float dy = h[Mathf.Min(Size - 1, y + 1) * Size + x] - h[Mathf.Max(0, y - 1) * Size + x];
                        float shade = Mathf.Clamp(1f + (dx + dy) / span * 2.2f, 0.55f, 1.4f);
                        Color c = v < 0.5f ? Color.Lerp(new Color(0.10f, 0.22f, 0.30f), new Color(0.30f, 0.48f, 0.28f), v * 2f) : Color.Lerp(new Color(0.30f, 0.48f, 0.28f), new Color(0.88f, 0.84f, 0.70f), (v - 0.5f) * 2f);
                        o[y * Size + x] = new Color32((byte)Mathf.Clamp(c.r * shade * 255f, 0f, 255f), (byte)Mathf.Clamp(c.g * shade * 255f, 0f, 255f), (byte)Mathf.Clamp(c.b * shade * 255f, 0f, 255f), 255);
                    }
                Texture2D t = new Texture2D(Size, Size, TextureFormat.RGBA32, false);
                t.hideFlags = HideFlags.HideAndDontSave;
                t.filterMode = FilterMode.Bilinear;
                t.SetPixels32(o);
                t.Apply(false);
                try { Directory.CreateDirectory(Dir); File.WriteAllBytes(CacheFile(d), t.EncodeToPNG()); } catch (Exception ex) { BtcLog.Warn("ReplayPreview", "cache write: " + ex.Message); }
                return t;
            }
            catch (Exception ex) { BtcLog.Warn("ReplayPreview", "build: " + ex.Message); return null; }
            finally { UnityEngine.Object.Destroy(src); }
        }

        /// <summary>Forget everything (the list was refreshed, files may have changed).</summary>
        internal static void Reset()
        {
            Failed.Clear();
        }
    }
}
