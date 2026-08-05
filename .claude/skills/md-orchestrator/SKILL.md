---
name: md-orchestrator
description: Builds and maintains a CLAUDE.md orchestrator documentation system - a thin CLAUDE.md entry point that links to AGENTS.md, WORKFLOWS.md, STRUCTURE.md, MEMORY.md, RULES.md, TIPS.md, DOCS.md and README.md. Use this skill whenever the user mentions CLAUDE.md, AGENTS.md, agent docs, project memory, "orchestrator", "orchestror", documentation cleanup, onboarding docs, or asks to set up / refactor / merge / audit / re-sync project documentation for Claude Code - and also when the user says any of "vytvor CLAUDE.md", "orchestror", "udelej poradek v dokumentaci", "propoj MD soubory", "MD agent", "md-sync", "predelej dokumentaci". Use it even when the user only asks for "a CLAUDE.md" or "docs for this repo" without naming the orchestrator pattern, and use it for existing projects with scattered, stale or duplicated markdown that needs to be consolidated into one maintainable system that still makes sense when you return to the project a month later.
---

# MD Orchestrator

Produce one documentation system that survives absence: a **thin CLAUDE.md router** plus **eight specialised files**, each owned by exactly one sub-agent, cross-linked through a fixed contract, and verifiable by script.

The value is not "more docs". The value is that in a month someone (or a fresh Claude session) opens CLAUDE.md, reads ~120 lines, and knows exactly which file answers their question — and that every one of those files says when it was last true.

---

## 0. Non-negotiable rules

These four rules define the skill. If a rule cannot be satisfied, stop and tell the user why rather than silently degrading.

**R1 — One agent per file.** Every generated `.md` file is written by its own dedicated sub-agent. The main agent is a **coordinator**: it scans, builds the context packet, dispatches, reviews, validates, and reports. The coordinator never writes file bodies itself. See `references/subagent-briefs.md`.

**R2 — CLI status protocol.** Every phase announces itself through `scripts/md_agent_log.sh` (red on start, blue during work, yellow near the end, green on completion). Never fake the banners by printing plain text — run the script, so colours, timing and the `.claude/md-agent.log` trail are real.

**R3 — Never destroy, always archive.** Any pre-existing markdown that gets replaced moves to `docs/_archive/<YYYY-MM-DD>/` with an `ARCHIVE-INDEX.md` mapping old path → new home. Deletion happens only after the user confirms the coverage report shows zero information loss.

**R4 — Fixed link contract.** File names, anchors and the direction of links are specified in `references/file-specs.md` and are not negotiable per-project. Deterministic anchors are what let eight parallel agents cross-link without seeing each other's output.

---

## 1. Announce, then detect the mode

Start every run with the banner, then decide which of the four modes applies:

```bash
bash scripts/md_agent_log.sh start
bash scripts/scan_project.sh > /tmp/md-scan.txt   # inventory, never modifies anything
```

| Mode | Trigger | Path |
|---|---|---|
| **INIT** | No `CLAUDE.md`, ≤2 stray markdown files | Section 2 → 4 |
| **MIGRATE** | Existing `CLAUDE.md`, `.cursorrules`, `AGENTS.md`, `docs/`, or scattered markdown | Read `references/migration.md` first, then Section 2 → 4 |
| **SYNC** | The orchestrator already exists (CLAUDE.md carries the `<!-- MD-ORCHESTRATOR:v1 -->` marker) and the user wants an update | Read `references/maintenance.md` |
| **AUDIT** | User asks "is our documentation still accurate / stale?" | `scripts/validate_docs.py --report`, then propose a SYNC |

State the detected mode to the user in one line before proceeding. If the repo is large or the mode is ambiguous (e.g. a half-migrated monorepo), ask once — a wrong mode wastes an entire generation cycle.

---

## 2. Research phase — the coordinator's only writing job

```bash
bash scripts/md_agent_log.sh think "Reading repository"
```

The sub-agents will each see only what the coordinator hands them, so the quality of the whole system is decided here. Gather:

- **Build & run reality**: package manifests, lockfiles, `Makefile`, CI configs, `docker-compose`, scripts sections. Prefer commands that actually exist over commands that sound plausible.
- **Structure**: top-level directories, where the entry points live, where tests live, what is generated vs authored.
- **Conventions**: linter/formatter configs, commit history style (`git log --oneline -40`), naming patterns, test layout.
- **Existing knowledge**: every markdown file found by the scan, plus `.cursorrules`, `.github/copilot-instructions.md`, `.windsurfrules`, issue templates, ADRs.
- **Decisions and their reasons**: `git log` merge messages, comments marked TODO/HACK/NOTE, anything the user explains in chat.
- **Gaps**: things you could not determine. These get written down as open questions, never guessed.

For MIGRATE, also extract every *claim* from the old docs into a claim ledger (`/tmp/md-claims.md`): `claim | source file:line | still true? | destination file`. `references/migration.md` describes how to verify claims against the code and how to resolve contradictions between two old files.

Write the result to `/tmp/md-context-packet.md`. This single file is what every sub-agent receives. If a fact is not in the packet, no agent knows it.

**Ask the user before dispatching** — one round, at most three questions, only about things the repo cannot tell you (deployment targets, team conventions, what future-you will actually forget). Use the interactive question tool if the host offers one.

---

## 3. Dispatch phase — eight agents, one file each

```bash
bash scripts/md_agent_log.sh generate "Dispatching 8 file agents"
```

Dispatch in **two waves**, because two files depend on the others' final shape:

- **Wave 1 (parallel, 6 agents)**: `STRUCTURE.md`, `RULES.md`, `WORKFLOWS.md`, `AGENTS.md`, `TIPS.md`, `DOCS.md`
- **Wave 2 (parallel, 2 agents)**: `MEMORY.md` (needs the decision set the others surfaced) and `README.md` (human-facing summary of the finished system)
- **CLAUDE.md is written last, by its own dedicated agent** — a ninth agent, dispatched only after both waves return, so the router links to what genuinely exists.

Each dispatch uses the brief template in `references/subagent-briefs.md`. Every brief carries: the context packet, that file's spec section from `references/file-specs.md`, the fixed anchor list, the length budget, the return contract, and the ban on touching any other file. An agent that cannot verify a fact must write it into its `open_questions` return field rather than inventing it.

When an agent reports back, log it:

```bash
bash scripts/md_agent_log.sh agent "RULES.md" "done - 74 lines, 3 open questions"
```

If any agent returns thin or generic output ("follow best practices", "the code is organised into modules"), re-dispatch that one agent with the specific missing evidence. Generic documentation is the failure mode this whole skill exists to prevent — one weak file makes the router untrustworthy.

For deep or contradictory repositories, escalate one wave to maximum reasoning and say so:

```bash
bash scripts/md_agent_log.sh ultrathink "Resolving 6 conflicting claims across 4 legacy docs"
```

**No sub-agents available (e.g. plain chat UI)?** Fall back to sequential single-file passes: one file per pass, in the wave order above, and between passes clear your working focus back to the context packet only. Tell the user you are running in sequential fallback — the contract still holds, only the parallelism is lost.

---

## 4. Final steps — wiring, self-install, verification

```bash
bash scripts/md_agent_log.sh final
```

1. **Self-installation** so the skill stays available in this project — see `references/self-install.md`. In short: copy the skill into `.claude/skills/md-orchestrator/`, install the `/md-sync` and `/md-audit` commands, and stamp the `<!-- MD-ORCHESTRATOR:v1 -->` marker plus a maintenance clause into CLAUDE.md and RULES.md. Show the user what will be written before writing it.
2. **Archive** the superseded originals per R3 and generate `docs/_archive/<date>/ARCHIVE-INDEX.md`.
3. **Validate**:
   ```bash
   python3 scripts/validate_docs.py --root . --strict
   ```
   This checks: all nine files present, front-matter valid, every internal link resolves, every anchor referenced by CLAUDE.md exists, no orphan markdown outside the archive, no file over its length budget, no `TODO-GENERATED` left behind. Fix findings by re-dispatching the owning agent — never by patching a file the coordinator does not own.
4. **Coverage report** (MIGRATE only): every claim in the ledger is either carried into a new file or explicitly listed as dropped-with-reason. Present it before offering deletion of the archive.

```bash
bash scripts/md_agent_log.sh done
```

Close with a short human summary: what was created, what moved where, the open questions each agent raised, and the one command (`/md-sync`) to run after significant changes.

---

## 5. The output at a glance

```
CLAUDE.md          router only, ~120 lines, links to all eight
├── README.md      human entry point: what this project is, how to run it
├── STRUCTURE.md   directory map, entry points, data flow, where to add things
├── RULES.md       hard constraints - what must and must never happen
├── WORKFLOWS.md   repeatable procedures: setup, test, release, debug
├── AGENTS.md      agent roles, tool permissions, delegation boundaries
├── MEMORY.md      dated decision log + current state - the "why", append-only
├── TIPS.md        hard-won specifics, gotchas, performance traps
└── DOCS.md        index of external + deeper docs, API refs, runbooks
```

Every file carries YAML front-matter (`updated`, `scope`, `stability`, `sources`, `owner_agent`) and a footer link back to CLAUDE.md. Exact templates, anchors and length budgets: `references/file-specs.md`.

---

## 6. Reference files

Read the one you need, when you need it — they are written to be loaded individually.

- `references/file-specs.md` — the spec and template for all nine files, canonical anchors, length budgets. **Required before any dispatch.**
- `references/subagent-briefs.md` — the dispatch brief template and the per-file mandate for each of the nine agents.
- `references/migration.md` — MIGRATE mode: inventory, claim ledger, conflict resolution, archiving, coverage report.
- `references/maintenance.md` — SYNC/AUDIT mode: staleness rules, the month-later re-entry protocol, what MEMORY.md must capture to survive a gap.
- `references/self-install.md` — making the skill permanently available in the project, `/md-sync` and `/md-audit` commands, optional hook.

Scripts (all stdlib / plain bash, safe to run repeatedly):

- `scripts/md_agent_log.sh` — the `[ MD AGENT ]` status protocol.
- `scripts/scan_project.sh` — read-only inventory of markdown, agent-config files and repo shape.
- `scripts/validate_docs.py` — link, anchor, front-matter, budget and orphan validation.
