using System;
using System.Diagnostics;
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

		public static void Begin()
		{
			try
			{
				_start = Stopwatch.GetTimestamp();
				_running = true;
				_total = 0.0;
				if (_inst != null) return;
				GameObject go = new GameObject("BtcLoadTimer");
				UnityEngine.Object.DontDestroyOnLoad(go);
				go.hideFlags = HideFlags.HideAndDontSave;
				_inst = go.AddComponent<BtcLoadTimer>();
			}
			catch (Exception e) { BtcLog.Error("LoadTimer", "begin: " + e); }
		}

		public static void End()
		{
			if (!_running) return;
			_running = false;
			_total = (Stopwatch.GetTimestamp() - _start) / (double)Stopwatch.Frequency;
			_hideAt = Time.realtimeSinceStartup + 6f;
			BtcLog.Info("LoadTimer", "world load took " + _total.ToString("F1") + " s");
		}

		private void OnGUI()
		{
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
			GUI.depth = -1000;
			Rect r = new Rect(Screen.width * 0.5f - 160f, Screen.height - 70f, 320f, 36f);
			Color old = GUI.color;
			GUI.color = new Color(0f, 0f, 0f, 0.6f);
			GUI.DrawTexture(r, Texture2D.whiteTexture);
			GUI.color = old;
			GUI.Label(r, text, _style);
		}
	}
}
