# Measures end-to-end throughput of a pseudo-console session: pipe read, UTF-8 decode, VT parse and
# grid update, with no UI attached.
#
# MUST run in a process whose standard output is not redirected. With redirected std handles the
# child shell writes into them instead of the pseudo console and the grid stays empty, which looks
# exactly like a broken implementation but is not. Launch it as:
#   Start-Process powershell -WindowStyle Hidden -Wait -ArgumentList @(
#       "-NoProfile","-File",".\tools\flood-benchmark.ps1","-Bin","<path>","-Work","<dir>","-Log","<file>")
param(
    [Parameter(Mandatory = $true)][string]$Bin,
    [Parameter(Mandatory = $true)][string]$Work,
    [Parameter(Mandatory = $true)][string]$Log,
    [int]$Lines = 87000
)

$report = New-Object System.Text.StringBuilder
function Write-Report($message) { [void]$report.AppendLine($message) }

Add-Type -Path (Join-Path $Bin "BetterTerminal.Interop.dll")
Add-Type -Path (Join-Path $Bin "BetterTerminal.Terminal.dll")

$payload = Join-Path $Work "flood.txt"
if (-not (Test-Path $payload)) {
    $line = ("The quick brown fox jumps over the lazy dog 0123456789 " * 2).PadRight(120).Substring(0, 120)
    $writer = New-Object System.IO.StreamWriter($payload, $false, [System.Text.Encoding]::ASCII)
    for ($i = 0; $i -lt $Lines; $i++) { $writer.WriteLine($line) }
    $writer.Close()
}

$sizeMb = (Get-Item $payload).Length / 1MB
Write-Report ("input MB: " + [math]::Round($sizeMb, 2))

$session = New-Object BetterTerminal.Terminal.ConPtySession(120, 40, 5000)
$shell = New-Object BetterTerminal.Terminal.ShellProfile(
    "Command Prompt", "$env:SystemRoot\system32\cmd.exe", "/c type `"$payload`"")

$timer = [System.Diagnostics.Stopwatch]::StartNew()
$session.Start($shell, $env:SystemDrive)
while ($session.IsRunning -and $timer.Elapsed.TotalSeconds -lt 120) { Start-Sleep -Milliseconds 20 }
$timer.Stop()

$grid = $session.Grid
Write-Report ("elapsed s: " + [math]::Round($timer.Elapsed.TotalSeconds, 2) + "  exit: " + $session.ExitCode)
Write-Report ("throughput MB/s: " + [math]::Round($sizeMb / $timer.Elapsed.TotalSeconds, 2))
Write-Report ("scrollback lines: $($grid.ScrollbackCount)  total: $($grid.TotalLines)  cursor: $($grid.CursorRow),$($grid.CursorColumn)")

$cells = $null
$version = $null
if ($grid.TryGetLine($grid.TotalLines - 2, [ref]$cells, [ref]$version)) {
    Write-Report ("last content line: [" + (-join ($cells | ForEach-Object { $_.Character })).TrimEnd() + "]")
}

$session.Close()
$session.Dispose()
Set-Content -Path $Log -Value $report.ToString() -Encoding UTF8
