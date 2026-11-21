using UnityEngine;

namespace BetterThanCastle
{
    /// <summary>
    /// Local visual-only optimization: unit Animators use CullUpdateTransforms (state machine, events and Animator.deltaPosition keep
    /// running; only retarget/IK/transform writes stop while no renderer is visible). Gated by Performance/AnimatorCulling and
    /// BetterConnection (read at call time). No wire impact.
    /// </summary>
    internal static class BtcAnimCull
    {
        internal static bool Active
        {
            get { return BtcConfig.AnimatorCulling != null && BtcConfig.AnimatorCulling.Value && BtcNetworkGate.Enabled; }
        }

        internal static void Apply(Component owner)
        {
            if (owner == null || !Active) return;
            Animator a = owner.GetComponent<Animator>();
            if (a != null && a.cullingMode == AnimatorCullingMode.AlwaysAnimate) a.cullingMode = AnimatorCullingMode.CullUpdateTransforms;
        }
    }
}
