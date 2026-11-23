#if DEV
using System;
using System.IO;
using System.Text;
using Brix.Engine;
using Brix.Game;
using Brix.Network;
using Brix.UI;
using Brix.UI.Icons;
using UnityEngine;
using UnityEngine.UI;

namespace BetterThanCastle.Dev
{
    /// <summary>DEV: "mmdump" logs why a mini map is (not) visible (spectator bug); "flagdump" writes the lobby flag sprites as PNGs to BTClogs.</summary>
    internal static class BtcDevSpecMap
    {
        internal static void MiniMapDump()
        {
            try
            {
                User lu = User.LocalUser;
                BtcLog.Info("MmDump", "showsMinimap=" + UIGameObserver.ShowsMinimap + " terrainReady=" + GameSignals.Received<Action>(GameSignals.TerrainReady)
                    + " faction=" + (lu != null && lu.faction != null ? lu.faction.name + "/spec=" + (lu.faction == Faction.Spectator) : "none"));
                MiniMap[] all = Resources.FindObjectsOfTypeAll<MiniMap>();
                for (int i = 0; i < all.Length; i++)
                {
                    MiniMap m = all[i];
                    if (m == null || !m.gameObject.scene.IsValid()) continue;
                    StringBuilder sb = new StringBuilder();
                    for (Transform t = m.transform; t != null; t = t.parent)
                    {
                        sb.Insert(0, "/" + t.name + (t.gameObject.activeSelf ? "" : "[OFF]"));
                        CanvasGroup cg = t.GetComponent<CanvasGroup>();
                        if (cg != null && cg.alpha < 0.99f) sb.Insert(0, "{cg " + t.name + " a=" + cg.alpha.ToString("0.##") + "}");
                        LayoutElement le = t.GetComponent<LayoutElement>();
                        if (le != null && le.ignoreLayout == false && (le.preferredWidth == 0f || le.preferredHeight == 0f)) sb.Insert(0, "{le0 " + t.name + "}");
                        RectTransform rt = t as RectTransform;
                        if (rt != null && (rt.rect.width < 1f || rt.rect.height < 1f)) sb.Insert(0, "{size0 " + t.name + " " + rt.rect.size + "}");
                        if (rt != null && Mathf.Abs(rt.localScale.x) < 0.01f) sb.Insert(0, "{scale0 " + t.name + "}");
                    }
                    RectTransform r = m.transform as RectTransform;
                    Vector3[] c = new Vector3[4];
                    if (r != null) r.GetWorldCorners(c);
                    RawImage img = m.mapImage;
                    string imgs = img == null ? "none" : ("en=" + img.enabled + " col=" + img.color + " tex=" + (img.texture != null ? img.texture.name + " " + img.texture.width + "x" + img.texture.height : "null")
                        + " mat=" + (img.material != null ? img.material.name : "null") + " cr.a=" + img.canvasRenderer.GetAlpha().ToString("0.##") + " cull=" + img.canvasRenderer.cull);
                    BtcLog.Info("MmDump", "mm " + sb + " active=" + m.gameObject.activeInHierarchy + " enabled=" + m.enabled + " heightmap=" + (m.heightmap != null)
                        + " size=" + (r != null ? r.rect.size.ToString() : "-") + " corners=" + c[0] + ".." + c[2] + " img " + imgs
                        + " fow=" + m.showFogOfWar + " terrain=" + m.showTerrain);
                }
                GameObject sbg = GameObject.Find("Menu: Scoreboard (Lua)");
                CanvasGroup scg = sbg != null ? sbg.GetComponent<CanvasGroup>() : null;
                BtcLog.Info("MmDump", "scoreboard " + (sbg != null) + " active=" + (sbg != null && sbg.activeInHierarchy) + " cg=" + (scg != null ? scg.alpha.ToString("0.##") : "-")
                    + " children=" + (sbg != null ? sbg.transform.childCount : -1));
            }
            catch (Exception e) { BtcLog.Error("MmDump", e.ToString()); }
        }

        internal static void FlagDump()
        {
            try
            {
                Save("flag-blue", IconKeys.FlagBlue.Get64());
                Save("flag-purple", IconKeys.FlagPurple.Get64());
                for (int k = 0; k < 4; k++) Save("flag-x" + k, BtcLobby.FlagFor(k, BtcLobby.ExtraColor(k)));
                Save("flag-spec", BtcLobby.MakeFlag(IconKeys.FlagBlue, new Color(0.62f, 0.62f, 0.66f, 1f), BtcLobby.FlagStyle.Eye));
            }
            catch (Exception e) { BtcLog.Error("FlagDump", e.ToString()); }
        }

        private static void Save(string name, Sprite s)
        {
            int w, h;
            Color[] px = BtcLobby.SpritePixels(s, out w, out h);
            if (px == null) { BtcLog.Warn("FlagDump", name + ": no pixels"); return; }
            Texture2D t = new Texture2D(w, h, TextureFormat.RGBA32, false);
            t.SetPixels(px); t.Apply();
            File.WriteAllBytes(Path.Combine(BtcDevPaths.LogDir, name + ".png"), t.EncodeToPNG());
            UnityEngine.Object.Destroy(t);
            BtcLog.Info("FlagDump", name + " " + w + "x" + h + " tex=" + s.texture.name + " " + s.texture.width + "x" + s.texture.height + " rect=" + s.textureRect + " packed=" + s.packed);
        }
    }
}
#endif
