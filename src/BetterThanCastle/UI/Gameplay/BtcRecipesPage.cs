using System;
using System.Collections.Generic;
using Brix.UI.Icons;
using UnityEngine;

namespace BetterThanCastle
{
    /// <summary>
    /// Recipes page of the Gameplay tab: group cards, recipe cards (game icons, aspect-correct), an editor with only the fields that apply to the
    /// recipe (work only for bench crafts, heat only for furnace recipes, judged from the vanilla values), ingredient tiles with - / + and remove,
    /// a changed marker per recipe (orange = output or ingredients, yellow = only work/heat), search, only-changed, undo, requests.
    /// Data, wire (msgs 84/85) and saving stay in BtcRecipes; Firefly is hidden (not a real craftable).
    /// </summary>
    internal sealed partial class BtcGameplayTab
    {
        private int RecGroup { get { return BtcMenuState.Int("gameplay:rec:g", -1); } set { BtcMenuState.SetInt("gameplay:rec:g", value); } }
        private int RecSel { get { return BtcMenuState.Int("gameplay:rec:k", -1); } set { BtcMenuState.SetInt("gameplay:rec:k", value); } }
        private bool _recAdding;
        private bool _recResetAll;
        private float _recResetUntil;
        private GUIStyle _cap;

        private static Color LevelColor(int lv) { return lv == 2 ? Critical : (lv == 1 ? Minor : new Color(1f, 1f, 1f, 0f)); }

        private void RecStyles(int fs)
        {
            if (_cap != null && _cap.fontSize == Mathf.RoundToInt(fs * 0.72f)) return;
            _cap = new GUIStyle(BtcVisualRows.Dim);
            _cap.fontSize = Mathf.RoundToInt(fs * 0.72f);
            _cap.alignment = TextAnchor.UpperCenter;
            _cap.wordWrap = true;
            _cap.clipping = TextClipping.Clip;
            _cap.normal.textColor = new Color(1f, 1f, 1f, 0.8f);
        }

        /// <summary>Square card: game icon, caption, a colour dot when changed and an accent frame when selected. Returns true when clicked.</summary>
        private bool RecCard(Rect r, int fs, IconKey icon, string caption, bool sel, int level, string tip)
        {
            Event e = Event.current;
            bool hv = r.Contains(e.mousePosition);
            BtcReplayUI.Fill(r, new Color(1f, 1f, 1f, hv ? 0.12f : 0.05f));
            if (sel)
            {
                Color a = BtcMenu.Accent;
                BtcReplayUI.Fill(new Rect(r.x, r.y, r.width, 2f), a);
                BtcReplayUI.Fill(new Rect(r.x, r.yMax - 2f, r.width, 2f), a);
                BtcReplayUI.Fill(new Rect(r.x, r.y, 2f, r.height), a);
                BtcReplayUI.Fill(new Rect(r.xMax - 2f, r.y, 2f, r.height), a);
            }
            float capH = fs * 2.1f, pad = fs * 0.4f;
            BtcRecipes.DrawIconFit(new Rect(r.x + pad, r.y + pad, r.width - pad * 2f, r.height - capH - pad), icon);
            GUI.Label(new Rect(r.x + 2f, r.yMax - capH, r.width - 4f, capH), caption, _cap);
            if (level > 0) BtcReplayUI.Fill(new Rect(r.xMax - fs * 0.9f, r.y + fs * 0.3f, fs * 0.55f, fs * 0.55f), LevelColor(level));
            if (!string.IsNullOrEmpty(tip)) BtcReplayIcons.Hint(r, new Rect(_off.x + r.x, _off.y + r.y, r.width, r.height), tip);
            return BtcSfx.Btn(r, "", BtcReplayUI.sBtn);
        }

        private static IconKey GroupIcon(int g)
        {
            int[] it = BtcRecipes.GroupItems(g);
            // the Alchemist kit group used a broken constant icon: take the icon of a real recipe (the hat) instead
            int k = (it.Length == 2 && it[1] == 10) ? it[1] : it[0];
            return BtcRecipes.RecipeIcon(k);
        }

        private void RecEdit(int[] arr, int k, bool host, string key, Action mutate)
        {
            int n = k * BtcRecipes.IntsPer;
            int[] old = new int[BtcRecipes.IntsPer];
            Array.Copy(arr, n, old, 0, old.Length);
            mutate();
            BtcRecipes.Edited(host);
            BtcUndo.Push(Scope, BtcRecipes.Name(k), delegate { Array.Copy(old, 0, arr, n, old.Length); BtcRecipes.Edited(host); }, key);
        }

        private void PageRecipes(ref float y, float w, int fs, bool host, bool session, bool draft)
        {
            BtcRecipes.Ensure();
            RecStyles(fs);
            bool interact = (host && BtcGameplay.CanEdit) || (!host && session && draft);
            int[] arr = host ? BtcRecipes.Cur : (draft ? BtcRecipes.Prop : BtcRecipes.Cur);
            int[] van = BtcRecipes.Van;
            int cnt = BtcRecipes.Count;

            // summary
            int crit = 0, minor = 0;
            for (int k = 0; k < cnt; k++)
            {
                if (BtcRecipes.Hidden(k)) continue;
                int lv = BtcRecipes.Level(arr, van, k);
                if (lv == 2) crit++; else if (lv == 1) minor++;
            }
            float pad = fs, h = fs * 2.1f;
            Rect sr = new Rect(pad, y, w - pad * 2f, h);
            BtcReplayUI.Fill(sr, new Color(1f, 1f, 1f, 0.03f));
            BtcVisualRows.Txt(new Rect(sr.x + fs * 0.6f, sr.y, fs * 14f, h), Tt("Recipes changed from vanilla:", "Рецептов изменено от ванили:"), BtcVisualRows.Lbl, new Color(1f, 1f, 1f, 0.8f));
            float sx = sr.x + fs * 15f;
            BtcReplayUI.Fill(new Rect(sx, sr.center.y - fs * 0.35f, fs * 0.7f, fs * 0.7f), Critical);
            BtcVisualRows.Txt(new Rect(sx + fs, sr.y, fs * 9f, h), crit + " " + Tt("critical", "критично"), BtcVisualRows.Lbl, crit > 0 ? Critical : new Color(1f, 1f, 1f, 0.5f));
            sx += fs * 9.5f;
            BtcReplayUI.Fill(new Rect(sx, sr.center.y - fs * 0.35f, fs * 0.7f, fs * 0.7f), Minor);
            BtcVisualRows.Txt(new Rect(sx + fs, sr.y, fs * 11f, h), minor + " " + Tt("minor", "незначительно"), BtcVisualRows.Lbl, minor > 0 ? Minor : new Color(1f, 1f, 1f, 0.5f));
            if (interact && (crit + minor) > 0)
            {
                bool conf = _recResetAll && Time.unscaledTime < _recResetUntil;
                Rect rr = new Rect(sr.xMax - fs * 2.4f, sr.y + h * 0.1f, fs * 2.2f, h * 0.8f);
                if (IcoBtn(rr, BtcReplayIcons.Refresh, conf, conf ? Critical : BtcMenu.Accent, conf ? Tt("Click again: reset ALL recipes to vanilla", "Ещё раз: сбросить ВСЕ рецепты к ваниле") : Tt("Reset all recipes to vanilla", "Сбросить все рецепты к ваниле")))
                {
                    if (conf)
                    {
                        int[] oldAll = (int[])arr.Clone();
                        Array.Copy(van, arr, arr.Length);
                        BtcRecipes.Edited(host);
                        BtcSfx.Ok();
                        BtcUndo.Push(Scope, Tt("Reset all recipes", "Сброс всех рецептов"), delegate { Array.Copy(oldAll, arr, arr.Length); BtcRecipes.Edited(host); });
                        _recResetAll = false;
                    }
                    else { _recResetAll = true; _recResetUntil = Time.unscaledTime + 3f; }
                }
            }
            y += h + fs * 0.4f;
            if (!interact) BtcUi.Note(ref y, w, fs, host ? Tt("Recipes can be edited by the host in the lobby.", "Рецепты правит хост в лобби.") : Tt("These are the host's recipes. To propose changes, make a request on the Requests page.", "Это рецепты хоста. Чтобы предложить изменения, составьте запрос на странице Запросы."));

            string q = Query.Trim().ToLowerInvariant();
            bool onlyCh = OnlyChanged;
            float cw = fs * 5.6f, ch = fs * 5.8f, gap = 6f;
            int perRow = Mathf.Max(1, (int)((w - pad * 2f + gap) / (cw + gap)));

            if (q.Length > 0)
            {
                BtcUi.Section(ref y, w, fs, Tt("Search results", "Результаты поиска"));
                int n = 0;
                for (int k = 0; k < cnt; k++)
                {
                    if (BtcRecipes.Hidden(k) || !BtcRecipes.Name(k).ToLowerInvariant().Contains(q)) continue;
                    int lv = BtcRecipes.Level(arr, van, k);
                    if (onlyCh && lv == 0) continue;
                    Rect cr = new Rect(pad + (n % perRow) * (cw + gap), y + (n / perRow) * (ch + gap), cw, ch);
                    if (RecCard(cr, fs, BtcRecipes.RecipeIcon(k), BtcRecipes.Name(k), RecSel == k, lv, BtcRecipes.Name(k))) { RecSel = RecSel == k ? -1 : k; _recAdding = false; }
                    n++;
                }
                y += ((n + perRow - 1) / perRow) * (ch + gap) + fs * 0.3f;
                if (n == 0) BtcUi.Note(ref y, w, fs, Tt("No recipe matches.", "Ничего не найдено."));
            }
            else
            {
                int gi = 0;
                int gcount = BtcRecipes.GroupCount;
                for (int g = 0; g < gcount; g++)
                {
                    int[] items = BtcRecipes.GroupItems(g);
                    int lvG = 0, vis = 0;
                    for (int i = 0; i < items.Length; i++) { if (BtcRecipes.Hidden(items[i])) continue; vis++; lvG = Mathf.Max(lvG, BtcRecipes.Level(arr, van, items[i])); }
                    if (vis == 0 || (onlyCh && lvG == 0)) continue;
                    Rect cr = new Rect(pad + (gi % perRow) * (cw + gap), y + (gi / perRow) * (ch + gap), cw, ch);
                    if (RecCard(cr, fs, GroupIcon(g), BtcRecipes.GroupName(g), RecGroup == g, lvG, BtcRecipes.GroupName(g))) { RecGroup = RecGroup == g ? -1 : g; RecSel = -1; _recAdding = false; }
                    gi++;
                }
                y += ((gi + perRow - 1) / perRow) * (ch + gap) + fs * 0.3f;
                int sg = RecGroup;
                if (sg >= 0 && sg < gcount)
                {
                    BtcUi.Section(ref y, w, fs, BtcRecipes.GroupName(sg));
                    int[] items = BtcRecipes.GroupItems(sg);
                    int n = 0;
                    for (int i = 0; i < items.Length; i++)
                    {
                        int k = items[i];
                        if (BtcRecipes.Hidden(k)) continue;
                        int lv = BtcRecipes.Level(arr, van, k);
                        if (onlyCh && lv == 0) continue;
                        Rect cr = new Rect(pad + (n % perRow) * (cw + gap), y + (n / perRow) * (ch + gap), cw, ch);
                        if (RecCard(cr, fs, BtcRecipes.RecipeIcon(k), BtcRecipes.Name(k), RecSel == k, lv, BtcRecipes.Name(k))) { RecSel = RecSel == k ? -1 : k; _recAdding = false; }
                        n++;
                    }
                    y += ((n + perRow - 1) / perRow) * (ch + gap) + fs * 0.3f;
                }
                else BtcUi.Note(ref y, w, fs, Tt("Pick a group to see its recipes.", "Выберите группу, чтобы увидеть её рецепты."));
            }

            int sel = RecSel;
            if (sel >= 0 && sel < cnt && !BtcRecipes.Hidden(sel)) RecEditor(ref y, w, fs, arr, van, sel, host, interact);

            RecRequests(ref y, w, fs, host, session, draft, interact);
        }

        private void RecEditor(ref float y, float w, int fs, int[] arr, int[] van, int k, bool host, bool interact)
        {
            int n = k * BtcRecipes.IntsPer;
            float pad = fs;
            y += fs * 0.3f;
            // header: big icon, name, changed state, reset
            float hh = fs * 4f;
            Rect hr = new Rect(pad, y, w - pad * 2f, hh);
            int lv = BtcRecipes.Level(arr, van, k);
            BtcReplayUI.Fill(hr, new Color(1f, 1f, 1f, 0.04f));
            if (lv > 0) BtcReplayUI.Fill(new Rect(hr.x, hr.y, 3f, hh), LevelColor(lv));
            BtcRecipes.DrawIconFit(new Rect(hr.x + fs * 0.6f, hr.y + fs * 0.4f, hh - fs * 0.8f, hh - fs * 0.8f), BtcRecipes.RecipeIcon(k));
            BtcVisualRows.Txt(new Rect(hr.x + hh + fs * 0.4f, hr.y, hr.width - hh - fs * 4f, hh * 0.55f), BtcRecipes.Name(k), _ttl, Color.white);
            string st = lv == 2 ? Tt("Changed: output or ingredients", "Изменено: выход или ингредиенты") : (lv == 1 ? Tt("Changed: crafting time only", "Изменено: только время крафта") : Tt("As in vanilla", "Как в ванили"));
            BtcVisualRows.Txt(new Rect(hr.x + hh + fs * 0.4f, hr.y + hh * 0.5f, hr.width - hh - fs * 4f, hh * 0.4f), st, BtcVisualRows.Dim, lv > 0 ? LevelColor(lv) : new Color(1f, 1f, 1f, 0.5f));
            if (interact && lv > 0)
            {
                Rect rr = new Rect(hr.xMax - fs * 2.6f, hr.y + (hh - fs * 2.2f) * 0.5f, fs * 2.2f, fs * 2.2f);
                if (IcoBtn(rr, BtcReplayIcons.Undo, false, BtcMenu.Accent, Tt("Reset this recipe to vanilla", "Сбросить рецепт к ваниле")))
                {
                    RecEdit(arr, k, host, "rec:" + k + ":reset", delegate { Array.Copy(van, n, arr, n, BtcRecipes.IntsPer); });
                    BtcSfx.Ok();
                }
            }
            y += hh + fs * 0.4f;

            // numeric fields: only those that apply to this recipe
            bool hasWork = van[n] > 0 || arr[n] > 0, hasHeat = van[n + 1] > 0 || arr[n + 1] > 0;
            float w2 = w;
            if (hasWork) RecSlider(ref y, w2, fs, arr, van, k, host, interact, 0, Tt("Work: hits at the crafting bench", "Работа: удары по крафтильне"), 0, Mathf.Max(40, van[n] * 5));
            if (hasHeat) RecSlider(ref y, w2, fs, arr, van, k, host, interact, 1, Tt("Heat: smelting time in the furnace", "Жар: время плавки в печи"), 0, Mathf.Max(20, van[n + 1] * 5));
            if (!hasWork && !hasHeat) BtcUi.Note(ref y, w, fs, Tt("This recipe has no crafting time.", "У этого рецепта нет времени крафта."));
            RecSlider(ref y, w2, fs, arr, van, k, host, interact, 2, Tt("Output quantity", "Количество на выходе"), 1, Mathf.Max(10, van[n + 2] * 5));

            // ingredients
            y += fs * 0.3f;
            BtcUi.Section(ref y, w, fs, Tt("Ingredients", "Ингредиенты"));
            float tw = fs * 6.2f, th = fs * 6.6f, gap = 6f;
            int perRow = Mathf.Max(1, (int)((w - pad * 2f + gap) / (tw + gap)));
            int idx = 0;
            for (int r = 0; r < BtcRecipes.ResCount; r++)
            {
                int v = arr[n + 3 + r];
                if (v <= 0) continue;
                Rect tr = new Rect(pad + (idx % perRow) * (tw + gap), y + (idx / perRow) * (th + gap), tw, th);
                RecTile(tr, fs, arr, van, k, host, interact, r);
                idx++;
            }
            if (interact)
            {
                Rect tr = new Rect(pad + (idx % perRow) * (tw + gap), y + (idx / perRow) * (th + gap), tw, th);
                bool hv = tr.Contains(Event.current.mousePosition);
                BtcReplayUI.Fill(tr, new Color(1f, 1f, 1f, hv || _recAdding ? 0.1f : 0.04f));
                float g = fs * 2.2f;
                BtcReplayIcons.Glyph(BtcReplayIcons.Plus, new Rect(tr.center.x - g * 0.5f, tr.center.y - g * 0.5f, g, g), _recAdding ? BtcMenu.Accent : new Color(1f, 1f, 1f, hv ? 0.95f : 0.55f));
                BtcReplayIcons.Hint(tr, new Rect(_off.x + tr.x, _off.y + tr.y, tr.width, tr.height), Tt("Add an ingredient", "Добавить ингредиент"));
                if (BtcSfx.Btn(tr, "", BtcReplayUI.sBtn)) _recAdding = !_recAdding;
                idx++;
            }
            y += ((idx + perRow - 1) / perRow) * (th + gap) + fs * 0.2f;
            if (_recAdding && interact)
            {
                BtcUi.Section(ref y, w, fs, Tt("Add an ingredient", "Добавить ингредиент"));
                int a = 0;
                float aw = fs * 5.4f, ah = fs * 5.4f;
                int per2 = Mathf.Max(1, (int)((w - pad * 2f + gap) / (aw + gap)));
                for (int r = 0; r < BtcRecipes.ResCount; r++)
                {
                    if (arr[n + 3 + r] > 0) continue;
                    Rect cr = new Rect(pad + (a % per2) * (aw + gap), y + (a / per2) * (ah + gap), aw, ah);
                    int rr2 = r;
                    if (RecCard(cr, fs, BtcRecipes.ResIcon(r), BtcRecipes.ResName(r), false, 0, BtcRecipes.ResName(r)))
                    {
                        RecEdit(arr, k, host, "rec:" + k + ":add", delegate { arr[n + 3 + rr2] = 1; });
                        _recAdding = false;
                        BtcSfx.Ok();
                    }
                    a++;
                }
                y += ((a + per2 - 1) / per2) * (ah + gap) + fs * 0.3f;
            }
        }

        private void RecSlider(ref float y, float w, int fs, int[] arr, int[] van, int k, bool host, bool interact, int field, string label, int min, int max)
        {
            int n = k * BtcRecipes.IntsPer + field;
            int cur = arr[n];
            string id = "rec:" + k + ":" + field;
            float nv = BtcVisualRows.Slider(ref y, w, fs, 0, id, label, cur, min, max, 1f, "0", cur != van[n] ? "  (" + van[n] + ")" : "");
            if (cur != van[n]) BtcReplayUI.Fill(new Rect(fs - 5f, y - fs * 2.1f - 3f, 3f, fs * 2.1f), field == 2 ? Critical : Minor);
            int v = Mathf.RoundToInt(nv);
            if (interact && v != cur) RecEdit(arr, k, host, id, delegate { arr[n] = v; });
        }

        private void RecTile(Rect tr, int fs, int[] arr, int[] van, int k, bool host, bool interact, int r)
        {
            int n = k * BtcRecipes.IntsPer + 3 + r;
            int v = arr[n];
            bool ch = v != van[n];
            BtcReplayUI.Fill(tr, new Color(1f, 1f, 1f, 0.05f));
            if (ch) BtcReplayUI.Fill(new Rect(tr.x, tr.y, 3f, tr.height), Critical);
            float pad = fs * 0.4f, rowH = fs * 1.9f;
            BtcRecipes.DrawIconFit(new Rect(tr.x + pad, tr.y + pad, tr.width - pad * 2f, tr.height - rowH - fs * 1.6f), BtcRecipes.ResIcon(r));
            GUI.Label(new Rect(tr.x + 2f, tr.y + tr.height - rowH - fs * 1.6f, tr.width - 4f, fs * 1.5f), BtcRecipes.ResName(r), _cap);
            Rect cr = new Rect(tr.x, tr.yMax - rowH, tr.width, rowH);
            float bw = rowH * 0.9f;
            Rect minus = new Rect(cr.x + 3f, cr.y + (rowH - bw) * 0.5f, bw, bw), plus = new Rect(cr.xMax - bw - 3f, minus.y, bw, bw);
            BtcVisualRows.Txt(new Rect(minus.xMax, cr.y, plus.x - minus.xMax, rowH), "x" + v + (ch ? " (" + van[n] + ")" : ""), _chip, ch ? Critical : Color.white);
            if (interact)
            {
                if (IcoBtn(minus, BtcReplayIcons.Back10, false, BtcMenu.Accent, Tt("Less", "Меньше")) && v > 1) RecEdit(arr, k, host, "rec:" + k + ":r" + r, delegate { arr[n] = v - 1; });
                if (IcoBtn(plus, BtcReplayIcons.Plus, false, BtcMenu.Accent, Tt("More", "Больше")) && v < 99) RecEdit(arr, k, host, "rec:" + k + ":r" + r, delegate { arr[n] = v + 1; });
                Rect del = new Rect(tr.xMax - fs * 1.7f, tr.y + 2f, fs * 1.5f, fs * 1.5f);
                if (BtcReplayIcons.Plain(del, BtcReplayIcons.Trash, Color.white, Tt("Remove this ingredient", "Убрать ингредиент"))) RecEdit(arr, k, host, "rec:" + k + ":rm" + r, delegate { arr[n] = 0; });
            }
        }

        private void RecRequests(ref float y, float w, int fs, bool host, bool session, bool draft, bool interact)
        {
            float pad = fs, h = fs * 2.2f;
            if (host && BtcRecipes.PendingCount > 0)
            {
                y += fs * 0.4f;
                BtcUi.Section(ref y, w, fs, Tt("Recipe requests from players", "Запросы рецептов от игроков"));
                for (int i = BtcRecipes.PendingCount - 1; i >= 0; i--)
                {
                    int cc = 0;
                    int[] vals = BtcRecipes.PendingVals(i);
                    for (int k = 0; k < BtcRecipes.Count; k++) if (!BtcRecipes.Hidden(k) && BtcRecipes.DiffersFrom(vals, BtcRecipes.Cur, k)) cc++;
                    Rect row = new Rect(pad, y, w - pad * 2f, h);
                    BtcReplayUI.Fill(row, new Color(1f, 1f, 1f, 0.04f));
                    BtcReplayUI.Fill(new Rect(row.x, row.y, 3f, h), Minor);
                    BtcVisualRows.Txt(new Rect(row.x + fs * 0.8f, row.y, row.width * 0.5f, h), BtcRecipes.PendingName(i) + ": " + cc + " " + Tt("recipes differ from the current ones", "рецептов отличаются от текущих"), BtcVisualRows.Lbl, Color.white);
                    float bw = fs * 2.0f;
                    Rect rej = new Rect(row.xMax - fs * 0.4f - bw, row.y + h * 0.1f, bw, h * 0.8f);
                    Rect acc = new Rect(rej.x - bw - 4f, rej.y, bw, rej.height);
                    int pi = i;
                    if (IcoBtn(acc, BtcReplayIcons.Check, false, BtcMenu.Accent, Tt("Accept the recipes", "Принять рецепты")))
                    {
                        if (BtcGameplay.CanEdit)
                        {
                            int[] oldAll = (int[])BtcRecipes.Cur.Clone();
                            BtcRecipes.ResolvePending(pi, true);
                            BtcSfx.Ok();
                            BtcUndo.Push(Scope, Tt("Accepted recipes", "Приняты рецепты"), delegate { Array.Copy(oldAll, BtcRecipes.Cur, oldAll.Length); BtcRecipes.Edited(true); });
                        }
                        else { BtcSfx.Warn(); BtcMenu.Toast(Tt("Rules are locked now", "Правила сейчас заблокированы"), BtcReplayIcons.Lock); }
                        return;
                    }
                    if (IcoBtn(rej, BtcReplayIcons.Cross, false, Critical, Tt("Reject the recipes", "Отклонить рецепты"))) { BtcRecipes.ResolvePending(pi, false); BtcSfx.Delete(); return; }
                    y += h + 2f;
                }
            }
            if (!host && session && draft)
            {
                y += fs * 0.4f;
                BtcUi.Section(ref y, w, fs, Tt("Send recipes to the host", "Отправить рецепты хосту"));
                int ms = BtcRecipes.MyState;
                string text = ms == 1 ? Tt("Waiting for the host...", "Ждём решения хоста...") : (ms == 2 ? Tt("The host approved. You can send again.", "Хост одобрил. Можно отправить снова.") : (ms == 3 ? Tt("The host rejected. You can send again.", "Хост отклонил. Можно отправить снова.") : Tt("Your recipes are a draft until you send them.", "Ваши рецепты - черновик, пока вы их не отправите.")));
                Rect b = new Rect(pad, y, fs * 2.2f, h);
                BtcVisualRows.Txt(new Rect(b.xMax + fs * 0.6f, y, w - fs * 5f, h), text, BtcVisualRows.Lbl, Color.white);
                if (IcoBtn(b, BtcReplayIcons.Export, ms != 1, BtcMenu.Accent, Tt("Send the recipes to the host", "Отправить рецепты хосту")) && ms != 1) { BtcRecipes.SendMine(); BtcSfx.Ok(); }
                y += h + 4f;
            }
        }
    }
}
