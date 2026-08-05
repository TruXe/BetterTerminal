# File specifications

The contract every sub-agent writes against. Names, anchors and front-matter are fixed so that eight agents working in parallel can link to each other's output without ever seeing it.

## Contents

- [Shared conventions](#shared-conventions)
- [Canonical anchors](#canonical-anchors)
- [CLAUDE.md](#claudemd)
- [README.md](#readmemd)
- [STRUCTURE.md](#structuremd)
- [RULES.md](#rulesmd)
- [WORKFLOWS.md](#workflowsmd)
- [AGENTS.md](#agentsmd)
- [MEMORY.md](#memorymd)
- [TIPS.md](#tipsmd)
- [DOCS.md](#docsmd)
- [Quality bar](#quality-bar)

---

## Shared conventions

**Front-matter.** Every file opens with:

```yaml
---
updated: 2026-07-31        # YYYY-MM-DD, the date the content was last verified
scope: <one line - what this file answers, and what it deliberately does not>
stability: stable | evolving | volatile
sources: [package.json, .github/workflows/ci.yml]   # what the content was derived from
owner_agent: structure-agent
---
```

`updated` is the trust signal. A reader in a month decides how much to believe a file based on that date, so it is only ever set to a date on which the content was actually verified against the code — never copied forward.

`stability` tells the reader how to treat a mismatch: `stable` means the code is probably wrong, `volatile` means the doc is probably wrong.

**Footer.** Every file ends with:

```markdown
---
[← CLAUDE.md](CLAUDE.md) · [STRUCTURE](STRUCTURE.md) · [WORKFLOWS](WORKFLOWS.md) · [MEMORY](MEMORY.md)
```

**Headings** are `##` for the canonical sections listed below, worded exactly as specified so anchors stay predictable. Extra `###` subsections are free.

**Uncertainty** is written down, never smoothed over: `> ❓ Unverified: <claim> - not confirmed against code, ask the maintainer.` Documentation that quietly guesses is worse than documentation with holes, because a hole is visible.

**Length budgets** are enforced by `validate_docs.py`. When a file grows past its budget the content does not get deleted — it moves to a deeper doc and DOCS.md indexes it.

---

## Canonical anchors

Link to these with confidence from any file:

| File | Anchors |
|---|---|
| STRUCTURE.md | `#directory-map`, `#entry-points`, `#data-flow`, `#where-to-add-things` |
| RULES.md | `#hard-rules`, `#code-rules`, `#git-rules`, `#security-and-secrets`, `#never-do` |
| WORKFLOWS.md | `#setup`, `#daily-development`, `#testing`, `#release`, `#debugging`, `#adding-a-feature` |
| AGENTS.md | `#agent-roster`, `#delegation-protocol`, `#tool-permissions`, `#handoff-format` |
| MEMORY.md | `#current-state`, `#decision-log`, `#open-threads`, `#failed-experiments`, `#glossary` |
| TIPS.md | `#gotchas`, `#performance`, `#debugging-tricks`, `#environment-quirks` |
| DOCS.md | `#internal-docs`, `#external-references`, `#api-references`, `#archive` |
| README.md | `#what-this-is`, `#quick-start`, `#project-layout`, `#documentation-map` |

---

## CLAUDE.md

**Budget: 160 lines. Target ~120.** This file is loaded into context on every single session, so every line costs tokens forever. It routes; it does not explain.

```markdown
<!-- MD-ORCHESTRATOR:v1 -->
---
updated: YYYY-MM-DD
scope: Router and session contract. Detail lives in the linked files.
stability: stable
sources: [scan]
owner_agent: claude-router-agent
---

# <Project> - Claude entry point

<Two sentences: what this project is, what state it is in.>

## Read this first
1. [RULES.md](RULES.md#hard-rules) - constraints that override anything else
2. [STRUCTURE.md](STRUCTURE.md#directory-map) - where things live
3. [MEMORY.md](MEMORY.md#current-state) - what happened recently and why

## Documentation map

| File | Answers | Read when |
|---|---|---|
| [RULES.md](RULES.md) | What must never happen | Always, before editing |
| [STRUCTURE.md](STRUCTURE.md) | Where code lives, how data flows | Locating or adding code |
| [WORKFLOWS.md](WORKFLOWS.md) | How to run, test, ship | Executing any procedure |
| [AGENTS.md](AGENTS.md) | Who does what, delegation rules | Splitting work across agents |
| [MEMORY.md](MEMORY.md) | Decisions, state, open threads | Returning after a break |
| [TIPS.md](TIPS.md) | Gotchas and hard-won specifics | Something behaves oddly |
| [DOCS.md](DOCS.md) | Index of deeper and external docs | Needing detail not here |
| [README.md](README.md) | Human-facing overview | Onboarding a person |

## Session contract
- Start: read RULES.md and MEMORY.md#current-state before proposing changes.
- During: prefer the commands in [WORKFLOWS.md](WORKFLOWS.md#daily-development); do not invent commands.
- End: append anything decided or learned to [MEMORY.md](MEMORY.md#decision-log). This is what makes the next session cheap.

## Fast facts
- Stack: <...>
- Run: `<command>` · Test: `<command>` · Build: `<command>`
- Entry point: `<path>`
- Never touch: `<generated dirs, lockfiles, secrets>`

## Maintaining these docs
This documentation set is generated and maintained by the `md-orchestrator` skill.
Run `/md-sync` after significant changes and `/md-audit` when returning after a break.
Never hand-edit more than one file without re-running the sync - the files are cross-linked by contract.
```

---

## README.md

**Budget: 200 lines.** Written for a human who has never seen the repository; the only file in the set that assumes no Claude context. If a README already exists with real content, preserve its voice and facts — rewrite structure, not identity. Never overwrite badges, licence blocks or contribution sections without asking.

Sections: `## What this is`, `## Quick start`, `## Project layout`, `## Documentation map`, plus whatever the original had (licence, badges, contributing, screenshots).

The documentation map section points humans at CLAUDE.md and the eight files, one line each.

---

## STRUCTURE.md

**Budget: 250 lines.** Answers "where is X and where does my new code go".

- `## Directory map` — annotated tree, one purpose line per directory. Only directories that exist. Mark generated dirs explicitly.
- `## Entry points` — table: entry | file | what triggers it.
- `## Data flow` — the two or three main paths through the system, described as request → layer → layer → store. Prose or ASCII, not a diagram library.
- `## Where to add things` — table: "a new API route" → path + the file to copy as a pattern. This section is what makes the file useful a month later, so it is worth more effort than the tree.
- `## Boundaries` — what must not import what, which modules are public API.

---

## RULES.md

**Budget: 200 lines.** Constraints only. Every rule is testable and has a consequence; a rule nobody can violate is noise.

- `## Hard rules` — numbered `R1..Rn`, each: rule, why, how it is enforced (lint, CI, review).
- `## Code rules` — style and pattern requirements that are actually enforced in this repo.
- `## Git rules` — branch naming, commit format, what never gets committed.
- `## Security and secrets` — where secrets live, what must never be logged or hardcoded.
- `## Never do` — the short blunt list: destructive commands, files never to edit, patterns previously banned (link the MEMORY.md entry that banned them).

Rules derived from actual configs are marked `[enforced]`; rules derived from convention are marked `[convention]`. The difference matters when someone decides whether to break one.

---

## WORKFLOWS.md

**Budget: 300 lines.** Procedures, each runnable start to finish without leaving the file.

Sections: `## Setup`, `## Daily development`, `## Testing`, `## Release`, `## Debugging`, `## Adding a feature`.

Each workflow uses this shape:

```markdown
### <Name>
**When:** <trigger> · **Takes:** <rough time> · **Needs:** <prereqs>

1. `command` - what it does
2. `command` - what it does

**Verify:** <how you know it worked>
**If it fails:** <the two most common failures and their fixes>
```

Every command must come from a real script, manifest or CI file. A plausible-looking invented command is the single most damaging thing this file can contain — mark anything unverified with the ❓ block instead.

---

## AGENTS.md

**Budget: 250 lines.** How work is split between agents in this project.

- `## Agent roster` — table: agent | responsibility | owns (paths) | must not touch. Include the doc agents from this skill so the roster is honest about who wrote the docs.
- `## Delegation protocol` — when the main agent splits work, how many run in parallel, what each receives, how results are merged. Restate the one-agent-per-file rule for documentation work.
- `## Tool permissions` — which tools each role may use; anything requiring human confirmation (destructive commands, deploys, force-push, secrets).
- `## Handoff format` — the exact structure a sub-agent returns: `files_changed`, `summary`, `open_questions`, `follow_ups`. Consistent handoffs are what make parallel work reviewable.
- `## Escalation` — what stops an agent: ambiguity, missing credentials, a rule conflict, more than N files touched.

---

## MEMORY.md

**Budget: 400 lines** (the largest, because this is the file that defeats a month of absence).

- `## Current state` — a dated snapshot, rewritten each sync: what works, what is in progress, what is broken, what is next. First thing anyone reads when returning.
- `## Decision log` — **append-only**, newest first:

```markdown
### YYYY-MM-DD - <decision in one line>
**Context:** <what forced a choice>
**Decision:** <what was chosen>
**Why:** <the reasoning that will otherwise evaporate>
**Alternatives rejected:** <option - reason>
**Consequences:** <what this constrains now>
**Revisit if:** <the condition that would reopen it>
```

- `## Open threads` — unfinished work with enough context to resume cold: what, where it stopped, next concrete step, blocker.
- `## Failed experiments` — what was tried and did not work, with the reason. This prevents the most expensive kind of repetition: cheerfully rebuilding something that already failed.
- `## Glossary` — project-specific vocabulary, including names that mean something non-obvious.

Rotation: when the decision log exceeds the budget, move entries older than a year to `docs/history/MEMORY-<year>.md` and index them in DOCS.md. Never delete a decision — a decision without its reasoning is how a codebase becomes haunted.

---

## TIPS.md

**Budget: 250 lines.** Only non-obvious, project-specific knowledge. Generic advice ("write tests") is banned; if a tip would be true of any repository, it does not belong here.

- `## Gotchas` — surprising behaviour and what to do about it.
- `## Performance` — known slow paths, measured numbers where available, what to avoid.
- `## Debugging tricks` — the flags, env vars and log locations that actually help here.
- `## Environment quirks` — OS/version/tooling specifics, versions that break things.

Each tip: symptom → cause → fix, one to four lines. Where a tip came from a real incident, link the MEMORY.md decision.

---

## DOCS.md

**Budget: 200 lines.** The index that keeps everything else short.

- `## Internal docs` — table: doc | path | covers | updated. Every markdown file in the repo outside the core nine appears here, or it is an orphan and the validator flags it.
- `## External references` — the specific pages actually needed (exact API pages, RFCs, vendor docs), each with why it matters. Not a link dump.
- `## API references` — endpoints/schemas/contracts and where their source of truth lives (OpenAPI file, proto, generated docs).
- `## Archive` — pointer to `docs/_archive/` with the date and reason for each archived set.

---

## Quality bar

Before an agent returns, it checks its own file against this list:

1. Would this sentence be true of any other project? → delete it.
2. Is every command copy-pasteable and taken from a real file? → or mark ❓.
3. Does a reader in a month know *why*, not just *what*?
4. Does every link use a canonical anchor from this spec?
5. Is the file within budget, with front-matter and footer?
6. Is every uncertainty visible rather than smoothed over?

A file that fails any of these gets rewritten by its own agent — not patched by the coordinator, because ownership is what keeps the set coherent.
