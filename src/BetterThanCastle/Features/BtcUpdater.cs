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
		private float _startAt, _recheckAt;
		private bool _dismissed;     // "Later" or "Skip": no more checks this session
		private WWW _www;
		private string _latest, _url, _sha, _msg;
		private GUIStyle _lbl;

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

#if DEV
		/// <summary>DEV: shows the update card without GitHub (scenario command updtest: 1 available, 2 downloading, 3 done, 4 error).</summary>
		internal static void DevShow(int phase)
		{
			if (_inst == null)
			{
				GameObject go = new GameObject("BtcUpdaterDev");
				UnityEngine.Object.DontDestroyOnLoad(go);
				go.hideFlags = HideFlags.HideAndDontSave;
				_inst = go.AddComponent<BtcUpdater>();
			}
			_inst._latest = "9.9.9"; _inst._msg = "dev error text"; _inst._restarting = true;
			_inst._phase = phase == 1 ? Phase.Available : phase == 2 ? Phase.Downloading : phase == 3 ? Phase.Done : phase == 4 ? Phase.Error : Phase.Hidden;
		}
#endif

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
				else if (_phase == Phase.Hidden && !_dismissed && _recheckAt > 0f && Time.realtimeSinceStartup >= _recheckAt) { _recheckAt = 0f; _startAt = 0f; _phase = Phase.Wait; }   // a release may appear while the game runs
				else if (_phase == Phase.Checking && _www != null && _www.isDone) OnChecked();
				else if (_phase == Phase.Downloading && _www != null && _www.isDone) OnDownloaded();
				else if (_phase == Phase.Done && !_restarting && Time.realtimeSinceStartup - _doneAt > 3.5f) Restart();
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
			if (Compare(_latest, BtcPlugin.Version) <= 0 || _latest == BtcConfig.UpdateSkipped.Value) { _phase = Phase.Hidden; _recheckAt = Time.realtimeSinceStartup + 1800f; return; }
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
			try { File.WriteAllText(PendingFile, _latest); } catch (Exception) { }
			_doneAt = Time.realtimeSinceStartup;
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
			// letter suffix of the last part (0.3.0a > 0.3.0)
			int sa = Suffix(x), sb = Suffix(y);
			return sa == sb ? 0 : (sa < sb ? -1 : 1);
		}

		private static int Suffix(string[] p)
		{
			if (p.Length < 3) return 0;
			Match m = Regex.Match(p[2], "[A-Za-z]");
			return m.Success ? char.ToLower(m.Value[0]) - 'a' + 1 : 0;
		}

		// ---- new look: flat dark card in the menu style (accent bar, icon, progress bar, sounds)
		private Phase _shownPhase = Phase.Wait;
		private float _doneAt;
		private bool _restarting;

		private static string T(string en, string ru) { return BtcReplayUI.T(en, ru); }

		private bool CardButton(Rect r, int icon, string text, bool primary)
		{
			Event e = Event.current;
			bool hov = r.Contains(e.mousePosition);
			Color ac = BtcMenu.Accent;
			BtcReplayUI.Fill(r, primary ? new Color(ac.r, ac.g, ac.b, hov ? 1f : 0.85f) : new Color(1f, 1f, 1f, hov ? 0.14f : 0.07f));
			Color ink = primary ? new Color(0.03f, 0.06f, 0.04f, 1f) : new Color(1f, 1f, 1f, 0.85f);
			float g = r.height * 0.5f;
			BtcReplayIcons.Glyph(icon, new Rect(r.x + r.height * 0.4f, r.center.y - g * 0.5f, g, g), ink);
			Color oc = GUI.contentColor; GUI.contentColor = ink;
			GUI.Label(new Rect(r.x + r.height * 0.4f + g + r.height * 0.3f, r.y, r.width, r.height), text, _lbl);
			GUI.contentColor = oc;
			return BtcSfx.Btn(r, "", BtcReplayUI.sBtn);
		}

		private void OnGUI()
		{
			if (_phase == Phase.Wait || _phase == Phase.Checking || _phase == Phase.Hidden) return;
			int fs = Mathf.Max(14, Screen.height / 38);
			BtcReplayUI.Ensure(fs);
			GUIStyle b = BtcReplayUI.sBarLabel ?? BtcReplayUI.sLabel;
			if (_lbl == null || _lbl.fontSize != Mathf.RoundToInt(fs * 0.9f))
			{
				_lbl = new GUIStyle(b) { fontSize = Mathf.RoundToInt(fs * 0.9f), alignment = TextAnchor.MiddleLeft, clipping = TextClipping.Clip };
				_lbl.normal.textColor = Color.white;
			}
			GUI.depth = -2000;
			Event e = Event.current;
			if (_phase != _shownPhase)
			{
				_shownPhase = _phase;
				if (_phase == Phase.Available) { BtcSfx.Open(); BtcHud.AddHistory(T("Update available: v" + _latest, "Доступно обновление: v" + _latest), BtcReplayIcons.Bolt, BtcMenu.Accent); }
				else if (_phase == Phase.Done) BtcSfx.Ok();
				else if (_phase == Phase.Error) BtcSfx.Warn();
			}
			bool err = _phase == Phase.Error;
			Color ac = err ? new Color(1f, 0.45f, 0.4f, 1f) : BtcMenu.Accent;
			float w = Mathf.Min(fs * 34f, Screen.width - 32f), pad = fs, ih = fs * 3f;
			float h = pad * 2f + ih + (_phase == Phase.Downloading || _phase == Phase.Done ? fs * 2.2f : fs * 3.6f);
			Rect r = new Rect((Screen.width - w) * 0.5f, fs * 0.8f, w, h);
			BtcReplayUI.Fill(new Rect(r.x - 1f, r.y - 1f, r.width + 2f, r.height + 2f), new Color(1f, 1f, 1f, 0.1f));
			BtcReplayUI.Fill(r, new Color(0.03f, 0.04f, 0.05f, 0.98f));
			BtcReplayUI.Fill(new Rect(r.x, r.y, 3f, r.height), ac);
			int icon = err ? BtcReplayIcons.Cross : (_phase == Phase.Available ? BtcReplayIcons.Bolt : (_phase == Phase.Done ? BtcReplayIcons.Check : BtcReplayIcons.Import));
			Rect ir = new Rect(r.x + pad, r.y + pad, ih, ih);
			BtcReplayUI.Fill(ir, new Color(ac.r, ac.g, ac.b, 0.15f));
			BtcReplayIcons.Glyph(icon, new Rect(ir.x + ih * 0.2f, ir.y + ih * 0.2f, ih * 0.6f, ih * 0.6f), ac);
			string title, sub;
			switch (_phase)
			{
				case Phase.Available:
					title = T("Update available: v" + _latest, "Доступно обновление: v" + _latest);
					sub = T("Installed v" + BtcPlugin.Version + ". The game restarts by itself and shows what is new.", "Установлена v" + BtcPlugin.Version + ". Игра сама перезапустится и покажет, что нового.");
					break;
				case Phase.Downloading:
					title = T("Downloading v" + _latest, "Скачиваю v" + _latest);
					sub = T("Please wait, the file is checked before it is installed", "Подожди, файл проверяется перед установкой");
					break;
				case Phase.Done:
					title = T("Updated to v" + _latest, "Обновлено до v" + _latest);
					sub = _restarting ? T("Restarting the game...", "Перезапускаю игру...") : T("Restarting the game in a moment", "Сейчас перезапущу игру");
					break;
				default:
					title = T("Update failed", "Не удалось обновить");
					sub = _msg;
					break;
			}
			GUIStyle big = new GUIStyle(_lbl) { fontSize = Mathf.RoundToInt(fs * 1.15f), fontStyle = FontStyle.Bold };
			GUIStyle dim = new GUIStyle(_lbl) { fontSize = Mathf.RoundToInt(fs * 0.8f) };
			dim.normal.textColor = new Color(1f, 1f, 1f, 0.55f);
			float tx = ir.xMax + pad;
			GUI.Label(new Rect(tx, r.y + pad, r.xMax - tx - pad, fs * 1.6f), title, big);
			GUI.Label(new Rect(tx, r.y + pad + fs * 1.6f, r.xMax - tx - pad, fs * 1.4f), sub, dim);
			float y = r.y + pad * 2f + ih;
			if (_phase == Phase.Downloading || _phase == Phase.Done)
			{
				float p = _phase == Phase.Done ? 1f : (_www != null ? Mathf.Clamp01(_www.progress) : 0f);
				Rect tr = new Rect(r.x + pad, y, r.width - pad * 2f - fs * 3.5f, fs * 0.6f);
				BtcReplayUI.Fill(tr, new Color(1f, 1f, 1f, 0.1f));
				BtcReplayUI.Fill(new Rect(tr.x, tr.y, tr.width * p, tr.height), ac);
				GUI.Label(new Rect(tr.xMax + fs * 0.6f, tr.y - fs * 0.5f, fs * 3f, fs * 1.6f), Mathf.RoundToInt(p * 100f) + "%", _lbl);
				return;
			}
			float bh = fs * 2.3f, bx = r.x + pad;
			if (_phase == Phase.Available)
			{
				if (CardButton(new Rect(bx, y, fs * 9f, bh), BtcReplayIcons.Import, T("Update", "Обновить"), true))
				{
					var h2 = new System.Collections.Generic.Dictionary<string, string>();
					h2["User-Agent"] = "BetterThanCastle/" + BtcPlugin.Version;
					_www = new WWW(_url, null, h2);
					_phase = Phase.Downloading;
				}
				bx += fs * 9.4f;
				if (CardButton(new Rect(bx, y, fs * 7f, bh), BtcReplayIcons.Undo, T("Later", "Позже"), false)) { _dismissed = true; _phase = Phase.Hidden; }
				bx += fs * 7.4f;
				if (CardButton(new Rect(bx, y, fs * 10f, bh), BtcReplayIcons.Cross, T("Skip this version", "Пропустить версию"), false))
				{
					BtcConfig.UpdateSkipped.Value = _latest;
					_dismissed = true;
					_phase = Phase.Hidden;
				}
			}
			else if (err)
			{
				if (CardButton(new Rect(bx, y, fs * 6f, bh), BtcReplayIcons.Check, "OK", false)) _phase = Phase.Hidden;
			}
		}

		// Relaunches the game after the DLL was replaced: Steam copies go through steam://, others start the exe. A detached cmd waits a few
		// seconds so the old process is gone first. A flag file makes the new session open the changelog.
		private void Restart()
		{
			if (_restarting) return;
			_restarting = true;
			try
			{
				string exe = System.Diagnostics.Process.GetCurrentProcess().MainModule.FileName;
				string target = "\"" + exe + "\"";
				if (exe.IndexOf("steamapps", StringComparison.OrdinalIgnoreCase) >= 0) target = "steam://rungameid/227860";
				else
				{
					string[] a = Environment.GetCommandLineArgs();
					for (int i = 1; i < a.Length; i++) target += " \"" + a[i] + "\"";
				}
				System.Diagnostics.ProcessStartInfo psi = new System.Diagnostics.ProcessStartInfo("cmd.exe", "/c ping -n 5 127.0.0.1 >nul & start \"\" " + target);
				psi.CreateNoWindow = true; psi.UseShellExecute = false;
				System.Diagnostics.Process.Start(psi);
				BtcLog.Info("Updater", "restarting: " + target);
				Application.Quit();
			}
			catch (Exception ex) { BtcLog.Error("Updater", "restart: " + ex); _restarting = false; _msg = BtcPlugin.Version + " -> " + _latest + ": restart the game by hand"; }
		}

		private static string PendingFile { get { return BtcPaths.Config("open-changelog.txt"); } }

		/// <summary>Called every frame by ResourcePackManager.Update: after an automatic update restart, opens the changelog once the game is up.</summary>
		internal static void PollPostUpdate()
		{
			try
			{
				if (Time.realtimeSinceStartup < 12f || !File.Exists(PendingFile)) return;
				File.Delete(PendingFile);
				ResourcePackManager.GoTab(10);
			}
			catch (Exception) { }
		}
	}
}
