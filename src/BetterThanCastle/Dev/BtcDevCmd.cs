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
        private string _newMap, _newMode, _newPreset, _menuAct, _joinIp, _joinPort, _file;
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

        private bool WaitDone(float now)
        {
            if (_wait == "measure") { if (!BtcDevEngine.Measuring) return true; if (now < _waitUntil + 1f) return false; BtcDevEngine.EndMeasure(); return true; }
            if (now >= _waitUntil) { if (_wait != "sec") BtcLog.Warn("Cmd", "wait " + _wait + " timed out"); return true; }
            if (_wait == "replay") return BtcReplay.Running;
            if (_wait == "finish") return BtcReplay.Finished;
            if (_wait == "menu")
            {
                if (!BtcDevMatch.MenuReady()) return false;
                if (_menuAct == "host") BtcDevNet.Host(_newMap, _newMode, _newPreset);
                else if (_menuAct == "join") BtcDevNet.Join(_joinIp, _joinPort);
                else if (_menuAct == "duel") BtcDuel.Launch();
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
                case "replay": StartReplay(p.Length > 1 ? p[1] : "latest"); break;
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
                        for (int i = 0; i < 12 && BtcReplay.PlaySpeed < want - 0.01f; i++) BtcReplay.SpeedStep(1);
                        for (int i = 0; i < 12 && BtcReplay.PlaySpeed > want + 0.01f; i++) BtcReplay.SpeedStep(-1);
                        BtcLog.Info("Cmd", "speed now " + BtcReplay.PlaySpeed);
                        break;
                    }
                case "seek": BtcReplay.SeekTo(F(p, 1, 0f)); break;
                case "timescale": Time.timeScale = F(p, 1, 1f); break;
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
