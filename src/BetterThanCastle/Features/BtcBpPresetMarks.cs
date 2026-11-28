using System;
using System.Collections.Generic;
using Brix.Game;
using Brix.Game.Components;
using Brix.Input;
using UnityEngine;
using UnityEngine.Networking;

namespace BetterThanCastle
{
    /// Icons above own blue marks with a preset, only in build mode (the current task's input mode is "blueprint"), like the vanilla
    /// overheads of built stations: stockpile filter, rack kit, station queue (a kit = one icon, runs of the same order = one icon "xN") + loop.
    /// IMGUI Repaint only; marks are re-resolved twice a second, icon rows are cached per payload; no allocation per frame.
    internal static partial class BtcBpPreset
    {
        private sealed class Row
        {
            internal Sprite[] Icons;
            internal string[] Counts;
            internal bool Loop;
        }

        private struct Mark { internal Transform T; internal string Payload; }

        private static readonly Dictionary<string, Row> Rows = new Dictionary<string, Row>();
        private static readonly List<Mark> Marks = new List<Mark>();
        private static float _marksAt;
        private static bool _marksBroken;
        private static Sprite _loopIcon;
        private static GUIStyle _countStyle;
        private static readonly GUIContent _countGc = new GUIContent();
        private static readonly Color Back = new Color(0.08f, 0.08f, 0.12f, 0.72f);
        private static readonly Color LoopTint = new Color(1f, 0.35f, 0.3f, 1f);
        private const float Icon = 26f, Pad = 2f;

        private static bool BuildMode()
        {
            InputContextComponent c = UIGameObserver.GetCurrentProjectInputContext();
            return c != null && c.defaultInputMode == InputMode.blueprint;
        }

        private static void ResolveMarks()
        {
            Marks.Clear();
            User u = User.LocalUser;
            if (u == null || Presets.Count == 0) return;
            foreach (KeyValuePair<uint, string> kv in Presets)
            {
                GameObject go = ClientScene.FindLocalObject(new NetworkInstanceId(kv.Key));
                Blueprint b = go != null ? go.GetComponent<Blueprint>() : null;
                if (b == null || !ReferenceEquals(b.faction, u.faction) || string.IsNullOrEmpty(kv.Value)) continue;
                Marks.Add(new Mark { T = b.transform, Payload = kv.Value });
            }
        }

        internal static void DrawMarks()
        {
            if (Event.current.type != EventType.Repaint || !LocalOn || Presets.Count == 0 || !BuildMode()) return;
            Camera cam = Camera.main;
            if (cam == null) return;
            float now = Time.unscaledTime;
            if (now >= _marksAt) { _marksAt = now + 0.5f; ResolveMarks(); }
            for (int i = 0; i < Marks.Count; i++)
            {
                Mark m = Marks[i];
                if (m.T == null || !m.T.gameObject.activeInHierarchy) continue;
                Vector3 s = cam.WorldToScreenPoint(m.T.position + Vector3.up * 2.2f);
                if (s.z <= 0f || s.z > 160f || s.x < -100f || s.x > Screen.width + 100f || s.y < -50f || s.y > Screen.height + 50f) continue;
                Row r = RowOf(m.Payload);
                if (r == null || r.Icons.Length == 0) continue;
                DrawRow(r, s.x, Screen.height - s.y);
            }
        }

        private static void DrawRow(Row r, float cx, float y)
        {
            int n = r.Icons.Length + (r.Loop ? 1 : 0);
            float w = n * Icon + (n + 1) * Pad;
            Rect bg = new Rect(cx - w * 0.5f, y - Icon - Pad * 2f, w, Icon + Pad * 2f);
            Color keep = GUI.color;
            GUI.color = Back;
            GUI.DrawTexture(bg, Texture2D.whiteTexture);
            GUI.color = Color.white;
            float x = bg.x + Pad;
            for (int i = 0; i < r.Icons.Length; i++)
            {
                Rect ir = new Rect(x, bg.y + Pad, Icon, Icon);
                DrawSprite(ir, r.Icons[i]);
                if (r.Counts[i] != null)
                {
                    if (_countStyle == null)
                    {
                        _countStyle = new GUIStyle(GUI.skin.label) { fontSize = 11, fontStyle = FontStyle.Bold, alignment = TextAnchor.UpperRight };
                        _countStyle.normal.textColor = new Color(1f, 0.86f, 0.2f, 1f);
                    }
                    _countGc.text = r.Counts[i];
                    _countStyle.Draw(new Rect(ir.x - 4f, ir.y - 5f, Icon + 6f, 16f), _countGc, false, false, false, false);
                }
                x += Icon + Pad;
            }
            if (r.Loop)
            {
                if (_loopIcon == null) _loopIcon = BtcGhKit.Icon("_UI_Wheel");
                GUI.color = LoopTint;
                DrawSprite(new Rect(x, bg.y + Pad, Icon, Icon), _loopIcon);
            }
            GUI.color = keep;
        }

        private static void DrawSprite(Rect r, Sprite s)
        {
            if (s == null || s.texture == null) return;
            Rect t = s.textureRect;
            float tw = s.texture.width, th = s.texture.height;
            GUI.DrawTextureWithTexCoords(r, s.texture, new Rect(t.x / tw, t.y / th, t.width / tw, t.height / th));
        }

        private static Row RowOf(string p)
        {
            Row r;
            if (Rows.TryGetValue(p, out r)) return r;
            if (Rows.Count > 256) Rows.Clear();
            r = BuildRow(p);
            Rows[p] = r;
            return r;
        }

        private static Row BuildRow(string p)
        {
            List<Sprite> icons = new List<Sprite>();
            List<string> counts = new List<string>();
            bool loop = false;
            try
            {
                if (p[0] == 'S')
                {
                    string ic = p == "S*" ? "_Stockpiled_Brick" : p == "S-" ? "_Stockpile_Empty" : Patches.BtcBpPie.IconOfCode(p.Substring(2));
                    icons.Add(BtcGhKit.Icon(ic ?? "MultiWood")); counts.Add(null);
                }
                else if (p[0] == 'R')
                {
                    int kit;
                    if (int.TryParse(p.Substring(1), out kit) && kit >= 0 && kit < BtcRackFilter.KitIcons.Length) { icons.Add(BtcGhKit.Icon(BtcRackFilter.KitIcons[kit])); counts.Add(null); }
                }
                else if (p[0] == 'C')
                {
                    Craft c = ParseCraft(p);
                    loop = c.Loop;
                    // tokens: a whole kit (any station slot whose parts follow in order) or one recipe; equal neighbours stack
                    string lastKey = null;
                    int run = 0;
                    for (int i = 0; i < c.Q.Count;)
                    {
                        string key; Sprite ic; int len;
                        Token(c.Q, i, out key, out ic, out len);
                        i += len;
                        if (key == lastKey) { run++; counts[counts.Count - 1] = "x" + run; continue; }
                        if (icons.Count >= 8) { counts[counts.Count - 1] = (counts[counts.Count - 1] ?? "") + "+"; break; }
                        icons.Add(ic); counts.Add(null);
                        lastKey = key; run = 1;
                    }
                }
            }
            catch (Exception e) { BtcLog.Warn("BpPreset", "icon row: " + e.Message); }
            return new Row { Icons = icons.ToArray(), Counts = counts.ToArray(), Loop = loop };
        }

        private static void Token(List<string> q, int at, out string key, out Sprite icon, out int len)
        {
            for (int k = 0; k < StationNames.Length; k++)
            {
                Slot[] s = SlotsOf(k);
                for (int j = 0; j < s.Length; j++)
                {
                    RecipeInfo[] inf = s[j].Infos();
                    if (inf.Length < 2 || at + inf.Length > q.Count) continue;
                    bool all = true;
                    for (int x = 0; x < inf.Length; x++) if (inf[x] == null || inf[x].Creates == null || q[at + x] != inf[x].Creates.Name) { all = false; break; }
                    if (!all) continue;
                    key = "kit:" + s[j].Icon; icon = BtcGhKit.Icon(s[j].Icon); len = inf.Length;
                    return;
                }
            }
            key = q[at]; len = 1;
            RecipeInfo r = RecipeByName(q[at]);
            icon = r != null ? BtcGhPopup.RecipeIcon(r) : null;
        }
    }
}
