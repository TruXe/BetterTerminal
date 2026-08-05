# Archive index - 2026-08-04

Superseded by the md-orchestrator documentation run of 2026-08-04 (INIT mode). Nothing here is
authoritative; it is kept so no claim is lost.

| Archived file | Original path | Superseded by | Reason |
|---|---|---|---|
| `RULES.md` | `RULES.md` (repository root) | [RULES.md](../../../RULES.md), [MEMORY.md](../../../MEMORY.md#decision-log), [TIPS.md](../../../TIPS.md#gotchas) | Hand-written before the orchestrator existed. Its rules were re-derived from the code, split into enforced and convention, and given consequences. |

## Claim coverage

Every claim in the archived file was carried forward:

| Claim in the archived RULES.md | Where it now lives |
|---|---|
| Start a task with `md-orchestrator`, finish with `md-sync` | RULES.md `## Hard rules` (workflow rule) and CLAUDE.md `## Maintaining these docs` |
| `md-orchestrator` and `md-sync` are not installed | Partly obsolete: `md-orchestrator` is now vendored at `.claude/skills/md-orchestrator`. Only `md-sync` is still missing, tracked in MEMORY.md `## Open threads` |
| No external API or package name in user-visible UI text | RULES.md `## Hard rules` |
| .NET Framework 4.8, C# 7.3, WPF, x64, no new NuGet packages | RULES.md `## Hard rules` |
| Job object with KILL_ON_JOB_CLOSE for every launched process | RULES.md `## Hard rules` |
| Background IO threads must never let an exception escape | RULES.md `## Hard rules`, TIPS.md `## Gotchas` |
| Teardown order, and the pane-close crash that caused it | RULES.md `## Hard rules`, MEMORY.md `## Decision log` |
| Git and package installs only when the user names them | RULES.md `## Git rules` |

Nothing was dropped.
