#!/usr/bin/env python3
"""Guard rail: forbid manual `.xlf` edits in human-authored PRs.

Repository policy (`.github/copilot-instructions.md` → Localization Guidelines):

    Add a corresponding entry in the resource file (.resx).
    NEVER manually modify *.xlf files. Instead, regenerate them by running
    `dotnet msbuild <project>.csproj /t:UpdateXlf` on the owning project.

The OneLocBuild bot owns the bulk update path. A human PR may legitimately
touch `.xlf` files only when those changes are the **output of**
`dotnet msbuild /t:UpdateXlf` after a `.resx` edit — in that case the same PR
will also contain a `.resx` change. A PR that hand-edits a `.xlf` without any
`.resx` change is the defect this guard catches.

Inputs:
    --diff-file <path>    Read a unified diff or `git diff --name-only` listing.
    --base <ref>          Git ref to diff against (default: $BASE_SHA or
                          origin/main).
    --author <login>      PR author login. Bot accounts (suffix `[bot]` or in
                          the known-bot list below) are exempt and the script
                          exits 0 without checking.

Exit codes:
    0 — no `.xlf` changes, OR PR author is a bot, OR every changed `.xlf` has
        a matching `.resx` change in the same PR.
    1 — a `.xlf` file was modified without a corresponding `.resx` change.
    2 — usage / IO error.
"""

from __future__ import annotations

import argparse
import os
import re
import subprocess
import sys
from pathlib import Path
from typing import Iterable, List, Set, Tuple


if hasattr(sys.stdout, "reconfigure"):
    try:
        sys.stdout.reconfigure(encoding="utf-8", errors="replace")
    except Exception:
        pass


# Accounts that are allowed to push pure-xlf updates. Loc-build and dependency
# bots fall here. Matches by login; we also exempt anything that ends in `[bot]`
# (the GitHub convention for GitHub App identities).
KNOWN_BOTS: Set[str] = {
    "dotnet-bot",
    "dotnet-maestro",
    "dotnet-maestro[bot]",
    "github-actions[bot]",
}


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
    return _run(["git", "diff", "--no-color", "--name-only", f"{base}...HEAD"])


def parse_changed_paths(diff_text: str) -> List[str]:
    paths: List[str] = []
    for raw_line in diff_text.splitlines():
        line = raw_line.strip()
        if not line:
            continue
        if line.startswith("diff --git "):
            parts = line.split(" b/", 1)
            if len(parts) == 2:
                paths.append(parts[1].strip())
            continue
        if line.startswith("+++ "):
            path = line[4:].strip()
            if path == "/dev/null":
                continue
            if path.startswith("b/"):
                path = path[2:]
            paths.append(path)
            continue
        if line.startswith(("---", "@@", "+", "-")):
            continue
        if "/" in line or line.endswith((".xlf", ".resx", ".cs")):
            paths.append(line)
    seen: Set[str] = set()
    out: List[str] = []
    for p in paths:
        if p not in seen:
            seen.add(p)
            out.append(p)
    return out


_XLF_LOCALE = re.compile(r"\.([a-zA-Z]{2,3}(?:-[A-Za-z0-9]+)*)\.xlf$")


def xlf_to_resx_basename(xlf_path: str) -> str:
    """Return the bare resource basename for a `.xlf` file.

    `path/to/Strings.de.xlf` → `Strings`
    `Resources/xlf/Resource.cs.xlf` → `Resource`
    `Strings.xlf` (no-locale variant) → `Strings`

    Basename matching is used instead of strict path matching because testfx
    places `.xlf` files under a `xlf/` subdirectory while the matching
    `.resx` lives in the parent directory (e.g.
    `Resources/Resource.resx` ↔ `Resources/xlf/Resource.cs.xlf`), and other
    projects may use yet other layouts. A basename match is permissive
    enough to handle every layout without false positives, since the only
    way it can miss a hand-edit is if the PR also happens to change a
    completely unrelated `.resx` with the same basename — extremely unlikely
    in practice.
    """
    name = Path(xlf_path.replace("\\", "/")).name
    m = _XLF_LOCALE.search(name)
    if m:
        return name[: m.start()]
    return Path(name).stem  # strip trailing `.xlf`


def find_violations(paths: Iterable[str]) -> List[Tuple[str, str]]:
    """Return `(xlf_path, expected_resx_basename)` for every `.xlf` change
    that has no `.resx` change with a matching basename in the diff."""
    norm_paths = [p.replace("\\", "/") for p in paths]
    resx_basenames: Set[str] = {
        Path(p).stem for p in norm_paths if p.endswith(".resx")
    }
    violations: List[Tuple[str, str]] = []
    for p in norm_paths:
        if not p.endswith(".xlf"):
            continue
        base = xlf_to_resx_basename(p)
        if base not in resx_basenames:
            violations.append((p, base + ".resx"))
    return violations


def is_bot(author: str) -> bool:
    if not author:
        return False
    if author in KNOWN_BOTS:
        return True
    if author.endswith("[bot]"):
        return True
    return False


def format_report(violations: List[Tuple[str, str]]) -> str:
    lines = [
        "❌ Localization policy violation: manual `.xlf` edits detected.",
        "",
        "Repository policy (`.github/copilot-instructions.md` → Localization",
        "Guidelines):",
        "",
        "  > NEVER manually modify `*.xlf` files. Instead, regenerate them by",
        "  > running `dotnet msbuild <project>.csproj /t:UpdateXlf` on the",
        "  > owning project.",
        "",
        "The following `.xlf` files were modified without a corresponding",
        "`.resx` change in the same PR. If this PR is a translation update,",
        "it should go through the OneLocBuild bot path; if it is a source-string",
        "update, edit the `.resx` and re-run `/t:UpdateXlf`.",
        "",
    ]
    for xlf, resx in violations:
        lines.append(f"  - `{xlf}` (expected a matching `{resx}` change in this PR)")
    lines += [
        "",
        "How to fix:",
        "",
        "  1. Edit the `.resx` source string.",
        "  2. Run:",
        "       dotnet msbuild <owning-project>.csproj /t:UpdateXlf",
        "  3. Commit both the `.resx` and the regenerated `.xlf` together.",
    ]
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
        help="Read the diff from this file instead of running `git diff`.",
    )
    parser.add_argument(
        "--author",
        default=os.environ.get("PR_AUTHOR", ""),
        help="PR author login (default: $PR_AUTHOR). Bot accounts are exempt.",
    )
    args = parser.parse_args(argv)

    if is_bot(args.author):
        print(f"✅ PR author '{args.author}' is a bot — manual-xlf guard skipped.")
        return 0

    if args.diff_file is not None:
        try:
            diff_text = args.diff_file.read_text(encoding="utf-8", errors="replace")
        except OSError as exc:
            sys.stderr.write(f"Could not read diff file {args.diff_file}: {exc}\n")
            return 2
    else:
        diff_text = _diff_from_git(args.base)

    paths = parse_changed_paths(diff_text)
    violations = find_violations(paths)
    if not violations:
        print("✅ No manual `.xlf` edits detected.")
        return 0

    report = format_report(violations)
    print(report)
    write_step_summary(report)
    return 1


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
