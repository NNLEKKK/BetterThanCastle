using Brix.Game.Components;

namespace BetterThanCastle
{
	// Bear traps task (BtcTrapTask, rule work.trapTask): re-arming is done by workers of a trap task, so the vanilla self-standing job
	// of every trap (ObjectActivator as its own project, fixed priority) is off for bear traps. ObjectActivator.HasWorkFor postfix in
	// Patches/ListRulesPatches.cs. The manual re-arm (builder secondary click on the trap) sends the Activate instruction directly and still works.
	public static class BtcTrapRearm
	{
		internal static bool BlockAuto(ObjectActivator act)
		{
			return act != null && BtcTrapTask.On && act.GetComponent<BearTrapTrigger>() != null;
		}
	}
}
