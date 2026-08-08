---
updated: 2026-08-08
scope: Why BetterTerminal is built the way it is - decisions, unfinished threads, dead ends and vocabulary, for a reader returning cold.
stability: evolving
sources: [session context packet 2026-08-04, source under D:\Multi Terminál Window after the design import, docs/_archive/2026-08-04/, tools/*.ps1, user answers]
owner_agent: memory-agent
---

# MEMORY

Everything here happened on 2026-08-04, and there is no git history to mine. If this file disagrees
with the code, the code wins - then fix it.

## Current state

**Update 2026-08-08 (1.3.1).** Keyboard input reaches a session as whole key events now, not as bare
characters the console host had to guess a key for. That guess used the **system default** keyboard
layout rather than this window's, so on a Czech layout every digit - and on any layout every capital
letter - arrived wrapped in synthetic Shift records and was dropped by programs that read keys rather
than lines; `choice.exe`, and therefore the `ai.bat` menus, ignored `1` to `9` while `Q` worked. The
newest [decision-log](#decision-log) entry has the cause, the measurements and what was deliberately
left alone.

**Update 2026-08-06 (BETA).** Two new, unverified-by-a-person features landed: a **CLI-AI Wizard**
profile in the shell picker - a guided launcher for CLI AI agents ported from Deerpfy's `ai.bat`
into `BetterTerminal.AIWizard` (DLL) plus `beterm-aiwizard.exe` - and a real Windows service host,
`beterm-service.exe` (**BetterTerminal Host**), that stands for the helper components as a service.
Both configurations still rebuild zero-warning. The user drives the interactive pass and only then
authorises a git push and a new build. Details and the deliberate elevation exception are in the
newest [decision-log](#decision-log) entry.

**Update 2026-08-05.** BetterTerminal now registers itself as the command `beterm`, opens the folder
it was called from as a project with its settings in a hidden `.beterm` folder, lets the user define
their own commands and values there, and keeps an address book of remote connections with a
port-22 reachability heart behind the header's SSH button. Everything in the snapshot below still
holds; what changed is described in the newest [decision-log](#decision-log) entry, and the five
items the user reserved for an interactive pass are still open. Known gap: opening `beterm` twice
gives two application windows that both write the same `workspace.json` on close, so the last one to
close wins.

Snapshot 2026-08-04, after the design-system import. BetterTerminal is a WPF desktop application
hosting multiple live shell sessions (cmd.exe and Windows PowerShell) in tabs and splits in one
window. Three planned phases complete, then the whole UI layer replaced by an imported design system.
Root is `D:\Multi Terminál Window`; layout in [STRUCTURE #directory-map](STRUCTURE.md#directory-map).

**What works.** Both configurations rebuild clean - `Debug|x64` and `Release|x64`, **zero errors and
zero warnings**; that is the pass condition and it is met. Sessions run, render, take keyboard input,
scroll back, copy and paste. On the designed shell, splits, new tab, close pane, the command palette
(12 entries), the settings window, the theme switch and the scheme switch all drive **real** sessions:
switching to Light repainted the chrome to **#FBFBFC** while the terminal kept its scheme, and
Solarized Dark repainted the live terminal to **#002B36**. Appearance and layout both survive a
restart. A 10.12 MB flood measured Debug 9.86 s = **1.03 MB/s**, Release 6.97 s = **1.45 MB/s** and a
later run 6.15 s = **1.64 MB/s** - so Release is a **1.45-1.64 MB/s range that varies between runs of
the same build**; quote the range, never one figure. Every run filled scrollback to its 5000-line cap
with the last line intact and exit 0. 20 cycles x 4 concurrent sessions left the console-hosting
process count at 11 before and 11 after - **no orphans** - and a full smoke run left none either. The
pane-close crash is fixed, covered by five UI-automation close sequences passing on **both Debug and
Release** with no crash events. A restored workspace with **5 panes rendered 5 independent live
sessions** with correct reflow.

**In progress:** nothing is mid-edit. **Broken:** nothing known. **Not verified:** The settings pages other than Appearance - Profiles, Keyboard, Panes and tabs,
About - are navigation entries with **no page content**: the bundle shipped only the Appearance page.
The interactive gaps the user reserved for themselves are still open: per-pane keyboard
independence; full-screen applications entering and leaving the **alternate screen buffer**; a DPI
change while running; window snap; input latency.

> ❓ Unverified: the five interactive items above - not confirmed against a running build.

**Next**, in order of leverage: the four empty settings pages, the user's interactive pass, then the
open threads below.

## Decision log

Append-only, newest first. Entries below 2026-08-05 are dated 2026-08-04; order within that day is
reconstructed.

### 2026-08-08 - Typed characters are sent as whole key events, because the host was guessing the key (1.3.1)

**Context.** Running Deerpfy's `ai.bat` in a pane, its menu ignored `1` to `9` while `Q` worked.
Reported on a Czech layout and reproduced with the window switched to English as well, which is the
detail that gives the cause away.

**Cause.** Input was written to the pseudo console as bare characters. The console host then has to
work out which key produced each character, and it does that with `VkKeyScan` against **its own**
keyboard layout - the system default, not the layout this window is using, which is why switching
the window to English changed nothing. Every character that needs Shift on that layout comes out
wrapped in separate `VK_SHIFT` key-down and key-up records: all of `1` to `9` on a Czech layout, and
every capital letter on any layout. `choice.exe` reads key records rather than a line, stops at the
`VK_SHIFT` record and never sees the character. Lower-case letters need no Shift, so `Q` always
worked. Measured, not assumed: a probe driving the real `ConPtySession` had `choice /c 1234FUQ` fall
through to its timeout default for `1` and for `F`, and accept `q` at once; a dump of the records
inside the pseudo console showed `DOWN vk=0x10 SHIFT_PRESSED` ahead of the character every time.

**Decision.** Honour win32 input mode. The host asks for whole key events with `CSI ? 9001 h` in the
first bytes of every session and BetterTerminal was ignoring the request; `VtParser` now records it
on the grid as `CellGrid.Win32InputMode`, and `TerminalRenderer.OnTextInput` encodes typed text as
`CSI vk ; scan ; char ; down ; controls ; repeat _` instead of sending the character alone. The
guess disappears because the key is stated.

**Decision.** The virtual key comes from `KeyInterop.VirtualKeyFromKey` on the key of the
`PreviewKeyDown` that precedes the text - no P/Invoke, and the value is derived from **this
window's** layout, which is the whole point. Sending zero there would have been simpler and does
satisfy `choice.exe`, but it breaks every program that switches on `ConsoleKey`, `BetterTerminal.Wrap`
and its `Q` to quit included. The scan code is left at zero: it names the physical key and nothing
downstream reads it. The real Shift, Ctrl and Alt state is passed through - a separate test proved
`SHIFT_PRESSED` on the character's own record is harmless, the standalone `VK_SHIFT` records were
the problem - so a program sees what a real console would show it.

**Decision.** Only typed text changes. Paste still goes as text, because bracketed paste mode wraps
it in markers that must stay literal, and startup and project commands are whole lines written
straight to the session. A character with no key behind it - composed, or from an input method -
reports key zero and is still delivered on its character alone.

**Verified.** Both configurations rebuild zero-error, zero-warning, and `BUILD\` is staged. Driven
end to end through a real `TerminalRenderer` hosting a real session, raising the same routed events
WPF raises: mode negotiated true, then `1` gave `RESULT=1`, `F` gave `RESULT=5` and `q` gave
`RESULT=7` from `choice /c 1234FUQ` - all three keys, where before only `q` answered. The records
arriving inside the session are now `vk=0x31 char='1'` and `vk=0x51 char='q'` with no modifier
noise. Regression checks on the line-reading path: `echo hi 123 ABC` typed and entered came back as
`GOT=[echo hi 123 ABC]`, and the accented `ěščř` round-tripped intact.

### 2026-08-08 - A dropped file is text on the input line, quoted for the shell that pane runs

**Context.** Dropping files from the file manager onto a pane should type their paths where the
pointer is, never where the focus is, and never run anything.

**Decision.** The drop target is `TerminalSurface`, one instance per pane, so the pane under the
pointer wins by hit testing alone - no focus lookup exists to get wrong. The surface needed a
transparent background first: a null one is not hit testable and the drag passed straight through.
Enter and leave are counted, not toggled, so crossing the renderer inside the pane cannot flicker
the highlight.

**Decision.** Quoting is chosen per pane from its own shell, not once for the application: double
quotes for the command prompt and only when the path needs them, single quotes doubled for
PowerShell, `'\''` for the two posix cases, and `/mnt/c/...` for WSL. This application has no WSL
or SSH profile - SSH is `ssh user@host` typed into a command prompt pane - so the kind is read
from the startup command first and the executable second. The limit is honest and worth knowing:
a shell the user reaches by typing `wsl` themselves mid-session still quotes as its profile.

**Decision.** Insertion reuses the paste path, which already reads bracketed paste mode off the
grid and wraps the text in `ESC [200~` and `ESC [201~`, so a drop onto a pane running something
cannot corrupt the output stream. Nothing appends a newline. The hosted-console fallback backend
refuses programmatic input by construction, so a drop there reports a short message instead of
throwing - and that backend's console window already handles dropped files itself.

**Verified.** Both configurations build zero-error, zero-warning and `BUILD\` is staged. The
quoting matrix was exercised against the built assembly for all four kinds, including a path with
a space, one with `&()`, one with an apostrophe, a folder, a drive root and a path carrying CRLF.
The teardown sequence (split right, split down, new tab, three pane closes) left the process
alive, which is what covers the revoke added to `CloseSession`.

### 2026-08-08 - The tab actions live in the caption strip, not in a band of their own

**Context.** The new-tab plus and the profile chevron sat 7 px lower than minimise, maximise and
close, at a different glyph size and with rounded corners, so the top right of the window read as two
unrelated rows. The user asked for one line.

**Decision.** They are caption buttons now: `Bt.Button.CaptionAccessory`, the caption template with
a **square 32 px target** instead of the 46 px window-control one, in the same stack panel as the
window buttons and top aligned with them. Both glyphs are 10 pt like every other glyph in that strip.
The plus was 14 pt - that was the size difference, not an illusion.

**Decision.** The band is a token now: `Bt.Size.WindowButtonHeight` (32) and
`Bt.Size.CaptionAccessoryWidth` (32) in `Primitives.xaml`, and `Bt.Button.Caption` reads the height
from it rather than carrying a literal 32. Alignment is structural - move the band and all five
buttons move together, which is what stops this from drifting apart again.

**Decision.** They moved out of the tab strip's column into the window-button column, so the grid
keeps the tabs clear of them; before, both lived in the star-width column and tabs could slide
underneath. The 8 px gap after the chevron is deliberate: it keeps the plus off the minimise target.

**Verified.** `Release|x64` and `Debug|x64` both build zero-error, zero-warning; `BUILD\` staged at
1.3.0. Captured with `PrintWindow`: the five glyphs sit on one line. No version bump - `SelfInstall`
replaces at the same version by file timestamp, which is exactly the case it was written for.

### 2026-08-07 - Code is coloured, hand-built, because there is no editor library (1.3.0)

**Context.** The user wanted code and structured files coloured the way an editor does it. The
earlier decision stands - no package, so no AvalonEdit - which means the whole thing is written here.

**Decision.** `RichTextBox` is the control. It is the only one in this framework that can be both
edited and hold colour; a `TextBox` cannot colour anything and a drawn control cannot be edited
without writing an editor. The price is that **a colour change is an edit like any other**, so the
undo history contains steps the user never typed. That is the known cost of the no-package decision
and it is not a bug to be chased.

**Decision.** One paragraph per line, one run per coloured stretch, and the state left open at the
end of a line (`SyntaxState`: normal, inside a block comment, inside an element) kept on the next
paragraph in its `Tag`. Typing therefore re-reads **one line**; the lines below it are re-read only
when that line changed what it leaves open - a comment marker typed, an element opened. Without that
the viewer would re-read the whole file on every keystroke.

**Decision.** Grammars are data, not code. `SyntaxLanguage` is where a comment starts, what quotes a
string and which words are keywords, and one generic reader serves C#, C/C++, JavaScript and
TypeScript, Java, Go, Rust, PHP, CSS, SQL, Python, PowerShell, shell and batch. Only two families
carry structure in the text itself and get a reader of their own: **JSON**, where what separates the
name of a member from a string value is the colon after it, and **markup**, where an element spans
lines. A name that is not in the catalogue gets **no** colours rather than the wrong ones, and falls
back to the plain box - guessing a grammar from the bytes is how a log ends up striped.

**Decision.** Eight `Bt.Syntax.*` brushes, present in all three token tiers as the parity rule
requires. Dark and light get their own hues, because the readable green for a comment on black is
not the readable one on white; high contrast maps to system colours and invents nothing. Colouring
stops above 512 KB - the view builds one element per coloured stretch, and a file too large to edit
is never coloured either.

**The trap.** Assigning `Document` in the constructor already raises a text change, so the handler
ran before the timer it uses existed and every open of a code file killed the application. Build the
fields a handler touches before anything that can raise one.

**Verified.** C#, JSON, XAML, Python, C++ opened with the right colours and the right language named
in the strip along the bottom. Typing into a Python file through the keyboard and pressing Ctrl+S
wrote it back: 264 to 290 bytes, the typed line first, **line endings still LF and no byte order
mark added**. Debug and Release both rebuild with zero errors and zero warnings.

### 2026-08-07 - The Files window shows anything: pictures, any text encoding, and bytes for the rest (1.2.0)

**Context.** The first version read every file as UTF-8 text, so a picture came out as noise, an old
batch file came out with the accents scrambled, and anything binary was meaningless. The user asked
for a viewer that can show everything.

**Decision.** What a file *is* decides how it is shown, and the answer comes from reading it, not
from its name. Three outcomes, and `OpenedFile` carries exactly one of them:

- **A picture** when the extension is one WIC is expected to decode and the decode actually
  succeeds. The list is only a filter - a missing codec or a name that lies falls through to the
  dump instead of failing. It is decoded on the pool thread and **frozen**, which is what lets it
  cross to the interface thread; shown `Uniform` with `StretchDirection="DownOnly"`, so a small
  picture keeps its own size and a large one fits.
- **Text** in the encoding it was actually written in: a byte order mark is taken at its word, a
  file without one that decodes strictly as UTF-8 is UTF-8, and one that does not is read in the
  machine's code page. That last arm is what makes a batch file written years ago on this computer
  legible, and saving writes it back the same way rather than converting it.
- **A dump** of the first 64 KB otherwise, in the usual three columns.

**Decision.** A zero byte decides that a file is not text. It is the one reliable sign: text without
a mark never contains one, and an executable or an archive reaches one almost immediately. UTF-16
without a byte order mark is the known false negative, and it is rare enough to accept.

**Decision.** Nothing is refused any more. A text file past the 2 MB editing limit is no longer an
error message - it opens read-only, showing its beginning, and the strip along the bottom says so.
Only editable text can be dirty, so a picture or a truncated log can never be saved over.

**Verified.** Seventeen files opened through UI automation: `.js .log .xml .json .cs .cpp .py .ps1
.txt`, a file with no extension, `.png .jpg .bmp`, a 2.9 MB log, and a blob with a zero byte in it -
each reported the right kind. A genuine windows-1250 batch file was read as `windows-1250` and a
UTF-16 file as `utf-16`. Debug and Release both rebuild with zero errors and zero warnings.

### 2026-08-07 - One version for the whole application, and BUILD is staged after every change

**Context.** Two things were true at once and both were wrong. The version number was written down
in eight places and none of them agreed - the launcher said 1.0.1, the application 1.0.0, the wizard
and the service 0.1.0, and `Interop` and `Terminal` had no version at all. And the copy under
`%LOCALAPPDATA%\BetterTerminal\app` was decided per file by its timestamp and only ever received
files matching `BetterTerminal*`, so the installed copy had **no banner, no wizard and no wrapper**
in it. The user's standing instruction: build into `BUILD` after every change, wrapper included, and
make the copy in AppData replace itself.

**Decision.** `VersionInfo.cs` at the repository root is the one version, linked into all eight .NET
projects (`<Compile Include="..\VersionInfo.cs"><Link>`), and the per-project `AssemblyVersion` and
`AssemblyFileVersion` attributes are gone. The launcher is native and cannot link a `.cs` file, so
its `Bootstrap.rc` still carries the number a second time - and `tools\build.ps1` **refuses to
stage** when the two disagree, because that number is exactly what the installed copy compares
against. Everything is now **1.1.0**.

**Decision.** `SelfInstall` compares versions, not timestamps: a build carrying a higher version
replaces the whole install folder, and at the same version the per-file timestamp still decides, so
a rebuilt developer build still lands without bumping anything. The version of the installed copy is
read with `FileVersionInfo` off the file rather than by loading the assembly, so a copy that is
running is still readable. The file set now includes `beterm-*` as well as `BetterTerminal*`.

**Decision.** `tools\build.ps1` is the command for every change: it builds `Release|x64` and stages
`BUILD\` - the launcher, `app\` with `beterm-wrap.exe` in it (its own project, referenced by nothing,
which is why it kept being forgotten), `service\`, the release layout in `dist\` and the zip. It
never kills a process. A file that is running is left at its old version, named in a `WARNING:`
block, and the script exits 2 - which is what happens in practice, because the user usually has
`BUILD\BetterTerminal.exe` open while it runs. `dist\` takes its launcher from the build output
rather than from `BUILD\`, so a release is never cut from a copy that could not be replaced.

**The trap this immediately sprang, and the guard for it.** The launcher in `BUILD` could not be
replaced while the user had it open, so it stayed at 1.0.1 **carrying its old embedded payload**.
Running it unpacked the old application into a fresh temporary folder - where every file is newer by
definition - and the timestamp rule then reinstalled 1.0.0 **over** the 1.1.0 copy under the user
profile. The user saw an application without the feature that had just been built. `SelfInstall` now
returns without touching anything when the installed version is higher than the running one; a build
can only ever move that copy forward. Read the general lesson as: a stale one-file launcher is a
carrier of the whole application, not just of an executable.

**Verified.** Debug and Release both rebuild with zero errors and zero warnings. `tools\build.ps1`
staged 26 files with both the launcher and `app\BetterTerminal.exe` at 1.1.0.0. Launching
`BUILD\app\BetterTerminal.exe` once took the copy under the user profile from **1.0.0.0 with four
files to 1.1.0.0 with seven** - the banner, the wizard and the wrapper among them - and the staged
build was checked through UI automation to carry the Files button.

### 2026-08-07 - A Files window: the folder as a tree, one file at a time in a plain editor (slice 1 of 4)

**Context.** The shell could open a folder as a project but never show what was in it. The first of
four planned slices: the tree, opening a file, editing it and Ctrl+S. Later slices add editor depth
(highlighting, find, line endings), explorer depth (lazy loading, a watcher, a context menu) and
session state (restored tabs, close prompts).

**Decision.** No editor library. The user picked a plain `TextBox` over adding `AvalonEdit`, so the
zero-package rule holds; the cost is that slice 2's highlighting and line numbers have to be built by
hand on a custom control, and that is now the known limit of this feature. The editor also refuses a
file over 2 MB (`WorkspaceFiles.MaximumFileBytes`) rather than freeze: the whole text is one string
and the editor binds it back on every keystroke.

**Decision.** Its own window, not a pane. The user asked for a separate form, so `Views/FilesWindow`
follows the settings / connections / workspace-setup pattern - owned by the main window, modeless,
one instance tracked in `_filesWindow`. Nothing about the pane tree, the session contract or the
layout persistence changed.

**Decision.** `Services/WorkspaceFiles.cs` is a new service, not code inside `TerminalWorkspace`. It
does the listing, the reading and the writing on a pool thread and posts the result back through the
dispatcher, exactly as `HostReachability` does; the view model stays state plus two events
(`OpenRequested`, `SaveRequested`) and the workspace wires them. There is no `async`/`await` in the
Shell and this did not introduce any.

**What it does.** The root is the project folder, or the working directory of the focused session
when the window was not opened in a project. Hidden folders are skipped, which is what keeps
`.beterm` and a source-control database out of the tree. A file is read with its byte order mark
detected and written back with the same encoding, so opening and saving does not add or remove one.
The tree is built in full on first open - fine for a project, slow for a folder with a `node_modules`
in it, and that is what slice 3's lazy loading is for.

**Two things the run caught.** A tree row announced its view-model type to assistive technology,
because the header is a panel and not a string - the same trap the saved-connections list hit; fixed
with `AutomationProperties.Name` on the container in `Bt.FileRow`. And `TreeViewItem.IsMouseOver` is
true while the mouse is over any *descendant*, so hovering a file lit up every folder above it; the
hover trigger is on the row `Border` (`SourceName="Fill"`), not on the item.

**Verified.** Debug and Release both rebuild with zero errors and zero warnings. Driven through UI
automation against a scratch folder opened with `--project`: the tree came up expanded with the
folder, its subfolder and its file (and no `.beterm`), selecting the file opened it with its text,
replacing the text and pressing Ctrl+S wrote the new content to disk, and the process stayed alive.
Note for anyone writing such a script: an owned window is **not** a child of `RootElement` in the
automation tree - the first attempt concluded the window had never opened. Find it by handle.

### 2026-08-06 - A native C++ launcher carrying the whole app, and the wizard hands the console over cleanly

**Context (two things in one run):** First, a bug: pressing Run in the CLI-AI Wizard froze the pane
instead of starting the agent. The wizard is the pane's own process and had left the console in its
own mode (escape-sequence processing, UTF-8) while a full-screen agent tried to take over - so the
agent inherited a console it did not own and stalled. Second, the user asked for a **single native
C++ executable** that embeds the entire C# build, unpacks it to a temp folder, runs it, waits, and
cleans up - one file to run the whole project.

**Fix for the freeze:** `TerminalMode` gained `Suspend()`/`Resume()` (in the shared Wrap file).
`WizardConsole.Launch` now **suspends** the console - hands it back exactly as it was before the
wizard configured it - runs the agent, and **resumes** only after it exits. This is the "close the
BAT first, then run with the specs" behaviour the user described. Verified: rebuilds zero-warning;
the live agent run is the user's BETA check.

**Launcher decision:** a new **native** project `BetterTerminal.Bootstrap` (a `vcxproj`, C++17, x64,
`/SUBSYSTEM:WINDOWS` so no console flashes) builds `BetterTerminal-Launcher.exe`. A build-time
packer, `tools\pack-payload.ps1`, packs the whole `BetterTerminal.Shell` output for the current
configuration into `payload.pack` (trivial format: magic, count, then per file a UTF-8 relative
path and its bytes), which the `.rc` embeds as one `RCDATA` resource. At run time modular RAII
classes do the rest: `ResourceManager` reads the resource, `PayloadArchive` parses it with bounds
checks, `TempDirectory` (RAII) makes `%TEMP%\BetterTerminal\{GUID}` and deletes it in its
destructor - so cleanup runs even on an exception, `PayloadExtractor` writes and size-verifies each
file, `ProcessLauncher` starts the app with `CreateProcessW` in that directory and waits, and the
child's exit code becomes the launcher's. Its own arguments are forwarded, so
`BetterTerminal-Launcher.exe --project C:\x` still opens a project.

**Why unpack, not hostfxr:** `hostfxr`/`nethost` host only .NET Core+, and this app is .NET
Framework 4.8 WPF. More decisively, **self-install depends on a real file on disk**:
`SelfInstall`/`CommandRegistration` read `Assembly.Location` and copy `BetterTerminal*` plus the
helpers out to `%LOCALAPPDATA%`. Launched from the temp copy, the first run copies everything into
the persistent per-user folders, so after the launcher deletes temp, `beterm` and the helpers keep
working. The launcher **waits for full exit** before cleanup, which is what makes this safe. So the
unpack-and-run model was kept and self-install is unchanged.

**Traps paid for, worth remembering:**
- A header named `WinError.h` on the include path **shadowed the Windows SDK's `winerror.h`**, which
  `windows.h` includes internally - so `windows.h` itself failed to compile with a cascade of errors
  in `shellapi.h`. Renamed to `Errors.h`. Never name a header the same as a SDK header that
  `windows.h` pulls in.
- `#include <shellapi.h>` **before** `<windows.h>` fails: shellapi.h needs windows.h's macros first.
  Order the Windows headers windows.h-first.

**Integration:** classic .NET projects unchanged. The launcher is added to the solution as a mixed
native project with a build dependency on `BetterTerminal.Shell` (so the app builds first, then the
packer runs). `PlatformToolset` is `$(DefaultPlatformToolset)` so it uses whatever C++ toolset is
installed. `payload.pack` is generated and git-ignored.

**Verified 2026-08-06:** both configurations rebuild all nine projects **zero-error zero-warning**;
the packer embeds the whole app (launcher exe is larger than the app payload, confirming the single
file carries it). **Not verified by a person:** the launcher actually unpacking and running the app
end to end, and the wizard's Run starting a live agent - the user's BETA pass.

### 2026-08-06 - A CLI-AI Wizard profile, ported from ai.bat, and a real service host (both BETA)

**Context:** The user asked for three things in one run: a guided launcher for CLI AI agents
(Claude, Codex, Gemini, Antigravity), ported from Deerpfy's `ai.bat`
(github.com/Deerpfy/ai.bat), added to the shell picker beside Command Prompt and PowerShell as
"CLI-AI Wizard"; the port to live in its own project used as a DLL; and the banner and wrap helpers
"installed as a Windows service" - invisible, but visible in `services.msc`. The whole feature set
is **BETA**; the user verifies it and only then gives the word to push and cut a build.

**Decision, in three parts:**

1. **`BetterTerminal.AIWizard` (DLL) holds the logic**, `BetterTerminal.AIWizard.Cli`
   (`beterm-aiwizard.exe`) is the console front end, and the picker launches the exe in a pane the
   same way it launches a shell. The DLL is pure data and string-building: engine definitions, the
   menu flows transcribed from `ai.bat` with each option's exact flag, the model list read from
   `ai-models.json`, the command composer, the project-root walk to the outermost `.git`, the Git
   Bash lookup and an allow-list sanitiser. The exe reuses `Palette`/`AnsiWriter`/`TerminalMode`
   from Wrap (linked, not copied) and drives the steps, then runs the assembled command through
   `cmd /c` in the resolved directory, inheriting the pane's console **and its job**, so closing the
   pane still takes the agent with it. The wizard header credits Deerpfy.

2. **The exe is reached by name**, exactly like the banner: `ShellProfile.CliAiWizard` carries the
   bare `beterm-aiwizard.exe`, `CommandRegistration` stages it and `BetterTerminal.AIWizard.dll`
   into the per-user `bin` folder on the search path, and the Shell references the Cli project only
   so the exe lands in its output. `CreateProcess` finds it on PATH; nothing quotes a path into a
   command line.

3. **One real Windows service, not two.** The banner runs once and exits and the wrap front end is
   interactive - neither is a service by nature, and a service has no console to give them. So
   `BetterTerminal.Service` (`beterm-service.exe`) is a single `ServiceBase` host with no window,
   visible in `services.msc` as **BetterTerminal Host**, that registers and accounts for the helper
   components and logs its life to the application log. It self-installs with
   `beterm-service.exe --install`.

**The rule this bends, on purpose:** registering a service is machine-wide and needs an elevated
prompt and the service database - against the standing "per user, never elevate" rule
([RULES #security-and-secrets](RULES.md#security-and-secrets)). This is the exception the user chose
after being told the trade-off; **the application itself still never elevates** - only the separate,
manually run `beterm-service.exe --install` does, once. Two framework assemblies were added for it,
`System.ServiceProcess` and `System.Configuration.Install`, and `System.Runtime.Serialization` +
`System.Xml` for the wizard's JSON model file - all part of .NET Framework 4.8, no package (R2 holds).

**Deliberately not ported:** `ai.bat`'s online model-list refresh (models.dev, the API key path,
the checksum/date update gates) and its OAuth account setup. Those are update tooling and a network
feature; the wizard reads `ai-models.json` if present and seeds a small built-in list otherwise, so
building a command needs no network. The model step always offers a custom id, so a stale list never
blocks a run. The per-engine flows are a faithful core subset, structured so a new option is one
line - not every branch of the launcher is present yet (BETA).

**Verified 2026-08-06:** both configurations rebuild **zero-error zero-warning**;
`beterm-aiwizard.exe` and `BetterTerminal.AIWizard.dll` land in the Shell output; `beterm-service.exe`
builds standalone and the Shell does not depend on it. **Not yet verified by a person:** the wizard
running live in a pane, each assembled command actually starting its agent, and the service install
on an elevated prompt - these are the user's BETA pass. Some agent flag mappings are transcribed
from a summary of `ai.sh`, not confirmed against each live CLI.

**Revisit if:** an agent changes a flag (fix the one line in `WizardCatalog`); the model list should
refresh online (that reopens the network-call rule); or the service should do more than register and
account for the helpers.

### 2026-08-05 - The session opens on a mark and four facts, and the prompt says machine and place

**Context:** The one-line greeting was to become a banner: an ASCII mark, the workspace underneath
it and whatever else identifies the session, with the text animating as it appears and a spinner
wherever something waits. The prompt was to read machine name, then the directory measured against
the workspace, then `>>`.

**Decision:** A fifth project, `BetterTerminal.Banner`, builds `beterm-banner.exe`, and the shell
runs it as its first command. The application cannot draw this itself - the screen belongs to the
shell from the moment it starts, and writing into the same grid from outside would race the parser
reading the shell's own output - and the command interpreter cannot animate anything, because its
only wait is a whole second. A small program run by the shell solves both at once and looks
identical in either shell. It is reached **by name**, from the folder the command registration puts
on the search path, so no path is ever quoted into a shell command line; `where /q` and
`Get-Command` keep a machine without it from reporting an error. `Palette.cs`, `AnsiWriter.cs` and
`Spinner.cs` are **linked** into it from the console front end rather than copied, so a colour and
an escape sequence are still each written down once.

**The prompt splits by what each shell can do.** PowerShell computes `/<project>/<folder>` with
forward slashes, and falls back to the whole path when the shell has walked out of the project.
The interpreter's `PROMPT` understands tokens, not expressions: it cannot measure a path against
anything, so it shows `$P` - the real path. A pretty prefix that goes stale after the first `cd`
would be worse than a long one.

**Three defects this cost, all worth remembering:**
`>>` written literally into a `/k` command line is **redirection**: the first run created a file
called `$S` in the project folder and the prompt lost its arrow. `PROMPT` has `$G` for exactly this.
The banner was invisible because the search-path entry was only pushed into **this process** when
the registry value was first written, so every later run started shells that could not find it -
`CommandRegistration.JoinSearchPath` now sets the process variable unconditionally, and that is what
every child inherits. And an executable copied on its own could not load: the banner needs the
interop assembly beside it, so both files are copied.

**Verified 2026-08-05:** a project opened in either shell shows the mark, Workspace, Project, Shell
and Machine, then `KEXO /banner-demo >>`; `cd src` turns that into `KEXO /banner-demo/src >>`.

### 2026-08-05 - The font size box fought every keystroke

`SettingsViewModel.FontSize` clamps to 8-36, and the box committed on **every character typed**, so
"12" was read as "1", snapped to 8, and the next digit made "84", which snapped to 36 - any number
became 36. The box now commits when it is left or when Enter is pressed
(`UpdateSourceTrigger=LostFocus`), and Up and Down step the value, because a box that only reacts to
losing the focus feels broken otherwise. The clamp itself is right and stays where it is: it is the
invariant, not the interaction.

### 2026-08-05 - A pane starts the shell looking like this application, not like a bare interpreter

**Context:** A new pane opened on the interpreter's own version banner and its default prompt. The
user asked for the application's look instead, and chose the smallest of three shapes: no banner,
the application's prompt, one greeting line - the shell still draws its own prompt, we only tell it
what to draw.

**Decision:** `Services\ShellPresentation.cs` returns a copy of a `ShellProfile` whose arguments
carry the presentation, and `TerminalWorkspace.CreatePane` starts that copy. The **name is kept**,
because the saved layout stores it and a restored pane looks the profile up by it. For the command
interpreter: `/k "prompt ... & cls & echo ..."` - there is no switch that suppresses its banner, so
the first thing it is asked to do is clear the screen, and `PROMPT` understands `$E` as an escape,
which is what colours the path. For Windows PowerShell: `-NoExit -EncodedCommand`, because the
script contains quotes, braces and dollar signs and every layer in between would otherwise get a
say in what they mean. The accent comes from `Bt.Color.AccentLight`; when the token is missing the
prompt is simply left uncoloured rather than falling back to a literal.

**Why not our own input line:** it was one of the three options and it breaks everything
interactive - an editor, a pager, a remote shell and every Y/N prompt need characters one at a time.
Telling the shell what to draw costs nothing and keeps all of that working.

**The rule this ran into:** the greeting names the project, and a project name is user text, so it
must not go on a command line ([RULES #security-and-secrets](RULES.md#security-and-secrets)). It
travels in the `BETERM_PROJECT` environment variable, which the shell inherits, and is reduced to
letters, digits, space, dot, dash and underscore before it is set - because an interpreter expands
a variable and *then* parses the result, so a folder called `a&b` would otherwise run `b`.

**Consequences:** the prompt colour is fixed when the session starts, so switching theme recolours
the chrome and the scheme but not the prompt of a pane that is already running. The profile list in
settings shows the real command line, encoded argument and all.

**Verified 2026-08-05:** a project opened with each shell shows the greeting line and an accent
path prompt with no banner, both sitting at a live interactive prompt; both configurations rebuild
warning-clean; two pane-teardown sequences pass on Release with the console-host count unchanged.

### 2026-08-05 - A console front end for the scripts, with hand-written escape sequences and no framework

**Context:** The user asked for a terminal interface over the scripts in `tools`, from a formula
whose build commands (`dotnet build`, `dotnet test`) do not exist here. Three answers settled it: a
fourth classic project in this solution rather than a separate modern-.NET repository, the four
scripts under `tools` as its subject, and a per-script flag deciding when the interface gets out of
the way.

**Decision:** `BetterTerminal.Wrap` builds `beterm-wrap.exe` - .NET Framework 4.8, C# 7.3, x64, no
package, escape sequences written by hand. It enables escape-sequence processing through
`SetConsoleMode` (a P/Invoke that went into `BetterTerminal.Interop`, per R10), switches both console
directions to UTF-8, and draws whole frames into a `StringBuilder` flushed in one write. A script is
either **streamed** - started with both pipes redirected and both drained, output into a 5000-line
scrollback - or **marked as taking the terminal over**, which withdraws the alternate screen and
gives the child the real console. Two things need the second mode: anything interactive or
full-screen, and `flood-benchmark.ps1` and `session-cycle.ps1`, which start their own shell and
measure nothing at all if it inherits a pipe. The verification gate is a warning-clean rebuild plus a
real run; there is no test project, and adding one would need a package.

**Why the flag rather than detection:** whether a child will take the terminal over has to be known
*before* it starts, and the only evidence - what it writes - is what pass-through gives away. A flag
per script is a line of transcription and is always right.

**Consequences:** `ScriptCatalog.cs` transcribes each script's `param` block, so a script that
changes its parameters has to be followed there. The scripts themselves are never read or written.

**Redrawn 2026-08-05 as a proper interface.** `Palette.cs` transcribes the dark theme's colours so
the two programs read as one product, `AnsiWriter` gained filled panels with box-drawn borders, a
scroll thumb and true-colour fills, and `InputField.cs` replaced the hand-rolled field: a caret that
moves, a value that scrolls under it, a hint while it is empty and an accent edge when it has the
focus. It is deliberately **not** `Console.ReadLine`, which owns the screen while it waits - the
loop has to stay free to redraw and to notice a child exiting. The argument screen now also shows
the command line it is about to issue, and the output screen a spinner and an elapsed clock.
Checked by replaying a rendered frame onto a grid in memory (`render-frame.ps1` in the session
scratchpad) rather than by opening a window - layout, clipping and the narrow-terminal case are all
visible that way without taking anyone's focus.

**Verified 2026-08-05, in a classic console:** `capture-window.ps1` run through the interface
returned **exit code 1** with the application closed and its PowerShell error visible - so stderr is
drained - and **exit code 0** with it running, writing the PNG it was asked for;
`flood-benchmark.ps1` withdrew the interface, wrote its own coloured output straight to the console,
and the interface came back with the exit code; a window resize redrew the frame at the new size with
no leftovers; and quitting left the shell that started it with its scrollback intact, a visible
cursor and no stray escape sequence. One defect was found and fixed this way: redirected output was
being decoded in the machine's OEM code page, which put a bar through the accent in "Terminál" -
a child inherits the console code page, and this program had already set that to UTF-8.

### 2026-08-05 - The first run installs a copy under the user profile, and the command runs that

**Context:** The command written by the first version of `CommandRegistration` embedded the absolute
path of the build output. Running `beterm` produced *"Windows cannot find D:\Multi Termin?l
Window\...\BetterTerminal.exe"* - the `á` had been decoded in a code page that was not the one the
file was written in. Writing the file in the OEM page fixed the default console but not one that had
been switched to 65001, and it did nothing about the deeper problem: the command pointed at a build
folder that can be deleted, moved or renamed.

**Decision:** `Services\SelfInstall.cs` copies `BetterTerminal.exe` and its two DLLs into
`%LOCALAPPDATA%\BetterTerminal\app` on every start, refreshing any file the running build is newer
than, and the shim in the sibling `bin` folder reaches it as **`%~dp0..\app\BetterTerminal.exe`**.
A process already running from the install folder skips the copy; a copy that cannot be replaced
because it is running is left alone and updated by the next start.

**Why:** the script now contains **no absolute path and no byte above 127**, so no code page can
mangle it, whatever the repository folder or the user profile is called. It also makes the command
independent of the build output: `beterm` keeps working after the working copy is moved or deleted.
Per user, no elevation, no `Program Files`, no uninstall entry - consistent with the app never
elevating.

**Alternatives rejected:** writing the script in the OEM code page - fixes one console setting out
of several. A short path (`GetShortPathName`) - 8.3 names can be disabled per volume. Relaunching
the installed copy and exiting the original - surprising when you just started a build from the IDE.

**Verified 2026-08-05:** with `%LOCALAPPDATA%\BetterTerminal` deleted, one launch of the Debug build
recreated `app\` with three files and a 225-byte shim containing zero non-ASCII bytes; `beterm` then
opened the app from a folder named `projekt-ěščř` **with the console switched to 65001**, from both
cmd and PowerShell, with the accented folder name correct in the pane header, the status badge and
`project.json`; the installed copy passes the pane-teardown smoke sequence.

### 2026-08-05 - Make the app reachable as `beterm`, give a folder a `.beterm` project, and add saved connections

**Context:** The user asked for five things in one run: the application should register itself as a
command usable from a command prompt or PowerShell; running it that way should adopt the current
directory as the workspace and keep that project's settings in a hidden `.beterm` folder; the user
should be able to define their own commands and store values there; a header button in the primary
colour should open saved remote connections, stored per user rather than per project, with a
reachability indicator; and the settings save/load path should be complete.

**Decision, one piece at a time:**
1. **Registration is per user and best effort.** `Services\CommandRegistration.cs` writes
   `%LOCALAPPDATA%\BetterTerminal\bin\beterm.cmd` - three lines that `start` the executable with
   `--project "%CD%"` - and joins that folder to `HKCU\Environment\Path`. No installer, no `HKLM`, no
   elevation. The stored path is read and written **unexpanded, with its original value kind**,
   because reading it through the expanded managed view and writing it back would freeze every
   `%USERPROFILE%` style entry in the user's own path into a fixed one. A managed
   `SetEnvironmentVariable` call for `BETERM_HOME` follows, purely because that is what announces the
   change to the desktop without a P/Invoke of `SendMessageTimeout` in the Shell (which R10 would
   send to the Interop project).
2. **A project is a folder plus `.beterm\project.json`,** created and hidden on first open, holding
   the project name, its shell, a startup line, the user's own commands and their named values. The
   project's commands are prepended to the command palette in a "Workspace" group and are **typed
   into the focused session**, never spliced into a child command line.
3. **The setup window is shown on first open and afterwards while its own toggle stays on** - the
   toggle, defaulting to on, is what reconciles "run the workspace setup when opened this way" with
   not making a dialog appear forever. It is **posted to the dispatcher**, not called inline, because
   `Restore()` runs inside the window's `Loaded` handler and a blocking dialog there leaves the
   splash screen on top of it.
4. **Saved connections are per user, in `%APPDATA%\BetterTerminal\connections.json`,** and store a
   user name and an address - nothing else, deliberately: authentication belongs to the shell's own
   client, so there is no field a password could be put in. The chosen connection opens either a pane
   in the grid or a `SessionWindow`, with `ssh <user>@<host>` written into the session as input.
5. **The reachability heart is a real TCP connect to port 22**, 2 s timeout, on a pool thread,
   reported back through the dispatcher. This **supersedes the "the app makes no network calls" rule**
   ([RULES #security-and-secrets](RULES.md#security-and-secrets)) - it is the user's explicit request,
   it sends no bytes and stores nothing, and it runs only while the connections window is open.
6. **Settings are written when they change,** not only on window close, and now include the window
   placement. `JsonFile.cs` became the single read/write path for all three stores.

**Why the seams are where they are:** the connections view model raises `Changed`,
`RefreshRequested` and `ConnectRequested` and does nothing else; `TerminalWorkspace` owns the files,
the probing, the sessions and the windows. That keeps the standing rule that a view model never
creates a session, opens a window or reads a store.

**Alternatives rejected:** an installer or a machine-wide path entry - needs elevation, which this
app never asks for. Storing connections in the project folder - the address book follows the person,
and a project file may be committed. A ping instead of a port check - answers a different question
and says nothing about whether a shell would connect. Deriving the project folder from the process
working directory instead of an argument - a shortcut, a scheduled task or an explorer launch would
each mean something different by it.

**Consequences:** three encoding and automation traps are now written down in
[TIPS #gotchas](TIPS.md#gotchas); the most expensive was that the command interpreter reads a `.cmd`
in the **OEM** codepage while `Encoding.Default` is the **ANSI** one, so the first shim mangled the
`á` in the repository path and `beterm` silently started nothing at all. The word "SSH" is now the
one allowed protocol name in user-visible text, [RULES #hard-rules](RULES.md#hard-rules).

**Verified on 2026-08-05:** both configurations rebuild zero-error zero-warning; `beterm` typed in a
folder opens the app there and creates the hidden `.beterm` folder; the connections window shows a
filled green heart for a host that answered on port 22 and a red outline for two that did not;
connecting typed `ssh root@127.0.0.1` into a new pane in the grid and, on the second run, into a
separate session window; four pane-close sequences pass on Release with the console-host count
unchanged (6 before, 6 after).

**Revisit if:** a second window instance is wanted per project (today a second `beterm` opens a
second application window and both write the same `workspace.json` on close), or if connections grow
a port field - the reachability probe hardcodes 22 today.

### 2026-08-04 - Replace the hand-built shell UI with the imported design system

**Context:** The user had a finished WPF UI layer in a Claude Design project ("BetterTerminal logo a
GUI", id `1b0bb5a7-4121-4d6c-8818-b0067eddd7f1`) already targeting .NET Framework 4.8, and asked to
import it and wire every function to the real terminal.
**Decision:** The designed shell became the UI and **the terminal core was kept untouched**. The
bundle's `MainWindow`, `CommandPalette`, `SettingsWindow`, `AboutWindow` and `SplashWindow` replaced
the hand-built chrome; `TerminalPane` and `SplitPane` were **deleted**, replaced by
`Views\TerminalSurface.cs` (a session host with no chrome), a view-model pane tree under
`ViewModels\`, and `Services\TerminalWorkspace.cs` as the wiring core. The bundle's About and Splash
text named the pseudo-console API, so the backend row now reads **"Virtual terminal"** or **"Hosted
console window"**.
**Why:** The bundle already shipped settings, palette, about and splash surfaces the hand-built shell
lacked, and it is the design source of truth - importing it whole gains features and keeps the UI
traceable. Keeping the core untouched is what made the swap safe.
**Alternatives rejected:** Keep the old chrome, import only the token dictionaries - throws away
those four surfaces. Rewrite the bundle to match the old code-behind layout - more work, and it
drifts from the design source immediately.
**Consequences:** The pane tree is now **view models rendered by implicit `DataTemplate`s**, so a
split needs two concrete types - `ColumnSplitViewModel` and `RowSplitViewModel` - because a template
keys off type, not off a property. `ThemeService` owns `MergedDictionaries` **slots 1 and 7** (theme
and scheme) and **nothing else may touch `MergedDictionaries`**. Appearance - theme, scheme, font
family, size, cursor shape, blink - now persists in `workspace.json` beside the layout. Three bundle
defects were fixed on import, each the kind that silently breaks a WPF build:
`Background="{StaticResource Bt.Color.*}"` bound a `Color` where a `Brush` is required (wrapped in
`SolidColorBrush` in `SettingsWindow` and `SplashWindow`); `x:Name="Cursor"` collided with
`FrameworkElement.Cursor` and produced **CS0108** (renamed `CaretBlock`); and `DisplayMemberPath`
does not work with the bundle's templated `ComboBox` - it rendered the type name until an explicit
`ItemTemplate` was added.
**Revisit if:** the design project ships a new bundle - re-import, re-applying these three fixes and
the backend wording unless they are fixed upstream.

### 2026-08-04 - Document the project with the md-orchestrator skill, one agent per file

**Context:** The project was code plus a short `RULES.md` - no entry point, no map, no record of why
anything was as it was. The `md-orchestrator` skill was at `.claude\skills\md-orchestrator\`.
**Decision:** Run `md-orchestrator`, producing a fixed root markdown set (CLAUDE, STRUCTURE, RULES,
WORKFLOWS, TIPS, DOCS, AGENTS, MEMORY), each written by one agent that writes that file and no other.
The old `RULES.md` was **archived** to `docs\_archive\2026-08-04\RULES.md`, not overwritten. Three
answers from the user bind every file: (1) **git plus GitHub is planned but not initialised** - rules
written for that future, `.gitignore` covers `bin/`, `obj/`, `.vs/`, `*.user`, `*.suo`,
`docs/_archive/*/flood.txt`; (2) the **README targets an outside developer**; (3) the **verification
scripts moved from the scratchpad into `tools\`**, making them runnable procedures.
**Why:** One agent per file keeps each file's scope coherent and stops agents overwriting each other;
archiving preserves the only prior written statement of intent. The questions had to come first
because each changes what the documents say - a README for the author is a different document from
one for a stranger.
**Alternatives rejected:** One agent writing everything - loses focus, later files truncated as its
budget runs out. Deleting the old RULES.md - destroys evidence. Guessing - aims at the wrong reader.
**Consequences:** The root markdown set is the contract and changes land there. Assistants still may
not run any git command unless the user names it, [RULES #git-rules](RULES.md#git-rules).
**Revisit if:** the repository is initialised under git, when history carries some of this.

### 2026-08-04 - Remove external API and package names from user-visible text; shorten path-like pane titles

**Context:** The UI surfaced implementation vocabulary - backend labels naming the console API in
pane headers and status text - and the user objected explicitly. Separately, `cmd.exe` reports its
own full image path as its console title, so panes were labelled with a long path.
**Decision:** No external API, framework or package name - the pseudo-console API, the console host,
WPF, Win32 - may appear in window chrome, pane headers, status bars, palette entries or any
user-visible message; they live in code comments and documentation only. A session title that is a
path with no spaces is displayed as its file name alone.
**Why:** Product surfaces describe what the user is doing, not what the program is made of. Same for
the title.
**Alternatives rejected:** A debug toggle - a second code path for what a debugger gives.
Middle-truncating long titles - still a path, just unreadable.
**Consequences:** Every new user-facing string must be checked against this hard rule,
[RULES #never-do](RULES.md#never-do). It is why the imported About and Splash text had to be changed.
**Revisit if:** the user asks for a diagnostic surface - an opt-in view, never chrome.

### 2026-08-04 - Fix the pane-close crash by fixing teardown order (the entry that matters most)

**Context:** Opening a second console pane and closing it killed **the entire application**, every
other pane with it, with exit code **0xE0434352** - the CLR's unhandled-managed-exception code, which
says nothing about the cause. The window simply vanished.
**Diagnosis path, the reusable part:** nothing showed in the debugger because the failure was on a
background thread. The answer came from the **Windows Application event log**, whose .NET Runtime
entry carried the full stack: an unhandled `System.ArgumentNullException` from `SemaphoreSlim.Wait`,
inside `BlockingCollection.GetConsumingEnumerable`, on the `ConPtySession` **writer thread**.
`Dispose()` had disposed the input queue while that thread was blocked inside it, so the collection's
internal semaphore went null under a waiting thread - and an exception escaping a background thread
terminates the process, hence one pane taking everything.
**Decision:** Rewrite session teardown in this exact order and adopt four supporting rules, all five
load-bearing:
1. **Order:** `CompleteAdding()` → close the pseudo console and the job object (this kills the client
   and breaks the pipe, which is what actually unblocks a reader parked on a read) → `Join()` both IO
   threads with a **2 s timeout** → only then dispose the streams, exit event, process handle and
   queue. Anything whose thread missed the timeout is **left to its finalizer**.
2. Reader and writer threads **copy their `FileStream` into a local** before looping, so a field
   nulled during dispose cannot cause a `NullReferenceException` on a background thread.
3. Both IO threads **catch broadly** and convert any failure into an `Exited` event with a reason.
4. `Write` and `Resize` **return early once disposed** - resizing a closed pseudo-console SafeHandle
   throws `ObjectDisposedException`, and that one lands on the UI thread.
5. Pane close calls `TerminalRenderer.Detach()` **first**, so the render timer and subscriptions are
   gone before what they observe is dismantled. (Written in `TerminalPane`; since the design import
   it lives in `Views\TerminalSurface.cs`.)
**Why:** The principle the bug taught: **never dispose a synchronisation primitive a thread may be
blocked on - stop the thread first, by removing its reason to block, not by yanking the object out
from under it.** Closing the pseudo console and the job is what makes the threads finish;
`CompleteAdding` and `Join` merely observe it. The timeout exists because a hung thread must not turn
a clean close into a hang or a second crash - leaking to the finalizer beats crashing.
**Alternatives rejected:** A broad try/catch round the writer loop - hides the symptom, leaves the
race. Aborting the IO threads - `Thread.Abort` corrupts state mid-write on a pipe. Disposing nothing -
the application outlives panes, so handles accumulate for the life of the window.
**Consequences:** These five are rules, not preferences, [RULES #code-rules](RULES.md#code-rules);
any new background thread in a session takes the same shape. Regression coverage is the UI-automation
sequences `New tab|Close pane`, `Split right|Close pane|Close pane`, `Split down|Split right|Close
pane x3`, `New tab x2|Close pane x3`, `Split right|New tab|Split down|Close pane x4` - run them via
`tools\ui-smoke.ps1` after any change to session lifetime, [WORKFLOWS #testing](WORKFLOWS.md#testing).
**Revisit if:** session IO moves to async/await or a channel abstraction, which changes what blocks
and therefore what must stop first. The five sequences must still pass afterwards.

### 2026-08-04 - Parse on the reader thread, render on a 16 ms timer, queue input to a writer thread

**Context:** A terminal must absorb output far faster than a human reads while staying responsive to
typing. The naive WPF shape - marshal every chunk to the UI thread - had to be ruled in or out first.
**Decision:** Three-way split. The **reader thread** decodes UTF-8 incrementally and runs the VT
parser straight into the `CellGrid`, off the UI thread. The renderer repaints on a **16 ms**
`DispatcherTimer`, only when the reader has flagged new output or a full redraw is pending. Input is
queued on a `BlockingCollection<byte[]>` drained by a **writer thread** so the UI never blocks on a
full pipe. The grid is shared under `SyncRoot`; per-line version stamps let the renderer redraw only
changed rows.
**Why:** Per-chunk marshalling to the UI thread is the classic way to build a terminal that drops
input under load - the dispatcher queue becomes the bottleneck and typing queues behind output.
Decoupling at 16 ms makes output volume cost parse time, not frame time: a flood coalesces into one
repaint per frame instead of thousands. The writer thread is the symmetric case.
**Alternatives rejected:** Marshalling every chunk to the UI thread - above.
`CompositionTarget.Rendering` - repaints every frame regardless. Synchronous writes from the UI
thread - a full pipe becomes a frozen window.
**Consequences:** The grid is touched under `SyncRoot` only, permanently. The 1.03-1.64 MB/s figures
are a property of this design; the teardown complexity above is its price.
**Revisit if:** the flood benchmark drops below roughly 1 MB/s on Release, or latency is perceptible.

### 2026-08-04 - One job object per session with KILL_ON_JOB_CLOSE

**Context:** Each session spawns a shell, and in the fallback path a console host too. Panes, tabs
and the window close independently and the application can be killed outright - each path risked
leaving a shell running with no window attached.
**Decision:** `ProcessJob` creates a Win32 job object per session with
`JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE` and assigns the launched process to it. When the job handle
closes - for any reason, including a hard kill - the OS kills everything in the job.
**Why:** This puts orphan prevention in the kernel rather than the shutdown path. Managed cleanup
only runs when shutdown is orderly; the job also covers the case where it is not.
**Alternatives rejected:** Tracking child PIDs - does not run if the parent is killed, and races PID
reuse. Relying on the shell noticing its pipe closed - not guaranteed, useless for the console host.
**Consequences:** Every launched process must be assigned to a job,
[RULES #hard-rules](RULES.md#hard-rules). The job handle's lifetime *is* the session's, which is why
closing the job sits mid-teardown: it is what makes the IO threads finish.
**Revisit if:** a child must outlive its pane - launch it outside the job, never weaken the rule.

### 2026-08-04 - The fallback backend launches conhost.exe with the shell as its argument

**Context:** The fallback backend finds a real classic console window and reparents it into a pane.
The obvious way to get one is to start the shell with `CREATE_NEW_CONSOLE`.
**Decision:** Launch `conhost.exe "<shell>"` explicitly instead, then find the `ConsoleWindowClass`
window by process ID with `EnumWindows` and reparent it with `SetParent`.
**Why:** On Windows 11 a plain `CREATE_NEW_CONSOLE` can be **handed off to the configured default
terminal application**. If that is not the classic console host, no `ConsoleWindowClass` window is
created and there is nothing to reparent - the backend silently produces an empty pane on exactly the
machines it exists to support. Naming `conhost.exe` opts out of the handoff.
**Alternatives rejected:** `CREATE_NEW_CONSOLE` and hope - fails wherever the default terminal has
been changed. Telling the user to change that setting - an application must not demand it.
**Consequences:** The path depends on `conhost.exe` still accepting a command-line argument. This
backend's `Write` throws `NotSupportedException` **by design** - there is no supported way to inject
input into another process's console - and `OutputReceived` never fires (avoiding CS0067).
**Revisit if:** the fallback runs on a real pre-17763 machine (it has not), or `conhost.exe` changes.

### 2026-08-04 - Select the backend with RtlGetVersion, not Environment.OSVersion (a real bug, caught at runtime)

**Context:** The pseudo-console path requires Windows 10 build 17763+, so `ConPtySession.IsSupported`
gates on the OS build. The first version asked `Environment.OSVersion`, and **on first launch the
application silently used the fallback console-window backend** on Windows 11 - no error, no warning.
**Decision:** Query `RtlGetVersion` from `ntdll.dll` and trust that for the backend check. An
`app.manifest` was added too (PerMonitorV2 DPI, Windows 8.1/10/11 `supportedOS`), but the ntdll call
is what the check trusts.
**Why:** .NET Framework's `Environment.OSVersion` reports **6.2** - Windows 8 - for an application
without an OS compatibility manifest, regardless of the real OS; that version-lie is a compatibility
shim applying to the whole managed API. `RtlGetVersion` is the documented, unshimmed way.
**Alternatives rejected:** Only the manifest's `supportedOS` entries - makes a correctness-critical
check depend on an XML entry someone could prune while tidying. Calling `CreatePseudoConsole` and
catching failure - exception control flow. The registry - undocumented for this purpose.
**Consequences:** `ntdll.dll!RtlGetVersion` is in the interop audit surface and any future OS-version
check must use it, [TIPS #environment-quirks](TIPS.md#environment-quirks). The lesson generalises:
**a silent fallback is worse than a failure**, because it looks like it worked.
**Revisit if:** the project ever moves off .NET Framework, where `OSVersion` is not shimmed this way.

### 2026-08-04 - One session contract (ITerminalSession) with two backends behind it

**Context:** Two entirely different mechanisms had to produce "a terminal in a pane": the modern
pseudo console, and reparenting a real console window on older builds. Without a common shape, every
piece of UI would have had to know which one it was talking to.
**Decision:** Define `ITerminalSession` - `Title`, `IsRunning`, `ExitCode`, `Columns`, `Rows`; events
`OutputReceived`, `TitleChanged`, `Exited`; methods `Start(ShellProfile, workingDirectory)`,
`Write(string)`, `Resize(columns, rows)`, `Close()`, plus `IDisposable` - implemented twice, in
`ConPtySession` and `HwndConsoleSession`, with `TerminalSessionFactory.Resolve` choosing. Exactly one
place in the UI branches on backend - `TerminalPane` then, `Views\TerminalSurface.cs` now.
**Why:** Backend choice is an environment detail and environment details must not propagate. This
seam is also what made the design import cheap: the entire UI was replaced without the sessions
noticing.
**Alternatives rejected:** Two parallel UI paths - doubles every future UI change and guarantees
drift. Only the pseudo-console backend, refusing to start on older builds - narrows supported
machines for no gain. An abstract base class - the two share essentially no implementation.
**Consequences:** The interface is the load-bearing seam: widening it changes both backends, and
`HwndConsoleSession` must answer for members it cannot honestly implement (hence `Write` throwing),
[STRUCTURE #boundaries](STRUCTURE.md#boundaries).
**Revisit if:** a third backend appears, or pre-17763 support stops mattering - then
`HwndConsoleSession`, `ConsoleHwndHost` and the one UI branch can all go.

### 2026-08-04 - Classic non-SDK csproj files with zero NuGet packages

**Context:** The project had to build with MSBuild where the task forbade installing anything, and no
package may be added unless the user names it.
**Decision:** Classic (non-SDK-style) `.csproj` for all three projects and **zero NuGet packages** -
no `packages.config`, no `PackageReference`, no restore step. Framework references plus P/Invoke.
**Why:** SDK-style WPF on .NET Framework requires a NuGet restore to resolve the WPF targets, meaning
network access and installed packages - both excluded. Classic projects build with `msbuild` alone,
offline, and zero packages means zero supply-chain surface in a project built on raw Win32.
**Alternatives rejected:** SDK-style csproj - needs restore. A vendored packages folder - dependency
management with none of the tooling. A native C++ project - a toolchain for what P/Invoke expresses.
**Consequences:** New source files must be added to the `.csproj` explicitly - classic projects do
not glob, and the design bundle's files were added by hand for that reason. Adding any package
violates [RULES #hard-rules](RULES.md#hard-rules) unless the user names it.
**Revisit if:** the user names a package, or the project moves where SDK-style is the only option.

### 2026-08-04 - Pin .NET Framework 4.8, C# 7.3, WPF, x64 only

**Context:** The directory was empty on 2026-08-04 - nothing pinned a framework, language version or
platform, so all of it was a free choice that would immediately become expensive to reverse.
**Decision:** .NET Framework **4.8**; C# language version **7.3**; **WPF**; **x64 only** - both
`Debug|x64` and `Release|x64`, no x86 and no AnyCPU. Minimum OS for the pseudo-console path is
Windows 10 build 17763.
**Why:** 4.8 and 4.7.2 reference assemblies were both installed and 4.8 is the newer of the two
allowed. x64 only because `SetWindowLongPtr` and the IntPtr-sized fields in the Win32 structs behave
differently on x86. C# 7.3 follows from the framework choice, not a separate decision.
**Alternatives rejected:** .NET Core / 5+ - different deployment story for WPF. WinUI - a different
UI stack with its own packaging. Avalonia - a package dependency, excluded. AnyCPU - would silently
run 32-bit in some hosts and break the interop assumptions.
**Consequences:** **No C# 8+ syntax anywhere** - no nullable reference types, no switch expressions,
no using declarations; the most common way a well-meaning edit breaks the build,
[RULES #code-rules](RULES.md#code-rules), [TIPS #gotchas](TIPS.md#gotchas). The design bundle already
targeted 4.8, which is why it could be imported at all.
**Revisit if:** a framework change or 32-bit host requirement appears - either invalidates the interop layer.

## Open threads

**1. No test project exists.** *Stopped at:* `vstest.console.exe` is installed but has nothing to
run; all verification is manual or script-driven. *Next step:* decide what is unit-testable without a
live console - `VtParser` and `CellGrid` are the candidates, pure state machines needing no process.
*Blocker:* that normally means a test-framework package, forbidden unless the user names one.

**2. There is no `md-sync` skill - only a command standing in for one.** *Stopped at:*
`md-orchestrator` is vendored at `.claude\skills\md-orchestrator` and `/md-sync`, `/md-audit` and
`/md-recall` exist under `.claude\commands\`, but the md-sync *skill* is still not installed, so
`/md-sync` means re-running md-orchestrator in SYNC mode. *Next step:* install a real md-sync skill,
or leave the command as the documented form. *Blocker:* the skill is not available on this machine.

**3. Git - DONE 2026-08-05.** The repository was initialised at the user's request and pushed to
`https://github.com/TruXe/BetterTerminal`, a **private** repository whose own first commit (README
and MIT licence) is the parent of the import. `.github/workflows/build.yml` rebuilds both
configurations with `/warnaserror` on every push and, on a `v*` tag, zips the Release output and
publishes it as a release using the token the run is given. *Open:* the `v1.0.0` run could not be
watched from here - the repository is private, so the API needs a token, and reading the one in the
credential store is blocked. Confirm the run and the release in the browser.

**4. Archive index - DONE 2026-08-04.** `docs\_archive\2026-08-04\ARCHIVE-INDEX.md` now exists, with
a claim-coverage table showing nothing was lost from the archived `RULES.md`. Kept as the record that
the skill's index rule is satisfied. See [DOCS #archive](DOCS.md#archive).

**5. Scrollback size, shell profiles and backend choice are not user-configurable.** *Stopped at:*
`DefaultScrollbackLines = 5000` is a compile-time constant, the shell list is built in code, and
`Resolve` decides the backend with no override. The design import settled the *where*: appearance
already persists in `workspace.json`, so these belong beside it. *Next step:* scrollback is cheapest.
*Blocker:* none. A backend override must survive the no-API-names rule - use the About wording.

**6. The interactive verification pass the user owns.** *Stopped at:* automated and scripted checks
done, human-in-the-loop ones not. *Next step:* run the five items at the end of *Current state*
against the Release build. *Blocker:* none; it needs a person at the keyboard.

**7. Four settings pages are navigation entries with no content.** *Stopped at:* Profiles, Keyboard,
Panes and tabs and About appear in the settings navigation, but the bundle shipped only the
Appearance page, so selecting them shows nothing. *Next step:* build each page against the settings
view models already under `ViewModels\`, Profiles first - it is the one backed by real state
(`ShellProfile`). *Blocker:* none; the bundle simply did not include them.

## Failed experiments

**Running the verification harness in a process with redirected standard output.** The first attempts
to drive `ConPtySession` from PowerShell produced an **empty cell grid** - session started, shell ran,
nothing appeared. It looked exactly like a broken pseudo-console implementation and was treated as
one for a while. It was not: when the hosting process has **redirected standard handles**, the child
shell inherits them and writes into them instead of into the pseudo console, so the grid legitimately
stays empty. Fix: run the harness with real, unredirected console handles - `Start-Process powershell
-WindowStyle Hidden -Wait`, writing results to a log file. This is a property of the *harness*, not
the product: a GUI application has no standard handles to inherit. `tools\flood-benchmark.ps1` and
`tools\session-cycle.ps1` both carry the requirement - [TIPS #gotchas](TIPS.md#gotchas),
[WORKFLOWS #debugging](WORKFLOWS.md#debugging).

**Hardcoding the repository path inside a PowerShell `-File` script.** Scripts containing the literal
path `D:\Multi Terminál Window` failed with "path does not exist" though the path plainly existed:
**PowerShell 5.1 reads a `-File` script as ANSI unless the file carries a UTF-8 BOM**, and the
directory name contains a non-ASCII character (the `á` in `Terminál`), so the path was mangled at
parse time before any file system call. Fix: **pass the path in as a parameter**, which every script
in `tools\` now does. A BOM also works, but the parameter survives the file being copied, re-saved or
generated by a tool that emits no BOM. See [TIPS #environment-quirks](TIPS.md#environment-quirks).

## Glossary

- **Pane** - one live terminal session with its own rendering surface; the smallest unit the user
  creates, focuses and closes. Since the design import it is `PaneViewModel` over `TerminalSurface`.
- **Split** - dividing a pane's area in two, side by side or stacked. Splits nest: the pane tree is
  view models (`ColumnSplitViewModel`, `RowSplitViewModel`) rendered by implicit `DataTemplate`s.
- **Tab** - one entry in the tab strip holding one pane tree (`TabViewModel`).
- **Session** - a running shell process plus everything kept for it: handles, job object, IO threads,
  grid. Expressed by `ITerminalSession`. Not the same as a pane - the pane is the UI.
- **Backend** - which of two mechanisms a session uses to be a terminal, chosen at creation by
  `TerminalSessionFactory.Resolve`. **Never named in the UI**, which says "Virtual terminal".
- **Pseudo console** - the Windows API (build 17763+) giving a process a console device whose input
  and output are pipes, so a host can drive a shell and read its output as bytes. The default backend.
- **Console host** - `conhost.exe`, the classic console window process; the fallback backend launches
  one deliberately so it has a real window to reparent.
- **Cell grid** - `CellGrid`: the screen as `TerminalCell[][]` plus scrollback plus an alternate
  screen. Shared between parser and UI threads; **every mutating member requires `SyncRoot`**.
- **Scrollback** - ring buffer of lines scrolled off the top. Capped at 5000, not user-configurable.
- **Alternate screen buffer** - a second, non-scrolling screen that full-screen applications (vim,
  less) switch to so they do not disturb the scrollback, switching back on exit. DEC 47/1047/1049.
- **SGR** - Select Graphic Rendition: the `CSI ... m` sequences setting colour, bold, underline.
- **OSC** - Operating System Command: escapes carrying string payloads; OSC 0 and 2 set the title.
- **CSI** - Control Sequence Introducer, `ESC [`: prefix for most cursor, erase and scroll sequences.
- **DEC private mode** - the `CSI ? <n> h` / `l` sequences that toggle behaviour rather than draw.
  Implemented: 1, 7, 25, 47/1047/1049 (alternate screen), 1048, 2004 (bracketed paste).
- **Bracketed paste** - DEC private mode 2004: pasted text is wrapped in markers so the shell can
  tell pasted from typed input and not execute it line by line.
- **Job object** - Win32 kernel object owning a group of processes; used with `KILL_ON_JOB_CLOSE`,
  one per session, so no shell or console host outlives its pane.
- **HwndHost** - the WPF class letting a raw Win32 window live in the visual tree; `ConsoleHwndHost`
  subclasses it to embed the reparented console window.
- **GlyphRun** - the low-level WPF text primitive the renderer draws with, one `DrawingVisual` per
  row. Chosen for speed and exact cell metrics, measured from the typeface.
- **workspace.json** - the persisted state at `%APPDATA%\BetterTerminal\workspace.json`: tabs, split
  tree, ratios, shell name, working directory, and since the design import the appearance settings.
- **Design bundle** - the imported WPF UI layer from the Claude Design project; its dictionaries live
  under `Themes\`, its windows under `Views\`, its view models under `ViewModels\`.
- **`ThemeService`** - owns `MergedDictionaries` slots 1 (theme) and 7 (scheme); nothing else may.
- **`TerminalWorkspace`** - `Services\TerminalWorkspace.cs`, the wiring core: it connects the designed
  shell's commands to real sessions.
- **`ConPtySession`** - default backend class; owns the pipes, pseudo console, grid, parser and the
  reader and writer threads. The class the teardown-order rules are about.
- **`HwndConsoleSession`** - fallback backend class; launches and reparents a real console window.
  Its `Write` throws `NotSupportedException` by design and its `OutputReceived` never fires.

---
[← CLAUDE.md](CLAUDE.md) · [STRUCTURE](STRUCTURE.md) · [WORKFLOWS](WORKFLOWS.md) · [MEMORY](MEMORY.md)
