using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Brix.Game;
using UnityEngine;

namespace BetterThanCastle
{
    /// <summary>
    /// Actions per minute of every player. Recording: each host-side [Command] (UNetCmdReplay.Run) adds one action to the faction of its first
    /// GameObject argument; the same command type of the same player within 150 ms is one action (drag-builds and group orders send bursts).
    /// The counts are kept in 5 s buckets and written into the timeline region ("A,RRGGBB,bucketSec,base36 digits"), so old replays simply have no
    /// wave. Playback: smoothed (triangular window ~30 s, cubic interpolation) and drawn as one line per player into a texture above the timeline.
    /// </summary>
    internal static class BtcApm
    {
        private const float Base = 2f, Merge = 0.1f, MaxChars = 2400f;

        private sealed class Series { internal string Col; internal List<int> B = new List<int>(); }

        private static readonly Dictionary<int, Dictionary<string, float>> LastAct = new Dictionary<int, Dictionary<string, float>>();

        /// <summary>Auto mark with a duration: Kind 0 idle (snowflake), 1 surge (flame), 2 personal peak.</summary>
        private struct Span { internal int Kind; internal float T0, T1, Val; }

        private sealed class Line
        {
            internal string Col, Name; internal Color C; internal float Bs; internal float[] V, VE; internal float Avg, Peak, EAvg = -1f;
            internal readonly List<Span> Spans = new List<Span>(), SpansE = new List<Span>();
        }

        private const float IdleSec = 20f;

        /// <summary>Shown series: 1 = APM (all presses that did something), 2 = EPM (no spam). Falls back to APM when a line has no EPM (command-count graph).</summary>
        private static float[] SeriesOf(Line l) { return BtcConfig.ReplayApmMode.Value == 2 && l.VE != null ? l.VE : l.V; }

        private static List<Span> SpansOf(Line l) { return BtcConfig.ReplayApmMode.Value == 2 && l.VE != null ? l.SpansE : l.Spans; }

        private static readonly Dictionary<int, Series> RecS = new Dictionary<int, Series>();
        private static readonly List<Line> Play = new List<Line>();
        private static bool _dirty = true;
        private static Texture2D _tex;
        private static int _texW = 1024, _texH = 96;
        private static float _scale, _total;
        private static Color32[] _px;
        private static float[] _col;

        // ------------------------------------------------------------------ recording
        internal static void OnRecordStart() { RecS.Clear(); }

        internal static void OnMatchStart() { LastAct.Clear(); }

        /// <summary>Server-side command body (BtcReplay.Active hook): counts one action for the player that owns the first GameObject argument.</summary>
        internal static void Note(string method, object[] args)
        {
            if (args == null) return;
            bool rec = BtcReplay.Mode == BtcReplay.State.Recording;
            if (!rec) return;   // command counts are only the fallback graph of replays recorded without the press counters
            Faction f = null;
            for (int i = 0; i < args.Length && f == null; i++)
            {
                GameObject go = args[i] as GameObject;
                if (go != null) f = Faction.GetFaction(go);
            }
            if (!BtcMatchStats.Tracks(f)) return;
            // one action = the first command of a burst: the same command type of the same player within 0.1 s of the burst start is merged (group orders send one per unit)
            Dictionary<string, float> last;
            if (!LastAct.TryGetValue(f.ID, out last)) { last = new Dictionary<string, float>(); LastAct[f.ID] = last; }
            float now = Time.unscaledTime, lt;
            if (last.TryGetValue(method, out lt) && now - lt < Merge) return;
            last[method] = now;
            if (!rec) return;
            float t = BtcReplay.RecTime;
            if (t < 0f) return;
            Series s;
            if (!RecS.TryGetValue(f.ID, out s)) { s = new Series { Col = ColorUtility.ToHtmlStringRGB(f.FactionColor) }; RecS[f.ID] = s; }
            int bk = (int)(t / Base);
            while (s.B.Count <= bk) s.B.Add(0);
            s.B[bk]++;
        }

        private const string Digits = "0123456789abcdefghijklmnopqrstuvwxyz";

        /// <summary>Tokens "A,col,bucketSec,digits" joined with ';', merged to fit about MaxChars.</summary>
        internal static string Payload()
        {
            int n = 0, buckets = 0;
            foreach (KeyValuePair<int, Series> kv in RecS) { n++; buckets = Mathf.Max(buckets, kv.Value.B.Count); }
            if (n == 0 || buckets == 0) return "";
            int k = Mathf.Max(1, Mathf.CeilToInt(buckets * n / MaxChars));
            float bs = Base * k;
            StringBuilder sb = new StringBuilder(1024);
            foreach (KeyValuePair<int, Series> kv in RecS)
            {
                List<int> b = kv.Value.B;
                if (sb.Length > 0) sb.Append(';');
                sb.Append("A,").Append(kv.Value.Col).Append(',').Append(bs.ToString("0.#", CultureInfo.InvariantCulture)).Append(',');
                for (int i = 0; i < buckets; i += k)
                {
                    int sum = 0;
                    for (int j = i; j < i + k && j < b.Count; j++) sum += b[j];
                    sb.Append(Digits[Mathf.Min(35, sum)]);
                }
            }
            BtcLog.Info("Apm", "payload " + sb.Length + " chars, " + n + " player(s), bucket " + bs + " s");
            return sb.ToString();
        }

        // ------------------------------------------------------------------ playback load
        internal static void ClearPlay() { Play.Clear(); _dirty = true; _clicksBuilt = false; }

        internal static bool Has { get { EnsureClicks(); return Play.Count > 0; } }

        private static bool _clicksBuilt;

        /// <summary>Playback: the graph comes from the press counters of the POV tracks (all players, keyboard + mouse, like StarCraft APM; the hover also shows EAPM without spam).
        /// Replays recorded before the counters existed fall back to the mouse button flags; without POV tracks the command counts stay.</summary>
        private static void EnsureClicks()
        {
            if (_clicksBuilt || BtcPovRec.Play.Count == 0) return;
            _clicksBuilt = true;
            bool anyAc = false;
            for (int i = 0; i < BtcPovRec.Play.Count && !anyAc; i++)
                for (int k = 0; k < BtcPovRec.Play[i].L.Count; k++) if (BtcPovRec.Play[i].L[k].Ac != 0) { anyAc = true; break; }
            List<Line> lines = new List<Line>();
            for (int i = 0; i < BtcPovRec.Play.Count; i++)
            {
                BtcPovRec.Track tr = BtcPovRec.Play[i];
                if (tr.L.Count < 2 || string.IsNullOrEmpty(tr.Col)) continue;
                string col = tr.Col.TrimStart('#').ToUpperInvariant();
                float dur = Mathf.Max(1f, tr.L[tr.L.Count - 1].T / 1000f);
                int n = (int)(dur / Base) + 1;
                float[] v = new float[n], ve = new float[n];
                int prev = 0, totE = 0;
                for (int k = 0; k < tr.L.Count; k++)
                {
                    int a, e;
                    if (anyAc) { a = tr.L[k].Ac & 15; e = (tr.L[k].Ac >> 4) & 15; }
                    else
                    {
                        int fl = tr.L[k].Fl & 3;
                        a = ((fl & 1) != 0 && (prev & 1) == 0 ? 1 : 0) + ((fl & 2) != 0 && (prev & 2) == 0 ? 1 : 0);
                        e = a;
                        prev = fl;
                    }
                    totE += e;
                    int bi = Mathf.Clamp((int)(tr.L[k].T / 1000f / Base), 0, n - 1);
                    if (a > 0) v[bi] += a * 60f / Base;
                    if (e > 0) ve[bi] += e * 60f / Base;
                }
                Line l = new Line { Col = col, Bs = Base, V = v, VE = anyAc ? ve : null, EAvg = totE * 60f / dur };
                Color c;
                l.C = ColorUtility.TryParseHtmlString("#" + col, out c) ? c : Color.white;
                l.C.a = 1f;
                Smooth(l);
                lines.Add(l);
            }
            if (lines.Count == 0) return;
            Play.Clear();
            Play.AddRange(lines);
            _dirty = true;
            { int sp = 0; for (int q = 0; q < lines.Count; q++) sp += lines[q].Spans.Count; BtcLog.Info("Apm", "graph from " + (anyAc ? "press counters" : "mouse button flags") + " of " + lines.Count + " player track(s), " + sp + " auto marks"); }
        }

        // ------------------------------------------------------------------ input counting (StarCraft style: every press of the local player)
        private static readonly float[] LastT = new float[340];
        private static readonly float[] DownT = new float[340];
        private static readonly List<int> Held = new List<int>();
        private static int _pA, _pE;

        /// <summary>Every frame while the POV recorder runs. Counts mouse left/right presses at once and key taps (held shorter than 0.35 s: held camera keys are movement, not actions;
        /// modifiers alone and typing into a text field are ignored). EAPM: the same key or button again within 0.15 s is spam.</summary>
        internal static void PollInput(float now)
        {
            if (Pending.Count > 0) ResolveTaps(now);
            if (UnityEngine.Input.GetMouseButtonDown(0)) { AddKey(320); Count(320, now); }
            if (UnityEngine.Input.GetMouseButtonDown(1)) { AddKey(321); Count(321, now); }
            // display only (not actions): middle / side mouse buttons, and modifier keys as down (code) and up (code + 1000) events
            for (int b = 2; b <= 4; b++) if (UnityEngine.Input.GetMouseButtonDown(b)) AddKey(320 + b);
            for (int i = 0; i < ModKeys.Length; i++)
            {
                KeyCode mk = (KeyCode)ModKeys[i];
                if (UnityEngine.Input.GetKeyDown(mk)) AddKey(ModKeys[i]);
                else if (UnityEngine.Input.GetKeyUp(mk)) AddKey(ModKeys[i] + 1000);
            }
            if (UnityEngine.Input.anyKeyDown)
                for (int k = 8; k <= 319; k++)
                {
                    if (k >= 300 && k <= 313) continue;   // num/caps/scroll lock, shift, ctrl, alt, command
                    if (UnityEngine.Input.GetKeyDown((KeyCode)k)) { DownT[k] = now; DownSeq[k] = _effectSeq; DownSel[k] = SelHash(); DownMode[k] = ModeNow(); Held.Add(k); }
                }
            for (int i = Held.Count - 1; i >= 0; i--)
            {
                int k = Held[i];
                if (UnityEngine.Input.GetKeyUp((KeyCode)k))
                {
                    if (now - DownT[k] < 0.35f && !TextFocus())
                    {
                        AddKey(k);   // shown in the replay
                        if (Pending.Count < 16) Pending.Add(new Tap { Key = k, Due = now + 0.25f });   // counted later, and only if it had an effect
                    }
                    Held.RemoveAt(i);
                }
                else if (!UnityEngine.Input.GetKey((KeyCode)k)) Held.RemoveAt(i);
            }
        }

        // ------------------------------------------------------------------ effect check: a key press is an action only if something happened because of it
        private struct Tap { internal int Key; internal float Due; }

        private static readonly List<Tap> Pending = new List<Tap>();
        private static readonly int[] DownSeq = new int[340], DownSel = new int[340], DownMode = new int[340];
        private static List<GameObject> SelBuf = new List<GameObject>();
        private static int _effectSeq;

        /// <summary>A key-bound game action ran: a menu shortcut (KeyBindingsUtility.RegisterAction wrapper) or an advanced hotkey (BtcHotkeys.ComboPressed).</summary>
        internal static void NoteEffect() { _effectSeq++; }

        private static int SelHash()
        {
            try
            {
                SelBuf.Clear();
                Brix.UI.GameSelector.UIGameSelector.GetSelectedEverything(ref SelBuf);
                int h = SelBuf.Count * 31;
                for (int i = 0; i < SelBuf.Count; i++) if (SelBuf[i] != null) h = h * 17 + SelBuf[i].GetInstanceID();
                return h;
            }
            catch { return 0; }
        }

        private static int ModeNow()
        {
            try { return (int)Brix.Input.InputModeController.GetCurrentMode(); }
            catch { return 0; }
        }

        private static void ResolveTaps(float now)
        {
            for (int i = Pending.Count - 1; i >= 0; i--)
            {
                Tap t = Pending[i];
                if (now < t.Due) continue;
                Pending.RemoveAt(i);
                if (_effectSeq != DownSeq[t.Key] || SelHash() != DownSel[t.Key] || ModeNow() != DownMode[t.Key]) Count(t.Key, now);
            }
        }

        private static bool TextFocus()
        {
            UnityEngine.EventSystems.EventSystem es = UnityEngine.EventSystems.EventSystem.current;
            return es != null && es.currentSelectedGameObject != null && es.currentSelectedGameObject.GetComponent<UnityEngine.UI.InputField>() != null;
        }

        private static readonly List<int> PendingKeys = new List<int>();
        private static readonly int[] ModKeys = { 301, 303, 304, 305, 306, 307, 308 };   // caps lock, right/left shift, ctrl, alt

        private static void AddKey(int code) { if (PendingKeys.Count < 12) PendingKeys.Add(code); }

        /// <summary>Codes of the presses counted since the last call (null when none); allocates only on a press.</summary>
        internal static int[] TakeKeys()
        {
            if (PendingKeys.Count == 0) return null;
            int[] r = PendingKeys.ToArray();
            PendingKeys.Clear();
            return r;
        }

        // ------------------------------------------------------------------ which keys do something: advanced hotkeys + the game's own bindings (Rewired keyboard maps)
        private static readonly HashSet<int> Bound = new HashSet<int>();
        private static bool _boundOk;
        private static float _boundNext;

        private static bool IsBound(int k)
        {
            float t = Time.unscaledTime;
            if (t >= _boundNext) { _boundNext = t + 10f; RefreshBound(); }
            if (!_boundOk || Bound.Contains(k)) return true;   // no binding source answered: every tap counts; advanced hotkeys always do
            List<RwAct> l;
            if (!_rw.TryGetValue(k, out l)) return false;
            for (int i = 0; i < l.Count; i++)
            {
                string c = l[i].Cat;
                if (c == "General" || c == "Bricktron") return true;                          // always-on gameplay actions
                if (c == "Camera" || c == "debug" || c.StartsWith("we_")) continue;            // camera move / rotate / zoom, debug and world editor are not actions
                if (Registered(l[i].Name)) return true;                                         // menu shortcuts count only while their menu is open
            }
            return false;
        }

        private sealed class RwAct { internal string Name, Cat; }

        private static Dictionary<int, List<RwAct>> _rw = new Dictionary<int, List<RwAct>>();
        private static System.Collections.IDictionary _reg;
        private static bool _regTried;

        /// <summary>The game registers a key-binding action only while the menu that owns it is active (KeyBindingsUtility.ActionMap).</summary>
        private static bool Registered(string action)
        {
            if (!_regTried)
            {
                _regTried = true;
                try { _reg = HarmonyLib.AccessTools.Field(typeof(Brix.Utils.UI.KeyBindingsUtility), "ActionMap").GetValue(null) as System.Collections.IDictionary; }
                catch (System.Exception e) { BtcLog.Warn("Apm", "ActionMap: " + e.Message); }
            }
            return _reg == null || _reg.Contains(action);   // unreadable: count the key
        }

        // ------------------------------------------------------------------ the player's own binding table (stored in the replay, sent to the host in chunks)
        private static byte[] _bindBlob;
        private static float _bindNext;
        private static int _bindPos;

        /// <summary>Deflated "key -> action labels" table of this PC (advanced hotkeys + the game's bindings); rebuilt every 60 s; null while nothing is known.</summary>
        internal static byte[] BindBlob()
        {
            float t = Time.unscaledTime;
            if (_bindBlob == null || t >= _bindNext)
            {
                _bindNext = t + 60f;
                Describe(-1);
                _bindBlob = BuildBindBlob();
                _bindPos = 0;
            }
            return _bindBlob;
        }

        /// <summary>Next chunk of the table for the client -> host batch; false once all chunks of this round went out.</summary>
        internal static bool NextBindChunk(int chunkLen, out int idx, out int total, out byte[] chunk)
        {
            idx = 0; total = 0; chunk = null;
            byte[] b = BindBlob();
            if (b == null || b.Length == 0) return false;
            total = (b.Length + chunkLen - 1) / chunkLen;
            if (total > 32 || _bindPos >= total) return false;
            idx = _bindPos++;
            int len = Mathf.Min(chunkLen, b.Length - idx * chunkLen);
            chunk = new byte[len];
            Buffer.BlockCopy(b, idx * chunkLen, chunk, 0, len);
            return true;
        }

        private static byte[] BuildBindBlob()
        {
            try
            {
                if (_actions.Count == 0) return null;
                System.IO.MemoryStream raw = new System.IO.MemoryStream();
                BtcPovRec.PutV(raw, (uint)_actions.Count);
                foreach (KeyValuePair<int, List<string>> kv in _actions)
                {
                    BtcPovRec.PutV(raw, (uint)kv.Key);
                    int n = Mathf.Min(4, kv.Value.Count);
                    BtcPovRec.PutV(raw, (uint)n);
                    for (int i = 0; i < n; i++)
                    {
                        string s = kv.Value[i].Length > 40 ? kv.Value[i].Substring(0, 40) : kv.Value[i];
                        byte[] sb = Encoding.UTF8.GetBytes(s);
                        BtcPovRec.PutV(raw, (uint)sb.Length);
                        raw.Write(sb, 0, sb.Length);
                    }
                }
                System.IO.MemoryStream o = new System.IO.MemoryStream();
                using (System.IO.Compression.DeflateStream ds = new System.IO.Compression.DeflateStream(o, System.IO.Compression.CompressionMode.Compress, true))
                {
                    byte[] rb = raw.ToArray();
                    ds.Write(rb, 0, rb.Length);
                }
                byte[] res = o.ToArray();
                if (res.Length > 32 * 400) return null;
                return res;
            }
            catch (Exception e) { BtcLog.Warn("Apm", "bind blob: " + e.Message); return null; }
        }

        private static Dictionary<int, List<string>> DecodeBind(byte[] blob)
        {
            Dictionary<int, List<string>> d = new Dictionary<int, List<string>>();
            try
            {
                System.IO.MemoryStream inp = new System.IO.MemoryStream(blob);
                System.IO.MemoryStream raw = new System.IO.MemoryStream();
                using (System.IO.Compression.DeflateStream ds = new System.IO.Compression.DeflateStream(inp, System.IO.Compression.CompressionMode.Decompress))
                {
                    byte[] buf = new byte[4096]; int n;
                    while ((n = ds.Read(buf, 0, buf.Length)) > 0) raw.Write(buf, 0, n);
                }
                byte[] r = raw.ToArray();
                int p = 0;
                int cnt = (int)BtcPovRec.GetV(r, ref p);
                for (int i = 0; i < cnt && p < r.Length; i++)
                {
                    int code = (int)BtcPovRec.GetV(r, ref p);
                    int ln = (int)BtcPovRec.GetV(r, ref p);
                    List<string> l = new List<string>();
                    for (int j = 0; j < ln && p < r.Length; j++)
                    {
                        int sl = (int)BtcPovRec.GetV(r, ref p);
                        if (sl < 0 || p + sl > r.Length) break;
                        l.Add(Encoding.UTF8.GetString(r, p, sl)); p += sl;
                    }
                    d[code] = l;
                }
            }
            catch (Exception e) { BtcLog.Warn("Apm", "decode bind: " + e.Message); }
            return d;
        }

        /// <summary>What the key does for the watched player: their recorded table; this PC's bindings when the replay has none.</summary>
        internal static List<string> DescribeFor(BtcPovRec.Track t, int code)
        {
            if (t == null || t.Bind == null) return Describe(code);
            if (t.BindD == null) t.BindD = DecodeBind(t.Bind);
            List<string> l;
            return t.BindD.TryGetValue(code, out l) ? l : null;
        }

        private static bool _tableLogged, _warmed;

        /// <summary>Playback: build the binding table once so it is in the log (and ready for the hover).</summary>
        internal static void Warm() { if (_warmed) return; _warmed = true; Describe(-1); }

        private static Dictionary<int, List<string>> _actions = new Dictionary<int, List<string>>();

        private static void AddAct(Dictionary<int, List<string>> d, int code, string label)
        {
            if (string.IsNullOrEmpty(label)) return;
            List<string> l;
            if (!d.TryGetValue(code, out l)) { l = new List<string>(); d[code] = l; }
            if (!l.Contains(label)) l.Add(label);
        }

        /// <summary>What the key does in the game (advanced hotkeys + the game's own bindings, as set on this PC); null when nothing. Used by the replay key hover.</summary>
        internal static List<string> Describe(int code)
        {
            float t = Time.unscaledTime;
            if (t >= _boundNext) { _boundNext = t + 10f; RefreshBound(); }
            List<string> l;
            return _actions.TryGetValue(code, out l) ? l : null;
        }

        private static void RefreshBound()
        {
            HashSet<int> s = new HashSet<int>();
            Dictionary<int, List<string>> acts = new Dictionary<int, List<string>>();
            Dictionary<int, List<RwAct>> rw = new Dictionary<int, List<RwAct>>();
            bool ok = false;
            try
            {
                IList<BtcHotkeys.Def> defs = BtcHotkeys.Defs;
                for (int i = 0; defs != null && i < defs.Count; i++)
                {
                    KeyCode[] ks = BtcHotkeys.GetKeys(defs[i].id, new KeyCode[0]);
                    for (int j = 0; ks != null && j < ks.Length; j++)
                    {
                        s.Add((int)ks[j]); ok = true;
                        int kc = (int)ks[j];
                        if (kc >= 300 && kc <= 313) continue;   // modifiers of a combo
                        AddAct(acts, kc, defs[i].Title + (ks.Length > 1 ? " (" + BtcHotkeys.ComboText(defs[i].id) + ")" : ""));
                    }
                }
            }
            catch (System.Exception e) { BtcLog.Warn("Apm", "hotkey keys: " + e.Message); }
            try
            {
                if (Rewired.ReInput.isReady)
                {
                    Rewired.Player p = Rewired.ReInput.players.GetPlayer(0);
                    foreach (Rewired.ControllerMap m in p.controllers.maps.GetMaps(Rewired.ControllerType.Keyboard, 0))
                    {
                        if (!_tableLogged)
                        {
                            Rewired.InputMapCategory mc = Rewired.ReInput.mapping.GetMapCategory(m.categoryId);
                            int cnt = 0;
                            foreach (Rewired.ActionElementMap q in m.AllMaps) cnt++;
                            BtcLog.Info("Apm", "map " + (mc != null ? mc.name : "?" + m.categoryId) + " layout=" + m.layoutId + " enabled=" + m.enabled + " n=" + cnt);
                        }
                        Rewired.InputMapCategory cat = Rewired.ReInput.mapping.GetMapCategory(m.categoryId);
                        string catName = cat != null ? cat.name : "";
                        foreach (Rewired.ActionElementMap a in m.AllMaps)
                            if ((int)a.keyCode > 0)
                            {
                                ok = true;
                                Rewired.InputAction ia = Rewired.ReInput.mapping.GetAction(a.actionId);
                                string nm = ia != null ? ia.name : "?";
                                string mod = a.modifierKey1 != Rewired.ModifierKey.None ? a.modifierKey1 + "+" : "";
                                string lbl = ia != null ? (string.IsNullOrEmpty(ia.descriptiveName) ? ia.name : ia.descriptiveName) : "?";
                                int pi = lbl.IndexOf("keybindings_");
                                if (pi >= 0) lbl = lbl.Substring(pi + 12).Replace('_', ' ');
                                AddAct(acts, (int)a.keyCode, mod + lbl);
                                List<RwAct> rl;
                                if (!rw.TryGetValue((int)a.keyCode, out rl)) { rl = new List<RwAct>(); rw[(int)a.keyCode] = rl; }
                                rl.Add(new RwAct { Name = nm, Cat = catName });
                            }
                    }
                }
            }
            catch (System.Exception e) { BtcLog.Warn("Apm", "game keys: " + e.Message); }
            _actions = acts;
            _rw = rw;
            if (!_tableLogged && ok)
            {
                _tableLogged = true;
                StringBuilder tb = new StringBuilder();
                foreach (KeyValuePair<int, List<string>> kv in acts) { tb.Append(kv.Key).Append("=").Append(string.Join("|", kv.Value.ToArray())).Append("; "); if (tb.Length > 3000) break; }
                BtcLog.Info("Apm", "key table: " + tb);
            }
            Bound.Clear();
            foreach (int k in s) Bound.Add(k);
            _boundOk = ok;
            BtcLog.Info("Apm", "bound keys: " + Bound.Count + (ok ? "" : " (none found: every key tap counts)"));
        }

        private static void Count(int id, float now)
        {
            _pA++;
            if (now - LastT[id] > 0.15f) _pE++;
            LastT[id] = now;
        }

        /// <summary>Presses since the last call, packed: low nibble all, high nibble effective.</summary>
        internal static int TakeActions()
        {
            int r = Mathf.Min(15, _pA) | (Mathf.Min(15, _pE) << 4);
            _pA = 0; _pE = 0;
            return r;
        }
        /// <summary>BtcTimer.LoadTl token "A,col,bucketSec,digits"; true when handled.</summary>
        internal static bool TryParse(string[] p, CultureInfo c)
        {
            if (p.Length < 4 || p[0] != "A") return false;
            float bs;
            if (!float.TryParse(p[2], NumberStyles.Float, c, out bs) || bs <= 0f) return true;
            string d = p[3];
            Line l = new Line { Col = p[1].ToUpperInvariant(), Bs = bs, V = new float[d.Length] };
            Color col;
            l.C = ColorUtility.TryParseHtmlString("#" + p[1], out col) ? col : Color.white;
            l.C.a = 1f;
            for (int i = 0; i < d.Length; i++) { int v = Digits.IndexOf(d[i]); l.V[i] = v < 0 ? 0f : v * 60f / bs; }
            Smooth(l);
            Play.Add(l);
            _dirty = true;
            return true;
        }

        /// <summary>Live APM: presses of the trailing 10 s window per minute, held per bucket (a step graph with sharp edges, no interpolation).</summary>
        private static void Smooth(Line l)
        {
            int n = l.V.Length;
            float total = 0f, peak = 0f;
            for (int i = 0; i < n; i++) total += l.V[i];
            l.Avg = n > 0 ? total / n : 0f;
            ComputeSpans(l, l.V, l.Spans);   // on the raw buckets, before the trailing window delays everything by its length
            if (l.VE != null) ComputeSpans(l, l.VE, l.SpansE);
            float[] o = Trail(l.V, l.Bs);
            for (int i = 0; i < n; i++) peak = Mathf.Max(peak, o[i]);
            l.V = o;
            if (l.VE != null)
            {
                float[] oe = Trail(l.VE, l.Bs);
                for (int i = 0; i < n; i++) peak = Mathf.Max(peak, oe[i]);
                l.VE = oe;
            }
            l.Peak = peak;
        }

        /// <summary>Presses of the trailing 10 s window per minute.</summary>
        private static float[] Trail(float[] src, float bs)
        {
            int n = src.Length, k = Mathf.Max(1, Mathf.CeilToInt(10f / bs));
            float[] o = new float[n];
            float run = 0f;
            for (int i = 0; i < n; i++)
            {
                run += src[i];
                if (i >= k) run -= src[i - k];
                o[i] = run / Mathf.Min(i + 1, k);
            }
            return o;
        }

        /// <summary>Idle (a strong drop below the player's average for IdleSec), surge (clearly above it) and the personal peak, as time spans.</summary>
        private static void ComputeSpans(Line l, float[] raw, List<Span> into)
        {
            into.Clear();
            int n = raw.Length;
            float avg = 0f;
            for (int i = 0; i < n; i++) avg += raw[i];
            avg = n > 0 ? avg / n : 0f;
            if (n < 8 || avg < 20f) return;
            int r = Mathf.Max(1, Mathf.CeilToInt(5f / l.Bs));
            float[] c = new float[n];
            for (int i = 0; i < n; i++)
            {
                float s = 0f; int cnt = 0;
                for (int j = Mathf.Max(0, i - r); j <= Mathf.Min(n - 1, i + r); j++) { s += raw[j]; cnt++; }
                c[i] = s / cnt;
            }
            int minIdle = Mathf.CeilToInt(IdleSec / l.Bs), minSurge = Mathf.CeilToInt(6f / l.Bs), gap = Mathf.CeilToInt(4f / l.Bs);
            List<Span> idle = new List<Span>(), surge = new List<Span>();
            int start = -1;
            for (int i = 0; i <= n; i++)
            {
                bool on = i < n && c[i] < 0.25f * avg;
                if (on && start < 0) start = i;
                if (!on && start >= 0)
                {
                    if (i - start >= minIdle) idle.Add(new Span { Kind = 0, T0 = start * l.Bs, T1 = i * l.Bs, Val = 0f });
                    start = -1;
                }
            }
            int last = -1; start = -1;
            for (int i = 0; i <= n; i++)
            {
                bool on = i < n && c[i] > 1.7f * avg && c[i] > avg + 40f;
                if (on) { if (start < 0) start = i; last = i; }
                else if (start >= 0 && (i >= n || i - last > gap))
                {
                    if (last - start + 1 >= minSurge) surge.Add(new Span { Kind = 1, T0 = start * l.Bs, T1 = (last + 1) * l.Bs, Val = 0f });
                    start = -1;
                }
            }
            Keep(into, idle, 4); Keep(into, surge, 4);
            int pk = 0;
            for (int i = 1; i < n; i++) if (c[i] > c[pk]) pk = i;
            if (c[pk] > avg * 1.1f)
            {
                int a = pk, b = pk;
                while (a > 0 && c[a - 1] >= 0.85f * c[pk]) a--;
                while (b < n - 1 && c[b + 1] >= 0.85f * c[pk]) b++;
                float t0 = a * l.Bs, t1 = (b + 1) * l.Bs;
                if (t1 - t0 < 6f) { float mid = (t0 + t1) * 0.5f; t0 = mid - 3f; t1 = mid + 3f; }
                into.Add(new Span { Kind = 2, T0 = Mathf.Max(0f, t0), T1 = t1, Val = c[pk] });
            }
        }

        /// <summary>The longest max spans of a list go to the line.</summary>
        private static void Keep(List<Span> into, List<Span> src, int max)
        {
            src.Sort(delegate (Span x, Span y) { return (y.T1 - y.T0).CompareTo(x.T1 - x.T0); });
            for (int i = 0; i < src.Count && i < max; i++) into.Add(src[i]);
        }
        private static string NameOf(Line l)
        {
            for (int i = 0; i < BtcPovRec.Play.Count; i++)
            {
                BtcPovRec.Track t = BtcPovRec.Play[i];
                if (t.Col != null && string.Equals(t.Col.TrimStart('#'), l.Col, StringComparison.OrdinalIgnoreCase)) return BtcStr.Plain(t.Name);
            }
            return null;
        }

        // ------------------------------------------------------------------ drawing
        private static float Sample(Line l, float t)
        {
            float[] v = SeriesOf(l);
            int n = v.Length;
            if (n == 0) return 0f;
            return v[Mathf.Clamp((int)(t / l.Bs), 0, n - 1)];
        }
        private static void Over(int idx, Color c, float a)
        {
            if (a <= 0f) return;
            Color32 d = _px[idx];
            float da = d.a / 255f;
            float oa = a + da * (1f - a);
            float k = da * (1f - a);
            _px[idx] = new Color32((byte)((c.r * a + d.r / 255f * k) / oa * 255f), (byte)((c.g * a + d.g / 255f * k) / oa * 255f), (byte)((c.b * a + d.b / 255f * k) / oa * 255f), (byte)(oa * 255f));
        }

        private static int _builtMode;

        private static void Rebuild(float total)
        {
            _total = Mathf.Max(1f, total);
            if (_tex == null) { _tex = new Texture2D(_texW, _texH, TextureFormat.RGBA32, false); _tex.wrapMode = TextureWrapMode.Clamp; _tex.filterMode = FilterMode.Bilinear; _px = new Color32[_texW * _texH]; _col = new float[_texW + 1]; }
            for (int i = 0; i < _px.Length; i++) _px[i] = new Color32(0, 0, 0, 0);
            float max = 60f;
            for (int i = 0; i < Play.Count; i++) { float[] sv = SeriesOf(Play[i]); for (int k = 0; k < sv.Length; k++) max = Mathf.Max(max, sv[k]); }
            _builtMode = BtcConfig.ReplayApmMode.Value;
            _scale = Mathf.Ceil(max * 1.12f / 20f) * 20f;
            float top = 3f, bottom = _texH + 1f;   // zero sits just below the strip: invisible on the timeline line
            float ph = bottom - top;
            // faint grid at 1/2 and full scale
            for (int g = 1; g <= 2; g++)
            {
                int gy = Mathf.RoundToInt(bottom - ph * g / 2f);
                for (int x = 0; x < _texW; x++) if ((x / 6) % 2 == 0) Over(Mathf.Clamp(gy, 0, _texH - 1) * _texW + x, Color.white, 0.12f);
            }
            for (int li = 0; li < Play.Count; li++)
            {
                Line l = Play[li];
                for (int x = 0; x <= _texW; x++) _col[x] = bottom - Mathf.Clamp01(Sample(l, _total * x / _texW) / _scale) * ph;
                for (int x = 0; x < _texW; x++)
                {
                    float y0 = _col[x], y1 = _col[x + 1];
                    float lo = Mathf.Min(y0, y1), hi = Mathf.Max(y0, y1);
                    int ylo = Mathf.Max(0, Mathf.FloorToInt(lo - 3f));
                    // area under the line, faint
                    for (int y = Mathf.FloorToInt(lo); y < _texH; y++) if (y >= 0) Over(y * _texW + x, l.C, 0.07f);
                    for (int y = ylo; y < _texH && y <= hi + 3f; y++)
                    {
                        float py = y + 0.5f;
                        float dist = py < lo ? lo - py : (py > hi ? py - hi : 0f);
                        float a = Mathf.Clamp01(1.5f + 0.5f - dist) * 0.95f;
                        if (a > 0f) Over(y * _texW + x, l.C, a);
                    }
                }
            }
            // rows were built top-down: flip into the texture (row 0 = bottom)
            for (int y = 0; y < _texH / 2; y++)
                for (int x = 0; x < _texW; x++)
                {
                    int a = y * _texW + x, b = (_texH - 1 - y) * _texW + x;
                    Color32 t = _px[a]; _px[a] = _px[b]; _px[b] = t;
                }
            _tex.SetPixels32(_px);
            _tex.Apply(false);
            _dirty = false;
        }

        /// <summary>The wave inside the replay bar, above the progress track (same time axis as the track). No background: the bar is the backdrop.</summary>
        internal static void Draw(Rect r, float total, float fs)
        {
            if (!Has) return;
            if (_dirty || _builtMode != BtcConfig.ReplayApmMode.Value || Mathf.Abs(_total - Mathf.Max(1f, total)) > 0.5f) Rebuild(total);
            if (Event.current.type != EventType.Repaint) return;
            GUI.DrawTexture(r, _tex, ScaleMode.StretchToFill, true);
            DrawSpans(r, fs);
        }

        private static readonly Color[] KindCol = { new Color(0.55f, 0.82f, 1f, 1f), new Color(1f, 0.55f, 0.15f, 1f), new Color(1f, 0.85f, 0.25f, 1f) };
        private static readonly int[] KindIcon = { BtcReplayIcons.Snow, BtcReplayIcons.Fire, BtcReplayIcons.PeakMark };

        private static string KindName(int kind)
        {
            return kind == 0 ? BtcReplayUI.T("Idle", "Простой") : (kind == 1 ? BtcReplayUI.T("Surge", "Всплеск") : BtcReplayUI.T("Peak", "Пик"));
        }

        /// <summary>The auto marks as frames over the wave: the player's colour as the outline, the kind's glyph in the corner, the width is the duration.</summary>
        private static void DrawSpans(Rect r, float fs)
        {
            float g = Mathf.Min(r.height * 0.55f, fs * 1.2f);
            for (int i = 0; i < Play.Count; i++)
            {
                Line l = Play[i];
                for (int k = 0; k < SpansOf(l).Count; k++)
                {
                    Span s = SpansOf(l)[k];
                    float x0 = r.x + r.width * Mathf.Clamp01(s.T0 / _total), x1 = r.x + r.width * Mathf.Clamp01(s.T1 / _total);
                    Rect f = new Rect(x0, r.y, Mathf.Max(5f, x1 - x0), r.height);
                    Color kc = KindCol[s.Kind];
                    BtcReplayUI.Fill(f, new Color(kc.r, kc.g, kc.b, 0.10f));
                    Color oc = new Color(l.C.r, l.C.g, l.C.b, 0.9f);
                    BtcReplayUI.Fill(new Rect(f.x, f.y, f.width, 2f), oc);
                    BtcReplayUI.Fill(new Rect(f.x, f.yMax - 2f, f.width, 2f), oc);
                    BtcReplayUI.Fill(new Rect(f.x, f.y, 2f, f.height), oc);
                    BtcReplayUI.Fill(new Rect(f.xMax - 2f, f.y, 2f, f.height), oc);
                    BtcReplayIcons.Glyph(KindIcon[s.Kind], new Rect(f.x + 3f, f.y + 3f, g, g), kc);
                }
            }
        }

        /// <summary>Hover readout (called while the pointer is over the track or the wave): values of every player at fraction frac, box ends at bottomY.</summary>
        internal static void DrawHover(Rect wave, float frac, float x, float bottomY, float fs)
        {
            if (Play.Count == 0 || Event.current.type != EventType.Repaint) return;
            float t = frac * _total;
            BtcReplayUI.Fill(new Rect(x - 1f, wave.y, 2f, wave.height), new Color(1f, 1f, 1f, 0.5f));
            int spans = 0;
            for (int i = 0; i < Play.Count; i++) for (int k = 0; k < SpansOf(Play[i]).Count; k++) if (t >= SpansOf(Play[i])[k].T0 && t <= SpansOf(Play[i])[k].T1) spans++;
            float rowH = fs * 1.35f, w = fs * 15f, h = rowH * (Play.Count + 1 + spans);
            Rect tip = new Rect(Mathf.Clamp(x - w * 0.5f, 2f, Screen.width - w - 2f), bottomY - h, w, h);
            BtcReplayUI.Fill(tip, new Color(0f, 0f, 0f, 0.88f));
            GUI.Label(new Rect(tip.x + 6f, tip.y, tip.width, rowH), BtcConfig.ReplayApmMode.Value == 2 ? "EPM" : "APM", BtcReplayUI.sBarLabel);
            int sr = 0;
            for (int i = 0; i < Play.Count; i++)
                for (int k = 0; k < SpansOf(Play[i]).Count; k++)
                {
                    Span s = SpansOf(Play[i])[k];
                    if (t < s.T0 || t > s.T1) continue;
                    float sy = tip.y + rowH * (Play.Count + 1 + sr);
                    sr++;
                    BtcReplayIcons.Glyph(KindIcon[s.Kind], new Rect(tip.x + 6f, sy + rowH * 0.1f, rowH * 0.8f, rowH * 0.8f), KindCol[s.Kind]);
                    string snm = NameOf(Play[i]);
                    if (snm != null && snm.Length > 7) snm = snm.Substring(0, 6) + "..";
                    GUI.Label(new Rect(tip.x + 6f + rowH, sy, tip.width, rowH), KindName(s.Kind) + " " + (snm ?? "#" + Play[i].Col) + " " + Mathf.RoundToInt(s.T1 - s.T0) + " s", BtcReplayUI.sBarLabel);
                }
            for (int i = 0; i < Play.Count; i++)
            {
                Line l = Play[i];
                float y = tip.y + rowH * (i + 1);
                BtcReplayUI.Fill(new Rect(tip.x + 6f, y + rowH * 0.3f, rowH * 0.4f, rowH * 0.4f), l.C);
                string nm = NameOf(l);
                if (nm != null && nm.Length > 8) nm = nm.Substring(0, 7) + "..";
                GUI.Label(new Rect(tip.x + 6f + rowH * 0.7f, y, tip.width, rowH), (nm ?? "#" + l.Col) + "  " + Sample(l, t).ToString("0"), BtcReplayUI.sBarLabel);
            }
        }
        
    }
}
