#!/usr/bin/env python3
"""Guard rail: forbid new `init` accessors on public API.

Repository policy (`.github/copilot-instructions.md` and overarching principle
#2 in `.github/agents/expert-reviewer.agent.md`):

    Public API for MSTest and Microsoft.Testing.Platform MUST NOT use
    `init` accessors. Existing MTP `init` accessors are grandfathered;
    no new ones may be introduced.

This script scans the PR diff (or a supplied diff file) for *additions* to
any `PublicAPI.Unshipped.txt` file whose right-hand side is `.init -> void`,
which is how the public-API analyzer records a property setter with the
`init` accessor. Additions to `PublicAPI.Shipped.txt` are ignored — those
are the grandfathered set.

Usage:
    # Default — compare ${BASE_SHA:-origin/main}..HEAD
    python .github/scripts/check_public_api_init.py

    # Compare against an explicit base ref
    python .github/scripts/check_public_api_init.py --base origin/main

    # Read a unified diff from a file (CI also writes one to /tmp/pr.diff)
    python .github/scripts/check_public_api_init.py --diff-file /tmp/pr.diff

Exit codes:
    0 — no violations
    1 — at least one new `.init -> void` line was added
    2 — usage / IO error
"""

from __future__ import annotations

import argparse
import os
import re
import subprocess
import sys
from pathlib import Path
from typing import Iterable, List, NamedTuple


# Ensure UTF-8 output on Windows consoles (cp1252 by default) so the ❌/✅
# markers in the report don't crash the script on local invocations.
if hasattr(sys.stdout, "reconfigure"):
    try:
        sys.stdout.reconfigure(encoding="utf-8", errors="replace")
    except Exception:
        pass


INIT_LINE = re.compile(r"\.init\s*->\s*void\s*$")
UNSHIPPED_BASENAME = "PublicAPI.Unshipped.txt"


class Violation(NamedTuple):
    file: str
    line: str


def _run(cmd: List[str]) -> str:
    result = subprocess.run(cmd, capture_output=True, text=True, check=False)
    if result.returncode != 0:
        sys.stderr.write(
            f"git command failed (exit {result.returncode}): {' '.join(cmd)}\n"
            f"stderr: {result.stderr}\n"
        )
        sys.exit(2)
    return result.stdout


def _diff_from_git(base: str) -> str:
    # Use --no-color and --unified=0 so we only see added/removed lines, no
    # context, and no ANSI escapes that would confuse the parser.
    return _run(["git", "diff", "--no-color", "--unified=0", f"{base}...HEAD"])


def parse_diff(diff_text: str) -> List[Violation]:
    """Walk a unified diff and yield additions to PublicAPI.Unshipped.txt that
    introduce an `init` accessor."""
    violations: List[Violation] = []
    current_file: str | None = None
    in_unshipped = False

    for raw_line in diff_text.splitlines():
        if raw_line.startswith("diff --git "):
            current_file = None
            in_unshipped = False
            continue
        if raw_line.startswith("+++ "):
            # +++ b/path/to/file  — strip the b/ prefix
            path = raw_line[4:].strip()
            if path == "/dev/null":
                current_file = None
                in_unshipped = False
                continue
            if path.startswith("b/"):
                path = path[2:]
            current_file = path
            in_unshipped = Path(path).name == UNSHIPPED_BASENAME
            continue
        if not in_unshipped or current_file is None:
            continue
        # Added lines start with a single '+' but not '+++'.
        if raw_line.startswith("+") and not raw_line.startswith("+++"):
            added = raw_line[1:]
            # Public-API entries are one symbol per line. Comments (`#`) and
            # the *REMOVED* / *NULLABILITY* sentinel lines are not API.
            stripped = added.strip()
            if not stripped or stripped.startswith("#") or stripped.startswith("*"):
                continue
            if INIT_LINE.search(stripped):
                violations.append(Violation(file=current_file, line=stripped))
    return violations


def format_report(violations: Iterable[Violation]) -> str:
    lines = [
        "❌ Public-API policy violation: new `init` accessors detected.",
        "",
        "Repository policy (`.github/copilot-instructions.md`) forbids `init`",
        "accessors on **new** public API for MSTest and Microsoft.Testing.Platform.",
        "Existing entries in `PublicAPI.Shipped.txt` are grandfathered, but every",
        "new line added to `PublicAPI.Unshipped.txt` must use a regular setter.",
        "",
        "Offending additions:",
        "",
    ]
    for v in violations:
        lines.append(f"  - `{v.file}` → `{v.line}`")
    lines.extend(
        [
            "",
            "To fix: change the property to use a regular `set` accessor",
            "(or make the setter `internal` / drop the setter entirely) and",
            "regenerate the `PublicAPI.Unshipped.txt` entry.",
        ]
    )
    return "\n".join(lines)


def write_step_summary(text: str) -> None:
    summary_path = os.environ.get("GITHUB_STEP_SUMMARY")
    if not summary_path:
        return
    try:
        with open(summary_path, "a", encoding="utf-8") as fh:
            fh.write(text)
            fh.write("\n")
    except OSError as exc:
        sys.stderr.write(f"Could not write GITHUB_STEP_SUMMARY: {exc}\n")


def main(argv: List[str]) -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument(
        "--base",
        default=os.environ.get("BASE_SHA", "origin/main"),
        help="Base ref to diff against (default: $BASE_SHA or origin/main).",
    )
    parser.add_argument(
        "--diff-file",
        type=Path,
        help="Read the unified diff from this file instead of running `git diff`.",
    )
    args = parser.parse_args(argv)

    if args.diff_file is not None:
        try:
            diff_text = args.diff_file.read_text(encoding="utf-8", errors="replace")
        except OSError as exc:
            sys.stderr.write(f"Could not read diff file {args.diff_file}: {exc}\n")
            return 2
    else:
        diff_text = _diff_from_git(args.base)

    violations = parse_diff(diff_text)
    if not violations:
        print("✅ No new `init` accessors detected in PublicAPI.Unshipped.txt additions.")
        return 0

    report = format_report(violations)
    print(report)
    write_step_summary(report)
    return 1


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
