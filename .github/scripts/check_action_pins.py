#!/usr/bin/env python3
"""
Verify that every GitHub Actions reference under .github/workflows is pinned and
aligned with the repo-wide pin ledger in .github/aw/actions-lock.json.

Motivation
----------
Compiling an agentic workflow with a locally-built `gh aw` extension can silently
rewrite security-relevant pins in the generated `*.lock.yml` files (see
https://github.com/microsoft/testfx/issues/10258). Two corruptions were observed:

  * `actions/checkout` downgraded from the repo-aligned v7.0.1 to v7.0.0, because
    `.github/aw/actions-lock.json` keys its overrides *by version string* and the
    framework-injected steps request a version the ledger does not carry. The
    lookup key-misses, no override applies, and the CLI's stale built-in SHA wins.
  * `github/gh-aw-actions/setup` un-pinned from an immutable SHA to the mutable
    `@v0.83.1` tag.

Both land in generated files that reviewers rarely read line-by-line, and the
`compiler_version` header does not catch them: it is an identity claim, not a
statement about the emitted bytes. This script is the missing gate.

Checks
------
  A. Every external `uses:` reference is pinned to a 40-character commit SHA.
  B. Every pinned reference carries a `# <version>` trailing comment.
  C. For actions tracked in .github/aw/actions-lock.json, the version comment
     matches the version recorded there (catches the v7.0.1 -> v7.0.0 downgrade).
  D. Each action repository resolves to exactly one SHA across all workflow
     files (catches a partial rewrite that touches only some call sites).
  E. Each `*.lock.yml` `gh-aw-manifest` header entry matches the ledger exactly
     (repo + version + SHA) for actions the ledger tracks.
  F. For actions tracked in the ledger, the SHA equals the ledger SHA or the
     commit it dereferences to via DEREFERENCED_TAG_SHAS. Any other SHA fails,
     so a tracked action cannot be repointed at an arbitrary commit by keeping
     the ledger's version comment and rewriting every call site consistently.

`gh aw` records the annotated *tag object* SHA in the ledger for some actions but
emits the dereferenced *commit* SHA in `uses:` lines. That single legitimate
divergence is recorded explicitly in DEREFERENCED_TAG_SHAS and reported as a
warning (fatal under --strict) so it stays visible rather than silently accepted.

Usage
-----
    python .github/scripts/check_action_pins.py
    python .github/scripts/check_action_pins.py --strict
"""

from __future__ import annotations

import argparse
import json
import re
import sys
from collections import defaultdict
from dataclasses import dataclass
from pathlib import Path

REPO_ROOT = Path(__file__).resolve().parents[2]
WORKFLOWS_DIR = REPO_ROOT / ".github" / "workflows"
ACTIONS_LOCK_PATH = REPO_ROOT / ".github" / "aw" / "actions-lock.json"

SHA_RE = re.compile(r"^[0-9a-f]{40}$")
MANIFEST_PREFIX = "# gh-aw-manifest: "

# Annotated tag objects the ledger records, mapped to the commit each dereferences
# to. `gh aw` stores the tag-object SHA in .github/aw/actions-lock.json but emits
# the commit SHA in `uses:` lines, so without this table the two can never agree.
# Recording the pair explicitly keeps that divergence approved rather than merely
# tolerated, which lets check F reject every other SHA. Verify a new entry with:
#   gh api repos/<owner>/<repo>/git/tags/<tag-object-sha> --jq .object.sha
DEREFERENCED_TAG_SHAS = {
    # github/codeql-action v4.37.3
    "c54b30b7df092240050e69945842bc67aee0f0f4": "e4fba868fa4b1b91e1fdab776edc8cfbe6e9fb81",
}

# `uses: owner/repo/path@ref  # v1.2.3`, optionally quoted and optionally a list item.
USES_RE = re.compile(
    r"""^\s*(?:-\s*)?uses:\s*(?P<quote>["']?)(?P<ref>[^"'\s#]+)(?P=quote)\s*(?:\#\s*(?P<comment>.*?))?\s*$"""
)

# The `# Custom actions used:` inventory block that `gh aw compile` writes into
# every lock file, e.g. `#   - actions/checkout@<sha> # v7.0.1`.
LISTED_RE = re.compile(
    r"""^\s*\#\s+-\s+(?P<ref>[^\s\#]+@[^\s\#]+)\s*(?:\#\s*(?P<comment>.*?))?\s*$"""
)


@dataclass(frozen=True)
class Reference:
    """A single `owner/repo[/path]@ref` occurrence found in a workflow file."""

    path: Path
    line_number: int
    repo: str
    ref: str
    version: str | None

    @property
    def location(self) -> str:
        return f"{self.path.relative_to(REPO_ROOT).as_posix()}:{self.line_number}"


@dataclass
class Findings:
    errors: list[str]
    warnings: list[str]


def load_ledger() -> dict[str, dict[str, str]]:
    """Map action repository -> {"version", "sha"} from the gh-aw pin ledger."""
    data = json.loads(ACTIONS_LOCK_PATH.read_text(encoding="utf-8"))
    ledger: dict[str, dict[str, str]] = {}
    for entry in data.get("entries", {}).values():
        repo = entry.get("repo")
        if not repo:
            continue
        ledger[repo] = {"version": entry.get("version", ""), "sha": entry.get("sha", "")}

    return ledger


def is_external_action(ref: str) -> bool:
    """Filter out local actions, reusable workflows, containers and expressions."""
    if not ref or ref.startswith(("./", "../", ".github/", "docker://")):
        return False
    if "${{" in ref:
        return False

    repo, separator, rest = ref.partition("@")
    if not separator or rest.startswith("sha256:"):
        return False

    owner, slash, _ = repo.partition("/")
    # Registry references such as `ghcr.io/github/gh-aw-node` are containers, not actions.
    return bool(slash) and "." not in owner and ":" not in repo


def parse_reference(path: Path, line_number: int, ref: str, comment: str | None) -> Reference:
    repo, _, pinned = ref.partition("@")
    # Version comments may carry a suffix, e.g. `# v9.0.0 (source v9)`.
    version = comment.split()[0] if comment and comment.split() else None

    return Reference(path=path, line_number=line_number, repo=repo, ref=pinned, version=version)


def collect_references(path: Path) -> list[Reference]:
    references: list[Reference] = []
    for line_number, line in enumerate(path.read_text(encoding="utf-8").splitlines(), start=1):
        for pattern in (USES_RE, LISTED_RE):
            match = pattern.match(line)
            if not match:
                continue
            ref = match.group("ref")
            if not is_external_action(ref):
                continue
            references.append(parse_reference(path, line_number, ref, match.group("comment")))
            break

    return references


def read_manifest_actions(path: Path) -> list[dict[str, str]]:
    """Return the `actions` array of a lock file's `gh-aw-manifest` header."""
    for line in path.read_text(encoding="utf-8").splitlines()[:10]:
        if line.startswith(MANIFEST_PREFIX):
            manifest = json.loads(line[len(MANIFEST_PREFIX) :])
            return manifest.get("actions", [])

    return []


def check_references(references: list[Reference], ledger: dict[str, dict[str, str]]) -> Findings:
    errors: list[str] = []
    warnings: list[str] = []
    shas_by_repo: dict[str, dict[str, list[Reference]]] = defaultdict(lambda: defaultdict(list))

    for reference in references:
        if not SHA_RE.match(reference.ref):
            errors.append(
                f"{reference.location}: '{reference.repo}@{reference.ref}' is not pinned to a "
                "40-character commit SHA. Mutable tags can be repointed upstream at any time."
            )
            continue

        shas_by_repo[reference.repo][reference.ref].append(reference)

        if reference.version is None:
            errors.append(
                f"{reference.location}: '{reference.repo}' is pinned to a SHA but has no "
                "'# <version>' comment, so pin drift cannot be reviewed."
            )
            continue

        tracked = ledger.get(reference.repo)
        if tracked and reference.version != tracked["version"]:
            errors.append(
                f"{reference.location}: '{reference.repo}' is pinned to {reference.version} but "
                f".github/aw/actions-lock.json records {tracked['version']}. Recompile the "
                "workflow on the aligned toolchain instead of a local gh-aw build."
            )

    for repo, occurrences in sorted(shas_by_repo.items()):
        if len(occurrences) > 1:
            details = "; ".join(
                f"{sha} ({refs[0].location}"
                + (f" and {len(refs) - 1} more" if len(refs) > 1 else "")
                + ")"
                for sha, refs in sorted(occurrences.items())
            )
            errors.append(f"'{repo}' is pinned to multiple SHAs across workflows: {details}")
            continue

        tracked = ledger.get(repo)
        if not tracked:
            continue

        sha = next(iter(occurrences))
        if sha == tracked["sha"]:
            continue

        location = occurrences[sha][0].location
        if sha == DEREFERENCED_TAG_SHAS.get(tracked["sha"]):
            warnings.append(
                f"'{repo}' is pinned to {sha}, the commit that the ledger's annotated tag "
                f"object {tracked['sha']} dereferences to. This pairing is recorded in "
                "DEREFERENCED_TAG_SHAS and is approved."
            )
            continue

        errors.append(
            f"{location}: '{repo}' is pinned to {sha} but .github/aw/actions-lock.json "
            f"records {tracked['sha']}, and {sha} is not a recorded dereference of it. "
            "Recompile the workflow on the aligned toolchain; if the ledger genuinely "
            "stores an annotated tag object, add the verified pair to DEREFERENCED_TAG_SHAS."
        )

    return Findings(errors=errors, warnings=warnings)


def check_manifests(lock_files: list[Path], ledger: dict[str, dict[str, str]]) -> Findings:
    errors: list[str] = []

    for path in lock_files:
        location = path.relative_to(REPO_ROOT).as_posix()
        for entry in read_manifest_actions(path):
            repo = entry.get("repo", "")
            tracked = ledger.get(repo)
            if not tracked:
                continue

            version = entry.get("version", "")
            sha = entry.get("sha", "")
            if version != tracked["version"] or sha != tracked["sha"]:
                errors.append(
                    f"{location}: gh-aw-manifest pins '{repo}' to {version}@{sha} but "
                    f".github/aw/actions-lock.json records {tracked['version']}@{tracked['sha']}. "
                    "Regenerate the lock file on the pinned github/gh-aw-actions/setup toolchain."
                )

    return Findings(errors=errors, warnings=[])


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--strict", action="store_true", help="Treat warnings as errors.")
    args = parser.parse_args()

    if not ACTIONS_LOCK_PATH.is_file():
        print(f"error: pin ledger not found at {ACTIONS_LOCK_PATH}", file=sys.stderr)
        return 1

    ledger = load_ledger()
    workflow_files = sorted(
        path for pattern in ("*.yml", "*.yaml") for path in WORKFLOWS_DIR.rglob(pattern)
    )
    if not workflow_files:
        print(f"error: no workflow files found under {WORKFLOWS_DIR}", file=sys.stderr)
        return 1

    references: list[Reference] = []
    for path in workflow_files:
        references.extend(collect_references(path))

    reference_findings = check_references(references, ledger)
    manifest_findings = check_manifests([p for p in workflow_files if p.name.endswith(".lock.yml")], ledger)

    errors = reference_findings.errors + manifest_findings.errors
    warnings = reference_findings.warnings + manifest_findings.warnings

    for warning in warnings:
        print(f"warning: {warning}")
    for error in errors:
        print(f"error: {error}", file=sys.stderr)

    if errors or (args.strict and warnings):
        print(
            f"\nAction pin check failed: {len(errors)} error(s), {len(warnings)} warning(s) "
            f"across {len(workflow_files)} workflow file(s).",
            file=sys.stderr,
        )
        print(
            "See .github/workflows/README.md ('Action pinning') for how to regenerate lock "
            "files on the aligned toolchain.",
            file=sys.stderr,
        )
        return 1

    print(
        f"Action pin check passed: {len(references)} reference(s) across "
        f"{len(workflow_files)} workflow file(s), {len(warnings)} warning(s)."
    )

    return 0


if __name__ == "__main__":
    sys.exit(main())
