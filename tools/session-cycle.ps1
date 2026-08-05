# Opens and closes many pseudo-console sessions and reports whether any shell or console host was
# left behind. Same std-handle caveat as flood-benchmark.ps1: launch it with Start-Process, not from
# a redirected pipeline.
param(
    [Parameter(Mandatory = $true)][string]$Bin,
    [Parameter(Mandatory = $true)][string]$Log,
    [int]$Cycles = 20,
    [int]$SessionsPerCycle = 4
)

$report = New-Object System.Text.StringBuilder

Add-Type -Path (Join-Path $Bin "BetterTerminal.Interop.dll")
Add-Type -Path (Join-Path $Bin "BetterTerminal.Terminal.dll")

$before = (Get-Process conhost, cmd, powershell -ErrorAction SilentlyContinue).Count

for ($cycle = 0; $cycle -lt $Cycles; $cycle++) {
    $sessions = @()
    for ($index = 0; $index -lt $SessionsPerCycle; $index++) {
        $session = New-Object BetterTerminal.Terminal.ConPtySession(100, 30, 2000)
        $shell = if ($index % 2 -eq 0) {
            New-Object BetterTerminal.Terminal.ShellProfile(
                "Command Prompt", "$env:SystemRoot\system32\cmd.exe", "")
        }
        else {
            New-Object BetterTerminal.Terminal.ShellProfile(
                "Windows PowerShell", "$env:SystemRoot\system32\WindowsPowerShell\v1.0\powershell.exe", "-NoLogo")
        }

        $session.Start($shell, $env:SystemDrive)
        $sessions += $session
    }

    Start-Sleep -Milliseconds 600
    foreach ($session in $sessions) { $session.Close(); $session.Dispose() }
    Start-Sleep -Milliseconds 200
}

Start-Sleep -Seconds 2
$after = (Get-Process conhost, cmd, powershell -ErrorAction SilentlyContinue).Count
[void]$report.AppendLine("console-hosting processes before: $before  after $Cycles cycles of $SessionsPerCycle sessions: $after")
Set-Content -Path $Log -Value $report.ToString() -Encoding UTF8
