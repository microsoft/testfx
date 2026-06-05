#!/usr/bin/env python3
"""Extract test methods that were added or modified in a PR.

Used by .github/workflows/grade-tests-on-pr.md.

Usage:
    extract-changed-tests.py <base_sha> <head_sha> <output_tsv>

For every C# file under `test/**` whose name suggests it is a test file
(matches `Tests`, `UnitTests`, or `IntegrationTests`), this script:

1. Asks git for the changed line ranges on the HEAD side (`--unified=0`).
2. Walks the file at HEAD and identifies test methods — defined as methods
   whose declaration is preceded (within a few lines) by an MSTest /
   xUnit / NUnit / TUnit attribute (`[TestMethod]`, `[DataTestMethod]`,
   `[Fact]`, `[Theory]`, `[Test]`, `[TestCase]`).
3. For each test method whose source span overlaps any changed line range,
   emits a TSV row:

       <filepath>\t<start_line>\t<end_line>\t<fully.qualified.name>

If no rows would be emitted, the script writes nothing and exits 0. The
caller checks for an empty output file to gate the agent.

The script is intentionally simple: it does not require Roslyn or any
NuGet package. It uses only the Python standard library, which is
available by default on the GitHub Actions ubuntu runners testfx uses.
"""

from __future__ import annotations

import re
import subprocess
import sys
from pathlib import Path

ATTR_RE = re.compile(r"\[(?:TestMethod|DataTestMethod|Fact|Theory|Test|TestCase)\b")
NS_RE = re.compile(r"^\s*namespace\s+([A-Za-z_][A-Za-z0-9_.]*)\s*[;{]?\s*$")
CLASS_RE = re.compile(
    r"^\s*(?:public|internal|sealed|abstract|static|partial|\s)+class\s+([A-Za-z_][A-Za-z0-9_]*)"
)
SIG_RE = re.compile(
    r"^\s*(?:public|internal|private|protected)"
    r"(?:\s+(?:static|async|virtual|override|sealed|new|partial))*"
    r"\s+[A-Za-z_][\w<>,\[\]\?\s\.]*?"
    r"\s+([A-Za-z_]\w*)\s*\("
)
TEST_FILE_HINT = re.compile(r"(Tests?|UnitTests?|IntegrationTests?)", re.IGNORECASE)


def run_git(*args: str) -> str:
    return subprocess.check_output(["git", *args], text=True, errors="replace")


def changed_test_files(base_sha: str, head_sha: str) -> list[str]:
    raw = run_git(
        "diff", "--name-only", "--diff-filter=AM",
        base_sha, head_sha, "--", "test/*.cs", "test/**/*.cs",
    )
    files = []
    for line in raw.splitlines():
        line = line.strip()
        if not line or not line.endswith(".cs"):
            continue
        if not line.startswith("test/"):
            continue
        if TEST_FILE_HINT.search(Path(line).name):
            files.append(line)
    return files


def changed_line_ranges(base_sha: str, head_sha: str, file_path: str) -> list[tuple[int, int]]:
    raw = run_git("diff", "--unified=0", base_sha, head_sha, "--", file_path)
    ranges: list[tuple[int, int]] = []
    for line in raw.splitlines():
        if not line.startswith("@@"):
            continue
        # @@ -a,b +c,d @@
        m = re.search(r"\+(\d+)(?:,(\d+))?", line)
        if not m:
            continue
        start = int(m.group(1))
        length = int(m.group(2)) if m.group(2) is not None else 1
        if length == 0:
            continue
        ranges.append((start, start + length - 1))
    return ranges


def test_method_spans(file_path: str) -> list[tuple[int, int, str, str, str]]:
    """Return list of (start_line, end_line, namespace, class, method) for every
    test method in the file. Lines are 1-based, inclusive."""
    try:
        text = Path(file_path).read_text(encoding="utf-8", errors="replace")
    except FileNotFoundError:
        return []
    lines = text.splitlines()

    ns = ""
    cls = ""
    spans: list[tuple[int, int, str, str, str]] = []

    i = 0
    n = len(lines)
    while i < n:
        line = lines[i]
        m = NS_RE.match(line)
        if m:
            ns = m.group(1)
        m = CLASS_RE.match(line)
        if m:
            cls = m.group(1)

        if ATTR_RE.search(line):
            attr_start = i + 1
            j = i + 1
            while j < n and (lines[j].lstrip().startswith("[") or lines[j].strip() == ""):
                j += 1
            if j >= n:
                i = j
                continue
            sm = SIG_RE.match(lines[j])
            if not sm:
                i = j
                continue
            method = sm.group(1)
            # Walk forward to find the matching closing brace
            k = j
            while k < n and "{" not in lines[k]:
                k += 1
            if k >= n:
                i = j + 1
                continue
            depth = 0
            started = False
            while k < n:
                for ch in lines[k]:
                    if ch == "{":
                        depth += 1
                        started = True
                    elif ch == "}":
                        depth -= 1
                if started and depth == 0:
                    break
                k += 1
            method_end = k + 1
            spans.append((attr_start, method_end, ns, cls, method))
            i = k + 1
            continue
        i += 1
    return spans


def overlaps(span_start: int, span_end: int, ranges: list[tuple[int, int]]) -> bool:
    return any(not (e < span_start or s > span_end) for s, e in ranges)


def main(argv: list[str]) -> int:
    if len(argv) != 4:
        print(__doc__, file=sys.stderr)
        return 2
    base_sha, head_sha, out_path = argv[1], argv[2], argv[3]

    files = changed_test_files(base_sha, head_sha)
    if not files:
        Path(out_path).write_text("", encoding="utf-8")
        return 0

    rows: list[str] = []
    for file_path in files:
        ranges = changed_line_ranges(base_sha, head_sha, file_path)
        if not ranges:
            continue
        for start, end, ns, cls, method in test_method_spans(file_path):
            if overlaps(start, end, ranges):
                fq = ".".join(p for p in (ns, cls, method) if p)
                rows.append(f"{file_path}\t{start}\t{end}\t{fq}")

    Path(out_path).write_text("\n".join(rows) + ("\n" if rows else ""), encoding="utf-8")
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv))
