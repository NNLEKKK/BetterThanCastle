param([Parameter(Mandatory=$true)][string]$Filter, [string]$CArgs = '', [switch]$NoStart)
# Starts tools/clumsy/clumsy.exe minimized with a WinDivert filter + module args, then presses its Start button (the CLI alone does not start filtering).
# Prints the clumsy PID (stop it with Stop-Process). Loopback packets are only seen as 'outbound'.
$root = Split-Path -Parent $PSScriptRoot
$exe = Join-Path $root 'tools\clumsy\clumsy.exe'
if (-not ('BtcWin' -as [type])) {
Add-Type @'
using System; using System.Runtime.InteropServices; using System.Text;
public static class BtcWin {
  public delegate bool EnumProc(IntPtr h, IntPtr l);
  [DllImport("user32.dll")] public static extern bool EnumChildWindows(IntPtr p, EnumProc f, IntPtr l);
  [DllImport("user32.dll", CharSet=CharSet.Auto)] public static extern int GetWindowText(IntPtr h, StringBuilder s, int n);
  [DllImport("user32.dll", CharSet=CharSet.Auto)] public static extern int GetClassName(IntPtr h, StringBuilder s, int n);
  [DllImport("user32.dll")] public static extern IntPtr SendMessage(IntPtr h, int m, IntPtr w, IntPtr l);
  [DllImport("user32.dll", CharSet=CharSet.Auto)] public static extern IntPtr SendMessage(IntPtr h, int m, IntPtr w, string l);
  [DllImport("user32.dll", CharSet=CharSet.Auto)] public static extern IntPtr SendMessage(IntPtr h, int m, IntPtr w, StringBuilder l);
  // the filter box = first Edit control (its default text is clumsy's preset filter)
  public static IntPtr FirstEdit(IntPtr top) {
    IntPtr hit = IntPtr.Zero;
    EnumChildWindows(top, (h, l) => { var sb = new StringBuilder(64); GetClassName(h, sb, 64); if (sb.ToString() == "Edit") { hit = h; return false; } return true; }, IntPtr.Zero);
    return hit;
  }
  public static string Text(IntPtr h) { var sb = new StringBuilder(2048); SendMessage(h, 0x000D, (IntPtr)2048, sb); return sb.ToString(); }
  public static IntPtr Find(IntPtr top, string text) {
    IntPtr hit = IntPtr.Zero;
    EnumChildWindows(top, (h, l) => { var sb = new StringBuilder(256); GetWindowText(h, sb, 256); if (sb.ToString() == text) { hit = h; return false; } return true; }, IntPtr.Zero);
    return hit;
  }
}
'@
}
$p = Start-Process $exe -ArgumentList ("--filter `"$Filter`" " + $CArgs) -PassThru -WindowStyle Minimized
$h = [IntPtr]::Zero
for ($i = 0; $i -lt 40 -and $h -eq [IntPtr]::Zero; $i++) { Start-Sleep -Milliseconds 150; $p.Refresh(); $h = $p.MainWindowHandle }
if ($h -eq [IntPtr]::Zero) { "CLUMSY FAIL: no window"; exit 1 }
# clumsy 0.3 ignores --filter from the command line (the box keeps the preset "outbound and loopback" = EVERY loopback flow):
# write the filter into the box before Start and verify it
$e = [BtcWin]::FirstEdit($h)
if ($e -eq [IntPtr]::Zero) { "CLUMSY FAIL: no filter box"; exit 1 }
[void][BtcWin]::SendMessage($e, 0x000C, [IntPtr]::Zero, $Filter)   # WM_SETTEXT
if ([BtcWin]::Text($e) -ne $Filter) { "CLUMSY FAIL: filter box says '$([BtcWin]::Text($e))'"; exit 1 }
if (-not $NoStart) {
    $b = [BtcWin]::Find($h, 'Start')
    if ($b -eq [IntPtr]::Zero) { "CLUMSY FAIL: no Start button"; exit 1 }
    [void][BtcWin]::SendMessage($b, 0x00F5, [IntPtr]::Zero, [IntPtr]::Zero)   # BM_CLICK
    Start-Sleep -Milliseconds 500
    if ([BtcWin]::Find($h, 'Stop') -eq [IntPtr]::Zero) { "CLUMSY WARN: Start pressed but no Stop button (filter error?)" }
}
$p.Id
