using System;
#if DEV
using System.IO;
#endif

namespace BetterThanCastle
{
    /// <summary>Greppable logging: every line is "[Tag] message". Not for hot paths (Update/OnGUI).
    /// DEV builds also write a compact machine-readable line to BTClogs/agent.log ("hh:mm:ss.fff|I/W/E|Tag|msg").</summary>
    internal static class BtcLog
    {
        internal static void Info(string tag, string msg)
        {
            if (BtcPlugin.Log != null) BtcPlugin.Log.LogInfo("[" + tag + "] " + msg);
            Agent('I', tag, msg);
        }

        internal static void Warn(string tag, string msg)
        {
            if (BtcPlugin.Log != null) BtcPlugin.Log.LogWarning("[" + tag + "] " + msg);
            Agent('W', tag, msg);
        }

        internal static void Error(string tag, string msg)
        {
            if (BtcPlugin.Log != null) BtcPlugin.Log.LogError("[" + tag + "] " + msg);
            Agent('E', tag, msg);
        }

        internal static void Error(string tag, string msg, Exception e)
        {
            Error(tag, msg + ": " + e);
        }

#if DEV
        private static StreamWriter _agent;
        private static bool _agentFailed;
        private static readonly object AgentLock = new object();

        private static void Agent(char level, string tag, string msg)
        {
            if (_agentFailed) return;
            lock (AgentLock)
            {
                try
                {
                    if (_agent == null)
                    {
                        Directory.CreateDirectory(Dev.BtcDevPaths.LogDir);
                        _agent = new StreamWriter(Path.Combine(Dev.BtcDevPaths.LogDir, "agent.log"), false) { AutoFlush = true };
                    }
                    string flat = (msg ?? string.Empty).Replace("\r", " ").Replace("\n", " / ");
                    if (flat.Length > 600) flat = flat.Substring(0, 600) + "...";
                    _agent.WriteLine(DateTime.Now.ToString("HH:mm:ss.fff") + "|" + level + "|" + tag + "|" + flat);
                }
                catch { _agentFailed = true; }
            }
        }
#else
        private static void Agent(char level, string tag, string msg) { }
#endif
    }
}
