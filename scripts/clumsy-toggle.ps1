param([Parameter(Mandatory=$true)][int]$ClumsyPid, [string]$Box = 'Drop')
# Clicks a module checkbox ('Drop', 'Lag', ...) in a running clumsy window: switches that module on/off without restarting
# WinDivert (start/stop of a clumsy instance stalls every loopback flow for seconds, two instances disturb each other).
if (-not ('BtcWin2' -as [type])) {
Add-Type @'
using System; using System.Runtime.InteropServices; using System.Text;
public static class BtcWin2 {
  public delegate bool EnumProc(IntPtr h, IntPtr l);
  [DllImport("user32.dll")] public static extern bool EnumChildWindows(IntPtr p, EnumProc f, IntPtr l);
  [DllImport("user32.dll", CharSet=CharSet.Auto)] public static extern int GetWindowText(IntPtr h, StringBuilder s, int n);
  [DllImport("user32.dll")] public static extern IntPtr SendMessage(IntPtr h, int m, IntPtr w, IntPtr l);
  public static IntPtr Find(IntPtr top, string text) {
    IntPtr hit = IntPtr.Zero;
    EnumChildWindows(top, (h, l) => { var sb = new StringBuilder(256); GetWindowText(h, sb, 256); if (sb.ToString() == text) { hit = h; return false; } return true; }, IntPtr.Zero);
    return hit;
  }
}
'@
}
$p = Get-Process -Id $ClumsyPid -ErrorAction SilentlyContinue
if (-not $p) { "CLUMSY TOGGLE FAIL: no process $ClumsyPid"; exit 1 }
$b = [BtcWin2]::Find($p.MainWindowHandle, $Box)
if ($b -eq [IntPtr]::Zero) { "CLUMSY TOGGLE FAIL: no '$Box' checkbox"; exit 1 }
[void][BtcWin2]::SendMessage($b, 0x00F5, [IntPtr]::Zero, [IntPtr]::Zero)   # BM_CLICK
$st = [int][BtcWin2]::SendMessage($b, 0x00F0, [IntPtr]::Zero, [IntPtr]::Zero)   # BM_GETCHECK
"toggled $Box -> $st"
