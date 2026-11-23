using Brix.Game.AI.Damage;
using Brix.Game.Components;
using HarmonyLib;

namespace BetterThanCastle
{
	// G4 (Gameplay > HP, BC ON, host): custom healing. heal.aura scales Healing Ward / Artificer healing (RegenAura, applied every heal
	// tick 1/s); heal.self = bricktrons' own regeneration in HP per second (vanilla 1), heal.delay = seconds without damage before it
	// starts (vanilla 10). Own regen is only replaced while it still has the vanilla value (or our previous one), so features that set
	// their own regen (phantoms) keep it. Patches/HealRulesPatches.cs.
	public static class BtcHealRules
	{
		private static int _iAura = -2, _iSelf = -2, _iDelay = -2;
		private static float _lastSelf = 1f;
		private static readonly AccessTools.FieldRef<RegenAura, int> CurRegen = AccessTools.FieldRefAccess<RegenAura, int>("_currentRegen");
		private static readonly AccessTools.FieldRef<RegenAura, int> CfgRegen = AccessTools.FieldRefAccess<RegenAura, int>("RegenConfig");

		public static void AddRows()
		{
			BtcGameplay.Add("heal.aura", "hp", "Healing of wards and Artificers x", "Хил вардов и магов x", 1f, 0f, 10f, 0.05f, false, "Multiplier of the healing per second of Healing Wards and Artificers in healing mode (1 = vanilla).", "Множитель хила в секунду у Healing Ward и магов в режиме лечения (1 = ваниль).");
			BtcGameplay.Add("heal.self", "hp", "Own regeneration of bricktrons (HP per second, vanilla 1)", "Собственный реген бриктронов (HP в секунду, ваниль 1)", 1f, 0f, 50f, 0.5f, false, "How much HP a bricktron restores by itself every second when it has not been hurt for a while.", "Сколько HP бриктрон восстанавливает сам каждую секунду, если его какое-то время не били.");
			BtcGameplay.Add("heal.delay", "hp", "Own regeneration starts after (seconds, vanilla 10)", "Собственный реген начинается через (секунд, ваниль 10)", 10f, 0f, 120f, 1f, false, "Seconds without damage (and stamina loss) before a bricktron starts to regenerate by itself.", "Сколько секунд без урона (и траты стамины) нужно, чтобы бриктрон начал регенерировать сам.");
		}

		private static float Rule(ref int idx, string id, float def)
		{
			if (idx == -2) idx = BtcGameplay.Idx(id);
			float[] a = BtcGameplay.Active;
			return idx >= 0 && a != null && idx < a.Length ? a[idx] : def;
		}

		// RegenAura.HealTick prefix (1/s per aura)
		internal static void OnAuraTick(RegenAura aura)
		{
			if (!BtcNetworkGate.Enabled) return;
			int cfg = CfgRegen(aura);
			if (cfg <= 0 || CurRegen(aura) <= 0) return;
			float k = Rule(ref _iAura, "heal.aura", 1f);
			CurRegen(aura) = UnityEngine.Mathf.Max(k > 0f ? 1 : 0, UnityEngine.Mathf.RoundToInt(cfg * k));
		}

		// BricktronDamageReceiver.BrixFixedUpdate prefix (hot path: two array reads, two field writes)
		internal static void OnBricktronTick(BricktronDamageReceiver dr)
		{
			if (!BtcNetworkGate.Enabled) return;
			float self = Rule(ref _iSelf, "heal.self", 1f);
			float cur = dr.RegenPerSecond;
			if (cur != self && (cur == 1f || cur == _lastSelf)) dr.RegenPerSecond = self;
			_lastSelf = self;
			dr._delayBeforeRegenPops = Rule(ref _iDelay, "heal.delay", 10f);
		}
	}
}
