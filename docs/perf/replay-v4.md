# Replay v4 (branch replay-v4): measurements

## Phase 0 (2026-11-07, Debug, hidden game)
- Client-side `OnSerialize(initial)` -> `OnDeserialize(initial)` -> `OnSerialize` round trip over the whole replay world: 0 errors, 0 byte diffs
  (85-1272 objects, 575-6436 behaviours, 1-8 ms, 4-18 KB). Scenario `replay-pk`.
- Legacy seek (before v4) on 2026-11-06_18-43-18 (56 min MP): forward 900 s = 3.2 s, rewind 936 -> 300 s = ~4 s.

## Instant seek (BtcReplaySeek), playback keyframes every 10 s
| replay | preprocess | PKs | RAM | jump |
|---|---|---|---|---|
| 2026-11-06_18-43-18 (56 min, ~100-240 objs) | 6.4 s | 339 | 3.7 MB | 7-86 ms |
| полётика1 (60 min, 1000-1600 objs incl. inactive) | 49 s | 360 | 30 MB | v1: 0.2-3 s (terrain Restore+full apply dominated) |

## Correctness (pkverify: restore PK i exactly and compare; then feed to PK i+1 and compare)
- First version: diffs in UNetAnimator only on the small replay (Play() is applied next frame: verify now compares the synced state).
- Big replay: initial OnDeserialize does not run SyncVar hooks (GenericParentTracker.currentTracker -> no re-parenting,
  CharacterState, CraftingState, AffiliationComponent): in-place restore is only allowed for hook-free parts
  (UNetTransform, UISelectable, UNetAnimator); anything else is destroyed + spawned through the normal UNET spawn path
  with the PK payload (children kept by the PK are detached and re-spawned after their parent).
- Inactive registered objects (~1000 of 1272 on полётика1: stored/hidden items) are part of the PK with their active flag.
- Remaining after respawn: a few CharacterState/CraftingState/User diffs (client re-derives fields on spawn); watch for visual issues.
