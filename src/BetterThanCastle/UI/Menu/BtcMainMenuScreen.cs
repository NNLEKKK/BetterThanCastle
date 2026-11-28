using UnityEngine;

namespace BetterThanCastle
{
    /// <summary>
    /// Which vanilla main-menu screen is shown. The menu is prefab-driven uGUI with no state API: the screen is the active child of
    /// "Canvas/Menu/Reskin_MenuMain */Main (H)/Left" ("Left: Main" on the first screen, "Left: Play: ..." etc. deeper; lab quickhost,
    /// BTClogs/qh/menutree-*.txt). Checked 4 times a second, the Left transform is cached.
    /// </summary>
    internal static class BtcMainMenuScreen
    {
        private static Transform _left;
        private static float _next, _findNext;
        private static bool _root;

        internal static bool IsRoot
        {
            get
            {
                float now = Time.unscaledTime;
                if (now < _next) return _root;
                _next = now + 0.25f;
                _root = Check(now);
                return _root;
            }
        }

        private static bool Check(float now)
        {
            if (_left == null)
            {
                if (now < _findNext) return false;
                _findNext = now + 2f;
                GameObject main = GameObject.Find("Main (H)");
                if (main == null) return false;
                _left = main.transform.Find("Left");
                if (_left == null) return false;
            }
            if (!_left.gameObject.activeInHierarchy) return false;
            bool root = false;
            for (int i = 0; i < _left.childCount; i++)
            {
                Transform c = _left.GetChild(i);
                if (!c.gameObject.activeSelf) continue;
                if (c.name != "Left: Main") return false;
                root = true;
            }
            return root;
        }
    }
}
