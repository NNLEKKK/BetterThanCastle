# Stubs and shims (things the old mod added to VANILLA types; replace stubs in the named wave)
| shim | file | replaces (old mod, in vanilla class) | status |
|---|---|---|---|
| BtcBc.Bc* settings | Shims/BtcBc.cs | UNetManager.BcEnabled/BcShowPing/BcShowPauseLabel/BcDiagLog/BcSet*/BcCanChangeEnabled/BcIsHost | REAL (BepInEx config) |
| BtcBc.Bc* network | Shims/BtcBc.cs | UNetManager.BcInSession/BcVoluntaryPause*/BcClientsCanPause/BcTogglePause/BcToggleClientPausePermission/BcStatusText/BcGetPingLines/BtcIsLoadingLevel | REAL (wave 4): forwards to Net/BtcNetManager (per-UNetManager State dict; hooks in Patches/UNetManagerPatches.cs) |
| BtcNetManager | Net/BtcNetManager.cs | UNetManager fields/methods added by the old mod (heartbeat 64, ping 70/71, voluntary pause 72-74, hard pause, reconnect loop, transport DisconnectTimeout/m_MaxBufferedPackets, OnGUI overlay -> BtcNetOverlay component) | REAL (wave 4); state dropped in BtcNetOverlay.OnDestroy |
| BtcStr | Util/BtcStr.cs | compiler string-switch hash (dnSpy artifact) | REAL |
Old mod settings files (configs/*.txt in <game>/BetterThanCastle) are still used by ResourcePackManager/BtcHotkeys; betterconnection.txt is superseded by BepInEx config (Network section).
