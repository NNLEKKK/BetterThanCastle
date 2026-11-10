#if DEV
using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace BetterThanCastle.Dev
{
    /// <summary>
    /// DEV only agent control. Script = env BTC_SCENARIO (file path) or BTClogs/cmd.txt (polled, consumed when done).
    /// One command per line, '#' comments. Commands: replay &lt;name|latest&gt; | waitreplay (until Running) | wait &lt;sec&gt;
    /// | waitfinish &lt;maxsec&gt; | newgame [mapPart|default] [gamemode e.g. conquest_mod|conquest|sandbox] [presetPart] | waitgame &lt;maxsec&gt; | spawn &lt;bricktron|chest|stockpile|rack|workbench|furnace|Factory.Name|fy_X&gt; &lt;n&gt; | waitspawn | order build &lt;n&gt; [Factory.Name] | speed &lt;x&gt; | seek &lt;sec&gt; | prof start|stop|reset | engine particles|animators|render|physics|shadows|lights|vsync on|off, engine quality | dump &lt;tag&gt; |lt;n|reset| dump &lt;tag&gt; |gt; | measure | dump &lt;tag&gt; |lt;tag| dump &lt;tag&gt; |gt; | dump &lt;tag&gt; |lt;sec| dump &lt;tag&gt; |gt; (-> engine.txt) | dump &lt;tag&gt; | timescale &lt;x&gt; | done | quit.
    /// Each executed command writes one agent.log line (tag Cmd). 'done' writes the marker BTClogs/done.txt.
    /// </summary>
    internal sealed class BtcDevCmd : MonoBehaviour
    {
        private readonly Queue<string> _q = new Queue<string>();
        private string _wait;
        private float _waitUntil;
        private float _nextPoll;
        private float _t0;
        private string _cmdFile;
        private float _lastReadyLog;
        private string _newMap, _newMode, _newPreset, _menuAct, _replayWant, _joinIp, _joinPort, _file;
        private int _n;
        private float _stable;

        internal static void Init()
        {
            BtcDevEngine.Init();
            if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("BTC_ROLE"))) BtcDevNet.Init();
            string sc = Environment.GetEnvironmentVariable("BTC_SCENARIO");
            string cmd = Path.Combine(BtcDevPaths.LogDir, "cmd.txt");
            if (string.IsNullOrEmpty(sc) && !File.Exists(cmd)) return;
            GameObject go = new GameObject("BtcDevCmd");
            UnityEngine.Object.DontDestroyOnLoad(go);
            go.hideFlags = HideFlags.HideAndDontSave;
            BtcDevCmd c = go.AddComponent<BtcDevCmd>();
            c._cmdFile = string.IsNullOrEmpty(sc) ? cmd : sc;
            try { string d = Path.Combine(BtcDevPaths.LogDir, "done.txt"); if (File.Exists(d)) File.Delete(d); } catch { }
            BtcLog.Info("Cmd", "armed: " + c._cmdFile);
        }

        private void Update()
        {
            float now = Time.realtimeSinceStartup;
            BtcDevMatch.Pump(25);
            if (_t0 == 0f) _t0 = now;
            if (_q.Count == 0 && _wait == null && now >= _nextPoll)
            {
                _nextPoll = now + 1f;
                Load();
            }
            if (_wait != null)
            {
                if (!WaitDone(now)) return;
                _wait = null;
            }
            while (_q.Count > 0 && _wait == null)
            {
                string line = _q.Dequeue();
                try { Exec(line, now); }
                catch (Exception e) { BtcLog.Error("Cmd", line + " -> " + e.Message); }
            }
        }

        private void Load()
        {
            try
            {
                if (!File.Exists(_cmdFile)) return;
                string[] lines = File.ReadAllLines(_cmdFile);
                if (Path.GetFileName(_cmdFile) == "cmd.txt") File.Delete(_cmdFile);
                else _cmdFile = Path.Combine(BtcDevPaths.LogDir, "cmd.txt"); // scenario runs once, then poll cmd.txt
                for (int i = 0; i < lines.Length; i++)
                {
                    string l = lines[i].Trim();
                    if (l.Length > 0 && l[0] != '#') _q.Enqueue(l);
                }
            }
            catch (Exception e) { BtcLog.Warn("Cmd", "load: " + e.Message); }
        }

        private float _playTo;
        private float _playSpeed = 1f;

        private bool WaitDone(float now)
        {
            if (_wait == "measure") { if (!BtcDevEngine.Measuring) return true; if (now < _waitUntil + 1f) return false; BtcDevEngine.EndMeasure(); return true; }
            if (now >= _waitUntil) { if (_wait != "sec") BtcLog.Warn("Cmd", "wait " + _wait + " timed out"); return true; }
            if (_wait == "replay") return BtcReplay.Running;
            if (_wait == "playto") { if (_playTo - BtcReplay.PlayClock < 2f) for (int i = 0; i < 12 && BtcReplay.PlaySpeed > 1.01f; i++) BtcReplay.SpeedStep(-1); if (BtcReplay.PlayClock < _playTo) return false; if (!BtcReplay.PlayPaused) BtcReplay.TogglePause(); BtcLog.Info("Cmd", "playto reached " + BtcReplay.PlayClock.ToString("0.00")); return true; }
            if (_wait == "finish") return BtcReplay.Finished;
            if (_wait == "menu")
            {
                if (!BtcDevMatch.MenuReady()) return false;
                if (_menuAct == "host") BtcDevNet.Host(_newMap, _newMode, _newPreset);
                else if (_menuAct == "join") BtcDevNet.Join(_joinIp, _joinPort);
                else if (_menuAct == "duel") BtcDuel.Launch();
                else if (_menuAct == "replay") StartReplay(_replayWant);
                else BtcDevMatch.NewGame(_newMap, _newMode, _newPreset);
                return true;
            }
            if (_wait == "game") { if (now - _lastReadyLog > 10f) { _lastReadyLog = now; BtcLog.Info("Cmd", "waitgame " + BtcDevMatch.ReadyState()); } if (!BtcDevMatch.Ready()) { _stable = 0f; return false; }
                if (Time.unscaledDeltaTime > 0.25f) _stable = 0f; else _stable += Time.unscaledDeltaTime;
                if (_stable < 4f) return false;
                BtcLog.Info("Cmd", "game ready " + BtcDevMatch.ReadyState());
                return true; }
            if (_wait == "duelstate") return BtcDuel.Match != null && (BtcDuel.Match.State == BtcDuelState.Fight || BtcDuel.Match.State == BtcDuelState.Over || (_n == 9 && BtcDuel.Match.State == BtcDuelState.Countdown));
            if (_wait == "duelover") return BtcDuel.Match != null && BtcDuel.Match.State == BtcDuelState.Over;
            if (_wait == "spawn") return BtcDevMatch.Pending == 0;
            if (_wait == "eco") return !BtcDevEcoOps.Busy;
            if (_wait == "clients") return BtcDevNet.ClientsReady(_n);
            if (_wait == "lobby") return BtcDevNet.LobbyReady();
            if (_wait == "ready") return BtcDevNet.ClientReady();
            if (_wait == "startmatch") return BtcDevNet.StartMatch();
            if (_wait == "file") return File.Exists(_file);
            return false;
        }

        private static float F(string[] p, int i, float def)
        {
            float v;
            return p.Length > i && float.TryParse(p[i], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out v) ? v : def;
        }

        private void Exec(string line, float now)
        {
            string[] p = line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            string c = p[0].ToLowerInvariant();
            BtcLog.Info("Cmd", "t=" + (int)(now - _t0) + " " + line);
            switch (c)
            {
                case "replay": _menuAct = "replay"; _replayWant = p.Length > 1 ? p[1] : "latest"; _wait = "menu"; _waitUntil = now + 120f; break; // waits for the menu: a replay started before the factories are ready never loads
                case "host": _menuAct = "host"; _newMap = p.Length > 1 ? p[1] : null; _newMode = p.Length > 2 ? p[2] : null; _newPreset = p.Length > 3 ? p[3] : null; _wait = "menu"; _waitUntil = now + 120f; break;
                case "join": _menuAct = "join"; _joinIp = p.Length > 1 ? p[1] : "127.0.0.1"; _joinPort = p.Length > 2 ? p[2] : null; _wait = "menu"; _waitUntil = now + 120f; break;
                case "waitclients": _n = (int)F(p, 1, 1f); _wait = "clients"; _waitUntil = now + F(p, 2, 120f); break;
                case "waitlobby": _wait = "lobby"; _waitUntil = now + F(p, 1, 60f); break;
                case "ready": _wait = "ready"; _waitUntil = now + F(p, 1, 60f); break;
                case "startmatch": _wait = "startmatch"; _waitUntil = now + F(p, 1, 90f); break;
                case "waitfile": _file = BtcDevNet.ResolvePath(p.Length > 1 ? p[1] : "BTClogs/host/host-ready.txt"); _wait = "file"; _waitUntil = now + F(p, 2, 300f); break;
                case "newgame": _menuAct = null; _newMap = p.Length > 1 ? p[1] : null; _newMode = p.Length > 2 ? p[2] : null; _newPreset = p.Length > 3 ? p[3] : null; _wait = "menu"; _waitUntil = now + 120f; break;
                case "duel": _menuAct = "duel"; _wait = "menu"; _waitUntil = now + 120f; break;
                case "waitduel": _n = 0; _wait = "duelstate"; _waitUntil = now + F(p, 1, 240f); break;
                case "waitduelover": _wait = "duelover"; _waitUntil = now + F(p, 1, 300f); break;
                case "duelunits": { int k = (int)F(p, 1, 4f); BtcDuelConfig.PlayerCount[k] = (int)F(p, 2, 5f); BtcDuelConfig.BotCount[k] = (int)F(p, 3, 5f); } break;
                case "duellzdump": BtcDuelBotLuminaZero.ObsDump = F(p, 1, 0f) != 0f; break;
                case "duelskip": BtcDuelConfig.SkipAnims = F(p, 1, 1f) > 0.5f; break;
                case "duelheal": BtcDuelConfig.InfiniteHeal = F(p, 1, 0f) > 0.5f; break;
                case "duelpmove": if (BtcDuel.Match != null) { float fx = F(p, 1, 0.5f); foreach (BtcDuelUnitRef du in BtcDuel.Match.Units) if (du.Side == 0 && !du.Dead) BtcDuel.MoveAttack(du, new UnityEngine.Vector3(BtcDuelMap.X0 + fx * BtcDuelMap.Len, BtcDuelMap.Floor, du.Tf.position.z)); } break;
                case "duelplayerbot": BtcDuel.AutoPlayerLevel = (int)F(p, 1, 0f); BtcDuel.AutoPlayerKind = (int)F(p, 2, 0f); BtcDuel.AutoPlayerTier = (int)F(p, 3, 4f); break;
                case "duellab": { string[] ds = (p.Length > 1 ? p[1] : "").Split(','); float[] dv = new float[ds.Length]; for (int q = 0; q < ds.Length; q++) float.TryParse(ds[q], out dv[q]); BtcDuelBot.LabDelays = ds.Length > 0 && ds[0].Length > 0 ? dv : null; } break;
                case "duellabforce": BtcDuelBot.LabForceSkip = F(p, 1, 1f) > 0.5f; break;
                case "duelarena": BtcDuelConfig.ArenaScale = F(p, 1, 1f) >= 2f ? 3 : 1; break;
                case "duelskipframes": BtcDuelBot.SkipFrames = (int)F(p, 1, 0f); break;
                case "duellabframes": { string[] fs = (p.Length > 1 ? p[1] : "").Split(','); int[] fv = new int[fs.Length]; for (int q = 0; q < fs.Length; q++) int.TryParse(fs[q], out fv[q]); BtcDuelBot.LabFrames = fs.Length > 0 && fs[0].Length > 0 ? fv : null; } break;
                case "duelfeat": { int fv0; if (p.Length > 1 && int.TryParse(p[1], out fv0)) BtcDuelBot.Feat = fv0; } break;     // int parse: a float drops the low bits of masks > 2^24
                case "duelfeat2": { int fv2; if (p.Length > 1 && int.TryParse(p[1], out fv2)) BtcDuelBot.Feat2 = fv2; } break;
                case "dueldodge": BtcDuel.LabDodgeMs = F(p, 1, 0f); break;
                case "dueltrace": BtcDevDuelTrace.Set(F(p, 1, 1f) > 0.5f); break;
                case "duelfps": { int tf = (int)F(p, 1, 0f); QualitySettings.vSyncCount = 0; Application.targetFrameRate = tf > 0 ? tf : -1; } break;
                case "duelskipdelay": BtcDuelBot.SkipDelayMs = F(p, 1, 350f); break;
                case "duelrematch": if (BtcDuel.Match != null) BtcDuel.Match.Rematch(); break;
                case "visualtest": ResourcePackManager.DevVisual((int)F(p, 1, 0f), F(p, 2, 0f) > 0.5f); break;
                case "profiletest": ResourcePackManager.DevProfile((int)F(p, 1, 0f)); break;
                case "uitest": BtcMenuState.SetInt("uiedit:page", (int)F(p, 1, 0f)); ResourcePackManager.GoTab(12); break;
                case "gsearch": BtcGlobalSearch.DevSet(p.Length > 1 ? p[1] : ""); break;
                case "gameplaytest": BtcGameplayTab.UseLegacy = false; BtcGameplayTab.Page = (int)F(p, 1, 0f); ResourcePackManager.GoTab(4); break;
                case "gameplayrec": BtcMenuState.SetInt("gameplay:rec:g", (int)F(p, 1, -1f)); BtcMenuState.SetInt("gameplay:rec:k", (int)F(p, 2, -1f)); break;
                case "updtest": BtcUpdater.DevShow((int)F(p, 1, 1f)); break;
                case "tabtest":
                    {
                        int tt = (int)F(p, 1, 0f);
                        ResourcePackManager.GoTab(tt);
                        if (tt == 9)
                        {
                            BtcReportData.Form tf = new BtcReportData.Form { Title = "dev test", What = "dev test report body" };
                            string full = BtcReportData.Full(tf);
                            BtcLog.Info("ReportDev", "full report chars=" + full.Length + " lines=" + BtcReportData.Lines(full) + " url=" + BtcReportData.IssueUrl(tf).Length);
                            for (int si = 0; si < BtcReportData.Sections.Count; si++) BtcLog.Info("ReportDev", BtcReportData.Sections[si].Key + " lines=" + BtcReportData.Lines(BtcReportData.Text(BtcReportData.Sections[si])));
                        }
                        if (tt == 10)
                        {
                            int en = 0; for (int ri = 0; ri < BtcChangelog.Releases.Count; ri++) en += BtcChangelog.Releases[ri].Entries.Count;
                            BtcLog.Info("ReportDev", "changelog releases=" + BtcChangelog.Releases.Count + " entries=" + en);
                        }
                    }
                    break;
                case "waitgame": _wait = "game"; _waitUntil = now + F(p, 1, 240f); break;
                case "waitspawn": _wait = "spawn"; _waitUntil = now + F(p, 1, 120f); break;
                case "spawn": BtcDevMatch.QueueSpawn(p.Length > 1 ? p[1] : "bricktron", (int)F(p, 2, 10f)); break;
                case "order":
                    if (p.Length > 1 && p[1] == "build") BtcDevMatch.OrderBuild((int)F(p, 2, 50f), p.Length > 3 ? p[3] : null);
                    else BtcLog.Warn("Cmd", "order: only 'order build <n> [Factory.Name]' is supported");
                    break;
                case "waitreplay": _wait = "replay"; _waitUntil = now + F(p, 1, 180f); break;
                case "waitfinish": _wait = "finish"; _waitUntil = now + F(p, 1, 600f); break;
                case "wait": _wait = "sec"; _waitUntil = now + F(p, 1, 1f); break;
                case "speed":
                    {
                        float want = F(p, 1, 1f);
                        _playSpeed = want;
                        for (int i = 0; i < 12 && BtcReplay.PlaySpeed < want - 0.01f; i++) BtcReplay.SpeedStep(1);
                        for (int i = 0; i < 12 && BtcReplay.PlaySpeed > want + 0.01f; i++) BtcReplay.SpeedStep(-1);
                        BtcLog.Info("Cmd", "speed now " + BtcReplay.PlaySpeed);
                        break;
                    }
                // normal viewing up to T (no seek), then pause: the reference for instant seek fidelity
                case "playto": _playTo = F(p, 1, 0f); for (int i = 0; i < 12 && BtcReplay.PlaySpeed < _playSpeed - 0.01f; i++) BtcReplay.SpeedStep(1); _wait = "playto"; _waitUntil = now + F(p, 2, 900f); if (BtcReplay.PlayPaused) BtcReplay.TogglePause(); break;
                case "seek": BtcReplay.SeekTo(F(p, 1, 0f)); break;
                case "statsdump": { BtcMsStore r = BtcMatchStats.Remote; int ns = 0; for (int i = 0; i < r.Factions.Count; i++) ns += r.Factions[i].Series.Count; BtcLog.Info("Cmd", "statsdump " + (p.Length > 1 ? p[1] : "") + ": match=" + r.MatchId + " facs=" + r.Factions.Count + " samples=" + ns + " T=" + r.T.ToString("0.0") + " fresh=" + BtcMatchStats.RemoteFresh + " clock=" + BtcReplay.PlayClock.ToString("0.0")); break; }
                case "marks": { System.Text.StringBuilder sb = new System.Text.StringBuilder(); foreach (BtcAutoMarks.AEv e in BtcAutoMarks.Play) if (e.K == 'D' && sb.Length < 600) sb.Append(e.T.ToString("0.0")).Append("(x").Append(e.N).Append(") "); BtcLog.Info("Cmd", "marks D: " + sb); break; }
                case "timescale": Time.timeScale = F(p, 1, 1f); break;
                case "worldsig": BtcDevWorldSig.Run(p.Length > 1 ? p[1] : "now", p.Length > 2 ? p[2] : null); break;
                case "pkpre": BtcReplaySeek.DevNoPreprocess = F(p, 1, 1f) < 0.5f; BtcLog.Info("Cmd", "pkpre " + !BtcReplaySeek.DevNoPreprocess); break;
                case "pkjump": BtcReplaySeek.DevNoJump = F(p, 1, 1f) < 0.5f; BtcLog.Info("Cmd", "pkjump " + !BtcReplaySeek.DevNoJump); break;
                case "voxprobe": BtcLog.Info("Cmd", "voxprobe: " + BtcReplayAnchor.ProbeColumn((int)F(p, 1, 0f), (int)F(p, 2, 140f), (int)F(p, 3, 145f))); break;
                case "voxset": BtcLog.Info("Cmd", "voxset: " + BtcReplayAnchor.DevVoxSet((int)F(p, 1, 0f), (int)F(p, 2, 0f), (int)F(p, 3, 0f), (int)F(p, 4, 0f), (int)F(p, 5, 0f), (int)F(p, 6, 0f))); break;
                case "voximpl":
                    {
                        object eng = Brix.Engine.Voxel.Engine;
                        if (eng == null) { BtcLog.Info("Cmd", "voximpl: null"); break; }
                        System.Collections.Generic.List<System.Type> types = new System.Collections.Generic.List<System.Type> { eng.GetType() };
                        foreach (System.Type t in eng.GetType().Assembly.GetTypes())
                            if (t.Name == "NegativeLayer" || t.Name == "TerrainSurfaceLayer" || t.Name == "Layers" || t.Name == "VoxelEngineImpl") types.Add(t);
                        foreach (System.Type t in types)
                        {
                            System.Text.StringBuilder sb = new System.Text.StringBuilder();
                            int n = 0;
                            foreach (System.Reflection.MethodInfo mi in t.GetMethods(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.DeclaredOnly))
                            {
                                sb.Append(mi.Name).Append('(');
                                foreach (System.Reflection.ParameterInfo pi in mi.GetParameters()) sb.Append(pi.ParameterType.Name).Append(' ').Append(pi.Name).Append(',');
                                sb.Append(") ");
                                if (++n % 10 == 0) { BtcLog.Info("Cmd", "voximpl " + t.FullName + ": " + sb); sb.Length = 0; }
                            }
                            if (sb.Length > 0) BtcLog.Info("Cmd", "voximpl " + t.FullName + ": " + sb);
                        }
                        break;
                    }
                case "pkpos": BtcLog.Info("Cmd", "pkpos: " + BtcReplaySeek.DevPkPos((uint)F(p, 1, 0f))); break;
                case "voxlay": BtcLog.Info("Cmd", "voxlay: " + BtcReplaySeek.DevLayerProbe(new Brix.Engine.XYZ((int)F(p, 1, 0f), (int)F(p, 2, 0f), (int)F(p, 3, 0f)))); break;
                case "skippauses": BtcReplayKeys.SkipPauses = F(p, 1, 1f) > 0.5f; BtcLog.Info("Cmd", "skippauses " + BtcReplayKeys.SkipPauses); break;
                case "voxfields": BtcDevVoxFields.Run(); break;
                case "pkcmp": BtcLog.Info("Cmd", "pkcmp: " + BtcReplaySeek.CompareActive((int)F(p, 1, 0f))); break;
                case "pkmove":BtcLog.Info("Cmd", "pkmove: " + BtcReplaySeek.MoveTo((int)F(p, 1, 0f), p.Length > 2 && p[2] == "linear")); break;
                case "pkprobe":BtcDevPkProbe.Run(F(p, 1, 1f) > 0.5f); break;
                case "pkverify":
                    {
                        int n = (int)F(p, 1, 5f), slots = BtcReplaySeek.SlotCount;
                        BtcLog.Info("Cmd", "pkverify: " + BtcReplaySeek.Count + " pks, " + slots + " slots");
                        for (int i = 0; i < n && slots > 1; i++)
                        {
                            int s = (int)((long)(slots - 2) * (n - 1 - i) / Math.Max(1, n - 1));
                            BtcLog.Info("Cmd", "pkverify " + s + ": " + BtcReplaySeek.VerifyPair(s));
                        }
                        break;
                    }
                case "ts":BtcLog.Info("Cmd", "ts timeScale=" + Time.timeScale + " speed=" + BtcReplay.PlaySpeed + " t=" + BtcReplay.PlayClock.ToString("0.0")); break;
                case "clock": // SP game clock like the player uses it: clock pause|default|fast|veryfast|double
                    {
                        string ck = p.Length > 1 ? p[1] : "default";
                        if (ck == "pause") ClockConfig.Paused = true;
                        else
                        {
                            if (ClockConfig.Paused) ClockConfig.Paused = false;
                            ClockConfig.SetClockSpeed(ck == "fast" ? ClockConfig.ClockSpeed.Fast : ck == "veryfast" ? ClockConfig.ClockSpeed.VeryFast : ck == "double" ? ClockConfig.ClockSpeed.Double : ClockConfig.ClockSpeed.Default);
                        }
                        BtcLog.Info("Cmd", "clock " + ck + " timeScale=" + Time.timeScale);
                        break;
                    }
                case "recsp": BtcReplay.GetRecordSingleplayer(); BtcReplay.RecordSingleplayer = F(p, 1, 1f) > 0.5f; break; // in memory only, cfg untouched
                case "rsync": BtcDevReplaySync.Set(F(p, 1, 1f) > 0.5f); break;
                case "rpause": BtcReplay.TogglePause(); break;
                case "prof":
                    {
                        string a = p.Length > 1 ? p[1] : "start";
                        if (a == "start") BtcDevProf.Start(); else if (a == "stop") BtcDevProf.Stop(); else if (a == "reset") BtcDevProf.Reset();
                        break;
                    }
                case "engine": BtcDevEngine.Command(p); break;
                case "walk": BtcDevMatch.Walk(p.Length > 1 ? p[1] : "go", p.Length > 2 ? p[2] : "w", (int)F(p, 2, 8f)); break;
                case "measure": BtcDevEngine.BeginMeasure(p.Length > 1 ? p[1] : "m", F(p, 2, 10f)); _wait = "measure"; _waitUntil = now + F(p, 2, 10f); break;
                case "ecodump": BtcDevEcoDump.Run(p); break;
                case "ecotrace": BtcDevEcoTrace.Set(F(p, 1, 1f) > 0.5f); break;
                case "econoore": BtcDevEcoOps.Ore(p); break;
                case "ecoauto": BtcDevEcoOps.Auto(F(p, 1, 1f) > 0.5f); break;
                case "ecoplace": BtcDevEcoOps.Place(p); _wait = "eco"; _waitUntil = now + 30f; break;
                case "ecobpcost": BtcDevEcoOps.BpCost(p); _wait = "eco"; _waitUntil = now + 600f; break;
                case "ecoplay": BtcDevEcoOps.Play(p); break;
                case "waitecoplay": _wait = "eco"; _waitUntil = now + F(p, 1, 600f); break;
                case "crystaldump": BtcDevCrystal.Dump(); break;
                case "chopdump": BtcDevChopDump.Dump(p); break;
                case "rackinfo": BtcDevRack.Info(); break;
                case "rackhostset": BtcDevRack.HostSet(p.Length > 1 ? p[1] : "Any"); break;
                case "rackgate": BtcConfig.BetterConnection.Value = p.Length < 2 || p[1] != "off"; break;
                case "rackalive": BtcDevRack.Alive(); break;
                case "rackspawn": BtcDevRack.Spawn(p.Length > 1 ? p[1] : "Toolrack", F(p, 2, 1f), (int)F(p, 3, 3f), p.Length > 4 && p[4] == "other"); break;
                case "rackset": BtcDevRack.Set(p.Length > 1 ? p[1] : "Any"); break;
                case "rackfill": BtcDevRack.Fill(p.Length > 1 ? p[1] : "Knight"); break;
                case "rackcheck": BtcDevRack.Check(p.Length > 1 ? p[1] : "x"); break;
                case "rackclear": BtcDevRack.Clear(); break;
                case "jumpsetup": BtcDevJump.Setup((int)F(p, 1, 8f)); break;
                case "jumpgo": BtcDevJump.Go(p.Length > 1 ? p[1] : "on"); break;
                case "jumpdump": BtcDevJump.Dump(p.Length > 1 ? p[1] : "x"); break;
                case "jumpreset": BtcDevJump.Reset(); break;
                case "crystaltint": BtcDevCrystal.Tint(p); break;
                case "crystalread": BtcDevCrystal.Read(); break;
                case "hostfaction": BtcDevFirefly.HostFaction((int)F(p, 1, 0f)); break;
                case "gameplay": BtcDevFirefly.Gameplay(p.Length > 1 ? p[1] : string.Empty); break;
                case "ffdump": BtcDevFirefly.Dump(p.Length > 1 ? p[1] : "cmd"); break;
                case "pov": BtcPov.Cycle(1); break;
                case "dots": BtcConfig.ReplayUnitDots.Value = (int)F(p, 1, 2f); break;
                case "taskspawn": BtcDevTasks.Spawn(); break;
                case "taskset": BtcDevTasks.SetBounds(); break;
                case "taskdump": BtcDevTasks.Dump(p.Length > 1 ? p[1] : "x"); break;
                case "bpspawn": BtcDevBp.Spawn((int)F(p, 1, 3f)); break;
                case "ffkill": BtcDevFirefly.Kill((int)F(p, 1, 1f), p.Length > 2 ? p[2] : null); break;
                case "dump": BtcDevProf.Dump(p.Length > 1 ? p[1] : "cmd"); break;
                case "done":
                    File.WriteAllText(Path.Combine(BtcDevPaths.LogDir, "done.txt"), DateTime.Now.ToString("HH:mm:ss"));
                    break;
                case "quit": BtcDevProf.Dump("quit"); File.WriteAllText(Path.Combine(BtcDevPaths.LogDir, "done.txt"), "quit"); Application.Quit(); break;
                default: if (!BtcDevLab.Cmd(c, p)) BtcLog.Warn("Cmd", "unknown: " + c); break;
            }
        }

        private static void StartReplay(string want)
        {
            List<BtcReplay.Data> list = BtcReplay.ListReplays();
            BtcReplay.Data pick = null;
            for (int i = 0; i < list.Count; i++)
            {
                BtcReplay.Data d = list[i];
                if (want == "latest") { if (pick == null || File.GetLastWriteTime(d.FilePath) > File.GetLastWriteTime(pick.FilePath)) pick = d; }
                else if (d.FileName != null && d.FileName.IndexOf(want, StringComparison.OrdinalIgnoreCase) >= 0) { pick = d; break; }
            }
            if (pick == null) { BtcLog.Warn("Cmd", "replay not found: " + want); return; }
            BtcReplay.StartReplay(pick);
        }
    }
}
#endif
