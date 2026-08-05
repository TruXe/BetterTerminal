---
updated: 2026-08-05
scope: Runnable procedures for building, running, verifying, releasing and debugging BetterTerminal on this machine.
stability: evolving
sources: [tools/capture-window.ps1, tools/ui-smoke.ps1, tools/flood-benchmark.ps1, tools/session-cycle.ps1, BetterTerminal.Shell/BetterTerminal.Shell.csproj, BetterTerminal.sln, md-context-packet.md]
owner_agent: workflows-agent
---

# WORKFLOWS

Every command below is copy-pasteable PowerShell, run from the repository root `D:\Multi Terminál Window` (quoted paths, because the root is non-ASCII). Set `$msbuild` per shell:

```powershell
$msbuild = "C:\Program Files\Microsoft Visual Studio\18\Community\MSBuild\Current\Bin\MSBuild.exe"
```

## Setup

### First-time machine setup

**When:** a fresh clone or a fresh machine · **Takes:** 0 minutes if the toolchain is present · **Needs:** Windows 10 build 17763 or newer, Visual Studio 2026 Community with the .NET desktop workload

There is **no restore step and nothing to install** — unusual, and deliberate: the projects are classic (non-SDK-style) `.csproj` with zero NuGet packages, so `msbuild` alone builds them. Do not run `nuget restore`, `dotnet restore` or `dotnet build`; none of them apply.

1. `Test-Path "C:\Program Files\Microsoft Visual Studio\18\Community\MSBuild\Current\Bin\MSBuild.exe"` - confirms MSBuild Current is present; this exact path is the one used all session
2. `Test-Path "C:\Program Files (x86)\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.8"` - confirms the .NET Framework 4.8 reference assemblies, which `<TargetFrameworkVersion>v4.8</TargetFrameworkVersion>` needs
3. `[System.Environment]::OSVersion.Version` - the build number must be 17763 or higher for the pseudo-console path; below that the app silently uses the console-window fallback

**Verify:** all three checks pass and the Daily development build below finishes zero-error, zero-warning. Nothing else is required.

**If it fails:**
- MSBuild path not found - a different VS edition or location; find it with `Get-ChildItem "C:\Program Files\Microsoft Visual Studio" -Recurse -Filter MSBuild.exe -Depth 5` and use that path. Do not substitute `dotnet build`.
- Reference assemblies missing - install the ".NET Framework 4.8 targeting pack" component from the Visual Studio Installer's Individual Components tab; the build error names `v4.8` explicitly.

## Daily development

### Build Debug and run the app

**When:** every edit-run cycle · **Takes:** about 15 s incremental, 40 s clean · **Needs:** `$msbuild` set

1. `& $msbuild "BetterTerminal.sln" /p:Configuration=Debug /p:Platform=x64 /v:minimal` - builds all three projects; x64 is the only platform that exists
2. `Start-Process ".\BetterTerminal.Shell\bin\x64\Debug\BetterTerminal.exe"` - launches the app detached from this shell

**Verify:** the build prints no `warning` and no `error` lines, and the window opens with one tab holding one live shell that echoes typed input.

**If it fails:**
- The build succeeds but a newly added `.cs` file's type "does not exist" - classic csproj does not glob; see [Adding a feature](#adding-a-feature).
- The app opens with a stale pane tree, theme or font - the persisted workspace is being restored; see [Reset the persisted workspace and appearance](#reset-the-persisted-workspace-and-appearance).

### Open a folder as a project with the `beterm` command

**When:** working on a specific folder · **Takes:** seconds · **Needs:** the app to have been launched at least once since the build

Every start copies the executable and its two DLLs into `%LOCALAPPDATA%\BetterTerminal\app`
(refreshing whatever the running build is newer than), writes
`%LOCALAPPDATA%\BetterTerminal\bin\beterm.cmd` next to it and joins that folder to the per-user
search path. **`beterm` always runs the installed copy**, so the build output directory can be
deleted or moved without breaking the command - and the last build you launch is the one that gets
installed.

1. `Start-Process ".\BetterTerminal.Shell\bin\x64\Release\BetterTerminal.exe"` - one launch installs the copy and registers the command; close it again
2. `Get-ChildItem "$env:LOCALAPPDATA\BetterTerminal\app"` - three files, timestamped from the build you just ran
3. Open a **new** prompt (the search path is only re-read by processes started after the change) and type `beterm` in any folder

**Verify:** the window opens with one tab whose working directory is that folder, the status strip
shows the project name, and `<folder>\.beterm\project.json` exists as a hidden folder
(`Get-ChildItem <folder> -Force`). The workspace setup window appears on first open and afterwards
whenever "Show this setup every time the project opens" is left on.

**If it fails:**
- `beterm` is not recognised - the prompt was open before the first launch; open a new one. `Get-ItemProperty HKCU:\Environment -Name Path` shows whether the entry was written.
- The command returns instantly with no window, or Windows reports a path with a replacement character in it - the shim is holding an absolute path it cannot decode. It must read `%~dp0..\app\BetterTerminal.exe` and contain no byte above 127: check with `[System.IO.File]::ReadAllBytes("$env:LOCALAPPDATA\BetterTerminal\bin\beterm.cmd") | Where-Object { $_ -gt 127 }`, which must print nothing. See [TIPS #gotchas](TIPS.md#gotchas).
- An old build keeps opening - the installed copy could not be replaced because it was running. Close every BetterTerminal window and launch the build you want once more.

### Reset a project, the saved connections, or the command registration

**When:** starting a project's settings over, clearing the address book, or removing the command · **Takes:** seconds · **Needs:** the app not running

Three separate stores, so reset only the one you mean:

1. `Remove-Item "<project>\.beterm" -Recurse -Force` - discards that project's name, shell, startup line, commands and values; the next `beterm` there starts from defaults
2. `Remove-Item "$env:APPDATA\BetterTerminal\connections.json"` - discards every saved connection
3. `Remove-Item "$env:LOCALAPPDATA\BetterTerminal" -Recurse -Force` - removes the installed copy and the command itself; the next launch of any build installs and registers it again

**Verify:** `Get-ChildItem "<project>" -Force`, `Test-Path "$env:APPDATA\BetterTerminal\connections.json"` and `Test-Path "$env:LOCALAPPDATA\BetterTerminal\bin\beterm.cmd"` report what you expect.

**If it fails:**
- The search-path entry survives step 3 - it is a separate value; edit `HKCU\Environment\Path` by hand, or leave it: an entry pointing at an empty folder is harmless.

### Reset the persisted workspace and appearance

**When:** the app opens with a stale pane tree, wrong split ratios, panes you already closed, or an unexpected theme or font · **Takes:** 5 seconds · **Needs:** the app not running

`SessionStore` writes `%APPDATA%\BetterTerminal\workspace.json` on window close and restores it on start. It now holds appearance as well as layout: tabs, the split tree, split ratios, shell name and working directory, plus `theme`, `scheme`, `fontFamily`, `fontSize`, `cursorShape` and `blinkCursor`. Deleting the file therefore resets the **whole shell**, not just the pane tree.

1. `Get-Process BetterTerminal -ErrorAction SilentlyContinue | Stop-Process` - the file is rewritten on close, so the app must be down first
2. `Get-Content "$env:APPDATA\BetterTerminal\workspace.json"` - optional: inspect the persisted layout and appearance before discarding them
3. `Remove-Item "$env:APPDATA\BetterTerminal\workspace.json"` - discards both

**Verify:** relaunch the app; it opens with exactly one tab, one pane, and the default theme, scheme and font.

**If it fails:**
- The old state comes back - the app was still running at step 3 and rewrote the file on close. Stop it first, then delete.
- The path does not exist - the app has never been closed gracefully, so nothing was persisted; there is nothing to reset and the stale state has another cause.

## Testing

### Rebuild warning-clean, both configurations

**When:** before any commit-worthy state, and after every feature · **Takes:** about 2 minutes · **Needs:** `$msbuild` set

There is **no test project and no unit tests**. `vstest.console.exe` exists at `C:\Program Files\Microsoft Visual Studio\18\Community\Common7\IDE\CommonExtensions\Microsoft\TestWindow\vstest.console.exe` but has nothing to run — do not invoke it. The real gate is these rebuilds plus the script-driven checks below.

1. `& $msbuild "BetterTerminal.sln" /t:Rebuild /p:Configuration=Debug /p:Platform=x64 /v:minimal` - full clean rebuild of Debug
2. `& $msbuild "BetterTerminal.sln" /t:Rebuild /p:Configuration=Release /p:Platform=x64 /v:minimal` - full clean rebuild of Release

**Verify:** both end in `0 Error(s)` and `0 Warning(s)`. Warning-clean is the pass condition, not just error-free; it is currently met in both configurations.

**If it fails:**
- `CS0067` on an unused event - an event is declared but never raised; the codebase solves this with explicit empty add/remove accessors, as `HwndConsoleSession.OutputReceived` does.
- A C# 8+ construct is rejected - `<LangVersion>7.3</LangVersion>` is pinned; rewrite without nullable reference types, switch expressions or using declarations.

### UI-automation smoke sequences

**When:** after any change to pane, tab, split or session teardown code · **Takes:** about 1 minute per sequence · **Needs:** a built exe, `tools\ui-smoke.ps1`

`tools\ui-smoke.ps1` starts the app, invokes buttons through UI Automation (no foreground focus
needed), then reports `RESULT: process alive` or the crash exit code. Parameters: `-Exe` (required),
`-Log` (required), `-Sequence` (default `New tab|Close pane`), `-StepDelaySeconds` (default 3).
Sequence steps are **`AutomationProperties.Name` values, not visible labels**; the current set is
`New tab`, `Split pane right`, `Split pane down`, `Close the focused pane`,
`Open the command palette`, `Settings`, `Minimize`, `Maximize or restore`, `Close window`,
`Saved connections`, `Workspace setup`. Note that invoking a button that opens a **modal** window
(`Saved connections`, `Workspace setup`, `Settings`) blocks the automation call until that window
closes, and that those windows are **nested under the main window** in the automation tree rather
than being root children - see [TIPS #gotchas](TIPS.md#gotchas). The script
matches on name **and** `ControlType.Button`, because a label inside a button exposes the same
automation name and would otherwise match first, yielding an element with no Invoke pattern. These
two sequences ran and passed against the Release build on 2026-08-04:

1. `.\tools\ui-smoke.ps1 -Exe ".\BetterTerminal.Shell\bin\x64\Release\BetterTerminal.exe" -Log ".\smoke1.log" -Sequence "Split pane right|Split pane down|New tab|Close the focused pane|Close the focused pane|Close the focused pane"` - splits and tabs interleaved, then torn down leaf by leaf; this is the teardown-crash regression
2. `.\tools\ui-smoke.ps1 -Exe ".\BetterTerminal.Shell\bin\x64\Release\BetterTerminal.exe" -Log ".\smoke2.log" -Sequence "Open the command palette"` - the palette overlay renders its 12 entries

**Verify:** both logs end with `RESULT: process alive`, and every step logs `invoked: <name>` rather
than `button not found`. A crash shows up as a non-zero exit code on the `RESULT:` line plus a
matching entry in the Windows Application event log — read it with [Pull a crash stack out of the
event log](#debugging). The palette's 12 entries are counted from a capture, not by the script.

**If it fails:**
- `button not found: <name>` - the step does not match any element's `AutomationProperties.Name`, or the match is not a Button. Use the exact automation name from the list above, not the visible label.
- `RESULT: process CRASHED or exited` with code 0xE0434352 - an unhandled managed exception, historically a background IO thread on `ConPtySession`. Pull the stack from the event log first.

> ❓ Unverified: the script's `-Sequence` default is still the old `New tab|Close pane`, which no
> longer matches any button. Always pass `-Sequence` explicitly; the default was not re-run.

### Run a verification script from the console front end

**When:** running any of the four scripts without remembering its parameters · **Takes:** as long as the script · **Needs:** a built `beterm-wrap.exe` and a real console

`BetterTerminal.Wrap` lists the scripts under `tools`, prompts for each declared parameter, streams
the output with scrollback and shows the exit code the script really returned. It never edits a
script; it only starts one.

1. `& $msbuild "BetterTerminal.sln" /p:Configuration=Release /p:Platform=x64 /v:minimal` - builds it with everything else
2. `.\BetterTerminal.Wrap\bin\x64\Release\beterm-wrap.exe` - run it from a console window; pass the tools folder as the first argument when running it from somewhere else
3. Up and Down pick a script, Enter opens its parameters, Up and Down move between fields, Enter runs, Ctrl+C stops a run, Q quits from the list

**Verify:** the exit code in the title bar matches what the script returns when run by hand. `flood-benchmark.ps1` and `session-cycle.ps1` are marked `[own console]`: the interface disappears while they run and comes back afterwards, because those two start a shell that must not inherit a pipe (see [TIPS #gotchas](TIPS.md#gotchas)).

**If it fails:**
- "This program draws on a console and cannot run with its input or output redirected" - it was started from a pipeline; run it in a console window of its own.
- The list is empty, or "No tools folder found" - it was started from a copy with no `tools` folder above it; pass that folder's path as the first argument.
- A parameter it prompts for is not one the script has - `ScriptCatalog.cs` transcribes each script's `param` block and has fallen behind; fix it there.

### Throughput benchmark

**When:** after touching the reader thread, the UTF-8 decoder, `VtParser` or `CellGrid` · **Takes:** about 30 s after the first run · **Needs:** a Release build of `BetterTerminal.Terminal`

`tools\flood-benchmark.ps1` pipes a generated payload through a real pseudo-console session with no
UI attached. Parameters: `-Bin` (required, the directory holding the built DLLs), `-Work` (required,
a scratch directory for `flood.txt`), `-Log` (required), `-Lines` (default 87000). It loads
`BetterTerminal.Interop.dll` and `BetterTerminal.Terminal.dll` with `Add-Type`, so `-Bin` must be a
build output directory, not the repository root.

**It must not run in a process with redirected standard handles.** With redirected std handles the child shell writes into those handles instead of the pseudo console and the grid stays empty — which looks exactly like a broken implementation but is not. Launch it only this way:

1. `& $msbuild "BetterTerminal.sln" /p:Configuration=Release /p:Platform=x64 /v:minimal` - the benchmark measures Release
2. ```powershell
   Start-Process powershell -WindowStyle Hidden -Wait -ArgumentList @(
     "-NoProfile","-File",".\tools\flood-benchmark.ps1",
     "-Bin",".\BetterTerminal.Terminal\bin\x64\Release",
     "-Work","$env:TEMP\bt-flood",
     "-Log",".\flood.log")
   ``` - runs the flood with clean std handles
3. `Get-Content .\flood.log` - reads the result out of the log file

**Verify:** the log reports roughly `input MB: 10.12`, `exit: 0`, `scrollback lines: 5000` and a throughput in the measured band — Release 1.45 to 1.64 MB/s (6.15 s to 6.97 s), Debug about 1.03 MB/s (9.86 s). The last content line must be intact, not truncated.

**If it fails:**
- `scrollback lines: 0` and an empty last line - the script ran with redirected std handles. Re-run through `Start-Process` exactly as in step 2.
- `Add-Type : Could not load file or assembly` - `-Bin` points at a directory without the two DLLs; it must be a `bin\x64\<Config>` directory of `BetterTerminal.Terminal`.

### Orphan-process check

**When:** after touching `ProcessJob`, session teardown, or handle disposal · **Takes:** about 1 minute · **Needs:** a Release build of `BetterTerminal.Terminal`

`tools\session-cycle.ps1` opens and closes 20 cycles of 4 concurrent sessions (2 cmd, 2 PowerShell) and reports the console-hosting process count before and after, so orphans show up as a difference. Parameters: `-Bin` (required), `-Log` (required), `-Cycles` (default 20), `-SessionsPerCycle` (default 4). Same redirected-std-handle rule as the benchmark.

1. ```powershell
   Start-Process powershell -WindowStyle Hidden -Wait -ArgumentList @(
     "-NoProfile","-File",".\tools\session-cycle.ps1",
     "-Bin",".\BetterTerminal.Terminal\bin\x64\Release",
     "-Log",".\cycle.log")
   ``` - runs 80 session open/close pairs
2. `Get-Content .\cycle.log` - reads the before/after counts

**Verify:** the two counts are equal (11 before, 11 after on 2026-08-04). Equality means the job object with `KILL_ON_JOB_CLOSE` cleaned up every shell and console host.

**If it fails:**
- The after count is higher - a session started without being assigned to its job object, or `Close()`/`Dispose()` was skipped on an error path. Find survivors with `Get-Process conhost, cmd, powershell | Select-Object Id, StartTime` and kill them before re-running.
- Both counts higher than expected - the script counts all `conhost`, `cmd` and `powershell` processes machine-wide, including unrelated ones; only the difference matters.

## Release

### Build the Release binaries

**When:** producing something to hand to another machine · **Takes:** about 1 minute · **Needs:** a passing Testing pass

There is **no installer, no code signing and no publish step yet**. A release is the contents of the Release output directory, copied by hand.

1. `& $msbuild "BetterTerminal.sln" /t:Rebuild /p:Configuration=Release /p:Platform=x64 /v:minimal` - clean Release rebuild
2. `Get-ChildItem ".\BetterTerminal.Shell\bin\x64\Release" -File | Select-Object Name, Length` - lists what ships: `BetterTerminal.exe`, `BetterTerminal.Interop.dll`, `BetterTerminal.Terminal.dll`
3. `Start-Process ".\BetterTerminal.Shell\bin\x64\Release\BetterTerminal.exe"` - smoke-launch the exact binary being shipped

**Verify:** zero errors and zero warnings, and the launched app opens a live shell. The target machine needs .NET Framework 4.8 and x64; there is no self-contained option.

**If it fails:**
- Runs here but not on the target - the target lacks .NET Framework 4.8 or is 32-bit; x64 by design.
- Only `BetterTerminal.exe` was copied - both class-library DLLs must travel with it; there is no merged or single-file output.

### Publish a release

**When:** shipping a build to the releases page · **Takes:** a few minutes of CI · **Needs:** a clean working tree on `main`, pushed

The tag is what publishes. `.github/workflows/build.yml` rebuilds both configurations with
`/warnaserror`, zips the Release output together with the README and the licence, and creates the
release with that zip using the token the run is given - no token is stored anywhere in the
repository.

1. `git tag -a v1.2.3 -m "BetterTerminal 1.2.3"` - annotated, `v` prefix; the workflow only publishes for `refs/tags/v*`
2. `git push origin v1.2.3` - this is what starts the run
3. Watch the run on the repository's Actions tab; the release appears when it finishes

**Verify:** the release lists `BetterTerminal-x64.zip`, and the zip holds `BetterTerminal.exe`,
`beterm-banner.exe`, `beterm-wrap.exe`, the two DLLs, `README.md` and `LICENSE`.

**If it fails:**
- The run fails in a build step - a warning was introduced; the workflow enforces the same zero-warning rule as [Rebuild warning-clean](#testing).
- The run is not listed at all - Actions is disabled for the repository, or the tag was pushed before the workflow file was on `main`.
- You need the same zip locally: build Release, then copy `BetterTerminal.Shell\bin\x64\Release\*.exe` and `*.dll`, `BetterTerminal.Wrap\bin\x64\Release\beterm-wrap.exe`, `README.md` and `LICENSE` into one folder and `Compress-Archive` it. That is exactly what the workflow does.

## Debugging

### Capture the window without stealing focus

**When:** you need to see what the UI rendered while the machine is in use · **Takes:** seconds · **Needs:** the app running

`tools\capture-window.ps1` uses `PrintWindow` with flag 2 (`PW_RENDERFULLCONTENT`), which renders a
composited WPF window even when it is occluded, so it never takes foreground focus. Parameters:
`-ProcessName` (default `BetterTerminal`), `-Out` (required).

1. `.\tools\capture-window.ps1 -ProcessName BetterTerminal -Out "$env:TEMP\bt.png"` - writes the PNG and prints its path and pixel size
2. `Invoke-Item "$env:TEMP\bt.png"` - open it

**Verify:** the script prints `<path> <width>x<height>` and the PNG shows the real pane contents, not a black or transparent rectangle.

**If it fails:**
- `Cannot find a process with the name` - the app is not running, or runs under another name; check with `Get-Process | Where-Object Name -like 'Better*'`.
- The image is black - the window is minimized, so it has no client area to render. Restore it; this is the one case `PrintWindow` cannot help with.

### Confirm a theme or scheme reached the live terminal

**When:** after changing a theme, colour scheme or any `Theme.xaml` token, to prove the change reached the running UI without a human looking · **Takes:** under a minute · **Needs:** the app running with the theme applied

Chrome and terminal body are painted by different code paths, so sample one pixel of each out of a
capture. This is how the appearance wiring was verified on 2026-08-04.

1. `.\tools\capture-window.ps1 -ProcessName BetterTerminal -Out "$env:TEMP\bt.png"` - capture without stealing focus
2. ```powershell
   Add-Type -AssemblyName System.Drawing
   $bmp = New-Object System.Drawing.Bitmap "$env:TEMP\bt.png"
   $x = [math]::Floor($bmp.Width / 2)
   $y = [math]::Floor($bmp.Height * 0.75)
   "chrome:   " + $bmp.GetPixel([int]$x, 8).Name
   "terminal: " + $bmp.GetPixel([int]$x, [int]$y).Name
   $bmp.Dispose()
   ``` - samples the title-bar chrome and a point well inside the terminal body

**Verify:** the printed ARGB names end in the expected hex. Light chrome reads `FBFBFC` at the top of
the window; the Solarized Dark terminal body reads `002B36`. `GetPixel(...).Name` returns eight hex
digits, so the alpha prefix `ff` precedes each value.

**If it fails:**
- Both samples read the same colour - the sampled row landed in the wrong region; the window may be small or a pane may be maximized. Open the PNG and pick coordinates by eye instead of the fractions above.
- The terminal sample matches the old scheme - the scheme changed in settings but was not pushed to live sessions, or the capture predates the change. Re-capture, and if it persists the scheme is only being applied to newly created panes.

### Identify the live backend from the child process

**When:** output looks wrong, or you need to know which of the two backends is actually running · **Takes:** seconds · **Needs:** the app running with at least one pane

1. `$app = Get-Process BetterTerminal | Select-Object -First 1` - grab the app's PID
2. `Get-CimInstance Win32_Process -Filter "ParentProcessId = $($app.Id)" | Select-Object ProcessId, Name, CommandLine | Format-List` - every process the app spawned, with the full command line

**Verify:** the command line tells the backends apart. Pseudo-console path: a headless console host
launched with `--width` / `--height` / `--signal` / `--server` arguments (the default on build 17763
and newer). Fallback path: a console host launched with the shell executable as its argument, for
example `conhost.exe "C:\Windows\system32\cmd.exe"`, plus a console window reparented into the pane.
The fallback appearing on a 17763+ machine means `ConPtySession.IsSupported` returned false — that
check uses `RtlGetVersion` from ntdll precisely because .NET Framework reports 6.2 for an
unmanifested app.

**If it fails:**
- The query returns nothing - no pane has a live session yet, or every session already exited; open a
  new tab and re-run.
- `CommandLine` is empty - the query is running without the rights to read another session's command
  line; run the same command from an elevated PowerShell.

### Pull a crash stack out of the event log

**When:** the app disappeared, or a smoke run reported a crash exit code · **Takes:** under a minute · **Needs:** nothing; the log is written by Windows

An unhandled managed exception exits with `0xE0434352` and writes the full .NET stack to the Windows
Application log. This is how the `System.ArgumentNullException` from `SemaphoreSlim.Wait` inside
`BlockingCollection.GetConsumingEnumerable` on the `ConPtySession` writer thread was found.

1. `Get-WinEvent -LogName Application -MaxEvents 300 | Where-Object { $_.Message -match 'BetterTerminal' } | Select-Object TimeCreated, Id, ProviderName -First 10` - lists recent events mentioning the app
2. `Get-WinEvent -LogName Application -MaxEvents 300 | Where-Object { $_.Message -match 'BetterTerminal' } | Select-Object -First 1 -ExpandProperty Message` - prints the full message of the newest one, including the exception type and stack

**Verify:** the message names an exception type and a stack frame in `BetterTerminal.*`. Confirm the
`TimeCreated` matches the run you are investigating — old crashes stay in the log.

**If it fails:**
- Nothing matches - no managed crash occurred; the process may have been killed (`ui-smoke.ps1` calls
  `$app.Kill()` on success, which writes no event). Widen with `-MaxEvents 2000` or filter on
  `ProviderName '.NET Runtime'` instead.
- `Get-WinEvent : No events were found` - the Application log was cleared; retry without `-MaxEvents`.

> ❓ Unverified: the exact `Get-WinEvent` filter used during the original diagnosis was not recorded;
> the two forms above are standard cmdlet usage, not confirmed against a live crash.

## Adding a feature

### The edit loop for a new source file

**When:** any change that introduces a new `.cs` file · **Takes:** 5 minutes plus the smoke pass · **Needs:** a warning-clean starting build

The trap here is real and silent: classic `.csproj` files **do not glob**. A `.cs` file that is not
listed as an explicit `<Compile Include="..." />` item simply never compiles, with no warning — the
build succeeds and your type "does not exist".

1. Write the new file next to its peers, e.g. `BetterTerminal.Shell\MyFeature.cs` - place it by project, per [STRUCTURE #where-to-add-things](STRUCTURE.md#where-to-add-things)
2. Add `<Compile Include="MyFeature.cs" />` to the `<ItemGroup>` in `BetterTerminal.Shell\BetterTerminal.Shell.csproj` that already lists `SplitPane.cs` and `SessionStore.cs` - a XAML-backed file needs the `<DependentUpon>` form used by `MainWindow.xaml.cs`
3. `& $msbuild "BetterTerminal.sln" /p:Configuration=Debug /p:Platform=x64 /v:minimal` - fast check that the file actually compiles
4. `& $msbuild "BetterTerminal.sln" /t:Rebuild /p:Configuration=Release /p:Platform=x64 /v:minimal` - the warning-clean gate
5. Re-run all five sequences from [UI-automation smoke sequences](#testing) - required whenever pane, tab, split or teardown code changed

**Verify:** `Select-String -Path ".\BetterTerminal.Shell\BetterTerminal.Shell.csproj" -Pattern "MyFeature"` returns a hit, both builds are zero-error zero-warning, and all five smoke logs say `RESULT: process alive`.

**If it fails:**
- `The type or namespace name 'MyFeature' could not be found` while the file clearly exists - step 2
  was skipped or the `Include` path is wrong; it is relative to the `.csproj` directory.
- A smoke sequence that passed before now crashes - the change touched teardown. The load-bearing
  order is `CompleteAdding()` → close the pseudo console and job → `Join()` both IO threads with a
  2 s timeout → only then dispose streams, handles and the queue. See [RULES #hard-rules](RULES.md#hard-rules).

---
[← CLAUDE.md](CLAUDE.md) · [STRUCTURE](STRUCTURE.md) · [WORKFLOWS](WORKFLOWS.md) · [MEMORY](MEMORY.md)
