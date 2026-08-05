#!/usr/bin/env bash
# md_agent_log.sh - the [ MD AGENT ] status protocol.
#
# Usage:
#   md_agent_log.sh start
#   md_agent_log.sh working    "Scanning 42 markdown files"
#   md_agent_log.sh thinking   "Resolving conflicts"
#   md_agent_log.sh generating "STRUCTURE.md"
#   md_agent_log.sh ultrathink "Reconciling 6 contradictory claims"
#   md_agent_log.sh agent      "RULES.md" "done - 74 lines"
#   md_agent_log.sh warn       "AGENTS.md returned 3 open questions"
#   md_agent_log.sh final
#   md_agent_log.sh done
#
# Every call is also appended to .claude/md-agent.log (without colour codes),
# so a session that gets interrupted can be resumed by reading the trail.
#
# Colour is disabled automatically when stdout is not a terminal, when NO_COLOR
# is set, or when MD_AGENT_NO_COLOR=1.

set -uo pipefail

if [[ -t 1 && -z "${NO_COLOR:-}" && "${MD_AGENT_NO_COLOR:-0}" != "1" ]]; then
  RED=$'\033[1;31m'; GREEN=$'\033[1;32m'; YELLOW=$'\033[1;33m'
  BLUE=$'\033[1;36m'; MAGENTA=$'\033[1;35m'; DIM=$'\033[2m'; RESET=$'\033[0m'
else
  RED=""; GREEN=""; YELLOW=""; BLUE=""; MAGENTA=""; DIM=""; RESET=""
fi

TAG="[ MD AGENT ]"
STATE="${1:-}"
shift || true
MSG="$*"

STAMP="$(date '+%H:%M:%S')"

emit() {
  # $1 = colour, $2 = rendered text
  printf '%s%s%s %s\n' "$1" "$TAG" "$RESET" "$2"
  local root logdir
  root="$(git rev-parse --show-toplevel 2>/dev/null || pwd)"
  logdir="$root/.claude"
  if mkdir -p "$logdir" 2>/dev/null; then
    printf '%s %s %s\n' "$STAMP" "$TAG" "$2" >> "$logdir/md-agent.log" 2>/dev/null || true
  fi
}

case "$STATE" in
  start)
    emit "$RED" "Started.."
    [[ -n "$MSG" ]] && emit "$DIM" "$MSG"
    exit 0
    ;;
  working|work)      emit "$BLUE"    "Working.. ${MSG}" ;;
  thinking|think)    emit "$BLUE"    "Thinking.. ${MSG}" ;;
  generating|gen)    emit "$BLUE"    "Generating.. ${MSG}" ;;
  ultrathink|ultra)  emit "$MAGENTA" "ULTRATHINK.. ${MSG}" ;;
  agent)
    # $1 was consumed as state; first remaining word-group is the file name
    FILE="${1:-unknown}"
    shift || true
    emit "$BLUE" "Agent[${FILE}] ${*}"
    ;;
  warn)              emit "$YELLOW"  "WARN: ${MSG}" ;;
  final|final_steps) emit "$YELLOW"  "Final Steps.. ${MSG}" ;;
  done|finish)
    emit "$GREEN" "DONE!"
    [[ -n "$MSG" ]] && emit "$DIM" "$MSG"
    exit 0
    ;;
  ""|help|-h|--help)
    cat <<'USAGE'
[ MD AGENT ] status protocol
  start | working | thinking | generating | ultrathink | agent | warn | final | done
Examples:
  md_agent_log.sh start
  md_agent_log.sh generating "MEMORY.md"
  md_agent_log.sh agent "RULES.md" "done - 74 lines, 0 open questions"
  md_agent_log.sh done
USAGE
    ;;
  *)
    emit "$BLUE" "Working.. ${STATE} ${MSG}"
    ;;
esac
