# Open questions / risks (user decisions marked; remove when resolved)
1. [user] BetterConnection default ON breaks play with vanilla peers until set OFF. Auto-detect a vanilla peer (hello handshake, MsgHello 78 timeout) and fall back per session? Mixed lobbies (host ON, client OFF) are not symmetric (mod messages); animator sync was removed 2026-10-29.
2. [user] Crossbow bolt (`AttackTemplate.Bolt`): legacy 45 dmg piercing; dirty DLL has 20 melee (foreign harpoon tuning). Revisit with the clean DLL.
3. [verify] Replay start failure (user): look at `Unknown message ID 64`, NRE in `NetworkLobbyPlayer.OnSceneLoaded`, `LuaMarkerUI.Start`, "key not present" (ControlContext "Chat"/InputModeController) after the network port + clean DLL.
4. [verify] `UnityEngine.Networking.dll` in game/csmod Managed is dated 2026-07-29 (newer than the others): was it modified by the legacy mod? Compare with a pristine copy.
5. [verify] Map preview/symmetry UI absence: check `BtcGameplay.InLobby` (compares active scene name with `UNetManager.lobbyScene`; may differ in the game build) and `CanEdit` (host + lobby + BC ON).
6. [verify] CookBook crossbow recipe patch was removed (foreign-mod compensator). With the clean DLL confirm legacy recipe equals vanilla.
7. [obsolete] AnimatorParameter SendOnNetwork override removed with animator sync (2026-10-29).
8. [design] `SimpleHostScript.Run` is a copied legacy body in a Prefix (vanilla is closures; no stable transpiler anchor).
9. [obsolete] BtcAnimSync removed 2026-10-29.
10. [info] Not ported on purpose: AppDomain unhandled-exception hook (diagnostics), `BcEnsureLoaded/BcSave` (BepInEx config replaces), UNetAnimator Locomotion4 caching (local perf).
11. [info] User data folders (<game>/BetterThanCastle: ResourcePacks, configs, Avatars, Plates, debug in Release) are created at runtime, not shipped files.
