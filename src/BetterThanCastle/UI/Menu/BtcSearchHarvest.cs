using System;
using System.Collections.Generic;
using UnityEngine;

namespace BetterThanCastle
{
    /// <summary>
    /// Feeds the global search with EVERY row of the menu tabs, without a hand-kept list: once per language each tab (and each of its pages) is
    /// drawn in an idle Layout event (nothing is painted, buttons never fire) while <see cref="Active"/> is set. The row helpers of BtcUi and
    /// BtcVisualRows report their label through <see cref="Row"/> / <see cref="Fold"/> (folds are forced open meanwhile, Expert rows shown),
    /// so new rows become searchable by themselves. A result jumps to the tab, page and fold and pulses the row (BtcJump scope "h", key = label).
    /// </summary>
    internal static class BtcSearchHarvest
    {
        internal sealed class Entry
        {
            public int Tab, Page = -1;
            public string Fold, FoldLabel, Label, Hint;
            public bool IsFold;
        }

        private sealed class Spec
        {
            public int Tab;
            public int[] Pages;
            public Func<int> Get;
            public Action<int> Set;
        }

        internal const string Scope = "h";
        internal static bool Active;
        private static int _tab, _page;
        private static string _fold, _foldLabel;
        private static readonly List<Entry> _list = new List<Entry>();
        private static readonly HashSet<string> _seen = new HashSet<string>();
        private static Spec _cur;

        private static Spec[] Specs()
        {
            return new[]
            {
                new Spec { Tab = 1, Pages = new[] { BtcVisualTab.PRings, BtcVisualTab.PWorld, BtcVisualTab.PPlates, BtcVisualTab.PStats, BtcVisualTab.PScreen },
                    Get = () => BtcMenuState.Int("visual:page", 0), Set = v => BtcMenuState.SetInt("visual:page", v) },
                new Spec { Tab = 2 },
                new Spec { Tab = 4, Pages = new[] { 5, 6 }, Get = () => BtcGameplayTab.Page, Set = v => BtcGameplayTab.Page = v },
                new Spec { Tab = 5 },
                new Spec { Tab = 7, Pages = new[] { 1 }, Get = () => BtcReplayLibrary.SettingsOpen ? 1 : 0, Set = v => BtcReplayLibrary.SettingsOpen = v == 1 },
                new Spec { Tab = 8, Pages = new[] { BtcPracticeTab.PDuel, BtcPracticeTab.PAim }, Get = () => BtcPracticeTab.Page, Set = v => BtcPracticeTab.Page = v },
                new Spec { Tab = 11, Pages = new[] { 2 }, Get = () => BtcMenuState.Int("stats:page", 0), Set = v => BtcMenuState.SetInt("stats:page", v) },
                new Spec { Tab = 12, Pages = new[] { 0, 1 }, Get = () => BtcMenuState.Int("uiedit:page", 0), Set = v => BtcMenuState.SetInt("uiedit:page", v) },
                new Spec { Tab = 14 },
                new Spec { Tab = BtcGeneralTab.TabId },
            };
        }

        /// <summary>Row helpers call this with their label and rect: notes the row while harvesting, otherwise lets a search jump pulse it.</summary>
        internal static void Row(string label, string hint, Rect r)
        {
            if (Active) { Note(label, hint, false); return; }
            BtcJump.Row(Scope, label, r);
        }

        /// <summary>Fold headers call this; returns true while harvesting (the fold is treated as open).</summary>
        internal static bool Fold(string key, string label)
        {
            if (!Active) return false;
            _fold = key; _foldLabel = label;
            Note(label, null, true);
            return true;
        }

        private static void Note(string label, string hint, bool isFold)
        {
            if (string.IsNullOrEmpty(label) || label.Length < 3) return;
            string k = _tab + "|" + label;
            if (!_seen.Add(k)) return;
            _list.Add(new Entry { Tab = _tab, Page = _cur != null && _cur.Pages != null ? _page : -1, Fold = isFold ? null : _fold, FoldLabel = isFold ? null : _foldLabel, Label = label, Hint = hint, IsFold = isFold });
        }

        /// <summary>Draws every tab once in an idle Layout event and returns what the rows reported. Call from inside OnGUI.</summary>
        internal static List<Entry> Run(int fs)
        {
            _list.Clear(); _seen.Clear();
            Event saved = Event.current;
            Event idle = new Event(); idle.type = EventType.Layout; idle.mousePosition = new Vector2(-9999f, -9999f);
            int savedTab = BtcMenu.CurrentId;
            bool savedExpert = BtcVisualRows.Expert;
            Active = true;
            try
            {
                Event.current = idle;
                foreach (Spec s in Specs())
                {
                    BtcMenuTab tab = BtcMenu.TabById(s.Tab);
                    if (tab == null) continue;
                    _cur = s; _tab = s.Tab;
                    int before = s.Get != null ? s.Get() : 0;
                    int[] pages = s.Pages ?? new[] { 0 };
                    for (int i = 0; i < pages.Length; i++)
                    {
                        _page = pages[i]; _fold = null; _foldLabel = null;
                        if (s.Set != null) s.Set(pages[i]);
                        BtcMenu.CurrentId = s.Tab;
                        try { tab.Draw(new Rect(0f, 0f, 1400f, 900f), fs); }
                        catch (Exception e) { BtcLog.Warn("Search", "harvest tab " + s.Tab + " page " + pages[i] + ": " + e.Message); }
                    }
                    if (s.Set != null) s.Set(before);
                }
            }
            finally
            {
                Active = false;
                _cur = null;
                Event.current = saved;
                BtcMenu.CurrentId = savedTab;
                BtcVisualRows.Expert = savedExpert;
                GUIUtility.keyboardControl = 0;
            }
            BtcLog.Info("Search", "harvested " + _list.Count + " rows");
            return new List<Entry>(_list);
        }

        /// <summary>Opens the tab / page / fold of an entry and pulses its row.</summary>
        internal static void Jump(Entry en)
        {
            if (!ResourcePackManager.GoTab(en.Tab)) return;
            BtcMenu.ClearSearch(en.Tab);
            foreach (Spec s in Specs()) if (s.Tab == en.Tab && s.Set != null && en.Page >= 0) s.Set(en.Page);
            if (en.Tab == 1 && BtcVisualTab.Instance != null && en.Page >= 0) BtcVisualTab.Instance.SetPage(en.Page);
            if (en.Fold != null) BtcUi.SetOpen(en.Fold, true);
            if (en.IsFold) return;
            BtcJump.Target(Scope, en.Label);
        }
    }
}
