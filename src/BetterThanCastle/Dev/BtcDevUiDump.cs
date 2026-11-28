#if DEV
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

namespace BetterThanCastle.Dev
{
    /// DEV: structural dump of a uGUI subtree (rects, sprites, colours, texts, layout) for 1:1 checks of the C# game menu against
    /// the Lua one without screenshots. cmd `uidump <root name prefix> <tag>` -> BTClogs/uidump-<tag>.txt; diff two dumps.
    internal static class BtcDevUiDump
    {
        internal static void Run(string prefix, string tag)
        {
            StringBuilder sb = new StringBuilder();
            int roots = 0;
            foreach (RectTransform rt in Resources.FindObjectsOfTypeAll<RectTransform>())
            {
                if (rt == null || !rt.gameObject.scene.IsValid() || !rt.name.StartsWith(prefix)) continue;
                if (rt.parent != null && rt.parent.name.StartsWith(prefix)) continue;
                roots++;
                Node(sb, rt, 0);
            }
            string path = Path.Combine(BtcDevPaths.LogDir, "uidump-" + tag + ".txt");
            File.WriteAllText(path, sb.ToString());
            BtcLog.Info("UiDump", "'" + prefix + "' roots=" + roots + " lines=" + sb.Length + " -> " + path);
        }

        /// <summary>cmd `menutree <tag> [depth]`: names of every ACTIVE uGUI object under each canvas root, down to depth (default 4).</summary>
        internal static void Tree(string tag, int maxDepth)
        {
            StringBuilder sb = new StringBuilder();
            foreach (Canvas cv in Object.FindObjectsOfType<Canvas>())
            {
                if (cv == null || !cv.isRootCanvas) continue;
                Active(sb, cv.transform, 0, maxDepth);
            }
            string path = Path.Combine(BtcDevPaths.LogDir, "menutree-" + tag + ".txt");
            File.WriteAllText(path, sb.ToString());
            BtcLog.Info("UiDump", "menutree " + tag + " lines=" + sb.Length + " -> " + path);
        }

        private static void Active(StringBuilder sb, Transform t, int depth, int maxDepth)
        {
            if (!t.gameObject.activeInHierarchy) return;
            sb.Append(' ', depth * 2).Append(t.name).Append('\n');
            if (depth >= maxDepth) return;
            for (int i = 0; i < t.childCount; i++) Active(sb, t.GetChild(i), depth + 1, maxDepth);
        }

        private static void Node(StringBuilder sb, RectTransform rt, int depth)
        {
            if (depth > 40) return;
            sb.Append(' ', depth * 2).Append(rt.name).Append(rt.gameObject.activeSelf ? "" : " [off]")
              .Append(" a=").Append(V(rt.anchorMin)).Append('-').Append(V(rt.anchorMax)).Append(" p=").Append(V(rt.pivot))
              .Append(" pos=").Append(V(rt.anchoredPosition)).Append(" sd=").Append(V(rt.sizeDelta)).Append(" r=").Append(V(rt.rect.size));
            Image img = rt.GetComponent<Image>();
            if (img != null) sb.Append(" img(").Append(img.sprite != null ? img.sprite.name : "-").Append(' ').Append(C(img.color)).Append(' ').Append(img.type).Append(img.enabled ? "" : " off").Append(')');
            RawImage raw = rt.GetComponent<RawImage>();
            if (raw != null) sb.Append(" raw(").Append(raw.texture != null ? raw.texture.name : "-").Append(' ').Append(C(raw.color)).Append(')');
            Text tx = rt.GetComponent<Text>();
            if (tx != null) sb.Append(" txt(\"").Append(tx.text.Replace("\n", "\\n")).Append("\" ").Append(tx.font != null ? tx.font.name : "-").Append(' ').Append(tx.fontSize).Append(' ').Append(C(tx.color)).Append(' ').Append(tx.alignment).Append(')');
            LayoutElement le = rt.GetComponent<LayoutElement>();
            if (le != null) sb.Append(" le(").Append(le.minWidth).Append(',').Append(le.minHeight).Append(',').Append(le.preferredWidth).Append(',').Append(le.preferredHeight).Append(',').Append(le.flexibleWidth).Append(',').Append(le.flexibleHeight).Append(le.ignoreLayout ? ",ign" : "").Append(')');
            HorizontalOrVerticalLayoutGroup lg = rt.GetComponent<HorizontalOrVerticalLayoutGroup>();
            if (lg != null) sb.Append(' ').Append(lg is HorizontalLayoutGroup ? "hlg" : "vlg").Append("(pad ").Append(lg.padding.left).Append(',').Append(lg.padding.right).Append(',').Append(lg.padding.top).Append(',').Append(lg.padding.bottom).Append(" sp ").Append(lg.spacing).Append(' ').Append(lg.childAlignment).Append(')');
            GridLayoutGroup gg = rt.GetComponent<GridLayoutGroup>();
            if (gg != null) sb.Append(" grid(").Append(V(gg.cellSize)).Append(' ').Append(V(gg.spacing)).Append(')');
            sb.Append('\n');
            for (int i = 0; i < rt.childCount; i++)
            {
                RectTransform c = rt.GetChild(i) as RectTransform;
                if (c != null) Node(sb, c, depth + 1);
            }
        }

        private static string V(Vector2 v) { return v.x.ToString("0.#") + "," + v.y.ToString("0.#"); }
        private static string C(Color c) { return ((Color32)c).r + "," + ((Color32)c).g + "," + ((Color32)c).b + "," + ((Color32)c).a; }
    }
}
#endif
