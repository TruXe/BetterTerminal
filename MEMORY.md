---
updated: 2026-08-16
scope: Why BetterTerminal is built the way it is - decisions, unfinished threads, dead ends and vocabulary, for a reader returning cold.
stability: evolving
sources: [session context packet 2026-08-04, source under D:\Multi TerminĂˇl Window after the design import, docs/_archive/2026-08-04/, tools/*.ps1, user answers]
owner_agent: memory-agent
---

# MEMORY

Everything here happened on 2026-08-04, and there is no git history to mine. If this file disagrees
with the code, the code wins - then fix it.

## Current state

**Update 2026-08-15 (1.4.13).** An address printed into a pane is something you can click. A program
can declare its own links, and the terminal also finds http, https, ftp, file, mailto and bare
`www.` hosts in plain text. The pointer underlines the whole link, across a wrapped row boundary as
one link, **Ctrl+Click** opens it, a right click over one offers to open or copy it, and the command
palette lists every link on screen and opens one by index. Exactly one path opens anything, and it
checks the address kind against a list at the moment of opening rather than when the link was found.
A new **Links** page in the settings window holds the four settings, and `BetterTerminal.Tests` is
the first test project this repository has had. Its [decision-log](#decision-log) entry of 2026-08-15
has the reasoning and what it cost. **Published on 2026-08-16** as release `v1.4.13`; it had been
sitting unpushed and untagged until then, which the newest entry explains.

**Update 2026-08-12 (1.4.12).** Scrolling in a pane that keeps writing works again:
the position anchor added in 1.4.10 was overruling the reader in both directions, so a wheel notch
down ended higher than it started. A downward gesture now suspends the anchor for about half a
second and the drift is folded in before the reader's own move. **1.4.11 as published carries the
fault**; the newest [decision-log](#decision-log) entry has the measurements.

**Update 2026-08-12 (1.4.11).** The history is **1 000 000 lines** per pane, not 5000.
That needed the history to stop being laid out in advance: the ring grows on demand and a line is
stored at the width it was written, so an idle pane costs nothing and a full million measures 693 MB
instead of about 2 GB. The newest [decision-log](#decision-log) entry has the measurements.

**Update 2026-08-12 (1.4.10).** Three fixes in the terminal surface and the caption strip: the view
stays on the lines being read while the session keeps writing (the scroll offset was measured from the
live bottom and drifted a line per line of output), Ctrl+V reaches the running program when the
clipboard holds a picture instead of being swallowed, and a **Check for Updates** button runs the
update probe on demand and answers either way. The newest [decision-log](#decision-log) entry has the
measurements.

**Update 2026-08-09 (1.4.9).** The application can put itself in the menu a folder shows on a right
click - named, with its icon, opening that folder as a project - and a new **Integration** page in
the settings window turns it on and off. It is off until asked for, per user, and the registry is the
only record of it. The newest [decision-log](#decision-log) entry has the detail and the proof.

**Update 2026-08-08 (1.4.3).** The application checks the release feed itself, at start and then
hourly, and shows its own corner notice - so a new version is announced at once instead of only when
the service polls. 1.4.1 was silent because the application only listened on the service's pipe and
never checked on its own; the shared `ReleaseFeed`/`UpdateDownloader` now serve both. Verified by
launching the built app against the test feed: the styled notice appeared on start with no service
involved.

**Update 2026-08-08 (1.4.2).** The service now updates and notifies on its own when nothing of ours
is running: it shows a message in the user's session with `WTSSendMessage` and installs the update
there with `CreateProcessAsUser`, so an update lands even with the application closed. The
application's own corner notice still handles the running case. README carries shields.io badges. The
newest [decision-log](#decision-log) entry has the detail.

**Update 2026-08-08 (1.4.1).** Self-update from the latest GitHub release, driven by the Windows
service and shown as a corner notice in the application's own style; panes and the two tools dock by
dragging their header; a fine terminal scrollbar; splitter minimums. The newest
[decision-log](#decision-log) entries have the detail.

**Update 2026-08-08 (1.4.0).** The splitter between two panes moves the boundary between them, both
directions, on both axes. It was resizing its own gutter column instead - a growing empty strip one
way, a dead splitter the other - because the style let `GridSplitter` fall back to its own default
alignment of `Right`. Direction and behaviour are now stated outright in both splitter styles; the
decision-log entry has the measurements.

**Update 2026-08-08 (1.3.2).** Two keyboard faults the 1.3.1 change left behind are fixed. **Escape**
did nothing at all: honouring win32 input mode made the host stop resolving a lone escape byte, and
only typed text had been converted to whole key events - every other key still went as classic VT.
**Ctrl+C and Ctrl+V** did not copy or paste; they sent the raw control bytes, so the only working
gestures were Ctrl+Shift+C/V and a right-click. Ctrl+C now copies when there is a selection and still
interrupts when there is not. The newest [decision-log](#decision-log) entry has the measurements.

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
Root is `D:\Multi TerminĂˇl Window`; layout in [STRUCTURE #directory-map](STRUCTURE.md#directory-map).

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

> âť“ Unverified: the five interactive items above - not confirmed against a running build.

**Next**, in order of leverage: the four empty settings pages, the user's interactive pass, then the
open threads below.

## Decision log

Append-only, newest first. Entries below 2026-08-05 are dated 2026-08-04; order within that day is
reconstructed.

### 2026-08-16 - 1.4.13 was written but not published, and the tag is the only thing that ships it

The 1.4.13 work was committed on 2026-08-15 and then stayed on this machine. `main` sat **one commit
ahead of `origin/main`** and there was **no `v1.4.13` tag** - the newest was `v1.4.12` - so not one
run had ever fired. `VersionInfo.cs` and `Bootstrap.rc` both read 1.4.13 and agreed with each other,
which is exactly what makes this easy to miss: everything local says the version exists.

Published on the user's explicit go-ahead, in the shape v1.4.11 and v1.4.12 already used - the
annotated tag sits on the `release:` commit itself: `git push origin main` (`9da1a49..26813e9`),
`git tag -a v1.4.13 -m "BetterTerminal 1.4.13"`, `git push origin v1.4.13`.

**Both runs finished success** - the `main` push build and the tag build (run 31934694069). The tag
run is the one that publishes, and it produced release **`v1.4.13`**, `prerelease=false`,
`draft=false`, marked latest, carrying the one asset `BetterTerminal.exe` at 0.62 MB. That is
[RULES #git-rules](RULES.md#git-rules) satisfied in fact rather than in intent.

**Worth keeping.** The version in `VersionInfo.cs` and the version on the releases page are two
different claims, and only a pushed `v*` tag joins them: `.github/workflows/build.yml` publishes for
`refs/tags/v*` and for nothing else, so a `release:` commit on its own ships nothing. It costs more
here than it would in most repositories, because the self-update path reads the **latest release** -
an unpublished version is not slow to reach people, it never reaches anyone at all, and the copy
under `%LOCALAPPDATA%` goes on believing it is current. Cheap check before believing a version
shipped: `git status -sb` and `git tag --sort=-v:refname | head -1`, read against `VersionInfo.cs`.

### 2026-08-15 - An address printed in a pane is something you can click

Released as 1.4.13. Two sources of links, kept apart because they behave differently. A program can
declare its own with **OSC 8** (`ESC ] 8 ; params ;
URI` closed by `ESC ] 8 ; ;`, either terminator, `id=` grouping non contiguous runs into one link),
and the terminal **finds addresses in plain text** (http, https, ftp, file, mailto, and a bare
`www.` host resolved over https). Where a program declared one, nothing is inferred: the declared
link wins over the cells it covers.

Five decisions worth keeping.

**The wrap bit rides on the last cell of the row** (`CellFlags.LineWrapped`), not in a table beside
the lines. Rows move as whole arrays through `ScrollUp`, `InsertLines`, the history and the
alternate screen, and none of those knew about a per line flag; on the last cell it follows the row
for free. `ResizeLine` clears it, because this grid truncates and pads instead of reflowing, and a
stale bit would join two lines that no longer run on.

**The link id is a `ushort` on `TerminalCell`, placed between `Character` and `Flags`** so the struct
stays at 16 bytes. A million lines of history is the constraint from 1.4.11; an `int` there would
have cost about 25 percent more for every cell ever written.

**Detection never runs on a frame.** It runs when something asks (the pointer moved, a right click, the
palette opened) and the answer is cached per logical line in a `ConditionalWeakTable` keyed by the row
array, checked against the line versions the scan was built from. A row that leaves the history takes
its cache with it. Rendering a screen with no links does no matching work at all, and scrolling back
over lines already scanned does none either.

**Exactly one path opens anything**: `TerminalLinkOpener.Open`. Ctrl+Click, the two context menu
entries and the palette all go through it, and the scheme allowlist is checked there, at the moment of
opening, not at detection. The address is handed to the shell association through `ProcessStartInfo`
with `UseShellExecute`; no command line string carrying a URI is built anywhere. A host that is
punycode or written in another script, and a declared link whose visible text disagrees with its
target, ask first and show the real target elided in the middle.

**A wide character now takes two cells** (`CellFlags.WideTrailing`, `CharacterWidth`). It had to, for
a hit test over a double width cell to mean anything, and it also stops a CJK glyph overprinting its
neighbour. Copying skips the trailing cell so the clipboard is unchanged.

**Open thread:** this terminal still implements no mouse reporting (DECSET 1000, 1002, 1003 are
parsed and ignored, and no mouse event is ever written to the child), so the rule that a child owning
the pointer keeps it needed nothing: an unmodified click still only selects. If mouse reporting is
ever added, revisit the activation test in `TerminalRenderer.ActivationHeld`.

**`BetterTerminal.Tests` now exists** (`beterm-tests.exe`, in the solution, no dependencies, a check
per line and an exit code): 77 checks over the parser, detection, hit testing and refusal. Run it
after a build. CLAUDE.md, STRUCTURE.md and WORKFLOWS.md still say the project has no tests, so they
want an `/md-sync`.

### 2026-08-12 - Holding position and catching up are opposite requests, and the anchor was winning both

**Reported straight after 1.4.11: "I cannot scroll in the console now."** The anchor added in 1.4.10
was applied unconditionally, so while output kept arriving it beat the reader in both directions. A
wheel notch down moves 3 lines; the lines written between one notch and the next moved the view
further up than that, so **every downward notch ended higher than it began**. Reproduced by driving
the real `TerminalRenderer` off screen (no window shown, nothing given focus, per R14) with 20 lines
arriving per notch: parked at 63, a notch down landed at **120**. With the view then pinned at the
oldest reachable line, upward scrolling was clamped and downward scrolling was overtaken - both
directions dead, which is exactly what "cannot scroll" means.

Two changes. **A downward gesture suspends the anchor** for `ChaseBottomFrames` (30 frames, about
half a second), counted down in `RenderViewport`: holding position and catching up cannot both be
honoured, and while the reader is on their way back to the live end the live end has to stop running
away. Parking resumes as soon as the gesture stops, and any keystroke still snaps to the bottom as
before. **The drift is folded in before the reader's own move**, in `ScrollBy` rather than only at
paint time, so a notch applies to where the view actually is instead of to a stale offset.

Measured on the same harness after the change: 63 -> 54 -> 45 -> 36 -> 27, three lines per notch
with 20 lines arriving in between, converging on the live end; parking still holds exactly (parked at
32, offset 72 after 40 lines). Both properties at once, which is the point.

**The lesson, worth keeping:** an automatic correction that fights the user needs an explicit way for
the user to win. The 1.4.10 anchor had none, and one release later it read as a broken scrollbar.

### 2026-08-12 - A million lines of history, which meant not laying it out in advance

Asked for outright: 5000 lines per pane raised to **1 000 000**. As the grid stood that was not a
constant to change - it was ~2 GB per pane. `CellGrid` allocated the whole ring up front (a million
slots is 8 MB of references per pane before a byte of output) and every history line was a full
screen width of `TerminalCell`, 16 bytes each, so a 40-character line cost 1.9 KB at 120 columns.

Two changes make the number honest. **The ring grows on demand**: it starts at
`InitialScrollbackSlots` (4096) and doubles - in logical order, oldest to slot zero - until it
reaches the capacity, so an idle pane pays for a few pages and the copying amortises to a constant
per line. **A line is stored at the width it was written**: `TrimTrailingBlanks` drops the padding
off the end on the way into the history. Cells carrying a background colour or an attribute are
kept whatever they hold, blank or not - that colour is on screen and has to still be there when it
is scrolled back to - so only genuinely empty default-background padding goes.

**Measured**, 120 columns, 30 rows, a million lines of ~40-character output: idle grid **0 MB**
managed (13 MB working set, which is the runtime), 50 000 lines **34 MB**, the full million **693 MB**
managed / 738 MB working set, written in 2.5 s. Read-back checked at indices 0, 1, 499 999 and the
two newest - all correct. The ring's two new paths were checked separately: growth past the first
4096 slots, rollover at capacity, a capacity of 100 and a capacity of 0, four cases each ordered
correctly end to end with the newest history line exactly where it belongs.

**The cost is real and worth stating**: a pane that genuinely scrolls a million lines holds about
700 MB, and panes do not share a history. Nothing is paid until the lines are written, and the
settings window now prints the number grouped (`{0:N0}`) instead of `1000000 lines`.

### 2026-08-12 - The history stopped sliding out from under a reader, a picture can be pasted, and the update check has a button

**The scroll position was measured from the wrong end.** `TerminalRenderer._scrollOffset` counts up
from the live bottom, and `RenderViewport` reads `top = TotalLines - Rows - offset`. Every line the
shell pushed into the history grew `TotalLines`, so the passage being read walked one line further up
per line written and was gone within a screenful - which is exactly what "the terminal does not hold
the scroll context, then the banner is back" describes. It was never a short history: a 300-line run
puts all 277 overflow lines in the grid (measured). `CellGrid` now exposes **`ScrolledLines`**, a
monotonic count of lines that have left the screen - the only honest measure, because once the ring is
full a push also drops the oldest line and every absolute index moves down one, while `TotalLines`
stops changing. `TerminalRenderer.HoldScrollPosition`, called under the grid lock at the top of
`RenderViewport`, adds that difference to the offset while the offset is above zero, clamped to the
oldest reachable line. Measured with a probe replaying the renderer's arithmetic against a live
session writing 25 lines/s: parked on "line 19", the old formula drifted to lines 44, 68, 94, 120,
145, 171 over six samples; the new one stayed on line 19 through all six. At the bottom
(`offset == 0`) nothing changes, so live output still follows.

**Ctrl+V was swallowed, so a captured picture could not be pasted into a command line tool.**
`Paste()` returned early unless the clipboard held text, and the key never reached the child - a tool
that takes a pasted screenshot reads the clipboard itself and has to be told the key was pressed.
Text still goes in as text; when there is none and the clipboard holds a picture, the keystroke is
handed over instead - `\x16`, or the whole key event when the host has asked for those. Clipboard
access is wrapped against `ExternalException`, which is what another application holding the clipboard
open throws, and would otherwise land unhandled on the UI thread.

**A Check for Updates button in the caption strip.** `Bt.Button.CaptionAccent` in `Controls.xaml` -
accent fill, ink-on-accent label, same band and height as its neighbours - sits left of the new-tab
accessory and calls the new `UpdateClient.CheckNow()`. It runs the same probe as the hourly timer on a
pool thread, and unlike the timer it **always answers**: the update notice, "No update available", or
"Update check failed", because a silent button cannot be told from a broken one. Asking again clears
the once-only guard, so a version already announced is shown again rather than swallowed as a repeat.

Released as **1.4.10** on the user's word, rebased onto the 1.4.9 folder-menu work that was already on
`origin` and had never been in this working tree.

### 2026-08-09 - Verification never touches the user's desktop (R14)

Said outright after the 1.4.9 work: driving his mouse and his screen is not allowed. What earned it
was a capture script that called `SetForegroundWindow` and then `SendKeys` with Ctrl+comma - foreground
focus taken from whatever he was doing, and a key gesture sent into it - followed by clicking the
settings window's controls through UI-Automation `Invoke`/`Select`/`Toggle` while windows opened on
his desktop mid-session. Now **R14**.

The principle was already in this repository and was simply not read: `tools\capture-window.ps1`
uses `PrintWindow` "so taking a screenshot never has to steal foreground focus from whatever the
user is doing", and this file already recorded that **the user drives the interactive pass**. Note
that `tools\ui-smoke.ps1` falls under the ban too - driving buttons by automation name is the same
act, which leaves the documented UI sequences as something to hand to the user, not to run at him.

**What to do instead**, and it is not weaker evidence: load the built assembly and call the shipped
class directly - `[Reflection.Assembly]::LoadFrom("BUILD\app\BetterTerminal.exe")`, then the static
method - and read the real side effect. That is exactly how the 1.4.9 registry behaviour was proven
(written, survived an update, removed cleanly), and none of it needed a window. Everything only a
person can judge is handed over as steps to click and what should happen.

### 2026-08-09 - The folder right-click menu is a switch in the settings window (1.4.9)

Asked for: the application in the menu a folder shows on a right click, named and with its icon, the
way the other tools on that machine put themselves there - and a switch for it in the settings
window, because a menu entry nobody asked for is a nuisance. `Services\ExplorerMenu.cs` writes it and
a new **Integration** settings page turns it on and off.

**What is written.** Two per-user class registrations, `Directory\Background\shell\BetterTerminal`
and `Directory\shell\BetterTerminal` under `HKCU\Software\Classes` - the background of an open folder
and a folder icon in a list are two different menus and both had to carry it. Default value is the
menu text (`BetterTerminal`), `Icon` is `"<exe>",0` so the mark comes out of the executable and no
image file is installed anywhere, and `command` is
`"<exe>" --project "%V"`. That is the **same switch the `beterm` shim uses**, so the menu and the
typed command open a folder identically - no second code path to keep in step. Nothing machine wide,
no elevation, no uninstall entry: it stays inside the rule that only the service registration leaves
the user profile.

**The registry is the only record, deliberately.** No `[DataMember]` in `workspace.json`. A mirrored
copy drifts the moment the user deletes the keys by hand, and the switch would then be describing
something that is not there; `SettingsViewModel` seeds itself from `ExplorerMenu.IsVisible` and the
setter stores **what the registry holds afterwards**, not what was clicked, so a write that failed
shows as off. `App.OnStartup` calls `ExplorerMenu.Refresh()` next to `CommandRegistration.Ensure()`:
it re-points an entry the user already turned on at the current installed copy and **never adds
one**, which is what keeps the entry working across a self-update. Writes go through `SetIfChanged`
because this runs on every start and re-writing an identical value is still a change to the desktop.

**Target is the installed copy** (`SelfInstall.InstalledExecutable`), falling back to the running
executable, for the same reason the `beterm` shim points there: a build folder can move or be
deleted, the copy under the profile cannot.

**Verified**, by calling the shipped class out of `BUILD\app\BetterTerminal.exe` and reading the
registry: off by default; on writes both keys with label, icon and command; running that exact
command line against a folder opened it as a project (banner reported the folder, `.beterm\
project.json` was created); the entry survived a restart and the 1.4.8 -> 1.4.9 self-update; off
removed both key trees leaving nothing behind. Left **off** afterwards - the point of the switch is
that the user chooses.

**Known limit, stated in the settings page.** This is the classic verb, so on Windows 11 it sits
under **Show more options** with everything else that registers this way. The compact Windows 11
menu takes packaged commands only, which this application is not.

### 2026-08-09 - Service-driven update notice confirmed; poll settled at 15 min (1.4.8)

End-to-end proof on frant's machine: with BetterTerminal **closed**, publishing v1.4.7.1 had the
service notice it, stage it and raise the app-drawn toast on its own - the Application event log shows
"Staged update 1.4.7.1." and "Notified the user of update 1.4.7.1." The whole notification chain
(service poll -> app-drawn Windows 11 toast, and the service self-upgrade to a current binary) is
working. The testing 15 s poll was raised to **15 minutes** for the release (`UpdateShared.
DefaultPollInterval`, initial delay back to 1 min); `BETERM_UPDATE_POLL_SECONDS` still overrides.
1.4.8 is the first release carrying the finished feature at a sane cadence.

### 2026-08-09 - The service upgrades itself; real-time is a 15 s poll for now (1.4.7)

**The bug behind "the service never notifies."** The installed service was **1.4.0.0** - from before
the update feature existed (poll + notify landed 1.4.2+). It never updates: `ServiceInstall.Ensure`
returned as soon as the service was installed, and a running service **locks its own binary**, so an
app update cannot replace `beterm-service.exe`. It is frozen at whatever version first registered it.
So only the running app's self-check ever raised a notice; the service did nothing.

**Fix - a never-locked update binary + an elevated self-upgrade.** The build now ships a second copy,
`beterm-service-update.exe` (a `Copy` target in the shell csproj `AfterTargets="Build"`, so CI and the
payload both carry it). Nothing runs it as a service, so an update always replaces it. `ServiceControl.
Install` is now the upgrade path too: run the update binary elevated and it stops the old service
(freeing the canonical file), copies the fresh bits onto `beterm-service.exe`, registers that canonical
path and starts it - the service always runs from the stable path, the update binary is a transient
installer. App side, `ServiceInstall.UpgradeIfOutdated` compares the update binary's version to the
registered one and runs it elevated when newer, once per newer version (marker `service-upgrade.txt`).
So a stuck old service self-heals on the next app start, at the cost of one UAC prompt. Verified live:
the elevated path took frant's 1.4.0.0 service to 1.4.7.0, Running, canonical path, exit 0.

**Poll cadence is TESTING-aggressive: 15 s** (`UpdateShared.DefaultPollInterval`, initial delay 10 s),
so a freshly published release is noticed at once while the flow is exercised. **Raise it to minutes
before this is left in anyone's hands** - 15 s is 240 req/h against the releases/latest redirect.
`BETERM_UPDATE_POLL_SECONDS` still overrides. Note the service floors "installed" at its own version,
so to see a closed-app notice the published release must be newer than the *service's* version.

### 2026-08-09 - The running-app update notice uses the library too (1.4.6)

1.4.5 shipped the notification library but left the **running-app** notice on the old
`UpdateToastWindow` (the amber "Restart now" card) - only the service-closed path was switched. So a
user on a live session still saw the old toast. `UpdateClient.Present` now builds a
`ToastNotification` directly (the shell references the library) with a single "Restart now" action
wired to the same `UpdateApply.Launch` + `Application.Current.Shutdown`; `UpdateToastWindow` is
deleted. Both paths - closed (service `--notify`) and running (in-app) - are now the same acrylic
window. Note the old behaviour a user sees before updating is their *installed* pre-1.4.6 build; the
change only shows once 1.4.6 is installed. The service remains a poller (default 4 h, first poll ~1
min after it starts), not a push - "real-time while closed" is bounded by that interval; the app
checks at once on open.

### 2026-08-09 - The notification is its own library (`BetterTerminal.Notifications.dll`)

**Context.** The `ToastEnabled=0` finding killed the WinRT route for good: the user's account has the
master Windows-notifications toggle off (`HKCU\...\PushNotifications\ToastEnabled = 0`), so every
system toast is dropped no matter the code - confirmed in the registry, `ToastNotifier.Setting` stayed
`DisabledForUser` even with a Start-menu shortcut and `SetCurrentProcessExplicitAppUserModelID`. The
user asked for a **custom window** that looks like a Windows 11 toast, then for it as a **library**.

**Decision - a WPF class library the host loads with a command line.** New project
`BetterTerminal.Notifications` (Library/.dll, .NET 4.8, C# 7.3, WPF; GUID ...C4000A) holds
`ToastNotification` (the window), `ToastAction`, `NotificationRequest` (the CLI parser),
`NotificationActions` (named actions -> delegates), and `NotificationHost.Run(args)`. Because the DLL
cannot be a process, the **host that loads it in the user session is `BetterTerminal.exe`**: the
service starts it with `--notify --title "..." --message "..." -btn1 install -btn2 later`
(`UpdateShared.UpdateNotifyArguments`), the shell's `App.OnStartup` sees `--notify` and calls
`NotificationHost.Run(e.Args)` and nothing else. `NotificationHost` self-hosts a WPF `Application`
when there is none (the launched case) or shows in-process when one exists (a live shell). Buttons:
`-btn1..3 <action>`, up to three, none required; `install`/`open` start the app (applies the staged
build), `later`/`dismiss` just close; `LABEL=action` gives a custom caption. The window ships in the
payload via a `ProjectReference` from the shell (like Banner), so it unpacks into `app\` beside the
exe. It is drawn by the app, so **`ToastEnabled=0` no longer matters** - it always shows.

**The window is the imported design.** From the user's Claude Design project (`DesignSync`,
`wpf/ToastNotification.xaml`): a pixel-measured Win11 toast, 364x157 DIP, DWM acrylic backdrop
(`DWMWA_SYSTEM_BACKDROP_TYPE` transient window) with a solid `#C71E2028` fallback, rounded corners,
right-side slide-in, hover-pauses-dismiss, stacked bottom-right. Adapted from .NET 6 (file-scoped ns,
nullable, `init`, property patterns) to .NET 4.8 / C# 7.3; the PowerShell tile became a `>_` tile.

**Removed as now-dead:** `SystemToastWindow` (the interim solid-card window from 1.4.4),
`ToastShortcut`, and the WinRT PowerShell path (`UpdateShared.ToastAppId`, `PowerShellExecutable`,
`ToastArguments`, `Escape`). The **Test notification button** was used to verify, then removed as the
user asked. `SessionNotice` (WTSSendMessage) still stands as the last-ditch fallback.

**Verified.** Debug rebuild is warning-clean (0/0); `BUILD` staged at 1.4.4 (30 files, DLL present in
`app\`). The window was shown via `BetterTerminal.exe --notify ...` and captured: acrylic toast,
Install now / Later. Not verified here: the service raising it as LocalSystem through
CreateProcessAsUser, which needs the installed service.

### 2026-08-08 - The service's update notice is a real Windows toast, raised through PowerShell

> Superseded 2026-08-09: the WinRT/PowerShell toast below was removed. The account's master
> notifications toggle was off (`ToastEnabled=0`), so no system toast could ever show; the notice is
> now the app-drawn `BetterTerminal.Notifications` window. Kept for the reasoning trail.

**Context.** The user wanted a proper toast rather than the message box the service showed when
BetterTerminal was closed, and pointed at the `WindowsToastNotifyApi` package.

**Why not that package.** It targets `net8.0` only and depends on `CommunityToolkit.WinUI.Notifications`;
this product is .NET Framework 4.8, and a net8 assembly cannot be referenced from it. Confirmed from
its csproj, not guessed.

**Decision - reach WinRT through Windows PowerShell.** A WinRT toast from an unpackaged .NET Framework
app needs `Windows.winmd` references and a registered AppUserModelID - fragile at build (SDK-version
pinned winmd) and a risk to the CI release pipeline. Windows PowerShell reaches the same
`Windows.UI.Notifications` API with no build-time metadata, so `UpdateShared.ToastArguments` builds a
`-EncodedCommand` script (shared, linked into both assemblies) that registers a `BetterTerminal`
AppUserModelID in HKCU and shows the toast. The **service** runs it in the user session with
`SessionLauncher.Run` (CreateProcessAsUser, session 0 cannot toast); the **application**, already in
the session, runs it directly. `SessionNotice` (WTSSendMessage) stays as the fallback if the toast
cannot be raised, and the old notification code is copied under `backup/notifications-2026-08-08/`.

**Decision - notify, do not force.** When BetterTerminal is closed the service now only *tells* the
user a build is ready (toast, once per version) and leaves the staged update for `TryApplyOnStartup`
to apply when they next open it - the earlier behaviour force-opened the updated app, which was
intrusive. Flip back by restoring the apply call in `NotifyIfNothingIsRunning` if wanted.

**Provisional.** A "Test notification" button in Settings raises the toast on demand, so it can be
seen without waiting for a release.

**Verified.** Both configurations build zero-warning under `/warnaserror`; `BUILD` staged at 1.4.4.
The exact script the code generates was run through `-EncodedCommand` and raised the toast with exit
0 (a game in the foreground routed it to the Action Center - Focus Assist, a Windows toast fact, not
a fault). Not verified here: the service raising it as LocalSystem through CreateProcessAsUser, which
needs the installed service.

### 2026-08-08 - The service updates and notifies on its own when the application is closed (1.4.2)

**Context.** The user wanted the update to happen through the service even when BetterTerminal is not
running, with a notice shown - purely by the service.

**The truth this rests on.** A service is in session 0 and cannot draw a window on the interactive
desktop; only a process in the user's session can. So "purely by the service" has two supported
routes, and neither is the application's own styled window: `WTSSendMessage` puts a plain system
message box in the active session, and `CreateProcessAsUser` starts a process there with the user's
token. The service uses the first to notify and the second to install - the launcher must run with
the user's token or it would unpack into the service account's profile and `%LOCALAPPDATA%` would
point at the wrong place.

**Decision.** The service acts on its own only when nothing of ours is running
(`Process.GetProcessesByName`). When the application is up, the notice and the apply stay its own,
over the pipe, so a live session is never disturbed. When it is closed, the service shows the message
and starts the staged launcher in the user's session; the application opening updated is the visible
result. The staged record is cleared after a successful apply so the next poll does not repeat it.

**Not verified here.** The `WTSSendMessage` and `CreateProcessAsUser` paths need the service running
as LocalSystem in session 0 and cannot be exercised from this environment. Offline, the check and
staging still work after the refactor, and both configurations build zero-warning under
`/warnaserror`. The notice is the system message box by design; the application's corner window is
the styled one and needs the running application.

### 2026-08-08 - The application updates itself from the latest release, driven by the service

**Context.** The repository is public now and each version ships as a GitHub release whose one asset
is the launcher, `BetterTerminal.exe`, tagged `vMAJOR.MINOR.PATCH`. The user wanted the application to
update itself to the latest release, driven by the Windows service, with a visible notification -
after several earlier attempts that "did not work".

**Why the earlier attempts failed, and the shape that follows from it.** A Windows service runs as
LocalSystem in session 0 and cannot draw anything on the interactive desktop - not a toast, not a
window. So the split is forced: the **service** checks and downloads; the **application**, which is
in the user's session, is the only thing that can show the notice. They meet over a named pipe
(`BetterTerminal.Update`, granted to authenticated users so the app can connect) and two small
records under `ProgramData` (readable by both LocalSystem and the user, which neither profile's
AppData is). This is `UpdateShared.cs`, linked into both assemblies the way `VersionInfo.cs` is,
because the service must not depend on the WPF application.

**Decision - no JSON, no token, no new dependency.** The latest version is read from the 302 that
`github.com/OWNER/REPO/releases/latest` returns to `/releases/tag/<tag>`; the tag is the version and
the asset URL is built from it. Everything uses assemblies already referenced (HttpWebRequest in
System, named pipes in System.Core). Nothing was added to any manifest but source files.

**Decision - the notice is our own window, not a Windows toast.** A system toast is routed through the
notification centre and silently held back whenever another application is in the foreground, which
is exactly when an update notice needs to show - measured live, a `Shell_NotifyIcon` balloon did not
appear over an active Discord window. `UpdateToastWindow` is a small app-styled window in the corner
that dismisses itself, so it is never suppressed by focus rules.

**Decision - automatic, but never on top of a live session.** "Automatic" does not mean killing the
running application out from under live shells and open files. A newer build is downloaded and staged
silently; the notice appears at once; the replacement happens on the next start, done by the launcher
before any window exists. The corner notice carries a one-click "Restart now" for immediacy. The
launcher gained `--wait <pid>`: it waits for the old process to exit before it unpacks over the
install folder, because files held open by the running copy cannot be replaced - the reliability
crux, and almost certainly part of why past attempts silently did nothing.

**Verified, offline.** Both configurations build zero-error, zero-warning; `BUILD\` staged at 1.4.0.
Driven through the real service binary and a real pipe client: with the test hook
(`BETERM_UPDATE_FEED` / `BETERM_UPDATE_ASSET`, off by default) a `--check` staged a fake 9.9.9.0,
wrote `staged.txt` and verified the staged file's version; against the live GitHub the same check
correctly reported nothing newer than 1.4.0 (no false update). The pipe pushed `update 9.9.9.0` to a
connecting client and answered a `check` request. The launcher's `--wait` was shown to block until
the named process exited and then proceed. `TryApplyOnStartup` was seen to fire on a staged newer
version.

**Not verified here, and it needs the user's machine.** The live end to end - the service installed
and running as LocalSystem polling GitHub on its timer, the real download, the corner notice on the
desktop, and the self-replace on restart - cannot run from this environment (no elevation, no desktop
session). **Known limit:** the running service holds `beterm-service.exe` open, so an update replaces
the application immediately but the **service binary** lags until the service is restarted; the
service reads the app's `installed.txt` so the version skew is handled correctly in the meantime.

### 2026-08-08 - The connection list and the file explorer are panels, so they dock like panes

**Context.** The user reported that grabbing the SSH form or the file explorer offered no docking.
Correct: they were windows with their content welded into them, and the pane grid had no way to hold
anything that was not a session.

**Decision.** Both tools moved into `UserControl`s - `ConnectionsPanel` and `FilesPanel` - that carry
no caption and no close button, and the windows became frames that host them. There is one definition
of each tool, so the windowed and docked forms cannot drift apart. `ToolPaneViewModel` wraps a panel
as a `DockLeafViewModel`, which is why no docking code needed a line changed: the tree, the splitters
and the tear-off already spoke that type.

**Decision.** The panel instance is handed to whatever hosts it, never rebuilt. A tool holds live
state - a reachability check in flight, an open folder, an edited file - and rebuilding on each dock
would throw it away, which is the same mistake as restarting a session. The connections dialog is
modal, so its dock is decided inside and acted on after `ShowDialog` returns; the pointer belongs to
the dialog until it is gone.

**Decision.** The layout saves only *which* tool was docked, not what it was showing. What a file
explorer had open belonged to the session that ended, and restoring it would show a stale view of a
folder that may have changed.

**Verified.** Both configurations rebuild zero-error, zero-warning; `BUILD\` staged at 1.4.0. Driven
through the real Release build with UI Automation: the file tree is absent from the main window,
opening the explorer gives a window whose caption carries the dock button, invoking it puts the tree
**inside the main window** at 266x779 and takes the grid from three panes to four. A screen capture
shows the extracted panel rendering correctly in the window - folder tree, status strip, both hints.

**Trap for the next probe.** `$pid` is a read-only PowerShell automatic variable and assigning to it
kills a UI Automation script in a way that still prints plausible-looking failures;
`AndCondition` also needs an explicit `[Condition[]]` array from PowerShell. Both cost a run here.

### 2026-08-08 - Panes tear off into windows of their own, and the tear-off is a move

**Context.** The user asked for a docking system: drag a pane header sideways and the pane becomes
its own window without losing the terminal's memory, drag it back to return it, dock targets shown in
the primary colour with icons, and the SSH and file windows usable as panes in the grid. Nothing of
the sort existed - `SessionWindow` looked close but starts a **new** session and kills it on close.

**Decision, and the one that matters.** A tear-off re-parents the very element the grid was holding.
`PaneViewModel.Surface` owns the session, so moving that instance keeps the process, the pseudo
console and the scrollback exactly as they were; nothing is rebuilt and nothing restarts. The trap is
that a content host keeps its child as a **logical** child until told otherwise, and adding an element
that still has a parent throws - so `DockController.Detach` clears the old holder first, binding and
all. Do not "simplify" that away; removing the leaf from the tree is not enough, because the discarded
presenter still owns the element at that instant.

**Decision.** `DockLeafViewModel` is the new base for anything that can be a leaf, so the tree, the
splitter branches and every docking path work in one currency and a tool pane will dock exactly the
way a session does. `PaneViewModel` derives from it; `CanFloat` is false for the hosted-console
fallback backend, whose child console window does not follow the element to another top-level window.

**Decision.** The floating window drags its header by hand rather than through the frame. Handing the
pointer to the frame's move loop blocks until the button comes up, and the dock targets have to be
hit-tested while the drag is still running. The drag is in physical screen pixels throughout -
`Window.Left`/`Top` are device-independent and their relation to real pixels depends on the monitor,
so a drag expressed in them tears when it crosses a screen of a different scale.

**Decision.** Center on the rosette swaps the two panes rather than tabbing them together: this pane
tree is binary splits with no tabbed groups inside a pane, so there is nothing to tab into, and a
swap is the one center meaning the four sides cannot already express.

**Verified.** Both configurations rebuild zero-error, zero-warning; `BUILD\` staged at 1.4.0; the
Release build starts and UI Automation still finds every caption button. The target geometry was
driven through the real `DockController` with a laid-out pane host: on a 1000x600 host split in two,
the pointer at the left pane's centre wins Center, one step left wins the Left arrow, the rosette
follows to the other pane, the left edge wins an outer Left, and empty space claims nothing - five of
five.

**Not verified, and it is the interesting half.** The tree surgery - `RemoveLeaf`, `InsertBeside`,
`InsertAtEdge`, `Replace` - and the end-to-end tear-off and dock-back have **not** been driven. The
probe that would have done it cannot host `MainWindow`: its `DataTemplate`s fail to resolve
`StaticResource Bt.Text.Icon` even though the same key resolves through
`Application.TryFindResource` in that same process. Worth solving once - it blocks every future probe
that wants the real window.

**Not built.** The SSH list and the file explorer are still windows only. Making them dock means
lifting `ConnectionsWindow` and `FilesWindow` content into panels that either a window or a leaf can
host; `DockLeafViewModel` exists so that work needs no change to anything written here.

### 2026-08-08 - The caption buttons got their own column, after a bad merge stacked them

**Context.** The user sent a screenshot of the caption strip with the glyphs colliding: a plus with
something drawn through it, a chevron over a square, and one window button too few.

**Cause.** Self-inflicted, in the `git stash pop` after pulling the contributor's four commits. The
local change split the single caption `StackPanel` into two so the window buttons could carry
`WindowChrome.IsHitTestVisibleInChrome`, and the conflict was resolved by keeping both panels - but
the caption `Grid` has only three columns and both panels were left on `Grid.Column="2"`. Two
children in one cell overlap, so the window buttons drew straight over the tab actions.

**Decision.** The caption grid has a fourth `Auto` column and the window buttons live in it. Keep the
two panels separate: merging them back would put the chrome flag on the tab actions as well, and the
comment on the second panel says why it exists.

**Verified.** UI Automation on the running Release build reports the five buttons side by side at
x = 1710, 1742, 1782, 1828, 1874, each 32 px tall, no two sharing an origin; before, minimise sat at
1710 on top of the plus. A `PrintWindow` capture of the strip shows plus, chevron, minimise, maximise
and close as five separate glyphs. Both configurations build zero-error, zero-warning.

### 2026-08-08 - The pane splitter resizes the panes, because it was resizing its own column

**Context.** The user reported that dragging the separator between two side-by-side sessions to the
right opened a growing empty strip between them, and that dragging it left did nothing at all. The
row splitter was never reported and turned out to be fine; only the column one was broken.

**Cause.** `GridSplitter` overrides the metadata of `HorizontalAlignment` with a default of **Right**
- the control's own default, not the framework's `Stretch`. `Bt.GridSplitter` replaces the theme
style outright and never set that property, so `Right` stood. With the default
`ResizeBehavior="BasedOnAlignment"` a Right-aligned splitter in a column resolves to
**CurrentAndNext**, and "current" is the splitter's **own** `Auto` column. So a drag was widening the
gutter, not moving the boundary: measured on a 1000 px grid, dragging right 80 px took the gutter
from 6 px to **86 px** and shrank *both* panes from 497 to 457 as the two stars re-split what was
left. Dragging left could only push the gutter back to its 6 px minimum and then stopped dead, which
is the "nothing happens". The row style escaped it because only the horizontal alignment carries that
overridden default; `VerticalAlignment` was still `Stretch`, which resolves to `PreviousAndNext`.

**Decision.** Both splitter styles now state direction and behaviour instead of letting them be
inferred from alignment: `HorizontalAlignment`/`VerticalAlignment` `Stretch`, an explicit
`ResizeDirection` and `ResizeBehavior="PreviousAndNext"`. Do not delete these setters as redundant -
the alignment heuristic is exactly what broke this, and `Bt.Size.SplitterThickness` is small enough
that the fallback tie-break on `ActualWidth <= ActualHeight` is not something to rely on either.
`Cursor` is set here too (`SizeWE`/`SizeNS`) because replacing the theme template threw away the
cursor that came with it, and `Focusable="False"` keeps the splitter out of the tab order.

**Verified.** Both configurations rebuild zero-error, zero-warning and `BUILD\` is staged at 1.4.0.
Driven through a probe that builds the same grids the two `DataTemplate`s build - real styles, real
`ColumnSplitViewModel`/`RowSplitViewModel`, the same TwoWay length bindings - and raises the real
`Thumb` drag events, which is the path a mouse drag takes. Columns on a 1000 px grid: gutter holds at
6 px throughout, right 80 gives 577/417, left 160 gives 417/577, and the drags clamp at the 120 px
`MinWidth` on either side. Rows on 600 px: down 60 gives 357/237, up 120 gives 237/357, clamping at
`MinHeight` 90. `FirstRatio` on the view model tracked every drag (0.5 -> 0.58 -> 0.42 -> 0.879), so
`TerminalWorkspace` persists and restores what the user set.
### 2026-08-08 - Escape and the plain clipboard keys, the two halves 1.3.1 left broken (1.3.2)

**Context.** Reported as "I can't copy and paste", then "Escape doesn't work but right-click paste
does". That pairing is what split the problem in two: the right-click path proves the session, the
grid and the clipboard are all healthy, so nothing was wrong with writing to the session or reading
the clipboard.

**Cause, Escape.** A regression from 1.3.1, and the interesting kind: the change was correct but
partial. Honouring win32 input mode tells the host to stop guessing keys from bare characters - and
`CSI ? 9001 h` arrives in the first bytes of **every** session, so the mode is always on. Only
`OnTextInput` was converted. `OnPreviewKeyDown` still emitted classic VT, which is fine for every key
that has an unambiguous form - `CR` for Enter, `CSI D` for Left, all complete - but a lone escape
byte is also how every sequence begins. The host's parser holds it waiting for the rest and the key
never lands. That is precisely the ambiguity win32 input mode exists to remove, so Escape is the one
key that had to be converted along with the text.

**Cause, clipboard.** Not a regression at all, and not in the drag-and-drop commit the report
suspected. `Ctrl+C` and `Ctrl+V` were never bound: `EncodeControl` turns them into `0x03` and `0x16`,
so only `Ctrl+Shift+C`/`Ctrl+Shift+V` and the right-click reached the clipboard. Copying itself
always worked - measured, with the selection, the built string and `Clipboard.SetText` all confirmed
in a driven run before anything was changed.

**Decision.** Send Escape as `CSI vk ; scan ; char ; down ; controls ; repeat _` when the grid
reports win32 input mode, via a new `VtKeyEncoder.EncodeKeyEvent`, and leave every other key on its
VT form. Converting all of them would be a larger change with nothing to gain: they are unambiguous
and they work today. The rewrite is keyed off the produced sequence being a lone escape, so
`Ctrl+[` - which encodes to the same byte - is carried with it.

**Decision.** Bind `Ctrl+C` to copy **only when a selection exists**, and drop the selection after
copying so the next `Ctrl+C` is the interrupt again; with nothing selected it falls through
untouched. `Ctrl+V` pastes. This is what the platform's own terminal does, and it keeps the interrupt
reachable at all times, which a bare "Ctrl+C is copy" binding would not.

**Verified.** Both configurations rebuild zero-error, zero-warning, and `BUILD\` is staged at 1.3.2.
Measured against the running app, driving real Win32 input, not synthetic routed events. Before the
fix: typing `THIS-SHOULD-VANISH` and pressing Escape left the text on the line. After: the line is
clear. `Ctrl+C` over a selection moved the text to the clipboard - it went from a sentinel value to
the selected text - and `Ctrl+V` typed it back onto the prompt. `Ctrl+C` with nothing selected
stopped `ping -n 30` after five replies, so the interrupt survives. A dead end worth not repeating:
a probe hosting `ConPtySession` from a console PowerShell is useless, because the child inherits that
console instead of the pseudo console and both `OutputReceived` and the grid stay empty; the app
itself, plus screenshots, is the reliable harness. See [TIPS](TIPS.md#gotchas).

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
`GOT=[echo hi 123 ABC]`, and the accented `Ä›ĹˇÄŤĹ™` round-tripped intact.

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
### 2026-08-07 - A maximised window stopped hanging off every edge of the screen

**Context.** The user reported the interface overflowing the screen when maximised and sent two
screenshots: the title bar cut off on the left, the status strip swallowed by the taskbar. Measured
rather than guessed: maximised the window was **-8,-8 at 1936x1048** against a work area of
**0,0 1920x1032** - eight pixels past all four edges. A window that draws its own frame is maximised
to the whole monitor *grown by the sizing border*, and that is a real window larger than the screen,
so nothing in the layout was at fault.

**Decision, and what the measuring actually showed.** `BetterTerminal.Terminal\WindowFrame.cs`
answers `WM_GETMINMAXINFO` with the monitor's work area - the correct thing to say, and it is said.
It does **not** settle it here. The answer was given before the frame's own hook and after it,
marked handled and not, with the work area and with the work area less the border; the frame put its
own numbers back every time except once, and that once did not reproduce. So the second half is what
carries the fix: while the window is maximised the content is inset by exactly that border, which is
entirely under this application's control. The window rectangle still hangs over, and the taskbar
draws on top of the eight pixels that do; what the user sees is aligned.

Do not "simplify" this by deleting either half. The hook is what keeps the window from growing past
the work area when it is snapped, and the inset is what makes the content line up.

**Also fixed.** The minimise, maximise and close buttons never carried
`WindowChrome.IsHitTestVisibleInChrome`, so the frame treated them as title bar and a click dragged
the window instead of pressing the button - the buttons beside them in the same strip had it, these
did not.

**Verified.** Measured before and after on a 1920x1080 screen with a 48-pixel taskbar, and confirmed
in a full-screen capture: the status strip now sits above the taskbar instead of under it, and the
left edge of the title bar is no longer cut. Debug and Release rebuild with zero warnings.

**Still open, told to the user rather than quietly dropped:** the docking system they asked for, and
the empty gaps they see when dragging a separator. The gaps could not be reproduced with synthetic
mouse input - the drag never took - and they may well have been this same eight-pixel clipping.

### 2026-08-07 - The first run registers the service, and the download is one file (1.4.0)

**Context.** The user asked for the release to carry `BetterTerminal.exe` alone, for the helper
programs to be presented behind the service rather than listed beside it, and for the service to be
installed as a Windows service. The last one was put to them as three options with the cost spelled
out; they chose **automatically on the first run**.

**Decision.** `Services\ServiceInstall.cs` starts `beterm-service.exe --install` with the `runas`
verb once, from the main window's `Loaded`, on a pool thread. Three constraints make it bearable and
each is load-bearing: the marker in `%LOCALAPPDATA%\BetterTerminal\service-install.txt` is written
**before** the attempt, so a refusal or a machine that cannot elevate is never asked twice; the work
is off the interface thread, because the prompt is the user's to answer in their own time; and
nothing in the application depends on the service, so refusing costs the user nothing.

**What this cost, stated plainly.** Two promises the project used to make are now false and have
been rewritten rather than left standing: "nothing written outside your user profile" and "never
installs the service, run by hand by the operator". The application process still runs `asInvoker` -
it starts an elevated child, which is not the same thing, and no hosted shell is ever elevated - but
deleting the two folders no longer removes everything, so the README's removal instructions now lead
with `beterm-service.exe --uninstall`.

**Decision.** The Shell now project-references `BetterTerminal.Service` and `BetterTerminal.Wrap`
purely so their executables land in its output, the way it already did for the banner and the
wizard. That is what puts all four helpers in the payload the one-file launcher carries and in the
copy `SelfInstall` makes - the service could not install itself from a download that did not contain
it.

**Decision.** The release asset is `BetterTerminal.exe` and nothing else; the archive of loose files
stays as the workflow run's own artifact. A release this workflow touches also has an older
`BetterTerminal-x64.zip` asset deleted, so a release that says it carries one file does.

**Verified.** Debug and Release both rebuild with zero errors and zero warnings, and all four
`beterm-*` programs now reach `%LOCALAPPDATA%\BetterTerminal\app`. The ask-once guard was checked
with a marker in place: no elevation prompt appeared, the application stayed up and the marker was
left alone. **Accepting the prompt itself was left to the user** - it is their machine and their
service database, and their standing workflow is to run the interactive pass themselves.

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
being decoded in the machine's OEM code page, which put a bar through the accent in "TerminĂˇl" -
a child inherits the console code page, and this program had already set that to UTF-8.

### 2026-08-05 - The first run installs a copy under the user profile, and the command runs that

**Context:** The command written by the first version of `CommandRegistration` embedded the absolute
path of the build output. Running `beterm` produced *"Windows cannot find D:\Multi Termin?l
Window\...\BetterTerminal.exe"* - the `Ăˇ` had been decoded in a code page that was not the one the
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
opened the app from a folder named `projekt-Ä›ĹˇÄŤĹ™` **with the console switched to 65001**, from both
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
`Ăˇ` in the repository path and `beterm` silently started nothing at all. The word "SSH" is now the
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
1. **Order:** `CompleteAdding()` â†’ close the pseudo console and the job object (this kills the client
   and breaks the pipe, which is what actually unblocks a reader parked on a read) â†’ `Join()` both IO
   threads with a **2 s timeout** â†’ only then dispose the streams, exit event, process handle and
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
path `D:\Multi TerminĂˇl Window` failed with "path does not exist" though the path plainly existed:
**PowerShell 5.1 reads a `-File` script as ANSI unless the file carries a UTF-8 BOM**, and the
directory name contains a non-ASCII character (the `Ăˇ` in `TerminĂˇl`), so the path was mangled at
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
[â† CLAUDE.md](CLAUDE.md) Â· [STRUCTURE](STRUCTURE.md) Â· [WORKFLOWS](WORKFLOWS.md) Â· [MEMORY](MEMORY.md)
