# Gameplay tab: criticality list (proposal, user marks disputed rows)

Tags: **C** = critical (changes fights, economy, who wins; red/orange highlight), **m** = minor (small tweak, yellow), **N** = not a rule (client-only repair/UX; never highlighted, no banner to players).
Per-unit rows exist for 7 jobs: Builder, Halberdier, Knight, Archer, Arbalist, Alchemist, Artificer.

## Combat
| id (family) | what | tag |
|---|---|---|
| dmg.all, dmg.j0-6, dmg.m.j*, dmg.p.j* | damage x (all / per unit / melee / projectile) | C |
| dmg.bld | damage to buildings and objects | C |
| dmg.bomb | alchemist bomb explosion | C |
| dmg.bar.unit / .blk / .rad | barrel explosion damage to units, blocks, radius | C |
| hp.all, hp.j0-6 | HP | C |
| stam.all | stamina (knight etc.) | C |
| spd.all, spd.j0-6 | movement speed | C |
| rng.max.all, rng.max.j3-6 | max shooting range | C |
| rng.min.all, rng.min.j3-6 | min shooting range | C |
| sent.mark | artificer focus mark hotkey | m |

## Economy
| id | what | tag |
|---|---|---|
| eco.income | crystal energy income x | C |
| eco.penFixed, eco.penProp | death penalty | C |
| eco.stable | fix random refund | m (fixes a vanilla bug, same on average) |
| eco.base, eco.factor, eco.begin | spawn energy cost curve | C |
| eco.cap | bricktron cap | C |
| start.workers, start.knight, start.archer | starting bricktrons | C |
| craft.max | max queued craft orders | m |
| build.chop1x1 | chopping blueprint is 1x1 | m |

## World
| id | what | tag |
|---|---|---|
| ore.stone, ore.iron, ore.brim | deep layer composition | C |
| gen.maxp | max players | C |
| timer.on, timer.peace, timer.limit | advanced timer, peace time, game length | C |
| timer.ms | show milliseconds | N |
| gen.editor | editor panel for everyone | m |

## Movement and AI (bricktron behaviour)
| id | what | tag |
|---|---|---|
| mov.noskip | ban animation skips | C (removes a skill-based technique) |
| mov.allies | pass through allies | C |
| mov.shootdown | shoot straight down | C |
| mov.items | ignore items blocking paths | m |
| mov.simple, mov.dropfix | simple physics, dropped-item fix | m |
| mov.noAutoPick | do not auto-pick loose items | m |
| mov.jumpLine | group jump in a line | m |
| mov.retry, mov.retryMax, mov.retryDelay, mov.cancel, mov.repath, mov.repathSteps, mov.adv | order retry, cancel, detour | m |
| bai.* (delayOn, delay, wait, build, hold, trust) | worker AI timings | m |
| path.* (default, free, corr, build, kupd, kexc) | pathfinding search budgets (performance) | m |

## Spectators / reconnect (all N except access rules)
| id | what | tag |
|---|---|---|
| spec.allow, spec.max, spec.ret, spec.retip | who may spectate / return | m |
| spec.pause, spec.ask | pause / ask when a spectator joins | N |
| spec.facfix, spec.facidle, spec.facsel, spec.facspec, spec.faclua, spec.hudfix, spec.mapfix | client-side repair toggles | N |

## Outside the number rows (decided per stage)
- Recipes (stage 2): cost / output / time per recipe: **C** for cost and output, **m** for time of non-combat items.
- Handicap (stage 2): income / dmg / HP / penalty / start workers per player: **C**.
- Map scale and symmetry (stage 3): **C** (changes the map).
- Rows from BtcOverlays / BtcFactionFix / BtcBlockMarks / BtcQol etc. that register with `Add(...)` are visual or client-only: **N** unless I find a gameplay effect.

## User decisions (2026-11-10)
- Agreed with all C and m rows EXCEPT: `timer.*` and `gen.maxp` are minor (m), not critical.
- `timer.ms` (show milliseconds) moves to the Visuals tab (it is a client display option, not a host rule).
- Gameplay Settings = ONLY what the host configures.
- `path.*` budgets: critical (C) when set BELOW vanilla (worse pathing), minor (m) when at or above vanilla.
- The warning banner shown to clients joining non-vanilla rules is reworked: clickable, opens the full list of differences; the old "list with brackets" is removed.

## Extra convenience features approved by the user
- Changed-only filter, per-row reset to vanilla, counters of changes per section, compare two presets, copy/paste rules as a text code, favourites, Ctrl+Z.
- As a CLIENT joining a host: the view shows how the host's rules differ from YOUR rules (not from vanilla); a switch flips the base between "my rules" and "vanilla".
- Host notification when a client proposes rules; reworked proposal flow.
