#if DEV
using System;
using System.Globalization;
using UnityEngine;
using Brix.Engine;
using Brix.Game;
using Brix.Game.AI;

namespace BetterThanCastle.Dev
{
    /// <summary>
    /// DEV only. Replay fidelity probe: every 5 s of REPLAY time (recording: BtcReplay.RecSeconds, playback: BtcReplay.PlayClock)
    /// logs agent.log "RSync m=rec|play t= ts= units= h1= c=" so a recording and its playback can be compared line by line
    /// (same t = same stream position). Scenario command: rsync 1|0.
    /// </summary>
    internal sealed class BtcDevReplaySync : MonoBehaviour
    {
        private static BtcDevReplaySync _i;
        private int _lastBucket = -1;
        private int _lastMode = -1;

        internal static void Set(bool on)
        {
            if (on && _i == null)
            {
                GameObject go = new GameObject("BtcDevReplaySync");
                DontDestroyOnLoad(go);
                go.hideFlags = HideFlags.HideAndDontSave;
                _i = go.AddComponent<BtcDevReplaySync>();
                try
                {
                    HarmonyLib.Harmony h = new HarmonyLib.Harmony("com.btc.rsync");
                    h.Patch(HarmonyLib.AccessTools.Method(typeof(Brix.NewUI.LoadingScreen.LoadingScreen), "SetLoadingTitle"), null,
                        new HarmonyLib.HarmonyMethod(typeof(BtcDevReplaySync), "TitlePost"));
                }
                catch (Exception e) { BtcLog.Warn("RSync", "title hook: " + e.Message); }
            }
            else if (!on && _i != null) { Destroy(_i.gameObject); _i = null; }
            BtcLog.Info("RSync", on ? "on" : "off");
        }

        private void Update()
        {
            try
            {
                int mode; float t;
                if (BtcReplay.Loading && Time.realtimeSinceStartup >= _diagAt) { _diagAt = Time.realtimeSinceStartup + 3f; LoadDiag(); }
                if (BtcReplay.RecSeconds > 0f) { mode = 0; t = BtcReplay.RecSeconds; }
                else if (BtcReplay.Running && !BtcReplay.Seeking) { mode = 1; t = BtcReplay.PlayClock; }
                else { _lastMode = -1; return; }
                int b = (int)(t / 5f);
                if (mode == _lastMode && b == _lastBucket) return;
                if (mode != _lastMode || b > _lastBucket) Line(mode, b * 5, t);
                _lastMode = mode; _lastBucket = b;
            }
            catch (Exception e) { BtcLog.Warn("RSync", e.Message); enabled = false; }
        }

        private float _diagAt;
        private static void LoadDiag()
        {
            string s = "loader: started=" + Sig(GameSignals.LoadingStarted) + " beforeTerrain=" + Sig(GameSignals.BeforeTerrainLoaded) + " terrain=" + Sig(GameSignals.TerrainLoaded)
                + " objects=" + Sig(GameSignals.GameObjectsLoaded) + " server=" + UnityEngine.Networking.NetworkServer.active + " mp=" + Neo.IsMultiplayer
                + " user=" + (User.LocalUser != null) + " noise=" + VoxelNoiseConfigurator.IsInitialized() + " factions=" + Faction.GetAllFactions().Count
                + " hostAll=" + (GameParam.instance != null && GameParam.instance.HostLoadedAllFactions) + " loader=" + (HarmonyLib.Traverse.Create(typeof(GameLoader)).Field("_instance").GetValue() != null) + " ts=" + Time.timeScale;
            BtcLog.Info("RSync", s);
        }
        private static string Sig(GameSignals.GameSignal g) { try { return GameSignals.Received<System.Action>(g) ? "1" : "0"; } catch (Exception) { return "?"; } }

        private static string _title;
        private static void TitlePost(string text)
        {
            if (text == _title) return;
            _title = text;
            BtcLog.Info("RSync", "loading title " + text + " server=" + UnityEngine.Networking.NetworkServer.active + " ts=" + Time.timeScale);
        }

        private static void Line(int mode, int bucket, float t)
        {
            if (!Architecte.commence) return;
            Labor[] ls = FindObjectsOfType<Labor>();
            uint h1 = 0; int n = 0; double cx = 0, cz = 0;
            unchecked
            {
                for (int i = 0; i < ls.Length; i++)
                {
                    if (ls[i] == null) continue;
                    Vector3 p = ls[i].transform.position;
                    n++; cx += p.x; cz += p.z;
                    h1 += (uint)Mathf.RoundToInt(p.x) * 73856093u ^ (uint)Mathf.RoundToInt(p.y) * 19349663u ^ (uint)Mathf.RoundToInt(p.z) * 83492791u;
                }
            }
            CultureInfo inv = CultureInfo.InvariantCulture;
            BtcLog.Info("RSync", "m=" + (mode == 0 ? "rec" : "play") + " t=" + bucket + " at=" + t.ToString("0.00", inv)
                + " ts=" + Time.timeScale.ToString("0.##", inv) + " units=" + n + " h1=" + h1.ToString("x8")
                + " c=" + (n > 0 ? (cx / n).ToString("0.00", inv) + "," + (cz / n).ToString("0.00", inv) : "-"));
        }
    }
}
#endif
