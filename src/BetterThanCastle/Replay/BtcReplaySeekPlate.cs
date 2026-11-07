using UnityEngine;

namespace BetterThanCastle
{
    /// <summary>Plate shown over the black screen while the replay is preprocessed for instant seek (BtcReplaySeek quiet mode).</summary>
    internal sealed class BtcReplaySeekPlate : MonoBehaviour
    {
        private GUIStyle _style;

        private void OnGUI()
        {
            string text = BtcReplaySeek.Plate;
            if (text == null) return;
            GUI.depth = -10000;
            if (_style == null)
            {
                _style = new GUIStyle(GUI.skin.box);
                _style.alignment = TextAnchor.MiddleCenter;
                _style.fontSize = 20;
                _style.wordWrap = true;
                _style.normal.textColor = Color.white;
            }
            // opaque full-screen cover: the world keeps rendering (terrain updates need it) but is not shown
            GUI.color = Color.black;
            GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = Color.white;
            float w = Mathf.Min(620f, Screen.width - 40f), h = 70f;
            GUI.Box(new Rect((Screen.width - w) * 0.5f, (Screen.height - h) * 0.5f, w, h), text, _style);
        }
    }
}
