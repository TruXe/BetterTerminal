# Captures the BetterTerminal window to a PNG using PrintWindow, which renders the window even when
# it is occluded, so taking a screenshot never has to steal foreground focus from whatever the user
# is doing.
param(
    [string]$ProcessName = "BetterTerminal",
    [Parameter(Mandatory = $true)][string]$Out
)

Add-Type -AssemblyName System.Drawing
Add-Type @"
using System;
using System.Runtime.InteropServices;
public class BetterTerminalCapture {
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);
    [DllImport("user32.dll")] public static extern bool PrintWindow(IntPtr hWnd, IntPtr hdcBlt, uint nFlags);
    [StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left, Top, Right, Bottom; }
}
"@

$app = Get-Process $ProcessName -ErrorAction Stop | Select-Object -First 1
$rect = New-Object BetterTerminalCapture+RECT
[BetterTerminalCapture]::GetWindowRect($app.MainWindowHandle, [ref]$rect) | Out-Null

$width = $rect.Right - $rect.Left
$height = $rect.Bottom - $rect.Top
$bitmap = New-Object System.Drawing.Bitmap $width, $height
$graphics = [System.Drawing.Graphics]::FromImage($bitmap)
$hdc = $graphics.GetHdc()

# 2 = PW_RENDERFULLCONTENT, required for a composited WPF window.
[BetterTerminalCapture]::PrintWindow($app.MainWindowHandle, $hdc, 2) | Out-Null

$graphics.ReleaseHdc($hdc)
$bitmap.Save($Out, [System.Drawing.Imaging.ImageFormat]::Png)
$graphics.Dispose()
$bitmap.Dispose()

"$Out ${width}x${height}"
