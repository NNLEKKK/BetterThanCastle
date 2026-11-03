using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Text;
using Brix.Game;
using Brix.Network;
using UnityEngine;

namespace BetterThanCastle
{
    /// <summary>
    /// Player nicks in replay files (header line "pl2=", optional: old readers ignore it, old files have none).
    /// Format: entries separated by ';', each "factionId:RRGGBB:base64(UTF-8 nick)"; the nick keeps its rich text (coloured nicks).
    /// Playback: the recorded User objects arrive without a usable screen name, so once per second every playing User whose
    /// faction has a recorded nick gets it written into User._screenName (local object only, nothing is sent).
    /// </summary>
    internal static class BtcReplayNames
    {
        private static readonly Dictionary<int, string> Nick = new Dictionary<int, string>();
        private static readonly Dictionary<int, Color> Col = new Dictionary<int, Color>();
        private static FieldInfo _screenField;
        private static float _next;

        /// <summary>Header line for the recording (empty string when no player has a faction yet).</summary>
        internal static string HeaderLine()
        {
            StringBuilder sb = new StringBuilder();
            try
            {
                List<User> users = UNetManager.instance != null ? UNetManager.instance.UserList : null;
                if (users == null) return string.Empty;
                HashSet<int> seen = new HashSet<int>();
                for (int i = 0; i < users.Count; i++)
                {
                    User u = users[i];
                    if (u == null || u.faction == null || u.faction.IsSpectator || string.IsNullOrEmpty(u.ScreenName)) continue;
                    int id = u.faction.ID;
                    if (!seen.Add(id)) continue;
                    Color c = u.faction.FactionColor;
                    if (sb.Length > 0) sb.Append(';');
                    sb.Append(id.ToString(CultureInfo.InvariantCulture)).Append(':')
                      .Append(((int)(Mathf.Clamp01(c.r) * 255f)).ToString("X2"))
                      .Append(((int)(Mathf.Clamp01(c.g) * 255f)).ToString("X2"))
                      .Append(((int)(Mathf.Clamp01(c.b) * 255f)).ToString("X2")).Append(':')
                      .Append(Convert.ToBase64String(Encoding.UTF8.GetBytes(u.ScreenName)));
                }
            }
            catch (Exception e) { BtcLog.Error("Replay", "names header: " + e.Message); }
            return sb.Length > 0 ? "pl2=" + sb + "\n" : string.Empty;
        }

        /// <summary>Called when a replay starts playing (text = header value of "pl2", null for old files).</summary>
        internal static void Load(string text)
        {
            BtcMatchStats.OnReplayStart();
            Nick.Clear();
            Col.Clear();
            _next = 0f;
            if (string.IsNullOrEmpty(text)) return;
            string[] es = text.Split(';');
            for (int i = 0; i < es.Length; i++)
            {
                try
                {
                    string[] p = es[i].Split(':');
                    if (p.Length < 3) continue;
                    int id = int.Parse(p[0], CultureInfo.InvariantCulture);
                    Nick[id] = Encoding.UTF8.GetString(Convert.FromBase64String(p[2]));
                    if (p[1].Length == 6)
                    {
                        int rgb = int.Parse(p[1], NumberStyles.HexNumber, CultureInfo.InvariantCulture);
                        Col[id] = new Color(((rgb >> 16) & 255) / 255f, ((rgb >> 8) & 255) / 255f, (rgb & 255) / 255f, 1f);
                    }
                }
                catch { }
            }
            BtcLog.Info("Replay", "names: " + Nick.Count + " players from the header");
        }

        /// <summary>Recorded nick (rich text) of a faction during playback, else null.</summary>
        internal static string NickOf(int facId)
        {
            if (BtcReplay.Mode != BtcReplay.State.Playing) return null;
            string n;
            return Nick.TryGetValue(facId, out n) ? n : null;
        }

        /// <summary>Update(): during playback put the recorded nicks onto the playing User objects (1 Hz, survives rewinds).</summary>
        internal static void Tick()
        {
            if (Nick.Count == 0 || BtcReplay.Mode != BtcReplay.State.Playing) return;
            float now = Time.unscaledTime;
            if (now < _next) return;
            _next = now + 1f;
            try
            {
                List<User> users = UNetManager.instance != null ? UNetManager.instance.UserList : null;
                if (users == null) return;
                if (_screenField == null) _screenField = typeof(User).GetField("_screenName", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                if (_screenField == null) return;
                for (int i = 0; i < users.Count; i++)
                {
                    User u = users[i];
                    if (u == null || u.faction == null || u.faction.IsSpectator) continue;
                    string n;
                    if (!Nick.TryGetValue(u.faction.ID, out n) || u.ScreenName == n) continue;
                    _screenField.SetValue(u, n);
                }
            }
            catch (Exception e) { BtcLog.Error("Replay", "names tick: " + e.Message); _next = now + 10f; }
        }
    }
}
