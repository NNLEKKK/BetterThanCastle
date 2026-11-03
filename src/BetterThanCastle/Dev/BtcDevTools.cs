#if DEV
using System;
using System.IO;
using BepInEx.Logging;

namespace BetterThanCastle.Dev
{
    /// <summary>DEV-only: mirrors the whole BepInEx log (incl. Unity log) into the repo's BTClogs folder.</summary>
    internal static class BtcDevTools
    {
        internal static void Init()
        {
            try
            {
                Directory.CreateDirectory(BtcDevPaths.LogDir);
                string file = Path.Combine(BtcDevPaths.LogDir, "btc-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".log");
                BepInEx.Logging.Logger.Listeners.Add(new FileListener(file));
            }
            catch (Exception e)
            {
                BtcPlugin.Log.LogWarning("[Dev] log dir init failed: " + e.Message);
            }
        }

        private sealed class FileListener : ILogListener
        {
            private readonly StreamWriter _w;

            internal FileListener(string path)
            {
                _w = new StreamWriter(path, false) { AutoFlush = true };
            }

            public void LogEvent(object sender, LogEventArgs e)
            {
                _w.WriteLine(e.ToString());
            }

            public void Dispose()
            {
                _w.Dispose();
            }
        }
    }
}
#endif
