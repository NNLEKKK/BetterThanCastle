using System;
using System.Collections.Generic;
using Brix.Assets;
using Brix.Engine;
using Brix.External.Factories;
using Brix.External.Signals;
using Brix.Game.Compounders;
using Brix.Input;
using Brix.UI.Builder.Widget;
using UnityEngine;
using UnityEngine.UI;

namespace BetterThanCastle
{
    /// Blueprints sidebar (GameMenu.lua 2020-2194): "save current selection" name field + save button, library header (n/16) with
    /// load / publish / delete, then up to 16 saved compounds (thumbnail 60x40, name, highlight All when selected).
    /// Perf: thumbnails are loaded only when the sidebar opens (Lua loads all 16 at menu load); rows are pooled.
    internal static class BtcGhBlueprints
    {
        private const int MaxCompound = 16;
        private static readonly List<string> Names = new List<string>();
        private static readonly List<SignalConnection> Conns = new List<SignalConnection>();
        private static readonly List<Row> Rows = new List<Row>();
        private static BtcGhSidebar _bar;
        private static Text _libHeader;
        private static BtcGhButton _save, _load, _publish, _delete;
        private static RectTransform _list;
        private static InputField _field;
        private static Image _fieldImg;
        private static RectTransform _fieldBtn;

        private sealed class Row
        {
            internal RectTransform P;
            internal Image Thumb;
            internal Text Name;
            internal BtcGhButton Btn;
            internal string Key;
        }

        private static readonly Color DeleteRed = new Color(1f, 0.1f, 0.1f, 1f);

        // ------------------------------------------------------------------ BlueprintSelection.* (BlueprintCompounder statics)
        private static bool HasSelectedAny { get { return BlueprintCompounder.currentSelected != string.Empty; } }
        private static bool FromWorkshop
        {
            get
            {
                Asset_Blueprints a;
                return HasSelectedAny && BlueprintCompounder.blueprintsDictionary.TryGetValue(BlueprintCompounder.currentSelected, out a) && a.authorType == AssetAuthorType.Workshop;
            }
        }
        private static bool HasSelection { get { return BlueprintCompounder.lua_HasBlueprintSelection(default(MoonSharp.Interpreter.ScriptExecutionContext), default(MoonSharp.Interpreter.CallbackArguments)).Boolean; } }
        private static bool CanSave { get { return Names.Count < MaxCompound && HasSelection && BlueprintCompounder.currentName != ""; } }

        private static void UpdateCompoundList()
        {
            Names.Clear();
            foreach (KeyValuePair<string, Asset_Blueprints> kv in BlueprintCompounder.blueprintsDictionary) Names.Add(kv.Key);
        }

        private static Sprite Thumb(string key)
        {
            Asset_Blueprints a;
            if (!BlueprintCompounder.blueprintsDictionary.TryGetValue(key, out a) || a == null) return null;
            Sprite s;
            if (BlueprintCompounder.thumbnailDictionary.TryGetValue(key, out s) && s != null && s.texture != null) return s;
            Texture2D t = a.Thumbnail;
            if (t == null) return null;
            s = Sprite.Create(t, new Rect(0f, 0f, t.width, t.height), new Vector2(0.5f, 0.5f));
            BlueprintCompounder.thumbnailDictionary[key] = s;
            return s;
        }


        // ------------------------------------------------------------------ library load (BlueprintSelection.InitializeCompounds)
        /// Same as BlueprintCompounder.lua_InitializeCompounds (InitializeCompoundDictionary + LeaveGame cleanup), but one saved
        /// compound per frame: each reads its thumbnail PNG and builds its graph (~400 ms for 6 blueprints when done at once).
        internal static void InitLibrary()
        {
            GameSignals.Connect(GameSignals.LeaveGame, new Action(BlueprintCompounder.ClearStaticLists), null);
            if (BtcGameHudLife.Inst != null) BtcGameHudLife.Inst.StartCoroutine(LoadLibrary());
            else BlueprintCompounder.InitializeCompoundDictionary();
        }

        private static System.Collections.IEnumerator LoadLibrary()
        {
            yield return null; // StartCoroutine runs synchronously up to the first yield: keep the disk scan out of the menu build
            List<Asset_Blueprints> all = null;
            try { all = BlueprintCompounder.GetAllSavedCompounds(); } catch (Exception e) { BtcLog.Error("GameHud", "bp library: " + e.Message); }
            if (all == null) yield break;
            for (int i = 0; i < all.Count; i++)
            {
                yield return null;
                try
                {
                    Asset_Blueprints a = all[i];
                    if (a == null || BlueprintCompounder.blueprintsDictionary.ContainsKey(a.customName) || !a.Disk_Exists()) continue;
                    if (a.thumbnail == null) a.Disk_ReadThumbnail();
                    if (a.thumbnail == null) continue;
                    BlueprintCompounder.blueprintsDictionary.Add(a.customName, a);
                    a.InitGraph();
                }
                catch (Exception e) { BtcLog.Warn("GameHud", "bp library item: " + e.Message); }
            }
            if (_bar != null) { UpdateCompoundList(); RefreshList(); }
        }
        // ------------------------------------------------------------------ build
        internal static void Fill(BtcGhSidebar bar)
        {
            _bar = bar;
            UpdateCompoundList();
            bar.Section(BtcGameHud.Loc("##gamemenu_taskbar_blueprintactions"));
            NameField(bar.Body);
            RectTransform grid = bar.Section("");
            _libHeader = grid.parent.GetChild(grid.GetSiblingIndex() - 1).GetComponentInChildren<Text>();
            _load = Action(bar, grid, "LoadBlueprints", "##gamemenu_taskbar_loadblueprint", OnLoad, delegate { return HasSelectedAny ? BtcGhKit.Red : BtcGhKit.Gray; });
            _publish = Action(bar, grid, "_UI_Steam", "##gamemenu_taskbar_publishblueprint", OnPublish, delegate { return CanPublish() ? BtcGhKit.Red : BtcGhKit.Gray; });
            _delete = Action(bar, grid, "UI_Trash", "##gamemenu_taskbar_blueprintdelete", OnDelete, delegate { return HasSelectedAny && !FromWorkshop ? DeleteRed : BtcGhKit.Gray; });
            _list = BtcGhKit.Grid(bar.Body, 256f, 40f);
            RefreshList();

            Conns.Add(GameSignals.Connect(PickingObserver.lua_BlueprintSelectionActionTaken, new Action(OnBpSelectionChanged), null));
            Conns.Add(BlueprintCompounder.lua_CompoundSelectionChanged._signal.Connect(new Action(OnCompoundSelected)));
            Conns.Add(BlueprintCompounder.lua_OnCompoundDeleted._signal.Connect(new Action(OnCompoundDeleted)));
            Conns.Add(BlueprintCompounder.lua_OnDoneInstantiating._signal.Connect(new Action(OnDoneInstantiating)));
            if (UIGameObserver.lua_OnBlueprintScreenshotTaken != null)
                Conns.Add(UIGameObserver.lua_OnBlueprintScreenshotTaken._signal.Connect(new Action(OnScreenshotTaken)));
        }

        internal static void Close()
        {
            for (int i = 0; i < Conns.Count; i++) { try { Conns[i].Disconnect(); } catch { } }
            Conns.Clear();
            Rows.Clear();
            Pending.Clear(); _loading = false;
            _bar = null; _list = null; _field = null; _libHeader = null;
        }

        private static bool CanPublish()
        {
            return HasSelectedAny && UIGameObserver.lua_IsSteamConnected(default(MoonSharp.Interpreter.ScriptExecutionContext), default(MoonSharp.Interpreter.CallbackArguments)).Boolean && !FromWorkshop;
        }

        private static BtcGhButton Action(BtcGhSidebar bar, RectTransform grid, string icon, string loc, System.Action act, Func<Color> color)
        {
            BtcGhButton b = BtcGhKit.GridButton(grid, BtcGhKit.Icon(icon), BtcGhKit.Gray, 32f, BtcGameHud.Loc(loc), true, null, false);
            b.OnAction = act;
            b.IconColor = color;
            return bar.Add(b);
        }

        /// ImagedButtonInputFieldPanel2 (SidebarFloatingMenu AddMenuHandleInputField): grey 1 px frame, black field, overlay button
        /// starts editing (InputModeController.ChangeInputToText), end edit sets BlueprintSelection name; save button 48x48 on the right.
        private static void NameField(RectTransform body)
        {
            RectTransform row = BtcGhKit.Box(Layouts.Horizontal, body, "", 0, 0, 0, 0, 2f, TextAnchor.UpperLeft);
            BtcGhKit.Le(row, -1f, 48f, -1f, 48f, 1f, -1f, false);
            RectTransform frame = BtcGhKit.Make(Layouts.Horizontal, row, "");
            BtcGhKit.Stretch(frame);
            BtcGhKit.Img(frame, null, BtcGhKit.Gray, true);
            BtcGhKit.Le(frame, -1f, 48f, -1f, 48f, 1f, -1f, false);
            BtcGhKit.Group(frame, 1, 1, 1, 1, 2f, TextAnchor.MiddleCenter);
            RectTransform black = BtcGhKit.Make(Layouts.Horizontal, frame, "");
            BtcGhKit.Stretch(black);
            BtcGhKit.Img(black, null, Color.black, true);
            BtcGhKit.Le(black, -1f, -1f, -1f, -1f, -1f, -1f, true);
            BtcGhKit.Group(black, 16, 0, 0, 0, 2f, TextAnchor.MiddleCenter);
            _fieldBtn = BtcGhKit.Make(Brix.External.Factories.Buttons.Standard, black, "");
            BtcGhKit.Stretch(_fieldBtn);
            BtcGhKit.Le(_fieldBtn, 64f, 64f, 64f, 64f, -1f, -1f, true);
            BtcGhButton edit = _fieldBtn.gameObject.AddComponent<BtcGhButton>();
            edit.Silent = true;
            edit.OnAction = StartEdit;

            RectTransform fld = BtcGhKit.Make(InputFields.LuaShort, frame, "");
            BtcGhKit.Rect(fld, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), BtcGhKit.Mid, Vector2.zero, new Vector2(240f, 48f));
            BtcGhKit.Le(fld, -1f, 48f, -1f, 48f, 1f, -1f, false);
            _fieldImg = fld.GetComponent<Image>();
            if (_fieldImg != null) _fieldImg.enabled = false;
            // ImprovedInputFieldPanel labels: placeholder "None" (white 0.75), value white, both size 12 MiddleLeft
            foreach (Text lt in fld.GetComponentsInChildren<Text>(true))
            {
                if (lt.name == "ValueLabelPlaceholder") { lt.text = "None"; lt.color = new Color(1f, 1f, 1f, 0.75f); lt.alignment = TextAnchor.MiddleLeft; lt.fontSize = 12; }
                else if (lt.name == "ValueLabel") { lt.color = Color.white; lt.alignment = TextAnchor.MiddleLeft; lt.fontSize = 12; }
            }
            InputFieldWidgetComponent w = fld.GetComponentInChildren<InputFieldWidgetComponent>(true);
            _field = w != null ? w._inputField : fld.GetComponentInChildren<InputField>(true);
            if (_field != null)
            {
                _field.text = BlueprintCompounder.currentName;
                _field.interactable = false;
                _field.onEndEdit.AddListener(EndEdit);
            }

            _save = BtcGhKit.GridButton(row, BtcGhKit.Icon("SaveBlueprints"), BtcGhKit.Gray, 32f, "", true, null, false);
            RectTransform sp = (RectTransform)_save.transform.parent;
            BtcGhKit.Le(sp, 48f, 48f, 48f, 48f, -1f, -1f, false);
            sp.GetComponent<Image>().color = new Color32(0, 0, 0, 127);
            _save.HoverColor = BtcGhKit.RailBg;
            _save.OnAction = OnSave;
            _save.IconColor = delegate { return CanSave ? BtcGhKit.Red : BtcGhKit.Gray; };
            _bar.Add(_save);
        }

        private static void StartEdit()
        {
            if (_field == null) return;
            BtcGhButton.Play("WorldEditor_UI_Item_Select");
            _field.interactable = true;
            if (_fieldImg != null) _fieldImg.enabled = true;
            _fieldBtn.gameObject.SetActive(false);
            Brix.UI.Builder.Menu.LuaMenuUtility.SetAllKeyBindingsTo(false); // LUI2.SetKeyBindings(false): typing must not fire hotkeys
            InputModeController.Instance.ChangeInputToMenu();
            _field.ActivateInputField();
            _field.Select();
        }

        private static void EndEdit(string v)
        {
            try
            {
                if (v != null) BlueprintCompounder.currentName = v;
                _field.interactable = false;
                if (_fieldImg != null) _fieldImg.enabled = false;
                _fieldBtn.gameObject.SetActive(true);
                Brix.UI.Builder.Menu.LuaMenuUtility.ResetAllKeyBindings();
                InputModeController.Instance.ChangeInputToGame();
                if (_save != null) _save.Refresh();
            }
            catch (Exception e) { BtcLog.Error("GameHud", "bp name: " + e); }
        }

        // ------------------------------------------------------------------ actions
        private static void OnSave()
        {
            if (CanSave) UIGameObserver.lua_UpdateBlueprintThumbnail(default(MoonSharp.Interpreter.ScriptExecutionContext), default(MoonSharp.Interpreter.CallbackArguments));
        }

        /// _m.OnBlueprintScreenshotTaken listener: DuplicateAndSave, UpdateCompoundList, onSaveCompoundBlueprint.
        private static void OnScreenshotTaken()
        {
            try
            {
                BlueprintCompounder.Duplicate();
                BlueprintCompounder.SaveSelected();
                UpdateCompoundList();
                RefreshList();
            }
            catch (Exception e) { BtcLog.Error("GameHud", "bp save: " + e); }
        }

        private static void OnLoad() { if (HasSelectedAny) BlueprintCompounder.lua_LoadSelected(default(MoonSharp.Interpreter.ScriptExecutionContext), default(MoonSharp.Interpreter.CallbackArguments)); }

        private static void OnPublish()
        {
            if (!CanPublish()) return;
            BlueprintCompounder.SendToWorkshop();
            UpdateCompoundList();
            RefreshList();
            GameSignals.Invoke<string, float>(GameSignals.OnAutoFadeDialog, BtcGameHud.Loc("##gamemenu_taskbar_blueprintpublished"), 3f);
        }

        private static void OnDelete()
        {
            if (!HasSelectedAny || FromWorkshop) return;
            BlueprintCompounder.lua_DeleteSelected(default(MoonSharp.Interpreter.ScriptExecutionContext), default(MoonSharp.Interpreter.CallbackArguments));
            UpdateCompoundList();
            RefreshList();
        }

        // ------------------------------------------------------------------ events
        private static void OnBpSelectionChanged() { if (_save != null) _save.Refresh(); }
        private static void OnCompoundSelected() { RefreshActions(); RefreshList(); }
        private static void OnCompoundDeleted() { if (_save != null) _save.Refresh(); RefreshActions(); }
        private static void OnDoneInstantiating() { RefreshList(); }

        private static void RefreshActions()
        {
            if (_load == null) return;
            _load.Refresh(); _publish.Refresh(); _delete.Refresh();
        }

        internal static void OnOpen()
        {
            UpdateCompoundList();
            if (_field != null && !_field.isFocused) _field.text = BlueprintCompounder.currentName;
            RefreshList();
        }

        private static void RefreshList()
        {
            if (_list == null) return;
            if (_libHeader != null) _libHeader.text = BtcGameHud.Loc("##gamemenu_taskbar_blueprintssaved") + " (" + Names.Count + "/" + MaxCompound + ")";
            int n = Math.Min(Names.Count, MaxCompound);
            while (Rows.Count < n) Rows.Add(NewRow(Rows.Count));
            bool busy = BlueprintCompounder.instantiatingCompound;
            for (int i = 0; i < Rows.Count; i++)
            {
                Row r = Rows[i];
                bool on = i < n;
                if (r.P.gameObject.activeSelf != on) r.P.gameObject.SetActive(on);
                if (!on) continue;
                if (r.Key != Names[i]) { r.Key = Names[i]; r.Thumb.sprite = null; r.Thumb.enabled = false; r.Name.text = r.Key; QueueThumb(r); }
                r.Thumb.color = busy ? BtcGhKit.Gray : Color.white;
                r.Btn.RefreshHighlight();
            }
        }


        // thumbnails: Asset_Blueprints.Thumbnail reads + decodes a PNG (~60 ms each): one per frame, so opening never hitches
        private static readonly Queue<Row> Pending = new Queue<Row>();
        private static bool _loading;

        private static void QueueThumb(Row r)
        {
            Pending.Enqueue(r);
            if (!_loading && BtcGameHudLife.Inst != null) { _loading = true; BtcGameHudLife.Inst.StartCoroutine(LoadThumbs()); }
        }

        private static System.Collections.IEnumerator LoadThumbs()
        {
            while (Pending.Count > 0)
            {
                yield return null;
                Row r = Pending.Dequeue();
                try { if (r.Thumb != null && r.Key != null) { r.Thumb.sprite = Thumb(r.Key); r.Thumb.enabled = r.Thumb.sprite != null; } }
                catch (Exception e) { BtcLog.Warn("GameHud", "bp thumb " + r.Key + ": " + e.Message); }
            }
            _loading = false;
        }
        /// AddCompoundHandleToggle -> LargeButtonPanel: 240x40, thumbnail at x -90 (60x40), name at x +70 MiddleLeft size 11 yellow.
        private static Row NewRow(int i)
        {
            Row r = new Row();
            RectTransform p = BtcGhKit.Make(Layouts.Horizontal, _list, "ButtonPanel @ _p");
            BtcGhKit.Img(p, null, BtcGhKit.Clear, true);
            BtcGhKit.Le(p, 240f, 40f, 240f, 40f, -1f, -1f, false);
            BtcGhKit.Group(p, 0, 0, 0, 0, 2f, TextAnchor.MiddleLeft);
            RectTransform bg = BtcGhKit.Make(Images.Default, p, "ButtonPanel @ _p.bgImg");
            BtcGhKit.Rect(bg, BtcGhKit.Mid, BtcGhKit.Mid, BtcGhKit.Mid, new Vector2(-90f, 0f), new Vector2(60f, 40f));
            BtcGhKit.Le(bg, 64f, 64f, -1f, -1f, -1f, -1f, true);
            r.Thumb = BtcGhKit.Img(bg, null, Color.white, true);
            RectTransform hov = BtcGhKit.Box(Layouts.Horizontal, p, "ButtonPanel @ _p.lyt_hover", 0, 4, 4, 0, 2f, TextAnchor.UpperLeft);
            BtcGhKit.Le(hov, -1f, -1f, -1f, -1f, -1f, -1f, true);
            RectTransform hbg = BtcGhKit.Make(Images.Default, hov, "ButtonPanel @ _p.lyt_hover.img_hoverBg");
            BtcGhKit.Stretch(hbg);
            Image hb = BtcGhKit.Img(hbg, null, BtcGhKit.Clear, true);
            BtcGhKit.Le(hbg, 64f, 64f, -1f, -1f, -1f, -1f, true);
            RectTransform lbl = BtcGhKit.Make(Texts.Default, p, "ButtonPanel @ _p.lbl_fg");
            BtcGhKit.Rect(lbl, BtcGhKit.Mid, BtcGhKit.Mid, BtcGhKit.Mid, new Vector2(70f, 0f), new Vector2(240f, 40f));
            r.Name = BtcGhKit.Txt(lbl, "", 11, BtcGhKit.Yellow, TextAnchor.MiddleLeft);
            BtcGhKit.Le(lbl, 64f, 64f, -1f, -1f, -1f, -1f, true);
            RectTransform btn = BtcGhKit.Make(Brix.External.Factories.Buttons.Standard, p, "ButtonPanel @ _p.btn");
            BtcGhKit.Stretch(btn);
            BtcGhKit.Le(btn, 64f, 64f, 64f, 64f, -1f, -1f, true);
            BtcGhButton b = btn.gameObject.AddComponent<BtcGhButton>();
            b.Frame = btn.GetComponent<FrameAnimatorWidgetComponent>() ?? btn.GetComponentInChildren<FrameAnimatorWidgetComponent>(true);
            b.HoverBg = hb; b.HoverColor = BtcGhKit.RailBg;
            b.OnAction = delegate
            {
                if (BlueprintCompounder.instantiatingCompound || r.Key == null) return;
                BlueprintCompounder.currentSelected = r.Key; // BlueprintSelection.SelectCompound
                BlueprintCompounder.lua_CompoundSelectionChanged._signal.Invoke();
            };
            b.Highlight = delegate { return r.Key != null && BlueprintCompounder.currentSelected == r.Key ? 15 : 0; };
            r.P = p; r.Btn = b;
            return r;
        }
    }
}
