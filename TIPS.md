---
updated: 2026-08-05
scope: Non-obvious, BetterTerminal-specific traps, measured performance facts, diagnosis techniques and machine quirks.
stability: evolving
sources: [BetterTerminal.Terminal/ConPtySession.cs, BetterTerminal.Terminal/HwndConsoleSession.cs, BetterTerminal.Terminal/ConsoleHwndHost.cs, BetterTerminal.Terminal/VtKeyEncoder.cs, BetterTerminal.Terminal/TerminalRenderer.cs, BetterTerminal.Terminal/TerminalSessionFactory.cs, BetterTerminal.Terminal/BetterTerminal.Terminal.csproj, BetterTerminal.Shell/ViewModels/SplitViewModel.cs, BetterTerminal.Shell/Views/TerminalSurface.cs, BetterTerminal.Shell/Themes/Controls.xaml, BetterTerminal.Shell/Views/MainWindow.xaml, BetterTerminal.Shell/Views/SplashWindow.xaml, BetterTerminal.Shell/Views/SettingsWindow.xaml, BetterTerminal.Shell/Services/TerminalWorkspace.cs, BetterTerminal.Shell/ViewModels/SampleData.cs, tools/capture-window.ps1, tools/ui-smoke.ps1, tools/flood-benchmark.ps1, tools/session-cycle.ps1]
owner_agent: tips-agent
---

# TIPS

Knowledge that cost time to learn here. Everything below is specific to this codebase, this OS
surface or this machine. Rules live in [RULES.md](RULES.md#hard-rules); this file explains why some
of them exist.

## Gotchas

**The app silently runs the wrong backend when you trust `Environment.OSVersion`.**
Symptom: a modern Windows 11 machine gets the reparented console window instead of the pseudo
console, with no error anywhere. Cause: .NET Framework reports 6.2 for a process without an OS
compatibility manifest, so the build-number check fails. Fix: `ConPtySession.IsSupported` calls
`RtlGetVersion` from ntdll and compares `BuildNumber >= 17763`; never reintroduce
`Environment.OSVersion` into that decision. The `app.manifest` was added for PerMonitorV2 DPI, but
the ntdll call is what the backend check trusts. [MEMORY.md#decision-log](MEMORY.md#decision-log)

**A plain `CREATE_NEW_CONSOLE` gives the fallback backend nothing to reparent.**
Symptom: the window search by `ConsoleWindowClass` times out after 5 s and the pane never fills.
Cause: on Windows 11 a new console can be handed to the default terminal application, so no classic
console window is ever created. Fix: `HwndConsoleSession` starts `System32\conhost.exe` explicitly
with the shell as its command line, then finds the window by PID with `EnumWindows`.

**`SetParent` returning zero is success, not failure.**
Symptom: a plausible-looking error check on the return value fires every single time. Cause:
`SetParent` returns the *previous* parent, which is NULL for a top-level console window. Fix:
`ConsoleHwndHost.AttachConsoleWindow` deliberately ignores the return value; use `SetLastError`
semantics if you ever need real failure detection there.

**Never restructure the pane tree by moving FrameworkElements.**
Symptom: `InvalidOperationException` about an element already having a logical parent when
splitting or closing a pane. Cause: WPF refuses to adopt an element that still has one, so any
hand-rolled reparenting has to detach before it attaches. Fix: the tree is a view-model tree —
`SplitViewModel` with `First`, `Second`, `FirstRatio` and a `Replace(oldChild, newChild)`, realised
by `ColumnSplitViewModel` / `RowSplitViewModel` and rendered through templates. Swap view models,
never visuals; the old element-level `SplitPane` and its detach-before-attach dance are gone.
[MEMORY.md#decision-log](MEMORY.md#decision-log)

**A harness process with redirected stdout makes the pseudo console look broken.**
Symptom: a session starts, the shell runs, and the cell grid stays completely empty. Cause: the
child inherits the harness's redirected standard handles and writes into them instead of into the
pty. Fix: launch `tools\flood-benchmark.ps1` and `tools\session-cycle.ps1` via
`Start-Process powershell -WindowStyle Hidden -Wait` and read results from the log file. The
shipped GUI app has no std handles, so the product itself is never affected — do not "fix" the
backend over this.

**Never kill terminal processes by image name in this project.**
Symptom: killing stray `cmd.exe` / `powershell.exe` / `conhost.exe` after a test run takes down the
user's own shells, or the running app. Cause: the product hosts processes with exactly those names,
indistinguishable from anything else on the box. Fix: kill by PID, obtained from the app's process
tree. Orphan checks in `tools\session-cycle.ps1` count hosts before and after rather than sweeping
by name.

**Classic csproj does not glob source files.**
Symptom: a new `.cs` file compiles nowhere, its type is "not found", and the build still reports
zero errors for the file itself. Cause: the projects are non-SDK-style; only explicit
`<Compile Include="..."/>` items are built (18 of them in `BetterTerminal.Terminal.csproj`). Fix:
add the `Compile` item by hand whenever you add a file — see
[STRUCTURE.md#where-to-add-things](STRUCTURE.md#where-to-add-things).

**Ctrl+Shift+letter never reaches the shell process, by design.**
Symptom: a terminal application that wants Ctrl+Shift+C or Ctrl+Shift+V does not receive it. Cause:
`VtKeyEncoder.EncodeControl` returns `null` for `Ctrl+Shift+A..Z` so the combination bubbles to the
WPF UI (copy, paste, panes, tabs, command palette). Fix: this is intentional; changing it steals the
shell UI's entire shortcut namespace.

**Colour tokens are `Color`, not `Brush`.**
Symptom: `Background="{StaticResource Bt.Color.Neutral.950}"` compiles cleanly and then throws at
XAML load time. Cause: WPF will not convert a `Color` to a `Brush` in a resource reference, and the
design-system tokens are `Color` values. Fix: wrap the token explicitly —
`<Border.Background><SolidColorBrush Color="{StaticResource Bt.Color.Neutral.950}"/></Border.Background>`.
The pattern is in `Views\SettingsWindow.xaml` (theme cards, 4 uses) and `Views\SplashWindow.xaml`
(9 uses).

**Never use `x:Name="Cursor"` on a FrameworkElement.**
Symptom: warning CS0108 in the generated `.g.cs`, which fails the zero-warning build rule. Cause:
`FrameworkElement` already declares a `Cursor` property, so the generated field hides it. Fix: pick
another name — the splash caret is `x:Name="CaretBlock"` in `Views\SplashWindow.xaml` for exactly
this reason.

**`DisplayMemberPath` does nothing on a `Bt.ComboBox`.**
Symptom: the drop-down list looks right but the closed selection box shows the view model's type
name. Cause: the `Bt.ComboBox` template in `Themes\Controls.xaml` renders `SelectionBoxItem` through
a plain `ContentPresenter`, which ignores `DisplayMemberPath` and falls back to `ToString()`. Fix:
give the ComboBox an explicit `ItemTemplate`, as the profile picker in `Views\MainWindow.xaml` does.

**An exception escaping an IO thread kills every pane in the app, not just its own.**
Symptom: the whole process disappears with exit code `0xE0434352` when a second pane is closed.
Cause: the 2026-08-04 crash was an unhandled `ArgumentNullException` from `SemaphoreSlim.Wait`
inside `BlockingCollection.GetConsumingEnumerable`, because `Dispose()` disposed the queue under a
blocked writer thread. Fix: both IO threads catch broadly and convert failures into an `Exited`
event; teardown is `CompleteAdding()` → close pseudo console and job → `Join()` both threads with a
2 s timeout → only then dispose streams, handles and queue, leaving anything that did not stop in
time to its finalizer. [MEMORY.md#decision-log](MEMORY.md#decision-log)

**`Write` and `Resize` must return early once disposed.**
Symptom: `ObjectDisposedException` on the UI thread while a pane is closing. Cause:
`ResizePseudoConsole` on an already-closed `SafeHandle` throws. Fix: both methods check the
`_disposed` flag first; keep that check when editing them.

**Never write an absolute path into a generated `.cmd`.**
Symptom: `beterm` returns instantly with no window, or Windows shows *"cannot find
D:\Multi Termin?l Window\...\BetterTerminal.exe"* with a replacement character where the `á` should
be. Cause: a script file is bytes, and the command interpreter decodes it in **whatever code page
the console is using at the time** - the OEM page by default, but 65001 after a `chcp`, and neither
matches the ANSI page that `Encoding.Default` means. Any non-ASCII character in the path is a
coin flip. Writing the file in the OEM page fixes only the default case. Fix, and the reason
`Services\SelfInstall.cs` exists: the application copies itself to
`%LOCALAPPDATA%\BetterTerminal\app` and the shim reaches it as `%~dp0..\app\BetterTerminal.exe`, so
the script is **pure ASCII no matter what the build folder or the user profile is called**. Assert
that if you touch it: `[System.IO.File]::ReadAllBytes($shim) | Where-Object { $_ -gt 127 }` must
return nothing. Same family as the two encoding traps under
[environment quirks](#environment-quirks).

**A modal dialog opened from `Restore()` leaves the splash screen on top of it.**
Symptom: the workspace setup appears with "Starting BetterTerminal" still floating over the main
window. Cause: `Restore()` runs inside `MainWindow`'s `Loaded` handler and the splash is closed by a
*later* handler on the same event, so a blocking `ShowDialog()` there stops the window from
finishing its load. Fix: `TerminalWorkspace.RestoreProject` posts the dialog with
`Dispatcher.BeginInvoke(..., DispatcherPriority.Background)` instead of calling it inline.

**An owned window is not a root child in the automation tree.**
Symptom: a UI Automation script that looks for the settings, connections or setup window among
`RootElement`'s children finds only the main window. Cause: those windows set `Owner`, so they are
nested **under the owner** in the automation tree even though they are separate top-level windows.
Fix: search `TreeScope::Descendants` from the main window. Related: a `ListBoxItem` with no
`AutomationProperties.Name` announces its view-model type name - `Views\ConnectionsWindow.xaml`
binds the name to the row's `Display` for that reason.

**Every mutating `CellGrid` member requires the caller to hold `SyncRoot`.**
Symptom: torn rows, index exceptions during a resize, or a renderer reading half-written cells.
Cause: the parser runs on the reader thread while the renderer reads on the UI thread. Fix: take
`SyncRoot` around any grid mutation or multi-field read; the grid does not lock internally for you.

## Performance

**Flood throughput, measured 2026-08-04 on a 10.12 MB payload** end to end through the pipe, UTF-8
decode, VT parse and grid: Debug 9.86 s = 1.03 MB/s; Release measured twice, 6.97 s = 1.45 MB/s and
6.15 s = 1.64 MB/s. Quote Release as roughly 1.45–1.64 MB/s and expect run-to-run variation. The
pseudo console itself throttles output, so this is not pure parser throughput — do not use the
number as a parser benchmark.

**Redraw is per-row, not per-frame.** `TerminalRenderer` keeps a `_renderedVersions` array and only
repaints rows whose per-line version stamp changed (plus the cursor row). Cause of any "the whole
screen redraws" regression is almost always a `_fullRedraw` left set or the version array being
rebuilt. Fix: preserve the stamp comparison in `RenderFrame`.

**The 16 ms frame timer does nothing when there is no output.** `FrameInterval` is 16 ms and
`CaretBlinkInterval` is 530 ms; the tick exits immediately unless the reader thread flagged new
output or a full redraw is pending. Adding unconditional work to that tick costs battery on an idle
window with several panes.

**Input is queued, output is not marshalled.** Keystrokes go onto a `BlockingCollection<byte[]>`
drained by a writer thread so the UI never blocks on a full pipe; parsing happens on the reader
thread so nothing is dispatched per byte. Per-byte marshalling to the UI thread was rejected as the
classic way to make a terminal drop input under load.
[MEMORY.md#decision-log](MEMORY.md#decision-log)

**Scrollback is capped at 5000 lines per session** (`TerminalSessionFactory.DefaultScrollbackLines`)
and is not user-configurable. A flood fills the cap; memory does not grow past it.

## Debugging tricks

**Read the managed stack out of the Windows event log, not out of the app.** Symptom: the process
vanishes before any handler or logger runs. Fix: `Get-WinEvent` over the Application log filtered on
`BetterTerminal` yields the full .NET exception type, message and stack. This is exactly how the
pane-close crash was identified. [MEMORY.md#decision-log](MEMORY.md#decision-log)

**Exit code `0xE0434352` means "unhandled managed exception"** — nothing more specific. Treat it as
a pointer to the event log, not as a diagnosis.

**Identify the live backend from the child process command line.** Use `Get-CimInstance
Win32_Process` filtered by `ParentProcessId` of `BetterTerminal.exe`. A pseudo-console session shows
a headless console host carrying `--width`, `--height`, `--signal` and `--server` arguments; the
fallback shows a console host launched with the shell as its argument. Faster and more reliable
than reasoning about the OS build.

**Screenshot the window without stealing focus.** `tools\capture-window.ps1 -ProcessName
BetterTerminal -Out <file.png>` calls `PrintWindow` with flag 2 (PW_RENDERFULLCONTENT), which is
required for a composited WPF window and works while the window is occluded or in the background.

**Drive the UI with no foreground focus.** `tools\ui-smoke.ps1 -Exe <exe> -Log <file> -Sequence
"Split pane right|Close the focused pane"` invokes buttons through UI Automation. Steps are
`AutomationProperties.Name` values, not visible labels: `New tab`, `Split pane right`,
`Split pane down`, `Close the focused pane`, `Open the command palette`, `Settings`, `Minimize`,
`Maximize or restore`, `Close window`. The search is an `AndCondition` on name *and*
`ControlType.Button`, because a label inside a button exposes the same automation name and matching
it first returns an element with no Invoke pattern. It reports `RESULT: process alive` or the crash
exit code — this is the regression harness for the teardown bug. See
[WORKFLOWS.md#testing](WORKFLOWS.md#testing).

**Orphan hunting.** `tools\session-cycle.ps1 -Bin <...\bin\x64\Release> -Log <file>` runs 20 cycles
of 4 concurrent sessions and prints the console-host process count before and after; a difference is
a leaked job object or a missed `Dispose`. Baseline observed: 11 before, 11 after.

## Environment quirks

**The repository path contains a non-ASCII character (`D:\Multi Terminál Window`).** Symptom: a
PowerShell script fails with "path does not exist" for a path that plainly exists. Cause: PowerShell
5.1 reads a `-File` script as ANSI unless the file carries a UTF-8 BOM, mangling the `á`. Fix: never
hardcode the repository path in a script — every script under `tools\` takes it as a parameter.

**A source file with non-ASCII and no UTF-8 BOM is compiled in the machine ANSI codepage.**
Symptom: Czech strings and Segoe icon glyphs come out mangled in the built UI, with no build error.
Cause: csc and the XAML compiler fall back to the ANSI codepage when a `.cs` or `.xaml` file carries
no BOM. Fix: two defences are in place — private-use icon glyphs are written as `\uE710`-style
escapes in C# (see `Services\TerminalWorkspace.cs`), and the one file that genuinely needs literal
non-ASCII, `ViewModels\SampleData.cs`, carries a BOM (it is the only source file in the Shell
project that does). Same family of bug as the PowerShell `-File` trap above.

**Windows 10 build 17763 is the floor for the pseudo-console path.** Below it,
`TerminalSessionFactory.Resolve` returns `HostedConsoleWindow` automatically and `Write(string)`
throws `NotSupportedException` — there is no supported way to inject input into another process's
console. Typing doing nothing on an old OS is expected behaviour, not a bug.

**x64 only, and not by preference.** `SetWindowLongPtrW` and `GetWindowLongPtrW` are 64-bit-only
exports, and the interop structs use `Pack` values chosen for x64 layout. There is no x86 and no
AnyCPU configuration; adding one would silently break the interop layer.

**Toolchain on the development machine:** Visual Studio Community 2026, MSBuild `Current`, host
Windows 11 build 22631. No NuGet packages, so `msbuild` builds the solution with no restore step;
both `Debug|x64` and `Release|x64` must finish with zero errors and zero warnings.

**`vstest.console.exe` exists on this machine but there is no test project**, so a green test run
means nothing here. Verification is script-driven only — see
[WORKFLOWS.md#testing](WORKFLOWS.md#testing).

---
[← CLAUDE.md](CLAUDE.md) · [STRUCTURE](STRUCTURE.md) · [WORKFLOWS](WORKFLOWS.md) · [MEMORY](MEMORY.md)
