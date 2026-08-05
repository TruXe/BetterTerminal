---
description: Read-only drift report for the CLAUDE.md orchestrator docs
---
Use the md-orchestrator skill in AUDIT mode.
Read .claude/skills/md-orchestrator/references/maintenance.md, run
scripts/validate_docs.py --report, compare the repo against each file's
`updated:` date, and report drift. Change nothing.
$ARGUMENTS
