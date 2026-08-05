---
updated: 2026-08-04
scope: How work is split between agents on BetterTerminal - roster, delegation boundaries, tool permissions, handoff shape, escalation. Not a description of the code itself.
stability: evolving
sources: [.claude/skills/md-orchestrator/SKILL.md, .claude/skills/md-orchestrator/references/subagent-briefs.md, .claude/skills/md-orchestrator/references/file-specs.md, BetterTerminal.Shell/BetterTerminal.Shell.csproj, BetterTerminal.Terminal/BetterTerminal.Terminal.csproj, BetterTerminal.Interop/BetterTerminal.Interop.csproj, tools/, md-context-packet]
owner_agent: agents-agent
---

# AGENTS.md

The roster here is small and honest about it. This repository has **no `.claude/agents/` directory
and no `settings.json`**. What `.claude/` does hold is the vendored skill
`.claude/skills/md-orchestrator/` (unpacked from `.claude/skills/md-orchestrator.skill`), the three
commands in `.claude/commands/`, and the log file `.claude/md-agent.log`. The nine documentation
agents below are real: they are defined by that vendored skill and they wrote this documentation
set. The four engineering roles below are **role boundaries, not configured agents** - they describe
how a single session should partition its own work, or how sub-agents should be briefed when the
host offers them.

`.claude/commands/` holds `md-sync.md`, `md-audit.md` and `md-recall.md`, so `/md-sync`, `/md-audit`
and `/md-recall` are invocable in this project. The separate **md-sync skill** is not installed -
only md-orchestrator is vendored - so `/md-sync` resolves to re-running `md-orchestrator` in SYNC
mode. The closing step of a task is executable; it just runs md-orchestrator rather than a dedicated
skill. Same wording as [RULES.md](RULES.md#hard-rules) R11.

## Agent roster

### Documentation agents (from the installed `md-orchestrator` skill)

Nine agents, one markdown file each. Each writes exactly one file and never edits another.

| agent | responsibility | owns (paths) | must not touch |
|---|---|---|---|
| structure-agent | Directory map, entry points, data flow, where to add things, boundaries | `STRUCTURE.md` | any other `.md`, any `.cs`, any `.csproj` |
| rules-agent | Constraints marked `[enforced]` or `[convention]`, git rules, never-do list | `RULES.md` | the other eight `.md`, source |
| workflows-agent | Runnable procedures built from `tools/*.ps1` and real MSBuild invocations | `WORKFLOWS.md` | the other eight `.md`, source |
| agents-agent | This file: roster, delegation, permissions, handoff, escalation | `AGENTS.md` | the other eight `.md`, source |
| tips-agent | Symptom -> cause -> fix for the non-obvious (harness gotchas, BOM, focus theft) | `TIPS.md` | the other eight `.md`, source |
| docs-agent | Index of every markdown outside the core nine, plus `docs/_archive/` | `DOCS.md` | the other eight `.md`, source |
| memory-agent | Dated decision log, current state, open threads, failed experiments | `MEMORY.md` | the other eight `.md`, source |
| readme-agent | Outside-developer onboarding: what it is, build, run, contribute | `README.md` | the other eight `.md`, source |
| claude-router-agent | Router only - map table, session contract, fast facts, `<!-- MD-ORCHESTRATOR:v1 -->` marker | `CLAUDE.md` | all eight owned files, source |

### Engineering roles (boundaries, not installed agents)

| role | responsibility | owns (paths) | must not touch |
|---|---|---|---|
| interop | Every P/Invoke signature, SafeHandle, Win32 struct, `RtlGetVersion` build check | `BetterTerminal.Interop\` | `BetterTerminal.Terminal\`, `BetterTerminal.Shell\`; must not add a project reference to either |
| terminal-core | `ITerminalSession` contract, `ConPtySession`, `HwndConsoleSession`, `VtParser`, `CellGrid`, `TerminalRenderer`, `TerminalSessionFactory` | `BetterTerminal.Terminal\` | any `DllImport` (belongs to interop), any XAML, `SessionStore` |
| shell-ui | Window, tabs, `SplitPane` tree, `TerminalPane`, `CommandPalette`, `Theme.xaml`, `app.manifest`, workspace persistence | `BetterTerminal.Shell\` | `VtParser`/`CellGrid` internals, `NativeMethods.cs`; must not branch on backend outside `TerminalPane` |
| verification | Smoke, flood and cycle runs; reporting pass/fail with numbers | `tools\*.ps1` | product source - a failing run is reported, not patched by this role |

**The `DllImport` rule is checked, not aspirational.** `DllImport` currently appears only in
`BetterTerminal.Interop\NativeMethods.cs`, `SafeKernelHandle.cs` and `SafePseudoConsoleHandle.cs`.
A `DllImport` anywhere else is a boundary violation - see [STRUCTURE.md](STRUCTURE.md#boundaries).

## Delegation protocol

### Documentation work

- **One agent per file.** No file has two authors. When a file comes back weak, exactly one agent is
  re-dispatched with the specific missing evidence - never with "make it better", and never patched
  by the coordinator.
- **The coordinator writes no file body.** Its only writing job is the context packet
  (`md-context-packet.md` in the session scratchpad). If a fact is not in the packet, no agent knows it.
- **Three waves.**
  1. Six in parallel: structure, rules, workflows, agents, tips, docs. Independent, derived straight
     from the repository.
  2. Two in parallel: memory and readme. They consume wave-1 `key_facts` and `cross_file_notes`.
  3. The router alone: `CLAUDE.md`, dispatched only after both waves return, so every link points at
     a file and anchor that genuinely exists.
- **Cross-file routing.** An agent that finds a fact belonging to someone else's file puts it in
  `cross_file_notes` and moves on. Between waves the coordinator routes each note to its owner; if
  that owner already returned, it is re-dispatched with the addition. This is what makes single
  ownership survive contact with a real repository.
- **Anchors are contractual.** Agents never read each other's drafts. They link to the canonical
  anchors only: `RULES.md#hard-rules`, `#git-rules`, `#never-do`; `STRUCTURE.md#directory-map`,
  `#where-to-add-things`, `#boundaries`; `WORKFLOWS.md#testing`, `#debugging`;
  `MEMORY.md#decision-log`, `#open-threads`; `TIPS.md#gotchas`; `DOCS.md#internal-docs`.
- **No sub-agents available?** Run sequentially in wave order, one file per pass, resetting focus to
  the context packet between passes, and say so. The contract holds; only parallelism is lost.

### Engineering work

The boundary that matters in this repository is the dependency graph, and it is one-directional:

```
BetterTerminal.Interop     -> no ProjectReference at all
BetterTerminal.Terminal    -> references Interop
BetterTerminal.Shell       -> references Interop and Terminal
```

Therefore: **a change that crosses two of the three projects settles the interop contract first.**
Both consumers depend on `BetterTerminal.Interop`, and it can depend on neither of them, so a
signature or struct layout decided after the callers are written forces a rewrite of both. Sequence
is: agree the P/Invoke signature and struct layout -> land it in Interop -> then let terminal-core
and shell-ui proceed, in that order, in parallel only once the contract is frozen.

A change confined to one project may proceed directly. A change touching all three is not a
delegation problem - it is an escalation (see [Escalation](#escalation)).

Cross-role handoffs use the same return structure as documentation agents, so a mixed run stays
reviewable in one format.

## Tool permissions

**Free - any agent, no confirmation:**

- Reading any source, `.csproj`, `.sln`, markdown or script in the repository.
- MSBuild from
  `C:\Program Files\Microsoft Visual Studio\18\Community\MSBuild\Current\Bin\MSBuild.exe`, any
  configuration, including `/t:Rebuild`. Classic csproj means no NuGet restore runs, so a build
  installs nothing. Pass condition is zero errors **and** zero warnings.
- Read-only git (`status`, `log`, `diff`, `show`) - noting that this is **not a git repository yet**,
  so these commands currently have nothing to report.
- The repository scripts: `tools\capture-window.ps1`, `tools\ui-smoke.ps1`,
  `tools\flood-benchmark.ps1`, `tools\session-cycle.ps1`. Details in
  [WORKFLOWS.md](WORKFLOWS.md#testing).

**Requires the user to ask for it by name, in the same run:**

- Any git write: `commit`, `push`, `merge`, `rebase`, `reset --hard`, `tag`, `cherry-pick`,
  `revert`, creating or merging a PR, and `git init` itself.
- Installing any package or dependency, by any package manager.
- Editing a `.csproj` to add a dependency or reference. The repository deliberately has zero NuGet
  packages; adding one is a decision, not an implementation detail. See
  [RULES.md](RULES.md#hard-rules).

**Allowed but touches the user's machine - use the least intrusive form:**

- Screenshots: `tools\capture-window.ps1` uses `PrintWindow` with `PW_RENDERFULLCONTENT`, which
  works while the window is occluded and **never steals foreground focus**. Prefer it over any
  screen-grab approach.
- `Start-Process` of `BetterTerminal.exe` **does open a real window on the user's desktop.**
  `tools\ui-smoke.ps1` starts the app itself; say so before running it and close what you opened.
- Killing processes by name is dangerous here: this project's own product hosts `cmd.exe`,
  `powershell.exe` and `conhost.exe`, so a name-based kill can take out consoles the user is using.
  Kill by PID captured from the process you started, never by name.
- `flood-benchmark.ps1` and `session-cycle.ps1` must be launched via
  `Start-Process powershell -WindowStyle Hidden -Wait`. In a process with redirected standard
  handles the child shell writes into those handles instead of the pseudo console and the grid stays
  empty - a false failure. See [TIPS.md](TIPS.md#gotchas).

## Handoff format

Every agent, documentation or engineering, returns exactly this and nothing else:

```
file: <absolute path, or the paths changed>
lines: <n>
sections: [<the ## headings emitted>]
anchors_emitted: [<slugs, so the coordinator can verify the link contract>]
key_facts: [<3-8 things a future reader most needs, one line each>]
open_questions: [<what could not be verified, and why it matters>]
cross_file_notes: [<facts found that belong to another agent>]
```

Why the rigidity is worth it:

- `lines` and `sections` are checked against the budget and the required headings without opening
  the file, so an off-spec return is caught before the next wave starts.
- `anchors_emitted` is how the router can link into eight files it never read. Without it, wave 3
  would have to open everything and the parallelism would buy nothing.
- `open_questions` collected across all agents is frequently the most valuable output of a run: it
  is the list of things the project itself does not know. It is never silently resolved by guessing.
- `cross_file_notes` keeps single ownership from becoming information loss.

An unverifiable claim that still matters to a reader goes into the file body as
`> ❓ Unverified: <claim> - not confirmed against code.` A visible hole beats a smooth guess.

## Escalation

Stop and ask the user. Do not proceed on a best guess when any of these is true:

1. **A pinned version is ambiguous.** .NET Framework 4.8, C# 7.3, WPF, x64-only and classic csproj
   are non-negotiable. If a task seems to require C# 8+ syntax, SDK-style projects, AnyCPU or a
   different framework, that is a decision for the user, not a workaround.
2. **Two rules conflict**, or a task instruction contradicts [RULES.md](RULES.md#hard-rules). Name
   both sides; do not pick one.
3. **A new dependency would be needed** - any NuGet package, any new assembly reference. The
   zero-package state is deliberate.
4. **A Win32 signature cannot be confirmed against documentation.** Never invent a P/Invoke
   declaration, struct layout, `Pack` value or flag constant. A wrong signature does not fail to
   compile; it corrupts memory at runtime, on x64, intermittently. If the documented layout cannot
   be found, stop.
5. **One change would touch more than one project.** Take it back to the interop-contract-first
   sequence in [Delegation protocol](#delegation-protocol) rather than editing across the boundary
   in one pass.
6. **A user-visible string would name an external API or package.** No occurrence of the pseudo
   console API, the console host, WPF, Win32 or any package name may appear in window chrome, pane
   headers, the status bar, the command palette or any message the user reads. Those names live in
   code comments and in this documentation set only. If a feature seems to require naming one, ask
   for the wording. See [MEMORY.md](MEMORY.md#decision-log).
7. **A background IO thread would be allowed to throw.** Not a judgement call - an escaping exception
   on a reader or writer thread terminates the whole application and takes every other pane with it.
   If a design needs an exception to propagate off a background thread, it is the wrong design.

> ❓ Unverified: whether the user wants the four engineering roles turned into real
> `.claude/agents/*.md` definitions - none exist today, and the roster above documents boundaries
> rather than installed configuration.

---
[← CLAUDE.md](CLAUDE.md) · [STRUCTURE](STRUCTURE.md) · [WORKFLOWS](WORKFLOWS.md) · [MEMORY](MEMORY.md)
