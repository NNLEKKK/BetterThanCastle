using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using Brix.Game;
using UnityEngine;
using UnityEngine.Networking;

namespace BetterThanCastle
{
    /// <summary>
    /// Matches split over several sessions by saves. Recording: every save gets a sidecar (btc-replaylink.txt: match id, the running recording id,
    /// its replay time at the save); a recording that starts from such a save writes "rid/match/from/fromCut" into its header, so the sessions link up
    /// by themselves (clients get the host file verbatim through BtcReplayAuto, nothing is sent on the wire). Library: the sessions form a tree per match
    /// (a save played on twice = a branch, the part played after a save that was not continued = a tail branch); the main line is the longest way through
    /// unless the user picks another. Manual links/breaks/main picks live in Replays/replay-chains.txt. A match exports to one .btcm file.
    /// </summary>
    internal static class BtcReplayChain
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        private static string Tt(string en, string ru) { return BtcReplayUI.T(en, ru); }

        // ================================================================== recording side
        /// <summary>True while a replay / spectator snapshot runs Disk_Save: that is not a save of the player, no link is written.</summary>
        internal static bool InSnapshot;

        private const string SideFile = "btc-replaylink.txt";

        private sealed class Pending { internal string Match = "", Rid = "", Map = ""; internal float Cut = -1f, At; }

        private static Pending _pending;
        private static string _recMatch = "", _recRid = "";
        private static object _sessionMap;
        private static string _sessionMatch;

        private static string NewId() { return Guid.NewGuid().ToString("N").Substring(0, 16); }

        private static bool SamePath(string a, string b)
        {
            if (string.IsNullOrEmpty(a) || string.IsNullOrEmpty(b)) return true;
            try { a = Path.GetFullPath(a).TrimEnd('\\', '/'); b = Path.GetFullPath(b).TrimEnd('\\', '/'); }
            catch (Exception) { }
            return string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
        }

        private static bool PendingFits()
        {
            Pending p = _pending;
            if (p == null || GameParam.Map == null) return false;
            if (GameParam.Map.IsNewGame()) return false;
            if (Time.unscaledTime - p.At > 3600f) return false;
            return SamePath(p.Map, GameParam.Map.path);
        }

        /// <summary>Header lines of a new recording (BtcReplay.BeginRecording).</summary>
        internal static string HeaderLines()
        {
            StringBuilder sb = new StringBuilder();
            try
            {
                _recRid = NewId();
                bool cont = PendingFits();
                Pending p = cont ? _pending : null;
                _recMatch = p != null && p.Match.Length > 0 ? p.Match : NewId();
                _sessionMap = GameParam.Map;
                _sessionMatch = _recMatch;
                sb.Append("rid=").Append(_recRid).Append('\n').Append("match=").Append(_recMatch).Append('\n');
                if (p != null && p.Rid.Length > 0)
                {
                    sb.Append("from=").Append(p.Rid).Append('\n').Append("fromCut=").Append(p.Cut.ToString("0.0", Inv)).Append('\n');
                    BtcLog.Info("ReplayChain", "recording continues " + p.Rid + " at " + p.Cut.ToString("0.0", Inv) + " s, match " + _recMatch);
                }
                else BtcLog.Info("ReplayChain", "recording " + _recRid + ", match " + _recMatch + (p != null ? " (save without a recording)" : " (new match)"));
            }
            catch (Exception e) { BtcLog.Error("ReplayChain", "header: " + e.Message); }
            return sb.ToString();
        }

        private static string SessionMatch()
        {
            if (BtcReplay.Mode == BtcReplay.State.Recording && _recMatch.Length > 0) return _recMatch;
            if (_sessionMatch != null && ReferenceEquals(_sessionMap, GameParam.Map)) return _sessionMatch;
            _sessionMap = GameParam.Map;
            _sessionMatch = PendingFits() && _pending.Match.Length > 0 ? _pending.Match : NewId();
            return _sessionMatch;
        }

        /// <summary>Asset_Map.Disk_Save postfix: the folder that becomes the save.</summary>
        internal static void OnSave(string path)
        {
            if (InSnapshot || BtcReplay.Active || string.IsNullOrEmpty(path) || !NetworkServer.active || GameParam.Map == null) return;
            StringBuilder sb = new StringBuilder();
            sb.Append("match=").Append(SessionMatch()).Append('\n');
            float t = BtcReplay.RecTime;
            if (t >= 0f && _recRid.Length > 0) sb.Append("rid=").Append(_recRid).Append('\n').Append("cut=").Append(t.ToString("0.0", Inv)).Append('\n');
            sb.Append("saved=").Append(DateTime.Now.Ticks.ToString(Inv)).Append('\n');
            File.WriteAllText(Path.Combine(path, SideFile), sb.ToString(), new UTF8Encoding(false));
            BtcLog.Info("ReplayChain", "save linked: match " + SessionMatch() + (t >= 0f ? ", recording " + _recRid + " at " + t.ToString("0.0", Inv) + " s" : ", not recording"));
        }

        /// <summary>Asset_Map.Disk_ReadGameObjects prefix: the save being loaded.</summary>
        internal static void OnLoad(string path)
        {
            if (BtcReplay.Active || string.IsNullOrEmpty(path)) return;
            _pending = null;
            string f = Path.Combine(path, SideFile);
            if (!File.Exists(f)) return;
            Pending p = new Pending { Map = path, At = Time.unscaledTime };
            foreach (string line in File.ReadAllLines(f, Encoding.UTF8))
            {
                int i = line.IndexOf('=');
                if (i <= 0) continue;
                string k = line.Substring(0, i), v = line.Substring(i + 1).Trim();
                if (k == "match") p.Match = v;
                else if (k == "rid") p.Rid = v;
                else if (k == "cut") float.TryParse(v, NumberStyles.Float, Inv, out p.Cut);
            }
            _pending = p;
            BtcLog.Info("ReplayChain", "loaded save of match " + p.Match + (p.Rid.Length > 0 ? ", continues " + p.Rid + " at " + p.Cut.ToString("0.0", Inv) + " s" : ""));
        }

        // ================================================================== model
        internal sealed class Node
        {
            internal BtcReplay.Data D;
            internal string Id;
            internal Node Parent;
            internal float Cut = -1f;                     // parent's replay time where this session continues
            internal bool Manual;
            internal readonly List<Node> Kids = new List<Node>();
            internal float Dur { get { return D.Duration; } }
        }

        internal struct Step { internal Node N; internal float End; }

        internal sealed class Line
        {
            internal readonly List<Step> S = new List<Step>();
            internal float Total;
            internal string Key = "";
            internal float Off(int i) { float o = 0f; for (int k = 0; k < i && k < S.Count; k++) o += S[k].End; return o; }
            internal int StepAt(float g) { float o = 0f; for (int k = 0; k < S.Count; k++) { if (g < o + S[k].End || k == S.Count - 1) return k; o += S[k].End; } return 0; }
            internal void Finish() { Total = 0f; StringBuilder sb = new StringBuilder(); for (int k = 0; k < S.Count; k++) { Total += S[k].End; sb.Append(S[k].N.Id).Append('@').Append(S[k].End.ToString("0.0", Inv)).Append('>'); } Key = sb.ToString(); }
            internal int IndexOf(Node n) { for (int k = 0; k < S.Count; k++) if (S[k].N == n) return k; return -1; }
        }

        /// <summary>Where another line leaves the shown one: global time on the shown line and the length of the different part.</summary>
        internal struct Fork { internal Line L; internal float At, Len; internal int Step; internal bool Tail; }

        internal sealed class Match
        {
            internal string Key;
            internal Node Root;
            internal readonly List<Node> Nodes = new List<Node>();
            internal readonly List<Line> Lines = new List<Line>();
            internal Line Main;
            internal int Sessions { get { return Nodes.Count; } }
            internal Line LineByKey(string k) { for (int i = 0; i < Lines.Count; i++) if (Lines[i].Key == k) return Lines[i]; return null; }

            internal List<Fork> Forks(Line cur)
            {
                List<Fork> r = new List<Fork>();
                for (int i = 0; i < Lines.Count; i++)
                {
                    Line l = Lines[i];
                    if (l == cur) continue;
                    int j = 0;
                    while (j < l.S.Count && j < cur.S.Count && l.S[j].N == cur.S[j].N && Mathf.Abs(l.S[j].End - cur.S[j].End) < 0.05f) j++;
                    float at = cur.Off(j);
                    if (j < l.S.Count && j < cur.S.Count && l.S[j].N == cur.S[j].N) at += Mathf.Min(l.S[j].End, cur.S[j].End);
                    float len = l.Total - at;
                    if (len < 1f) continue;
                    bool tail = l.S.Count > 0 && l.S[l.S.Count - 1].N.Kids.Count > 0;
                    r.Add(new Fork { L = l, At = at, Len = len, Step = j, Tail = tail });
                }
                r.Sort(delegate (Fork a, Fork b) { return a.At.CompareTo(b.At); });
                return r;
            }
        }

        private static readonly Dictionary<string, Match> ByFile = new Dictionary<string, Match>();
        private static readonly List<Match> All = new List<Match>();
        private static string _sig = "";
        private static List<BtcReplay.Data> _lastList;
        private static int _ver;

        internal static string IdOf(BtcReplay.Data d) { return !string.IsNullOrEmpty(d.Rid) ? d.Rid : "f:" + d.FileName; }

        private static bool Playable(BtcReplay.Data d) { return d != null && d.Error == null && d.Complete && d.SnapBytes > 0; }

        /// <summary>The match a replay file belongs to (null for a single session).</summary>
        internal static Match Of(BtcReplay.Data d) { Build(); Match m; return d != null && ByFile.TryGetValue(d.FileName, out m) ? m : null; }

        /// <summary>A file that is a session of a match but not its first one: the library shows only the match card.</summary>
        internal static bool Hidden(BtcReplay.Data d) { Match m = Of(d); return m != null && m.Root.D != d; }

        internal static int Version { get { Build(); return _ver; } }

        internal static void Invalidate() { _sig = ""; }

        private static void Build()
        {
            List<BtcReplay.Data> list = BtcReplayUI.list;
            LoadOverrides();
            string sig = list.Count + "|" + (list.Count > 0 ? list[0].FileName + list[list.Count - 1].FileName : "") + "|" + _ovVer;
            if (sig == _sig && ReferenceEquals(list, _lastList)) return;   // a refresh makes new Data objects: rebuild then too
            _sig = sig;
            _lastList = list;
            _ver++;
            ByFile.Clear();
            All.Clear();
            Dictionary<string, Node> byId = new Dictionary<string, Node>();
            List<Node> nodes = new List<Node>();
            for (int i = 0; i < list.Count; i++)
            {
                BtcReplay.Data d = list[i];
                if (!Playable(d)) continue;
                string id = IdOf(d);
                if (byId.ContainsKey(id)) continue;   // the same recording twice (a received copy): the first one counts
                Node n = new Node { D = d, Id = id };
                byId[id] = n;
                nodes.Add(n);
            }
            nodes.Sort(delegate (Node a, Node b) { return a.D.Ticks.CompareTo(b.D.Ticks); });
            // explicit parents: manual links, then the header link
            for (int i = 0; i < nodes.Count; i++)
            {
                Node n = nodes[i];
                if (Breaks.Contains(n.Id)) continue;
                string[] ml;
                Node p;
                if (Links.TryGetValue(n.Id, out ml) && byId.TryGetValue(ml[0], out p) && Attach(n, p, ParseF(ml[1]), true)) continue;
                if (!string.IsNullOrEmpty(n.D.From) && byId.TryGetValue(n.D.From, out p)) Attach(n, p, n.D.FromCut, false);
            }
            // same match id without a header link (a session in between was not recorded): continue the latest earlier session of the match
            for (int i = 0; i < nodes.Count; i++)
            {
                Node n = nodes[i];
                if (n.Parent != null || Breaks.Contains(n.Id) || string.IsNullOrEmpty(n.D.Match)) continue;
                for (int k = i - 1; k >= 0; k--)
                {
                    Node p = nodes[k];
                    if (p.D.Match != n.D.Match || p.D.Ticks >= n.D.Ticks) continue;
                    if (Attach(n, p, -1f, false)) break;
                }
            }
            for (int i = 0; i < nodes.Count; i++)
            {
                Node r = nodes[i];
                if (r.Parent != null || r.Kids.Count == 0) continue;
                Match m = new Match { Root = r, Key = r.Id };
                Collect(r, m.Nodes);
                for (int k = 0; k < m.Nodes.Count; k++)
                {
                    Node n = m.Nodes[k];
                    n.Kids.Sort(delegate (Node a, Node b) { int c = a.Cut.CompareTo(b.Cut); return c != 0 ? c : a.D.Ticks.CompareTo(b.D.Ticks); });
                    ByFile[n.D.FileName] = m;
                }
                Enumerate(r, new List<Step>(), m.Lines);
                m.Main = PickMain(m);
                All.Add(m);
            }
            if (All.Count > 0) BtcLog.Info("ReplayChain", All.Count + " match(es) from " + nodes.Count + " replays");
        }

        private static float ParseF(string s) { float f; return float.TryParse(s, NumberStyles.Float, Inv, out f) ? f : -1f; }

        private static bool Attach(Node n, Node p, float cut, bool manual)
        {
            if (p == n || p.D.Ticks >= n.D.Ticks) return false;
            for (Node a = p; a != null; a = a.Parent) if (a == n) return false;
            if (cut < 0f || cut > p.Dur - 0.5f) cut = p.Dur;
            n.Parent = p; n.Cut = cut; n.Manual = manual;
            p.Kids.Add(n);
            return true;
        }

        private static void Collect(Node n, List<Node> into) { into.Add(n); for (int i = 0; i < n.Kids.Count; i++) Collect(n.Kids[i], into); }

        private static void Enumerate(Node n, List<Step> prefix, List<Line> into)
        {
            if (into.Count >= 48) return;
            if (n.Kids.Count == 0) { Emit(prefix, n, n.Dur, into); return; }
            float last = 0f;
            for (int i = 0; i < n.Kids.Count; i++)
            {
                Node k = n.Kids[i];
                last = Mathf.Max(last, k.Cut);
                prefix.Add(new Step { N = n, End = k.Cut });
                Enumerate(k, prefix, into);
                prefix.RemoveAt(prefix.Count - 1);
            }
            if (n.Dur - last > 5f) Emit(prefix, n, n.Dur, into);   // played on after the last continued save: a tail branch
        }

        private static void Emit(List<Step> prefix, Node n, float end, List<Line> into)
        {
            Line l = new Line();
            l.S.AddRange(prefix);
            l.S.Add(new Step { N = n, End = end });
            l.Finish();
            into.Add(l);
        }

        private static float Longest(Node n)
        {
            float best = n.Dur;
            for (int i = 0; i < n.Kids.Count; i++) best = Mathf.Max(best, n.Kids[i].Cut + Longest(n.Kids[i]));
            return best;
        }

        /// <summary>Main line: at every session the user's pick (a session or "the rest of this one"), else the longest way on.</summary>
        private static Line PickMain(Match m)
        {
            Line l = new Line();
            Node n = m.Root;
            while (n != null && l.S.Count < 64)
            {
                if (n.Kids.Count == 0) { l.S.Add(new Step { N = n, End = n.Dur }); break; }
                string pick;
                Node next = null;
                bool tail = false;
                if (MainPick.TryGetValue(n.Id, out pick))
                {
                    if (pick == "*") tail = true;
                    else for (int i = 0; i < n.Kids.Count; i++) if (n.Kids[i].Id == pick) next = n.Kids[i];
                }
                if (next == null && !tail)
                {
                    float best = -1f;
                    for (int i = 0; i < n.Kids.Count; i++)
                    {
                        float v = n.Kids[i].Cut + Longest(n.Kids[i]);
                        if (v > best + 0.5f || (Mathf.Abs(v - best) <= 0.5f && next != null && n.Kids[i].D.Ticks > next.D.Ticks)) { best = v; next = n.Kids[i]; }
                    }
                }
                if (tail || next == null) { l.S.Add(new Step { N = n, End = n.Dur }); break; }
                l.S.Add(new Step { N = n, End = next.Cut });
                n = next;
            }
            l.Finish();
            Line same = m.LineByKey(l.Key);
            return same ?? (m.Lines.Count > 0 ? m.Lines[0] : l);
        }

        internal static List<string> PlayersOf(Match m)
        {
            List<string> r = new List<string>();
            for (int i = 0; i < m.Nodes.Count; i++)
                for (int k = 0; k < m.Nodes[i].D.Players.Count; k++)
                {
                    string p = m.Nodes[i].D.Players[k];
                    if (p != "REC" && !r.Contains(p)) r.Add(p);
                }
            return r;
        }

        internal static string Clock(float s) { return BtcReplayUI.Clock(s); }

        // ================================================================== overrides (Replays/replay-chains.txt)
        private static readonly HashSet<string> Breaks = new HashSet<string>();
        private static readonly Dictionary<string, string[]> Links = new Dictionary<string, string[]>();   // child -> { parent, cut }
        private static readonly Dictionary<string, string> MainPick = new Dictionary<string, string>();     // node -> child id or "*"
        private static bool _ovLoaded;
        private static int _ovVer;

        private static string OvFile { get { return Path.Combine(BtcReplay.ReplaysDir, "replay-chains.txt"); } }

        private static void LoadOverrides()
        {
            if (_ovLoaded) return;
            _ovLoaded = true;
            Breaks.Clear(); Links.Clear(); MainPick.Clear();
            try
            {
                if (!File.Exists(OvFile)) return;
                ParseOverrides(File.ReadAllText(OvFile, Encoding.UTF8));
            }
            catch (Exception e) { BtcLog.Error("ReplayChain", "overrides load: " + e.Message); }
        }

        private static void ParseOverrides(string text)
        {
            foreach (string line in text.Split('\n'))
            {
                string[] p = line.TrimEnd('\r').Split('\t');
                if (p.Length >= 2 && p[0] == "B") Breaks.Add(p[1]);
                else if (p.Length >= 4 && p[0] == "L") { Links[p[1]] = new[] { p[2], p[3] }; Breaks.Remove(p[1]); }
                else if (p.Length >= 3 && p[0] == "M") MainPick[p[1]] = p[2];
            }
        }

        private static string SerializeOverrides()
        {
            StringBuilder sb = new StringBuilder();
            foreach (string b in Breaks) sb.Append("B\t").Append(b).Append('\n');
            foreach (KeyValuePair<string, string[]> kv in Links) sb.Append("L\t").Append(kv.Key).Append('\t').Append(kv.Value[0]).Append('\t').Append(kv.Value[1]).Append('\n');
            foreach (KeyValuePair<string, string> kv in MainPick) sb.Append("M\t").Append(kv.Key).Append('\t').Append(kv.Value).Append('\n');
            return sb.ToString();
        }

        private static void SaveOverrides()
        {
            try
            {
                Directory.CreateDirectory(BtcReplay.ReplaysDir);
                File.WriteAllText(OvFile, SerializeOverrides(), new UTF8Encoding(false));
            }
            catch (Exception e) { BtcLog.Error("ReplayChain", "overrides save: " + e.Message); }
        }

        /// <summary>Every change goes through here: one Ctrl+Z step restores the previous state.</summary>
        private static void Change(string label, Action body)
        {
            LoadOverrides();
            string before = SerializeOverrides();
            body();
            string after = SerializeOverrides();
            if (after == before) return;
            _ovVer++;
            SaveOverrides();
            BtcUndo.Push("replay", label, delegate
            {
                Breaks.Clear(); Links.Clear(); MainPick.Clear();
                ParseOverrides(before);
                _ovVer++;
                SaveOverrides();
            });
        }

        /// <summary>"This is another match": the session and everything played on from it become their own match.</summary>
        internal static void Unlink(Node n)
        {
            if (n == null || n.Parent == null) return;
            Change(Tt("Sessions unlinked", "Сессии разъединены"), delegate
            {
                Links.Remove(n.Id);
                Breaks.Add(n.Id);
            });
            BtcSfx.Delete();
            BtcToast.Show(Tt("Unlinked: it is a separate match now (Ctrl+Z to undo)", "Разъединено: теперь это отдельная катка (Ctrl+Z - отменить)"));
        }

        /// <summary>The user picked the line to treat as the match: every session on it becomes the main pick of its parent.</summary>
        internal static void MakeMain(Match m, Line l)
        {
            if (m == null || l == null) return;
            Change(Tt("Main line of the match", "Главная линия катки"), delegate
            {
                for (int i = 0; i < l.S.Count; i++)
                {
                    Node n = l.S[i].N;
                    if (n.Kids.Count == 0) continue;
                    string pick = "*";
                    if (i + 1 < l.S.Count) pick = l.S[i + 1].N.Id;
                    MainPick[n.Id] = pick;
                }
            });
            BtcSfx.Click();
        }

        /// <summary>Glue replays (or matches) into one match in recording order; each next one continues the end of the previous one.</summary>
        internal static string Merge(List<BtcReplay.Data> picks)
        {
            Build();
            List<Node> heads = new List<Node>();
            string map = null;
            foreach (BtcReplay.Data d in picks)
            {
                if (!Playable(d)) return Tt("A picked replay cannot be played.", "Один из выбранных реплеев нельзя проиграть.");
                Match m = Of(d);
                string mp = string.IsNullOrEmpty(d.Map) ? d.MapName : d.Map;
                if (map == null) map = mp;
                else if (!string.Equals(map, mp, StringComparison.OrdinalIgnoreCase)) return Tt("The replays are from different maps.", "Реплеи с разных карт.");
                Node n = m != null ? m.Root : NodeOf(d);
                if (n != null && !heads.Contains(n)) heads.Add(n);
            }
            if (heads.Count < 2) return Tt("Pick at least two replays.", "Выберите хотя бы два реплея.");
            heads.Sort(delegate (Node a, Node b) { return a.D.Ticks.CompareTo(b.D.Ticks); });
            Change(Tt("Replays merged", "Реплеи склеены"), delegate
            {
                for (int i = 1; i < heads.Count; i++)
                {
                    Node prev = LastOnMain(heads[i - 1]);
                    Links[heads[i].Id] = new[] { prev.Id, prev.Dur.ToString("0.0", Inv) };
                    Breaks.Remove(heads[i].Id);
                }
            });
            BtcSfx.Click();
            return null;
        }

        private static Node NodeOf(BtcReplay.Data d)
        {
            // a single session: a node of its own, not part of the built matches
            return new Node { D = d, Id = IdOf(d) };
        }

        private static Node LastOnMain(Node head)
        {
            Match m = null;
            if (head.Kids.Count > 0 || head.Parent != null) ByFile.TryGetValue(head.D.FileName, out m);
            if (m == null || m.Main == null || m.Main.S.Count == 0) return head;
            return m.Main.S[m.Main.S.Count - 1].N;
        }

        // ================================================================== one file (.btcm)
        private static readonly byte[] PackMagic = { 66, 84, 67, 77, 1 };   // "BTCM" v1

        /// <summary>Writes the sessions of a match (all of them, branches too) and its links into one .btcm file next to the replays; returns its path.</summary>
        internal static string Export(Match m, string title)
        {
            try
            {
                StringBuilder man = new StringBuilder();
                man.Append("name=").Append((title ?? "").Replace('\n', ' ')).Append('\n');
                for (int i = 0; i < m.Nodes.Count; i++)
                {
                    Node n = m.Nodes[i];
                    man.Append("f\t").Append(n.D.FileName).Append('\t').Append(new FileInfo(n.D.FilePath).Length.ToString(Inv)).Append('\n');
                    if (n.Parent != null) man.Append("L\t").Append(n.Id).Append('\t').Append(n.Parent.Id).Append('\t').Append(n.Cut.ToString("0.0", Inv)).Append('\n');
                    string pick;
                    if (MainPick.TryGetValue(n.Id, out pick)) man.Append("M\t").Append(n.Id).Append('\t').Append(pick).Append('\n');
                }
                byte[] mb = Encoding.UTF8.GetBytes(man.ToString());
                string baseName = Path.GetFileNameWithoutExtension(m.Root.D.FileName) + "_match";
                string path = Path.Combine(BtcReplay.ReplaysDir, baseName + ".btcm");
                using (FileStream fs = new FileStream(path, FileMode.Create, FileAccess.Write))
                {
                    fs.Write(PackMagic, 0, PackMagic.Length);
                    fs.Write(BitConverter.GetBytes(mb.Length), 0, 4);
                    fs.Write(mb, 0, mb.Length);
                    byte[] buf = new byte[1 << 16];
                    for (int i = 0; i < m.Nodes.Count; i++)
                        using (FileStream src = new FileStream(m.Nodes[i].D.FilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                        {
                            int r;
                            while ((r = src.Read(buf, 0, buf.Length)) > 0) fs.Write(buf, 0, r);
                        }
                }
                BtcLog.Info("ReplayChain", "exported " + m.Nodes.Count + " sessions to " + path);
                return path;
            }
            catch (Exception e)
            {
                BtcLog.Error("ReplayChain", "export: " + e.Message);
                return null;
            }
        }

        private static float _nextImport;

        /// <summary>A .btcm dropped into the replays folder: its sessions are unpacked as normal replays, its links added, the file moves to Replays/.imported.</summary>
        internal static bool ImportPending()
        {
            if (Time.unscaledTime < _nextImport) return false;
            _nextImport = Time.unscaledTime + 3f;
            bool any = false;
            try
            {
                string dir = BtcReplay.ReplaysDir;
                if (!Directory.Exists(dir)) return false;
                foreach (string fp in Directory.GetFiles(dir, "*.btcm"))
                {
                    if (Import(fp)) any = true;
                }
            }
            catch (Exception e) { BtcLog.Warn("ReplayChain", "import scan: " + e.Message); }
            return any;
        }

        /// <summary>A match file pasted or dropped into the game: unpack it now; returns the file names of its sessions (to highlight them).</summary>
        internal static List<string> ImportNow(string fp)
        {
            List<string> names = new List<string>();
            try { Import(fp, names); }
            catch (Exception e) { BtcLog.Error("ReplayChain", "import " + Path.GetFileName(fp) + ": " + e.Message); }
            return names;
        }

        private static bool Import(string fp) { return Import(fp, null); }

        private static bool Import(string fp, List<string> names)
        {
            string exported = Path.Combine(BtcReplay.ReplaysDir, Path.GetFileName(fp));
            // our own export stays where it is (the player shares it): import only files whose sessions are missing here
            using (FileStream fs = new FileStream(fp, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                byte[] head = new byte[9];
                if (fs.Read(head, 0, 9) < 9 || head[0] != 66 || head[1] != 84 || head[2] != 67 || head[3] != 77 || head[4] != 1) return false;
                int ml = BitConverter.ToInt32(head, 5);
                if (ml <= 0 || ml > 1 << 20) return false;
                byte[] mb = new byte[ml];
                if (fs.Read(mb, 0, ml) < ml) return false;
                string man = Encoding.UTF8.GetString(mb);
                List<string[]> files = new List<string[]>();
                StringBuilder ov = new StringBuilder();
                foreach (string line in man.Split('\n'))
                {
                    string[] p = line.Split('\t');
                    if (p.Length >= 3 && p[0] == "f") { files.Add(p); if (names != null) names.Add(Path.GetFileName(p[1])); }
                    else if (p.Length >= 3 && (p[0] == "L" || p[0] == "M")) ov.Append(line).Append('\n');
                }
                bool missing = false;
                for (int i = 0; i < files.Count; i++)
                {
                    string dst = Path.Combine(BtcReplay.ReplaysDir, Path.GetFileName(files[i][1]));
                    long len;
                    long.TryParse(files[i][2], NumberStyles.Integer, Inv, out len);
                    if (!File.Exists(dst) || new FileInfo(dst).Length != len) missing = true;
                }
                if (!missing) return false;
                byte[] buf = new byte[1 << 16];
                for (int i = 0; i < files.Count; i++)
                {
                    string name = Path.GetFileName(files[i][1]);
                    long len;
                    long.TryParse(files[i][2], NumberStyles.Integer, Inv, out len);
                    string dst = Path.Combine(BtcReplay.ReplaysDir, name);
                    bool have = File.Exists(dst) && new FileInfo(dst).Length == len;
                    if (have) { fs.Seek(len, SeekOrigin.Current); continue; }
                    using (FileStream o = new FileStream(dst + ".part", FileMode.Create, FileAccess.Write))
                    {
                        long left = len;
                        while (left > 0)
                        {
                            int r = fs.Read(buf, 0, (int)Math.Min(buf.Length, left));
                            if (r <= 0) throw new IOException("truncated match file");
                            o.Write(buf, 0, r);
                            left -= r;
                        }
                    }
                    if (File.Exists(dst)) File.Delete(dst);
                    File.Move(dst + ".part", dst);
                }
                LoadOverrides();
                ParseOverrides(ov.ToString());
                _ovVer++;
                SaveOverrides();
            }
            try
            {
                string done = Path.Combine(BtcReplay.ReplaysDir, ".imported");
                Directory.CreateDirectory(done);
                string to = Path.Combine(done, Path.GetFileName(fp));
                if (File.Exists(to)) File.Delete(to);
                File.Move(fp, to);
            }
            catch (Exception) { }
            BtcLog.Info("ReplayChain", "imported match file " + Path.GetFileName(exported));
            BtcToast.Show(Tt("Match imported: ", "Катка добавлена: ") + Path.GetFileNameWithoutExtension(fp));
            return true;
        }
    }
}
