#if DEV
using Brix.NewUI.OverheadDisplay;
using UnityEngine;

namespace BetterThanCastle
{
    /// DEV cmd "ohead": overhead (icons over heads) cost lab. pan r period | pan off | showall 0/1 | plates p i.
    internal static class BtcDevOverhead
    {
        private static bool _pan;
        private static float _r, _period, _t0;
        private static Vector3 _base;

        internal static void Cmd(string[] p)
        {
            string op = p.Length > 1 ? p[1] : "info";
            float a = p.Length > 2 ? Parse(p[2], 0f) : 0f, b = p.Length > 3 ? Parse(p[3], 0f) : 0f;
            switch (op)
            {
                case "pan":
                    if (p.Length > 2 && p[2] == "off") { _pan = false; Brix.Input.CameraScript.Target = _base; break; }
                    if (!_pan) _base = Brix.Input.CameraScript.Target;
                    _pan = true; _r = a > 0f ? a : 12f; _period = b > 0f ? b : 6f; _t0 = Time.realtimeSinceStartup;
                    break;
                case "showall":
                    int n = 0;
                    foreach (OverheadDisplay d in Object.FindObjectsOfType<OverheadDisplay>())
                    {
                        if (d.NameTag == null || !d.NameTag.IsSpawned) continue;
                        d.ShowAllButton = a > 0.5f; d.ShowOrHide(); n++;
                    }
                    BtcLog.Info("Ohead", "showall " + a + " n=" + n);
                    break;
                case "dump": Dump(); BtcLog.Info("Ohead", "draw " + BtcOverheadDraw.Info()); BtcLog.Info("Ohead", BtcOverheadDraw.Check()); break;
                case "fast": BtcConfig.FastOverheads.Value = a > 0.5f; break;
                case "nosort": NoSort = a > 0.5f; break;
                case "trim":
                    {
                        int off = 0, tr = 0, act = 0;
                        foreach (OverheadDisplay d in Object.FindObjectsOfType<OverheadDisplay>())
                        {
                            foreach (Overhead l in d.GetComponentsInChildren<Overhead>(true))
                            {
                                CanvasGroup g = l.GetComponent<CanvasGroup>();
                                if (g != null && g.alpha <= 0f && l.gameObject.activeSelf == (a > 0.5f)) { l.gameObject.SetActive(a < 0.5f); off++; }
                            }
                            foreach (Transform t in d.GetComponentsInChildren<Transform>(false)) { act++; }
                            tr += d.GetComponentsInChildren<Transform>(true).Length;
                        }
                        BtcLog.Info("Ohead", "trim " + a + " lines toggled=" + off + " transforms all=" + tr + " active=" + act);
                    }
                    break;
                case "freeze": Frozen.Clear(); foreach (TransformConstraint tc in Object.FindObjectsOfType<TransformConstraint>()) if (tc.GetComponent<OverheadDisplay>() != null) { if (a > 0.5f) Frozen.Add(tc); tc.enabled = a < 0.5f; } break;
                case "camoff": { Camera c = GameObject.Find("BtcOverheadCam") != null ? GameObject.Find("BtcOverheadCam").GetComponent<Camera>() : null; if (c != null) c.gameObject.SetActive(a < 0.5f); } break;
                case "anim":
                    { int n2 = 0; foreach (OverheadDisplay d in Object.FindObjectsOfType<OverheadDisplay>()) foreach (Animator an in d.GetComponentsInChildren<Animator>(true)) { an.enabled = a > 0.5f; n2++; } BtcLog.Info("Ohead", "animators " + a + " n=" + n2); }
                    break;
                case "canvasoff":
                    foreach (OverheadDisplay d in Object.FindObjectsOfType<OverheadDisplay>()) { Canvas c = d.GetComponent<Canvas>(); if (c != null) c.enabled = a < 0.5f; }
                    break;
                case "nopix":
                    foreach (Canvas c in Object.FindObjectsOfType<Canvas>()) if (c.GetComponentInParent<OverheadDisplay>() != null || c.name.IndexOf("Overhead", System.StringComparison.OrdinalIgnoreCase) >= 0) c.pixelPerfect = a < 0.5f;
                    break;
                case "plates":
                    BtcUnitPlates.Load();
                    BtcUnitPlates.PlateScale = a; BtcUnitPlates.IconScale = b;
                    break;
            }
            BtcLog.Info("Ohead", op + " pan=" + _pan + " plates=" + BtcUnitPlates.PlateScale + "/" + BtcUnitPlates.IconScale);
        }

        internal static void Tick()
        {
            for (int i = 0; i < Frozen.Count; i++) if (Frozen[i] != null && Frozen[i].enabled) Frozen[i].enabled = false;
            if (!_pan) return;
            float w = (Time.realtimeSinceStartup - _t0) / _period * Mathf.PI * 2f;
            Brix.Input.CameraScript.Target = _base + new Vector3(Mathf.Cos(w) * _r, 0f, Mathf.Sin(w) * _r);
        }

        internal static bool NoSort, Freeze;
        private static readonly System.Collections.Generic.List<TransformConstraint> Frozen = new System.Collections.Generic.List<TransformConstraint>();

        private static void Dump()
        {
            OverheadDisplay[] ds = Object.FindObjectsOfType<OverheadDisplay>();
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            sb.Append("displays=").Append(ds.Length);
            if (ds.Length > 0)
            {
                Canvas root = ds[0].GetComponentInParent<Canvas>();
                if (root != null) root = root.rootCanvas;
                if (root != null) sb.Append(" root=").Append(root.name).Append(" mode=").Append(root.renderMode).Append(" pix=").Append(root.pixelPerfect).Append(" scale=").Append(root.scaleFactor)
                    .Append(" rootCanvases=").Append(root.GetComponentsInChildren<Canvas>(true).Length).Append(" rootGraphics=").Append(root.GetComponentsInChildren<UnityEngine.UI.Graphic>(true).Length);
                OverheadDisplay d = ds[0];
                sb.Append(" | one: canvases=").Append(d.GetComponentsInChildren<Canvas>(true).Length)
                    .Append(" graphics=").Append(d.GetComponentsInChildren<UnityEngine.UI.Graphic>(true).Length)
                    .Append(" texts=").Append(d.GetComponentsInChildren<UnityEngine.UI.Text>(true).Length)
                    .Append(" layouts=").Append(d.GetComponentsInChildren<UnityEngine.UI.LayoutGroup>(true).Length)
                    .Append(" fitters=").Append(d.GetComponentsInChildren<UnityEngine.UI.ContentSizeFitter>(true).Length)
                    .Append(" masks=").Append(d.GetComponentsInChildren<UnityEngine.UI.Mask>(true).Length + d.GetComponentsInChildren<UnityEngine.UI.RectMask2D>(true).Length)
                    .Append(" animators=").Append(d.GetComponentsInChildren<Animator>(true).Length)
                    .Append(" outlines=").Append(d.GetComponentsInChildren<UnityEngine.UI.Shadow>(true).Length);
                foreach (Canvas c in d.GetComponentsInChildren<Canvas>(true)) sb.Append(" [").Append(c.name).Append(" ov=").Append(c.overrideSorting).Append(" pix=").Append(c.pixelPerfect).Append(']');
            }
            BtcLog.Info("Ohead", "dump " + sb);
        }

        private static float Parse(string s, float d)
        {
            float v; return float.TryParse(s, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out v) ? v : d;
        }
    }

    [HarmonyLib.HarmonyPatch(typeof(OverheadDisplay), "BrixUpdate")]
    internal static class BtcDevOverheadNoSort
    {
        private static bool Prefix() { return !BtcDevOverhead.NoSort; }
    }


}
#endif
