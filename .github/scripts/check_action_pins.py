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
  A. Every repository-style `uses:` reference is pinned to a 40-character commit
     SHA, and every `docker://` container action is pinned to an image digest.
     Both execute arbitrary code, so both must be immutable.
  B. Every pinned repository reference carries a trailing comment that actually
     looks like a version, so a placeholder such as `# TODO` cannot stand in.
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
  G. A given SHA is labelled with the same version everywhere. For actions the
     ledger does not track, this is the only thing keeping their labels honest.
  H. Every `uses` value reachable in the *parsed* YAML is also written as a plain
     scannable line. The structural pass sees exactly what GitHub Actions would
     execute, so block scalars, flow mappings, quoted keys and aliases cannot hide
     an unpinned action from the text pass that validates pins and version
     comments; anything the text pass cannot see fails instead of passing silently.

`gh aw` records the annotated *tag object* SHA in the ledger for some actions but
emits the dereferenced *commit* SHA in `uses:` lines. That single legitimate
divergence is recorded explicitly in DEREFERENCED_TAG_SHAS and reported as a
warning (fatal under --strict) so it stays visible rather than silently accepted.

Usage
-----
    python -m pip install -r .github/scripts/check-action-pins-requirements.txt
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

try:
    import yaml
except ModuleNotFoundError:  # pragma: no cover - guarded so the gate never silently degrades
    print(
        "error: PyYAML is required. The structural pass parses each workflow to find every "
        "`uses` value that GitHub Actions would really execute, so the gate must not run "
        "without it. Install it with: python -m pip install pyyaml",
        file=sys.stderr,
    )
    raise SystemExit(1)

REPO_ROOT = Path(__file__).resolve().parents[2]
WORKFLOWS_DIR = REPO_ROOT / ".github" / "workflows"
ACTIONS_LOCK_PATH = REPO_ROOT / ".github" / "aw" / "actions-lock.json"

SHA_RE = re.compile(r"^[0-9a-f]{40}$")
DOCKER_PREFIX = "docker://"
# `docker://` container actions are pinned by image digest rather than commit SHA.
IMAGE_DIGEST_RE = re.compile(r"^sha256:[0-9a-f]{64}$")
# A version comment must actually look like a version. Accepting any non-empty
# token would let `# TODO` satisfy check B, which matters most for actions the
# ledger does not track and therefore cannot cross-check.
VERSION_RE = re.compile(r"^v?\d+(?:\.\d+)*(?:[-+][0-9A-Za-z.-]+)?$")
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

# `uses: owner/repo/path@ref  # v1.2.3`. YAML accepts a quoted key and whitespace
# before the colon (`"uses":`, `uses :`), and all of those still execute as `uses`
# entries, so the key must be matched as loosely as YAML parses it. Otherwise an
# unpinned action slips past this gate simply by being written differently.
USES_RE = re.compile(
    r"""^\s*(?:-\s*)?(?P<kq>["']?)uses(?P=kq)\s*:\s*"""
    r"""(?P<quote>["']?)(?P<ref>[^"'\s#]+)(?P=quote)\s*(?:\#\s*(?P<comment>.*?))?\s*$"""
)

# The `# Custom actions used:` inventory block that `gh aw compile` writes into
# every lock file, e.g. `#   - actions/checkout@<sha> # v7.0.1`.
LISTED_RE = re.compile(
    r"""^\s*\#\s+-\s+(?P<ref>[^\s\#]+@[^\s\#]+)\s*(?:\#\s*(?P<comment>.*?))?\s*$"""
)


@dataclass(frozen=True)
class Reference:
    """A single `uses` occurrence found in a workflow file."""

    path: Path
    line_number: int
    raw: str
    repo: str
    ref: str
    version: str | None

    @property
    def is_docker(self) -> bool:
        return self.raw.startswith(DOCKER_PREFIX)

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
    """True for a repository-style `owner/repo[/path]@ref` action reference."""
    if not ref or ref.startswith(("./", "../", ".github/", DOCKER_PREFIX)):
        return False
    if "${{" in ref:
        return False

    repo, separator, rest = ref.partition("@")
    if not separator or rest.startswith("sha256:"):
        return False

    owner, slash, _ = repo.partition("/")
    # Registry references such as `ghcr.io/github/gh-aw-node` are containers, not actions.
    return bool(slash) and "." not in owner and ":" not in repo


def is_docker_action(ref: str) -> bool:
    """True for a `docker://` container action, which Actions executes like any other."""
    return ref.startswith(DOCKER_PREFIX) and "${{" not in ref


def is_checkable_uses(ref: str) -> bool:
    """True for any `uses` form this gate is responsible for holding immutable."""
    return is_external_action(ref) or is_docker_action(ref)


def parse_reference(path: Path, line_number: int, ref: str, comment: str | None) -> Reference:
    repo, _, pinned = ref.partition("@")
    # Version comments may carry a suffix, e.g. `# v9.0.0 (source v9)`.
    version = comment.split()[0] if comment and comment.split() else None

    return Reference(
        path=path, line_number=line_number, raw=ref, repo=repo, ref=pinned, version=version
    )


def collect_references(path: Path) -> list[Reference]:
    references: list[Reference] = []
    for line_number, line in enumerate(path.read_text(encoding="utf-8").splitlines(), start=1):
        for pattern in (USES_RE, LISTED_RE):
            match = pattern.match(line)
            if not match:
                continue
            ref = match.group("ref")
            if not is_checkable_uses(ref):
                continue
            references.append(parse_reference(path, line_number, ref, match.group("comment")))
            break

    return references


def iter_uses_values(node: object):
    """Yield every `uses` value reachable in a parsed workflow document.

    Walking the parsed structure is what makes the gate closed by construction: it sees
    exactly what GitHub Actions would execute, regardless of whether the author wrote a
    plain line, a quoted key, a block scalar, a flow mapping, or an alias.
    """
    if isinstance(node, dict):
        for key, value in node.items():
            if key == "uses" and isinstance(value, str):
                yield value
            yield from iter_uses_values(value)
    elif isinstance(node, list):
        for item in node:
            yield from iter_uses_values(item)


def collect_structural_uses(path: Path) -> set[str]:
    """External action references reachable in `path` once parsed as YAML."""
    document = yaml.safe_load(path.read_text(encoding="utf-8"))

    return {value for value in iter_uses_values(document) if is_checkable_uses(value)}


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
        if reference.is_docker:
            # A container action runs arbitrary code just like a repository action, so a
            # mutable tag such as `docker://alpine:latest` is the same risk as a mutable
            # action tag. Digests are the only immutable identifier for an image.
            if not IMAGE_DIGEST_RE.match(reference.ref):
                errors.append(
                    f"{reference.location}: '{reference.raw}' is a container action that is "
                    "not pinned to an image digest. Use "
                    "'docker://<image>@sha256:<64-hex-digest>'; tags can be repointed "
                    "upstream at any time."
                )
            continue

        if not SHA_RE.match(reference.ref):
            errors.append(
                f"{reference.location}: '{reference.repo}@{reference.ref}' is not pinned to a "
                "40-character commit SHA. Mutable tags can be repointed upstream at any time."
            )
            continue

        shas_by_repo[reference.repo][reference.ref].append(reference)

        if reference.version is None or not VERSION_RE.match(reference.version):
            found = "no" if reference.version is None else f"'{reference.version}' as its"
            errors.append(
                f"{reference.location}: '{reference.repo}' is pinned to a SHA but has {found} "
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
        # One SHA must not be described by conflicting version comments. For actions the
        # ledger does not track this is the only thing that keeps their labels honest.
        for sha, refs in sorted(occurrences.items()):
            labels = {ref.version for ref in refs if ref.version is not None}
            if len(labels) > 1:
                details = "; ".join(
                    f"{version} ({next(r.location for r in refs if r.version == version)})"
                    for version in sorted(labels)
                )
                errors.append(
                    f"'{repo}' pins {sha} but labels it with conflicting versions: {details}. "
                    "A single commit cannot be two versions; correct the stale comment(s)."
                )

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
    structural_errors: list[str] = []
    for path in workflow_files:
        location = path.relative_to(REPO_ROOT).as_posix()
        file_references = collect_references(path)
        references.extend(file_references)

        # Reconcile the structural pass against the text pass. The text pass is what
        # validates the pin and its `# <version>` comment, so any `uses` the parser can
        # reach but the text pass cannot see is a hard failure rather than a silent pass.
        try:
            structural = collect_structural_uses(path)
        except yaml.YAMLError as error:
            structural_errors.append(
                f"{location}: could not be parsed as YAML, so its `uses` references cannot "
                f"be validated: {str(error).splitlines()[0]}"
            )
            continue

        scanned = {reference.raw for reference in file_references}
        for value in sorted(structural - scanned):
            structural_errors.append(
                f"{location}: `uses: {value}` is reachable in the parsed workflow but is not "
                "written as a plain `uses: <owner>/<repo>@<sha> # <version>` line. Block "
                "scalars, flow mappings and aliases hide the pin and its version comment from "
                "review, so rewrite it as a plain line."
            )

    reference_findings = check_references(references, ledger)
    manifest_findings = check_manifests([p for p in workflow_files if p.name.endswith(".lock.yml")], ledger)

    errors = structural_errors + reference_findings.errors + manifest_findings.errors
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
