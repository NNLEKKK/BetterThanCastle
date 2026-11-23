using System;
using System.Collections.Generic;
using Brix.Engine;
using Brix.External.Factories;
using Brix.Lua;
using HarmonyLib;
using MoonSharp.Interpreter;
using UnityEngine;
using UnityEngine.UI;

namespace BetterThanCastle
{
    /// <summary>
    /// The developers' Editor (TestDebugSidebar.lua) and movie (TestMovieSidebar.lua) sidebars of the right bar, rebuilt for the C# game
    /// menu. Shown while debug menus are allowed (Architecte.allowDebugMenu: the host rule gen.editor in multiplayer, the settings
    /// "toggle debug" button otherwise). The UI is our prefab kit; every button runs the SAME Lua statement as the original sidebar, in a
    /// small script of our own loaded with the game's full Lua library (LuaLibs.BigLib, like a gamemode), so the behaviour is the
    /// game's. Built on first open. Vanilla bugs fixed: the raid buttons used an undefined `raid` (they errored before spawning).
    /// </summary>
    internal static class BtcGhDev
    {
        // ------------------------------------------------------------------ Lua side (the original sidebar functions, verbatim where possible)
        private const string Prelude = @"
selectedObject = nil
if GetRounded == nil then GetRounded = function(n, d) local m = 10 ^ (d or 0) return math.floor(n * m + 0.5) / m end end
__btcFactions = Faction:GetAll()
__btcFaction = nil
for x, f in pairs(__btcFactions) do if f:HasOwner() then __btcFaction = f end end
__btcLifebar = true
pcall(function() Hooks.Connect(UISelectable.hk_OnSelected, function(s) selectedObject = s end) end)
pcall(function() Hooks.Connect(UISelectable.hk_OnUnselected, function(s) selectedObject = nil end) end)
local function at() return MainCamera.targetLocation end
local function near() return Random.VoxelAround(MainCamera.targetLocation) end
__btcSpawn = function(kind, tools)
  local u = kind:New(__btcFaction, at())
  if tools then for i = 1, #tools do u:EquipTool(tools[i]) end end
end
__btcRaid = function(list, name)
  local r = fy_RaidProject:New(__btcFaction, Vector3.zero)
  r:SetBeaconPosition(fy_Crystal:GetFirst().position)
  for i = 1, #list do Project.AssignLabor(r, list[i]:New(__btcFaction, near())) end
  if name then r.name = name end
end
__btcResetAI = function() if selectedObject then local p = selectedObject.position local t = selectedObject.type selectedObject:Destroy() t:New(__btcFaction, p) end end
__btcLife = function()
  __btcLifebar = not __btcLifebar
  for _, k in pairs({fy_Bricktron, fy_Minitron, fy_Corruptron, fy_Biftron}) do for x, u in pairs(k:GetAll()) do u:ToggleDisplayCircle(__btcLifebar) end end
end
__btcHideBalise = function() for x, p in pairs(Project.GetAll()) do Project.ToggleProjectInvisibility(p) end end
__btcFPS = function()
  MainCamera.FPSmode = not MainCamera.FPSmode
  MainCamera.chosenBricktron = selectedObject
  if MainCamera.FPSmode then MainCamera:RegisterPositionAndRotation() selectedObject:EnableManualOverride()
  else selectedObject:DisableManualOverride() MainCamera:RepositionCamera() end
end
__btcFog = function(i) local a, b, c = GetFogColor() if i == 1 then return a elseif i == 2 then return b end return c end
";

        private static Script _lua;
        private static bool _hooked;

        private static Script Lua()
        {
            if (_lua != null) return _lua;
            try
            {
                Script s;
                LuaLoader.LoadString(out s, "return true", null, LuaLibs.BigLib, null);
                s.DoString(Prelude, null, "btc_devpanels");
                _lua = s;
                if (!_hooked) { _hooked = true; GameSignals.Connect(GameSignals.LeaveGame, delegate { Kill(); }, null); }
            }
            catch (Exception e) { BtcLog.Error("DevPanel", "lua: " + e.Message); }
            return _lua;
        }

        private static void Kill()
        {
            if (_lua == null) return;
            try { Script s = _lua; Script.Kill(ref s); } catch { }
            _lua = null;
        }

        internal static void Run(string code)
        {
            Script s = Lua();
            if (s == null) return;
            try { s.DoString(code, null, "btc_dev"); }
            catch (Exception e) { BtcLog.Warn("DevPanel", code + " -> " + e.Message); }
        }

        internal static string Eval(string expr)
        {
            Script s = Lua();
            if (s == null) return "";
            try { DynValue v = s.DoString("return tostring(" + expr + ")", null, "btc_dev"); return v.ToPrintString(); }
            catch (Exception) { return "?"; }
        }

        /// "Compact panel" (remembered): no faces, scarves, reset AI or raids, only spawn / delete / kill and the rest
        internal static bool Compact
        {
            get { return BtcMenuState.Bool("ghdev:compact", false); }
            set { BtcMenuState.SetBool("ghdev:compact", value); }
        }

        // debug menus allowed (MP: the host rule gen.editor sets this for every peer) or, in single player, the editor panel rule itself
        internal static bool Allowed { get { try { return Architecte.allowDebugMenu || (!Neo.IsMultiplayer && BtcGameplay.EditorPanelOn); } catch { return false; } } }

        private static bool _lastAllowed;
        /// once per frame (BtcEditorShare.Tick): the rule or the debug toggle changed -> rail buttons appear / an open dev sidebar closes
        internal static void Poll()
        {
            bool a = Allowed;
            if (a == _lastAllowed) return;
            _lastAllowed = a;
            try { BtcGhRightBar.CheckDev(); BtcGameHud.RefreshAll(); } catch { }
        }

        // ------------------------------------------------------------------ widgets (LabeledDividerPanel2, LabeledIconPanel, LabeledButtonPanel, dropdown, slider, inputs)
        internal sealed class Ctx
        {
            internal BtcGhSidebar Bar;
            internal readonly List<Action> Refresh = new List<Action>();
            internal RectTransform Grid;
        }

        private static void Divider(Ctx c, string label) { BtcGhKit.SectionHeader(c.Bar.Body, label, BtcGhKit.Red); c.Grid = null; }

        private static void Icon(Ctx c, string icon, string label, string lua)
        {
            if (c.Grid == null) c.Grid = BtcGhKit.Grid(c.Bar.Body, 64f, 64f);
            BtcGhButton b = BtcGhKit.GridButton(c.Grid, BtcGhKit.Icon(icon), Color.white, 40f, label, true, null, false);
            b.OnAction = delegate { Run(lua); };
            c.Bar.Add(b);
            // LabeledIconPanel: the label is always visible under the icon (not only on hover)
            RectTransform l = BtcGhKit.Make(Texts.Default, b.transform.parent, "");
            BtcGhKit.Rect(l, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 1f), new Vector2(0f, 14f));
            Text t = BtcGhKit.Txt(l, label, 10, BtcGhKit.Gray, TextAnchor.LowerCenter);
            if (t != null) { t.raycastTarget = false; t.horizontalOverflow = HorizontalWrapMode.Wrap; t.verticalOverflow = VerticalWrapMode.Overflow; }
            BtcGhKit.Le(l, -1f, -1f, -1f, -1f, -1f, -1f, true);
        }

        private static RectTransform Row(Ctx c, float h)
        {
            c.Grid = null;
            RectTransform r = BtcGhKit.Box(Layouts.Horizontal, c.Bar.Body, "", 0, 0, 0, 0, 4f, TextAnchor.MiddleLeft);
            BtcGhKit.Le(r, -1f, h, -1f, h, 1f, -1f, false);
            return r;
        }

        private static Text Label(RectTransform row, string s, float w)
        {
            RectTransform l = BtcGhKit.Make(Texts.Default, row, "");
            Text t = BtcGhKit.Txt(l, s, 14, BtcGhKit.Gray, TextAnchor.MiddleLeft);
            if (t != null && BtcGhKit.Korolev != null) t.font = BtcGhKit.Korolev;
            BtcGhKit.Le(l, w, 24f, w, 24f, w > 0f ? -1f : 1f, -1f, false);
            return t;
        }

        // LabeledButtonPanel: a wide text button (label = caption)
        private static void TextButton(Ctx c, string label, string lua, Func<string> caption) { CsButton(c, label, delegate { Run(lua); }, caption); }

        private static void CsButton(Ctx c, string label, Action act, Func<string> caption)
        {
            RectTransform row = Row(c, 32f);
            RectTransform cell = BtcGhKit.Make(Layouts.Horizontal, row, "");
            BtcGhKit.Le(cell, 240f, 32f, 240f, 32f, 1f, -1f, false);
            BtcGhKit.Img(cell, null, BtcGhKit.RailBg, true);
            Text t = Label(cell, label, 0f);
            if (t != null) { t.alignment = TextAnchor.MiddleCenter; t.color = Color.white; }
            BtcGhButton b = cell.gameObject.AddComponent<BtcGhButton>();
            b.Root = cell.GetComponent<Image>();
            b.BgColor = delegate { return b.Hovering ? BtcGhKit.Selected : BtcGhKit.RailBg; };
            if (caption != null && t != null) t.text = caption();
            b.OnAction = delegate { act(); if (caption != null && t != null) t.text = caption(); };
            if (caption != null) c.Refresh.Add(delegate { if (t != null) t.text = caption(); });
            c.Bar.Add(b);
        }

        private static Transform Find(Transform t, string name)
        {
            for (int i = 0; i < t.childCount; i++)
            {
                Transform ch = t.GetChild(i);
                if (ch.name == name) return ch;
                Transform r = Find(ch, name);
                if (r != null) return r;
            }
            return null;
        }

        private static void Dropdown(Ctx c, string label, string[] options, Action<int> set)
        {
            RectTransform row = Row(c, 32f);
            RectTransform dd = BtcGhKit.Make(Buttons.Dropdown, row, "");
            BtcGhKit.Le(dd, 250f, 32f, 250f, 32f, 1f, -1f, false);
            Image ddImg = dd != null ? dd.GetComponent<Image>() : null;
            if (ddImg != null) ddImg.enabled = false;
            Transform key = dd != null ? Find(dd, "KeyLabel") : null;
            Text keyT = key != null ? key.GetComponent<Text>() : null;
            if (keyT != null) keyT.text = label;
            Transform keyBox = dd != null ? Find(dd, "Key") : null;
            LayoutElement kle = keyBox != null ? keyBox.GetComponent<LayoutElement>() : null;
            if (kle != null) { kle.minWidth = 110f; kle.preferredWidth = 110f; }
            Dropdown d = dd != null ? dd.GetComponentInChildren<Dropdown>(true) : null;
            if (d == null) { BtcLog.Warn("DevPanel", "dropdown prefab without Dropdown: " + label); return; }
            d.ClearOptions();
            d.AddOptions(new List<string>(options));
            d.value = 0;
            d.RefreshShownValue();
            Transform vl = Find(dd, "ValueLabel");
            Text vt = vl != null ? vl.GetComponent<Text>() : null;
            if (vt != null) vt.text = options.Length > 0 ? options[0] : "";
            d.onValueChanged.RemoveAllListeners();
            d.onValueChanged.AddListener(delegate(int v) { if (vt != null && v >= 0 && v < options.Length) vt.text = options[v]; try { set(v); } catch (Exception e) { BtcLog.Warn("DevPanel", label + ": " + e.Message); } });
        }

        private static void Slider(Ctx c, string label, float min, float max, string getExpr, string setFmt)
        {
            RectTransform row = Row(c, 32f);
            Label(row, label, 110f);
            // LabeledSliderPanel.lua: Panels.Slider (prefab Sliders.Short) + a value label
            RectTransform sl = BtcGhKit.Make(Sliders.Short, row, "");
            BtcGhKit.Le(sl, 100f, 24f, 100f, 24f, 1f, -1f, false);
            Slider s = sl != null ? sl.GetComponentInChildren<Slider>(true) : null;
            InputField f = null;
            Text val = Label(row, "", 36f);
            if (val != null) { val.alignment = TextAnchor.MiddleRight; val.color = Color.white; }
            if (s == null) { BtcLog.Warn("DevPanel", "slider prefab without Slider: " + label); return; }
            s.minValue = min; s.maxValue = max;
            bool busy = false;
            Action pull = delegate
            {
                float v;
                if (!float.TryParse(Eval(getExpr), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out v)) return;
                busy = true; s.value = v; if (val != null) val.text = v.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture); busy = false;
            };
            s.onValueChanged.RemoveAllListeners();
            s.onValueChanged.AddListener(delegate(float v)
            {
                if (busy) return;
                Run(string.Format(System.Globalization.CultureInfo.InvariantCulture, setFmt, v));
                if (val != null) val.text = v.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
            });
            if (f != null)
            {
                f.onEndEdit.RemoveAllListeners();
                f.onEndEdit.AddListener(delegate(string txt)
                {
                    float v;
                    if (float.TryParse(txt, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out v)) s.value = Mathf.Clamp(v, min, max);
                });
            }
            c.Refresh.Add(pull);
        }

        // Vector1/2/3InputPanel: label + 1..3 fields; getters / setters are Lua (setter gets {0} = the typed number)
        private static void Inputs(Ctx c, string label, string[] get, string[] set)
        {
            RectTransform row = Row(c, 32f);
            Label(row, label, 110f);
            for (int i = 0; i < get.Length; i++)
            {
                RectTransform fi = BtcGhKit.Make(InputFields.ReskinInput, row, "");
                float w = 150f / get.Length - 4f;
                BtcGhKit.Le(fi, w, 26f, w, 26f, -1f, -1f, false);
                InputField f = fi != null ? fi.GetComponentInChildren<InputField>(true) : null;
                if (f == null) continue;
                string g = get[i], st = set[i];
                f.contentType = InputField.ContentType.DecimalNumber;
                f.onEndEdit.RemoveAllListeners();
                f.onEndEdit.AddListener(delegate(string txt)
                {
                    float v;
                    if (!float.TryParse(txt, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out v)) return;
                    Run(string.Format(System.Globalization.CultureInfo.InvariantCulture, st, v));
                    f.text = Eval(g);
                });
                c.Refresh.Add(delegate { if (!f.isFocused) f.text = Eval(g); });
            }
        }

        private static void Note(Ctx c, string s)
        {
            RectTransform row = Row(c, 24f);
            Label(row, s, 0f);
        }

        // ------------------------------------------------------------------ Editor (TestDebugSidebar.lua)
        private static readonly string[] Faces = { "Blank", "Happy", "Sad", "Pleased", "Angry", "Confused", "Tired", "Irritated", "WTF", "Serious", "Incredulous", "Confident", "Fierce", "Pouty", "Rage", "Ill" };

        internal static Ctx BuildEditor(BtcGhSidebar bar)
        {
            Ctx c = new Ctx { Bar = bar };
            Lua();
            bool compact = Compact;
            CsButton(c, "", delegate { Compact = !Compact; BtcGhRightBar.RebuildEditor(); },
                delegate { return Compact ? BtcReplayUI.T("Compact panel: on", "Компактная панель: вкл") : BtcReplayUI.T("Compact panel: off", "Компактная панель: выкл"); });
            Divider(c, "Spawn characters");
            List<string> fac = new List<string>();
            try
            {
                Script s = Lua();
                DynValue all = s != null ? s.Globals.Get("__btcFactions") : DynValue.Nil;
                if (all.Type == DataType.Table)
                    foreach (TablePair kv in all.Table.Pairs)
                    {
                        string x = kv.Key.ToPrintString();
                        DynValue f = kv.Value;
                        string kind = s.DoString("local f = __btcFactions[" + x + "] if f:HasOwner() then return 'Bricktrons ' .. " + x + " elseif f.isAI then return 'Corruptrons' end return 'Neutral ' .. " + x).ToPrintString();
                        fac.Add(kind);
                    }
            }
            catch (Exception e) { BtcLog.Warn("DevPanel", "factions: " + e.Message); }
            if (fac.Count > 0)
                Dropdown(c, "Select Faction", fac.ToArray(), delegate(int v) { Run("__btcFaction = __btcFactions[" + (v + 1) + "]"); });
            Icon(c, "_Bricktron", "worker", "__btcSpawn(fy_Bricktron)");
            Icon(c, "_Halberdier", "halberdier", "__btcSpawn(fy_Bricktron, {fy_HalberdierWeapon, fy_HalberdierHelmet})");
            Icon(c, "_Knight", "knight", "__btcSpawn(fy_Bricktron, {fy_SwordProp, fy_ShieldProp, fy_KnightHelmetProp})");
            Icon(c, "_Archer", "archer", "__btcSpawn(fy_Bricktron, {fy_BowProp, fy_ArcherCapProp, fy_QuiverProp})");
            Icon(c, "_Arbalist", "arbalist", "__btcSpawn(fy_Bricktron, {fy_ArbalistWeapon, fy_ArbalistHat, fy_ArbalistPouch})");
            Icon(c, "_Alchemist", "alchemist", "__btcSpawn(fy_Bricktron, {fy_AlchemistHat, fy_AlchemistBackpack})");
            Icon(c, "_Artificer", "artificer", "__btcSpawn(fy_Bricktron, {fy_ArtificerWeapon, fy_ArtificerHat, fy_ArtificerPouch})");
            Icon(c, "_Minitron", "minitron", "__btcSpawn(fy_Minitron)");
            Icon(c, "_Corruptron", "corruptron", "__btcSpawn(fy_Corruptron)");
            Icon(c, "_Biftron", "biftron", "__btcSpawn(fy_Biftron)");
            Icon(c, "_Warlock", "magitron", "__btcSpawn(fy_Magitron)");
            if (!compact) Icon(c, "_Corruptrons_Group", "small raid", "__btcRaid({fy_Minitron, fy_Minitron, fy_Corruptron})");
            if (!compact) Icon(c, "_Corruptrons_Group", "medium raid", "__btcRaid({fy_Minitron, fy_Minitron, fy_Minitron, fy_Corruptron, fy_Corruptron})");
            if (!compact) Icon(c, "_Corruptrons_Group", "big raid", "__btcRaid({fy_Minitron, fy_Minitron, fy_Minitron, fy_Minitron, fy_Corruptron, fy_Corruptron, fy_Corruptron, fy_Biftron, fy_Biftron})");
            if (!compact) Icon(c, "_Corruptrons_Group", "new york city boys", "__btcRaid({fy_Minitron, fy_Minitron, fy_Corruptron, fy_Corruptron, fy_Biftron}, 'New York City Boys')");

            Divider(c, "Selected Objects");
            if (!compact) Dropdown(c, "Bricktron Face", Faces, delegate(int v) { Run("if selectedObject then selectedObject:ChangeBricktronFace('" + Faces[v] + "') end"); });
            if (!compact) Icon(c, "UI_Reset", "Reset A.I", "__btcResetAI()");
            Icon(c, "UI_Trash", "Delete", "if selectedObject then selectedObject:Destroy() end");
            Icon(c, "UI_Trash", "Kill", "if selectedObject and selectedObject.killable ~= nil then selectedObject:Kill() end");
            if (!compact) Icon(c, "MultiItems", "Scarf", "if selectedObject then selectedObject:EquipScarf() end");
            if (!compact) Icon(c, "MultiItems", "Orange", "if selectedObject then selectedObject:SetScarfColor('Orange') end");
            if (!compact) Icon(c, "MultiItems", "Red", "if selectedObject then selectedObject:SetScarfColor('Red') end");
            if (!compact) Icon(c, "MultiItems", "Blue", "if selectedObject then selectedObject:SetScarfColor('Blue') end");
            if (!compact) Icon(c, "MultiItems", "Green", "if selectedObject then selectedObject:SetScarfColor('Green') end");
            if (!compact) Icon(c, "MultiItems", "Ablaze", "if selectedObject then selectedObject:SetScarfColor('Sun') end");

            Divider(c, "Pickers");
            Icon(c, "_Explosive_Barrel", "explosion", "SetExplosionPicker()");
            Icon(c, "UI_Up", "regular", "Picking.Clear()");
            Icon(c, "UI_Up", "arrow", "SetArrowPicker()");

            Divider(c, "Fill selected stockpiles with");
            string[,] fill = {
                { "_Stockpiled_Brick", "StoneBlock" }, { "_Stockpiled_Brick", "Stones" }, { "_Stockpiled_Log", "WoodBlock" }, { "_Stockpiled_Plank", "PlankBlock" },
                { "_Stockpiled_Raw_Iron", "RawIron" }, { "_Stockpiled_Raw_Orange_Crystal", "OrangeCrystal" }, { "_Stockpiled_Raw_Blue_Crystal", "BlueCrystal" }, { "_Plant", "Plant" },
                { "_Rope", "Rope" }, { "_Fabric", "Fabric" }, { "_Stockpiled_Glass", "Glass" }, { "_Stockpiled_Cog", "Cog" }, { "_Bomb", "Bomb" }, { "_Stockpiled_Iron", "Iron" } };
            for (int i = 0; i < fill.GetLength(0); i++)
                Icon(c, fill[i, 0], fill[i, 1], "if selectedObject then selectedObject:FillWithRessource('" + fill[i, 1] + "') end");
            Icon(c, "StockpileEmpty", "Empty", "if selectedObject then selectedObject:WipeAll() end");

            Divider(c, "Global settings");
            Icon(c, "HammerGroup", "instant build", "ToggleEditorMode()");
            Icon(c, "Blueprints_Split", "disable mergeback", "Mergeback.allowMergeback = not Mergeback.allowMergeback");
            Icon(c, "BroomGroup", "ramasse cette merde", "TabulaRasa()");
            return c;
        }

        // ------------------------------------------------------------------ movie (TestMovieSidebar.lua)
        internal static Ctx BuildMovie(BtcGhSidebar bar)
        {
            Ctx c = new Ctx { Bar = bar };
            Lua();
            Divider(c, "Video flow");
            Icon(c, "UI_Pause", "pause", "TogglePause()");
            Icon(c, "UI_Right", "step 1s", "StepOneSecond()");
            Icon(c, "UI_Right", "step 0.1s", "StepOneTenth()");
            Icon(c, "UI_Play", "slow 0.5", "ToggleHalfSpeed()");
            Icon(c, "UI_Play", "slow 0.1", "ToggleTenthSpeed()");
            Icon(c, "UI_Play", "fast 1.5", "ToggleFastSpeed()");
            Icon(c, "UI_Play", "fast 5", "ToggleVeryFastSpeed()");

            Divider(c, "UI toggles");
            Icon(c, "UI_Chat", "hide Bubbles", "fy_OverheadDisplay.Cinematic = not fy_OverheadDisplay.Cinematic");
            Icon(c, "SelectTarget", "hide pivot", "MainCamera:ToggleHideGizmos()");
            Icon(c, "UI_Flag", "hide gizmos", "__btcHideBalise()");
            Icon(c, "BricktronFace", "hide health", "__btcLife()");

            Divider(c, "Camera speed");
            Inputs(c, "Travel Speed",
                new[] { "GetRounded((function() local a, b, c = MainCamera:GetTravelSpeed() return a end)(), 3)", "GetRounded((function() local a, b, c = MainCamera:GetTravelSpeed() return b end)(), 3)", "GetRounded((function() local a, b, c = MainCamera:GetTravelSpeed() return c end)(), 3)" },
                new[] { "MainCamera:SetTravelSpeed({0}, MainCamera.translationSpeedMod.y, MainCamera.translationSpeedMod.z)",
                        "MainCamera:SetTravelSpeed(MainCamera.translationSpeedMod.x, {0}, MainCamera.translationSpeedMod.z)",
                        "MainCamera:SetTravelSpeed(MainCamera.translationSpeedMod.x, MainCamera.translationSpeedMod.y, {0})" });
            Inputs(c, "Orbit Speed", new[] { "GetRounded(MainCamera:GetOrbitSpeed(), 3)" }, new[] { "MainCamera:SetOrbitSpeed({0})" });
            Inputs(c, "Zoom Speed", new[] { "GetRounded(MainCamera:GetZoomSpeed(), 3)" }, new[] { "MainCamera:SetZoomSpeed({0})" });
            TextButton(c, "Editor Camera", "ToggleDebugCamera()", null);
            TextButton(c, "Possess Bricktron", "__btcFPS()", null);

            Divider(c, "Daytime");
            TextButton(c, "Stop the Sun", "Skybox.dayPause = not Skybox.dayPause", null);
            TextButton(c, "Toggle Fog of War", "Rules.ToggleFog(false)", null);
            Inputs(c, "Ambient Fog Distance", new[] { "GetFogStartDistance()", "GetFogEndDistance()" }, new[] { "SetFogStartDistance({0})", "SetFogEndDistance({0})" });
            Inputs(c, "Ambient Fog Settings", new[] { "__btcFog(1)", "__btcFog(2)", "__btcFog(3)" },
                new[] { "SetFogColor({0}, __btcFog(2), __btcFog(3))", "SetFogColor(__btcFog(1), {0}, __btcFog(3))", "SetFogColor(__btcFog(1), __btcFog(2), {0})" });
            Slider(c, "Time of Day", 0f, 24f, "GetRounded(Skybox.dayTime, 1)", "Skybox.dayTime = GetRounded({0}, 1)");
            Slider(c, "Sun Speed", 0f, 20f, "GetRounded(Skybox.sunSpeed, 1)", "Skybox.sunSpeed = GetRounded({0}, 1)");
            Slider(c, "Orbit Distance", 1f, 20f, "GetRounded(Skybox.Sun.OrbitDistance, 2)", "Skybox.Sun.OrbitDistance = GetRounded({0}, 2)");
            Slider(c, "Cardinal Angle", 0f, 1f, "GetRounded(Skybox.Sun.CardinalAngle, 2)", "Skybox.Sun.CardinalAngle = GetRounded({0}, 2)");
            TextButton(c, "LOD Bias", "ToggleMaxLodBias()", null);

            Divider(c, "Timelapse");
            TextButton(c, "Activate Timelapse", "MainCamera:ToggleTimelapse()", null);
            Inputs(c, "Translation",
                new[] { "MainCamera.timelapseTranslationVector.x", "MainCamera.timelapseTranslationVector.y", "MainCamera.timelapseTranslationVector.z" },
                new[] { "MainCamera:SetTimelapseTranslation({0}, MainCamera.timelapseTranslationVector.y, MainCamera.timelapseTranslationVector.z)",
                        "MainCamera:SetTimelapseTranslation(MainCamera.timelapseTranslationVector.x, {0}, MainCamera.timelapseTranslationVector.z)",
                        "MainCamera:SetTimelapseTranslation(MainCamera.timelapseTranslationVector.x, MainCamera.timelapseTranslationVector.y, {0})" });
            Inputs(c, "Rotation", new[] { "MainCamera.timelapseLatitudinalRotation", "MainCamera.timelapseLongitudinalRotation" },
                new[] { "MainCamera:SetTimelapseRotation({0}, MainCamera.timelapseLongitudinalRotation)", "MainCamera:SetTimelapseRotation(MainCamera.timelapseLatitudinalRotation, {0})" });
            Inputs(c, "Zoom", new[] { "MainCamera.timelapseZoom" }, new[] { "MainCamera:SetTimelapseZoom({0})" });

            Divider(c, "Initial position");
            Icon(c, "UI_Save", "record position", "MainCamera:RegisterPositionAndRotation()");
            Icon(c, "UI_Reset", "reset position", "MainCamera:RepositionCamera()");
            return c;
        }

        // ------------------------------------------------------------------ right bar glue
        private static readonly Dictionary<BtcGhSidebar, Ctx> Built = new Dictionary<BtcGhSidebar, Ctx>();

        internal static void Track(BtcGhSidebar bar, Ctx c) { if (bar != null && c != null) Built[bar] = c; }

        internal static void RefreshValues(BtcGhSidebar bar)
        {
            Ctx c;
            if (bar == null || !Built.TryGetValue(bar, out c)) return;
            for (int i = 0; i < c.Refresh.Count; i++) { try { c.Refresh[i](); } catch (Exception e) { BtcLog.Warn("DevPanel", "refresh: " + e.Message); } }
        }

        internal static void Close() { Built.Clear(); Kill(); }

        internal static void Forget(BtcGhSidebar bar) { if (bar != null) Built.Remove(bar); }
    }

    // the right bar shows the dev buttons from the moment debug menus are allowed (host rule gen.editor, settings toggle)
    [HarmonyPatch(typeof(Architecte), "allowDebugMenu", MethodType.Setter)]
    internal static class GhDevAllowPatch
    {
        private static void Postfix() { try { BtcGhRightBar.CheckDev(); BtcGameHud.RefreshAll(); } catch { } }
    }
}
