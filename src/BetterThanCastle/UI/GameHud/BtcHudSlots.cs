using System.Collections.Generic;
using UnityEngine;

namespace BetterThanCastle
{
    /// UI editor (tab 12) slots for uGUI regions of the C# game menu: the editor writes BtcHud.Slot values (Enabled, Scale, Alpha,
    /// Norm offset as a fraction of the screen height, y down) and calls ApplyAll; each registered root is shown/hidden, scaled
    /// around its pivot, faded via a CanvasGroup and shifted from its vanilla position. Event driven: no per-frame work.
    internal static class BtcHudSlots
    {
        private sealed class Reg
        {
            internal string Id;
            internal RectTransform Rt;
            internal Vector2 BasePos;
            internal CanvasGroup Group;
        }

        private static readonly List<Reg> Regs = new List<Reg>();

        internal static void Register(string id, RectTransform rt)
        {
            if (rt == null) return;
            for (int i = Regs.Count - 1; i >= 0; i--) if (Regs[i].Id == id || Regs[i].Rt == null) Regs.RemoveAt(i);
            Reg r = new Reg { Id = id, Rt = rt, BasePos = rt.anchoredPosition };
            Regs.Add(r);
            Apply(r);
        }

        /// <summary>True when the transform (or one of its parents) is a C# game menu region owned by a slot (BtcInterface skips it).</summary>
        internal static bool Owns(Transform t)
        {
            for (int i = 0; i < Regs.Count; i++)
            {
                RectTransform rt = Regs[i].Rt;
                if (rt != null && (t == rt || t.IsChildOf(rt) || rt.IsChildOf(t))) return true;
            }
            return false;
        }

        internal static void ApplyAll()
        {
            for (int i = Regs.Count - 1; i >= 0; i--)
            {
                if (Regs[i].Rt == null) { Regs.RemoveAt(i); continue; }
                Apply(Regs[i]);
            }
        }

        private static void Apply(Reg r)
        {
            try
            {
                BtcHud.Slot s = BtcHud.SlotOf(r.Id);
                float sf = 1f;
                Canvas c = r.Rt.GetComponentInParent<Canvas>();
                if (c != null && c.rootCanvas != null && c.rootCanvas.scaleFactor > 0f) sf = c.rootCanvas.scaleFactor;
                Vector2 off = s.Offset / sf;
                r.Rt.anchoredPosition = r.BasePos + new Vector2(off.x, -off.y);
                float k = s.Scale > 0.05f ? s.Scale : 1f;
                r.Rt.localScale = new Vector3(k, k, 1f);
                bool fade = s.Alpha < 0.999f || !s.Enabled;
                if (fade && r.Group == null) r.Group = r.Rt.gameObject.GetComponent<CanvasGroup>() ?? r.Rt.gameObject.AddComponent<CanvasGroup>();
                if (r.Group != null)
                {
                    r.Group.alpha = s.Enabled ? s.Alpha : 0f;
                    r.Group.blocksRaycasts = s.Enabled;
                    r.Group.interactable = s.Enabled;
                }
            }
            catch (System.Exception e) { BtcLog.Error("GameHud", "slot " + r.Id + ": " + e.Message); }
        }
    }
}
