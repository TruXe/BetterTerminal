# MIGRATE mode — consolidating an existing mess

An existing project rarely has "no docs". It has seven of them, three of which contradict each other, one of which was true in March. Migration is therefore an **information-preservation problem** first and a writing problem second.

The order matters: inventory → extract → verify → resolve → archive → generate → prove. Generating before extracting is how knowledge gets silently deleted.

## 1. Inventory

```bash
bash scripts/md_agent_log.sh working "Inventory of existing documentation"
bash scripts/scan_project.sh > /tmp/md-scan.txt
```

Classify every file the scan finds:

| Class | Meaning | Action |
|---|---|---|
| **Core** | CLAUDE.md and friends, project instructions | Extract claims, then archive |
| **Agent config** | `.cursorrules`, `.windsurfrules`, `copilot-instructions.md`, `GEMINI.md`, `AGENT.md` | Extract rules into RULES.md/AGENTS.md; **leave the original file in place** — other tools read it |
| **Real docs** | Architecture notes, ADRs, runbooks, API docs | Keep where they are, index in DOCS.md |
| **Stale** | Contradicted by code, or untouched for a year | Extract anything still true, archive |
| **Generated** | Tool output, changelogs, `node_modules` docs | Ignore entirely |

Nested `CLAUDE.md` files in subdirectories are a special case: Claude Code loads them contextually, so they are legitimate. Keep them, shrink them to their local concern, and have the root CLAUDE.md list them. Do not flatten them into the root — that inflates the always-loaded context for everyone.

## 2. Claim ledger

Write `/tmp/md-claims.md`. One row per factual assertion found anywhere in the old docs:

```markdown
| # | Claim | Source | Verified | Verdict | Destination |
|---|-------|--------|----------|---------|-------------|
| 1 | `npm run dev` starts on :3000 | CLAUDE.md:14 | package.json:12 → :3001 | CORRECTED | WORKFLOWS.md#setup |
| 2 | Never commit to main | .cursorrules:3 | branch protection on | TRUE | RULES.md#git-rules |
| 3 | Redis is used for sessions | docs/arch.md:40 | no redis dependency | DROPPED | — (removed 2024) |
```

Verdicts: `TRUE` · `CORRECTED` · `DROPPED` (no longer applies, note why) · `UNVERIFIABLE` (carry forward with the ❓ marker) · `DUPLICATE` (of row N).

The ledger is the contract with the user. Nothing leaves the old docs without a row, which is what makes the coverage report at the end meaningful instead of reassuring.

## 3. Verify against code, not against other docs

Docs agree with each other far more often than they agree with reality. For each claim, check the source of truth: commands against manifests and CI, paths against the filesystem, versions against lockfiles, endpoints against the router or spec, env vars against `.env.example` and the code that reads them.

Claims about *intent* ("we chose Postgres because…") cannot be verified this way and should not be dropped — they are exactly what MEMORY.md exists to preserve. Mark them `UNVERIFIABLE`, keep the reasoning, attribute the source.

## 4. Resolve conflicts

```bash
bash scripts/md_agent_log.sh ultrathink "Resolving <n> conflicting claims"
```

Precedence when two documents disagree:

1. The code and its configs
2. The more recently committed document
3. The more specific document (a subdirectory doc beats a root doc about that subdirectory)
4. The user

Never split the difference and never keep both versions. A doc set that hedges gives the next reader the same conflict, plus the false impression that someone looked into it. Where a conflict was resolved by a real decision, record it in MEMORY.md as a dated entry — that is precisely the knowledge that was missing the first time.

## 5. Archive before writing

```bash
bash scripts/md_agent_log.sh working "Archiving superseded documentation"
mkdir -p "docs/_archive/$(date +%Y-%m-%d)"
git mv <old-doc> "docs/_archive/$(date +%Y-%m-%d)/" 2>/dev/null || mv <old-doc> "docs/_archive/$(date +%Y-%m-%d)/"
```

Then write `docs/_archive/<date>/ARCHIVE-INDEX.md`:

```markdown
# Archived <date> — migration to MD Orchestrator v1

| Original | Lines | Claims extracted | Now lives in |
|---|---|---|---|
| CLAUDE.md | 340 | 22 (18 true, 3 corrected, 1 dropped) | RULES.md, WORKFLOWS.md, STRUCTURE.md |
| docs/old-arch.md | 120 | 9 (5 true, 4 dropped) | STRUCTURE.md, MEMORY.md |

Dropped claims and why:
- "Redis for sessions" — dependency removed in 4f2a1c (2024-11)
```

`git mv` keeps history attached to the file, which matters the first time someone asks "when did this stop being true". Delete the archive only after the user has seen the coverage report and asked for it.

## 6. Generate

Hand the coordinator's context packet **plus the verified ledger** to the agents (Section 3 of SKILL.md). Each agent's brief includes the ledger rows destined for its file, so nothing depends on an agent noticing a claim on its own.

## 7. Prove no loss

```bash
bash scripts/md_agent_log.sh final "Coverage report"
python3 scripts/validate_docs.py --root . --strict --report
```

Present:

```
Migration coverage
  Files consolidated:     7 → 9 (archived at docs/_archive/2026-07-31/)
  Claims extracted:       48
    carried forward:      39   (31 verified true, 8 corrected)
    dropped:               6   (each listed with reason)
    unverifiable:          3   (marked ❓ in the new docs)
  Lines: 1,240 → 980  (root always-loaded context: 340 → 118)
  Validation: 0 errors, 2 warnings
  Open questions from agents: 5   ← worth reading, these are real gaps
```

The always-loaded context number is usually the most persuasive line in the report: the point of the router pattern is that a session pays ~120 lines up front instead of 340, and reads deeper only when the task requires it.

## Common migration traps

- **Deleting `.cursorrules` and friends.** Other tools still read them. Extract, keep, and note in RULES.md that they are mirrors with a single source of truth.
- **Flattening nested CLAUDE.md files.** They exist to be loaded contextually; merging them into the root makes every session more expensive.
- **Carrying forward a stale command because it appeared in three docs.** Frequency is not evidence. Only the manifest is.
- **Rewriting a good README's voice.** Restructure, don't re-author.
- **Migrating in one pass without a ledger.** It feels faster and reliably loses the two or three facts that mattered most.
