#!/usr/bin/env python3
"""validate_docs.py - integrity check for the CLAUDE.md orchestrator doc set.

Checks performed:
  1. all nine files exist
  2. YAML front-matter present with the required keys
  3. length budgets respected
  4. every internal markdown link resolves (file + anchor)
  5. CLAUDE.md links to all eight children; every child links back
  6. no leftover placeholders (TODO-GENERATED, TBD-AGENT, lorem)
  7. no orphan markdown outside docs/_archive that nothing references
  8. staleness: `updated:` older than --max-age-days, or older than the file's
     last git commit (i.e. edited without bumping the date)

Usage:
    python3 validate_docs.py [--root .] [--strict] [--report] [--max-age-days 45] [--json]

Exit codes: 0 = clean (or non-strict), 1 = errors found with --strict, 2 = bad usage.
Stdlib only, no dependencies.
"""

from __future__ import annotations

import argparse
import json
import os
import re
import subprocess
import sys
from datetime import date, datetime
from pathlib import Path

REQUIRED = {
    "CLAUDE.md": 160,
    "README.md": 200,
    "STRUCTURE.md": 250,
    "RULES.md": 200,
    "WORKFLOWS.md": 300,
    "AGENTS.md": 250,
    "MEMORY.md": 400,
    "TIPS.md": 250,
    "DOCS.md": 200,
}
CHILDREN = [f for f in REQUIRED if f != "CLAUDE.md"]
FRONT_MATTER_KEYS = {"updated", "scope", "stability"}
PLACEHOLDERS = ("TODO-GENERATED", "TBD-AGENT", "lorem ipsum", "<!-- fill in -->")

LINK_RE = re.compile(r"\[[^\]]*\]\(([^)\s]+)(?:\s+\"[^\"]*\")?\)")
HEADING_RE = re.compile(r"^(#{1,6})\s+(.*?)\s*$", re.M)
EXPLICIT_ANCHOR_RE = re.compile(r"<a\s+(?:id|name)=[\"']([^\"']+)[\"']", re.I)
CURLY_ANCHOR_RE = re.compile(r"\{#([A-Za-z0-9_-]+)\}\s*$")


def slugify(heading: str) -> str:
    h = CURLY_ANCHOR_RE.sub("", heading).strip()
    h = re.sub(r"[`*_~]", "", h)
    h = h.lower()
    h = re.sub(r"[^\w\s-]", "", h, flags=re.UNICODE)
    return re.sub(r"[\s]+", "-", h).strip("-")


def read(path: Path) -> str:
    try:
        return path.read_text(encoding="utf-8", errors="replace")
    except OSError:
        return ""


def parse_front_matter(text: str) -> dict:
    # the <!-- MD-ORCHESTRATOR:v1 --> marker is allowed above the block
    text = re.sub(r"\A(?:\s*<!--.*?-->\s*)+", "", text, flags=re.S)
    if not text.startswith("---"):
        return {}
    end = text.find("\n---", 3)
    if end == -1:
        return {}
    data = {}
    for line in text[3:end].splitlines():
        line = line.strip()
        if not line or line.startswith("#") or ":" not in line:
            continue
        k, v = line.split(":", 1)
        data[k.strip()] = v.strip().strip("\"'")
    return data


def anchors_of(text: str) -> set[str]:
    found = set()
    for _, heading in HEADING_RE.findall(text):
        m = CURLY_ANCHOR_RE.search(heading)
        if m:
            found.add(m.group(1).lower())
        found.add(slugify(heading))
    found.update(a.lower() for a in EXPLICIT_ANCHOR_RE.findall(text))
    found.discard("")
    return found


def git_last_commit_date(root: Path, rel: str):
    try:
        out = subprocess.run(
            ["git", "log", "-1", "--format=%ad", "--date=short", "--", rel],
            cwd=root, capture_output=True, text=True, timeout=10,
        ).stdout.strip()
        return datetime.strptime(out, "%Y-%m-%d").date() if out else None
    except Exception:
        return None


def collect_markdown(root: Path) -> list[Path]:
    skip_dirs = {".git", "node_modules", "dist", "build", ".venv", "venv",
                 "target", "vendor", ".next", "__pycache__"}
    out = []
    for dirpath, dirnames, filenames in os.walk(root):
        dirnames[:] = [d for d in dirnames if d not in skip_dirs]
        for fn in filenames:
            if fn.endswith(".md"):
                out.append(Path(dirpath) / fn)
    return sorted(out)


def main() -> int:
    ap = argparse.ArgumentParser(description=__doc__,
                                 formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--root", default=".")
    ap.add_argument("--strict", action="store_true", help="exit 1 when errors are found")
    ap.add_argument("--report", action="store_true", help="print the full report even when clean")
    ap.add_argument("--max-age-days", type=int, default=45)
    ap.add_argument("--json", action="store_true", help="machine-readable output")
    args = ap.parse_args()

    root = Path(args.root).resolve()
    errors: list[str] = []
    warnings: list[str] = []
    info: list[str] = []

    # 1. presence + 2. front matter + 3. budgets
    texts: dict[str, str] = {}
    for name, budget in REQUIRED.items():
        p = root / name
        if not p.is_file():
            errors.append(f"missing file: {name}")
            continue
        text = read(p)
        texts[name] = text
        lines = text.count("\n") + 1
        if lines > budget:
            warnings.append(f"{name}: {lines} lines exceeds budget of {budget} "
                            f"- move detail into DOCS.md or split it")
        fm = parse_front_matter(text)
        missing = FRONT_MATTER_KEYS - set(fm)
        if missing:
            errors.append(f"{name}: front-matter missing {sorted(missing)}")
        # 8. staleness
        if "updated" in fm:
            try:
                upd = datetime.strptime(fm["updated"][:10], "%Y-%m-%d").date()
            except ValueError:
                errors.append(f"{name}: `updated` is not YYYY-MM-DD ({fm['updated']!r})")
            else:
                age = (date.today() - upd).days
                if age > args.max_age_days:
                    warnings.append(f"{name}: last updated {age} days ago - run /md-sync")
                commit = git_last_commit_date(root, name)
                if commit and commit > upd:
                    errors.append(f"{name}: edited on {commit} but `updated:` still says {upd} "
                                  f"- the date is the trust signal, bump it")
        # 6. placeholders
        low = text.lower()
        for ph in PLACEHOLDERS:
            if ph.lower() in low:
                errors.append(f"{name}: leftover placeholder {ph!r}")

    # 5. router contract
    if "CLAUDE.md" in texts:
        claude_links = {os.path.basename(l.split("#")[0])
                        for l in LINK_RE.findall(texts["CLAUDE.md"])}
        for child in CHILDREN:
            if child not in claude_links:
                errors.append(f"CLAUDE.md does not link to {child} - the router must be complete")
    for child in CHILDREN:
        if child in texts and "CLAUDE.md" not in texts[child]:
            warnings.append(f"{child}: no link back to CLAUDE.md in the footer")

    # 4. link + anchor resolution across every markdown file in the repo
    all_md = collect_markdown(root)
    anchor_cache: dict[Path, set[str]] = {}
    referenced: set[Path] = set()

    for p in all_md:
        rel = p.relative_to(root)
        if "_archive" in rel.parts:
            continue
        text = texts.get(p.name) if p.parent == root else None
        if text is None:
            text = read(p)
        for link in LINK_RE.findall(text):
            if link.startswith(("http://", "https://", "mailto:", "tel:")):
                continue
            target, _, frag = link.partition("#")
            if not target:  # same-file anchor
                tgt_path = p
            else:
                tgt_path = (p.parent / target).resolve()
            if not tgt_path.exists():
                errors.append(f"{rel}: broken link -> {link}")
                continue
            if tgt_path.suffix == ".md":
                referenced.add(tgt_path)
            if frag and tgt_path.suffix == ".md":
                if tgt_path not in anchor_cache:
                    anchor_cache[tgt_path] = anchors_of(read(tgt_path))
                if frag.lower() not in anchor_cache[tgt_path]:
                    errors.append(f"{rel}: anchor not found -> {link}")

    # 7. orphans
    for p in all_md:
        rel = p.relative_to(root)
        if "_archive" in rel.parts or p.name in REQUIRED:
            continue
        if p.resolve() not in referenced:
            warnings.append(f"orphan markdown: {rel} - link it from DOCS.md or archive it")

    info.append(f"scanned {len(all_md)} markdown files under {root}")

    if args.json:
        print(json.dumps({"errors": errors, "warnings": warnings, "info": info}, indent=2))
    elif errors or warnings or args.report:
        print(f"MD ORCHESTRATOR VALIDATION - {root}")
        for e in errors:
            print(f"  ERROR   {e}")
        for w in warnings:
            print(f"  WARN    {w}")
        for i in info:
            print(f"  INFO    {i}")
        print(f"\n  {len(errors)} error(s), {len(warnings)} warning(s)")
        if not errors and not warnings:
            print("  documentation set is consistent")
    else:
        print("OK - documentation set is consistent")

    return 1 if (errors and args.strict) else 0


if __name__ == "__main__":
    try:
        sys.exit(main())
    except KeyboardInterrupt:
        sys.exit(130)
