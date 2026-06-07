#!/usr/bin/env python3
"""Reminder: when a PR touches an `ICommandLineOptionsProvider` implementation,
the corresponding `--help` / `--info` acceptance-test expectations MUST be
updated in lockstep (per `.github/copilot-instructions.md`).

This script does NOT decide whether the change actually affects the rendered
help output — only a human (or the acceptance tests at CI time) can know that
for sure. What it does is the cheap, deterministic part: flag every PR that
changes a provider file without also touching any of the four expectation
files, so the author and reviewer get an early, in-PR reminder.

Inputs:
    --diff-file <path>   Read a unified diff. Falls back to `git diff
                         <base>...HEAD` otherwise.
    --base <ref>         Git ref to diff against (default: $BASE_REF,
                         then $BASE_SHA for backward compatibility,
                         then origin/main).

Exit codes:
    0  — non-blocking by design. Either no provider files changed, OR
         provider files changed and at least one help-expectation file
         is in the diff (contract plausibly satisfied), OR provider
         files changed without expectation-file changes (a refactor
         that doesn't touch help output is common; the reminder is
         surfaced via stdout + GITHUB_STEP_SUMMARY + a workflow
         `::notice` annotation but the workflow stays green).
    2  — usage / IO error.

The "always exit 0" choice mirrors how the upstream policy reads: the
acceptance tests are the authoritative gate. This script's job is to make
the policy visible *before* a reviewer has to remember it.
"""

from __future__ import annotations

import argparse
import os
import re
import subprocess
import sys
from pathlib import Path
from typing import Iterable, List, Set, Tuple


# Ensure UTF-8 output on Windows consoles (cp1252 by default) so the ⚠/✅
# markers in the report don't crash the script on local invocations.
# Encoding setup is best-effort and must not fail the script; narrow the
# except to the failures the call actually raises so we don't swallow
# unrelated programmer errors.
if hasattr(sys.stdout, "reconfigure"):
    try:
        sys.stdout.reconfigure(encoding="utf-8", errors="replace")
    except (AttributeError, ValueError, OSError):
        # Reconfigure is best-effort: if stdout is non-reconfigurable or the
        # platform raises during the swap, fall back to whatever encoding
        # Python picked at startup. Failing the script here would break CI
        # for environments where the existing encoding is already fine.
        pass


# Files that hold the `--help` / `--info` expectations enumerated in
# `.github/copilot-instructions.md` (CLI options guidelines section).
EXPECTATION_FILES: Set[str] = {
    "test/IntegrationTests/Microsoft.Testing.Platform.Acceptance.IntegrationTests/HelpInfoTests.cs",
    "test/IntegrationTests/Microsoft.Testing.Platform.Acceptance.IntegrationTests/HelpInfoAllExtensionsTests.cs",
    "test/IntegrationTests/Microsoft.Testing.Platform.Acceptance.IntegrationTests/MSBuild.KnownExtensionRegistration.cs",
    "test/IntegrationTests/MSTest.Acceptance.IntegrationTests/HelpInfoTests.cs",
}

# File-name patterns that indicate a CLI-options provider has been touched.
# Names rather than paths so the check stays robust against folder moves.
# Patterns require the filename to END at `Provider.cs` so test files like
# `*CommandLineOptionsProviderTests.cs` do not trigger false positives.
PROVIDER_PATTERNS: Tuple[re.Pattern[str], ...] = (
    re.compile(r"CommandLineOptionsProvider\.cs$", re.IGNORECASE),
    re.compile(r"^PlatformCommandLineProvider\.cs$", re.IGNORECASE),
    re.compile(r"^MSTestExtension\.cs$"),
)


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
    """Accepts either a `git diff --name-only` listing or a unified diff."""
    paths: List[str] = []
    for raw_line in diff_text.splitlines():
        line = raw_line.strip()
        if not line:
            continue
        if line.startswith("diff --git "):
            # `diff --git a/<path> b/<path>` — take the b-side.
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
        # name-only mode: a bare path per line.
        if "/" in line or line.endswith(".cs"):
            paths.append(line)
    # De-duplicate while preserving order.
    seen: Set[str] = set()
    out: List[str] = []
    for p in paths:
        if p not in seen:
            seen.add(p)
            out.append(p)
    return out


def classify(paths: Iterable[str]) -> Tuple[List[str], List[str]]:
    providers: List[str] = []
    expectations: List[str] = []
    for path in paths:
        norm = path.replace("\\", "/")
        if norm in EXPECTATION_FILES:
            expectations.append(norm)
            continue
        name = Path(norm).name
        for pattern in PROVIDER_PATTERNS:
            if pattern.search(name):
                providers.append(norm)
                break
    return providers, expectations


def format_reminder(providers: List[str]) -> str:
    lines = [
        "⚠️ CLI help-text expectation reminder",
        "",
        "This PR changes one or more CLI-options provider files but does not",
        "touch any of the four `--help` / `--info` acceptance-test expectation",
        "files documented in `.github/copilot-instructions.md`:",
        "",
    ]
    for f in sorted(EXPECTATION_FILES):
        lines.append(f"  - `{f}`")
    lines += [
        "",
        "If this PR adds, renames, or changes the description/arguments of any",
        "CLI option, the matching `--help` and `--info` blocks in those files",
        "MUST be updated in the same change. If the provider edit is a pure",
        "refactor with no observable change to the rendered help output, you",
        "can ignore this reminder — the acceptance tests in CI are the final",
        "word.",
        "",
        "Changed provider files in this PR:",
        "",
    ]
    for p in providers:
        lines.append(f"  - `{p}`")
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
        default=os.environ.get("BASE_REF", os.environ.get("BASE_SHA", "origin/main")),
        help=(
            "Base ref to diff against (default: $BASE_REF, then $BASE_SHA "
            "for backward compatibility, then origin/main)."
        ),
    )
    parser.add_argument(
        "--diff-file",
        type=Path,
        help="Read the diff (unified or name-only) from this file instead of "
        "running `git diff`.",
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

    paths = parse_changed_paths(diff_text)
    providers, expectations = classify(paths)

    if not providers:
        print("✅ No CLI-options provider files changed in this PR.")
        return 0

    if expectations:
        print(
            "✅ CLI-options provider files were changed and at least one help/"
            "info expectation file is in the diff — contract plausibly satisfied. "
            "Acceptance tests in CI are the final word.\n"
        )
        print("Changed providers:")
        for p in providers:
            print(f"  - {p}")
        print("\nChanged expectation files:")
        for e in expectations:
            print(f"  - {e}")
        return 0

    reminder = format_reminder(providers)
    print(reminder)
    write_step_summary(reminder)
    # Also emit a workflow `notice` annotation so it shows up in the PR
    # Checks tab as a soft signal (yellow ⚠ icon) rather than a hard red X.
    print(
        "::notice title=CLI help-text expectation reminder::"
        + "CLI-options provider files changed without touching any "
        + "HelpInfo*.cs / MSBuild.KnownExtensionRegistration.cs. See "
        + "the workflow summary for details."
    )
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
