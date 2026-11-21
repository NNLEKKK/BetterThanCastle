using UnityEngine;

namespace BetterThanCastle
{
    /// <summary>The Replay tab in the new menu architecture: own scroll view around the replay library (BtcReplayLibrary) and the recording settings.</summary>
    internal sealed class BtcReplayTab : BtcMenuTab
    {
        private Vector2 _scroll;
        private float _h = 100f;

        internal override int Id { get { return 7; } }
        internal override bool IsActive { get { return BtcReplayUI.TabActive; } }
        internal override string UndoScope { get { return "replay"; } }

        internal override void Draw(Rect area, int fs)
        {
            BtcReplayUI.Ensure(fs);
            Rect view = new Rect(0f, 0f, area.width - (_h > area.height ? 14f : 0f), _h);
            _scroll = GUI.BeginScrollView(area, _scroll, view);
            BtcReplayUI.Draw(Event.current, view.width, fs * 2f, fs, GUI.color);
            GUI.EndScrollView();
            _h = BtcReplayUI.ContentH + fs;
        }
    }
}
