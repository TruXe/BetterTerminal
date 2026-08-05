# SYNC and AUDIT — keeping the set true

Generating good documentation is the easy half. The hard half is that documentation decays silently: nothing breaks, no test fails, and the first symptom is a confidently wrong answer three weeks later.

Two operations keep the set honest.

- **SYNC** — the code moved; update the docs to match.
- **AUDIT** — read-only; report where the docs and the repo have drifted apart.

Both run under the same rules as generation: one agent per file, coordinator coordinates, banners on.

---

## AUDIT

```bash
bash scripts/md_agent_log.sh start
bash scripts/md_agent_log.sh working "Auditing documentation drift"
python3 scripts/validate_docs.py --root . --report
git log --oneline --since="$(grep -m1 '^updated:' CLAUDE.md | cut -d' ' -f2)" | head -50
git diff --stat "@{$(grep -m1 '^updated:' CLAUDE.md | cut -d' ' -f2)}" 2>/dev/null | tail -20
```

Drift signals worth reporting, roughly in order of how much damage they cause:

| Signal | Why it matters |
|---|---|
| A documented command no longer exists in any manifest | The most damaging error: a reader runs it and loses time |
| New top-level directory absent from STRUCTURE.md | Readers conclude the map is unreliable |
| Dependency added/removed since `updated:` | Stack facts in CLAUDE.md and README.md are wrong |
| CI workflow changed | WORKFLOWS.md release/test steps suspect |
| >30 commits since the last MEMORY.md entry | Decisions were made and their reasoning is being lost right now |
| A file's `updated:` older than its last commit | Someone edited it without re-verifying — validator errors on this |
| New orphan markdown | Knowledge accumulating outside the index |

Output a table of file → drift found → suggested action, then offer a SYNC. Do not change anything in AUDIT mode; the value of a read-only check is that it can be run without deciding anything.

---

## SYNC

Only the affected files are re-dispatched. A full regeneration throws away hand-refinements the team made and burns the trust that comes from stable documents.

1. `bash scripts/md_agent_log.sh start`
2. Rebuild the context packet from the current repo (Section 2 of SKILL.md) and add a **diff section**: commits, changed files, added/removed dependencies since each file's `updated:` date.
3. Decide the dispatch set from the drift table. Structural change → structure-agent. New scripts or CI → workflows-agent. New config/lint rules → rules-agent. Anything at all → memory-agent, always.
4. Dispatch those agents in parallel with the standard brief plus: *"This file exists. Preserve everything still true — including hand-written additions. Update what changed. Bump `updated:`. Report what you changed and why."*
5. `bash scripts/md_agent_log.sh final` → validate → `done`.
6. Report: files touched, what changed in each, what was preserved.

**memory-agent runs on every sync without exception.** Structure can be re-derived from the code at any time; the reason a decision was made cannot. Every sync that skips MEMORY.md loses knowledge permanently.

---

## The month-later re-entry protocol

This is what the whole system is built for. When someone returns to the project after a break, run this in order — it takes about a minute and replaces an hour of re-reading code:

```bash
bash scripts/md_agent_log.sh start
bash scripts/md_agent_log.sh working "Re-entry: reconstructing context"
```

1. `MEMORY.md#current-state` — the dated snapshot: what worked, what was in progress, what was broken.
2. `MEMORY.md#open-threads` — the concrete next step of whatever was interrupted mid-flight.
3. `git log --oneline --since=<the current-state date>` — what moved without you.
4. `python3 scripts/validate_docs.py --report` — how much of the above can still be trusted.
5. `MEMORY.md#decision-log` — the entries dated after your last session.

Then state the reconstructed situation back to the user in five lines: where the project stands, what was left unfinished, what changed since, what looks stale, what you propose doing first. Getting this wrong is expensive, so name explicitly anything you are unsure about instead of presenting a smooth summary.

### What MEMORY.md must contain for this to work

The protocol only pays off if earlier sessions wrote the right things down. Every session should append:

- Decisions **with rejected alternatives** — future-you will re-propose exactly those alternatives.
- Work stopped mid-flight, with the *next concrete step*, not a vague area.
- Failures with causes — the most expensive repetition is cheerfully rebuilding something that already failed.
- New vocabulary and any name whose meaning is not obvious.
- Anything that surprised you. Surprise is a reliable signal of a fact that is not derivable from the code.

Not: what a file does (STRUCTURE.md), how to run something (WORKFLOWS.md), or a narrative of the session. MEMORY.md is for what cannot be re-derived from the repository.

---

## Cadence

| When | Do |
|---|---|
| End of any substantive session | Append to `MEMORY.md#decision-log` — the habit the whole system rests on |
| After a feature merges | `/md-sync` on structure + workflows + memory |
| Monthly, or on return after a break | `/md-audit`, then sync whatever it flags |
| After a dependency or CI change | `/md-sync` on workflows + rules |
| Before onboarding someone | Full `/md-audit` — a new reader finds every stale line you have stopped seeing |

## Anti-patterns

- **Regenerating everything on every sync.** Destroys hand-refinements; teaches the team the docs are disposable.
- **Bumping `updated:` without re-verifying.** Turns the trust signal into decoration. The validator catches the reverse case (edited without bumping) but cannot catch this one — it depends on discipline.
- **Letting CLAUDE.md grow.** Every added line is paid in every future session. New material belongs in a linked file.
- **Skipping the archive on a sync rewrite.** Even a partial rewrite should be recoverable.
- **Documenting aspiration.** "We use trunk-based development" when three long-lived branches exist teaches readers to distrust the whole set.
