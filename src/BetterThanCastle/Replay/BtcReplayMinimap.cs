using System.Collections.Generic;
using Brix.UI;
using UnityEngine;
using UnityEngine.UI;

namespace BetterThanCastle
{
    /// <summary>
    /// Replay only. The game puts the mini map into the "Menu: Scoreboard" panel (background, header, timer, player rows). In the replay that panel
    /// is always made invisible (every graphic of it except the mini map itself: alpha 0, no raycasts), and the bare mini map is pinned flush into
    /// the top-left screen corner. The bar icon hides the mini map too (group alpha 0 and moved off screen: the map shader may ignore the alpha).
    /// </summary>
    internal static class BtcReplayMinimap
    {
        private static MiniMap _mm;
        private static RectTransform _panel;
        private static Transform _menu;
        private static CanvasGroup _cg;
        private static float _next;
        private static bool _touched, _logged;
        private static readonly Vector3[] Corners = new Vector3[4];
        private static readonly List<Graphic> Gfx = new List<Graphic>();

        /// <summary>Called every frame from the replay UI; does its work at ~4 Hz.</summary>
        internal static void Tick()
        {
            if (Event.current == null || Event.current.type != EventType.Repaint) return;
            float t = Time.unscaledTime;
            if (t < _next) return;
            _next = t + 0.25f;
            try
            {
                // bound objects must stay alive and inside the scoreboard menu of a canvas; otherwise (scene change, rewind, stray instance) bind again
                if (_mm == null || _panel == null || _menu == null || !_panel.gameObject.activeInHierarchy || _panel.GetComponentInParent<Canvas>() == null)
                {
                    Unbind();
                    if (!Bind()) { _next = t + 1f; return; }
                }
                if (_cg == null) { _cg = _panel.GetComponent<CanvasGroup>(); if (_cg == null) _cg = _panel.gameObject.AddComponent<CanvasGroup>(); }
                bool hide = BtcConfig.ReplayHideMinimap != null && BtcConfig.ReplayHideMinimap.Value;
                _cg.alpha = hide ? 0f : 1f;
                _cg.blocksRaycasts = !hide;
                _cg.interactable = !hide;
                _touched = true;
                HideScoreboard();
                Canvas cv = _panel.GetComponentInParent<Canvas>();
                Camera cam = cv != null && cv.renderMode != RenderMode.ScreenSpaceOverlay ? cv.worldCamera : null;
                RectTransform par = _panel.parent as RectTransform;
                if (par == null) return;
                _panel.GetWorldCorners(Corners);   // 1 = top-left
                Vector2 cur = RectTransformUtility.WorldToScreenPoint(cam, Corners[1]);
                Vector2 want = new Vector2(hide ? -4000f : 0f, (float)Screen.height);
                Vector2 la, lb;
                if (RectTransformUtility.ScreenPointToLocalPointInRectangle(par, cur, cam, out la) && RectTransformUtility.ScreenPointToLocalPointInRectangle(par, want, cam, out lb))
                {
                    Vector2 d = lb - la;
                    if (d.sqrMagnitude > 0.25f) _panel.anchoredPosition += d;
                }
                if (!_logged)
                {
                    _logged = true;
                    BtcLog.Info("ReplayMinimap", "panel " + _panel.name + " menu " + (_menu != null ? _menu.name : "none") + " canvas " + (cv != null ? cv.renderMode.ToString() : "none") + " graphics " + Gfx.Count);
                }
            }
            catch (System.Exception e) { BtcLog.Error("ReplayMinimap", e.Message); _next = t + 5f; }
        }

        private static void Unbind()
        {
            _mm = null; _panel = null; _cg = null; _menu = null; Gfx.Clear();
        }

        /// <summary>Picks the mini map that lives in an active "Menu: ..." under a canvas (a stray instance outside any canvas is ignored).</summary>
        private static bool Bind()
        {
            MiniMap[] all = UnityEngine.Object.FindObjectsOfType<MiniMap>();
            for (int i = 0; i < all.Length; i++)
            {
                MiniMap m = all[i];
                if (m == null || !m.gameObject.activeInHierarchy) continue;
                RectTransform p = FindPanel(m.transform);
                Transform menu = p != null ? FindMenu(p) : null;
                if (p == null || menu == null || p.GetComponentInParent<Canvas>() == null) continue;
                _mm = m; _panel = p; _menu = menu; _cg = null;
                BtcLog.Info("ReplayMinimap", "bound " + p.name + " in " + menu.name + " (candidates " + all.Length + ")");
                return true;
            }
            return false;
        }

        /// <summary>Everything of the scoreboard menu except the mini map: invisible and not clickable (re-applied, Lua rebuilds rows).</summary>
        private static void HideScoreboard()
        {
            if (_menu == null) return;
            _menu.GetComponentsInChildren<Graphic>(true, Gfx);
            for (int i = 0; i < Gfx.Count; i++)
            {
                Graphic g = Gfx[i];
                if (g == null || g.transform.IsChildOf(_panel)) continue;
                g.canvasRenderer.SetAlpha(0f);
                g.raycastTarget = false;
            }
        }

        /// <summary>The 256x256 panel anchored to the top-left of its parent (see SandboxFactionPlayersPanel.lua); falls back to the mini map itself.</summary>
        private static RectTransform FindPanel(Transform from)
        {
            Transform t = from;
            for (int i = 0; i < 6 && t != null; i++, t = t.parent)
            {
                RectTransform rt = t as RectTransform;
                if (rt == null) continue;
                if (rt.anchorMin == new Vector2(0f, 1f) && rt.anchorMax == new Vector2(0f, 1f)) return rt;
            }
            return null;
        }

        /// <summary>The "Menu: Scoreboard (Lua)" root above the mini map.</summary>
        private static Transform FindMenu(Transform from)
        {
            Transform t = from;
            for (int i = 0; i < 10 && t != null; i++, t = t.parent)
                if (t.name.StartsWith("Menu:")) return t;
            return null;
        }

        /// <summary>Playback ended: show everything again.</summary>
        internal static void Reset()
        {
            if (!_touched) return;
            _touched = false;
            try
            {
                if (_cg != null) { _cg.alpha = 1f; _cg.blocksRaycasts = true; _cg.interactable = true; }
                for (int i = 0; i < Gfx.Count; i++) if (Gfx[i] != null) Gfx[i].canvasRenderer.SetAlpha(1f);
            }
            catch { }
            Gfx.Clear();
            Unbind();
        }
    }
}
