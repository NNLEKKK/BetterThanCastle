#if DEV
using System.Collections;
using System.Diagnostics;
using UnityEngine;

namespace BetterThanCastle
{
    /// DEV cmd "nrec seg <tag>": splits the frame by timestamps (no native profiler in the player):
    /// upd = our Update tick -> lateEnd (LateUpdate of this component), lateEnd -> willRender (canvas pre-render), willRender -> cam0
    /// (first camera pre-cull), cams (first pre-cull -> last post-render), cams -> gui (first OnGUI), gui -> eof (all OnGUI +
    /// overlay canvases), eof -> next upd (present, physics fixed steps, input, early update).
    internal sealed class BtcDevSeg : MonoBehaviour
    {
        private static BtcDevSeg _inst;
        private static long _tUpd, _tLate, _tWr, _tCam0, _tCamN, _tGui, _tEof;
        private static readonly double[] Sum = new double[7];
        private static int _n;
        private static readonly double K = 1000.0 / Stopwatch.Frequency;

        internal static void Ensure()
        {
            if (_inst != null) return;
            GameObject g = new GameObject("BtcDevSeg");
            DontDestroyOnLoad(g);
            _inst = g.AddComponent<BtcDevSeg>();
            Camera.onPreCull += c => { if (_tCam0 == 0) _tCam0 = Stopwatch.GetTimestamp(); };
            Camera.onPostRender += c => { _tCamN = Stopwatch.GetTimestamp(); };
            Canvas.willRenderCanvases += () => { if (_tWr == 0) _tWr = Stopwatch.GetTimestamp(); };
            _inst.StartCoroutine(_inst.Eof());
        }

        internal static void OnUpdate()
        {
            long now = Stopwatch.GetTimestamp();
            if (_tUpd != 0 && _tLate != 0 && _tEof != 0)
            {
                long wr = _tWr != 0 ? _tWr : _tLate, c0 = _tCam0 != 0 ? _tCam0 : wr, cn = _tCamN != 0 ? _tCamN : c0, gui = _tGui != 0 ? _tGui : cn;
                Sum[0] += (_tLate - _tUpd) * K; Sum[1] += (wr - _tLate) * K; Sum[2] += (c0 - wr) * K; Sum[3] += (cn - c0) * K;
                Sum[4] += (gui - cn) * K; Sum[5] += (_tEof - gui) * K; Sum[6] += (now - _tEof) * K; _n++;
            }
            _tUpd = now; _tLate = _tWr = _tCam0 = _tCamN = _tGui = _tEof = 0;
        }

        private void LateUpdate() { _tLate = Stopwatch.GetTimestamp(); }
        private void OnGUI() { if (_tGui == 0) _tGui = Stopwatch.GetTimestamp(); }

        private IEnumerator Eof()
        {
            WaitForEndOfFrame w = new WaitForEndOfFrame();
            while (true) { yield return w; _tEof = Stopwatch.GetTimestamp(); }
        }

        internal static string Dump()
        {
            string[] names = { "upd>late", "late>willRender", "willRender>cam0", "cams", "cams>gui", "gui>eof", "eof>nextUpd" };
            System.Text.StringBuilder sb = new System.Text.StringBuilder("seg n=" + _n);
            for (int i = 0; i < 7; i++) { sb.Append(' ').Append(names[i]).Append('=').Append(_n > 0 ? (Sum[i] / _n).ToString("0.00") : "0"); Sum[i] = 0; }
            _n = 0;
            return sb.ToString();
        }
    }
}
#endif
