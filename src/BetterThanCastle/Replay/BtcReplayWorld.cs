using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using Brix.Game;
using Brix.Game.Components;
using HarmonyLib;
using UnityEngine;
using UnityEngine.Networking;

namespace BetterThanCastle
{
    /// <summary>
    /// Replay spectator extras. (1) Blueprints of ALL players: the vanilla server replicates a blueprint only to users of its own faction, so the REC
    /// spectator (the fake client the recording is made from) never received the others. While recording, Blueprint.OnRebuildObservers also lists the
    /// recording connections (local, in-process: nothing changes on the wire to real players), so every blueprint is part of the replay as a normal object with
    /// its normal ghost look. In playback the ghost material is re-tinted with the owner colour (the game paints all of them blue) and the "Blueprints"
    /// button shows or hides them (hidden = renderers off, no per-frame work). (2) Crafting queues: read live from the station components while Alt is held.
    /// Everything is a no-op outside replay playback / recording. No wire traffic.
    /// </summary>
    internal static class BtcReplayWorld
    {
        internal static readonly List<Blueprint> Blueprints = new List<Blueprint>();
        private static readonly HashSet<int> BpIds = new HashSet<int>();
        internal static readonly List<CraftingStation> Stations = new List<CraftingStation>();
        private static readonly HashSet<int> StIds = new HashSet<int>();
        private static bool _lastInfo = true;
        private static float _nextDiag;
        private static int _recolored;

        internal static void Register(Blueprint b)
        {
            if (b != null && BpIds.Add(b.GetInstanceID())) Blueprints.Add(b);
        }

        internal static void Register(CraftingStation s)
        {
            if (s != null && StIds.Add(s.GetInstanceID())) Stations.Add(s);
        }

        // ------------------------------------------------------------------ blueprints: tint + visibility (playback only)
        private static readonly Dictionary<string, BlueprintMaterialCache.BlueprintDisplay> Displays = new Dictionary<string, BlueprintMaterialCache.BlueprintDisplay>();
        private static readonly HashSet<int> Tinted = new HashSet<int>();
        private static readonly Dictionary<int, int> Tries = new Dictionary<int, int>();
        private static float _nextRetry;
        private static int _fails;
        private static int _dbg;
        private static int _reapplied;

        private static BlueprintMaterialCache.BlueprintDisplay DisplayFor(Color owner)
        {
            string k = ColorUtility.ToHtmlStringRGB(owner);
            BlueprintMaterialCache.BlueprintDisplay d;
            if (Displays.TryGetValue(k, out d)) return d;
            BindingFlags bf = BindingFlags.NonPublic | BindingFlags.Instance;
            object inst = typeof(BlueprintMaterialCache).GetField("instance", BindingFlags.NonPublic | BindingFlags.Static).GetValue(null);
            if (inst == null) return null;
            BlueprintMaterialCache.BlueprintDisplay game = (BlueprintMaterialCache.BlueprintDisplay)typeof(BlueprintMaterialCache).GetField("placed_valid", bf).GetValue(inst);
            BlueprintMaterialCache.MaterialTrio refs = (BlueprintMaterialCache.MaterialTrio)typeof(BlueprintMaterialCache.BlueprintDisplay).GetField("references", bf).GetValue(game);
            Color c = owner;
            c.a = game.color.a;
            d = new BlueprintMaterialCache.BlueprintDisplay("btc_" + k, refs, c, 1f);
            Displays[k] = d;
            return d;
        }

        private sealed class Ghost
        {
            internal Blueprint Bp;
            internal StateRenderer Sr;
            internal MeshRenderer First;
            internal Material Mine;       // first material we applied (reference check)
        }

        private static readonly Dictionary<int, Ghost> Ghosts = new Dictionary<int, Ghost>();
        private static FieldInfo _passesField;

        private static BlueprintMaterialCache.MaterialTrio.Mode PassesOf(StateRenderer sr)
        {
            try
            {
                if (_passesField == null) _passesField = typeof(StateRenderer).GetField("passes", BindingFlags.NonPublic | BindingFlags.Instance);
                if (_passesField != null) return (BlueprintMaterialCache.MaterialTrio.Mode)_passesField.GetValue(sr);
            }
            catch { }
            return BlueprintMaterialCache.MaterialTrio.Mode.ThreePass;
        }

        /// <summary>StateRenderer.UpdateRenderersMaterial postfix (playback): owner-coloured ghost, or hidden when the Blueprints button is off.</summary>
        internal static void Recolor(StateRenderer sr, PlacementFlags mode)
        {
            if (BtcReplay.Mode != BtcReplay.State.Playing || sr == null) return;
            Blueprint bp = sr.GetComponent<Blueprint>();
            if (bp == null) return;
            Apply(bp, sr, mode);
        }

        private static void Apply(Blueprint bp, StateRenderer sr, PlacementFlags st)
        {
            bool on = BtcConfig.ReplayInfo == null || BtcConfig.ReplayInfo.Value;
            MeshRenderer[] rs = sr.GetComponentsInChildren<MeshRenderer>(true);
            int id = bp.GetInstanceID();
            if (!on)
            {
                for (int i = 0; i < rs.Length; i++) if (rs[i] != null) rs[i].enabled = false;
                Ghosts.Remove(id);
                return;
            }
            try
            {
                // only the plain valid look is re-tinted; anti (orange), invalid (red) and removal (yellow) keep the game colours
                bool plain = !st.HasFlag(PlacementFlags.anti) && st.HasFlag(PlacementFlags.valid) && !st.HasFlag(PlacementFlags.mayRemove);
                for (int i = 0; i < rs.Length; i++) if (rs[i] != null) rs[i].enabled = true;
                if (!plain) { Ghosts.Remove(id); return; }
                Faction f = Faction.GetFaction(bp.gameObject);
                if (!BtcMatchStats.Tracks(f)) return;
                BlueprintMaterialCache.BlueprintDisplay d = DisplayFor(f.FactionColor);
                if (d == null) return;
                Material[] mats = d.ForKey(bp.AssetKey, true).WithPasses(PassesOf(sr));
                for (int i = 0; i < rs.Length; i++) if (rs[i] != null) rs[i].sharedMaterials = mats;
                Ghost g;
                if (!Ghosts.TryGetValue(id, out g)) { g = new Ghost(); Ghosts[id] = g; }
                g.Bp = bp; g.Sr = sr; g.First = rs.Length > 0 ? rs[0] : null; g.Mine = mats.Length > 0 ? mats[0] : null;
                Tinted.Add(id);
                _recolored++;
            }
            catch (Exception e) { if (_fails++ < 3) BtcLog.Warn("ReplayWorld", "recolor: " + e.Message); }
        }

        /// <summary>Once a second: a tinted ghost whose material was swapped back by some other game path (placing more structures re-validates the old ones) is tinted again.</summary>
        private static void Verify()
        {
            List<int> drop = null;
            foreach (KeyValuePair<int, Ghost> kv in Ghosts)
            {
                Ghost g = kv.Value;
                if (g.Bp == null || g.Sr == null || g.First == null) { if (drop == null) drop = new List<int>(); drop.Add(kv.Key); continue; }
                if (g.First.sharedMaterial == g.Mine) continue;
                _reapplied++;
                try { Apply(g.Bp, g.Sr, g.Bp.State); } catch { }
            }
            if (drop != null) for (int i = 0; i < drop.Count; i++) Ghosts.Remove(drop[i]);
        }

        /// <summary>Every frame (BtcPovRec.PlayTick): when the Blueprints button flips, refresh the existing blueprints once.</summary>
        internal static void Tick()
        {
            if (BtcReplay.Mode != BtcReplay.State.Playing) return;
            bool on = BtcConfig.ReplayInfo != null && BtcConfig.ReplayInfo.Value;
            if (on != _lastInfo)
            {
                _lastInfo = on;
                for (int i = Blueprints.Count - 1; i >= 0; i--)
                {
                    Blueprint b = Blueprints[i];
                    if (b == null) { Blueprints.RemoveAt(i); continue; }
                    try { b.RefreshVisual(); } catch { }
                }
            }
            if (on && Time.unscaledTime >= _nextRetry)
            {
                // the faction of a ghost arrives after the first material pass: retry the untinted ones a few times
                _nextRetry = Time.unscaledTime + 1f;
                for (int i = Blueprints.Count - 1; i >= 0; i--)
                {
                    Blueprint b = Blueprints[i];
                    if (b == null) { Blueprints.RemoveAt(i); continue; }
                    int id = b.GetInstanceID();
                    if (Tinted.Contains(id) || !b.gameObject.activeInHierarchy) continue;
                    int n; Tries.TryGetValue(id, out n);
                    if (n >= 12) continue;
                    Tries[id] = n + 1;
                    if (BtcMatchStats.Tracks(Faction.GetFaction(b.gameObject))) { try { b.RefreshVisual(); } catch { } }
                }
                Verify();
            }
            if (Time.unscaledTime >= _nextDiag)
            {
                _nextDiag = Time.unscaledTime + 8f;
                int alive = 0, withQ = 0;
                for (int i = 0; i < Blueprints.Count; i++) if (Blueprints[i] != null) alive++;
                for (int i = 0; i < Stations.Count; i++) if (Stations[i] != null && Stations[i].QueueCount > 0) withQ++;
                BtcLog.Info("ReplayWorld", "playback: blueprints=" + alive + " recolored=" + _recolored + " reapplied=" + _reapplied + " on=" + on + " stations=" + Stations.Count + " withQueue=" + withQ);
            }
        }

        // ------------------------------------------------------------------ crafting queues (Alt)
        private static GUIStyle _lab;
        private sealed class QText { internal string Text; internal float Next; }
        private static readonly Dictionary<int, QText> QCache = new Dictionary<int, QText>();
        private static readonly Dictionary<string, int> QCount = new Dictionary<string, int>();
        private static readonly List<string> QOrder = new List<string>();
        private static readonly StringBuilder Sb = new StringBuilder(128);

        private static GUIStyle Label(float fs, Color c)
        {
            if (_lab == null) { _lab = new GUIStyle(GUI.skin.label); _lab.fontStyle = FontStyle.Bold; _lab.alignment = TextAnchor.UpperLeft; _lab.clipping = TextClipping.Overflow; _lab.wordWrap = false; }
            _lab.fontSize = Mathf.Max(10, (int)(fs * 0.55f));
            _lab.normal.textColor = c;
            return _lab;
        }

        /// <summary>Playback, OnGUI: while Alt is held, the production queue of every crafting building in view (owner colour bar).</summary>
        internal static void Draw(float fs)
        {
            if (BtcReplay.Mode != BtcReplay.State.Playing || BtcReplay.Seeking || BtcReplay.Loading) return;
            if (!BtcAutoMarks.AltDown || Event.current.type != EventType.Repaint) return;
            Camera cam = Camera.main;
            if (cam == null) return;
            float now = Time.unscaledTime;
            Vector3 cp = cam.transform.position;
            for (int i = Stations.Count - 1; i >= 0; i--)
            {
                CraftingStation st = Stations[i];
                if (st == null) { Stations.RemoveAt(i); continue; }
                if (!st.gameObject.activeInHierarchy) continue;
                Vector3 pos = st.transform.position;
                if ((pos - cp).sqrMagnitude > 200f * 200f) continue;
                Vector3 sp = cam.WorldToScreenPoint(pos + Vector3.up * 3f);
                if (sp.z <= 0f || sp.x < -100f || sp.x > Screen.width + 100f || sp.y < -100f || sp.y > Screen.height + 100f) continue;
                int id = st.GetInstanceID();
                QText q;
                if (!QCache.TryGetValue(id, out q)) { q = new QText(); QCache[id] = q; }
                if (now >= q.Next) { q.Next = now + 0.5f; q.Text = QueueText(st); }
                if (string.IsNullOrEmpty(q.Text)) continue;
                Faction f = Faction.GetFaction(st.gameObject);
                Color c = BtcMatchStats.Tracks(f) ? f.FactionColor : Color.white;
                int lines = 1;
                for (int k = 0; k < q.Text.Length; k++) if (q.Text[k] == '\n') lines++;
                float w = fs * 9f, h = fs * 0.78f * lines + 6f;
                Rect r = new Rect(sp.x - w * 0.5f, Screen.height - sp.y - h, w, h);
                Color old = GUI.color;
                GUI.color = new Color(0f, 0f, 0f, 0.7f);
                GUI.DrawTexture(r, BtcUiStyle.White);
                GUI.color = new Color(c.r, c.g, c.b, 0.95f);
                GUI.DrawTexture(new Rect(r.x, r.y, 4f, r.height), BtcUiStyle.White);
                GUI.color = old;
                Rect tr = new Rect(r.x + 8f, r.y + 2f, r.width - 8f, r.height);
                GUI.Label(new Rect(tr.x + 1f, tr.y + 1f, tr.width, tr.height), q.Text, Label(fs, new Color(0f, 0f, 0f, 0.85f)));
                GUI.Label(tr, q.Text, Label(fs, Color.white));
            }
        }

        private static string QueueText(CraftingStation st)
        {
            QCount.Clear(); QOrder.Clear();
            try
            {
                List<RecipeInfo> l = st.QueuedRecipes;
                if (l == null || l.Count == 0) return null;
                for (int i = 0; i < l.Count && i < 40; i++)
                {
                    RecipeInfo r = l[i];
                    if (r == null || r.Creates == null) continue;
                    string n = BtcMatchStats.Pretty(r.Creates.Name);
                    int c;
                    if (!QCount.TryGetValue(n, out c)) QOrder.Add(n);
                    QCount[n] = c + Mathf.Max(1, r.QtyCreated);
                }
                Sb.Length = 0;
                for (int i = 0; i < QOrder.Count && i < 4; i++)
                {
                    if (i > 0) Sb.Append('\n');
                    Sb.Append(i == 0 ? "> " : "  ").Append(QOrder[i]);
                    if (QCount[QOrder[i]] > 1) Sb.Append(" x").Append(QCount[QOrder[i]]);
                }
                if (QOrder.Count > 4) Sb.Append("\n  +").Append(QOrder.Count - 4);
                return Sb.ToString();
            }
            catch { return null; }
        }
    }

    [HarmonyPatch(typeof(Blueprint), "OnStartAnywhere")]
    internal static class ReplayWorldBlueprintPatch
    {
        private static void Postfix(Blueprint __instance)
        {
            try { BtcReplayWorld.Register(__instance); }
            catch (Exception e) { BtcLog.Error("Hook", "ReplayWorldBlueprint: " + e.Message); }
        }
    }

    [HarmonyPatch(typeof(CraftingStation), "Initialize")]
    internal static class ReplayWorldStationPatch
    {
        private static void Postfix(CraftingStation __instance)
        {
            try { BtcReplayWorld.Register(__instance); }
            catch (Exception e) { BtcLog.Error("Hook", "ReplayWorldStation: " + e.Message); }
        }
    }

    /// <summary>Recording: the REC spectator connections also observe every blueprint (vanilla: only the owner faction). Local, in-process connections only.</summary>
    [HarmonyPatch(typeof(Blueprint), "OnRebuildObservers")]
    internal static class ReplayWorldObserversPatch
    {
        private static void Postfix(HashSet<NetworkConnection> observers)
        {
            try { BtcReplay.AddRecObservers(observers); }
            catch (Exception e) { BtcLog.Error("Hook", "ReplayWorldObservers: " + e.Message); }
        }
    }

    /// <summary>Playback: owner tint / hide for blueprint ghosts.</summary>
    [HarmonyPatch(typeof(StateRenderer), "UpdateRenderersMaterial")]
    internal static class ReplayWorldRecolorPatch
    {
        private static void Postfix(StateRenderer __instance, PlacementFlags mode)
        {
            try { BtcReplayWorld.Recolor(__instance, mode); }
            catch (Exception e) { BtcLog.Error("Hook", "ReplayWorldRecolor: " + e.Message); }
        }
    }
}
