using System;
using Brix.Game.Network;
using UnityEngine;
using UnityEngine.Networking;

namespace BetterThanCastle
{
	// Gameplay > General, rule gen.noCycle (default ON, host, BC ON): no day/night change. The host keeps the sun stopped
	// (vanilla Journee pause, the same one as the movie panel "stop the sun"; UNetJourneeSync sends it to clients), and a
	// match that would start at night is moved to noon once. Turning the rule off in a match restarts the sun we stopped.
	public static class BtcNoDayCycle
	{
		private static int _idx = -2;
		private static bool _ownPause;
		private static Journee _for;

		public static void AddRows()
		{
			BtcGameplay.Add("gen.noCycle", "gen", "No day and night change", "Без смены дня и ночи", 1f, 0f, 1f, 1f, true,
				"The time of day stays as it is for the whole match (a match that would start at night starts at noon). Off = vanilla day and night.",
				"Время суток не меняется весь матч (матч, который начался бы ночью, начинается в полдень). Выкл = обычные день и ночь, как в ванили.");
		}

		private static bool On
		{
			get
			{
				if (!BtcNetworkGate.Enabled) return false;
				if (_idx == -2) _idx = BtcGameplay.Idx("gen.noCycle");
				float[] a = BtcGameplay.Active;
				return _idx >= 0 && a != null && _idx < a.Length && a[_idx] > 0.5f;
			}
		}

		/// <summary>Journee.Update prefix (host only).</summary>
		internal static void Tick(Journee j)
		{
			if (!NetworkServer.active || j == null || j != Journee.instance || BtcGameplay.InLobby) return;
			if (_for != j) { _for = j; _ownPause = false; }
			if (On)
			{
				if (j.Pause) return;
				if (!_ownPause && (j.Heure >= 19f || j.Heure <= 5f))
				{
					j.SetTime(12f);
					if (UNetJourneeSync.instance != null) UNetJourneeSync.instance.ForceUpdateHeure();
				}
				j.IsPaused = true;
				_ownPause = true;
				BtcLog.Info("DayCycle", "sun stopped at " + j.Heure.ToString("0.0") + " h (gen.noCycle)");
			}
			else if (_ownPause)
			{
				_ownPause = false;
				j.IsPaused = false;
				BtcLog.Info("DayCycle", "sun runs again (gen.noCycle off)");
			}
		}
	}
}
