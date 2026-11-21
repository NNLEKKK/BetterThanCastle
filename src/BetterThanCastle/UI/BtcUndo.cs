using System;
using System.Collections.Generic;
using UnityEngine;

namespace BetterThanCastle
{
    /// <summary>
    /// Undo (Ctrl+Z, as many steps as there are) for the menu tabs. One stack per scope ("hotkeys", "replay", ...): a tab names its scope in
    /// BtcMenuTab.UndoScope, every user change pushes an entry with the closure that reverts it, the shell handles Ctrl+Z (BtcMenu.HandleUndo),
    /// plays a sound and shows "Undone: ..." with the undo icon at the bottom of the menu. RULE for every new / migrated tab: each change the user can make
    /// by accident (a toggle, a bind, a delete, a move, a rename...) must push an entry (docs/menu.md "Undo").
    /// </summary>
    internal static class BtcUndo
    {
        private sealed class Entry { internal string Label; internal Action Undo; internal float At; internal string Key; }

        private const int Max = 200;
        private static readonly Dictionary<string, List<Entry>> Stacks = new Dictionary<string, List<Entry>>();

        /// <summary>True while an entry runs: changes made by the undo itself must not be recorded again.</summary>
        internal static bool Suspended;

        /// <summary>Records a change. mergeKey: repeated changes of the same thing within 0.8 s (a slider drag) collapse into one step.</summary>
        internal static void Push(string scope, string label, Action undo, string mergeKey)
        {
            if (Suspended || undo == null) return;
            if (_group != null) { _group.Add(new Entry { Label = label, Undo = undo }); return; }
            List<Entry> l;
            if (!Stacks.TryGetValue(scope, out l)) { l = new List<Entry>(); Stacks[scope] = l; }
            float now = Time.unscaledTime;
            if (mergeKey != null && l.Count > 0 && l[l.Count - 1].Key == mergeKey && now - l[l.Count - 1].At < 0.8f) { l[l.Count - 1].At = now; return; }
            l.Add(new Entry { Label = label, Undo = undo, At = now, Key = mergeKey });
            if (l.Count > Max) l.RemoveAt(0);
        }

        private static List<Entry> _group;

        /// <summary>Runs body and folds every change it records into ONE undo step (e.g. a profile that touches mod and vanilla binds).</summary>
        internal static void Group(string scope, string label, Action body)
        {
            if (Suspended || _group != null) { body(); return; }
            _group = new List<Entry>();
            try { body(); }
            finally
            {
                List<Entry> g = _group;
                _group = null;
                if (g.Count > 0)
                {
                    Push(scope, label, delegate { for (int i = g.Count - 1; i >= 0; i--) g[i].Undo(); });
                }
            }
        }

        internal static void Push(string scope, string label, Action undo) { Push(scope, label, undo, null); }

        internal static int Count(string scope)
        {
            List<Entry> l;
            return Stacks.TryGetValue(scope, out l) ? l.Count : 0;
        }

        /// <summary>Reverts the newest change of the scope. Returns false when there is nothing to undo (a message is shown either way).</summary>
        internal static bool Undo(string scope)
        {
            List<Entry> l;
            if (!Stacks.TryGetValue(scope, out l) || l.Count == 0)
            {
                BtcMenu.Toast(BtcReplayUI.T("Nothing to undo", "Откатывать нечего"), BtcReplayIcons.Undo);
                BtcSfx.Miss();
                return false;
            }
            Entry e = l[l.Count - 1];
            l.RemoveAt(l.Count - 1);
            Suspended = true;
            try { e.Undo(); }
            catch (Exception ex) { BtcLog.Error("Undo", "revert failed: " + e.Label, ex); }
            finally { Suspended = false; }
            BtcMenu.Toast(BtcReplayUI.T("Undone: ", "Откат: ") + e.Label + (l.Count > 0 ? "   (" + l.Count + ")" : ""), BtcReplayIcons.Undo);
            BtcSfx.Jump();
            return true;
        }
    }
}
