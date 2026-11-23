using System;
using System.Collections.Generic;
using Brix.Game;
using Brix.Game.AI;
using Brix.Game.Components;
using Brix.Game.AI.Damage;
using UnityEngine;

namespace BetterThanCastle
{
    /// <summary>
    /// Focus feedback (Visuals > World overlays > Focus, BtcMenuState "focus:*", local only, no net): a hit sound when your focus order on an enemy
    /// unit really goes through (vanilla AcceptNewOrder said yes, or the "focus without auto-run" order was sent), and a marker above the focused
    /// unit while it lives and the units you gave the focus to have not received another order.
    /// </summary>
    internal static class BtcFocusFx
    {
        internal static bool Sound { get { return BtcMenuState.Bool("focus:snd", true); } }
        internal static bool Mark { get { return BtcMenuState.Bool("focus:mark", true); } }
        internal static int Volume { get { return Mathf.Clamp(BtcMenuState.Int("focus:vol", 70), 0, 100); } }
        internal static float Size { get { return Mathf.Clamp(BtcMenuState.Int("focus:size", 100), 50, 250) * 0.01f; } }

        private static GameObject _target;
        private static DamageReceiver _dr;
        private static Renderer _rend;
        private static readonly HashSet<int> _units = new HashSet<int>();
        private static readonly List<GameObject> _unitGos = new List<GameObject>();
        private static IntPtr _snd;
        private static int _sndVol = -1, _frame = -1;

        /// <summary>AcceptNewOrder postfix (accepted orders only) and BtcFocusHold.Issue.</summary>
        internal static void OnOrder(List<GameObject> actors, GameObject target)
        {
            try
            {
                if (actors == null || actors.Count == 0 || BtcAim.Active || BtcReplay.Mode == BtcReplay.State.Playing) return;
                if (IsFocus(actors, target))
                {
                    // "focus without auto-run" splits one click into two orders (ranged + the rest): merge them, one sound
                    bool sameClick = target == _target && Time.frameCount == _frame;
                    _frame = Time.frameCount;
                    if (sameClick)
                    {
                        for (int i = 0; i < actors.Count; i++)
                            if (actors[i] != null && _units.Add(actors[i].GetInstanceID())) _unitGos.Add(actors[i]);
                        return;
                    }
                    _target = target;
                    _dr = target.GetComponent<DamageReceiver>();
                    _rend = null;
                    Renderer[] rs = target.GetComponentsInChildren<Renderer>();
                    for (int i = 0; i < rs.Length && _rend == null; i++) if (rs[i] is SkinnedMeshRenderer) _rend = rs[i];
                    if (_rend == null && rs.Length > 0) _rend = rs[0];
                    _units.Clear(); _unitGos.Clear();
                    for (int i = 0; i < actors.Count; i++)
                        if (actors[i] != null && _units.Add(actors[i].GetInstanceID())) _unitGos.Add(actors[i]);
                    if (Sound) Hit();
                    return;
                }
                // any other order takes those units off the focus
                if (_target == null || _units.Count == 0) return;
                for (int i = 0; i < actors.Count; i++)
                {
                    if (actors[i] == null || !_units.Remove(actors[i].GetInstanceID())) continue;
                    _unitGos.Remove(actors[i]);
                }
                if (_units.Count == 0) Clear();
            }
            catch (Exception e) { BtcLog.Error("FocusFx", "OnOrder: " + e.Message); }
        }

        internal static void OnOrder(List<Labor> units, GameObject target)
        {
            List<GameObject> gos = new List<GameObject>(units.Count);
            for (int i = 0; i < units.Count; i++) if (units[i] != null) gos.Add(units[i].gameObject);
            OnOrder(gos, target);
        }

        // focus = an order on a living enemy unit given to at least one own unit that is hostile to it
        private static bool IsFocus(List<GameObject> actors, GameObject target)
        {
            if (target == null || target.GetComponent<Labor>() == null) return false;
            DamageReceiver dr = target.GetComponent<DamageReceiver>();
            if (dr == null || dr.CurrentHP <= 0f) return false;
            for (int i = 0; i < actors.Count; i++)
                if (actors[i] != null && actors[i].GetComponent<Labor>() != null && Affiliation.IsEnemy(actors[i], target)) return true;
            return false;
        }

        private static void Clear()
        {
            _target = null; _dr = null; _rend = null;
            _units.Clear(); _unitGos.Clear();
        }

        private static void Hit()
        {
            int vol = Volume;
            if (vol <= 0) return;
            if (vol != _sndVol || _snd == IntPtr.Zero)
            {
                _sndVol = vol;
                // the aim trainer hit tick (BtcSfx.Click at full UI level), own volume
                _snd = BtcSfx.Pin(BtcSfx.Tick(1100f, 0.028f, 130f, 0.20f * vol * 0.01f * 1.4f, 0.05f, 3200f));
            }
            BtcSfx.PlayPtr(_snd);
        }

        /// <summary>Settings preview (volume slider).</summary>
        internal static void PreviewSound() { Hit(); }

        /// <summary>CameraBookmarkHotkey.OnGUI: marker above the focused unit.</summary>
        internal static void Draw()
        {
            if (_target == null) { if (_units.Count > 0) Clear(); return; }
            if (Event.current.type != EventType.Repaint) return;
            if (_dr == null || _dr.CurrentHP <= 0f) { Clear(); return; }
            bool anyAlive = false;
            for (int i = 0; i < _unitGos.Count && !anyAlive; i++) anyAlive = _unitGos[i] != null;
            if (!anyAlive) { Clear(); return; }
            if (!Mark || BtcReplay.Mode == BtcReplay.State.Playing) return;
            if (_rend != null && !_rend.enabled) return;   // hidden (fog of war)
            Camera cam = Camera.main;
            if (cam == null) return;
            Vector3 p = cam.WorldToScreenPoint(_target.transform.position + Vector3.up * 3.4f);
            if (p.z <= 0f) return;
            float s = Mathf.Round(Screen.height * 0.026f * Size);
            float bob = Mathf.Sin(Time.unscaledTime * 6f) * s * 0.12f;
            Rect r = new Rect(p.x - s * 0.5f, Screen.height - p.y - s + bob, s, s);
            BtcReplayIcons.Glyph(BtcReplayIcons.ChevD, new Rect(r.x + 1f, r.y + 1.5f, s, s), new Color(0f, 0f, 0f, 0.55f));
            BtcReplayIcons.Glyph(BtcReplayIcons.ChevD, r, new Color(1f, 0.25f, 0.2f, 1f));
        }
    }
}
