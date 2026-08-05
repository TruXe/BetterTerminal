# Sub-agent briefs

Rule R1 in one sentence: **each markdown file is written by exactly one agent, and the coordinator writes none of them.**

The reason is not ceremony. One agent per file means one owner per fact, no merge conflicts between overlapping authors, parallel wall-clock time, and — most importantly — a clean re-dispatch path: when a file is weak, exactly one agent gets sent back with better evidence, and nothing else in the set moves.

## The dispatch brief

Every agent receives this, filled in. Nothing is left implicit; an agent that has to guess its boundaries will drift into another agent's file.

```
You are <file>-agent in the MD Orchestrator system.
You write exactly ONE file: <FILENAME>. You do not create, edit, read-to-modify,
or comment on any other file. If you believe another file is wrong, say so in
open_questions - do not fix it.

CONTEXT PACKET (everything known about this repository):
<contents of /tmp/md-context-packet.md>

YOUR SPEC (obey exactly - section headings, front-matter, footer, budget):
<the file's section from references/file-specs.md, plus the canonical anchor table>

YOUR MANDATE:
<the per-file mandate below>

EVIDENCE RULES:
- Every factual claim traces to something in the context packet or to a file you
  read yourself. You may read the repository; you may not read sibling docs being
  written in this run - they do not exist yet and their anchors are contractual.
- Anything you cannot verify goes in open_questions AND, if it matters to a reader,
  into the file as: > ❓ Unverified: <claim> - not confirmed against code.
- Never invent a command, path, port, env var or version. A wrong command costs
  more than a missing one.
- Nothing generic. If a sentence would be true of any repository, cut it.

WRITE TO: <absolute path>

RETURN (exactly this structure, nothing else):
  file: <path>
  lines: <n>
  sections: [<the ## headings you emitted>]
  anchors_emitted: [<slugs, so the coordinator can verify the link contract>]
  key_facts: [<3-8 things a future reader most needs, one line each>]
  open_questions: [<what you could not verify and why it matters>]
  cross_file_notes: [<facts you found that belong in another agent's file>]
```

`cross_file_notes` is the pressure valve that makes single ownership work: an agent that stumbles on a deployment gotcha while mapping directories reports it instead of squatting on TIPS.md, and the coordinator forwards it to the right agent in the second wave.

## Wave order

| Wave | Agents | Why here |
|---|---|---|
| 1 | structure, rules, workflows, agents, tips, docs | Independent; derived directly from the repo |
| 2 | memory, readme | Consume wave-1 `key_facts` and `cross_file_notes` |
| 3 | claude-router | Links only to what actually exists, with verified anchors |

Waves 1 and 2 run their agents in parallel. Wave 3 is a single agent.

## Per-file mandates

**structure-agent** — Map the repository as it is, not as it was intended. Walk the tree yourself; do not trust the scan alone. The `Where to add things` table is the deliverable that matters most: for each common task, the destination path and an existing file to copy as a pattern. Mark generated directories so nobody edits them by hand.

**rules-agent** — Extract constraints that are real. Read linter, formatter, CI, pre-commit and hook configs, plus `.gitignore` and any security config. Mark each rule `[enforced]` or `[convention]`. Do not import generic best practices; a rule that nothing in this repo cares about trains readers to ignore the file.

**workflows-agent** — Turn the repo's actual scripts into runnable procedures. Every command comes from `package.json`, `Makefile`, `pyproject.toml`, CI workflow, or an existing doc — verified. Include the verify step and the two most common failure modes for each workflow; those are what make a procedure survive a version bump.

**agents-agent** — Define how work is delegated in this project, including the documentation agents themselves. Cover roster, boundaries, tool permissions, the handoff structure and escalation triggers. If the project has existing agent config (`.claude/`, `.cursorrules`, MCP servers), reflect what is actually configured rather than an idealised roster.

**tips-agent** — Harvest the non-obvious: TODO/HACK/FIXME comments with context, workaround-shaped code, unusual version pins, anything in the git history that looks like a painful fix. Symptom → cause → fix. If you find fewer than five real tips, return five and say so — padding this file with generic advice is worse than a short file.

**docs-agent** — Index every markdown file outside the core nine, plus external references that are genuinely needed here (the specific API page, not the vendor's homepage). For each internal doc: what it covers and whether it still looks current. Flag anything that duplicates the core nine as a merge candidate in `open_questions`.

**memory-agent** — The highest-stakes file. Reconstruct decisions from git history, ADRs, old docs, code comments and the context packet, then write `Current state` as a dated snapshot a returning reader can act on immediately. For each decision capture the *why* and the *rejected alternatives*; those are the parts that evaporate. Fold wave-1 `cross_file_notes` about history in here. Where a decision's reasoning is unrecoverable, record it as `Reasoning lost - reconstructed from <evidence>` rather than inventing a rationale.

**readme-agent** — Human-first. If a README exists, preserve its identity, badges, licence and contribution content; restructure around them. Assume no Claude context and no prior knowledge of the project.

**claude-router-agent** — Dispatched last, with the full return payload of all eight agents. Write the router only: two-sentence orientation, the documentation map table, the session contract, fast facts, the maintenance clause with the `<!-- MD-ORCHESTRATOR:v1 -->` marker. Every link must use an anchor that appeared in some agent's `anchors_emitted`. Resist explanation — anything explained here is duplicated somewhere and will rot out of sync. Under ~120 lines.

## Coordinator duties between waves

1. Log each return: `bash scripts/md_agent_log.sh agent "<FILE>" "done - <n> lines, <k> open questions"`.
2. Route every `cross_file_notes` item to its owner; if the owner already returned, re-dispatch that single agent with the addition.
3. Reject and re-dispatch any file that is generic, under-evidenced, off-spec, or over budget. Re-dispatch with the *specific* missing evidence, not with "make it better".
4. Collect all `open_questions` into one list for the user — this list is often the most valuable output of the entire run, because it names what the project itself does not know.
