using System.Collections.Generic;
using Brix.Game;
using Brix.Game.AI;
using UnityEngine;

namespace BetterThanCastle
{
    /// <summary>
    /// Replay: a coloured dot over every unit showing whose it is (Off / only while Alt is held / always). IMGUI, drawn on Repaint only; the unit list is
    /// refreshed 3 times a second from the object registry, the positions are projected per frame (no allocations).
    /// </summary>
    internal static class BtcUnitDots
    {
        private static readonly List<Labor> Tmp = new List<Labor>();
        private static Labor[] _units = new Labor[0];
        private static Color[] _cols = new Color[0];
        private static float _next;
        private static Texture2D _disc;

        internal static int Mode { get { return BtcConfig.ReplayUnitDots != null ? BtcConfig.ReplayUnitDots.Value : 1; } }

        internal static void Cycle()
        {
            if (BtcConfig.ReplayUnitDots != null) BtcConfig.ReplayUnitDots.Value = (Mode + 1) % 3;
        }

        private static Texture2D Disc()
        {
            if (_disc != null) return _disc;
            const int S = 32;
            Color[] px = new Color[S * S];
            for (int y = 0; y < S; y++)
                for (int x = 0; x < S; x++)
                {
                    float d = Mathf.Sqrt((x - 15.5f) * (x - 15.5f) + (y - 15.5f) * (y - 15.5f));
                    px[y * S + x] = new Color(1f, 1f, 1f, Mathf.Clamp01(16f - d));
                }
            _disc = new Texture2D(S, S, TextureFormat.RGBA32, false);
            _disc.SetPixels(px); _disc.Apply();
            _disc.hideFlags = HideFlags.HideAndDontSave; _disc.wrapMode = TextureWrapMode.Clamp; _disc.filterMode = FilterMode.Bilinear;
            return _disc;
        }

        private static void Refresh()
        {
            Tmp.Clear();
            BtcRegistry.CollectLabors(Tmp);
            int n = 0;
            if (_units.Length < Tmp.Count) { _units = new Labor[Tmp.Count + 32]; _cols = new Color[Tmp.Count + 32]; }
            for (int i = 0; i < Tmp.Count; i++)
            {
                Labor l = Tmp[i];
                if (l == null || !BtcMatchStats.Tracks(l.faction)) continue;
                _units[n] = l; _cols[n] = l.faction.FactionColor; n++;
            }
            for (int i = n; i < _units.Length; i++) _units[i] = null;
        }

        internal static void Draw(float fs)
        {
            if (Event.current.type != EventType.Repaint) return;
            int mode = Mode;
            if (mode == 0 || (mode == 1 && !BtcAutoMarks.AltDown)) return;
            Camera cam = Camera.main;
            if (cam == null || BtcReplay.Loading || BtcReplay.Seeking) return;
            float now = Time.unscaledTime;
            if (now >= _next) { _next = now + 0.33f; Refresh(); }
            float s = fs * 0.45f * BtcReplayLayout.CursorScale;
            Texture2D disc = Disc();
            Color old = GUI.color;
            for (int i = 0; i < _units.Length; i++)
            {
                Labor l = _units[i];
                if (l == null) break;
                if (!l.gameObject.activeInHierarchy) continue;
                Vector3 sp = cam.WorldToScreenPoint(l.transform.position + new Vector3(0f, 2.4f, 0f));
                if (sp.z <= 0f || sp.x < -s || sp.x > Screen.width + s || sp.y < -s || sp.y > Screen.height + s) continue;
                float x = sp.x, y = Screen.height - sp.y;
                GUI.color = new Color(0f, 0f, 0f, 0.85f);
                GUI.DrawTexture(new Rect(x - s * 0.5f - 1.5f, y - s * 0.5f - 1.5f, s + 3f, s + 3f), disc);
                Color c = _cols[i];
                GUI.color = new Color(c.r, c.g, c.b, 1f);
                GUI.DrawTexture(new Rect(x - s * 0.5f, y - s * 0.5f, s, s), disc);
            }
            GUI.color = old;
        }
    }
}
