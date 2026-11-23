using UnityEngine.SceneManagement;

namespace BetterThanCastle
{
    /// Active scene name/key without a new string per call (Scene.name builds one every time: per-frame callers made ~10 KB/s).
    internal static class BtcScene
    {
        private static int _h = int.MinValue;
        private static string _name = "", _key = "";

        private static void Check()
        {
            Scene s = SceneManager.GetActiveScene();
            int h = s.GetHashCode();
            if (h == _h) return;
            _h = h;
            _name = s.name ?? "";
            _key = s.buildIndex + ":" + _name;
        }

        internal static string Name { get { Check(); return _name; } }
        internal static string Key { get { Check(); return _key; } }
    }
}
