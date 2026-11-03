using System;
using System.IO;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using UnityEngine;

namespace BetterThanCastle
{
	// Checks GitHub Releases at startup and offers to replace BetterThanCastle.dll with the newest release.
	// Only touches the mod's own DLL (never game files). Needs the SHA256 of the asset in the release notes.
	internal sealed class BtcUpdater : MonoBehaviour
	{
		// Set to "<owner>/<repo>" of the public repository. Updater stays off while it contains OWNER.
		internal const string Repo = "NNLEKKK/BetterThanCastle";
		private const string AssetName = "BetterThanCastle.dll";

		private enum Phase { Wait, Checking, Available, Downloading, Done, Error, Hidden }

		private static BtcUpdater _inst;
		private static string _pluginPath;

		private Phase _phase = Phase.Wait;
		private float _startAt;
		private WWW _www;
		private string _latest, _url, _sha, _msg;
		private GUIStyle _box, _lbl, _btn;

		internal static void Init(string pluginDllPath)
		{
			try
			{
				_pluginPath = pluginDllPath;
				CleanupOld();
#if DEV
				return; // dev builds (game/csmod) must never be replaced by a release DLL
#endif
				if (_inst != null || !BtcConfig.UpdateCheck.Value || Repo.Contains("OWNER")) return;
				GameObject go = new GameObject("BtcUpdater");
				UnityEngine.Object.DontDestroyOnLoad(go);
				go.hideFlags = HideFlags.HideAndDontSave;
				_inst = go.AddComponent<BtcUpdater>();
				_inst._startAt = Time.realtimeSinceStartup + 8f;
			}
			catch (Exception e) { BtcLog.Error("Updater", "init: " + e); }
		}

		private static void CleanupOld()
		{
			try
			{
				if (string.IsNullOrEmpty(_pluginPath)) return;
				string old = _pluginPath + ".old";
				if (File.Exists(old)) File.Delete(old);
				string nw = _pluginPath + ".new";
				if (File.Exists(nw)) File.Delete(nw);
			}
			catch (Exception e) { BtcLog.Warn("Updater", "cleanup: " + e.Message); }
		}

		private void Update()
		{
			try
			{
				if (_phase == Phase.Wait && Time.realtimeSinceStartup >= _startAt)
				{
					var h = new System.Collections.Generic.Dictionary<string, string>();
					h["User-Agent"] = "BetterThanCastle/" + BtcPlugin.Version;
					h["Accept"] = "application/vnd.github+json";
					_www = new WWW("https://api.github.com/repos/" + Repo + "/releases/latest", null, h);
					_phase = Phase.Checking;
				}
				else if (_phase == Phase.Checking && _www != null && _www.isDone) OnChecked();
				else if (_phase == Phase.Downloading && _www != null && _www.isDone) OnDownloaded();
			}
			catch (Exception e) { Fail("update: " + e.Message); }
		}

		private void OnChecked()
		{
			WWW w = _www; _www = null;
			if (!string.IsNullOrEmpty(w.error)) { BtcLog.Warn("Updater", "check failed: " + w.error); _phase = Phase.Hidden; return; }
			string json = w.text;
			Match tag = Regex.Match(json, "\"tag_name\"\\s*:\\s*\"([^\"]+)\"");
			Match url = Regex.Match(json, "\"browser_download_url\"\\s*:\\s*\"(https://[^\"]*/" + Regex.Escape(AssetName) + ")\"");
			// sha256 only from the release body: assets carry their own "digest": "sha256:..." fields (other files).
			Match body = Regex.Match(json, "\"body\"\\s*:\\s*\"((?:[^\"\\\\]|\\\\.)*)\"");
			Match sha = Regex.Match(body.Success ? body.Groups[1].Value : "", "sha256[:\\s=`]*([0-9a-fA-F]{64})", RegexOptions.IgnoreCase);
			if (!tag.Success || !url.Success || !sha.Success) { BtcLog.Warn("Updater", "release has no tag/asset/sha256"); _phase = Phase.Hidden; return; }
			string u = url.Groups[1].Value;
			// Any owner of a repo named BetterThanCastle on github.com: the account was renamed once (lokillFamalli -> NNLEKKK) and
			// GitHub redirects the API of the old name, but the asset urls carry the new owner. The SHA-256 check still applies.
			if (!Regex.IsMatch(u, "^https://github\\.com/[A-Za-z0-9-]+/BetterThanCastle/releases/download/")) { BtcLog.Warn("Updater", "unexpected asset url"); _phase = Phase.Hidden; return; }
			_latest = tag.Groups[1].Value.TrimStart('v', 'V');
			_url = u;
			_sha = sha.Groups[1].Value.ToLowerInvariant();
			if (Compare(_latest, BtcPlugin.Version) <= 0 || _latest == BtcConfig.UpdateSkipped.Value) { _phase = Phase.Hidden; return; }
			BtcLog.Info("Updater", "new version " + _latest + " (installed " + BtcPlugin.Version + ")");
			_phase = Phase.Available;
		}

		private void OnDownloaded()
		{
			WWW w = _www; _www = null;
			if (!string.IsNullOrEmpty(w.error)) { Fail("download: " + w.error); return; }
			byte[] data = w.bytes;
			string hash;
			using (SHA256 s = SHA256.Create()) hash = BitConverter.ToString(s.ComputeHash(data)).Replace("-", "").ToLowerInvariant();
			if (hash != _sha || data.Length < 100000) { Fail("checksum mismatch, update cancelled"); return; }
			if (string.IsNullOrEmpty(_pluginPath) || !File.Exists(_pluginPath)) { Fail("plugin path unknown"); return; }
			string nw = _pluginPath + ".new", old = _pluginPath + ".old";
			try
			{
				File.WriteAllBytes(nw, data);
				if (File.Exists(old)) File.Delete(old);
				File.Move(_pluginPath, old);
				try { File.Move(nw, _pluginPath); }
				catch { File.Move(old, _pluginPath); throw; }
			}
			catch (Exception e) { Fail("install: " + e.Message); return; }
			BtcLog.Info("Updater", "installed " + _latest + ", restart required");
			_phase = Phase.Done;
		}

		private void Fail(string m)
		{
			BtcLog.Error("Updater", m);
			_msg = m;
			_phase = Phase.Error;
		}

		internal static int Compare(string a, string b)
		{
			string[] x = a.Split('.'), y = b.Split('.');
			for (int i = 0; i < 3; i++)
			{
				int p = 0, q = 0;
				if (i < x.Length) int.TryParse(Regex.Match(x[i], "\\d+").Value, out p);
				if (i < y.Length) int.TryParse(Regex.Match(y[i], "\\d+").Value, out q);
				if (p != q) return p < q ? -1 : 1;
			}
			return 0;
		}

		private void OnGUI()
		{
			if (_phase == Phase.Wait || _phase == Phase.Checking || _phase == Phase.Hidden) return;
			if (_box == null)
			{
				_box = new GUIStyle(GUI.skin.box) { alignment = TextAnchor.MiddleCenter, wordWrap = true, fontSize = 15 };
				_lbl = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, wordWrap = true, fontSize = 15, normal = { textColor = Color.white } };
				_btn = new GUIStyle(GUI.skin.button) { fontSize = 14 };
			}
			GUI.depth = -2000;
			float w = Mathf.Min(560f, Screen.width - 32f), h = 96f;
			Rect r = new Rect((Screen.width - w) * 0.5f, 12f, w, h);
			Color c = GUI.color;
			GUI.color = new Color(0.1f, 0.12f, 0.1f, 0.96f);
			GUI.DrawTexture(r, Texture2D.whiteTexture);
			GUI.color = c;
			string text;
			switch (_phase)
			{
				case Phase.Available:
					text = BtcGameplay.T("Better Than Castle " + _latest + " is available (installed " + BtcPlugin.Version + ")", "Доступна Better Than Castle " + _latest + " (установлена " + BtcPlugin.Version + ")");
					break;
				case Phase.Downloading: text = BtcGameplay.T("Downloading update...", "Скачиваю обновление..."); break;
				case Phase.Done: text = BtcGameplay.T("Updated to " + _latest + ". Restart the game to apply.", "Обновлено до " + _latest + ". Перезапустите игру."); break;
				default: text = BtcGameplay.T("Update failed: ", "Ошибка обновления: ") + _msg; break;
			}
			GUI.Label(new Rect(r.x + 8f, r.y + 6f, r.width - 16f, 50f), text, _lbl);
			float bw = (r.width - 40f) / 3f, by = r.y + 58f;
			if (_phase == Phase.Available)
			{
				if (GUI.Button(new Rect(r.x + 10f, by, bw, 30f), BtcGameplay.T("Update", "Обновить"), _btn))
				{
					var h2 = new System.Collections.Generic.Dictionary<string, string>();
					h2["User-Agent"] = "BetterThanCastle/" + BtcPlugin.Version;
					_www = new WWW(_url, null, h2);
					_phase = Phase.Downloading;
				}
				if (GUI.Button(new Rect(r.x + 20f + bw, by, bw, 30f), BtcGameplay.T("Later", "Позже"), _btn)) _phase = Phase.Hidden;
				if (GUI.Button(new Rect(r.x + 30f + bw * 2f, by, bw, 30f), BtcGameplay.T("Skip this version", "Пропустить версию"), _btn))
				{
					BtcConfig.UpdateSkipped.Value = _latest;
					_phase = Phase.Hidden;
				}
			}
			else if (_phase == Phase.Done || _phase == Phase.Error)
			{
				if (GUI.Button(new Rect(r.x + r.width * 0.5f - 60f, by, 120f, 30f), "OK", _btn)) _phase = Phase.Hidden;
			}
		}
	}
}
