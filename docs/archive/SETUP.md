# Setup (once)
1. Put decompiles into: ref/vanilla (vanilla Assembly-CSharp), ref/mod-old (old modded Assembly-CSharp), ref/lua (game Lua).
   Different names are fine: write real paths into the first prompt.
2. Install BepInEx 5.4.x (Mono, matching x64/x86) into the game folder, run the game once.
3. Check: `dotnet --version`.
4. Open Claude Code in this folder, press Shift+Tab to Plan mode, run `/model opusplan`,
   paste the prompt from first-session-prompt.md (fill in the <...> paths).
5. Approve the plan -> the agent executes the bootstrap. Then: one feature per session, /clear between.
