# Vanilla network audit (2026-11-22, static read of ref/vanilla)

Scale: 72 networked classes, 113 [Command], 25 [ClientRpc], 160 [SyncVar], 7 [SyncEvent]. Host-authoritative UNET HLAPI.
Transport (UNetManager.ManuallyInitTransport): channel 0 ReliableSequenced, channel 1 Unreliable, Acks64, MaxSentMessageQueueSize 2048,
all other LLAPI values default (ResendTimeout, SendDelay, PacketSize). Only UNetTransform and UNetAnimator use channel 1.

## Findings (priority order)
1. FOG OF WAR = SPAWN/DESTROY. LocalFactionVision.OnRebuildObservers adds a client only while its faction sees the object, so every
   enemy unit/building entering vision is spawned on that client from scratch (initial state) and destroyed when it leaves.
   a) Holder links (GenericParentTracker TrackingPair) read the GameObject at once: item spawned before its holder = kit lost.
      FIXED 2026-11-22 ca438b0 (Net/BtcTrackFix.cs, BC ON or replay): also covers rejoin and replay seek.
   b) Initial OnDeserialize assigns SyncVar fields without hooks; classes without a client start that re-applies them stay wrong
      after a fog spawn or rejoin: Porte (open door looks closed: ForceOpen not called), BearTrapTrigger (shut trap looks open),
      WardAttack/MagitronWard heat/glow, RecepteurState exclusive (Recepteur exclusions not rebuilt), AffiliationComponent
      FactionChangedActions, CraftingState, CaptureProgress. Replay seek already re-runs hooks (BtcReplaySeek.ForceHooks).
      TODO: run state hooks (whitelist, no sound/trigger ones like WardAttack.OnFireChanged) after a client spawn.
   c) An army leaving fog = burst of spawn messages on the reliable channel + Instantiate on the client: suspected spike. TODO measure.
2. POSITIONS: UNetTransform sends at 6 Hz (SendRate 0.1667), client lerps exponentially (InterpolationRate 9): ~200-300 ms of
   visual lag on top of ping, stutter. It is a SyncVar delta on the unreliable channel: UNET clears dirty bits after sending, so a
   lost last packet of a unit that stopped leaves it off-position until it moves again. Locomotion4 has no sync of its own.
   TODO (BC ON): timestamped snapshot interpolation like BtcAnimNet + resend of the final pose.
3. ONE RELIABLE PIPE: HP/stamina SyncVars, damage SyncEvents, RpcDie, every arrow (ProjectileWeapon spawns a network object per
   shot), terrain deblocs (msgs 51-53, SendToAll), Lua msg 55, fog spawns all share channel 0 ReliableSequenced: one lost packet
   holds everything behind it until the resend (head-of-line). TODO: measure under clumsy loss (scenario with .net.txt) before
   changing channels (channel lists must match on both ends).
4. RPC-ONLY STATE (lost on rejoin, fog spawn, replay seek): stockpile exclusions (UNetRecepteurCmd.RpcAdd/RemoveExclusion,
   UNetProjectCmd.RpcMakeExclusive), Explosifs.RpcStartFuse (lit barrel), highlights. TODO: host resends on join (BC ON).
5. Faction ally/owner lists (SyncListGameObject) read GameObjects at once on initial state: same class of bug as 1a (partly
   handled by the rejoin owner-list fix).
