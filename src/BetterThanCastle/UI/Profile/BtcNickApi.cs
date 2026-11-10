using System;
using System.Collections.Generic;
using UnityEngine;

namespace BetterThanCastle
{
    /// <summary>
    /// Clean surface of the legacy nick engine (BtcNick: parsing, colour tags, length limit, gradients, animation, lobby push) for the new Profile tab.
    /// The engine itself stays in UI/BtcNick.cs; the tab never touches its private state directly.
    /// </summary>
    public static partial class BtcNick
    {
        private static bool _styleOn = true;

        /// <summary>Master switch of the Profile tab: off = the nick goes out plain (no colour tags, no animation).</summary>
        internal static bool StyleOn { get { return _styleOn; } }

        internal static void SetStyleOn(bool on)
        {
            EnsureReady();
            if (_styleOn == on) return;
            _styleOn = on;
            BtcMenuState.SetBool("profile:on", on);
            _animCur = string.Empty;
            _animKFor = -1;
            BtcAvatar.Bump();
            BtcPlate.Resend();
            bool inLobby;
            if (CanEdit(out inLobby))
            {
                string n = NameFor(Active());
                if (n.Length > 0) SetName(n);
            }
        }

        internal static void EnsureReady()
        {
            if (!_loaded) Load();
            if (!_init)
            {
                _init = true;
                _text = SlotText(_slot);
                SyncFromText();
                _focus = false;
            }
        }

        // ---- state readers
        internal static int Slot { get { return _slot; } }
        internal static int Limit { get { return Lim; } }
        internal static int ActiveSlot { get { return Active(); } }
        internal static string PlainText { get { return _plain; } }
        internal static int[] Colors { get { return _cols; } }
        internal static string Tags { get { return _text ?? string.Empty; } }
        internal static int UsedChars { get { Compose(_plain, _cols, Lim); return _used; } }
        internal static bool EditAllowed(out bool inLobby) { return CanEdit(out inLobby); }
        internal static bool AutoApply { get { return _auto; } set { if (_auto != value) { _auto = value; SaveCfg(); } } }
        internal static bool KeepForGood { get { return _save; } set { if (_save != value) { _save = value; SaveCfg(); } } }
        internal static IList<string> History { get { return _hist; } }
        internal static bool Pending { get { return _pend; } set { _pend = value; } }
        internal static string AppliedTags(int slot) { return _nick[slot]; }

        /// <summary>True when the edited nick differs from the one that is applied for the open slot.</summary>
        internal static bool Dirty { get { return Clean(_text, Lim) != _nick[_slot]; } }

        /// <summary>The nick as the lobby shows it right now (animation frame when it runs), with colour tags.</summary>
        internal static string LiveTags(bool inLobby)
        {
            if (_styleOn && _anim && inLobby && _slot == 0 && Active() == 0 && _animCur.Length > 0) return _animCur;
            return _styleOn ? (_text ?? string.Empty) : Plain(_text);
        }

        internal static string Message { get { return (_msg.Length > 0 && Time.unscaledTime < _msgUntil) ? _msg : null; } }

        // ---- editing
        internal static void SelectSlot(int n) { if (n != _slot) SwitchSlot(n); }

        /// <summary>Replaces the visible text; colours of untouched characters stay (common prefix / suffix are kept, inserted characters take the colour of their left neighbour).</summary>
        internal static void SetPlain(string np)
        {
            if (np == null) np = string.Empty;
            string op = _plain;
            if (np == op) return;
            int[] oc = _cols;
            int p = 0;
            int max = Math.Min(op.Length, np.Length);
            while (p < max && op[p] == np[p]) p++;
            int s = 0;
            while (s < max - p && op[op.Length - 1 - s] == np[np.Length - 1 - s]) s++;
            int[] nc = new int[np.Length];
            int fill = p > 0 ? oc[p - 1] : (op.Length > 0 && oc.Length > 0 ? oc[0] : -1);
            for (int i = 0; i < np.Length; i++)
            {
                if (i < p) nc[i] = oc[i];
                else if (i >= np.Length - s) nc[i] = oc[op.Length - (np.Length - i)];
                else nc[i] = fill;
            }
            _plain = np;
            _cols = nc;
            ClampRange();
            SyncToText();
            _pend = true;
        }

        /// <summary>Snapshot of the editable nick for undo.</summary>
        internal static string[] Snapshot()
        {
            string[] r = new string[2];
            r[0] = _plain;
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            for (int i = 0; i < _cols.Length; i++) { if (i > 0) sb.Append(','); sb.Append(_cols[i]); }
            r[1] = sb.ToString();
            return r;
        }

        internal static void Restore(string[] snap)
        {
            if (snap == null || snap.Length < 2) return;
            _plain = snap[0];
            string[] parts = snap[1].Length == 0 ? new string[0] : snap[1].Split(',');
            int[] c = new int[_plain.Length];
            for (int i = 0; i < c.Length; i++)
            {
                int v;
                c[i] = (i < parts.Length && int.TryParse(parts[i], out v)) ? v : -1;
            }
            _cols = c;
            ClampRange();
            SyncToText();
            _pend = true;
        }

        internal static void SetTagsRaw(string tags)
        {
            _text = tags ?? string.Empty;
            SyncFromText();
            _pend = true;
        }

        internal static void Paint(int from, int to, int rgb) { PaintRange(from, to, rgb); }
        internal static void PaintGradient(int from, int to) { ApplyGradient(from, to); }
        internal static void StripColors()
        {
            for (int i = 0; i < _cols.Length; i++) _cols[i] = -1;
            SyncToText();
            _pend = true;
        }

        internal static void Revert()
        {
            _text = SlotText(_slot);
            SyncFromText();
        }

        /// <summary>Applies the edited nick (to the lobby / settings when the open slot is the active one).</summary>
        internal static void ApplyNow() { Apply(); }

        // ---- gradients
        internal static int PresetCount { get { return PName.Length; } }
        internal static string PresetName(int i) { return PName[i]; }
        internal static int[] PresetStops(int i) { return PStops()[i]; }
        internal static int GradSel { get { return _gradSel; } set { if (_gradSel != value) { _gradSel = value; SaveCfg(); } } }
        internal static bool Reverse { get { return _rev; } set { if (_rev != value) { _rev = value; SaveCfg(); } } }
        internal static float Spread { get { return _spread; } set { value = Mathf.Clamp(Mathf.Round(value * 2f) / 2f, 1f, 6f); if (Mathf.Abs(_spread - value) > 0.001f) { _spread = value; SaveCfg(); } } }
        internal static int[] CurrentStops() { CurStops(); return _stops; }
        internal static bool CurrentLoop { get { CurStops(); return _loop; } }
        internal static int CustomCount { get { return _cust.Count; } }
        internal static int CustomStop(int i) { return _cust[i]; }
        internal static void SetCustomStops(IList<int> st)
        {
            _cust.Clear();
            for (int i = 0; i < st.Count && i < 6; i++) _cust.Add(st[i]);
            _custArr = null;
            SaveCfg();
        }
        internal static string[] PaletteHex { get { return Palette; } }
        internal static int ColorOfGradient(int[] stops, float t) { return Lin(stops, t); }
        internal static bool HexToRgb(string s, out int c) { return TryHex(s, out c); }

        // ---- profile sets (whole nick state as text lines)
        internal static string ExportState()
        {
            EnsureReady();
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            sb.Append("nick=").Append(_nick[0]).Append("\n");
            sb.Append("snick=").Append(_nick[1]).Append("\n");
            sb.Append("grad=").Append((_gradSel >= 0 && _gradSel < PName.Length) ? PName[_gradSel] : "custom").Append("\n");
            System.Text.StringBuilder cs = new System.Text.StringBuilder();
            for (int i = 0; i < _cust.Count; i++) { if (i > 0) cs.Append(','); cs.Append(_cust[i].ToString("x6")); }
            sb.Append("custom=").Append(cs).Append("\n");
            sb.Append("rev=").Append(_rev ? "1" : "0").Append("\n");
            sb.Append("spread=").Append(_spread.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture)).Append("\n");
            sb.Append("anim=").Append(_anim ? "1" : "0").Append("\n");
            sb.Append("aspeed=").Append(_aSpeed.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture)).Append("\n");
            sb.Append("arate=").Append(_aRate.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture)).Append("\n");
            sb.Append("fx=").Append(BtcFx.Mode).Append("\n");
            return sb.ToString();
        }

        internal static void ImportState(IDictionary<string, string> kv)
        {
            EnsureReady();
            string v;
            if (kv.TryGetValue("nick", out v) && v.Length > 0) _nick[0] = Clean(v, 999);
            if (kv.TryGetValue("snick", out v)) _nick[1] = Clean(v, 30);
            if (kv.TryGetValue("custom", out v))
            {
                _cust.Clear();
                foreach (string p in v.Split(','))
                {
                    int c;
                    if (_cust.Count < 6 && TryHex(p.Trim(), out c)) _cust.Add(c);
                }
                _custArr = null;
            }
            if (kv.TryGetValue("grad", out v))
            {
                _gradSel = -1;
                for (int j = 0; j < PName.Length; j++) if (PName[j] == v) _gradSel = j;
            }
            if (kv.TryGetValue("rev", out v)) _rev = v == "1";
            float f;
            if (kv.TryGetValue("spread", out v) && float.TryParse(v, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out f)) _spread = Mathf.Clamp(f, 1f, 6f);
            if (kv.TryGetValue("aspeed", out v) && float.TryParse(v, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out f)) _aSpeed = Mathf.Clamp(f, 0.05f, 2f);
            if (kv.TryGetValue("arate", out v) && float.TryParse(v, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out f)) _aRate = Mathf.Clamp(Mathf.Round(f), 1f, 10f);
            if (kv.TryGetValue("anim", out v)) { _anim = v == "1"; _animCur = string.Empty; }
            int m;
            if (kv.TryGetValue("fx", out v) && int.TryParse(v, out m)) BtcFx.Mode = m;
            _animKFor = -1;
            _text = SlotText(_slot);
            SyncFromText();
            SaveCfg();
            bool inLobby;
            if (CanEdit(out inLobby))
            {
                string n = NameFor(Active());
                if (n.Length > 0) SetName(n);
            }
        }

        // ---- animation
        internal static bool AnimOn
        {
            get { return _anim; }
            set
            {
                if (_anim == value) return;
                _anim = value;
                _animCur = string.Empty;
                _animKFor = -1;
                SaveCfg();
                bool inLobby;
                if (!_anim && CanEdit(out inLobby) && inLobby)
                {
                    string t = NameFor(Active());
                    if (t.Length > 0) PushLobby(t);
                }
            }
        }
        internal static float AnimSpeed { get { return _aSpeed; } set { value = Mathf.Clamp(value, 0.05f, 2f); if (Mathf.Abs(_aSpeed - value) > 0.0005f) { _aSpeed = value; SaveCfg(); } } }
        internal static float AnimRate { get { return _aRate; } set { value = Mathf.Clamp(Mathf.Round(value), 1f, 10f); if (_aRate != value) { _aRate = value; SaveCfg(); } } }
        internal static float AnimEffectiveRate { get { return EffRate(); } }
    }
}
