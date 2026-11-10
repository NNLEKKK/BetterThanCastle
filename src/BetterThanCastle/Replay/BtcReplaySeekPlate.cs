using UnityEngine;

namespace BetterThanCastle
{
    /// <summary>Kept for the quiet-mode bookkeeping of BtcReplaySeek; the loading screen itself is drawn by BtcReplayLoadUI.</summary>
    internal sealed class BtcReplaySeekPlate : MonoBehaviour
    {
        private void Awake() { BtcReplayLoadUI.EnsureHost(); }
    }
}
