# Self-installation

The goal: after one run, this skill is reachable in the project forever — by a teammate who never heard of it, by a fresh Claude session, and by future-you a month from now who remembers only that "the docs update themselves somehow".

Everything here is written into the repository in plain sight, and the user is shown the plan before anything is written. A skill that quietly installs hooks into someone's project is a skill nobody should trust — announce, then install.

```bash
bash scripts/md_agent_log.sh final "Self-installation"
```

## 1. Vendor the skill into the project

```bash
mkdir -p .claude/skills/md-orchestrator
cp -r <skill-source>/{SKILL.md,references,scripts} .claude/skills/md-orchestrator/
chmod +x .claude/skills/md-orchestrator/scripts/*.sh
```

A project-local copy means the skill travels with the repository: anyone who clones it gets the same documentation system, including CI, and the docs stay reproducible even if the user's personal skill folder changes. Add a line to `.gitignore` only if the user asks — by default this should be committed.

## 2. Install the commands

`.claude/commands/md-sync.md`:

```markdown
---
description: Re-sync the CLAUDE.md orchestrator docs with the current state of the code
---
Use the md-orchestrator skill in SYNC mode.
Read .claude/skills/md-orchestrator/references/maintenance.md first.
Detect drift, re-dispatch only the affected file agents (one agent per file),
always include memory-agent, then validate with scripts/validate_docs.py --strict.
$ARGUMENTS
```

`.claude/commands/md-audit.md`:

```markdown
---
description: Read-only drift report for the CLAUDE.md orchestrator docs
---
Use the md-orchestrator skill in AUDIT mode.
Read .claude/skills/md-orchestrator/references/maintenance.md, run
scripts/validate_docs.py --report, compare the repo against each file's
`updated:` date, and report drift. Change nothing.
$ARGUMENTS
```

`.claude/commands/md-recall.md`:

```markdown
---
description: Reconstruct project context after a break (the month-later protocol)
---
Run the re-entry protocol from
.claude/skills/md-orchestrator/references/maintenance.md:
MEMORY.md#current-state → #open-threads → git log since that date →
validate_docs.py --report → decision log entries since the last session.
Then state the reconstructed situation in five lines, naming anything uncertain.
```

## 3. Stamp the marker and the maintenance clause

Into `CLAUDE.md`, first line:

```markdown
<!-- MD-ORCHESTRATOR:v1 -->
```

This is how a future session detects SYNC mode instead of re-migrating a set that is already correct. `scan_project.sh` looks for exactly this string.

Into `CLAUDE.md`, the maintenance section from the template — the three commands and the warning that the files are cross-linked by contract, so hand-editing several at once breaks the link graph.

Into `RULES.md#hard-rules`, one rule that makes maintenance a project constraint rather than a good intention:

```markdown
### R<n> — Documentation is updated in the same change as the code [convention]
Any change that alters structure, commands, dependencies or a decision updates the
owning file (see [CLAUDE.md](CLAUDE.md)) and appends to [MEMORY.md](MEMORY.md#decision-log).
Enforced by: `/md-audit` before release; `validate_docs.py --strict` in CI.
Why: a wrong document costs more than a missing one, because it is acted on.
```

Into `AGENTS.md#agent-roster`, the nine documentation agents, so the roster tells the truth about who wrote the docs.

## 4. Optional, offer explicitly

Never install these silently — describe what each does and let the user choose.

**Validation in CI** (`.github/workflows/docs.yml`):

```yaml
name: docs
on: [pull_request]
jobs:
  validate:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
        with: { fetch-depth: 0 }   # git dates are used for staleness checks
      - run: python3 .claude/skills/md-orchestrator/scripts/validate_docs.py --root . --strict --report
```

**Session-start reminder** (`.claude/settings.json`) — prints the current state so a session opens with context instead of assumptions:

```json
{
  "hooks": {
    "SessionStart": [
      {
        "hooks": [
          {
            "type": "command",
            "command": "sed -n '/## Current state/,/^## /p' MEMORY.md | head -25"
          }
        ]
      }
    ]
  }
}
```

If `.claude/settings.json` already exists, merge rather than overwrite, and show the diff. Clobbering someone's existing hook configuration is the fastest way to make them delete the skill.

**Pre-commit staleness nudge** — warns (never blocks) when code changed but no doc did.

## 5. Verify the installation

```bash
test -f .claude/skills/md-orchestrator/SKILL.md && echo "skill: vendored"
ls .claude/commands/md-*.md
grep -q 'MD-ORCHESTRATOR:v1' CLAUDE.md && echo "marker: present"
python3 .claude/skills/md-orchestrator/scripts/validate_docs.py --root . --strict --report
bash scripts/md_agent_log.sh done "Installed - /md-sync, /md-audit, /md-recall available"
```

Then tell the user, in one short paragraph: what was written where, which three commands now exist, and the single habit that keeps the whole thing alive — appending to `MEMORY.md#decision-log` at the end of a working session.
