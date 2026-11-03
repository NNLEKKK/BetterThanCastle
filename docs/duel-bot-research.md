# Duel bot research log (2026-10-29/30)

Method: `scripts/duel-par.ps1 -FeatList a,b,c,d -Units 8 -Matches 10 [-Opp 1] [-OppTier t]` runs 4 hidden games in parallel (own BTClogs/parN, duel-logs/parN), current bot (side 1, feature mask in `BtcDuelBot.Feat`) against the frozen bot v2 (`BtcDuelBotV2`, side 0). `scripts/duel-analyze.sh parN` = per-match metrics, `scripts/duel-tally.sh`.
Noise: n=10 has sd ~1.6 wins; only n>=40 is evidence.

Facts measured (halberdiers, 500 HP, 32 dmg):
- strike -> damage ~450-480 ms; an order sent < ~8.5 simulation frames after the strike starts cancels it; skip landing 91% with ms timers (100% from K>=9 frames), realistic cycle 1.1-1.2 s (vanilla 1.5 s, ideal 0.585 s).
- Dodging a strike by stepping away does not work (reach up to 3.5).
- 100% of first-blood victims (40/40) were units ordered to retreat/pull; 76% of retreating units die while running.
- 70% of strike pauses > 1.6 s are target switches. 30% of strikes come after such a pause.
- Redesigned bot v3 (chaos walk, push, grid cells, chaser defenders, ttd pull, formation arc) lost to v2: push 3/10, cells 3/10, defenders 1-2/10, formation 7/20, guard 8/20, survivable-retreat 6/20, ttd-pull 7/20, reach-weighted focus 7/20.
- Neutral: chaos walk 6/10, watchdog 9/20, overkill 21/40, frame skips 4/10.
- Gain: mask 2048 (engaged unit stays on its target, switches only for targets <= 8 hits) 26/40 = 65%. Combined with watchdog 16/30.
- Vs human-like tiers (v2 class with tier 3 / 2 limits) the current bot only gets 47% / 55%: skip speed alone does not decide, uptime and first blood do.

Ideas not yet done (v4): attack-uptime model (assign by travel time, no re-pathing), avoid being the first-blood victim without retreating (rotate focus damage), formation without line, per-unit stun detection.
## Update 2026-10-30 (production mask 2050056)
Measured with scripts/duel-par.ps1 (4 parallel games, n=40, 8v8, vs frozen v2 with skips): base v2 + masks 8 (ttd pull) + 2048 (stay on engaged target) + 16384 (keep running while really hit) + 65536 (chasers of withdrawn units first) = 34/40; + 131072 (wounded wait behind the line) + 262144 (target coverage) + 524288 (anti-surround) + 1048576 (cohesion) = 35/40, bot losses 0.5/match.
Vs Hunter (all units on one target, chase, no skips; kind 3): base 23/40 -> 83976: 33/40 -> 12v12 10/10 with 0 losses. Vs Shift+RMB style (kind 4): 18/20.
Bug found by the log analysis fork: hitting a unit in its own safe zone gave the WIN to the attacker (fixed: victim side wins).
Open: edge-bait unit, per-unit cells for a tight formation (only a simple "return to group slot" exists), real human tests.

## Update 2026-10-30 (after the user's 9 games on the x3 arena, 3 analysis agents + human-movement agent)
Findings from the user's logs: bot stood in a straight line at the mid for 10-30 s and never pushed; withdrawn units went 80-240 m (to the far safe zone), got blocked by enemy bodies and died standing; pull spam every tick (Advance() cleared the pull flag); 4/4 bot losses were units 28-49 m from the group; the human stops 9-10 m from the bot, orders MoveAttack x8 then GoAttackDirectly x8 on one unit, switches target within 0.6 s of every pull, and does not use walls.
Changes: rear point 9 m behind the group instead of the safe zone, trapped runner turns and fights, healed units rejoin, isolated unit regroups, arc posture with push (bit 4194304, 7/8 vs Hunter on x3 vs 5/8 without), earlier engage (12.5 m predicted), pursuit when the enemy escapes, counter-focus of attackers of a focused unit, safe-zone attack winner bug fixed.
Rejected by A/B on x3: survivable-only retreat (bit 4: 2/8), watchdog+chase rule (16/64: 2/8). Production mask 8341512.
Bug found: PostureArc compared LastDest (Y=floor) with a cell at Y=0 -> re-ordered every tick -> long orders restarted forever, units stood at the spawn.

## Update 2026-10-30 evening (25 games on x3 by the user: bot 3 wins, human 22; 5 analysis agents)
Findings: 7/7 large focus batches (8-10 units on one fresh bot unit) ended in the kill 5-9 s later, the pull did not save them (pull under a real chase: 1/5 survived; pull of an unchased unit: 12/12); every retreat at hits_left=3 died; bot clump 1.3-2.2x looser than the human at first contact; 6/7 fake baits were preceded by a bot pull (the human then switches target); the human wins by first kill + 20 s without an answer (escape rule), bot answers with 0-5 hits after the kill.
Changes: pull/retreat only for small focus (<=3 attackers) with landed hits; mass focus (>=4 attackers + 2 hits in 2 s) = stand and counter-focus the weakest attacker with the whole army; pull/rear point 3.5 m behind the group centre; compact two-rank wedge (1.55 m columns) instead of a long arc. Not yet tested in the sandbox (the user's game was running).
Sandbox check (x3, 10v10 vs Hunter, 6 matches each): mass-focus stand+counter 0/6, selective pull/retreat gating 2/6, rear point 3.5 m 5/6, compact wedge 5/6, rear+wedge together 18/24 (previous default 15/24; all four new rules together 2/24). Production mask 109004808 = old default + rear point 3.5 m + wedge. Mass-focus counter and gating stay in the code behind bits 8388608 / 16777216 (off).
Elo (anchor v2 = 1500): naive ~1000, v1 ~1200, v3-redesign ~1370, v2+stay-on-target ~1610, mask 83976 ~1800, mask 2050056 ~1830, Hunter ~1540, current production ~1730 (75% vs Hunter on x3), human ~2000-2150.

## 2026-10-30 (afternoon): Guard layer (bit 134217728, now default)
- Clinch analysis of 14 logs: pulls ordered with the nearest enemy 1-3 m away and 5-8 enemies within 6 m die; pulls at 3-5 m escape (equal speed 6 m/s). The old logic reacted to the focus, not to the approach.
- Guard(): focus dodge (>=40% of enemy army targets one unit -> it steps away at once, released when unfocused) + Rest (hp<55%: never fights, repulsion from enemies, waits 9 m behind the healthy group, back at >85%/80% with inf heal). Works while the enemy escapes too.
- Rules: space control = any unit in mid, no enemy, 10 s; escape = side with more kills.
- LESSON: errors.ps1 does NOT deploy the DLL; duel-par.ps1 -NoBuild after it tests the STALE dll (two invalid A/Bs). Run duel-par WITHOUT -NoBuild.
- Valid A/B (10v10 x3 vs Hunter, 20 games each): bot losses/match base 1.0 -> Guard 0.05 (20/20 wins both). Rest never triggered (nobody dropped below 55%). Hunter is saturated for win rate; use losses/match.
- User games 12:00-12:14 (before Guard): 12/12 user wins, first blood always the user.
