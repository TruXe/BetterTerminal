# Drives BetterTerminal through UI Automation and reports whether it survived. Invoking toolbar
# buttons by name needs no foreground focus, so this runs while the machine is in use.
#
# Example:
#   .\tools\ui-smoke.ps1 -Exe .\BetterTerminal.Shell\bin\x64\Release\BetterTerminal.exe `
#                        -Log .\smoke.log -Sequence "Split pane right|New tab|Close the focused pane"
#
# Steps are AutomationProperties.Name values, not button labels. Current buttons:
#   "New tab", "Split pane right", "Split pane down", "Close the focused pane",
#   "Open the command palette", "Settings", "Minimize", "Maximize or restore", "Close window"
param(
    [Parameter(Mandatory = $true)][string]$Exe,
    [Parameter(Mandatory = $true)][string]$Log,
    [string]$Sequence = "New tab|Close pane",
    [int]$StepDelaySeconds = 3
)

Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes

$report = New-Object System.Text.StringBuilder
function Write-Report($message) { [void]$report.AppendLine($message) }

$app = Start-Process $Exe -PassThru
Start-Sleep -Seconds 4

$processCondition = New-Object System.Windows.Automation.PropertyCondition(
    [System.Windows.Automation.AutomationElement]::ProcessIdProperty, $app.Id)
$window = [System.Windows.Automation.AutomationElement]::RootElement.FindFirst(
    [System.Windows.Automation.TreeScope]::Children, $processCondition)

if ($window -eq $null) {
    Write-Report "window not found"
    Set-Content -Path $Log -Value $report.ToString() -Encoding UTF8
    exit 1
}

Write-Report "window: $($window.Current.Name)"

foreach ($step in $Sequence.Split("|")) {
    if ($app.HasExited) {
        Write-Report "process died before step '$step' (exit $($app.ExitCode))"
        break
    }

    $nameCondition = New-Object System.Windows.Automation.PropertyCondition(
        [System.Windows.Automation.AutomationElement]::NameProperty, $step)
    # Filter on Button as well: a label inside a button exposes the same automation name, and
    # matching it first yields an element with no Invoke pattern.
    $buttonCondition = New-Object System.Windows.Automation.PropertyCondition(
        [System.Windows.Automation.AutomationElement]::ControlTypeProperty,
        [System.Windows.Automation.ControlType]::Button)
    $condition = New-Object System.Windows.Automation.AndCondition($nameCondition, $buttonCondition)
    $button = $window.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $condition)

    if ($button -eq $null) {
        Write-Report "button not found: $step"
        continue
    }

    $button.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke()
    Write-Report "invoked: $step"
    Start-Sleep -Seconds $StepDelaySeconds
}

Start-Sleep -Seconds 2
if ($app.HasExited) {
    Write-Report "RESULT: process CRASHED or exited, code $($app.ExitCode)"
}
else {
    Write-Report "RESULT: process alive"
    $app.Kill()
}

Set-Content -Path $Log -Value $report.ToString() -Encoding UTF8
Get-Content $Log
