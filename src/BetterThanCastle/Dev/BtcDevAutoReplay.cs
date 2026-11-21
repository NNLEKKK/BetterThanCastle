#if DEV
using System;
using System.Collections.Generic;
using UnityEngine;

namespace BetterThanCastle.Dev
{
    /// <summary>DEV only. env BTC_AUTOREPLAY=&lt;file name part|latest&gt; starts that replay once the main menu allows it.</summary>
    internal sealed class BtcDevAutoReplay : MonoBehaviour
    {
        private string _want;
        private float _next;
        private bool _started;
        private float _lastLog;

        internal static void Init()
        {
            string w = Environment.GetEnvironmentVariable("BTC_AUTOREPLAY");
            if (string.IsNullOrEmpty(w)) return;
            GameObject go = new GameObject("BtcDevAutoReplay");
            UnityEngine.Object.DontDestroyOnLoad(go);
            go.hideFlags = HideFlags.HideAndDontSave;
            go.AddComponent<BtcDevAutoReplay>()._want = w;
            BtcLog.Info("AutoReplay", "armed: " + w);
        }

        private void Update()
        {
            if (_started && Time.realtimeSinceStartup - _lastLog > 5f)
            {
                _lastLog = Time.realtimeSinceStartup;
                BtcLog.Info("AutoReplay", "state loading=" + BtcReplay.Loading + " running=" + BtcReplay.Running + " fin=" + BtcReplay.Finished + " dt=" + (int)(Time.unscaledDeltaTime * 1000f) + "ms");
            }
            if (_started || Time.realtimeSinceStartup < _next) return;
            _next = Time.realtimeSinceStartup + 2f;
            try
            {
                if (!BtcReplay.CanStartReplay())
                {
                    if (Time.realtimeSinceStartup - _lastLog > 20f) { _lastLog = Time.realtimeSinceStartup; BtcLog.Info("AutoReplay", "waiting for menu"); }
                    return;
                }
                if (Time.realtimeSinceStartup < 20f) return;
                List<BtcReplay.Data> list = BtcReplay.ListReplays();
                if (list.Count == 0) { BtcLog.Warn("AutoReplay", "no replays found"); _started = true; return; }
                BtcReplay.Data pick = null;
                if (_want == "latest")
                {
                    pick = list[0];
                    for (int i = 1; i < list.Count; i++)
                        if (System.IO.File.GetLastWriteTime(list[i].FilePath) > System.IO.File.GetLastWriteTime(pick.FilePath)) pick = list[i];
                }
                else
                {
                    for (int i = 0; i < list.Count; i++)
                        if (list[i].FileName != null && list[i].FileName.IndexOf(_want, StringComparison.OrdinalIgnoreCase) >= 0) { pick = list[i]; break; }
                }
                _started = true;
                if (pick == null) { BtcLog.Warn("AutoReplay", "no match for " + _want); return; }
                BtcLog.Info("AutoReplay", "starting " + pick.FileName);
                BtcReplay.StartReplay(pick);
            }
            catch (Exception e) { _started = true; BtcLog.Error("AutoReplay", e.ToString()); }
        }
    }
}
#endif
