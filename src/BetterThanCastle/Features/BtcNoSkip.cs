using System.Collections.Generic;
using Brix.Game.AI;
using Brix.Game.AI.Nodes;
using UnityEngine;

namespace BetterThanCastle
{
	// Rule "ban skips": a melee unit keeps its strike cooldown across new orders. Vanilla builds a fresh AttackInfo per order, which forgets
	// NextAttackFrame, so a move-attack right before a hit cancels the rest of the animation (the "skip").
	internal static class BtcNoSkip
	{
		private static int _idx = -2;
		private static readonly Dictionary<int, float> ReadyAt = new Dictionary<int, float>();

		public static void AddRows()
		{
			BtcGameplay.Add("mov.noskip", "mov", "Ban animation skips", "Запретить скипы анимации", 0f, 0f, 1f, 1f, true, "Melee units keep the full strike cooldown after a new order, so the move-attack animation skip does not work. Everywhere, not only in duels.", "Ближники сохраняют полный кулдаун удара после нового приказа, поэтому скип анимации приказом move-attack не работает. Везде, не только в дуэлях.");
		}

		public static bool On
		{
			get
			{
				if (_idx == -2) _idx = BtcGameplay.Idx("mov.noskip");
				float[] e = BtcGameplay.E;
				return _idx >= 0 && e != null && _idx < e.Length && e[_idx] > 0.5f;
			}
		}

		// true = Strike is too early and must be dropped
		public static bool Blocked(Labor a)
		{
			float t;
			return a != null && ReadyAt.TryGetValue(a.GetInstanceID(), out t) && Time.time < t - 0.015f;
		}

		public static void Record(AttackInfo ai)
		{
			Labor a = ai.Attacker;
			if (a == null) return;
			if (ReadyAt.Count > 4000) ReadyAt.Clear();
			ReadyAt[a.GetInstanceID()] = Time.time + (ai.NextAttackFrame - ai.CurrentFrame) * 0.02f;
		}
	}
}
