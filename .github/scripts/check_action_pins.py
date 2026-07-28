#!/usr/bin/env python3
"""
Audit GitHub Actions `uses:` pins across the workflows in this repository.

Generated agentic-workflow files (`*.lock.yml`, plus the gh-aw-generated
`agentic_commands.yml` / `agentics-maintenance.yml`) are produced by
`gh aw compile`. A locally installed `gh aw` build can silently emit pins that
disagree with the repo-aligned values recorded in `.github/aw/actions-lock.json`
-- for example downgrading `actions/checkout` or replacing an immutable SHA with
a mutable tag -- and because the damage lands in generated files it easily rides
along unnoticed in an unrelated pull request (see microsoft/testfx#10258).

This script is a deterministic, offline guard for exactly that failure mode.
It enforces five rules:

    R1 (generated files) Every `uses:` reference must be pinned to a 40-character
       commit SHA. A mutable tag or branch is rejected. Generated files are
       classified by filename (`*.lock.yml` plus the gh-aw-generated
       `agentic_commands.yml` / `agentics-maintenance.yml`), with header banners
       as a secondary net.
    R2 (all files)       When an action repository appears in
       `.github/aw/actions-lock.json`, every SHA-pinned reference to it must use
       the locked SHA and carry a trailing `# vX.Y.Z` comment naming the locked
       version. A missing label fails too, so the check cannot be evaded by
       deleting it.
    R3 (all files)       An action repository must resolve to a single SHA across
       every scanned file. This catches drift for actions the compiler injects
       but that are absent from `actions-lock.json`.
    R4 (all files)       A `docker://` container action must be pinned to an image
       digest. Container actions run arbitrary code exactly like repository
       actions, so a mutable tag such as `docker://alpine:latest` carries the same
       risk, and a digest is the only immutable identifier for an image.
    R5 (all files)       Every `uses` occurrence reachable in the *parsed* YAML must
       also be written as a plain, scannable line. R1-R4 read lines, so a `uses`
       expressed as a block scalar, a flow mapping or an alias would otherwise be
       executed by GitHub while remaining invisible to the audit. Reconciliation
       counts occurrences and ignores the generated header, so a hidden occurrence
       is never excused by a comment or by another occurrence on a plain line.

Comparisons are made on a canonical form. GitHub resolves owner/repository names
case-insensitively, so `Actions/Checkout` would otherwise look like an untracked
action and escape R2 entirely; SHAs and digests are hex and are compared
case-insensitively too. Action subpaths keep their original case, since those are
repository paths and Linux runners are case-sensitive.

The audit fails closed: a missing, malformed, or empty `actions-lock.json` is an
error rather than a licence to skip R2, since R1 and R3 alone both pass when
every occurrence of an action is rewritten to the same stale SHA. A missing
PyYAML is likewise an error rather than a licence to skip R5.

References inside a workflow's generated header (the `# Custom actions used:`
block) are audited alongside the live `uses:` steps, because the corruption shows
up there too. The machine-readable `# gh-aw-manifest:` JSON header is *not*
audited: it is compiler bookkeeping copied from `actions-lock.json` at compile
time rather than an executable reference, so it self-heals on the next recompile.

Known limitation
----------------
Collection is position-independent: any mapping key named `uses` is treated as
executable, and the line scan matches any `uses:`-shaped line. An action input
literally named `uses` (a `uses:` key nested under `with:`), or a literal `uses:`
line inside a `run:` block, is data rather than an executable step but is still
collected. It is therefore audited, and because R5 compares counts per value a
non-executable line can offset a hidden executable occurrence of the same value.
Neither breaches pin integrity, since any value involved has itself been
validated, and no such key or line exists in this repository today. The behaviour
is deliberate because it fails *closed*: restricting collection to `jobs.*.uses`
and `jobs.*.steps[*].uses` would replace a conservative over-collection with an
inclusion list that fails *open* if the workflow schema ever grows another
executable `uses` position. A complete fix drives the audit from the parsed
document with line marks (`yaml.compose`) and excludes known data mappings, so
unknown structures still fail closed.
See https://github.com/microsoft/testfx/pull/10264#discussion_r3659972254.

Usage:
    python -m pip install -r .github/scripts/check-action-pins-requirements.txt
    python .github/scripts/check_action_pins.py          # audit, exit 1 on failure
    python .github/scripts/check_action_pins.py --list   # print the resolved pin table
"""

from __future__ import annotations

import argparse
import json
import re
import sys
from collections import Counter, defaultdict
from dataclasses import dataclass
from pathlib import Path

try:
    import yaml
except ModuleNotFoundError:  # pragma: no cover - guarded so the audit never silently degrades
    print(
        "error: PyYAML is required. R5 parses each workflow to find every `uses` value that "
        "GitHub Actions would really execute, so the audit must not run without it. Install it "
        "the same way CI does, with the hash-pinned requirements file: python -m pip install "
        "--require-hashes -r .github/scripts/check-action-pins-requirements.txt",
        file=sys.stderr,
    )
    raise SystemExit(1)

REPO_ROOT = Path(__file__).resolve().parents[2]
WORKFLOW_DIR = REPO_ROOT / ".github" / "workflows"
ACTIONS_DIR = REPO_ROOT / ".github" / "actions"
ACTIONS_LOCK_PATH = REPO_ROOT / ".github" / "aw" / "actions-lock.json"

SHA_RE = re.compile(r"^[0-9a-f]{40}$")
DOCKER_PREFIX = "docker://"
# Container actions are pinned by image digest rather than by commit SHA.
IMAGE_DIGEST_RE = re.compile(r"^sha256:[0-9a-f]{64}$")
ACTION_REF_RE = re.compile(r"^[A-Za-z0-9._-]+(?:/[A-Za-z0-9._-]+)+@\S+$")
# YAML accepts a quoted key and whitespace before the colon (`"uses":`, `uses :`),
# and all of those still execute as `uses` entries, so the key has to be matched as
# loosely as YAML parses it. Otherwise an unpinned action slips past simply by being
# written differently.
USES_RE = re.compile(
    r"""^\s*(?:-\s*)?(?P<kq>['"]?)uses(?P=kq)\s*:\s*"""
    r"""['"]?(?P<ref>[^'"\s]+)['"]?\s*(?:\#\s*(?P<comment>.*?))?\s*$"""
)
# `#   - actions/checkout@<sha> # v7.0.1` inside the generated header block.
HEADER_ACTION_RE = re.compile(
    r"""^#\s+-\s+(?P<ref>\S+@\S+?)\s*(?:\#\s*(?P<comment>.*?))?\s*$"""
)
MANIFEST_RE = re.compile(r"^#\s*gh-aw-manifest:")
# A file is "generated" when its name says so -- filename classification is
# deterministic and cannot be defeated by a compiler changing its header text.
# gh-aw emits several distinct banners (`generated by gh-aw`,
# `generated by pkg/workflow/maintenance_workflow.go`, ...), so the header
# markers below are only a secondary net for generated files added later.
GENERATED_SUFFIX = ".lock.yml"
GENERATED_FILENAMES = frozenset({"agentic_commands.yml", "agentics-maintenance.yml"})
GENERATED_MARKERS = ("# gh-aw-manifest:", "automatically generated by")
HEADER_ACTIONS_SECTION = "# Custom actions used:"


@dataclass(frozen=True)
class Reference:
    """A single `uses:`-style action reference discovered in a workflow file."""

    repo: str
    ref: str
    comment_version: str | None
    path: Path
    line: int
    generated: bool
    raw: str = ""
    is_docker: bool = False
    from_header: bool = False

    @property
    def location(self) -> str:
        return f"{self.path.relative_to(REPO_ROOT).as_posix()}:{self.line}"

    @property
    def is_sha_pinned(self) -> bool:
        return SHA_RE.match(self.ref) is not None


def canonical_repo(repo: str) -> str:
    """Canonicalize an action reference for comparison.

    GitHub resolves the owner and repository segments case-insensitively, so
    `Actions/Checkout` executes the same code as `actions/checkout` and must not be
    treated as a separate, untracked action that escapes R2. Any action subpath is
    left verbatim: it is a path inside the repository, and Linux runners are
    case-sensitive.
    """
    owner, slash, rest = repo.partition("/")
    if not slash:
        return owner.lower()

    name, subpath_slash, subpath = rest.partition("/")
    canonical = f"{owner.lower()}/{name.lower()}"

    return f"{canonical}/{subpath}" if subpath_slash else canonical


class LockFileError(RuntimeError):
    """Raised when `.github/aw/actions-lock.json` is missing or unusable."""


def load_actions_lock() -> dict[str, tuple[str, str]]:
    """Map action repository -> (version, sha) from .github/aw/actions-lock.json.

    Fails closed. The lock file is R2's only source of truth, so treating an
    absent or empty one as "nothing to enforce" would silently downgrade the
    audit to R1 + R3 -- and those two pass happily when every occurrence of an
    action is rewritten to the same stale SHA. Deleting the lock must therefore
    be an error, not a way to weaken the check.
    """
    if not ACTIONS_LOCK_PATH.exists():
        raise LockFileError(f"{ACTIONS_LOCK_PATH.relative_to(REPO_ROOT).as_posix()} is missing")

    try:
        data = json.loads(ACTIONS_LOCK_PATH.read_text(encoding="utf-8"))
    except json.JSONDecodeError as error:
        raise LockFileError(
            f"{ACTIONS_LOCK_PATH.relative_to(REPO_ROOT).as_posix()} is not valid JSON: {error}"
        ) from error

    locked: dict[str, tuple[str, str]] = {}
    for entry in data.get("entries", {}).values():
        repo = entry.get("repo")
        sha = entry.get("sha")
        version = entry.get("version", "")
        if repo and sha:
            locked[canonical_repo(repo)] = (version, sha.lower())

    if not locked:
        raise LockFileError(
            f"{ACTIONS_LOCK_PATH.relative_to(REPO_ROOT).as_posix()} contains no usable entries"
        )

    return locked


def split_ref(raw: str) -> tuple[str, str] | None:
    """Split `owner/repo@ref` into its parts, or return None when not an action ref."""
    if raw.startswith(("./", "../", ".\\", DOCKER_PREFIX)) or raw.startswith("${{"):
        return None
    if not ACTION_REF_RE.match(raw):
        return None

    repo, _, ref = raw.partition("@")
    if not repo or not ref or ref.startswith("sha256:"):
        return None

    return canonical_repo(repo), ref.lower()


def split_docker_ref(raw: str) -> tuple[str, str] | None:
    """Split `docker://image@digest` (or `docker://image:tag`) into image and pin.

    Container actions run arbitrary code exactly like repository actions, so they are
    audited rather than skipped; R4 requires the pin to be an immutable image digest.
    """
    if not raw.startswith(DOCKER_PREFIX) or "${{" in raw:
        return None

    image = raw[len(DOCKER_PREFIX) :]
    if not image:
        return None

    name, separator, digest = image.partition("@")

    return (name, digest.lower()) if separator else (image, "")


def normalize_comment_version(comment: str | None) -> str | None:
    """Extract the version token from a trailing comment such as `v9.0.0 (source v9)`."""
    if not comment:
        return None

    token = comment.strip().split()[0] if comment.strip() else ""
    return token or None


def is_generated(path: Path, text: str) -> bool:
    """Classify a workflow as compiler-generated.

    Filename classification is primary: `agentics-maintenance.yml` carries a
    `generated by pkg/workflow/maintenance_workflow.go` banner and no
    `gh-aw-manifest` header, so header sniffing alone would silently exempt it
    from R1 even though it holds a dozen `github/gh-aw-actions/setup*` pins --
    exactly the references #10258 reports being un-pinned.
    """
    if path.name.endswith(GENERATED_SUFFIX) or path.name in GENERATED_FILENAMES:
        return True

    return any(marker in text for marker in GENERATED_MARKERS)


def collect_references(path: Path) -> list[Reference]:
    text = path.read_text(encoding="utf-8")
    generated = is_generated(path, text)
    references: list[Reference] = []
    in_header_actions = False

    for lineno, line in enumerate(text.splitlines(), start=1):
        if MANIFEST_RE.match(line):
            # Compiler bookkeeping, not an executable reference. See module docstring.
            continue

        if line.startswith("#"):
            if line.rstrip() == HEADER_ACTIONS_SECTION:
                in_header_actions = True
                continue
            if in_header_actions:
                header_match = HEADER_ACTION_RE.match(line)
                if header_match is None:
                    in_header_actions = False
                    continue
                parts = split_ref(header_match.group("ref"))
                if parts is not None:
                    references.append(
                        Reference(
                            repo=parts[0],
                            ref=parts[1],
                            comment_version=normalize_comment_version(header_match.group("comment")),
                            path=path,
                            line=lineno,
                            generated=generated,
                            raw=header_match.group("ref"),
                            from_header=True,
                        )
                    )
            continue

        in_header_actions = False
        uses_match = USES_RE.match(line)
        if uses_match is None:
            continue

        raw = uses_match.group("ref")
        parts = split_ref(raw)
        is_docker = False
        if parts is None:
            parts = split_docker_ref(raw)
            is_docker = parts is not None
        if parts is None:
            continue

        references.append(
            Reference(
                repo=parts[0],
                ref=parts[1],
                comment_version=normalize_comment_version(uses_match.group("comment")),
                path=path,
                line=lineno,
                generated=generated,
                raw=raw,
                is_docker=is_docker,
            )
        )

    return references


def iter_uses_values(node: object):
    """Yield every `uses` value reachable in a parsed workflow document."""
    if isinstance(node, dict):
        for key, value in node.items():
            if key == "uses" and isinstance(value, str):
                yield value
            yield from iter_uses_values(value)
    elif isinstance(node, list):
        for item in node:
            yield from iter_uses_values(item)


def collect_structural_uses(path: Path) -> Counter[str]:
    """Count each auditable `uses` value reachable in `path` once parsed as YAML.

    Counts rather than distinct values, so an occurrence written in an unscannable
    form cannot be excused by a different occurrence of the same value that happens
    to sit on a plain line.
    """
    document = yaml.safe_load(path.read_text(encoding="utf-8"))
    values = (
        value
        for value in iter_uses_values(document)
        if split_ref(value) is not None or split_docker_ref(value) is not None
    )

    return Counter(values)


def iter_workflow_files() -> list[Path]:
    files: list[Path] = []
    for directory, patterns in ((WORKFLOW_DIR, ("*.yml", "*.yaml")), (ACTIONS_DIR, ("**/*.yml", "**/*.yaml"))):
        if not directory.exists():
            continue
        for pattern in patterns:
            files.extend(directory.glob(pattern))

    return sorted(set(files))


def audit(references: list[Reference], locked: dict[str, tuple[str, str]]) -> list[str]:
    errors: list[str] = []

    # R1: generated files must never carry a mutable reference.
    for reference in references:
        if reference.generated and not reference.is_docker and not reference.is_sha_pinned:
            errors.append(
                f"{reference.location}: `{reference.repo}@{reference.ref}` is not pinned to a commit SHA. "
                "Generated workflows must pin every action to an immutable SHA; recompile with the "
                "toolchain pinned in .github/aw/actions-lock.json."
            )

    # R4: container actions run arbitrary code just like repository actions, so a
    # mutable tag such as `docker://alpine:latest` carries the same risk as a mutable
    # action tag. A digest is the only immutable identifier for an image.
    for reference in references:
        if reference.is_docker and not IMAGE_DIGEST_RE.match(reference.ref):
            errors.append(
                f"{reference.location}: `{reference.raw}` is a container action that is not pinned "
                "to an image digest. Use `docker://<image>@sha256:<64-hex-digest>`; tags can be "
                "repointed upstream at any time."
            )

    # R2: SHA-pinned references must agree with .github/aw/actions-lock.json.
    for reference in references:
        if reference.is_docker or not reference.is_sha_pinned:
            continue
        expected = locked.get(reference.repo)
        if expected is None:
            continue

        expected_version, expected_sha = expected
        if reference.ref != expected_sha:
            errors.append(
                f"{reference.location}: `{reference.repo}` is pinned to {reference.ref} "
                f"({reference.comment_version or 'unknown version'}) but .github/aw/actions-lock.json "
                f"records {expected_sha} ({expected_version})."
            )
        elif expected_version and reference.comment_version != expected_version:
            # A missing label is a failure too: dropping `# vX.Y.Z` would otherwise
            # let a mislabelled pin through by simply deleting the evidence.
            actual = reference.comment_version or "no version label"
            errors.append(
                f"{reference.location}: `{reference.repo}` is pinned to the expected SHA but is labelled "
                f"{actual} instead of {expected_version}."
            )

    # R3: every action must resolve to a single SHA repo-wide.
    by_repo: dict[str, dict[str, list[Reference]]] = defaultdict(lambda: defaultdict(list))
    for reference in references:
        if reference.is_sha_pinned and not reference.is_docker:
            by_repo[reference.repo][reference.ref].append(reference)

    for repo, shas in sorted(by_repo.items()):
        if len(shas) < 2:
            continue
        details = []
        for sha, refs in sorted(shas.items()):
            versions = sorted({ref.comment_version for ref in refs if ref.comment_version})
            label = f" ({', '.join(versions)})" if versions else ""
            sample = ", ".join(ref.location for ref in refs[:3])
            more = f", +{len(refs) - 3} more" if len(refs) > 3 else ""
            details.append(f"    {sha}{label}: {sample}{more}")
        errors.append(
            f"`{repo}` resolves to {len(shas)} different SHAs across the workflows:\n" + "\n".join(details)
        )

    return errors


def print_pin_table(references: list[Reference]) -> None:
    by_repo: dict[str, set[tuple[str, str]]] = defaultdict(set)
    for reference in references:
        by_repo[reference.repo].add((reference.ref, reference.comment_version or ""))

    for repo, pins in sorted(by_repo.items()):
        for ref, version in sorted(pins):
            suffix = f" # {version}" if version else ""
            print(f"{repo}@{ref}{suffix}")


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--list", action="store_true", help="Print the resolved pin table and exit.")
    args = parser.parse_args(argv)

    files = iter_workflow_files()
    if not files:
        print(f"error: no workflow files found under {WORKFLOW_DIR}", file=sys.stderr)
        return 1

    references: list[Reference] = []
    structural_errors: list[str] = []
    for path in files:
        file_references = collect_references(path)
        references.extend(file_references)

        # R5: reconcile the parsed document against the line scan. The line scan is what
        # validates the pin and its `# <version>` comment, so any `uses` the parser can
        # reach but the scan cannot see is a hard failure rather than a silent pass.
        try:
            structural = collect_structural_uses(path)
        except yaml.YAMLError as error:
            structural_errors.append(
                f"{path.relative_to(REPO_ROOT).as_posix()}: could not be parsed as YAML, so its "
                f"`uses` references cannot be audited: {str(error).splitlines()[0]}"
            )
            continue

        # The generated `# Custom actions used:` header is excluded: it is a comment,
        # not an executable step, so it must not vouch for a hidden `uses`.
        scanned = Counter(
            reference.raw for reference in file_references if not reference.from_header
        )
        for value, count in sorted(structural.items()):
            hidden = count - scanned.get(value, 0)
            if hidden <= 0:
                continue

            occurrences = "occurrence" if hidden == 1 else "occurrences"
            structural_errors.append(
                f"{path.relative_to(REPO_ROOT).as_posix()}: {hidden} {occurrences} of "
                f"`uses: {value}` are reachable in the parsed workflow but are not written as a "
                "plain `uses: <owner>/<repo>@<sha> # <version>` line. Block scalars, flow "
                "mappings and aliases hide the pin and its version comment from review, so "
                "rewrite them as plain lines."
            )

    if args.list:
        print_pin_table(references)
        return 0

    try:
        locked = load_actions_lock()
    except LockFileError as error:
        print(f"error: {error}.", file=sys.stderr)
        print(
            "The actions lock is the only source of truth for the SHA every action must "
            "resolve to, so the audit fails closed rather than silently skipping that check. "
            "Restore the file (it is generated by `gh aw compile`) and re-run.",
            file=sys.stderr,
        )
        return 1

    errors = structural_errors + audit(references, locked)
    if errors:
        print(f"Action pin audit failed with {len(errors)} problem(s):\n", file=sys.stderr)
        for error in errors:
            print(f"  - {error}", file=sys.stderr)
        print(
            "\nGenerated `.lock.yml` files must be compiled with the toolchain pinned in "
            "`.github/aw/actions-lock.json`. A locally installed `gh aw` build can emit stale "
            "pins (microsoft/testfx#10258); recompile on the pinned toolchain, or re-run "
            "`gh aw compile` after `gh extension upgrade aw`, and commit the corrected lock files.",
            file=sys.stderr,
        )
        return 1

    print(f"Action pin audit passed: {len(references)} reference(s) across {len(files)} file(s).")
    return 0


if __name__ == "__main__":
    sys.exit(main())
