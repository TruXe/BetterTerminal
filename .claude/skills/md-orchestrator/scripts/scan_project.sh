#!/usr/bin/env bash
# scan_project.sh - read-only inventory used by the MD Orchestrator coordinator.
# Writes nothing except to stdout. Safe to run in any repository.
#
# Usage: bash scan_project.sh [root]

set -uo pipefail
ROOT="${1:-$(git rev-parse --show-toplevel 2>/dev/null || pwd)}"
cd "$ROOT" || exit 1

PRUNE=( -name node_modules -o -name .git -o -name dist -o -name build -o -name .venv -o -name venv -o -name target -o -name vendor -o -name .next -o -name __pycache__ )

section() { printf '\n=== %s ===\n' "$1"; }

printf 'MD ORCHESTRATOR SCAN\nroot: %s\ndate: %s\n' "$ROOT" "$(date '+%Y-%m-%d %H:%M')"

section "ORCHESTRATOR STATE"
if [[ -f CLAUDE.md ]]; then
  if grep -q 'MD-ORCHESTRATOR:v1' CLAUDE.md 2>/dev/null; then
    echo "mode-hint: SYNC (orchestrator marker present)"
  else
    echo "mode-hint: MIGRATE (CLAUDE.md exists, no orchestrator marker)"
  fi
else
  echo "mode-hint: INIT (no CLAUDE.md at root)"
fi
for f in CLAUDE.md AGENTS.md WORKFLOWS.md STRUCTURE.md MEMORY.md RULES.md TIPS.md DOCS.md README.md; do
  [[ -f "$f" ]] && printf '  present: %-14s %s lines\n' "$f" "$(wc -l < "$f" | tr -d ' ')"
done

section "MARKDOWN INVENTORY (path | lines | last commit)"
find . \( "${PRUNE[@]}" \) -prune -o -type f -name '*.md' -print 2>/dev/null | sort | while read -r f; do
  lines=$(wc -l < "$f" 2>/dev/null | tr -d ' ')
  last=$(git log -1 --format=%ad --date=short -- "$f" 2>/dev/null)
  printf '  %-60s %6s  %s\n' "${f#./}" "$lines" "${last:-untracked}"
done

section "AGENT / ASSISTANT CONFIG FILES"
for f in .cursorrules .cursor/rules .windsurfrules .github/copilot-instructions.md \
         .claude/settings.json .claude/settings.local.json AGENT.md .aider.conf.yml GEMINI.md; do
  [[ -e "$f" ]] && echo "  found: $f"
done
[[ -d .claude/commands ]] && echo "  found: .claude/commands ($(ls -1 .claude/commands 2>/dev/null | wc -l | tr -d ' ') commands)"
[[ -d .claude/skills ]]   && echo "  found: .claude/skills ($(ls -1 .claude/skills 2>/dev/null | tr '\n' ' '))"

section "REPO SHAPE (top level)"
ls -1p 2>/dev/null | grep -v '^\.' | head -40 | sed 's/^/  /'

section "BUILD / RUN SIGNALS"
for f in package.json pyproject.toml requirements.txt Cargo.toml go.mod Gemfile pom.xml build.gradle \
         Makefile Justfile Taskfile.yml docker-compose.yml Dockerfile .tool-versions; do
  [[ -f "$f" ]] && echo "  found: $f"
done
[[ -d .github/workflows ]] && echo "  found: .github/workflows ($(ls -1 .github/workflows 2>/dev/null | tr '\n' ' '))"

section "TEST / QUALITY SIGNALS"
for f in .eslintrc .eslintrc.json .eslintrc.js .prettierrc ruff.toml setup.cfg tox.ini \
         .pre-commit-config.yaml jest.config.js vitest.config.ts pytest.ini; do
  [[ -e "$f" ]] && echo "  found: $f"
done
for d in tests test spec __tests__; do [[ -d "$d" ]] && echo "  found: $d/"; done

section "RECENT HISTORY (last 25 commits)"
git log --oneline -25 2>/dev/null | sed 's/^/  /' || echo "  (not a git repository)"

section "INLINE KNOWLEDGE MARKERS (TODO/HACK/FIXME/NOTE, top 25)"
grep -rInE '\b(TODO|HACK|FIXME|XXX|NOTE):' . \
  --exclude-dir={node_modules,.git,dist,build,.venv,venv,target,vendor,.next,__pycache__} 2>/dev/null \
  | head -25 | cut -c1-160 | sed 's/^/  /'

printf '\n=== END OF SCAN ===\n'
