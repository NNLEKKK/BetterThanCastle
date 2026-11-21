using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace BetterThanCastle
{
    /// <summary>Profile tab: the Avatar page. The current avatar with its status and a card grid of the pictures in BetterThanCastle/Avatars (png, jpg, gif); thumbnails load lazily (see FileGrid).</summary>
    internal sealed partial class BtcProfileTab
    {
        private const long ThumbMaxBytes = 300 * 1024;
        private readonly Dictionary<string, Texture2D> _thumbs = new Dictionary<string, Texture2D>();
        private readonly HashSet<string> _noThumb = new HashSet<string>();
        private int _thumbFrame = -1;

        private void ForgetThumbs()
        {
            foreach (KeyValuePair<string, Texture2D> kv in _thumbs) if (kv.Value != null) UnityEngine.Object.Destroy(kv.Value);
            _thumbs.Clear();
            _noThumb.Clear();
        }

        private static void OpenFolder(string dir)
        {
            try
            {
                Directory.CreateDirectory(dir);
                System.Diagnostics.Process.Start("explorer.exe", "\"" + dir + "\"");
            }
            catch (Exception ex) { BtcLog.Warn("Profile", "open folder failed: " + ex.Message); }
        }

        private void AvatarPage(ref float y, float w, int fs)
        {
            BtcAvatar.UiSeen();
            float pad = fs, ib = fs * 1.9f;
            string[] files = BtcAvatar.FilesList;
            string cur = BtcAvatar.CurrentFile ?? string.Empty;

            if (BtcUi.Fold(ref y, w, fs, KeyAdd("profile:avcur", y), Tt("Your avatar", "Ваш аватар"), 0))
            {
                float big = fs * 6f;
                Rect card = new Rect(pad, y, w - pad * 2f, big + fs);
                BtcReplayUI.Fill(card, new Color(1f, 1f, 1f, 0.03f));
                Rect ar = new Rect(card.x + fs * 0.5f, card.y + fs * 0.5f, big, big);
                BtcReplayUI.Fill(ar, new Color(0f, 0f, 0f, 0.4f));
                Texture2D tex = BtcAvatar.LocalTex;
                if (tex != null) GUI.DrawTexture(new Rect(ar.x + 2f, ar.y + 2f, ar.width - 4f, ar.height - 4f), tex, ScaleMode.ScaleToFit, true);
                else BtcReplayIcons.Glyph(BtcReplayIcons.Group, new Rect(ar.x + big * 0.25f, ar.y + big * 0.25f, big * 0.5f, big * 0.5f), new Color(1f, 1f, 1f, 0.25f));
                float tx = ar.xMax + fs;
                Txt(new Rect(tx, card.y + fs * 0.5f, card.xMax - tx - fs, fs * 2f), cur.Length == 0 ? Tt("Steam avatar (no custom picture)", "Аватар Steam (своей картинки нет)") : cur, _ttl, Color.white);
                string st = BtcAvatar.StatusText;
                if (!string.IsNullOrEmpty(st)) Txt(new Rect(tx, card.y + fs * 2.5f, card.xMax - tx - fs, fs * 2f), st, _dim, new Color(1f, 1f, 1f, 0.6f));
                if (!BtcNick.StyleOn) Txt(new Rect(tx, card.y + fs * 4.4f, card.xMax - tx - fs, fs * 1.6f), Tt("Hidden: mod styling is off", "Скрыт: оформление мода выключено"), _dim, new Color(0.95f, 0.78f, 0.1f, 1f));
                y += card.height + fs * 0.4f;
                if (cur.Length > 0 && BtcUi.Action(ref y, w, fs, BtcReplayIcons.Cross, Tt("Use the Steam avatar", "Вернуть аватар Steam"), null)) ChooseAvatar(string.Empty, cur);
                y += fs * 0.2f;
            }

            if (BtcUi.Fold(ref y, w, fs, KeyAdd("profile:avfiles", y), Tt("Pictures", "Картинки"), files.Length))
            {
                float x = pad;
                if (BtcReplayIcons.Plain(new Rect(x, y, ib, ib), BtcReplayIcons.Folder, Color.white, Tt("Open the Avatars folder: drop png, jpg or gif files there", "Открыть папку Avatars: кидайте туда png, jpg или gif")))
                    OpenFolder(BtcAvatar.FolderPath);
                x += ib + 3f;
                if (BtcReplayIcons.Plain(new Rect(x, y, ib, ib), BtcReplayIcons.Refresh, Color.white, Tt("Rescan the folder", "Перечитать папку"))) { BtcAvatar.Rescan(); ForgetThumbs(); }
                x += ib + fs;
                Txt(new Rect(x, y, w - x - pad, ib), "BetterThanCastle/Avatars  (png, jpg, gif)", _dim, new Color(1f, 1f, 1f, 0.55f));
                y += ib + fs * 0.4f;
                FileGrid(ref y, w, fs, files, cur, BtcAvatar.FolderPath, delegate (string f) { ChooseAvatar(f, cur); });
                BtcUi.Note(ref y, w, fs, Tt("Pictures are shrunk to 64x64; a gif plays as an animation in the lobby list.", "Картинки сжимаются до 64x64; gif играет как анимация в списке лобби."));
            }
        }

        private static void ChooseAvatar(string file, string before)
        {
            BtcAvatar.Choose(file);
            BtcUndo.Push(Scope, Tt("Avatar", "Аватар"), delegate { BtcAvatar.Choose(before); });
        }
    }
}
