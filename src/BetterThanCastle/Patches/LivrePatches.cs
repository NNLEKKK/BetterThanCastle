using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using Brix.Legacy;
using HarmonyLib;

namespace BetterThanCastle.Patches
{
    /// <summary>Livre.Lire(patron, emplacement): File.ReadAllText(path) -> BtcLivre.Read(path) so the mod can append pie menu DSL (game files stay untouched).</summary>
    [HarmonyPatch(typeof(Livre), "Lire", new[] { typeof(string), typeof(string) })]
    internal static class LivreLirePatch
    {
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            List<CodeInstruction> ins = new List<CodeInstruction>(instructions);
            MethodInfo from = AccessTools.Method(typeof(File), "ReadAllText", new[] { typeof(string) });
            MethodInfo to = AccessTools.Method(typeof(BtcLivre), "Read", new[] { typeof(string) });
            int n = 0;
            for (int i = 0; i < ins.Count; i++)
            {
                if ((ins[i].operand as MethodInfo) == from)
                {
                    ins[i].operand = to;
                    n++;
                }
            }
            if (n == 0) BtcLog.Error("Hook", "Livre.Lire: File.ReadAllText not found, rack filter menu not injected");
            return ins;
        }
    }

    public static class BtcLivre
    {
        public static string Read(string path)
        {
            string text = File.ReadAllText(path);
            try
            {
                string p = path != null ? path.Replace((char)92, '/') : string.Empty;
                bool crlf = text.IndexOf((char)13) >= 0;
                if (p.EndsWith("/PieMenu_Buttons.txt")) return Back(text.Replace("\r\n", "\n") + RackButtons(), crlf);
                if (p.EndsWith("/PieMenu_Menus.txt")) return Back(PatchMenus(text.Replace("\r\n", "\n")), crlf);
            }
            catch (Exception e) { BtcLog.Error("Livre", "inject failed: " + e); }
            return text;
        }

        private static string Back(string s, bool crlf) { return crlf ? s.Replace("\n", "\r\n") : s; }

        private static string T(string en, string ru) { return BtcLang.Russian ? ru : en; }

        private static string RackButtons()
        {
            string[] ru = { "Рыцарь", "Лучник", "Алебардист", "Арбалетчик", "Алхимик", "Геомант" };
            StringBuilder b = new StringBuilder(2048);
            b.Append("\n\nBUTTON RackKitHub\n\tName : ").Append(T("Rack filter", "Фильтр стойки"))
             .Append("\n\tCondition : Condition_Rack_Filter_Visible\n\tIcon : _Knight_Kit\n\tTooltip : ")
             .Append(T("Allow only one kit on this rack (kits already on it stay until taken)", "Разрешить на стойке только один набор (лежащие наборы остаются, пока их не заберут)")).Append('\n');
            b.Append("\nBUTTON RackKitAny\n\tName : ").Append(T("Any kit", "Любой набор"))
             .Append("\n\tEffect : Event_Rack_Filter_Any\n\tCondition : Condition_Rack_Filter_Not_Any\n\tIcon : UI_Check\n\tTooltip : ")
             .Append(T("No filter (vanilla behaviour)", "Без фильтра (как в оригинале)")).Append('\n');
            for (int i = 0; i < BtcRackFilter.KitNames.Length; i++)
            {
                string k = BtcRackFilter.KitNames[i];
                b.Append("\nBUTTON RackKit").Append(k).Append("\n\tName : ").Append(T(k + " kit only", ru[i] + ": только этот набор"))
                 .Append("\n\tEffect : Event_Rack_Filter_").Append(k).Append("\n\tCondition : Condition_Rack_Filter_Not_").Append(k)
                 .Append("\n\tIcon : ").Append(BtcRackFilter.KitIcons[i]).Append("\n\tTooltip : ")
                 .Append(T("Only " + k.ToLowerInvariant() + " kits can be stored on this rack", "На стойку можно класть только набор: " + ru[i])).Append('\n');
            }
            return b.ToString();
        }

        private static string PatchMenus(string text)
        {
            int m = text.IndexOf("MENU BuilderToolRack", StringComparison.Ordinal);
            string anchor = "Button: StoreInContainer\n\tHole\n";
            int a = m < 0 ? -1 : text.IndexOf(anchor, m, StringComparison.Ordinal);
            if (a < 0 || a - m > 1500)
            {
                BtcLog.Error("Livre", "MENU BuilderToolRack anchor not found, rack filter submenu not injected");
                return text;
            }
            StringBuilder b = new StringBuilder();
            b.Append("Button: StoreInContainer\n\tIf: Condition_Rack_Filter_Visible\n\t\tSubmenu: RackKitHub\n\t\t\tButton: RackKitAny\n");
            for (int i = 0; i < BtcRackFilter.KitNames.Length; i++) b.Append("\t\t\tButton: RackKit").Append(BtcRackFilter.KitNames[i]).Append('\n');
            b.Append("\t\tEnd\n\tElse:\n\t\tHole\n\tEnd\n");
            text = text.Substring(0, a) + b + text.Substring(a + anchor.Length);

            // nothing selected, hovering an own rack: the pie is the kit filter itself (vanilla shows New Task there)
            string hover = "\t\t\tElse: ToolRack\n\t\t\t\tMenu: New Task\n";
            int h = text.IndexOf(hover, StringComparison.Ordinal);
            if (h < 0)
            {
                BtcLog.Error("Livre", "MENU Game hover ToolRack anchor not found, rack filter on hover not injected");
                return text;
            }
            string repl = "\t\t\tElse: ToolRack\n\t\t\t\tIf: Condition_Rack_Filter_Visible\n\t\t\t\t\tMenu: RackKitHover\n\t\t\t\tElse:\n\t\t\t\t\tMenu: New Task\n\t\t\t\tEnd\n";
            StringBuilder hm = new StringBuilder("\nMENU RackKitHover\n\tButton: RackKitAny\n");
            for (int i = 0; i < BtcRackFilter.KitNames.Length; i++) hm.Append("\tButton: RackKit").Append(BtcRackFilter.KitNames[i]).Append('\n');
            hm.Append("\tHole\n");
            return text.Substring(0, h) + repl + text.Substring(h + hover.Length) + hm;
        }
    }
}
