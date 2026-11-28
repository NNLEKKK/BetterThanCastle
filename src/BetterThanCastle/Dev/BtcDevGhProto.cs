#if DEV
using UnityEngine;
using UnityEngine.UI;

namespace BetterThanCastle.Dev
{
    /// DEV: C# copy of the start of the Lua left rail (Score, Blueprints, New Task) next to "Menu: Taskbar (Lua)", to check BtcGhKit
    /// against the vanilla dump (cmd ghproto, then uidump Menu:_BtcTaskbar proto).
    internal static class BtcDevGhProto
    {
        internal static void Run()
        {
            Transform lua = null;
            foreach (RectTransform rt in Resources.FindObjectsOfTypeAll<RectTransform>())
                if (rt != null && rt.gameObject.scene.IsValid() && rt.name == "Menu: Taskbar (Lua)") { lua = rt; break; }
            if (lua == null) { BtcLog.Warn("GhProto", "no Lua taskbar"); return; }
            RectTransform src = (RectTransform)lua;
            GameObject root = new GameObject("Menu: BtcTaskbar", typeof(RectTransform));
            RectTransform r = (RectTransform)root.transform;
            r.SetParent(src.parent, false);
            r.anchorMin = src.anchorMin; r.anchorMax = src.anchorMax; r.pivot = src.pivot; r.anchoredPosition = src.anchoredPosition; r.sizeDelta = src.sizeDelta;
            RectTransform rail = BtcGhKit.Make(Brix.External.Factories.Layouts.Vertical, r, "");
            BtcGhKit.Rect(rail, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 0.5f), Vector2.zero, new Vector2(48f, 0f));
            BtcGhKit.Img(rail, null, new Color32(0, 0, 0, 192), false);
            BtcGhKit.Le(rail, -1f, -1f, -1f, -1f, -1f, -1f, true);
            BtcGhKit.Group(rail, 0, 0, 0, 0, 0f, TextAnchor.UpperLeft);
            BtcGhButton b;
            BtcGhKit.RailButton(rail, BtcGhKit.Icon("UI_Cup"), BtcGhKit.Yellow, "Score", "", BtcGhKit.Yellow, out b);
            BtcGhKit.RailButton(rail, BtcGhKit.Icon("UI_SelectBlueprints"), new Color32(127, 127, 127, 255), "Blueprints", "", new Color32(255, 0, 100, 255), out b);
            BtcGhKit.RailButton(rail, BtcGhKit.Icon("UI_Pick"), new Color32(255, 0, 100, 255), "New Task", "RightBracket", new Color32(255, 0, 100, 255), out b);
            Canvas.ForceUpdateCanvases();
            BtcLog.Info("GhProto", "built under " + src.parent.name);
        }

        /// cmd gh open <Side> | gh blocks | gh structures | gh newtask | gh key <action>: drive the C# game menu like a player.
        internal static void Cmd(string[] p)
        {
            string a = p.Length > 1 ? p[1] : "";
            if (a == "open" && p.Length > 2) BtcGhLeft.SetOpen((BtcGhLeft.Side)System.Enum.Parse(typeof(BtcGhLeft.Side), p[2], true));
            else if (a == "blocks" && BtcGhModes.BlocksMenu != null) BtcGhModes.BlocksMenu();
            else if (a == "structures" && BtcGhModes.StructuresMenu != null) BtcGhModes.StructuresMenu();
            else if (a == "newtask" && BtcGhModes.NewTask != null) BtcGhModes.NewTask();
            else if (a == "bp") BtcGhLeft.OnBlueprints();
            else if (a == "settings") BtcGhRightBar.Toggle(1);
            else if (a == "allowdebug") Brix.Engine.Architecte.allowDebugMenu = p.Length < 3 || p[2] != "0";
            else if (a == "editor") BtcGhRightBar.Toggle(3);
            else if (a == "movie") BtcGhRightBar.Toggle(4);
            else if (a == "devrun" && p.Length > 2) BtcGhDev.Run(string.Join(" ", p, 2, p.Length - 2));
            else if (a == "devdo" && p.Length > 2) BtcGhDev.Do(string.Join(" ", p, 2, p.Length - 2), "lab action", true);
            else if (a == "devexp") { if (BtcEditorShare.Remote) BtcEditorShare.SendAct(BtcEditorShare.KindExplosion, "", "explosion", null, null, Brix.Input.CameraScript.Target, 4f); BtcLog.Info("GhProto", "devexp remote=" + BtcEditorShare.Remote); }
            else if (a == "devfac" && p.Length > 2) { BtcGhDev.Run("__btcFaction = __btcFactions[" + p[2] + "]"); BtcLog.Info("GhProto", "devfac " + BtcGhDev.Eval("__btcFaction.name")); }
            else if (a == "deveval" && p.Length > 2) BtcLog.Info("GhProto", "deveval [" + BtcGhDev.Eval(string.Join(" ", p, 2, p.Length - 2)) + "]");
            else if (a == "rule" && p.Length > 3) { float[] v = HarmonyLib.Traverse.Create(typeof(BtcGameplay)).Field("V").GetValue<float[]>(); int i = HarmonyLib.Traverse.Create(typeof(BtcGameplay)).Method("Idx", p[2]).GetValue<int>(); if (v != null && i >= 0) { v[i] = float.Parse(p[3], System.Globalization.CultureInfo.InvariantCulture); BtcGameplay.Refresh(); } BtcLog.Info("GhProto", "rule " + p[2] + " idx " + i + " editorOn=" + BtcGameplay.EditorPanelOn + " allowed=" + BtcGhDev.Allowed); }
            else if (a == "devcount") BtcLog.Info("GhProto", "devcount bricktrons=" + UnityEngine.Object.FindObjectsOfType<Brix.Game.AI.Occupation>().Length + " minitrons=" + UnityEngine.Object.FindObjectsOfType<Brix.Game.AI.Locomotion4>().Length);
            BtcLog.Info("GhProto", "gh " + a + " -> sel=" + (BtcGhLeft.Selected != null ? BtcGhLeft.Selected.GetType().Name : "null") + " held=" + Brix.Input.Picking.BlueprintBeingPlaced());
        }
    }
}
#endif
