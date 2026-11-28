using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Brix.Assets;
using Brix.Game.Components;
using Brix.Game.Components.Validators.Graphs;
using Brix.UI.GameSelector;
using UnityEngine;

namespace BetterThanCastle
{
    /// Anchor point of compound blueprints: the block that sits under the cursor while the compound is placed (vanilla: the centre of all
    /// marks at the lowest level). Before saving: hover one of the selected marks and press bp_anchor (that mark becomes the anchor).
    /// While placing: press bp_anchor with the cursor over another block of the plan (nearest preview block on screen) to move the anchor;
    /// the new anchor is kept for further placements of that blueprint in this session (saved with the blueprint on the next save).
    /// Stored as "@anchor x y z" (graph space = ValidationNode.Pos) in btc-presets.txt of the compound folder. Local only, no net.
    internal static partial class BtcBpPreset
    {
        private static readonly Dictionary<BlueprintGraph, Vector3> Anchors = new Dictionary<BlueprintGraph, Vector3>();
        private static readonly Dictionary<int, Vector3> BccLocal = new Dictionary<int, Vector3>();   // compound instance id -> anchor in its local space
        private static Blueprint _anchorBp;            // live mark chosen before saving
        private static BlueprintCompoundComponent _placing;
        private static BlueprintGraph _placingGraph;   // graph of the asset being placed (its nodes target the preview copies)
        private static bool _noShift;

        private static string Fv(Vector3 v)
        {
            CultureInfo c = CultureInfo.InvariantCulture;
            return v.x.ToString("F2", c) + " " + v.y.ToString("F2", c) + " " + v.z.ToString("F2", c);
        }

        /// GenerateBlueprintGraph postfix: the chosen live mark (or the anchor block of the compound being duplicated) is the anchor
        private static void OnGraphAnchor(BlueprintGraph g)
        {
            Anchors.Remove(g);
            Blueprint a = Live(_anchorBp);
            Transform anchorCopy = null;
            Vector3 l;
            if (_placing != null && BccLocal.TryGetValue(_placing.GetInstanceID(), out l)) anchorCopy = NearestChild(_placing.transform, l);
            foreach (ValidationNode n in g.Nodes)
            {
                if (n.Target == null) continue;
                if ((a != null && n.Target == a.gameObject) || (anchorCopy != null && n.Target.transform == anchorCopy))
                {
                    Anchors[g] = n.Pos;
                    BtcLog.Info("BpPreset", "compound anchor " + n.Type.Name + " at " + Fv(n.Pos));
                    return;
                }
            }
        }

        private static Transform NearestChild(Transform parent, Vector3 local)
        {
            Transform best = null;
            float bd = 0.3f;
            for (int i = 0; i < parent.childCount; i++)
            {
                Transform c = parent.GetChild(i);
                float d = (c.localPosition - local).sqrMagnitude;
                if (d < bd) { bd = d; best = c; }
            }
            return best;
        }

        private static void AppendAnchor(BlueprintGraph g, StringBuilder sb)
        {
            Vector3 a;
            if (g != null && Anchors.TryGetValue(g, out a)) sb.Append("@anchor ").Append(Fv(a)).Append('\n');
        }

        private static bool ReadAnchor(BlueprintGraph g, string line)
        {
            if (!line.StartsWith("@anchor ")) return false;
            string[] t = line.Substring(8).Trim().Split(' ');
            CultureInfo c = CultureInfo.InvariantCulture;
            float x, y, z;
            if (t.Length == 3 && float.TryParse(t[0], NumberStyles.Float, c, out x) && float.TryParse(t[1], NumberStyles.Float, c, out y) && float.TryParse(t[2], NumberStyles.Float, c, out z))
                Anchors[g] = new Vector3(x, y, z);
            return true;
        }

        /// LoadCompoundBlueprints postfix: anchor of the new compound in its local space (the preview block at the anchor node)
        internal static void OnCompoundLoaded(Asset_Blueprints a, BlueprintCompoundComponent bcc)
        {
            if (bcc == null) return;
            _placing = bcc;
            _placingGraph = a != null ? a.Graph : null;
            BccLocal.Remove(bcc.GetInstanceID());
            Vector3 an;
            if (a == null || a.Graph == null || !Anchors.TryGetValue(a.Graph, out an) || bcc.Graph == null) return;
            ValidationNode best = null;
            float bd = 0.3f;
            foreach (ValidationNode n in bcc.Graph.Nodes)
            {
                float d = (n.Pos - an).sqrMagnitude;
                if (n.Target != null && d < bd) { bd = d; best = n; }
            }
            if (best != null) BccLocal[bcc.GetInstanceID()] = best.Target.transform.localPosition;
        }

        /// BlueprintCompoundComponent.MoveAt prefix: the anchor (not the centre) lands on the cursor point
        internal static void BeforeMoveAt(BlueprintCompoundComponent bcc, ref Vector3 p)
        {
            Vector3 l;
            if (_noShift || bcc == null || BccLocal.Count == 0 || !BccLocal.TryGetValue(bcc.GetInstanceID(), out l)) return;
            p -= bcc.transform.rotation * l;
        }

        internal static void NoShift(bool on) { _noShift = on; }

        private static KeyCode[] _selKeys;
        private static int _selFrame = -1;
        private static bool _selHeld;

        /// bp_select_all held (checked once per frame)
        internal static bool SelectAllHeld()
        {
            if (_selFrame == Time.frameCount) return _selHeld;
            _selFrame = Time.frameCount;
            KeyCode[] k = BtcHotkeys.GetKeys("bp_select_all", NoKeys);
            _selHeld = (k.Length > 0 && BtcHotkeys.AllHeld(k)) || (k.Length == 1 && k[0] == KeyCode.LeftAlt && (Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt)));
            return _selHeld;
        }

        internal static bool SelectableAcrossTasks(Blueprint b)
        {
            return b != null && b.State.HasFlag(PlacementFlags.placed) && IsMine(b);
        }

        private static KeyCode[] _anchorKeys;
        private static int _anchorFrame = -1;
        private static readonly KeyCode[] NoKeys = new KeyCode[0];

        /// per frame (BtcTimer.Tick): the anchor key
        internal static void AnchorTick()
        {
            if (_anchorFrame == Time.frameCount) return;   // called from several per-frame places
            _anchorFrame = Time.frameCount;
            KeyCode[] k = BtcHotkeys.GetKeys("bp_anchor", NoKeys);
            bool raw = Input.GetKeyDown(KeyCode.X) && (Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt));   // default combo, also when the bind system blocks it
            bool bind = k.Length > 0 && BtcHotkeys.ComboPressed(k);
            if (!raw && !bind) return;
            try
            {
                BlueprintCompoundComponent bcc = _placing;
                if (bcc != null && bcc.gameObject.activeInHierarchy && bcc.Graph != null) { MoveAnchor(bcc); return; }
                Blueprint b = MarkUnderCursor(false);
                if (b == null) { b = Live(_hover); if (b != null && !IsMine(b)) b = null; }
                if (b == null) b = NearestMark();
                BtcLog.Info("BpPreset", "anchor key (bind " + bind + ", raw " + raw + "): placing=" + (bcc != null) + " hover=" + (_hover != null) + " mark=" + (b != null ? b.AssetKey.Name : "none"));
                if (b == null) return;
                _anchorBp = ReferenceEquals(_anchorBp, b) ? null : b;
                BtcSfx.Click();
                BtcLog.Info("BpPreset", _anchorBp != null ? "anchor mark " + b.AssetKey.Name + " at " + b.transform.position : "anchor mark cleared");
            }
            catch (Exception e) { BtcLog.Error("BpPreset", "anchor key: " + e.Message); }
        }

        private static readonly RaycastHitComparer HitCmp = new RaycastHitComparer();
        private sealed class RaycastHitComparer : IComparer<RaycastHit> { public int Compare(RaycastHit a, RaycastHit b) { return a.distance.CompareTo(b.distance); } }

        /// own live mark hit by the camera ray through the cursor (colliders of the mark; nearest mark hit), stations only if asked
        internal static Blueprint MarkUnderCursor(bool stationOnly)
        {
            Camera cam = Camera.main;
            if (cam == null) return null;
            RaycastHit[] hits = Physics.RaycastAll(cam.ScreenPointToRay(Input.mousePosition), 600f, ~0, QueryTriggerInteraction.Collide);
            Array.Sort(hits, HitCmp);
            for (int i = 0; i < hits.Length; i++)
            {
                Blueprint b = hits[i].collider != null ? hits[i].collider.GetComponentInParent<Blueprint>() : null;
                if (b == null || KeyOf(b) == 0u || !IsMine(b)) continue;
                if (stationOnly && StationKind(b) < 0) return null;   // nearest own mark is not a station
                return b;
            }
            return null;
        }

        /// own mark nearest to the cursor on screen (within 40 px), when the hover callback did not give one
        private static Blueprint NearestMark()
        {
            Camera cam = Camera.main;
            if (cam == null) return null;
            Vector2 m = Input.mousePosition;
            Blueprint best = null;
            float bd = 40f * 40f;
            foreach (Blueprint b in UnityEngine.Object.FindObjectsOfType<Blueprint>())
            {
                if (b == null || KeyOf(b) == 0u || !IsMine(b)) continue;
                Vector3 s = cam.WorldToScreenPoint(b.transform.position);
                if (s.z <= 0f) continue;
                float d = (new Vector2(s.x, s.y) - m).sqrMagnitude;
                if (d < bd) { bd = d; best = b; }
            }
            return best;
        }

        private static void MoveAnchor(BlueprintCompoundComponent bcc)
        {
            Camera cam = Camera.main;
            if (cam == null) return;
            Vector2 m = Input.mousePosition;
            Transform best = null;
            float bd = 60f * 60f;
            for (int i = 0; i < bcc.transform.childCount; i++)
            {
                Transform c = bcc.transform.GetChild(i);
                Vector3 s = cam.WorldToScreenPoint(c.position);
                if (s.z <= 0f) continue;
                float d = (new Vector2(s.x, s.y) - m).sqrMagnitude;
                if (d < bd) { bd = d; best = c; }
            }
            if (best == null) return;
            BccLocal[bcc.GetInstanceID()] = best.localPosition;
            if (_placingGraph != null)
                foreach (ValidationNode n in _placingGraph.Nodes)
                    if (n.Target != null && n.Target.transform == best) { Anchors[_placingGraph] = n.Pos; break; }
            BtcSfx.Click();
            BtcLog.Info("BpPreset", "placement anchor moved to " + best.name);
        }

        /// IMGUI: a small marker on the anchor (chosen mark before saving, anchor block while placing)
        private static void DrawAnchor()
        {
            Camera cam = Camera.main;
            if (cam == null) return;
            Vector3 w;
            Vector3 l;
            BlueprintCompoundComponent bcc = _placing;
            if (bcc != null && bcc.gameObject.activeInHierarchy && BccLocal.TryGetValue(bcc.GetInstanceID(), out l)) w = bcc.transform.TransformPoint(l);
            else
            {
                Blueprint a = Live(_anchorBp);
                if (a == null) return;
                w = a.transform.position;
            }
            Vector3 s = cam.WorldToScreenPoint(w + Vector3.up * 0.5f);
            if (s.z <= 0f) return;
            float x = s.x, y = Screen.height - s.y;
            Color keep = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, 0.8f);
            GUI.DrawTexture(new Rect(x - 7f, y - 7f, 14f, 14f), Texture2D.whiteTexture);
            GUI.color = new Color(1f, 0.85f, 0.15f, 1f);
            GUI.DrawTexture(new Rect(x - 5f, y - 5f, 10f, 10f), Texture2D.whiteTexture);
            GUI.color = keep;
        }

#if DEV
        internal static void DevAnchor(Blueprint b) { _anchorBp = b; }
        internal static string DevAnchorState(BlueprintCompoundComponent bcc)
        {
            Vector3 l;
            return bcc != null && BccLocal.TryGetValue(bcc.GetInstanceID(), out l) ? "local " + l + " world " + bcc.transform.TransformPoint(l) : "no anchor";
        }
#endif

        internal static void OnPlaceDone(BlueprintCompoundComponent bcc)
        {
            if (bcc != null) BccLocal.Remove(bcc.GetInstanceID());
            if (ReferenceEquals(bcc, _placing)) _placing = null;
        }
    }
}
