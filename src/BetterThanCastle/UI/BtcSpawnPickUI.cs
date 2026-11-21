using System;
using System.Collections.Generic;
using System.Text;
using Brix.Network;
using UnityEngine;

namespace BetterThanCastle
{
    /// <summary>
    /// Lobby Map tab: start position selection (BtcSpawnPick). Host clicks a slot then a yellow start marker;
    /// clients press "Request" and click a marker. Called from BtcSymmetry.Draw (IMGUI, menu only).
    /// </summary>
    internal static class BtcSpawnPickUI
    {
        private sealed class Slot
        {
            internal int faction;
            internal string names;
            internal bool mine;
        }

        private static readonly List<Slot> _slots = new List<Slot>();
        private static float _slotsAt;
        private static int _sel = -1;
        private static bool _reqMode;

        private static string T(string en, string ru) { return BtcSymmetry.T(en, ru); }

        private static void RefreshSlots()
        {
            if (Time.unscaledTime < _slotsAt) return;
            _slotsAt = Time.unscaledTime + 0.5f;
            _slots.Clear();
            UNetManager m = UNetManager.instance;
            if (m == null || m.LobbyPlayerList == null) return;
            for (int i = 0; i < m.LobbyPlayerList.Count; i++)
            {
                LobbyPlayer p = m.LobbyPlayerList[i];
                if (p == null) continue;
                Slot s = null;
                for (int j = 0; j < _slots.Count; j++) if (_slots[j].faction == p.factionNumber) { s = _slots[j]; break; }
                if (s == null)
                {
                    s = new Slot { faction = p.factionNumber, names = string.Empty };
                    _slots.Add(s);
                }
                string n = string.IsNullOrEmpty(p.playerName) ? "?" : p.playerName;
                if (n.Length > 24) n = n.Substring(0, 24);
                s.names = s.names.Length == 0 ? n : s.names + ", " + n;
                if (p.isLocalPlayer) s.mine = true;
            }
            _slots.Sort((a, b) => a.faction.CompareTo(b.faction));
        }

        internal static void Draw(Event e, ref float y, float w, float rowH, int fs, Color old)
        {
            if (!BtcNetworkGate.Enabled) return;
            try { DrawInner(e, ref y, w, rowH, fs, old); }
            catch (Exception ex) { BtcLog.Error("SpawnPick", "UI: " + ex); }
        }

        private static void DrawInner(Event e, ref float y, float w, float rowH, int fs, Color old)
        {
            BtcSymmetry.Styles(fs);
            RefreshSlots();
            bool host = BtcGameplay.CanEdit;
            bool client = !host && BtcGameplay.InLobby && BtcGameplay.InSession;
            GUI.Label(new Rect(fs, y, w - fs * 2f, rowH), T("Start positions", "Стартовые позиции"), BtcSymmetry._label);
            y += rowH;
            BtcSymmetry.Note(ref y, w, fs, host
                ? T("Pick a slot, then click a yellow start point. Unassigned slots start where the game decides (vanilla). Original Conquest and Sandbox only.",
                    "Выберите слот, затем кликните по жёлтой стартовой точке. Неназначенные слоты стартуют как в ванили. Только оригинальные Conquest и Sandbox.")
                : T("The host assigns start points. Press Request and click a yellow point to ask for it.",
                    "Стартовые точки назначает хост. Нажмите «Запросить» и кликните по жёлтой точке."));

            Texture2D tex;
            int minX, minZ, pw, ph;
            List<float[]> marks;
            bool pv = BtcSymmetry.SpawnPreview(out tex, out minX, out minZ, out pw, out ph, out marks);
            if (pv)
            {
                float maxW = w - fs * 2f;
                float maxH = rowH * 8f;
                float sc = Mathf.Min(maxW / pw, maxH / ph);
                Rect r = new Rect(fs, y, pw * sc, ph * sc);
                bool picking = (host && _sel >= 0) || (client && _reqMode);
                if (picking && e.type == EventType.MouseDown && e.button == 0 && r.Contains(e.mousePosition) && !BtcHotkeys.UiClickSwallowed)
                {
                    float[] best = null;
                    float bd = (fs * 1.2f) * (fs * 1.2f);
                    for (int i = 0; i < marks.Count; i++)
                    {
                        float[] mk = marks[i];
                        if (mk[2] != 1f) continue;
                        Vector2 sp = ToScreen(r, mk, minX, minZ, pw, ph);
                        float d = (sp - e.mousePosition).sqrMagnitude;
                        if (d < bd) { bd = d; best = mk; }
                    }
                    if (best != null)
                    {
                        Vector2 pos = new Vector2(best[0], best[1]);
                        if (host) { BtcSpawnPick.HostAssign(_sel, pos); _sel = -1; }
                        else { BtcSpawnPick.SendRequest(pos); _reqMode = false; }
                    }
                    e.Use();
                }
                if (e.type == EventType.Repaint)
                {
                    BtcSymmetry.Line(r, new Color(0.08f, 0.08f, 0.08f, 1f), old);
                    GUI.DrawTexture(r, tex, ScaleMode.StretchToFill, true);
                    for (int i = 0; i < marks.Count; i++)
                    {
                        float[] mk = marks[i];
                        if (mk[2] != 1f && mk[2] != 2f) continue;
                        Vector2 sp = ToScreen(r, mk, minX, minZ, pw, ph);
                        float s = mk[2] == 1f ? 7f : 4f;
                        Color c = mk[2] == 1f ? new Color(1f, 0.9f, 0.2f, 1f) : new Color(0.3f, 0.9f, 1f, 0.8f);
                        BtcSymmetry.Line(new Rect(sp.x - s * 0.5f, sp.y - s * 0.5f, s, s), c, old);
                    }
                    DrawTags(r, BtcSpawnPick.Assign, minX, minZ, pw, ph, fs, new Color(0.55f, 1f, 0.55f, 1f), old, "");
                    DrawTags(r, BtcSpawnPick.Requests, minX, minZ, pw, ph, fs, new Color(0.5f, 0.85f, 1f, 1f), old, "?");
                }
                y += r.height + rowH * 0.2f;
            }
            else
            {
                BtcSymmetry.Note(ref y, w, fs, T("No map preview (no map selected or no heightmap): positions are shown as coordinates.", "Нет превью карты (карта не выбрана или нет heightmap): позиции показаны координатами."));
            }

            for (int i = 0; i < _slots.Count; i++)
            {
                Slot s = _slots[i];
                Vector2 a, q;
                bool has = BtcSpawnPick.Assign.TryGetValue(s.faction, out a);
                bool req = BtcSpawnPick.Requests.TryGetValue(s.faction, out q);
                string txt = "#" + (s.faction + 1) + "  " + s.names + ":  " + (has ? Fmt(a) : T("vanilla", "как в ванили")) + (req ? "   " + T("wants ", "просит ") + Fmt(q) : string.Empty);
                float bw = rowH * 3.2f;
                float right = w - fs;
                if (host)
                {
                    if (req)
                    {
                        right -= bw * 0.6f;
                        if (BtcSymmetry.Button(e, new Rect(right, y + rowH * 0.05f, bw * 0.6f - 4f, rowH * 0.9f), "X", new Color(1f, 0.45f, 0.45f, 1f), old)) BtcSpawnPick.HostReject(s.faction);
                        right -= bw * 0.6f;
                        if (BtcSymmetry.Button(e, new Rect(right, y + rowH * 0.05f, bw * 0.6f - 4f, rowH * 0.9f), "OK", BtcSymmetry.Accent, old)) BtcSpawnPick.HostApprove(s.faction);
                    }
                    if (has)
                    {
                        right -= bw;
                        if (BtcSymmetry.Button(e, new Rect(right, y + rowH * 0.05f, bw - 4f, rowH * 0.9f), T("Vanilla", "Ваниль"), Color.white, old)) BtcSpawnPick.HostAssign(s.faction, null);
                    }
                    right -= bw;
                    bool sel = _sel == s.faction;
                    if (BtcSymmetry.Button(e, new Rect(right, y + rowH * 0.05f, bw - 4f, rowH * 0.9f), sel ? T("[click map]", "[клик по карте]") : T("Pick", "Выбрать"), sel ? BtcSymmetry.Accent : Color.white, old))
                        _sel = sel ? -1 : s.faction;
                }
                else if (client && s.mine)
                {
                    if (req)
                    {
                        right -= bw;
                        if (BtcSymmetry.Button(e, new Rect(right, y + rowH * 0.05f, bw - 4f, rowH * 0.9f), T("Withdraw", "Отозвать"), Color.white, old)) BtcSpawnPick.SendRequest(null);
                    }
                    if (pv)
                    {
                        right -= bw;
                        if (BtcSymmetry.Button(e, new Rect(right, y + rowH * 0.05f, bw - 4f, rowH * 0.9f), _reqMode ? T("[click map]", "[клик по карте]") : T("Request", "Запросить"), _reqMode ? BtcSymmetry.Accent : Color.white, old))
                            _reqMode = !_reqMode;
                    }
                }
                GUI.Label(new Rect(fs, y, Mathf.Max(10f, right - fs - 4f), rowH), txt, BtcSymmetry._label);
                y += rowH;
            }
            if (host && (BtcSpawnPick.Assign.Count > 0 || BtcSpawnPick.Requests.Count > 0))
            {
                if (BtcSymmetry.Button(e, new Rect(fs, y + rowH * 0.05f, w * 0.4f, rowH * 0.9f), T("All slots: vanilla", "Все слоты: ваниль"), Color.white, old))
                {
                    _sel = -1;
                    BtcSpawnPick.HostClearAll();
                }
                y += rowH;
            }
            y += rowH * 0.2f;
        }

        private static Vector2 ToScreen(Rect r, float[] mk, int minX, int minZ, int pw, int ph)
        {
            return new Vector2(r.x + (mk[0] - minX + 0.5f) / pw * r.width, r.yMax - (mk[1] - minZ + 0.5f) / ph * r.height);
        }

        private static void DrawTags(Rect r, Dictionary<int, Vector2> d, int minX, int minZ, int pw, int ph, int fs, Color c, Color old, string suffix)
        {
            if (d.Count == 0) return;
            Color tc = BtcSymmetry._label.normal.textColor;
            BtcSymmetry._label.normal.textColor = c;
            float[] tmp = new float[3];
            foreach (KeyValuePair<int, Vector2> kv in d)
            {
                tmp[0] = kv.Value.x;
                tmp[1] = kv.Value.y;
                Vector2 sp = ToScreen(r, tmp, minX, minZ, pw, ph);
                GUI.Label(new Rect(sp.x + 5f, sp.y - fs, fs * 4f, fs * 1.6f), "#" + (kv.Key + 1) + suffix, BtcSymmetry._label);
            }
            BtcSymmetry._label.normal.textColor = tc;
        }

        private static string Fmt(Vector2 v)
        {
            return "(" + Mathf.RoundToInt(v.x) + ", " + Mathf.RoundToInt(v.y) + ")";
        }
    }
}
