#if DEV
using System;
using System.Collections.Generic;
using Brix.Assets;
using Brix.Engine;
using Brix.External.Factories;
using Brix.Game;
using Brix.Game.AI;
using Brix.Game.Network;
using Brix.Lua;
using Brix.Network;
using UnityEngine.Networking;
using UnityEngine;

namespace BetterThanCastle.Dev
{
    /// <summary>
    /// DEV only. Local single-player match automation for BtcDevCmd: newgame (same path as the menu Start button:
    /// GameParam.Map/Gamemode/Preset + GameParam.StartGame), game-ready check, stress spawning (LuaFactory.InstantiateFromCLR, the
    /// path Lua's Key:New uses), simple build orders (UNetBlueprint.CallCmdPlaceBlueprint, the path Lua's Factory.PlaceBlueprint uses).
    /// </summary>
    internal static class BtcDevMatch
    {
        // Default = Info/DebugLaunchSettings.txt of the game (sandbox on the first official map).
        private const string DefMap = "PlateauMont-Royal_34885478-77e1-4597-ac79-de48e3f5f9a1";
        private const string DefMode = "sandbox";

        private sealed class SpawnJob { public string Name; public Factory.AssetKey Key; public bool IsUnit; public int Left; public int Done; }
        private static readonly Queue<SpawnJob> Jobs = new Queue<SpawnJob>();
        private static int _spawnedTotal;
        private static bool _started;
        private static float _startedAt;

        internal static bool Started { get { return _started; } }
        internal static int Pending { get { int n = 0; foreach (SpawnJob j in Jobs) n += j.Left; return n; } }

        // ---------------- newgame ----------------
        internal static void NewGame(string mapArg, string modeArg = null, string presetArg = null)
        {
            if (!Pick(mapArg, modeArg, presetArg, AssetPlayableContext.Singleplayer)) return;
            _started = true; _startedAt = Time.realtimeSinceStartup;
            GameParam.StartGame(null);
        }

        /// <summary>Resolves map/mode/preset (names are substrings; default = official first map) and writes them into GameParam.</summary>
        internal static bool Pick(string mapArg, string modeArg, string presetArg, AssetPlayableContext ctx)
        {
            if (GameParam.instance == null) { BtcLog.Warn("Match", "GameParam.instance == null (not in main menu yet)"); return false; }
            Asset_Map map = null;
            if (!string.IsNullOrEmpty(mapArg) && mapArg != "default")
            {
                List<Asset_Map> all = AssetDispensary.maps.SendQuery(AssetQueryParameters.AllSources, ctx, false);
                for (int i = 0; i < all.Count && map == null; i++)
                    if (all[i].codename != null && all[i].codename.IndexOf(mapArg, StringComparison.OrdinalIgnoreCase) >= 0
                        || all[i].displayName != null && all[i].displayName.IndexOf(mapArg, StringComparison.OrdinalIgnoreCase) >= 0) map = all[i];
            }
            string modeName = string.IsNullOrEmpty(modeArg) ? DefMode : modeArg.Replace(' ', '_');
            if (modeName == "conquest_mod") modeName = "conquest_mod";
            Asset_Gamemode mode = AssetDispensary.gamemodes.SendCodenameQuery(modeName, AssetQueryParameters.AllSources, AssetPlayableContext.Any);
            if (mode == null && modeName.IndexOf('_') >= 0)
                mode = AssetDispensary.gamemodes.SendCodenameQuery(modeName.Replace('_', ' '), AssetQueryParameters.AllSources, AssetPlayableContext.Any);
            if (mode == null) BtcLog.Warn("Match", "gamemode '" + modeName + "' not found, falling back");
            if (map == null && mode != null && modeName != DefMode)
            {
                // Prefer a default-ish map that supports the requested mode in single player.
                Asset_Map def = AssetDispensary.maps.SendCodenameQuery(DefMap);
                if (def != null && SupportsMode(def, mode, ctx)) map = def;
                else
                {
                    List<Asset_Map> all = AssetDispensary.maps.SendQuery(AssetQueryParameters.AllSources, ctx, false);
                    for (int i = 0; i < all.Count && map == null; i++) if (SupportsMode(all[i], mode, ctx)) map = all[i];
                }
            }
            if (map == null) map = AssetDispensary.maps.SendCodenameQuery(DefMap);
            if (map == null)
            {
                List<Asset_Map> all = AssetDispensary.maps.SendQuery(AssetQueryParameters.AllSources, ctx, false);
                if (all.Count > 0) map = all[0];
            }
            if (map == null) { BtcLog.Error("Match", "no map found"); return false; }
            if (mode == null) mode = AssetDispensary.gamemodes.SendCodenameQuery(DefMode, AssetQueryParameters.AllSources, AssetPlayableContext.Any);
            if (mode == null)
            {
                List<Asset_Gamemode> gms = map.GetGamemodes(ctx);
                if (gms != null && gms.Count > 0) mode = gms[0];
            }
            if (mode == null) { BtcLog.Error("Match", "no gamemode for map " + map.codename); return false; }
            Asset_GamemodePreset preset = null;
            if (!string.IsNullOrEmpty(presetArg))
            {
                try
                {
                    List<Asset_GamemodePreset> ps = mode.GetPresets(AssetQueryParameters.AllSources, AssetPlayableContext.Any);
                    for (int i = 0; ps != null && i < ps.Count && preset == null; i++)
                        if (ps[i].codename != null && ps[i].codename.IndexOf(presetArg, StringComparison.OrdinalIgnoreCase) >= 0) preset = ps[i];
                }
                catch (Exception e) { BtcLog.Warn("Match", "preset lookup: " + e.Message); }
            }
            if (preset == null) preset = mode.GetDefaultPreset(AssetQueryParameters.AllSources, AssetPlayableContext.Any);            GameParam.Map = map;
            GameParam.Gamemode = mode;
            GameParam.Preset = preset;
            BtcLog.Info("Match", "newgame map=" + map.codename + " mode=" + mode.codename + " preset=" + (preset == null ? "null" : preset.codename));
            BtcLog.Info("Match", "scene=" + UnityEngine.SceneManagement.SceneManager.GetActiveScene().name + "#" + UnityEngine.SceneManagement.SceneManager.GetActiveScene().buildIndex + " lobbyScene=" + UNetManager.instance.lobbyScene + " playScene=" + UNetManager.instance.playScene);
            return true;
        }

        private static bool SupportsMode(Asset_Map m, Asset_Gamemode mode, AssetPlayableContext ctx)
        {
            try
            {
                List<Asset_Gamemode> g = m.GetGamemodes(ctx);
                for (int i = 0; g != null && i < g.Count; i++) if (g[i].codename == mode.codename) return true;
            }
            catch (Exception) { }
            return false;
        }

        /// <summary>Main menu is up (not in a session, not in the play scene) and the title/intro phase is over.</summary>
        internal static bool MenuReady()
        {
            try
            {
                if (Time.realtimeSinceStartup < 25f) return false;
                if (GameParam.instance == null || UNetManager.instance == null) return false;
                if (NetworkServer.active || NetworkClient.active) return false;
                return UnityEngine.SceneManagement.SceneManager.GetActiveScene().buildIndex != 2;
            }
            catch (Exception) { return false; }
        }

        // ---------------- readiness ----------------
        internal static string ReadyState()
        {
            bool commence = false, phys = false; User u = null;
            try { commence = Architecte.commence; phys = Architecte.physicsReady; u = User.LocalUser; } catch (Exception) { }
            return "commence=" + commence + " physics=" + phys + " user=" + (u != null) + " faction=" + (u != null && u.faction != null) + " anchor=" + (Anchor() != Vector3.zero);
        }

        internal static bool Ready()
        {
            try
            {
                if (!Architecte.commence || !Architecte.physicsReady) return false;
                User u = User.LocalUser;
                if (u == null || u.faction == null) return false;
                return Anchor() != Vector3.zero;
            }
            catch (Exception) { return false; }
        }

        private static Vector3 Anchor()
        {
            try
            {
                User u = User.LocalUser;
                if (u == null || u.faction == null) return Vector3.zero;
                LuaStaticDriver sd;
                if (LuaDriverHQ.AllStatics.TryGetValue("fy_Crystal", out sd))
                {
                    LuaDriver d = sd.GetFirstOfFaction(u.faction);
                    if (d != null) return d.transform.position;
                }
                Labor[] ls = UnityEngine.Object.FindObjectsOfType<Labor>();
                for (int i = 0; i < ls.Length; i++)
                    if (ls[i] != null && u.faction.IsSame(ls[i].gameObject)) return ls[i].transform.position;
            }
            catch (Exception) { }
            return Vector3.zero;
        }

        // ---------------- spawn ----------------
        private static bool Resolve(string what, out Factory.AssetKey key, out bool unit)
        {
            key = null; unit = false;
            string w = what.ToLowerInvariant();
            string fy = null;
            if (w == "bricktron" || w == "worker" || w == "bricktrons") fy = "fy_Bricktron";
            else if (w == "biftron") fy = "fy_Biftron";
            else if (w == "corruptron") fy = "fy_Corruptron";
            else if (w == "minitron") fy = "fy_Minitron";
            else if (w == "firefly") fy = "fy_Firefly";
            else if (w.StartsWith("fy_")) fy = what;
            if (fy != null)
            {
                LuaStaticDriver sd;
                if (!LuaDriverHQ.AllStatics.TryGetValue(fy, out sd) || sd == null) { BtcLog.Warn("Match", "no static driver " + fy); return false; }
                key = sd.AssetKey; unit = fy != "fy_Firefly";
                return key != null;
            }
            string fac = "ObjetsDynamiques", nm = what;
            int dot = what.IndexOf('.');
            if (dot > 0) { fac = what.Substring(0, dot); nm = what.Substring(dot + 1); }
            else if (w == "stockpile" || w == "pallet" || w == "storage") nm = "Palette";
            else if (w == "rack" || w == "weaponrack" || w == "toolrack") nm = "Toolrack";
            else if (w == "chest") nm = "Chest";
            else if (w == "workbench") nm = "Workbench";
            else if (w == "furnace") nm = "Furnace";
            key = new Factory.AssetKey(fac, nm);
            return true;
        }

        internal static void QueueSpawn(string what, int n)
        {
            Factory.AssetKey key; bool unit;
            if (!Resolve(what, out key, out unit)) return;
            SpawnJob j = new SpawnJob(); j.Name = what; j.Key = key; j.IsUnit = unit; j.Left = n;
            Jobs.Enqueue(j);
            BtcLog.Info("Match", "queued spawn " + what + " x" + n + " key=" + key.Factory + "." + key.Name);
        }

        /// <summary>Called every frame from BtcDevCmd; spawns at most perFrame entities.</summary>
        internal static void Pump(int perFrame)
        {
            WalkTick();
            if (Jobs.Count == 0) return;
            Vector3 anchor = Anchor();
            if (anchor == Vector3.zero) return;
            Faction f = User.LocalUser.faction;
            for (int k = 0; k < perFrame && Jobs.Count > 0; k++)
            {
                SpawnJob j = Jobs.Peek();
                try
                {
                    XYZ p = Architecte.PigerUneCase(anchor, j.IsUnit ? 14f : 24f, 30);
                    Vector3 pos = p.Equals(XYZ.zero) ? anchor + new Vector3(UnityEngine.Random.Range(-3f, 3f), 1f, UnityEngine.Random.Range(-3f, 3f)) : p.ToVector3() + Vector3.up * 0.5f;
                    LuaFactory.InstantiateFromCLR(j.Key, f, pos, Quaternion.Euler(0f, UnityEngine.Random.Range(0f, 360f), 0f));
                    j.Done++; _spawnedTotal++;
                }
                catch (Exception e)
                {
                    BtcLog.Error("Match", "spawn " + j.Name + " failed after " + j.Done + ": " + e.Message);
                    j.Left = 0;
                }
                j.Left--;
                if (j.Left <= 0)
                {
                    Jobs.Dequeue();
                    BtcLog.Info("Match", "spawn " + j.Name + " done " + j.Done + " (total " + _spawnedTotal + ")");
                }
            }
        }


        // ---------------- walk (movement correctness A/B) ----------------
        // walk init <n>: picks n local units, remembers start S and a valid destination D each. walk go <tag>: sends all to D (then S, alternating)
        // through the same order the game uses (Order.Combat.MoveAttack) and logs arrival time + final position per unit.
        private static readonly List<Labor> WkL = new List<Labor>();
        private static readonly List<Vector3> WkS = new List<Vector3>(), WkD = new List<Vector3>();
        private static float[] _wkArr; private static Vector3[] _wkTgt;
        private static bool _wkAct, _wkToD = true; private static float _wkT0, _wkLimit; private static string _wkTag;

        internal static void Walk(string sub, string tag, int n)
        {
            User u = User.LocalUser; Vector3 a = Anchor();
            if (u == null || a == Vector3.zero) { BtcLog.Warn("Walk", "game not ready"); return; }
            if (sub == "init")
            {
                WkL.Clear(); WkS.Clear(); WkD.Clear(); _wkToD = true;
                Labor[] ls = UnityEngine.Object.FindObjectsOfType<Labor>();
                List<Labor> mine = new List<Labor>();
                for (int i = 0; i < ls.Length; i++)
                    if (ls[i] != null && u.faction.IsSame(ls[i].gameObject) && ls[i].GetComponent<Locomotion4>() != null && ls[i].GetComponent<Animator>() != null) mine.Add(ls[i]);
                mine.Sort(delegate (Labor x, Labor y) { int c = x.transform.position.x.CompareTo(y.transform.position.x); return c != 0 ? c : x.transform.position.z.CompareTo(y.transform.position.z); });
                for (int i = 0; i < mine.Count && WkL.Count < n; i++)
                {
                    XYZ p = Architecte.PigerUneCase(a, 14f, 30);
                    if (p.Equals(XYZ.zero)) continue;
                    WkL.Add(mine[i]); WkS.Add(mine[i].transform.position); WkD.Add(p.ToVector3());
                }
                BtcLog.Info("Walk", "init units=" + WkL.Count + "/" + mine.Count + "/" + ls.Length + " first=" + (WkL.Count > 0 ? WkL[0].name : "-"));
                return;
            }
            if (WkL.Count == 0) { BtcLog.Warn("Walk", "no units (walk init first)"); return; }
            int cnt = WkL.Count;
            _wkArr = new float[cnt]; _wkTgt = new Vector3[cnt];
            for (int i = 0; i < cnt; i++)
            {
                _wkArr[i] = -1f; _wkTgt[i] = _wkToD ? WkD[i] : WkS[i];
                try { Order.Combat.MoveAttack.Send(WkL[i], new SpaceLocation(_wkTgt[i], true), Labor.DecisionSource.User); }
                catch (Exception e) { BtcLog.Error("Walk", "order: " + e.Message); }
            }
            _wkTag = tag + (_wkToD ? "->D" : "->S"); _wkToD = !_wkToD; _wkAct = true; _wkT0 = Time.time; _wkLimit = Time.time + 45f;
        }

        private static void WalkTick()
        {
            if (!_wkAct) return;
            int done = 0, cnt = WkL.Count;
            for (int i = 0; i < cnt; i++)
            {
                if (_wkArr[i] >= 0f) { done++; continue; }
                if (WkL[i] == null) { _wkArr[i] = 99f; done++; continue; }
                Vector3 d = WkL[i].transform.position - _wkTgt[i]; d.y = 0f;
                if (d.magnitude < 1.3f) { _wkArr[i] = Time.time - _wkT0; done++; }
            }
            if (done < cnt && Time.time < _wkLimit) return;
            _wkAct = false;
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            float sum = 0f, mx = 0f; int ok = 0;
            for (int i = 0; i < cnt; i++)
            {
                Vector3 pp = WkL[i] != null ? WkL[i].transform.position : Vector3.zero;
                sb.Append(' ').Append(i).Append(':').Append(_wkArr[i] >= 0f ? _wkArr[i].ToString("0.0") : "x").Append('@').Append(pp.x.ToString("0.0")).Append(',').Append(pp.z.ToString("0.0"));
                if (_wkArr[i] >= 0f && _wkArr[i] < 99f) { sum += _wkArr[i]; if (_wkArr[i] > mx) mx = _wkArr[i]; ok++; }
            }
            Animator an = cnt > 0 && WkL[0] != null ? WkL[0].GetComponent<Animator>() : null;
            string line = "Walk " + _wkTag + " mode=" + (an != null ? an.cullingMode.ToString() : "?") + " arrived=" + ok + "/" + cnt + " tmean=" + (ok > 0 ? sum / ok : 0f).ToString("0.00") + " tmax=" + mx.ToString("0.0") + " |" + sb;
            BtcLog.Info("Walk", line);
            try { System.IO.File.AppendAllText(System.IO.Path.Combine(BtcDevPaths.LogDir, "engine.txt"), line + System.Environment.NewLine); } catch { }
        }
        // ---------------- orders ----------------
        /// <summary>order build &lt;n&gt; [Factory.Name]: n block blueprints on a grid next to the home crystal (creates build tasks).</summary>
        internal static void OrderBuild(int n, string key)
        {
            User u = User.LocalUser; Vector3 a = Anchor();
            if (u == null || a == Vector3.zero) { BtcLog.Warn("Match", "order build: game not ready"); return; }
            UNetBlueprint ub = u.GetComponent<UNetBlueprint>();
            if (ub == null) { BtcLog.Warn("Match", "order build: no UNetBlueprint"); return; }
            string fac = "Blocks", nm = "Stone_Brick";
            if (!string.IsNullOrEmpty(key)) { int d = key.IndexOf('.'); if (d > 0) { fac = key.Substring(0, d); nm = key.Substring(d + 1); } else nm = key; }
            // Vanilla CmdPlaceBlueprint is broken (uses base.name as key), so go through CmdSpawnPlacedBlueprint with the block's blueprint key. No BuildProject: experimental, may not create worker tasks.
            string bpFac = fac, bpName = nm;
            try { Factory.AssetKey bk = BlockInfo.GetFromKey(new Factory.AssetKey(fac, nm)).BlueprintKey; bpFac = bk.Factory; bpName = bk.Name; }
            catch (Exception e) { BtcLog.Warn("Match", "no blueprint key for " + fac + "." + nm + ": " + e.Message); }
            int side = Mathf.CeilToInt(Mathf.Sqrt(n)), placed = 0;
            for (int i = 0; i < n; i++)
            {
                Vector3 pos = new Vector3(Mathf.Round(a.x) + 6 + (i % side), Mathf.Round(a.y), Mathf.Round(a.z) + 6 + (i / side));
                try { ub.CallCmdSpawnPlacedBlueprint(bpFac, bpName, pos, Quaternion.identity, false, null, false); placed++; }
                catch (Exception e) { BtcLog.Error("Match", "place blueprint: " + e.Message); break; }
            }
            BtcLog.Info("Match", "order build " + fac + "." + nm + " placed " + placed + "/" + n);
        }
    }
}
#endif
