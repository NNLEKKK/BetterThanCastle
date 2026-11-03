# Measured duel mechanics (real game, hidden DEV instances, 2026-10-31)
Halberdier vs halberdier, level 1, flat plateau. Measured with the DEV lab commands (src/BetterThanCastle/Dev/BtcDevLab.cs: duelo, duelplace, duelcancel, duelnobot, duelnorules, dueldump, duellog) and the per-FixedUpdate TRACE (BtcDevDuelTrace, extended with tg/want/locomotion flags `loc=` R run, W walk, K stance, Y ready, C crank, S swing, X react, O turn-on-spot).
Scenarios: tests/scenarios/lab-kin.txt, lab-strike.txt, lab-strike2.txt (rematch per rep), lab-chase.txt, lab-cancel.txt; runner `scripts/lab-run.ps1 -Names a,b,..` (BTC_LOGDIR=labN, duel logs in game/csmod/BetterThanCastle/duel-logs/labN). Raw logs of this session were overwritten by later runs except lab-strike2; analysis scripts (awk) lived in the session scratchpad: ext.awk (TRACE -> rows), kin.awk, strike.awk, coll.awk, cancel.awk.
Caveats: the user config has arena scale 3 (300x60), so the lab ran on the big arena (mechanics are size-independent). 4 instances in parallel: render fps 50-180 with hitches (several FixedUpdates without a render frame: positions freeze then jump). Fixed dt = 0.0200 s always.

## Prefab numbers (dueldump, runtime values of the halberdier; they override the class defaults in mech-locomotion.md)
| field | value | note |
|---|---|---|
| Running.RunSpeedMperS | 7 | but measured cruise is 5.9-6.0 m/s (see below) |
| Running.RunArriveSpeedMperS / RunArrivalDistance / RunAbruptStopDistance | 2.5 / 1 / 0.75 | |
| Running.RunSpeedStrikeBonus | 3 | |
| Running/Walking TurningTopSpeed | 1 | |
| Walking.WalkSpeedMperS / WalkArriveSpeedMperS | 4 / 2.5 | |
| Run/Walk BlockedDelay, BlockedMoveFactor, BlockedMaxCount | 0.1 s, 0.1, 20 | collision stall logic |
| TurnOnSpot.TurningSpeed | 2 (720 deg/s) | |
| Stepping.CombatStepSpeed | 5 | |
| ReadyToStrike.IdleTimeoutDelay | 20 | |
| Locomotion4.FloorOffset / PathAnticipationTime | 0.02 / 1 | |
| Profession.AttackCooldown / Randomness | 75 / 0 | |
| Profession.BaseAggroRange | 15 | |
| DamageReceiver MaxHP / regenPerSecond / delayBeforeRegenPops | 500 / 4 / 12 s | |
| melee/other resistances, _meleeBlockResist | 0 | |
| BricktronDamageReceiver max stamina / StaminaRegenPerSecond | 200 / 10 | RequiredStaminaForShieldBlock 0.01 (no block seen anyway) |
| LocalFactionVision.LineOfSightRange | 50 | |
| AttackInfo.MeleeSearchRadius (idle unit) | 3 | |
| Animator clips (30 fps) | Halberdier_Attack_Crank 0.267 s, Attack_Swing 0.067 s, Attack_Hit 0.567 s, Attack_Blocked 0.567, AbortStrike 0.033, Halberd_RunCycle 0.533, Halberdier_WalkCycle 0.533, TurnOnSpot 090 0.267 / 180 0.333 | |

## Parameter table
| parameter | value | spread | n | method / source |
|---|---|---|---|---|
| run cruise speed | 5.95 m/s | 5.78-6.09 | 10 straight legs (25-75% progress), 3 fps settings | lab-kin KIN-* |
| order -> first movement | 0.12-0.14 s (high fps); 0.20 s at 20 fps; up to 0.36 s | | 30 | lab-kin |
| acceleration | none visible: cruise speed reached within the first 0.1-0.2 s | | 10 | lab-kin |
| 40 m straight run, order -> stop | 7.0 s (6.96-7.08) | | 6 | lab-kin |
| short moves order -> arrive | 1 m: 0.62-0.70 s (0.98 @20fps); 2 m: 0.66-0.82; 3 m: 0.92-1.12; 2.83 m diag: 0.86; 5.8 m: 1.40-1.72 | | 3 each | lab-kin; 1-2 m moves use Walk/close-step (loc W or none), not Run |
| arrival | exactly on the integer target cell centre, no overshoot (along/across error 0.00) | | 30 | lab-kin |
| reversal while running (order back at speed) | moves in the new direction from the next frame; facing rotates 180 deg in ~0.24 s while moving (~750 deg/s), no stop | | 2 | lab-kin |
| turn on spot (TurnTo order) | 90 deg in 0.12-0.18 s after ~0.06 s delay (~600-720 deg/s) | | 3 | lab-kin |
| 90 deg course change while running | smooth, arc ~1 cell, no speed loss visible | | 2 | lab-kin |
| strike start -> damage, unit already in stance | 0.34 s (17 ticks) mode; mean 0.37 incl. hitch outliers | 0.33-0.62 | 109 | lab-strike/strike2 |
| strike start -> damage, first strike after an approach / new order | 0.45-0.46 s (23 ticks) mode | 0.44-0.89 | 22 | same |
| phases (in stance) | crank flag +0.02, swing +0.28, react(=damage) +0.34, react end/ready +0.88 | | 109 | loc flags |
| phases (first strike) | crank +0.12-0.14, swing +0.40, damage +0.46, ready +1.00 | | 22 | loc flags |
| fps dependence of strike | 20 fps: in-stance hit 0.38 s (sd 0.005), first 0.53 s; 60 fps: 0.36 / 0.48 | | 28+8 per fps | lab-strike2 S20/S60 |
| strike cycle (no orders, no skip) | 1500 ms exactly (since=1500, a few 1499/1540) | | ~140 | STRIKE since= |
| first strike after Attack order from 20 m (target idle) | 2.9-3.1 s; mutual attack 1.8-2.1 s | | 8 | sinceOrd |
| strike start geometry | first strike starts at cell offset (3,0) or (1,2)/(2,1)-like (d 2.2-3.0) and the attacker lunges ~1.0 m into reach during the crank; later strikes from offsets (1,0) d=1, (1,1) d=1.41, (2,0) d=2 | | 131 | strike.awk |
| facing gate | none: strikes started with facing error up to 89 deg | | 131 | |
| hit distance check | none: hits landed with the victim 3.9-5.5 m away (it ran during the swing) | | 6 | chase, line-enemy |
| cancel window | an order K ticks after strike start cancels the damage for K <= 8; K >= 9 the hit lands (K=9: 8/9, K>=10 all) | | 16 K values x ~12 strikes | lab-cancel (Attack re-issue; same for skip/moveattack modes; Move at K=10 lands 6/9) |
| re-strike after a cancelling order (K<=8) | 340 ms (17 ticks) after the order, no cooldown | 320-360 | ~150 | lab-cancel |
| re-strike after an order at K>=9 | bimodal: 360 ms or 680-700 ms after the order | | ~80 | lab-cancel |
| fastest skip cycle (order at K=9-10) | 560-580 ms, half the time ~880 ms; hits of re-ordered strikes use the 0.45 s "first strike" timing | | | lab-cancel |
| damage | 32 per hit, no block, no variance | | all | |
| regen | starts 12.0 s after the last damage, +4 HP per 1 s tick, also while running | | 1 unit, 60 s | lab-kin REGEN |
| idle auto-engage | d<=3 (incl. diagonal (1,1)): HoldPositionAndAttackWithPriority, first strike 0.4-0.8 s after placement, no movement; d=4,6,10: GoAttackWithPriority chase, first strike 1.15/1.4/1.8 s; d=15, 20: nothing | | 9 placements | lab-cancel IDLE-* |
| aggro (idle unit reacts to a passing runner) | reacted at 10.5-13.5 m; runner passing at 16 m lateral: no reaction | | 8 | MA-OFF* |
| MoveAttack acquisition | acquires the target at 7.3-8.8 m straight-line distance (B was already coming); passing at 16 m: no acquisition | | 8 | MA-OFF* (lab-strike2) |
| chase, equal speed | chaser never catches a fleeing unit: gap 7 m -> 9 m over 15 s (~0.13 m/s lost); chaser leaves stance ~1.0-1.5 s after its own strike start | | 3 | lab-chase |
| chase with turns | chaser cuts corners: gap 7 -> 5 m at each 90 deg turn of the target | | 1 | C-TURNS |
| instinct chase leash | none: a unit that auto-engaged (GoAttackWithPriority) followed the runner 140 m | | 1 | REGEN segment |
| min centre distance, head-on movers | 1.0-1.1 m | | 3 | lab-chase D-* |
| min centre distance, crowding to one point | transient 0.0 m (two units in the same spot), 0.54-0.85 m while moving | | 2 | D-CROWD, D-LINE |

## Behaviours (precise enough to implement)
1. Coordinates: units at rest sit exactly on integer world x,z (= voxel centres; spawn 2.5 -> 3.0, i.e. round to nearest). Arrival always snaps to the target cell centre. In motion positions are continuous. Positions are updated per RENDER frame (several FixedUpdates may see the same position, then a jump); time-average speed is still 5.95 m/s. Use 6 m/s continuous in the sim, not 7.
2. Paths: short/medium moves follow an octile path that runs STRAIGHT FIRST then 45-degree diagonal (60,10 -> 100,20 went along z=10 to x~90 then diagonal). Long moves (>30 m) use a coarse hierarchical path with waypoints near 16-voxel chunk borders: a 45-degree 42 m order went 25 m along x, then mostly z, path 45.6 m and did not arrive in 8 s. Model: octile A* for <= ~30 m; long orders are inefficient (+10-40% path).
3. Movement start: 0.12 s after the order (6 ticks), full speed immediately; stop: instant at the cell centre (final 2.5 m/s slide < 0.5 m is invisible in the data). 1-2 m moves are walk/close steps and take 0.65-0.8 s total.
4. Strike: needs target in reach of the cell it occupies OR is entering; the first strike of an approach starts at distance 3 (or offset (1,2)) and the attacker is moved one cell forward during the crank. Damage at +17 ticks in stance, +23 ticks for a first strike (draw/lunge), damage is applied regardless of distance or facing (<9.5 m). Cooldown 75 ticks from strike start. 20 fps adds ~2 ticks (render-frame quantisation of animator transitions).
5. Order during a strike: K<=8 ticks after strike start -> no damage, new strike possible after 17 ticks (no cooldown). K>=9 -> damage still lands, next strike after 18 or 34-35 ticks. This is the skip exploit; the best realistic cycle is ~0.56-0.88 s vs 1.5 s.
6. Idle units: enemy within reach (<= 3 incl. diagonal) -> hold and attack in place; enemy 4..~13 m -> GoAttackWithPriority (chase) with no leash; >= 15 m ignored. Units under a Move order do not stop to fight (enemy head-on Move: they passed each other).
7. Blocking: pathing avoids cells occupied at plan time (a runner detours around a standing ally/enemy or a 5-unit wall, path +5-30%); moving units do not hard-collide: head-on pairs both sidestep one cell, one stalls ~0.5 s (blocked delay), they pass at ~1.0 m. Standing units are never pushed. Several units ordered to one point end on the point and its 4 neighbours, transiently overlapping.

## Not measured / unreliable
- Walk speed (no walk orders issued), combat step speed, knockback/stun (halberd never produces them), block (never happens).
- Exact aggro radius (only bracketed 10-15 m) and BFS tie-breaks with several enemies; 5-frame retarget flips.
- Turn rate while in combat stance (TurningTo), landing-stun duration.
- Path shape is terrain/chunk dependent; only measured on this plateau.
- lab-strike run 1 lost its fps and MoveAttack parts (a unit died despite heal: 10 HP/s < 21 HP/s); rerun as lab-strike2 with a rematch per rep. lab-chase lost D-CROWD (match ended: a unit was hit in its safe zone after a teleport), rerun in lab-strike2.

## Order streams (2026-11-01, tests/scenarios/lab-reorder, lab-reorder2..4; `btclab reorder-lab [--set 2]`, `btclab trace-unit`)
- A unit that is running (or stopped from a Move < ~0.5 s ago) takes a new order only ~0.35 s after it was given (reversal 0.35-0.45 s in lab-reorder4; 0.35 best fit on a real LuminaZero match); a newer order replaces the pending one with a fresh timer, so streams faster than ~0.35 s never change the course (SH2/SH5: the unit does the first move only). Idle units: unchanged 0.12 s start.
- MoveAttack / Attack on a unit that is not running are applied at once (MoveAttack spam every 0.1 s: 0 m, the AI search restarts).
- Sim: SimParams.BusyOrders / BusyOrderSec 0.35 / BusyWindowSec 0.5. Real LuminaZero match replay: LZ unit path error 243 m -> 44 m (6 units, 15 s); bot and human units unchanged.
- The old note "reversal while running moves the new way from the next frame" is wrong for a running unit (2 samples); superseded by this section.
- Unit blocking (lab-block2, 2026-11-01): an enemy or ally standing on the run line makes the runner side-step ~0.8-1.0 m when passing it, an enemy 1+ m beside the line causes no deviation; sim within ~1 m (trace-unit on the lab log). Game path search blocks voxels with character volumes (SearchPathRequest.CanCrossAt -> VoxelPresence presence tests: enemies always block, allies only when they cannot pass through). lab-block (teleported 3-enemy layouts) showed a 4 m early detour in every layout: suspected stale presence after `duelplace`, not reproduced in lab-block2.
- Hidden test games do not start the duel while a 15-thread CPU training runs (load starves): pause training for lab scenarios.
