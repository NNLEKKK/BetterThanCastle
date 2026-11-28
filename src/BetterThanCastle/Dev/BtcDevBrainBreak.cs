#if DEV
using System;
using Brix.Game.AI;
using Brix.Game.AI.InstructionType;
using Brix.UI.Icons;
using Motus.Behavior;
using UnityEngine;

namespace BetterThanCastle
{
    /// <summary>DEV lab for BehaviorErrorLogPatch (frozen units): "brainbreak go" gives the first worker a task whose node throws
    /// (vanilla would disable his Behavior for good), "brainbreak move" orders him 8 blocks away, "brainbreak report" logs where he
    /// is and whether his brain runs.</summary>
    internal static class BtcDevBrainBreak
    {
        private static Labor _w;
        private static Vector3 _start;

        internal static void Run(string what)
        {
            try
            {
                if (what == "go")
                {
                    Occupation[] occ = UnityEngine.Object.FindObjectsOfType<Occupation>();
                    _w = occ.Length > 0 ? occ[0].GetComponent<Labor>() : null;
                    if (_w == null) { BtcLog.Warn("DevBrain", "no worker"); return; }
                    _start = _w.transform.position;
                    WorkInstruction<Labor> wi = new WorkInstruction<Labor>("BrainBreak", IconKeys.Operator, l => Node.Sequence.Do(() => Boom()).ToNode);
                    Order.Package.Of<Labor>(wi, Labor.DecisionSource.User).Send(_w);
                    BtcLog.Info("DevBrain", "sent throwing task to " + _w.name + " at " + _start);
                }
                else if (what == "move" && _w != null)
                {
                    Order.Move.RunToward.Send(_w, Location.Of(_start + new Vector3(8f, 0f, 0f)), Labor.DecisionSource.User);
                    BtcLog.Info("DevBrain", "move order to " + _w.name);
                }
                else if (_w != null)
                {
                    BtcLog.Info("DevBrain", "report " + _w.name + " moved=" + Vector3.Distance(_start, _w.transform.position).ToString("0.0")
                        + " brain=" + _w.enabled + " err=" + _w.ErrorHappened + " task=" + (_w.CurrentTask != null ? _w.CurrentTask.Description : "-")
                        + " pending=" + (_w.PendingTask != null));
                }
            }
            catch (Exception e) { BtcLog.Warn("DevBrain", what + ": " + e); }
        }

        private static Node Boom() { throw new InvalidOperationException("brainbreak lab"); }
    }
}
#endif
