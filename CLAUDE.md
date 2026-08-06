<!-- MD-ORCHESTRATOR:v1 -->
---
updated: 2026-08-05
scope: Router and session contract. Detail lives in the linked files.
stability: stable
sources: [scan]
owner_agent: claude-router-agent
---

# BetterTerminal - Claude entry point

BetterTerminal is a Windows desktop shell application: a WPF window that hosts multiple live cmd.exe and PowerShell sessions in tabs and splits, backed by ConPTY with a reparented-console fallback. All three planned phases are complete and both x64 configurations build warning-clean; there is no git repository, no CI and no automated test project.

Since 2026-08-05 the first run installs a copy of itself under `%LOCALAPPDATA%\BetterTerminal\app` and registers the command `beterm`, which opens the folder it was called from as a project with its settings in a hidden `.beterm` folder; a per-user address book of remote connections sits behind the command bar's SSH button.

## Read this first
1. [RULES.md](RULES.md#hard-rules) - constraints that override anything else
2. [STRUCTURE.md](STRUCTURE.md#directory-map) - where things live
3. [MEMORY.md](MEMORY.md#current-state) - what happened recently and why

## Documentation map

| File | Answers | Read when |
| --- | --- | --- |
| [RULES.md](RULES.md#hard-rules) | What must never change or be done | Before proposing or writing any change |
| [STRUCTURE.md](STRUCTURE.md#directory-map) | Which project owns what, how data flows | Locating code or deciding where new code goes |
| [WORKFLOWS.md](WORKFLOWS.md#daily-development) | The exact build, run and verify commands | Running anything on this machine |
| [MEMORY.md](MEMORY.md#current-state) | Current state, decisions and their reasons | Starting a session or questioning a design |
| [AGENTS.md](AGENTS.md#agent-roster) | Which subagent does what and how work is handed off | Delegating or splitting a task |
| [TIPS.md](TIPS.md#gotchas) | Traps: harness, encoding, interop, DPI | Something behaves inexplicably |
| [DOCS.md](DOCS.md#internal-docs) | Where docs and external references live | Hunting for a spec or an archived file |
| [README.md](README.md#quick-start) | Onboarding for an outside developer | Setting the project up from scratch |

## Session contract
- Start: read RULES.md and MEMORY.md#current-state before proposing changes.
- During: prefer the commands in [WORKFLOWS.md](WORKFLOWS.md#daily-development); do not invent commands.
- End: append anything decided or learned to [MEMORY.md](MEMORY.md#decision-log). This is what makes the next session cheap.
- No git write operation (commit, push, merge, rebase, reset, tag, PR) unless the user names it in the same run; read-only git is fine. See [RULES.md](RULES.md#git-rules).
- No external API or package name (ConPTY, conhost, WPF, Win32) in user-visible UI text; code comments and docs only. See [RULES.md](RULES.md#never-do).

## Fast facts
- Stack: .NET Framework 4.8, C# 7.3, WPF, x64 only, classic non-SDK csproj, zero NuGet packages. Nine projects (eight .NET, one native C++): Interop, Terminal, Shell (the app), Wrap (`beterm-wrap.exe`, a console front end for `tools\*.ps1`), Banner (`beterm-banner.exe`, what a session prints when it opens), AIWizard (a DLL of CLI-AI Wizard logic), AIWizard.Cli (`beterm-aiwizard.exe`, the wizard the shell picker can launch), Service (`beterm-service.exe`, a Windows service host) and Bootstrap (a C++ `vcxproj` building `BetterTerminal-Launcher.exe`, a one-file launcher that embeds and unpacks the whole app). The last four landed 2026-08-06 as BETA.
- Run: `BetterTerminal.Shell\bin\x64\Debug\BetterTerminal.exe` · Test: no test project exists - verification is a warning-clean rebuild plus the `tools\ui-smoke.ps1` pane sequences, see [WORKFLOWS.md](WORKFLOWS.md#testing) · Build: `MSBuild.exe "BetterTerminal.sln" /p:Configuration=Debug /p:Platform=x64` (also `Release`).
- Entry point: `BetterTerminal.Shell` (`App.xaml`, `MainWindow.xaml`), see [STRUCTURE.md](STRUCTURE.md#entry-points).
- Never touch: `bin\`, `obj\`, `.vs\`, `docs\_archive\`.

## Maintaining these docs
This documentation set is generated and maintained by the `md-orchestrator` skill.
Run `/md-sync` after significant changes and `/md-audit` when returning after a break.
The `/md-sync`, `/md-audit` and `/md-recall` commands are installed in `.claude/commands/`; only the separate `md-sync` skill is missing, so `/md-sync` re-runs `md-orchestrator` in SYNC mode.
Never hand-edit more than one file without re-running the sync - the files are cross-linked by contract.
