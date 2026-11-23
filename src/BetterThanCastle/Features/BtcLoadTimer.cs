using System;
using System.Diagnostics;
using Brix.Engine;
using UnityEngine;

namespace BetterThanCastle
{
	/// <summary>Overlay with the seconds the world load has been running; after the load it shows the total for a few seconds.</summary>
	public sealed class BtcLoadTimer : MonoBehaviour
	{
		private static BtcLoadTimer _inst;
		private static long _start;
		private static bool _running;
		private static double _total;
		private static float _hideAt;
		private static GUIStyle _style;

		public static double LastTotalSeconds { get { return _total; } }

		/// <summary>Start click (GameParam.StartGame) or load begin; a load begin shortly after the click keeps the click as the start.</summary>
		public static void Begin()
		{
			try
			{
				if (_running && (Stopwatch.GetTimestamp() - _start) / (double)Stopwatch.Frequency < 30.0) return;
				_start = Stopwatch.GetTimestamp();
				_running = true;
				_endPending = false;
				_armed = true;
				_armedAt = Time.realtimeSinceStartup;
				_total = 0.0;
				if (_inst != null) return;
				GameObject go = new GameObject("BtcLoadTimer");
				UnityEngine.Object.DontDestroyOnLoad(go);
				go.hideFlags = HideFlags.HideAndDontSave;
				_inst = go.AddComponent<BtcLoadTimer>();
			}
			catch (Exception e) { BtcLog.Error("LoadTimer", "begin: " + e); }
		}

		/// <summary>End of the loader (StartStep): the timer stops when the loading screen has slid away (the game is playable), at most 30 s later.</summary>
		public static void End()
		{
			if (!_running || _endPending) return;
			_endPending = true;
			_endBy = Time.realtimeSinceStartup + 30f;
			try { GameSignals.Once(GameSignals.LoadingScreenAnimEnd, Finish, null); } catch (Exception) { Finish(); }
		}

		private static bool _endPending;
		private static float _endBy;

		private static void Finish()
		{
			if (!_running) return;
			_running = false;
			_endPending = false;
			_total = (Stopwatch.GetTimestamp() - _start) / (double)Stopwatch.Frequency;
			_hideAt = Time.realtimeSinceStartup + 6f;
			BtcLog.Info("LoadTimer", "world load took " + _total.ToString("F1") + " s (start to playable)");
		}

		// the cancel cross stays from the load start until the game can really be played (a client waiting for the host or the
		// players' loads after our StartStep mark still sees the loading screen); 10 min safety limit
		private static bool _armed;
		private static float _armedAt;

		private static bool Playable()
		{
			try
			{
				return Architecte.commence && Architecte.physicsReady
					&& (UnityEngine.Networking.NetworkServer.active || !Neo.IsMultiplayer || (GameParam.instance != null && GameParam.instance.HostLoaded));
			}
			catch (Exception) { return true; }
		}

		private static bool ShowCancel()
		{
			if (_running) return true;
			if (!_armed) return false;
			if (Playable() || Time.realtimeSinceStartup - _armedAt > 600f || BtcReplay.Mode == BtcReplay.State.Playing) { _armed = false; return false; }
			return true;
		}

		private static void DrawCancel()
		{
			Rect r = new Rect(Screen.width - 56f, Screen.height - 56f, 40f, 40f);
			Color old = GUI.color;
			GUI.color = r.Contains(Event.current.mousePosition) ? new Color(0.85f, 0.2f, 0.2f, 0.9f) : new Color(0f, 0f, 0f, 0.6f);
			GUI.DrawTexture(r, Texture2D.whiteTexture);
			GUI.color = Color.white;
			GUIStyle s = new GUIStyle(GUI.skin.label);
			s.fontSize = 26;
			s.fontStyle = FontStyle.Bold;
			s.alignment = TextAnchor.MiddleCenter;
			s.normal.textColor = Color.white;
			GUI.Label(r, "X", s);
			GUI.color = old;
			if (Event.current.type == EventType.MouseDown && r.Contains(Event.current.mousePosition))
			{
				Event.current.Use();
				_running = false;
				_armed = false;
				BtcLog.Info("LoadTimer", "load cancelled by the user");
				try { Architecte.QuitToMainMenu(); } catch (Exception e) { BtcLog.Error("LoadTimer", "cancel: " + e); }
			}
		}

		private void OnGUI()
		{
			if (useGUILayout) useGUILayout = false; // no GUILayout here: skip Unity's per-event layout pass (garbage)
			// above every other overlay (HUD -1100, launcher -1200, updater -2000): the cross must never be covered
			GUI.depth = -20000;
			if (_endPending && Time.realtimeSinceStartup > _endBy) Finish();
			if (BtcConfig.LoadCancel.Value && ShowCancel()) DrawCancel();
			if (!BtcConfig.LoadTimer.Value) return;
			string text;
			if (_running)
				text = "Loading: " + ((Stopwatch.GetTimestamp() - _start) / (double)Stopwatch.Frequency).ToString("F1") + " s";
			else if (Time.realtimeSinceStartup < _hideAt)
				text = "Loaded in " + _total.ToString("F1") + " s";
			else
				return;
			if (_style == null)
			{
				_style = new GUIStyle(GUI.skin.label);
				_style.fontSize = 22;
				_style.fontStyle = FontStyle.Bold;
				_style.alignment = TextAnchor.MiddleCenter;
				_style.normal.textColor = Color.white;
			}

			Rect r = new Rect(Screen.width * 0.5f - 160f, Screen.height - 70f, 320f, 36f);
			Color old = GUI.color;
			GUI.color = new Color(0f, 0f, 0f, 0.6f);
			GUI.DrawTexture(r, Texture2D.whiteTexture);
			GUI.color = old;
			GUI.Label(r, text, _style);
		}
	}
	/// <summary>The timer starts at the Start click (single player waits for the curtain before the host starts), not at the loader begin.</summary>
	[HarmonyLib.HarmonyPatch(typeof(GameParam), "StartGame")]
	internal static class LoadTimerStartClickPatch
	{
		private static void Prefix()
		{
			try { BtcLoadTimer.Begin(); }
			catch (Exception e) { BtcLog.Error("Hook", "LoadTimerStartClick: " + e.Message); }
		}
	}
}
