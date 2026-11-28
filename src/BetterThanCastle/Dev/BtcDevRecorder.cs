#if DEV
using System.Text;
using UnityEngine;
using UnityEngine.Profiling;

namespace BetterThanCastle
{
    /// DEV cmd "nrec on|off|dump": native profiler sample timings via Profiling.Recorder (needs the engine to collect samples;
    /// isValid=false entries are reported as such). Averages per frame since "on".
    internal static class BtcDevRecorder
    {
        private static readonly string[] Names =
        {
            "Canvas.SendWillRenderCanvases", "Canvas.BuildBatch", "UGUI.Rendering.UpdateBatches", "Layout.PerformLayoutCalculation", "Canvas.RenderOverlays",
            "PostLateUpdate.PlayerUpdateCanvases", "PostLateUpdate.UpdateAllRenderers", "PostLateUpdate.FinishFrameRendering", "Camera.Render", "Culling",
            "Gfx.WaitForPresent", "Gfx.PresentFrame", "Physics.Simulate", "FixedUpdate.PhysicsFixedUpdate", "Update.ScriptRunBehaviourUpdate",
            "PreLateUpdate.DirectorUpdateAnimationBegin", "Animators.Update", "TransformChangedDispatch", "PostLateUpdate.UpdateRectTransform", "RectTransform.SendReapplyDrivenProperties",
            "PostLateUpdate.UpdateCustomRenderTextures", "EarlyUpdate.UpdateCanvasRectTransform", "UpdateRendererBoundingVolumes", "Render.OpaqueGeometry", "Render.TransparentGeometry"
        };
        private static Recorder[] _r;
        private static double[] _sum;
        private static int _frames;
        private static bool _on;

        internal static void Cmd(string[] p)
        {
            string op = p.Length > 1 ? p[1] : "dump";
            if (op == "on")
            {
                WrapCanvas();
                BtcDevSeg.Ensure();
                if (_r == null) { _r = new Recorder[Names.Length]; for (int i = 0; i < Names.Length; i++) _r[i] = Recorder.Get(Names[i]); }
                _sum = new double[Names.Length]; _frames = 0; _on = true;
                for (int i = 0; i < _r.Length; i++) _r[i].enabled = true;
            }
            else if (op == "off") _on = false;
            else Dump(p.Length > 2 ? p[2] : "rec");
        }

        // Canvas.willRenderCanvases = CanvasUpdateRegistry (layout + graphic mesh rebuilds) and friends: timed by wrapping the static delegate
        private static Canvas.WillRenderCanvases _orig;
        private static bool _wrapped;
        private static double _wrSum, _wrMax;
        private static void WrapCanvas()
        {
            if (_wrapped) return;
            System.Reflection.FieldInfo f = typeof(Canvas).GetField("willRenderCanvases", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public);
            if (f == null) { BtcLog.Warn("NRec", "willRenderCanvases field not found"); return; }
            _orig = f.GetValue(null) as Canvas.WillRenderCanvases;
            f.SetValue(null, new Canvas.WillRenderCanvases(Wrapped));
            _wrapped = true;
            BtcLog.Info("NRec", "canvas hook on, handlers=" + (_orig != null ? _orig.GetInvocationList().Length : 0));
        }

        private static void Wrapped()
        {
            long t0 = System.Diagnostics.Stopwatch.GetTimestamp();
            if (_orig != null) _orig();
            double ms = (System.Diagnostics.Stopwatch.GetTimestamp() - t0) * 1000.0 / System.Diagnostics.Stopwatch.Frequency;
            _wrSum += ms; if (ms > _wrMax) _wrMax = ms;
        }

        internal static void Tick()
        {
            if (!_on || _r == null) return;
            BtcDevSeg.OnUpdate();
            _frames++;
            for (int i = 0; i < _r.Length; i++) if (_r[i].isValid) _sum[i] += _r[i].elapsedNanoseconds / 1000000.0;
        }

        private static void Dump(string tag)
        {
            if (_r == null) { BtcLog.Info("NRec", tag + " not started"); return; }
            StringBuilder sb = new StringBuilder();
            sb.Append(tag).Append(" frames=").Append(_frames).Append(" fps=").Append((1f / Mathf.Max(0.0001f, Time.smoothDeltaTime)).ToString("0"))
                .Append(" willRenderCanvases avg=").Append(_frames > 0 ? (_wrSum / _frames).ToString("0.00") : "0").Append(" max=").Append(_wrMax.ToString("0.0"));
            int valid = 0;
            for (int i = 0; i < _r.Length; i++)
            {
                if (!_r[i].isValid) continue;
                valid++;
                sb.Append(" | ").Append(Names[i]).Append('=').Append(_frames > 0 ? (_sum[i] / _frames).ToString("0.00") : "0");
            }
            if (valid == 0) sb.Append(" (no native samples in this player)");
            BtcLog.Info("NRec", sb.ToString());
            BtcLog.Info("NRec", tag + " " + BtcDevSeg.Dump());
            _sum = new double[Names.Length]; _frames = 0; _wrSum = 0; _wrMax = 0;
        }
    }
}
#endif
