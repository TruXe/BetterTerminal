---
description: Re-sync the CLAUDE.md orchestrator docs with the current state of the code
---
Use the md-orchestrator skill in SYNC mode.
Read .claude/skills/md-orchestrator/references/maintenance.md first.
Detect drift, re-dispatch only the affected file agents (one agent per file),
always include memory-agent, then validate with scripts/validate_docs.py --strict.
$ARGUMENTS
