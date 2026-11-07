using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using Brix.Assets;
using Brix.Engine;
using Brix.Game;
using Brix.Network;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.Networking.NetworkSystem;
using UnityEngine.SceneManagement;

namespace BetterThanCastle
{
    /// <summary>
    /// Advanced match timer (Gameplay Settings > Timer). BetterConnection ON only: OFF registers no handler, sends nothing, shows nothing, blocks nothing.
    /// Phases: None -> Waiting (everybody presses Ready, or the host forces the start; crystals brew no energy) -> Running (elapsed from 0,
    /// or from the saved value after loading a save, in which case the game stays on the host voluntary pause until the start).
    /// Time base = Time.deltaTime (scaled): game pause and speed changes affect every peer the same way, so nothing extra is synced for them.
    /// Wire (mod ids, BC ON only): 104 state host->clients (1 Hz while running, at once on change), 105 hello/ready client->host.
    /// Save: sidecar file "btc-timer.txt" in the save folder (vanilla ignores it), written after Asset_Map.Disk_Overwrite/Disk_WriteAsNew.
    /// </summary>
    internal static partial class BtcTimer
    {
        internal const short MsgState = 104;
        internal const short MsgReady = 105;
        internal const string SideFile = "btc-timer.txt";

        internal enum Phase { None = 0, Waiting = 1, Running = 2, Counting = 3 }

        private sealed class Part { internal string Name; internal bool Ready; }

        internal static Phase Cur;
        internal static float Elapsed;
        internal static float PeaceSec, LimitSec;
        internal static float Count;
        internal static bool Resume;               // this start continues a loaded save
        internal static bool MouseOver;            // pointer is over the waiting panel (input lock)

        /// <summary>True while crystals must not brew (host side: Waiting, timer enabled).</summary>
        internal static bool FreezeEnergy;

        // host: connection id -> participant; clients: list received from the host
        private static readonly Dictionary<int, Part> Parts = new Dictionary<int, Part>();
        private static readonly List<KeyValuePair<string, bool>> Shown = new List<KeyValuePair<string, bool>>();
        private static bool _hostReady;
        private static int _sceneKey = -1;
        private static float _nextSend, _nextPrune;
        private static Phase _lastCur;
        private static int _asks, _asksLogged;
        private static float _lastAskT;
        private static float _nextAskLog;
        private static bool _dirty, _helloSent, _gotState;
        private static bool _pausedByUs;
        private static int _pauseTries;
        private static int iMs = -1, iOn = -1, iPeace = -1, iLimit = -1;
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        internal static void AddRows()
        {
            BtcGameplay.Add("timer.on", "timer", "Advanced timer", "Продвинутый таймер", 1f, 0f, 1f, 1f, true,
                "Host: everybody presses Ready after the level loads (or the host starts by force). The timer then starts from 0 on every machine; crystals give no energy until then. Needs BetterConnection ON.",
                "Хост: после загрузки уровня все жмут Готов (или хост запускает принудительно). Тогда таймер стартует с нуля у всех, до этого кристаллы не дают энергию. Нужен BetterConnection ВКЛ.");
            BtcGameplay.Add("timer.peace", "timer", "Peace time (minutes, 0 = none)", "Мирное время (минуты, 0 = нет)", 0f, 0f, 60f, 1f, false,
                "The timer is green during peace time. Only the display: combat is not blocked.", "Таймер зелёный в мирное время. Только отображение: бой не блокируется.");
            BtcGameplay.Add("timer.limit", "timer", "Game length (minutes, 0 = unlimited)", "Длительность игры (минуты, 0 = без лимита)", 0f, 0f, 180f, 1f, false,
                "When the time is up the timer turns red. The game goes on.", "Когда время вышло, таймер краснеет. Игра продолжается.");
            BtcGameplay.Add("timer.ms", "timer", "Show milliseconds", "Показывать миллисекунды", 0f, 0f, 1f, 1f, true,
                "The score panel clock shows m:ss.mmm. The panel refreshes it only a few times per second.", "Часы в панели показывают м:сс.ммм. Панель обновляет их лишь несколько раз в секунду.");
            iMs = BtcGameplay.Idx("timer.ms");
            iOn = BtcGameplay.Idx("timer.on");
            iPeace = BtcGameplay.Idx("timer.peace");
            iLimit = BtcGameplay.Idx("timer.limit");
        }

        private static float Val(int i, float def)
        {
            float[] e = BtcGameplay.E;
            return (e == null || i < 0 || i >= e.Length) ? def : e[i];
        }

        internal static bool SettingOn { get { return Val(iOn, 1f) > 0.5f; } }

        private static bool InMatch()
        {
            Scene s = SceneManager.GetActiveScene();
            if (s.buildIndex != 2) return false;
            if (BtcGameplay.InLobby) return false;
            if (!GameSignals.Received<Action>(GameSignals.LevelReady)) return false;
            return User.LocalUser != null || NetworkServer.active;
        }

        private static bool Host { get { return NetworkServer.active; } }

        // ------------------------------------------------------------------ lifecycle
        internal static void Tick()
        {
            BtcAutoMarks.Tick();
            BtcCraftQueue.Tick();
            BtcInfHeal.Tick();
            BtcHeldWards.Tick();
            BtcPovRec.Tick();
            BtcPovRec.PlayTick();
            if (Cur != _lastCur) { BtcLog.Info("Timer", "phase " + _lastCur + " -> " + Cur + " (replay " + BtcReplay.Mode + ")"); _lastCur = Cur; }
            try
            {
                if (BtcReplay.Mode == BtcReplay.State.Playing)
                {
                    PlayTick();
                    if (Time.unscaledTime >= _nextAskLog) { _nextAskLog = Time.unscaledTime + 5f; BtcLog.Info("Timer", "replay pc=" + (BtcReplay.Seeking ? BtcReplay.SeekTarget : BtcReplay.PlayClock).ToString("F0") + " el=" + Elapsed.ToString("F0") + " panelAsks+" + (_asks - _asksLogged)); _asksLogged = _asks; }
                    return;
                }
                bool want = BtcNetworkGate.Enabled && Neo.multiplayer && BtcReplay.Mode != BtcReplay.State.Playing && InMatch()
                            && (Host ? SettingOn : true);
                if (!want)
                {
                    if (Cur != Phase.None) End();
                    FreezeEnergy = false;
                    MouseOver = false;
                    return;
                }
                int key = SceneManager.GetActiveScene().GetHashCode();
                if (key != _sceneKey) { _sceneKey = key; Begin(); }
                float now = Time.unscaledTime;
                if (Host) HostTick(now);
                else ClientTick(now);
                FreezeEnergy = Host && (Cur == Phase.Waiting || Cur == Phase.Counting);
                if (Cur == Phase.Counting) Count -= Time.unscaledDeltaTime;
                if (Cur == Phase.Running) Elapsed += Time.deltaTime;
                if (Cur != Phase.None) MarkKeyTick();
                if (Host && Cur == Phase.Running) RecTick();
            }
            catch (Exception e) { BtcLog.Error("Timer", "tick: " + e); }
        }

        private static void End()
        {
            ReleaseOurPause();
            Cur = Phase.None;
            Elapsed = 0f;
            Resume = false;
            _sceneKey = -1;
            _gotState = false;
            _helloSent = false;
            _hostReady = false;
            Parts.Clear();
            Shown.Clear();
        }

        private static void Begin()
        {
            Parts.Clear();
            Shown.Clear();
            _hostReady = false;
            _gotState = false;
            _helloSent = false;
            _pausedByUs = false;
            _pauseTries = 0;
            Elapsed = 0f;
            Resume = false;
            Cur = Host ? Phase.Waiting : Phase.None;
            if (Host)
            {
                float saved;
                if (TryReadSide(out saved)) { Elapsed = saved; Resume = true; }
                _dirty = true;
                BtcLog.Info("Timer", "waiting for ready (resume=" + Resume + ", t=" + Elapsed.ToString("F1", Inv) + ")");
            }
        }

        private static void HostTick(float now)
        {
            PeaceSec = Val(iPeace, 0f) * 60f;
            LimitSec = Val(iLimit, 0f) * 60f;
            if (Cur == Phase.Waiting)
            {
                if (Resume && !_pausedByUs && _pauseTries < 20 && !BtcNetManager.VoluntaryPauseActive)
                {
                    _pauseTries++;
                    UNetManager m = NetworkManager.singleton as UNetManager;
                    if (m != null) { BtcNetManager.RequestVoluntaryPauseToggle(m); _pausedByUs = BtcNetManager.VoluntaryPauseActive; }
                }
                if (now >= _nextPrune)
                {
                    _nextPrune = now + 1f;
                    Prune();
                    if (AllReady()) StartNow("all ready");
                }
            }
            if (Cur == Phase.Counting && Count <= 0f)
            {
                Cur = Phase.Running;
                FreezeEnergy = false;
                ReleaseOurPause();
                _dirty = true;
                RecStarted();
                BtcLog.Info("Timer", "start t=" + Elapsed.ToString("F1", Inv) + " peace=" + PeaceSec + " limit=" + LimitSec);
            }
            if (_dirty || now >= _nextSend)
            {
                _dirty = false;
                _nextSend = now + (Cur == Phase.Running ? 1f : 0.5f);
                Broadcast();
            }
        }

        private static void ClientTick(float now)
        {
            if (!_helloSent && !Host && _gotState == false)
            {
                // announce ourselves to the host (registers us as a participant, host replies with the state)
                if (now >= _nextSend)
                {
                    _nextSend = now + 2f;
                    SendToHost("H");
                }
            }
        }

        private static void Prune()
        {
            List<int> gone = null;
            foreach (KeyValuePair<int, Part> kv in Parts)
            {
                bool alive = false;
                for (int i = 0; i < NetworkServer.connections.Count; i++)
                {
                    NetworkConnection c = NetworkServer.connections[i];
                    if (c != null && c.isConnected && c.connectionId == kv.Key) { alive = true; break; }
                }
                if (!alive) { if (gone == null) gone = new List<int>(); gone.Add(kv.Key); }
            }
            if (gone != null) { for (int i = 0; i < gone.Count; i++) Parts.Remove(gone[i]); _dirty = true; }
        }

        private static bool AllReady()
        {
            if (!_hostReady) return false;
            foreach (KeyValuePair<int, Part> kv in Parts) if (!kv.Value.Ready) return false;
            return true;
        }

        internal static void StartNow(string why)
        {
            if (!Host || Cur != Phase.Waiting) return;
            Cur = Phase.Counting;
            Count = 3f;
            _dirty = true;
            BtcLog.Info("Timer", "countdown (" + why + ") t=" + Elapsed.ToString("F1", Inv) + " peace=" + PeaceSec + " limit=" + LimitSec);
        }

        private static void ReleaseOurPause()
        {
            if (!_pausedByUs) return;
            _pausedByUs = false;
            try
            {
                UNetManager m = NetworkManager.singleton as UNetManager;
                if (m != null && BtcNetManager.VoluntaryPauseActive) BtcNetManager.RequestVoluntaryPauseToggle(m);
            }
            catch (Exception e) { BtcLog.Error("Timer", "release pause: " + e); }
        }

        // ------------------------------------------------------------------ user actions (HUD buttons)
        internal static void PressReady()
        {
            if (Cur != Phase.Waiting && Host) return;
            if (Host)
            {
                _hostReady = true;
                _dirty = true;
                if (AllReady()) StartNow("all ready");
            }
            else SendToHost("R");
        }

        internal static bool LocalIsReady()
        {
            if (Host) return _hostReady;
            string me = LocalName();
            for (int i = 0; i < Shown.Count; i++) if (Shown[i].Key == me) return Shown[i].Value;
            return false;
        }

        private static string LocalName()
        {
            UNetManager m = NetworkManager.singleton as UNetManager;
            return Clean(m != null ? m.LocalPlayerName : "?");
        }

        // ------------------------------------------------------------------ net
        internal static void RegisterHandlers(NetworkClient clientOverride)
        {
            if (!BtcNetworkGate.Enabled) return;
            try
            {
                if (NetworkServer.active)
                {
                    NetworkServer.UnregisterHandler(MsgReady);
                    NetworkServer.RegisterHandler(MsgReady, OnReadyAtHost);
                }
                UNetManager m = NetworkManager.singleton as UNetManager;
                NetworkClient c = clientOverride ?? (m != null ? m.LocalNetworkClient : null);
                if (c != null)
                {
                    c.UnregisterHandler(MsgState);
                    c.RegisterHandler(MsgState, OnStateAtClient);
                }
            }
            catch (Exception e) { BtcLog.Error("Timer", "RegisterHandlers: " + e); }
        }

        private static string Clean(string s)
        {
            if (string.IsNullOrEmpty(s)) return "?";
            return s.Replace(';', '_').Replace(',', '_').Replace('~', '_').Replace('|', '_');
        }

        private static void SendToHost(string kind)
        {
            if (!BtcNetworkGate.Enabled || Host) return;
            try
            {
                UNetManager m = NetworkManager.singleton as UNetManager;
                if (m == null || m.LocalNetworkClient == null || !m.LocalNetworkClient.isConnected) return;
                m.LocalNetworkClient.Send(MsgReady, new StringMessage(kind + "|" + LocalName()));
            }
            catch (Exception e) { BtcLog.Error("Timer", "SendToHost: " + e); }
        }

        private static void OnReadyAtHost(NetworkMessage nm)
        {
            try
            {
                StringMessage msg = nm.ReadMessage<StringMessage>();
                if (!NetworkServer.active || !BtcNetworkGate.Enabled || Cur == Phase.None) return;
                string[] a = msg.value.Split('|');
                if (a.Length < 2) return;
                if (a[0] == "K") { HostMark(nm.conn.connectionId, a[1]); return; }
                int id = nm.conn.connectionId;
                Part p;
                if (!Parts.TryGetValue(id, out p)) { p = new Part(); Parts[id] = p; }
                p.Name = Clean(a[1]);
                if (a[0] == "R" && Cur == Phase.Waiting) p.Ready = true;
                _dirty = true;
                if (Cur == Phase.Waiting && AllReady()) StartNow("all ready");
            }
            catch (Exception e) { BtcLog.Error("Timer", "OnReadyAtHost: " + e); }
        }

        private static string Encode()
        {
            StringBuilder sb = new StringBuilder(96);
            sb.Append((int)Cur).Append(';').Append(Elapsed.ToString("F2", Inv)).Append(';')
              .Append(PeaceSec.ToString("F0", Inv)).Append(';').Append(LimitSec.ToString("F0", Inv)).Append(';')
              .Append(Resume ? 1 : 0).Append(';').Append(Mathf.Max(0f, Count).ToString("F2", Inv)).Append(';');
            sb.Append(LocalName()).Append('~').Append(_hostReady ? 1 : 0);
            foreach (KeyValuePair<int, Part> kv in Parts) sb.Append(',').Append(kv.Value.Name).Append('~').Append(kv.Value.Ready ? 1 : 0);
            return sb.ToString();
        }

        private static void Broadcast()
        {
            if (!NetworkServer.active || !BtcNetworkGate.Enabled) return;
            try
            {
                StringMessage msg = new StringMessage(Encode());
                for (int i = 0; i < NetworkServer.connections.Count; i++)
                {
                    NetworkConnection c = NetworkServer.connections[i];
                    if (c != null && c.isConnected && !BtcNetManager.IsLocalConnection(c)) c.Send(MsgState, msg);
                }
                RebuildShown();
            }
            catch (Exception e) { BtcLog.Error("Timer", "Broadcast: " + e); }
        }

        private static void RebuildShown()
        {
            Shown.Clear();
            Shown.Add(new KeyValuePair<string, bool>(LocalName(), _hostReady));
            foreach (KeyValuePair<int, Part> kv in Parts) Shown.Add(new KeyValuePair<string, bool>(kv.Value.Name, kv.Value.Ready));
        }

        private static void OnStateAtClient(NetworkMessage nm)
        {
            try
            {
                StringMessage msg = nm.ReadMessage<StringMessage>();
                if (BtcReplay.Mode == BtcReplay.State.Playing) return;
                if (NetworkServer.active || !BtcNetworkGate.Enabled) return;
                string[] a = msg.value.Split(';');
                if (a.Length < 7) return;
                int ph = int.Parse(a[0], Inv);
                float el = float.Parse(a[1], Inv);
                PeaceSec = float.Parse(a[2], Inv);
                LimitSec = float.Parse(a[3], Inv);
                Resume = a[4] == "1";
                Count = float.Parse(a[5], Inv);
                Phase np = (Phase)ph;
                // running: keep our own smooth clock unless it drifted
                if (np == Phase.Running && Cur == Phase.Running && Mathf.Abs(Elapsed - el) < 0.5f) { }
                else Elapsed = el;
                if (np != Cur) BtcLog.Info("Timer", "state " + Cur + " -> " + np + " t=" + el.ToString("F1", Inv));
                Cur = np;
                _gotState = true;
                _helloSent = true;
                Shown.Clear();
                string[] ps = a[6].Split(',');
                for (int i = 0; i < ps.Length; i++)
                {
                    string[] kv = ps[i].Split('~');
                    if (kv.Length == 2) Shown.Add(new KeyValuePair<string, bool>(kv[0], kv[1] == "1"));
                }
            }
            catch (Exception e) { BtcLog.Error("Timer", "OnStateAtClient: " + e); }
        }

        // ------------------------------------------------------------------ save sidecar
        internal static void WriteSide(Asset_Map map)
        {
            try
            {
                if (map == null || !Host || Cur == Phase.None || string.IsNullOrEmpty(map.path)) return;
                if (map.mapType == Asset_Map.MapType.Map) return;
                string f = Path.Combine(map.path, SideFile);
                if (Cur != Phase.Running) { if (File.Exists(f)) File.Delete(f); return; }
                File.WriteAllText(f, "v1\n" + Elapsed.ToString("F2", Inv) + "\n");
                BtcLog.Info("Timer", "saved t=" + Elapsed.ToString("F1", Inv) + " -> " + f);
            }
            catch (Exception e) { BtcLog.Error("Timer", "WriteSide: " + e); }
        }

        private static bool TryReadSide(out float t)
        {
            t = 0f;
            try
            {
                Asset_Map map = GameParam.Map;
                if (map == null || string.IsNullOrEmpty(map.path) || map.mapType == Asset_Map.MapType.Map) return false;
                string f = Path.Combine(map.path, SideFile);
                if (!File.Exists(f)) return false;
                string[] l = File.ReadAllLines(f);
                return l.Length >= 2 && l[0] == "v1" && float.TryParse(l[1], NumberStyles.Float, Inv, out t);
            }
            catch (Exception e) { BtcLog.Error("Timer", "TryReadSide: " + e); return false; }
        }

        // ------------------------------------------------------------------ HUD
        private static GUIStyle _lbl, _small;
        private static string _lastRound;

        internal static string Fmt(float s)
        {
            if (s < 0f) s = 0f;
            int t = (int)s;
            return (t / 60).ToString("00") + ":" + (t % 60).ToString("00");
        }

        internal static void OnGUI()
        {
            if (Cur == Phase.None) { MouseOver = false; return; }
            try
            {
                if (_lbl == null)
                {
                    _lbl = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
                    _small = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, richText = true };
                }
                float sc = Mathf.Max(0.75f, Screen.height / 1080f);
                _lbl.fontSize = Mathf.RoundToInt(26 * sc);
                _small.fontSize = Mathf.RoundToInt(15 * sc);
                if (BtcReplay.Mode == BtcReplay.State.Playing) MouseOver = false;
                else if (Cur == Phase.Waiting) DrawWaiting(sc);
                else
                {
                    MouseOver = false;
                    if (Cur == Phase.Counting) DrawCount(sc);
                }
            }
            catch (Exception e) { BtcLog.Error("Timer", "OnGUI: " + e); }
        }

        private static void Box(Rect r, Color c)
        {
            Color old = GUI.color;
            GUI.color = c;
            GUI.DrawTexture(r, BtcUiStyle.White);
            GUI.color = old;
        }

        private static void Shadow(Rect r, string text, GUIStyle st, Color c)
        {
            Color old = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, 0.8f);
            GUI.Label(new Rect(r.x + 1.5f, r.y + 1.5f, r.width, r.height), text, st);
            GUI.color = c;
            GUI.Label(r, text, st);
            GUI.color = old;
        }

        // Text for the vanilla score panel clock (Patches/TimerPatches.cs replaces Timer:FormatedElapsedSeconds): "m:ss (-m:ss)", coloured by phase.
        internal static string PanelText(bool count = true)
        {
            if (Cur == Phase.None) return null;
            float t = Val(iMs, 0f) > 0.5f ? Elapsed : Mathf.Floor(Elapsed + 0.5f);   // the panel samples about once a second: rounding keeps it from lagging behind
            if (count) { _asks++; _lastAskT = Time.unscaledTime; }
            if (count && !_panelLogged && BtcReplay.Mode == BtcReplay.State.Playing) { _panelLogged = true; BtcLog.Info("Timer", "panel clock asked during replay, t=" + t.ToString("F1")); }
            string col = "#D1D1D1";
            string tail = string.Empty;
            if (LimitSec > 0f && t >= LimitSec) col = "#FF4033";
            else if (PeaceSec > 0f && t < PeaceSec) { col = "#59E566"; tail = " (-" + Clock(PeaceSec - t) + ")"; }
            else if (LimitSec > 0f) tail = " (-" + Clock(LimitSec - t) + ")";
            return "<color=" + col + ">" + Clock(t) + tail + "</color>";
        }

        /// <summary>Discord RPC: phase text ("Task planning" / "Peace time -1:23" / "Time limit -5:00" / "Time is up") and the clock to show (-1 = hide it).</summary>
        internal static bool DiscordInfo(out float elapsed, out string text)
        {
            elapsed = -1f;
            text = null;
            if (Cur == Phase.None || !BtcNetworkGate.Enabled) return false;
            bool ru = BtcLang.Russian;
            if (Cur == Phase.Waiting) { text = ru ? "Расстановка заданий" : "Task planning"; return true; }
            if (Cur == Phase.Counting) { text = ru ? "Старт игры" : "Starting"; return true; }
            float t = Elapsed;
            elapsed = t;
            if (PeaceSec > 0f && t < PeaceSec) text = (ru ? "Мирное время -" : "Peace time -") + Mmss(PeaceSec - t);
            else if (LimitSec > 0f && t >= LimitSec) text = ru ? "Время вышло" : "Time is up";
            else if (LimitSec > 0f) text = (ru ? "До лимита -" : "Time limit -") + Mmss(LimitSec - t);
            return true;
        }

        private static string Mmss(float s)
        {
            int n = Mathf.Max(0, Mathf.CeilToInt(s));
            return n < 3600 ? (n / 60) + ":" + (n % 60).ToString("00") : (n / 3600) + ":" + (n / 60 % 60).ToString("00") + ":" + (n % 60).ToString("00");
        }

        internal static string Clock(float s)
        {
            int n = Mathf.Max(0, Mathf.FloorToInt(s));
            string ms = Val(iMs, 0f) > 0.5f ? "." + Mathf.Clamp(Mathf.FloorToInt((Mathf.Max(0f, s) - n) * 1000f), 0, 999).ToString("000") : string.Empty;
            if (n < 3600) return (n / 60) + ":" + (n % 60).ToString("00") + ms;
            return (n / 3600) + ":" + (n / 60 % 60).ToString("00") + ":" + (n % 60).ToString("00") + ms;
        }

        private static void DrawCount(float sc)
        {
            int n = Mathf.Max(1, Mathf.CeilToInt(Count));
            int old = _lbl.fontSize;
            _lbl.fontSize = Mathf.RoundToInt(110 * sc);
            Shadow(new Rect(0f, Screen.height * 0.25f, Screen.width, 140f * sc), n.ToString(), _lbl, new Color(1f, 0.95f, 0.5f));
            _lbl.fontSize = old;
        }

        private static void DrawWaiting(float sc)
        {
            float w = 440f * sc, h = 120f * sc;
            Rect r = new Rect((Screen.width - w) * 0.5f, 4f * sc, w, h);
            Box(r, new Color(0f, 0f, 0f, 0.55f));
            int ready = 0;
            for (int i = 0; i < Shown.Count; i++) if (Shown[i].Value) ready++;
            string title = Resume ? BtcGameplay.T("Resume: waiting for players ", "Продолжение: ждём игроков ") : BtcGameplay.T("Waiting for players ", "Ждём игроков ");
            title += ready + "/" + Mathf.Max(Shown.Count, 1);
            Shadow(new Rect(r.x, r.y + 2f * sc, w, 30f * sc), title, _small, Color.white);
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < Shown.Count; i++)
            {
                if (i > 0) sb.Append("   ");
                sb.Append(Shown[i].Value ? "[x] " : "[ ] ").Append(Shown[i].Key);
            }
            Shadow(new Rect(r.x, r.y + 26f * sc, w, 26f * sc), sb.ToString(), _small, new Color(0.8f, 0.9f, 1f));
            Event e = Event.current;
            MouseOver = e != null && r.Contains(e.mousePosition);
            bool me = LocalIsReady();
            float bw = (Host ? 190f : 260f) * sc, bh = 34f * sc;
            Rect b1 = new Rect(r.x + (Host ? 18f * sc : (w - bw) * 0.5f), r.y + h - bh - 10f * sc, bw, bh);
            GUI.enabled = !me;
            if (GUI.Button(b1, me ? BtcGameplay.T("Ready (waiting)", "Готов (ждём)") : BtcGameplay.T("Ready", "Готов"))) PressReady();
            GUI.enabled = true;
            if (Host)
            {
                Rect b2 = new Rect(r.x + w - bw - 18f * sc, b1.y, bw, bh);
                if (GUI.Button(b2, BtcGameplay.T("Start now (host)", "Начать (хост)"))) StartNow("host force");
            }
        }
    }
}
