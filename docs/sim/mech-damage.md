# Mechanics: halberdier receiving a melee hit (spec for headless sim)
Sources: V = ref/vanilla; measured = game/csmod/BetterThanCastle/duel-logs/*.txt (45 matches, 3116 HIT lines). Paths relative to ref/vanilla/Brix/Game/AI unless stated.

## 1. Hit pipeline (Damage/BricktronDamageReceiver.cs)
1. Attack creation: MeleeWeapon.CreateAttack (ref/vanilla/MeleeWeapon.cs:11-19): damage = Profession.MeleeDamage, ForceFactor = MeleeWeight (min 1), Lift = 1, type Melee,
   Reaction = Smash if MeleeWeight >= 5, else AttackTemplate.DefaultMelee.Reaction = Twitch (AttackTemplate.cs:78-85).
2. MeleeAttack.Apply (MeleeAttack.cs:90-98) -> target.ReceiveAttack(dmg,type,reaction,contact,force,lift,attacker) (BricktronDamageReceiver.cs:~313-320):
   a. GetReactionToAttack (:~232-252): start = expected. If Smash: DoesAttackSmash (:~437-443): non-explosive -> Smash unless (Knight && Defending && stamina>=1) or Unwavering, else Stun.
      Unwavering: Stun/Twitch -> Endure. If CanBlockAttack && reaction != Smash -> Block; else if dmg >= CurrentHP (or Suicide) -> Die.
   b. client event (sound only), c. ApplyDamage, d. ApplyReactionServer.
3. NO RNG in reaction choice: deterministic function of (attack template, victim state). There are no "probabilities".
4. CanBlockAttack (:~403-433), all must hold: not Unwavering; stamina >= RequiredStaminaForShieldBlock (1); Profession.CanBlock() (= BlockAngle stat > 0, Profession.cs:442,1130; stat from equipped items);
   if victim is not Labor.Defending: type must be Melee AND Locomotion.IsReadyToBlock() (CanBlockQuery, Locomotion4.cs:45,644) AND (not cranking or stamina >= 2.5);
   finally Vector3.Angle(force, -victim.forward) < BlockAngle (attack must come from the front cone).
5. ApplyDamage (:~262-300): if CanBlockAttack: dmg *= (1 - meleeBlockResist [prefab]); stamina absorbs: stamina >= dmg -> stamina -= dmg (Smash: stamina = 0), dmg = 0; else dmg -= stamina, stamina = 0.
   Then ApplyResistances (Damage/DamageReceiver.cs:268: Melee dmg *= 1 - _meleeResistance, prefab field, value not in V; measured 0 effect => 0).
   Resets _lastDamageTimestamp and _lastStaminaLostTimestamp (hurt delay). HP -= dmg (clamped 0..MaxHP). HP <= 0 -> _Kill (Locomotion.Kill ragdoll, lua hk_OnKilled, GameSignals.UnitDied).
   Death: HP <= 0 after damage; no overkill/death prevention. Final hit is logged as remaining HP (dmg=20 hp=0 = 500 - 15*32).
6. ApplyReactionServer (:~303-330): Twitch -> Locomotion.SmallHurt (additive upper-body "SoftHurt" layer; ends when clip finishes or 2 s, Locomotion/Hurting.cs:20-47; no state change);
   Stun -> BigHurt (full Hurting state, MatchTarget to nearest aligned cell, duration = animator clip, Hurting.cs:12-18); Block -> DoStartBlocking(0.5 s);
   Smash -> Locomotion.Smash (airborne/knockdown, stand up after; Knight force *0.6); Endure/Die -> nothing. Stun/knockdown durations are animator-clip lengths (not constants in code; need measurement). Knockback distance/direction = from `force` vector (attack Force, built in MeleeAttack.Set) - not extracted.
7. Stamina: max 5 (ctor :19; items add via SetStaminaBonus), +1/s after hurt delay. Arbalist-only: sprint/ladder drain, +16 stamina per melee hit dealt (MeleeAttack.cs:99-106).

## 2. Regen
- BrixFixedUpdate (BricktronDamageReceiver.cs:~171-191): each time _lastRegenTick.Delta >= 1 s: Heal(EffectiveRegenPerSecond) if HurtDelayPassed() (both _lastDamageTimestamp and _lastStaminaLostTimestamp Delta > _delayBeforeRegenPops, default 10 s, DamageReceiver.cs:480, prefab-serializable) or ExternalRegenerator present. Bricktron RegenPerSecond prefab value NOT in V (assume 0 in the duel without heal; not verified).
- Hurt delay resets on ANY ApplyDamage call (even fully blocked) and on stamina loss. Heal() only resets _lastHealTimestamp. Negative regen = periodic ApplyDamage.

## 3. Halberdier stats
- MaxHP = (Protection stat of equipped tools + prefab MaxHP) * CharacterLevel (Profession.cs:1054-1058, called from Toolbag.cs:429). Measured UNIT log: maxhp=500 hp=500 lvl=1. KettleHat "Protection" is an HP bonus, NOT damage reduction. Prefab/item numbers are not in ref/lua or V code (only the sum 500 is known; split hat vs base unrecoverable here).
- Melee dmg = round(max(meleeDamage stat, 12) * Rules.unitToUnitDamageMultiplier (1.0, lua gameplayTest.lua:1) * (1+0.2*(lvl-1))) (Profession.cs:1070-1088). Measured 32 per hit => Poleaxe/halberd stat 32. 500 HP = 16 hits (15x32 + 20).
- Block: needs BlockAngle stat > 0. Measured: zero 0-damage hits in 3116 => halberdiers never block in the duel (treat CanBlock = false).
- AttackCooldown = 75 frames default (Profession.cs:1288) = 1.5 s at 50 Hz sim; AttackCooldownRandomness field default 0 (:1291); level scaling /(1+0.3*(lvl-1)); level 1 in duel.

## 4. Flinch / timing
- Twitch (all halberd hits, MeleeWeight < 5 assumed) = additive animation only. Strike gating (AttackInfo.cs:623): Locomotion.IsReadyToStrike && CurrentFrame >= NextAttackFrame; not tied to hurt layer. Measured: attackers keep a ~1.5 s cycle while being hit => no flinch interrupt of strikes, no speed change seen in code.
- Stun/Smash (not produced by halberd Twitch) would enter Hurting/airborne states, blocking attacks while they last.
- Strike(): AttackInfo.cs:795-799: DoStrike then NextAttackFrame = CurrentFrame + RandomizedAttackCooldown. Frame counter NextFrame (:789) = 20 ms sim ticks. Damage lands at the animation hit point ~0.47 s after Strike() (measured D mode 460-480 ms), applied immediately in ApplyDamage; trades are independent, each swing already in progress lands on its own (not verified for a victim that dies mid-swing).
- A fresh AttackInfo (new order) resets NextAttackFrame = 0 (:242-246): cooldown forgotten => "skip" exploit; mod guard in section 6.

## 5. RNG
- Only RandomizedAttackCooldown: AttackCooldown + UnityEngine.Random.Range(-R, R) (Profession.cs:147-152), shared Unity stream, R = 0 default => effectively deterministic. Reactions/damage/block: no RNG.

## 6. Mod changes in duel (active only when BtcDuel.Active)
- Patches/DuelPatches.cs: ApplyDamage Prefix/Postfix only observe (HP before/after -> BtcDuelMatch.OnDamage, logging, LastHurtAt for dealt > 0.5); no behaviour change.
- AttackInfo.Strike Prefix: with SkipAnims OFF, cancels the strike if Time.time - u.StrikeAt < VanillaCycleMs*0.001 - 0.015 s (enforces the full 1.5 s cycle across order changes). Postfix records StrikeAt/frame. AttackInfo.NextFrame Postfix drives the "skip" schedule (SkipFrameWait ticks after strike). Labor.SendOrder Prefix: log only.
- BtcDuelMatch.HealTick (:307-336): if cfg InfiniteHeal, every 0.2 s accumulated: Heal(min(10*dt, room)) each living unit => +10 HP/s (about +2 per step). Heal() does not reset hurt-delay timers. Bots start acting after FightTime > 1.2 s.
- Profession.MeleeDamage is patched globally (Patches/ProfessionPatches.cs:88-100): BtcGameplay.MeleeDmg(job, v) * BtcHandicap.Dmg(faction), min 1. Defaults give vanilla 32 (measured). HP handicap yields maxhp = 500 (measured neutral).

## Measured (3116 HIT lines)
- dmg: 32.0 x3062; 20.0 x39; 4/8/16/24/28 x15 total. All non-32 values are kill hits (hp=0, capped by remaining HP). No 0-dmg hits (OnDamage logs enemy hits regardless of amount) => no block/resist/variance.
- D (strike -> hit, ms): main mode 440-500 (~1960 of 3116, peak 460-480), secondary cluster 300-340 (~460), tail to 1700.
- dist at hit (m): mean 2.33; peaks 1.0-1.5 (943) and 2.0-2.5 (1155); tail to 9.0 (units move during the 0.47 s swing).
- gap (since attacker's previous hit): 454 hits gap=0 (first hit); common cycle values 1371-1500 ms; SKIP flagged on 418 hits (gap < 0.9 cycle).
