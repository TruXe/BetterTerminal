# Builds BetterTerminal and stages the result in BUILD, which is what the user runs and what a
# release is cut from. Run it after every change - that is the standing rule for this repository.
#
# What it produces under BUILD:
#   BetterTerminal.exe        the one-file launcher, the whole application in a single file
#   app\                      the same application as loose files, INCLUDING beterm-wrap.exe
#   service\                  the optional Windows service and the helpers it accounts for
#   README.txt                kept in step with the version being built
#   dist\, BetterTerminal-x64.zip   the release layout and its archive, same as the workflow makes
#
# Example:
#   .\tools\build.ps1
#   .\tools\build.ps1 -Configuration Debug -SkipZip
param(
    [ValidateSet("Debug", "Release")][string]$Configuration = "Release",
    [switch]$Rebuild,
    [switch]$SkipZip
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot

$msbuild = "C:\Program Files\Microsoft Visual Studio\18\Community\MSBuild\Current\Bin\MSBuild.exe"
if (-not (Test-Path $msbuild)) {
    $found = Get-ChildItem "C:\Program Files\Microsoft Visual Studio" -Recurse -Filter MSBuild.exe -Depth 5 -ErrorAction SilentlyContinue |
        Where-Object { $_.FullName -like "*\Current\Bin\MSBuild.exe" } | Select-Object -First 1
    if ($null -eq $found) { throw "MSBuild was not found. Set the path at the top of this script." }
    $msbuild = $found.FullName
}

# The one version of the application. Every assembly links VersionInfo.cs, and the launcher carries
# the same number in its own resource; disagreeing numbers mean the copy under the user profile
# would compare against the wrong one, so this stops rather than shipping them.
$versionFile = Join-Path $root "VersionInfo.cs"
$version = ([regex]::Match((Get-Content $versionFile -Raw), 'AssemblyFileVersion\("([^"]+)"\)')).Groups[1].Value
if (-not $version) { throw "No AssemblyFileVersion in VersionInfo.cs" }
$launcher = ([regex]::Match((Get-Content (Join-Path $root "BetterTerminal.Bootstrap\Bootstrap.rc") -Raw), 'VALUE "FileVersion", "([^"]+)"')).Groups[1].Value
if ($launcher -ne $version) { throw "VersionInfo.cs says $version but the launcher resource says $launcher" }
$short = ($version -split '\.')[0..2] -join '.'
Write-Host "BetterTerminal $short ($Configuration)"

$target = if ($Rebuild) { "/t:Rebuild" } else { "" }
& $msbuild (Join-Path $root "BetterTerminal.sln") /p:Configuration=$Configuration /p:Platform=x64 /v:minimal $target
if ($LASTEXITCODE -ne 0) { throw "The build failed; BUILD was left untouched." }

$bin = { param($project) Join-Path $root "$project\bin\x64\$Configuration" }
$build = Join-Path $root "BUILD"
$dist = Join-Path $build "dist"

# A file that is being run right now cannot be replaced, and the most likely one is the launcher the
# user has open. That is worth naming rather than throwing a copy error from the middle of staging,
# so every file goes through here and what could not be written is reported at the end.
$locked = New-Object System.Collections.Generic.List[string]
function Stage($source, $destination) {
    foreach ($file in Get-ChildItem $source -File) {
        $target = Join-Path $destination $file.Name
        try { Copy-Item $file.FullName $target -Force }
        catch [System.IO.IOException] { [void]$locked.Add($target.Substring($root.Length + 1)) }
    }
}

# Staged from scratch so a file that stopped being produced does not linger in a release.
foreach ($folder in @((Join-Path $build "app"), (Join-Path $build "service"), $dist)) {
    if (Test-Path $folder) {
        try { Remove-Item $folder -Recurse -Force }
        catch [System.IO.IOException] { [void]$locked.Add($folder.Substring($root.Length + 1)) }
    }
}
New-Item -ItemType Directory -Force (Join-Path $build "app"), (Join-Path $build "service") | Out-Null

$shell = & $bin "BetterTerminal.Shell"
Stage (Join-Path $shell "*.exe") (Join-Path $build "app")
Stage (Join-Path $shell "*.dll") (Join-Path $build "app")
# The wrapper is its own project and nothing references it, so it has to be staged by hand.
Stage (Join-Path (& $bin "BetterTerminal.Wrap") "beterm-wrap.exe") (Join-Path $build "app")
Stage (Join-Path (& $bin "BetterTerminal.Service") "beterm-service.exe") (Join-Path $build "service")
Stage (Join-Path $shell "*.dll") (Join-Path $build "service")
Stage (Join-Path $shell "beterm-aiwizard.exe") (Join-Path $build "service")
Stage (Join-Path (& $bin "BetterTerminal.Bootstrap") "BetterTerminal.exe") $build

$readme = Join-Path $build "README.txt"
if (Test-Path $readme) {
    $text = Get-Content $readme -Raw
    $text = [regex]::Replace($text, 'BetterTerminal \d+\.\d+\.\d+ - final build', "BetterTerminal $short - final build")
    Set-Content $readme $text -Encoding UTF8 -NoNewline
}

# The release layout, identical to the one .github\workflows\build.yml assembles.
New-Item -ItemType Directory -Force (Join-Path $dist "app"), (Join-Path $dist "service") | Out-Null
# From the build output, not from BUILD: the copy there may be the one the user is running, and a
# release must never be cut from a launcher that could not be replaced.
Stage (Join-Path (& $bin "BetterTerminal.Bootstrap") "BetterTerminal.exe") $dist
Stage (Join-Path $build "app\*") (Join-Path $dist "app")
Stage (Join-Path $build "service\beterm-service.exe") (Join-Path $dist "service")
Copy-Item (Join-Path $root "README.md"), (Join-Path $root "LICENSE") $dist

if (-not $SkipZip) {
    $zip = Join-Path $build "BetterTerminal-x64.zip"
    if (Test-Path $zip) { Remove-Item $zip -Force }
    Compress-Archive -Path (Join-Path $dist "*") -DestinationPath $zip
}

$staged = Get-ChildItem $build -Recurse -File | Measure-Object -Property Length -Sum
"RESULT: BetterTerminal $short staged in BUILD - $($staged.Count) file(s), $([math]::Round($staged.Sum / 1MB, 2)) MB"
"  launcher: $((Get-Item (Join-Path $build 'BetterTerminal.exe')).VersionInfo.FileVersion)"
"  app:      $((Get-Item (Join-Path $build 'app\BetterTerminal.exe')).VersionInfo.FileVersion)"

if ($locked.Count -gt 0) {
    ""
    "WARNING: these were left at their old version because they are running right now."
    "Close BetterTerminal and run this script again to finish staging:"
    foreach ($file in ($locked | Sort-Object -Unique)) { "  $file" }
    exit 2
}
