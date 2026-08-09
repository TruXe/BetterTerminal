---
updated: 2026-08-05
scope: Binding constraints for BetterTerminal - stack, interop, teardown, UI text, git and workflow
stability: stable
sources: [BetterTerminal.Interop/BetterTerminal.Interop.csproj, BetterTerminal.Terminal/BetterTerminal.Terminal.csproj, BetterTerminal.Shell/BetterTerminal.Shell.csproj, BetterTerminal.sln, .gitignore, BetterTerminal.Shell/app.manifest, BetterTerminal.Terminal/ProcessJob.cs, BetterTerminal.Terminal/ConPtySession.cs, BetterTerminal.Terminal/CellGrid.cs, BetterTerminal.Interop/, docs/_archive/2026-08-04/RULES.md]
owner_agent: rules-agent
---

# RULES

Constraints that are real for this repository. Each rule says how it is enforced. `[enforced]` means a
config, compiler or tool rejects the violation. `[convention]` means only a human or an agent catches
it - break those only with a reason written into [MEMORY.md#decision-log](MEMORY.md#decision-log).

## Hard rules

**R1 [enforced] Target .NET Framework 4.8, C# 7.3, WPF, x64 only.**
Why: chosen on 2026-08-04 because `SetWindowLongPtr` and IntPtr-sized struct fields differ on x86, and
4.8 was the newest installed reference assembly set. Enforced by
`<TargetFrameworkVersion>v4.8</TargetFrameworkVersion>`, `<LangVersion>7.3</LangVersion>` and
`<PlatformTarget>x64</PlatformTarget>` in all three `.csproj` files; the solution defines only
`Debug|x64` and `Release|x64`. C# 8+ syntax (nullable reference types, switch expressions, using
declarations) fails compilation.

**R2 [enforced] Zero NuGet packages. Classic non-SDK `.csproj` only.**
Why: SDK-style WPF on .NET Framework requires a NuGet restore, and the project was built under an
instruction to install nothing; classic projects build with msbuild alone. Enforced by the absence of
any `PackageReference` or `packages.config` - adding one breaks the "msbuild with no restore"
property. Every `.cs` file must also be an explicit `<Compile Include>` item; an unlisted file is
silently not compiled.

**R3 [convention] No new NuGet package, assembly reference or `app.manifest` dependency without the
user naming it in the same run.**
Why: standing user instruction. Enforced by review only.

**R4 [enforced] Builds must produce zero errors and zero warnings in both configurations.**
Why: the code was kept warning-clean by construction - `HwndConsoleSession` gives `OutputReceived`
empty add/remove accessors purely to avoid CS0067, `TerminalRenderer` uses the pixelsPerDip `GlyphRun`
overload to avoid the obsolete-API warning. `<WarningLevel>4</WarningLevel>` is set in every
configuration of every project; `TreatWarningsAsErrors` is deliberately not set, so the check is the
build log. Pass condition: `MSBuild.exe BetterTerminal.sln /t:Rebuild /p:Configuration=<Debug|Release>
/p:Platform=x64` prints no warnings. See [WORKFLOWS.md#testing](WORKFLOWS.md#testing).

**R5 [enforced] Every launched process is assigned to a job object with
`JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE`.**
Why: the only thing that guarantees no orphaned shell or console host when a pane, the window or the
whole app disappears, including a hard kill. Verified by 20 cycles x 4 sessions leaving the
console-host count unchanged (11 before, 11 after). Enforced in `ProcessJob.cs`, regression-checked by
`tools/session-cycle.ps1`.

**R6 [convention] Background IO threads must never let an exception escape.**
Why: an escaping exception on a background thread terminates the process and takes every other pane
with it. The reader and writer threads in `ConPtySession` catch broadly and convert any failure into
an `Exited` event carrying a reason. Enforced by review and by `tools/ui-smoke.ps1`.

**R7 [convention] Session teardown order is fixed: `CompleteAdding()` -> close the pseudo console and
the job -> `Join()` both IO threads with a 2 s timeout -> only then dispose streams, exit event,
process handle and queue.**
Why: paid for in the pane-close crash of 2026-08-04 (exit code 0xE0434352). `Dispose()` disposed the
`BlockingCollection` while the writer thread was blocked inside `GetConsumingEnumerable`, producing an
unhandled `ArgumentNullException` from `SemaphoreSlim.Wait` that killed the whole application. Closing
the pseudo console and job first kills the client and breaks the pipe so the threads can leave; a
thread that misses the timeout is left to its finalizer rather than torn down under a block.
Corollaries, all load-bearing: IO threads copy their `FileStream` into a local before looping, so a
field nulled during dispose cannot throw on a background thread; `Write` and `Resize` return early
once disposed, because `ResizePseudoConsole` on a closed SafeHandle throws `ObjectDisposedException`
on the UI thread; `TerminalPane.CloseSession` calls `TerminalRenderer.Detach()` first. Enforced by the
UI-automation sequences in [WORKFLOWS.md#testing](WORKFLOWS.md#testing), not by any compiler.

**R8 [convention] Every mutating member of `CellGrid` requires the caller to hold `CellGrid.SyncRoot`.**
Why: the VT parser mutates the grid on the reader thread while the renderer reads it on the UI thread.
Enforced by the contract comment at the top of `CellGrid.cs` and by review.

**R9 [convention] No external API, package or platform name in user-visible text.**
Why: explicit user decision of 2026-08-04. "ConPTY", "conhost", "WPF", "Win32", "P/Invoke" and package
names must not appear in window chrome, tab or pane headers, the status bar, the command palette or
any message the user can read. They belong in code comments and documentation only. Enforced by review
of XAML and of every user-facing string. **One deliberate exception, added 2026-08-05:** the header
button and its window say **"SSH"**, because that is the command the user types and asked for by
name - it describes what they are doing, not what the program is made of. The port check is still
worded as "the standard port", never as a protocol internal.

**R10 [convention] All P/Invoke lives in `BetterTerminal.Interop`.**
Why: it keeps the Win32 audit surface to one project, which is what makes the DllImport inventory in
[STRUCTURE.md#entry-points](STRUCTURE.md#entry-points) trustworthy. No `DllImport` may appear in
`BetterTerminal.Terminal` or `BetterTerminal.Shell`. Enforced by review.

**R11 [convention] Start a task with the `md-orchestrator` skill and finish with `md-sync`.**
Why: the documentation set stays consistent only if one process writes it. `md-orchestrator` is
vendored at `.claude/skills/md-orchestrator`, and `/md-sync`, `/md-audit` and `/md-recall` exist at
`.claude/commands/`. The md-sync *skill* itself is still not installed, so `/md-sync` resolves to
re-running md-orchestrator in SYNC mode - say that rather than claiming the step was skipped. Tracked
in [MEMORY.md#open-threads](MEMORY.md#open-threads).

**R12 [convention] No emoji anywhere** - code, comments, UI text, documentation, commit messages.
Why: standing user instruction. Enforced by review.

**R13 [convention] Documentation is updated in the same change as the code.**
Why: a wrong document costs more than a missing one, because it is acted on. Any change that alters
structure, commands, dependencies or a decision updates the owning file (routed from CLAUDE.md) and
appends an entry to [MEMORY.md#decision-log](MEMORY.md#decision-log). Enforced by `/md-audit` before a
release and by `.claude/skills/md-orchestrator/scripts/validate_docs.py --strict`.

**R14 [convention] No assistant takes over the user's mouse, keyboard or foreground window.**
Why: it is the user's machine and his session. Banned unless he asks for it in the same run:
`SendKeys`, `SetForegroundWindow`/`SetActiveWindow`, `SendInput`/`mouse_event`/cursor moves, and the
UI-Automation patterns that actuate a control (`InvokePattern.Invoke`, `TogglePattern.Toggle`,
`SelectionItemPattern.Select`) - `tools\ui-smoke.ps1` included, because it drives buttons by
automation name. Synthetic input lands in whatever window has focus, so it types into the user's
work, and a window forced to the front interrupts him. The file this repository already wrote it
into is `tools\capture-window.ps1`: it uses `PrintWindow` "so taking a screenshot never has to steal
foreground focus from whatever the user is doing". **Verify without the desktop instead** - load the
built assembly and call the shipped class directly, then read the real side effect (file, registry,
version); that is what proved the 1.4.9 registry work. What only a person can confirm is handed to
the user with the exact steps: **the user drives the interactive pass**. Standing user instruction
2026-08-09, [MEMORY.md#decision-log](MEMORY.md#decision-log).

## Code rules

- **[enforced] Explicit `<Compile Include>` per file.** A new `.cs` file must be added to its `.csproj`
  by hand or it is not built into the assembly (R2).
- **[convention] One `SafeHandle` subclass per handle kind; no raw `IntPtr` handle kept in a field.**
  `SafeKernelHandle`, `SafePseudoConsoleHandle` and `SafeProcThreadAttributeList` exist for this; an
  unneeded handle (the process thread handle) is wrapped and disposed at once rather than leaked.
- **[convention] Every interop struct carries an explicit `StructLayout` with `Pack`, plus
  `CharSet = CharSet.Unicode` when it holds strings.** Today: `Pack = 2` for `Coord`, `Pack = 4` for
  `OsVersionInfo`, `Pack = 8` for the rest. Pointer fields are `IntPtr`, never `int` or `long` - that
  is what makes the x64 build correct.
- **[convention] Wide (`W`) Win32 entry points only**, `SetLastError = true` where the API documents
  it, failures routed through `Win32Error.Throw(<api name>)`.
- **[convention] OS version comes from `RtlGetVersion` (ntdll), never `Environment.OSVersion`.** Why:
  .NET Framework reports 6.2 for an app without an OS compatibility manifest, which silently forced
  the console-window fallback backend on a machine that supported ConPTY.
- **[convention] Both backends implement `ITerminalSession` and nothing branches on backend except
  `TerminalPane` and `TerminalSessionFactory.Resolve`.**
- **[convention] Parse off the UI thread, repaint on the 16 ms timer.** Do not marshal per byte or per
  cell to the dispatcher - that is the classic way to make a terminal drop input under load.
- **[convention] `SplitPane`: detach before attach.** WPF rejects an element that still has a logical
  parent, which is why there is deliberately no `Replace`.
- **[convention] Shipped XAML contains real strings only** - no placeholder or filler text, no `TODO`.
- **[convention] Theme tokens are defined once, in `BetterTerminal.Shell/Theme.xaml`.** No literal
  colour, brush, corner radius or font family anywhere else in XAML or code.
- **[convention] UI design constraints, already honoured; a regression counts as a bug:** one signal
  carrier per status (never a pill plus a dot plus an outline for one state); no gradient headers, no
  purple or indigo gradients, no glassmorphism; one corner radius (2) and one shadow level, app-wide.
- **[convention] Cell metrics are measured from the glyph typeface, never hardcoded or guessed.**
- **[convention] Unknown VT sequences are dropped silently, never printed to the grid.**

## Git rules

The repository was initialised on 2026-08-05 and pushed to
`https://github.com/TruXe/BetterTerminal` (**private**), on top of the repository's own first
commit. Default branch `main`, licence MIT. These remain `[convention]` - nothing but review
enforces them.

- **[convention] No assistant runs a git write operation unless the user asks for it by name in the
  same run.** Banned without a named request: `commit`, `push`, `merge`, `rebase`, `reset --hard`,
  `tag`, `cherry-pick`, `revert`, PR creation and PR merge. Read-only git (`status`, `log`, `diff`,
  `show`) is always fine. Standing user instruction, [MEMORY.md#decision-log](MEMORY.md#decision-log).
- **[convention] Branch naming:** `feature/<short-slug>`, `fix/<short-slug>`, `docs/<short-slug>`,
  lowercase ASCII with hyphens. Default branch `main`.
- **[convention] Commit format:** `<area>: <imperative summary>` on one line, at most 72 characters,
  where `<area>` is `interop`, `terminal`, `shell`, `tools` or `docs`. Optional body wrapped at 72. No
  emoji, no trailing period, English only.
- **[convention] A commit must build clean** in both `Debug|x64` and `Release|x64`, zero warnings (R4).
- **[convention] A release message lists everything that changed, one tagged line each:**
  `[ FIXED ]` for something that was broken, `[ ADDED ]` for something new, `[ REMOVED ]` for
  something taken away, `[ WARN ]` for anything the user must know before updating - a prompt they
  will see, a promise that no longer holds, a manual step, a known limit. Written with the spaces
  inside the brackets, in the release notes and in the tag message. The lines come from the
  `MEMORY.md` decision-log entries written since the previous tag, which is where each decision and
  its cost is already recorded. `[ WARN ]` is not optional. Standing user instruction 2026-08-07,
  [MEMORY.md#decision-log](MEMORY.md#decision-log).
- **[enforced by .github/workflows/build.yml] A published version is a Release, never a pre-release.**
  The publish step creates with `--latest` and corrects an existing one with
  `gh release edit $tag --prerelease=false --latest`. Adding `--prerelease` back is a regression: a
  pre-release is hidden from the repository's latest-release link, which is the download link the
  README points at. The BETA wording in the notes describes the software, not the release flag.
  Standing user instruction, [MEMORY.md#decision-log](MEMORY.md#decision-log).
- **[enforced by .gitignore] Never committed:** `bin/`, `obj/`, `.vs/`, `*.user`, `*.suo`, and the
  generated 10 MB payload `docs/_archive/*/flood.txt` produced by `tools/flood-benchmark.ps1`.
- **[convention] Also never committed:** screenshots and `.log` files produced by `tools/`, and any
  copy of `%APPDATA%\BetterTerminal\workspace.json` - it holds the user's working directories.

## Security and secrets

- **[convention] The repository contains no secrets and none may be added** - no API keys, tokens,
  connection strings or credentials in source, XAML, `.csproj`, `tools/*.ps1` or docs. There is
  nothing to rotate today; keep it that way.
- **[enforced] The app runs `asInvoker` and never elevates.** `BetterTerminal.Shell/app.manifest`
  declares `<requestedExecutionLevel level="asInvoker" uiAccess="false" />`. Do not raise it: a
  terminal host that auto-elevates would run every child shell elevated.
- **[convention] The app makes exactly one kind of network call and sends no telemetry.**
  `Services\HostReachability.cs` opens a TCP connection to port 22 of a **saved connection's host**,
  with a 2 s timeout, and closes it again - it sends no bytes, reads none and stores no result on
  disk. It runs only while the connections window is open, and only against addresses the user
  typed. Nothing else in the application may reach the network: there is no HTTP client and no such
  dependency, and adding one is a dependency change and falls under R3. Superseded the original "no
  network calls at all" rule on 2026-08-05 at the user's explicit request,
  [MEMORY.md#decision-log](MEMORY.md#decision-log).
- **[convention] Terminal output and typed input are never written to disk, debug logging included.**
  They are a credential channel - users type passwords into shells.
- **[convention] The three stores persist only what they persist today. Never add command history or
  scrollback contents to any of them.**
  `SessionStore` - tab layout, split ratios, shell name, working directory, appearance and window
  placement - `%APPDATA%\BetterTerminal\workspace.json`.
  `ConnectionStore` - a user name and an address per saved connection, nothing else -
  `%APPDATA%\BetterTerminal\connections.json`. **No password, key path or passphrase field may be
  added**: the connection is handed to the shell as a typed line and the shell's own client handles
  authentication.
  `ProjectStore` - the project name, its shell, its startup line, its user-defined commands and its
  named values - `<project>\.beterm\project.json`, inside the project folder. It is plain text the
  user may commit, so the setup window says so and nothing writes a secret into it.
- **[convention] `BetterTerminal.Wrap` never modifies a script.** It reads no `.ps1`, `.bat` or
  `.cmd` file and writes none - it starts them and reports what they returned. It must also never
  invent an exit code: what `Process.ExitCode` gives is what is shown, including the cases where a
  script reports its verdict in its output instead.
- **[convention] Never build a child command line out of user-typed text.** Input reaches the child
  only through the pseudo console input pipe; the command line comes from `ShellProfile`. This is
  what makes the saved connections and the user-defined project commands safe: both are written
  through `TerminalSurface.StartupCommand` or `Write`, exactly as if the user had typed them.
- **[convention] Installation and command registration stay per user and never elevate.** The
  service registration below is the one thing that leaves the user profile; nothing else may.
  `Services\SelfInstall.cs` copies the application, its libraries and the `beterm-*` helpers to
  `%LOCALAPPDATA%\BetterTerminal\app`, and `Services\CommandRegistration.cs` writes
  `%LOCALAPPDATA%\BetterTerminal\bin\beterm.cmd` and joins that one folder to
  `HKCU\Environment\Path`. Nothing is written to `Program Files`, to the machine-wide search path or
  to `HKLM`, no uninstall entry is registered, and both steps stay best effort - a failure may not
  stop the window opening.
- **[convention] The folder right-click entry is per user, opt-in, and holds no state of its own.**
  `Services\ExplorerMenu.cs` writes `Directory\Background\shell\BetterTerminal` and
  `Directory\shell\BetterTerminal` under `HKCU\Software\Classes` only - never `HKCR` directly, never
  `HKLM`, no elevation. It is written only when the user turns the switch on in the settings window;
  `ExplorerMenu.Refresh()` on start may re-point an existing entry and **must never create one**. The
  registry is the single record - do not mirror it into `workspace.json`, or the switch starts lying
  the first time the user deletes the keys by hand. Added 2026-08-09,
  [MEMORY.md#decision-log](MEMORY.md#decision-log).
- **[convention] One deliberate elevation exception: the separate `beterm-service.exe` service
  host, which the first run registers.** Registering a Windows service is machine-wide and needs an
  elevated prompt and the service database, so this is the one component that elevates. Since
  2026-08-07 the application asks for it itself: `Services\ServiceInstall.cs` starts
  `beterm-service.exe --install` with the `runas` verb once, after the main window is up, on a pool
  thread. **The application process itself still runs `asInvoker` and never elevates** - it starts
  an elevated child, which is not the same thing, and no shell it hosts is ever elevated. Three
  constraints hold and a change that breaks any of them is a regression:
  **(1) asked once** - a marker in `%LOCALAPPDATA%\BetterTerminal\service-install.txt` is written
  *before* the attempt, so a refusal, a failure or a machine with no elevation available is never
  asked twice; **(2) never blocking** - the prompt is the user's to answer in their own time and no
  window waits on it; **(3) never required** - nothing in the application depends on the service, so
  refusing it costs the user nothing. The service writes only to the machine service database and
  the application event log. Superseded the "run by hand, never by the application" form of this
  rule on 2026-08-07 at the user's explicit request after being told it means a prompt and a write
  outside the user profile, [MEMORY #decision-log](MEMORY.md#decision-log).
- **[convention] The CLI-AI Wizard builds a command line from menu choices - which is its whole
  point - so its own child is exempt from "never build a child command line out of user-typed
  text".** It is safe because every free-text value is run through the allow-list `TextSanitizer`
  first, the choices are fixed strings, and the assembled command is the wizard's own child, not a
  Shell command line spliced from user text. The Shell's rule is unchanged.
- **[convention] The one-file launcher is native and is the named exception to R1/R2's "everything is
  .NET 4.8, classic csproj" - which describe the .NET projects.** `BetterTerminal.Bootstrap` is a C++
  `vcxproj` (C++17, x64, no package, no vcpkg/NuGet) that only embeds, unpacks, runs and cleans up;
  it must not change the application's behaviour, and it runs `asInvoker` like everything else. It
  depends on the C# build being present, declared as a solution build dependency on the Shell.
- **[convention] New framework assembly references added 2026-08-06, no package (R2 holds):**
  `System.ServiceProcess` and `System.Configuration.Install` for the service, and
  `System.Runtime.Serialization` + `System.Xml` for the wizard's JSON model file. All ship with
  .NET Framework 4.8. They implement features the user named, so they satisfy R3.
- **[enforced by construction] The generated script contains no absolute path and no non-ASCII
  byte.** It reaches the installed copy as `%~dp0..\app\BetterTerminal.exe`. A script is decoded in
  whatever code page the console is using, so an accented character in it is a coin flip - this is
  what made the first version fail, [TIPS #gotchas](TIPS.md#gotchas).
  The stored path is read and written **unexpanded and with its original value kind**, or every
  `%USERPROFILE%` style entry in the user's path would be frozen into a fixed path.

## Never do

- **Never let a background thread throw** - it kills every pane. R6,
  [MEMORY.md#decision-log](MEMORY.md#decision-log).
- **Never dispose a `BlockingCollection`, stream or SafeHandle before joining the thread using it.**
  R7 - this is the exact 2026-08-04 pane-close crash.
- **Never mutate `CellGrid` without holding `SyncRoot`.** R8.
- **Never run `git commit`, `push`, `reset --hard`, `rebase`, `merge`, `tag`, `cherry-pick`, `revert`,
  or open or merge a PR** unless the user named that operation in the same run.
  [MEMORY.md#decision-log](MEMORY.md#decision-log).
- **Never run `dotnet add package` or `Install-Package`, or edit `app.manifest` to add a dependency**,
  without the user naming it. R3.
- **Never convert a `.csproj` to SDK-style and never add a `PackageReference`.** R2.
- **Never write `DllImport` outside `BetterTerminal.Interop`.** R10.
- **Never put "ConPTY", "conhost", "WPF", "Win32" or a package name into user-visible UI text.** R9.
- **Never use C# 8+ syntax** - it does not compile under `LangVersion 7.3`. R1.
- **Never hardcode a colour, radius, shadow or font outside `Theme.xaml`.**
- **Never use emoji** in code, comments, UI text, docs or commit messages. R12.
- **Never send synthetic input, pull a window to the foreground, or click a control through
  automation.** R14 - the user's desktop is his, and a script's click is not evidence of anything.
- **Never delete `docs/_archive/`** - it is the only record of superseded documentation.
- **Never hardcode the repository path `D:\Multi Terminál Window` inside a `.ps1` file.** PowerShell
  5.1 reads a `-File` script as ANSI unless it has a UTF-8 BOM, so the non-ASCII directory name fails
  with "path does not exist". Pass it as a parameter.
  [TIPS.md#environment-quirks](TIPS.md#environment-quirks).
- **Never run `tools/flood-benchmark.ps1` or `tools/session-cycle.ps1` from a process with redirected
  standard output.** The child shell writes into the inherited handles instead of the pseudo console
  and the grid stays empty - which looks exactly like a broken ConPTY implementation and is not. Use
  `Start-Process powershell -WindowStyle Hidden -Wait`. [TIPS.md#gotchas](TIPS.md#gotchas).

---
[← CLAUDE.md](CLAUDE.md) · [STRUCTURE](STRUCTURE.md) · [WORKFLOWS](WORKFLOWS.md) · [MEMORY](MEMORY.md)
