---
updated: 2026-08-04
scope: Index of every markdown file outside the core nine, the external documents this code is written against, and the internal contracts that stand in for an API surface.
stability: evolving
sources: [.claude/skills/md-orchestrator/SKILL.md, .claude/skills/md-orchestrator/references/file-specs.md, .claude/skills/md-orchestrator/references/maintenance.md, .claude/skills/md-orchestrator/references/migration.md, .claude/skills/md-orchestrator/references/self-install.md, .claude/skills/md-orchestrator/references/subagent-briefs.md, .claude/commands/md-sync.md, .claude/commands/md-audit.md, .claude/commands/md-recall.md, docs/_archive/2026-08-04/ARCHIVE-INDEX.md, docs/_archive/2026-08-04/RULES.md, BetterTerminal.Terminal/ITerminalSession.cs, BetterTerminal.Interop/NativeMethods.cs, BetterTerminal.Shell/PersistedWorkspace.cs, BetterTerminal.Shell/PersistedTab.cs, BetterTerminal.Shell/PersistedNode.cs]
owner_agent: docs-agent
---

# DOCS

Where documentation lives that is not one of the core nine root files (CLAUDE, README, STRUCTURE,
RULES, WORKFLOWS, AGENTS, MEMORY, TIPS, DOCS), which external documents the code was written
against, and what passes for an API contract in a desktop application that exposes no network
surface.

Repository walked on 2026-08-04. Eleven markdown files exist outside the core nine; all eleven are
indexed below. There are no other `.md` files anywhere in the tree (`bin/` and `obj/` contain none).

## Internal docs

| doc | path | covers | updated |
|---|---|---|---|
| md-orchestrator SKILL | [.claude/skills/md-orchestrator/SKILL.md](.claude/skills/md-orchestrator/SKILL.md) | The documentation system that produced these nine files: the four non-negotiable rules (one agent per file, CLI status banners, archive never delete, fixed link contract), mode detection, research/dispatch/verification phases. | 2026-07-31, current — this run followed it |
| File specifications | [.claude/skills/md-orchestrator/references/file-specs.md](.claude/skills/md-orchestrator/references/file-specs.md) | The per-file contract: front-matter keys, exact section headings, line budgets, canonical anchors, footer, quality bar. The authority when any core file's shape is questioned. | 2026-07-31, current |
| Maintenance (SYNC / AUDIT) | [.claude/skills/md-orchestrator/references/maintenance.md](.claude/skills/md-orchestrator/references/maintenance.md) | How to detect and repair documentation drift, the month-later re-entry protocol, cadence, anti-patterns. Read this before the next doc refresh. | 2026-07-31, current in intent — its AUDIT commands assume `git log`/`git diff`, and this repository is not yet a git repository |
| Migration | [.claude/skills/md-orchestrator/references/migration.md](.claude/skills/md-orchestrator/references/migration.md) | Consolidating pre-existing scattered markdown: inventory, claim ledger, verify against code, resolve conflicts, archive, generate, prove no loss. | 2026-07-31, applied this run to the single pre-existing `RULES.md` |
| Self-installation | [.claude/skills/md-orchestrator/references/self-install.md](.claude/skills/md-orchestrator/references/self-install.md) | Vendoring the skill into `.claude/skills/`, installing the slash commands, the marker and maintenance clause, optional hooks and CI. | 2026-07-31, applied this run — skill vendored and all three commands installed |
| Sub-agent briefs | [.claude/skills/md-orchestrator/references/subagent-briefs.md](.claude/skills/md-orchestrator/references/subagent-briefs.md) | The dispatch brief template, wave order, per-file mandates, coordinator duties between waves. Explains why each core file has exactly one owner. | 2026-07-31, current |
| `/md-sync` command | [.claude/commands/md-sync.md](.claude/commands/md-sync.md) | Re-syncs this documentation set with the code: read `maintenance.md`, detect drift, re-dispatch only the affected file agents plus memory-agent, validate with `validate_docs.py --strict`. | 2026-08-04, current |
| `/md-audit` command | [.claude/commands/md-audit.md](.claude/commands/md-audit.md) | Read-only drift report: `validate_docs.py --report` plus a comparison of the repository against each file's `updated:` date. Changes nothing. | 2026-08-04, current |
| `/md-recall` command | [.claude/commands/md-recall.md](.claude/commands/md-recall.md) | The month-later re-entry protocol: MEMORY.md current state, open threads, changes since that date, validation report, then a five-line reconstruction naming what is uncertain. | 2026-08-04, current |
| Archive index | [docs/_archive/2026-08-04/ARCHIVE-INDEX.md](docs/_archive/2026-08-04/ARCHIVE-INDEX.md) | Maps each archived file to its successors and carries a claim-by-claim coverage table proving nothing was dropped. | 2026-08-04, current |
| Archived RULES | [docs/_archive/2026-08-04/RULES.md](docs/_archive/2026-08-04/RULES.md) | The pre-orchestrator rules file, superseded by the root `RULES.md` written this run. See [Archive](#archive). | 2026-08-04, frozen by definition |

`/md-sync` currently means *re-run the md-orchestrator skill in SYNC mode*: the separate `md-sync`
skill referenced by older notes is not installed on this machine, and the command file compensates
by driving md-orchestrator directly. Nothing else in this table duplicates a core file's job — the
skill files describe how documentation is produced, not what this application does.

## External references

No URLs are recorded here. Each entry names the document precisely enough to find it by title; a
wrong link is worse than a name, and none of these could be verified from this machine.

> ❓ Unverified: every entry below is identified by document title only — the exact published URLs
> were not confirmed during this run.

**Pseudo console (ConPTY) — the primary backend**

- Microsoft Windows Console documentation, *Creating a Pseudoconsole Session* — the end-to-end
  sample this project's `BetterTerminal.Terminal/ConPtySession.cs` is structured after.
- `CreatePseudoConsole`, `ResizePseudoConsole`, `ClosePseudoConsole` function pages
  (`consoleapi.h`, Windows 10 version 1809 / build 17763 and later) — the three entry points
  declared in `BetterTerminal.Interop/NativeMethods.cs`; the 17763 floor is the reason
  `TerminalSessionFactory.Resolve` has a fallback path at all.
- `PROC_THREAD_ATTRIBUTE_PSEUDOCONSOLE` in the *Process and Thread Attributes* /
  `UpdateProcThreadAttribute` reference, together with `InitializeProcThreadAttributeList` and
  `DeleteProcThreadAttributeList` — the attribute value `0x00020016` and the two-call sizing
  protocol implemented in `BetterTerminal.Interop/SafeProcThreadAttributeList.cs` and used by
  `ConPtySession` with `EXTENDED_STARTUPINFO_PRESENT`.

**Process lifetime**

- *Job Objects* overview plus `JOBOBJECT_EXTENDED_LIMIT_INFORMATION` and the
  `JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE` flag — the mechanism in
  `BetterTerminal.Terminal/ProcessJob.cs` that guarantees no orphaned shell or console host when a
  pane, the window, or the whole process dies. Mirrored by
  `BetterTerminal.Interop/JobObjectExtendedLimitInformation.cs`.
- `RtlGetVersion` (ntdll, Windows Driver Kit reference) — used instead of `Environment.OSVersion`
  because .NET Framework reports 6.2 without an OS compatibility manifest; declared in
  `NativeMethods.cs`, consumed by `ConPtySession.IsSupported`.

**Hosting a real console window — the fallback backend**

- WPF *HwndHost class* reference and the *WPF and Win32 Interoperation* guide — `HwndHost` is the
  base class of `BetterTerminal.Terminal/ConsoleHwndHost.cs`, which reparents a real
  `ConsoleWindowClass` window with `SetParent` and keeps it sized with `MoveWindow`.

**Rendering**

- WPF *GlyphRun class* and *GlyphTypeface class* references, including the `GlyphRun` constructor
  overload that takes `pixelsPerDip` — `BetterTerminal.Terminal/TerminalRenderer.cs` draws every
  row through `GlyphRun` and measures cell metrics from the typeface rather than guessing; the
  pixelsPerDip overload is what keeps the build warning-free.

**Persistence**

- `DataContractJsonSerializer` class reference and the *Data Contract* / `DataMemberAttribute`
  documentation — `BetterTerminal.Shell/SessionStore.cs` serialises the workspace with it, and the
  lowercase `Name = "..."` values on each `[DataMember]` are the JSON schema in
  [API references](#api-references).

**Terminal control sequences — what the parser is written against**

- ECMA-48, *Control Functions for Coded Character Sets* (5th edition) — the standard behind the C0
  controls, CSI framing and SGR handling in `BetterTerminal.Terminal/VtParser.cs`.
- *XTerm Control Sequences* (`ctlseqs`, Thomas E. Dickey) — the practical reference for the DEC
  private modes actually implemented (1, 7, 25, 47/1047/1049, 1048, 2004), OSC 0 and 2 title
  setting, and DSR/DA answerback routed through `VtParser.ResponseWriter`.

**Verification tooling (documents behind `tools/`)**

- `PrintWindow` function reference including the `PW_RENDERFULLCONTENT` flag (value 2) — the reason
  `tools/capture-window.ps1` can screenshot an occluded window without stealing focus.
- UI Automation *InvokePattern* / `IUIAutomationInvokePattern` reference — how
  `tools/ui-smoke.ps1` drives toolbar buttons by name (`New tab`, `Split right`, `Split down`,
  `Close pane`, `Commands`).

## API references

This application exposes **no network API**. There is no OpenAPI or Swagger document, no protobuf
or gRPC definition, no generated reference site, and no XML documentation build. Nothing here is
published for external consumers. What follows are the four internal contracts worth pointing at.

**1. The session contract — `BetterTerminal.Terminal/ITerminalSession.cs`**

The single interface both backends implement, and the only thing the UI is allowed to depend on.
Members: `Title`, `IsRunning`, `ExitCode` (`int?`), `Columns`, `Rows`; events `OutputReceived`,
`TitleChanged`, `Exited`; methods `Start(ShellProfile, workingDirectory)`, `Write(string)`,
`Resize(columns, rows)`, `Close()`; plus `IDisposable`. Implementations: `ConPtySession` and
`HwndConsoleSession`. `HwndConsoleSession.Write` throws `NotSupportedException` by design and its
`OutputReceived` never fires — a documented, deliberate hole in the contract, not a bug.
Selection happens in `TerminalSessionFactory.Resolve`.

**2. The Win32 surface — `BetterTerminal.Interop/`**

Every P/Invoke signature is declared in `BetterTerminal.Interop/NativeMethods.cs`; handle ownership
lives in `SafeKernelHandle.cs`, `SafePseudoConsoleHandle.cs` and `SafeProcThreadAttributeList.cs`;
the marshalled structs are the remaining files in that project. The complete audit surface:

- **kernel32.dll** — CreatePipe, CreatePseudoConsole, ResizePseudoConsole, ClosePseudoConsole,
  InitializeProcThreadAttributeList, UpdateProcThreadAttribute, DeleteProcThreadAttributeList,
  CreateProcessW, GetExitCodeProcess, CloseHandle, CreateJobObjectW, SetInformationJobObject,
  AssignProcessToJobObject.
- **user32.dll** — CreateWindowExW, DestroyWindow, SetParent, GetWindowLongPtrW, SetWindowLongPtrW,
  SetWindowPos, MoveWindow, ShowWindow, SetFocus, EnumWindows, GetWindowThreadProcessId,
  GetClassNameW, GetWindowTextW.
- **ntdll.dll** — RtlGetVersion.

Anything not on this list is not called. Adding to it is an interop change and carries the
SafeHandle / CharSet / Pack discipline in [RULES.md](RULES.md#hard-rules).

**3. The persisted workspace schema — `%APPDATA%\BetterTerminal\workspace.json`**

Written by `SessionStore` with `DataContractJsonSerializer` on window close, read on start. The
wire names are the lowercase `[DataMember(Name = ...)]` values, verified against the source:

- `PersistedWorkspace` — `backend` (string), `selectedTab` (int), `tabs` (array of `PersistedTab`).
- `PersistedTab` — `header` (string), `root` (`PersistedNode`).
- `PersistedNode` — `kind` (`"pane"` or `"split"`, the constants `PaneKind`/`SplitKind`),
  `shell`, `workingDirectory`, `orientation`, `firstRatio` (double), `first` and `second`
  (nested `PersistedNode`, present only for splits).

The tree is recursive through `first`/`second`, which is what restores split layouts and ratios.
There is no version field, so an older file is read on a best-effort basis.

**4. The VT sequence subset — `BetterTerminal.Terminal/VtParser.cs`**

The parser implements a deliberate subset, not a full terminal: C0 controls; CSI cursor movement
(A–H, f, d, G), erase (J, K, X), insert/delete (L, M, P, @), scroll (S, T), scroll region (r),
save/restore (s, u), SGR (m), DEC private mode set/reset (h, l), DSR (n), DA (c); OSC 0 and 2 for
the window title; DCS/APC/PM payloads discarded; charset selectors skipped. DEC private modes
implemented: 1, 7, 25, 47, 1047, 1049, 1048, 2004. Unknown sequences are dropped silently and are
never printed. Colours come from `TerminalPalette` (Campbell 16 extended to xterm-256).

## Archive

Archived material lives under `docs/_archive/<YYYY-MM-DD>/`. Nothing is ever deleted; a superseded
file is moved here with the date of the run that replaced it. Each archive set carries its own
index: [docs/_archive/2026-08-04/ARCHIVE-INDEX.md](docs/_archive/2026-08-04/ARCHIVE-INDEX.md) maps
old path to new home and shows, claim by claim, where every statement went.

| date | path | reason |
|---|---|---|
| 2026-08-04 | [docs/_archive/2026-08-04/RULES.md](docs/_archive/2026-08-04/RULES.md) | The pre-orchestrator rules file. Superseded by the root `RULES.md` produced by this documentation run, which restates its content (md-orchestrator workflow, no external API or package names in the UI, .NET Framework 4.8 / C# 7.3 / WPF / x64 with no new packages, job object per process, no exception may escape a background IO thread, teardown order, git and installs only on explicit request) in the fixed contract shape. |

`.gitignore` excludes generated archive payloads (`docs/_archive/*/flood.txt`), so benchmark output
placed here is intentionally not preserved.

---
[← CLAUDE.md](CLAUDE.md) · [STRUCTURE](STRUCTURE.md) · [WORKFLOWS](WORKFLOWS.md) · [MEMORY](MEMORY.md)
