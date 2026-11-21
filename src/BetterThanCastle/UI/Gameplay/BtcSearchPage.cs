using System;
using System.Collections.Generic;
using UnityEngine;

namespace BetterThanCastle
{
    /// <summary>
    /// Global search of the Gameplay tab: the search field is in the controls row of every page; while it holds text this page replaces the
    /// current one and lists matching pages, rules (editable in place, grouped) and recipes (a click opens the recipe).
    /// </summary>
    internal sealed partial class BtcGameplayTab
    {
        private bool ResultRow(ref float y, float w, int fs, int icon, string text, string right)
        {
            float pad = fs;
            Rect r = new Rect(pad, y, w - pad * 2f, fs * 2.1f);
            bool hover = r.Contains(Event.current.mousePosition);
            BtcReplayUI.Fill(r, new Color(1f, 1f, 1f, hover ? 0.09f : 0.04f));
            float ig = fs * 1.3f;
            BtcReplayIcons.Glyph(icon, new Rect(r.x + fs * 0.6f, r.center.y - ig * 0.5f, ig, ig), hover ? BtcMenu.Accent : new Color(1f, 1f, 1f, 0.6f));
            BtcVisualRows.Txt(new Rect(r.x + fs * 2.4f, r.y, r.width * 0.6f, r.height), text, BtcVisualRows.Lbl, Color.white);
            if (right.Length > 0) BtcVisualRows.Txt(new Rect(r.x + r.width * 0.6f, r.y, r.width * 0.4f - fs * 0.6f, r.height), right, BtcVisualRows.Dim, new Color(1f, 1f, 1f, 0.6f));
            y += r.height + 2f;
            return GUI.Button(r, GUIContent.none, GUIStyle.none);
        }

        private void PageSearch(ref float y, float w, int fs, float[] shown, float[] bas, bool interact)
        {
            string q = Query.Trim().ToLowerInvariant();
            bool onlyCh = OnlyChanged;
            bool any = false;

            // pages
            bool pagesHead = false;
            for (int i = 0; i < RailPages.Length; i++)
            {
                int p = RailPages[i];
                if (p == PRules || !PageName(p).ToLowerInvariant().Contains(q)) continue;
                if (!pagesHead) { BtcUi.Section(ref y, w, fs, Tt("Pages", "Страницы")); pagesHead = true; }
                any = true;
                if (ResultRow(ref y, w, fs, PageIcon(p), PageName(p), "")) { Query = ""; Page = p; GUIUtility.keyboardControl = 0; _scroll = Vector2.zero; }
            }

            // rules, grouped
            List<string> groups = GroupList();
            for (int g = 0; g < groups.Count; g++)
            {
                List<int> rows = new List<int>();
                for (int i = 0; i < BtcGameplay.Defs.Count; i++)
                {
                    BtcGameplay.Def d = BtcGameplay.Defs[i];
                    if (GroupOf(i) != groups[g] || !Visible(d, shown) || !Match(d, q)) continue;
                    if (onlyCh && !RowChanged(i, shown, bas)) continue;
                    rows.Add(i);
                }
                if (rows.Count == 0) continue;
                any = true;
                BtcUi.Section(ref y, w, fs, GroupName(groups[g]) + " (" + rows.Count + ")");
                for (int r = 0; r < rows.Count; r++) Row(ref y, w, fs, rows[r], shown, bas, interact);
                y += fs * 0.3f;
            }

            // recipes
            BtcRecipes.Ensure();
            bool recHead = false;
            for (int k = 0; k < BtcRecipes.Count; k++)
            {
                if (BtcRecipes.Hidden(k) || !BtcRecipes.Name(k).ToLowerInvariant().Contains(q)) continue;
                if (!recHead) { BtcUi.Section(ref y, w, fs, Tt("Recipes", "Рецепты")); recHead = true; }
                any = true;
                int grp = -1;
                for (int g = 0; g < BtcRecipes.GroupCount && grp < 0; g++)
                {
                    int[] it = BtcRecipes.GroupItems(g);
                    for (int j = 0; j < it.Length; j++) if (it[j] == k) { grp = g; break; }
                }
                if (ResultRow(ref y, w, fs, BtcReplayIcons.Blueprint, BtcRecipes.Name(k), grp >= 0 ? BtcRecipes.GroupName(grp) : ""))
                {
                    Query = ""; GUIUtility.keyboardControl = 0; Page = PRecipes;
                    RecGroup = grp; RecSel = k; _scroll = Vector2.zero;
                }
            }

            if (!any) BtcUi.Note(ref y, w, fs, Tt("Nothing found in rules, recipes or pages.", "Ничего не найдено ни в правилах, ни в рецептах, ни в страницах."));
        }
    }
}
