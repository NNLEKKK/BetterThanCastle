using System.Collections;
using UnityEngine;

namespace BetterThanCastle
{
    /// Menu_Popup ev_onSet / ev_onReset from the task list: the popup is visible while a target panel is set and its left edge
    /// tracks the target (AnchoredPositionTracking with offset (-50, h/2) from the panel centre, once; clamped to the border).
    /// Placed one frame later so the task list layout has run; no per-frame tracking (the Lua one is "once" too).
    internal static partial class BtcGhPopup
    {
        private static RectTransform _target;
        private static readonly Vector3[] Corners = new Vector3[4];

        internal static void Track(RectTransform target)
        {
            _target = target;
            if (_lyt == null) return;
            if (!_lyt.gameObject.activeSelf) _lyt.gameObject.SetActive(true);
            if (BtcGameHudLife.Inst != null) BtcGameHudLife.Inst.StartCoroutine(PlaceNextFrame());
            else Place();
        }

        internal static void Untrack()
        {
            _target = null;
            if (_lyt != null && _lyt.gameObject.activeSelf) _lyt.gameObject.SetActive(false);
        }

        internal static bool Shown { get { return _target != null && _lyt != null && _lyt.gameObject.activeSelf; } }

        private static IEnumerator PlaceNextFrame()
        {
            yield return null;
            Place();
        }

        private static void Place()
        {
            try
            {
                if (_target == null || _lyt == null || _pop == null) return;
                _target.GetWorldCorners(Corners);
                Vector3 centre = (Corners[0] + Corners[2]) * 0.5f;
                Vector3 local = _pop.InverseTransformPoint(centre);
                Rect pr = _pop.rect;
                float x = local.x - pr.xMin - 50f;
                float max = pr.width - _lyt.rect.width;
                if (x > max) x = max;
                if (x < 0f) x = 0f;
                _lyt.anchoredPosition = new Vector2(x, 0f);
            }
            catch (System.Exception e) { BtcLog.Error("GameHud", "popup place: " + e.Message); }
        }
    }
}
