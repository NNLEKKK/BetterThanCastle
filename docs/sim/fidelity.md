# Sim fidelity: three gates

The duel simulator (lab/Btc.Sim + the mod's own bot sources in lab/Btc.Bots) is only useful for training and bot research if it behaves like the real game. Fidelity is checked at three levels, from micro to macro. Every mechanics change must rerun gates (a) and (b); gate (c) is rerun whenever a new real reference batch exists.

## (a) Micro timings
Single-mechanic measurements from the real game (hidden DEV lab instances) are the sim's calibration targets: see `docs/sim/measured.md` (run speed 5.95 m/s, strike->damage 17 ticks in stance / 23 ticks for a first strike, cancel window K <= 8 ticks, 1500 ms strike cycle, re-strike after a cancel 17 ticks, regen 4 HP/s after 12 s, acquisition radii). `SimSelfTest` (lab/Btc.Sim/SimSelfTest.cs) asserts these numbers and the rules (determinism per seed, safe-zone entry, safe-zone hit, kill and escape with equal-kill reset, space control). Pass condition: ALL OK. Each SimParams field is tagged `[measured]`, `[code]`, `[log]`, `[guess]` or `[sim]` in lab/Btc.Sim/DuelSim.cs; `[guess]` fields are the first suspects when gate (c) fails.

## (b) Open-loop replay on real logs
`LogReplay` (lab/Btc.Sim/LogReplay.cs) parses real duel logs (`t|TAG|msg`).
- `RulesParity`: logged positions, hits and kills drive only the sim's rules; END (winner, reason, time +-0.3 s) must match. Last result: 41/41.
- `OpenLoop`: from each logged SNAP the sim is started with the logged unit state, the logged ORD/BORD orders are injected, and predicted positions/hp are compared with the later SNAPs. Last result: position RMSE 3.8 m at the horizon versus 9.3 m for the "units stand still" baseline.
This gate tests the engine (movement, striking, targeting) with the bots taken out of the loop.

## (c) Closed-loop distribution check: `btclab fidelity`
Gates (a) and (b) cannot see errors that only appear when bot and engine interact (bot timing assumptions, reaction to latency, skip rhythm, chase behaviour). So the same matchup is played in the real game (hidden instances, many matches) and in the sim (same mod bot code in `SimBotHost`), and the distributions of match-level metrics are compared.

```
btclab fidelity --real game/csmod/BetterThanCastle/duel-logs/par0,..par3 --a v2 --b prod4 --units 10 --arena 3 --skips 1 --heal 0 --n 400
                [--threads T] [--out docs/sim/fidelity.md] [--params key=value,..] [--seed S] [--time-limit 300] [--min-real N]
```
- Real side: every `duel-*.txt` with an END line; kept only when CFG matches (units, skip, heal) and the arena size (x-spread of the first SNAP: > 200 m = x3) matches. Side 0 = "player" = `--a`, side 1 = "bot" = `--b`.
- Sim side: `--n` matches with fixed seeds, rules from the flags; the host LogSink feeds STRIKE / SKIP / KILL / END lines into the same `MatchRec` reducer that parses the real logs. HIT (distance, strike->damage D) and SNAP (every 0.2 s) are produced by a sim tap in the same line semantics. One metric code path for both sources.
- `--params`: SimParams fields (`fps=90`, `RunSpeed=5.9`, ..) and `rules.SpaceControlSec=5`. Default render fps is 60.
- Metrics per match: winner, end-reason category, length, kills / hits / skips per side, hits per minute, time to first kill, first-blood side, mean HIT distance, strike->damage D (median, p10, p90 per match), median gap between hits of one attacker, fraction of STRIKE without HIT (cancelled / aborted), mean speed of moving units (SNAP deltas), mean distance between team centroids.
- Table: real mean +- 95% CI, sim mean +- 95% CI, difference, z, verdict. Proportions use Wilson intervals and a two-proportion z. Verdict OK when |z| < 2 or the relative difference is below 10%, else MISMATCH. Exit code = number of MISMATCH rows. No multiple-comparison correction: with ~26 rows one or two false alarms at |z| ~ 2-2.5 are expected, so look for large |z| and for groups of related rows.
- Each run appends a dated section below.

Reading tips: a large |z| on a row with a tiny CI (D, speed, HIT distance) can be an immaterial offset; judge by the relative difference column too. Rows with sim sd = 0 (D quantiles) show that the sim is deterministic where the real game has render-frame jitter: raise `fps` variety or add RenderJitter to match the real spread.

---
## Results

## Closed-loop run 2026-10-31 03:51

- matchup: side 0 `v2` vs side 1 `prod4`, units 10v10, arena x3, skips on, heal off, time limit 300s
- real: 160 matches from `game/csmod/BetterThanCastle/duel-logs/par0,game/csmod/BetterThanCastle/duel-logs/par1,game/csmod/BetterThanCastle/duel-logs/par2,game/csmod/BetterThanCastle/duel-logs/par3` (160 logs scanned); sim: n=400, seed=4242, threads=16, params: defaults (RenderFps 60)
- bot statics: Feat=511657992 Feat2=63; verdict: OK if |z| < 2 or relative diff < 10%

| metric | real mean ± 95% CI (n) | sim mean ± 95% CI (n) | diff (sim-real) | z | verdict |
|---|---|---|---|---|---|
| win rate bot (side 1) | 98.1% [94.6, 99.4] (160) | 95.0% [92.4, 96.7] (400) | -3.1 pp | -1.7 | OK |
| win rate player (side 0) | 1.9% [0.6, 5.4] (160) | 4.8% [3.1, 7.3] (400) | +2.9 pp | 1.6 | OK |
| no winner / time limit | 0.0% [0.0, 2.3] (160) | 0.2% [0.0, 1.4] (400) | +0.3 pp | 0.6 | OK |
| reason: kill and escape | 96.9% [92.9, 98.7] (160) | 84.2% [80.4, 87.5] (400) | -12.6 pp | -4.1 | MISMATCH |
| reason: space control | 0.6% [0.1, 3.5] (160) | 1.5% [0.7, 3.2] (400) | +0.9 pp | 0.8 | OK |
| reason: all dead | 0.0% [0.0, 2.3] (160) | 0.0% [0.0, 1.0] (400) | +0.0 pp | 0.0 | OK |
| reason: safe zone | 2.5% [1.0, 6.3] (160) | 14.0% [10.9, 17.7] (400) | +11.5 pp | 4.0 | MISMATCH |
| reason: other | 0.0% [0.0, 2.3] (160) | 0.2% [0.0, 1.4] (400) | +0.3 pp | 0.6 | OK |
| match length t (s) | 61.63 ± 1.81 (160) | 55.96 ± 1.54 (400) | -5.67 (-9%) | -4.7 | OK |
| kills side 0 | 0.08 ± 0.05 (160) | 0.12 ± 0.04 (400) | +0.04 (+48%) | 1.3 | OK |
| kills side 1 | 1.04 ± 0.05 (160) | 1.00 ± 0.03 (400) | -0.04 (-4%) | -1.3 | OK |
| hits side 0 | 43.34 ± 2.82 (160) | 40.15 ± 1.51 (400) | -3.19 (-7%) | -2.0 | OK |
| hits side 1 | 38.84 ± 2.67 (160) | 38.33 ± 1.42 (400) | -0.51 (-1%) | -0.3 | OK |
| skips side 0 | 36.64 ± 2.45 (160) | 35.23 ± 1.54 (400) | -1.41 (-4%) | -1.0 | OK |
| skips side 1 | 48.44 ± 3.20 (160) | 41.38 ± 1.53 (400) | -7.05 (-15%) | -3.9 | MISMATCH |
| hits per minute | 77.92 ± 2.84 (160) | 83.48 ± 1.71 (400) | +5.56 (+7%) | 3.3 | OK |
| time to first kill (s) | 40.84 ± 1.54 (159) | 35.51 ± 0.69 (390) | -5.33 (-13%) | -6.2 | MISMATCH |
| first blood by bot (of matches with a kill) | 96.2% [92.0, 98.3] (159) | 98.5% [96.7, 99.3] (390) | +2.2 pp | 1.6 | OK |
| mean HIT dist (m) | 2.89 ± 0.04 (160) | 2.27 ± 0.01 (400) | -0.63 (-22%) | -31.3 | MISMATCH |
| strike->damage D median (ms) | 485 ± 1.06 (160) | 467 ± 0.00 (400) | -17.91 (-4%) | -33.0 | OK |
| D p10 (ms) | 456 ± 4.13 (160) | 449 ± 1.11 (400) | -7.71 (-2%) | -3.5 | OK |
| D p90 (ms) | 565 ± 9.69 (160) | 467 ± 0.00 (400) | -98.61 (-17%) | -19.9 | MISMATCH |
| gap between hits, same attacker, median (ms) | 1763 ± 59.51 (160) | 1432 ± 27.27 (400) | -331 (-19%) | -9.9 | MISMATCH |
| fraction of STRIKE without HIT | 0.19 ± 0.01 (160) | 0.10 ± 0.01 (400) | -0.09 (-46%) | -15.1 | MISMATCH |
| mean speed of moving units (m/s) | 5.70 ± 0.01 (160) | 5.81 ± 0.00 (400) | +0.11 (+2%) | 15.6 | OK |
| mean centroid distance (m) | 78.00 ± 0.92 (160) | 84.97 ± 0.63 (400) | +6.97 (+9%) | 12.2 | OK |

MISMATCH rows: 8 of 26 (no multiple-comparison correction: expect ~1 false alarm per 20 rows).

Largest mismatches by |z|: mean HIT dist (m) (z=31.3, real 2.89 ± 0.04, sim 2.27 ± 0.01); D p90 (ms) (z=19.9, real 565 ± 9.69, sim 467 ± 0.00); fraction of STRIKE without HIT (z=15.1, real 0.19 ± 0.01, sim 0.10 ± 0.01); gap between hits, same attacker, median (ms) (z=9.9, real 1763 ± 59.51, sim 1432 ± 27.27); time to first kill (s) (z=6.2, real 40.84 ± 1.54, sim 35.51 ± 0.69)
