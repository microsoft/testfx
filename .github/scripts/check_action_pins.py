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
It enforces six rules:

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
    R3 (all files)       An action *repository* must resolve to a single SHA across
       every scanned file, grouping `owner/repo/action-a` with `owner/repo/action-b`
       since they share a checkout. This catches drift for actions the compiler
       injects but that are absent from `actions-lock.json`.
    R4 (all files)       A `docker://` container action must be pinned to an image
       digest. Container actions run arbitrary code exactly like repository
       actions, so a mutable tag such as `docker://alpine:latest` carries the same
       risk, and a digest is the only immutable identifier for an image.
    R5 (all files)       Every executable `uses` occurrence reachable in the
       *parsed* YAML must be written as a plain, single-line `uses: <ref>`
       mapping in a form the audit can parse. Block scalars, flow mappings and
       aliases hide the pin and any `# vX.Y.Z` label from review; a reference the
       recognizers cannot parse would fall out of every rule. Both fail here
       instead.
    R6 (lock files)      Every action reference declared in a generated
       `gh-aw-manifest` must use that manifest's SHA. This catches partial
       dependency updates that change executable `uses:` lines without
       recompiling the workflow.

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
up there too. The machine-readable `# gh-aw-manifest:` JSON header is also checked
against those references so a partial dependency update cannot combine generated
steps from one gh-aw version with setup scripts from another.

Executable collection
---------------------
The audit is driven from the parsed YAML node tree (`yaml.compose`) so strings
that merely contain `uses:` -- for example inside `run: |` scripts -- are never
mistaken for actions. It still fails closed: instead of allow-listing only the
currently documented executable schema positions, it treats every `uses` mapping
key as executable unless that key is nested below a known data mapping such as
`with`, `env`, `secrets`, `inputs`, `outputs`, or `defaults`. If GitHub Actions
adds another executable `uses` position later, the new structure is audited by
default rather than skipped silently.

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
from collections import defaultdict
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
MANIFEST_RE = re.compile(r"^#\s*gh-aw-manifest:\s*(?P<json>.*)$")
# A file is "generated" when its name says so -- filename classification is
# deterministic and cannot be defeated by a compiler changing its header text.
# gh-aw emits several distinct banners (`generated by gh-aw`,
# `generated by pkg/workflow/maintenance_workflow.go`, ...), so the header
# markers below are only a secondary net for generated files added later.
GENERATED_SUFFIX = ".lock.yml"
GENERATED_FILENAMES = frozenset({"agentic_commands.yml", "agentics-maintenance.yml"})
GENERATED_MARKERS = ("# gh-aw-manifest:", "automatically generated by")
HEADER_ACTIONS_SECTION = "# Custom actions used:"
# Keep this list narrow and data-oriented. We intentionally do not enumerate the
# executable GitHub Actions schema (`jobs.*.uses`, `steps[*].uses`, ...): that
# would fail open when the schema grows. Unknown mappings remain audited unless
# they sit below one of these known data containers.
DATA_MAPPING_KEYS = frozenset({"with", "env", "secrets", "inputs", "outputs", "defaults"})


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

    @property
    def location(self) -> str:
        return f"{self.path.relative_to(REPO_ROOT).as_posix()}:{self.line}"

    @property
    def is_sha_pinned(self) -> bool:
        return SHA_RE.match(self.ref) is not None

    @property
    def repo_root(self) -> str:
        """The canonical `owner/repo` this action lives in, ignoring any subpath.

        `owner/repo/action-a` and `owner/repo/action-b` are two actions from a single
        repository checkout, so R3 must hold them to one SHA. R2 keeps using the full
        locator, which is how `actions-lock.json` keys subpath actions such as
        `actions/cache/restore`.
        """
        owner, slash, rest = self.repo.partition("/")
        if not slash:
            return self.repo

        return f"{owner}/{rest.partition('/')[0]}"


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


def create_reference(
    *,
    raw: str,
    comment: str | None,
    path: Path,
    line: int,
    generated: bool,
) -> Reference | None:
    parts = split_ref(raw)
    is_docker = False
    if parts is None:
        parts = split_docker_ref(raw)
        is_docker = parts is not None
    if parts is None:
        return None

    return Reference(
        repo=parts[0],
        ref=parts[1],
        comment_version=normalize_comment_version(comment),
        path=path,
        line=line,
        generated=generated,
        raw=raw,
        is_docker=is_docker,
    )


def collect_header_references(path: Path, text: str, generated: bool) -> list[Reference]:
    """Collect generated-header references that YAML parsing cannot see."""
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
                reference = create_reference(
                    raw=header_match.group("ref"),
                    comment=header_match.group("comment"),
                    path=path,
                    line=lineno,
                    generated=generated,
                )
                if reference is not None:
                    references.append(reference)
            continue

        in_header_actions = False

    return references


def collect_manifest_pins(path: Path, text: str) -> tuple[dict[str, str], list[str]]:
    """Collect action SHAs from a generated gh-aw manifest."""
    manifests: list[tuple[int, re.Match[str]]] = []
    for lineno, line in enumerate(text.splitlines(), start=1):
        if manifest_match := MANIFEST_RE.match(line):
            manifests.append((lineno, manifest_match))

    if not manifests:
        return {}, []

    relative_path = path.relative_to(REPO_ROOT).as_posix()
    if len(manifests) > 1:
        return {}, [f"{relative_path}: contains more than one `gh-aw-manifest` header."]

    lineno, manifest_match = manifests[0]
    try:
        manifest = json.loads(manifest_match.group("json"))
    except json.JSONDecodeError as error:
        return {}, [f"{relative_path}:{lineno}: `gh-aw-manifest` is not valid JSON: {error}"]

    if not isinstance(manifest, dict):
        return {}, [f"{relative_path}:{lineno}: `gh-aw-manifest` must be a JSON object."]

    actions = manifest.get("actions")
    if not isinstance(actions, list):
        return {}, [f"{relative_path}:{lineno}: `gh-aw-manifest.actions` must be a JSON array."]

    pins: dict[str, str] = {}
    errors: list[str] = []
    for action in actions:
        if not isinstance(action, dict):
            errors.append(
                f"{relative_path}:{lineno}: every `gh-aw-manifest.actions` entry must be a JSON object."
            )
            continue

        repo = action.get("repo")
        sha = action.get("sha")
        if not isinstance(repo, str) or not isinstance(sha, str):
            errors.append(
                f"{relative_path}:{lineno}: every `gh-aw-manifest.actions` entry must contain "
                "string `repo` and `sha` fields."
            )
            continue

        parsed_ref = split_ref(f"{repo}@{sha}")
        if parsed_ref is None or not SHA_RE.match(parsed_ref[1]):
            errors.append(
                f"{relative_path}:{lineno}: `gh-aw-manifest.actions` entry `{repo}@{sha}` must "
                "name a valid action and a 40-character commit SHA."
            )
            continue

        canonical, normalized_sha = parsed_ref
        existing = pins.get(canonical)
        if existing is not None and existing != normalized_sha:
            errors.append(
                f"{relative_path}:{lineno}: `gh-aw-manifest` records multiple SHAs for "
                f"`{canonical}`: {existing} and {normalized_sha}."
            )
        pins[canonical] = normalized_sha

    return pins, errors


def is_scalar_named(node: yaml.nodes.Node, name: str) -> bool:
    return isinstance(node, yaml.nodes.ScalarNode) and node.value == name


def iter_executable_uses_nodes(
    node: yaml.nodes.Node | None,
) -> list[tuple[yaml.nodes.Node, yaml.nodes.Node]]:
    """Return `uses` mapping entries outside known YAML data containers.

    This is intentionally an exclusion list, not an inclusion list of executable
    GitHub Actions schema positions. Unknown structures stay in scope so the audit
    fails closed if Actions adds another executable `uses` position.
    """
    if node is None:
        return []

    found: list[tuple[yaml.nodes.Node, yaml.nodes.Node]] = []
    if isinstance(node, yaml.nodes.MappingNode):
        for key_node, value_node in node.value:
            if any(is_scalar_named(key_node, key) for key in DATA_MAPPING_KEYS):
                continue

            if is_scalar_named(key_node, "uses"):
                found.append((key_node, value_node))

            found.extend(iter_executable_uses_nodes(value_node))
    elif isinstance(node, yaml.nodes.SequenceNode):
        for item in node.value:
            found.extend(iter_executable_uses_nodes(item))

    return found


def is_auditable_uses(raw: str) -> bool:
    """True for a `uses` value this audit is responsible for holding immutable.

    Deliberately broader than `split_ref`: it excludes only the forms that are not
    remote actions at all (local paths and expressions). Anything else is in scope,
    so a reference the narrower recognizers cannot parse is reported by R5 as
    unsupported rather than silently dropped from every rule.
    """
    if not raw or raw.startswith(("./", "../", ".\\")) or "${{" in raw:
        return False

    return True


def collect_references(path: Path) -> tuple[list[Reference], list[str]]:
    text = path.read_text(encoding="utf-8")
    generated = is_generated(path, text)
    references = collect_header_references(path, text, generated)
    structural_errors: list[str] = []
    lines = text.splitlines()

    try:
        document = yaml.compose(text)
    except yaml.YAMLError as error:
        structural_errors.append(
            f"{path.relative_to(REPO_ROOT).as_posix()}: could not be parsed as YAML, so its "
            f"`uses` references cannot be audited: {str(error).splitlines()[0]}"
        )
        return references, structural_errors

    for key_node, value_node in iter_executable_uses_nodes(document):
        location = f"{path.relative_to(REPO_ROOT).as_posix()}:{key_node.start_mark.line + 1}"
        if not isinstance(value_node, yaml.nodes.ScalarNode) or not isinstance(value_node.value, str):
            structural_errors.append(
                f"{location}: `uses` must be a plain scalar action reference so the audit can "
                "validate its pin and version label."
            )
            continue

        raw = value_node.value
        if not is_auditable_uses(raw):
            continue

        line_index = value_node.start_mark.line
        raw_line = lines[line_index] if 0 <= line_index < len(lines) else ""
        uses_match = USES_RE.match(raw_line)
        if (
            uses_match is None
            or key_node.start_mark.line != value_node.start_mark.line
            or value_node.start_mark.line != value_node.end_mark.line
            or uses_match.group("ref") != raw
        ):
            if split_ref(raw) is None and split_docker_ref(raw) is None:
                # The parsed workflow can reach the value, but no pin rule knows how
                # to reason about it. Failing here keeps unsupported forms from
                # becoming silent exemptions from R1-R4.
                structural_errors.append(
                    f"{location}: `uses: {raw}` uses a reference form this audit cannot parse, so no "
                    "pin rule can be applied to it. Use `<owner>/<repo>[/<path>]@<sha> # <version>` "
                    "or `docker://<image>@sha256:<digest>`, or extend the audit to cover this form."
                )
                continue

            structural_errors.append(
                f"{location}: `uses: {raw}` is reachable in the parsed workflow but is not written as a "
                "plain `uses: <owner>/<repo>@<sha> # <version>` line. Block scalars, flow mappings "
                "and aliases hide the pin and its version comment from review, so rewrite it as a "
                "plain line."
            )
            continue

        reference = create_reference(
            raw=raw,
            comment=uses_match.group("comment"),
            path=path,
            line=line_index + 1,
            generated=generated,
        )
        if reference is None:
            structural_errors.append(
                f"{location}: `uses: {raw}` uses a reference form this audit cannot parse, so no "
                "pin rule can be applied to it. Use `<owner>/<repo>[/<path>]@<sha> # <version>` "
                "or `docker://<image>@sha256:<digest>`, or extend the audit to cover this form."
            )
            continue

        references.append(reference)

    return references, structural_errors


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

    # R3: every action must resolve to a single SHA repo-wide. Grouping is by the
    # `owner/repo` root rather than the full locator: actions sharing a repository
    # share a checkout, so `owner/repo/action-a` and `owner/repo/action-b` must agree.
    # This is also the only cross-check available for actions absent from the lock.
    by_repo: dict[str, dict[str, list[Reference]]] = defaultdict(lambda: defaultdict(list))
    for reference in references:
        if reference.is_sha_pinned and not reference.is_docker:
            by_repo[reference.repo_root][reference.ref].append(reference)

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


def audit_manifest_consistency(
    references: list[Reference],
    manifest_pins: dict[Path, dict[str, str]],
) -> list[str]:
    """Ensure each generated workflow uses the action SHAs in its own manifest."""
    by_path_and_repo: dict[Path, dict[str, list[Reference]]] = defaultdict(lambda: defaultdict(list))
    for reference in references:
        if not reference.is_docker and reference.is_sha_pinned:
            by_path_and_repo[reference.path][reference.repo].append(reference)

    errors: list[str] = []
    for path, pins in manifest_pins.items():
        references_by_repo = by_path_and_repo.get(path, {})
        manifest_repos = set(pins)
        reference_repos = set(references_by_repo)

        for repo in sorted(manifest_repos - reference_repos):
            errors.append(
                f"{path.relative_to(REPO_ROOT).as_posix()}: `gh-aw-manifest` records `{repo}`, "
                "but the generated workflow has no matching action reference."
            )

        for repo in sorted(reference_repos - manifest_repos):
            locations = ", ".join(reference.location for reference in references_by_repo[repo][:3])
            errors.append(
                f"{path.relative_to(REPO_ROOT).as_posix()}: generated action `{repo}` is missing "
                f"from `gh-aw-manifest`. References: {locations}."
            )

        for repo in sorted(manifest_repos & reference_repos):
            expected_sha = pins[repo]
            mismatches = [
                reference
                for reference in references_by_repo[repo]
                if reference.ref != expected_sha
            ]
            if not mismatches:
                continue

            actual_shas = ", ".join(sorted({reference.ref for reference in mismatches}))
            locations = ", ".join(reference.location for reference in mismatches[:3])
            more = f", +{len(mismatches) - 3} more" if len(mismatches) > 3 else ""
            errors.append(
                f"{path.relative_to(REPO_ROOT).as_posix()}: `{repo}` uses {actual_shas}, but its "
                f"`gh-aw-manifest` records {expected_sha}. Mismatches: {locations}{more}."
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
    manifest_pins: dict[Path, dict[str, str]] = {}
    for path in files:
        text = path.read_text(encoding="utf-8")
        file_manifest_pins, manifest_errors = collect_manifest_pins(path, text)
        file_references, file_errors = collect_references(path)
        has_manifest = any(MANIFEST_RE.match(line) for line in text.splitlines())
        if has_manifest:
            manifest_pins[path] = file_manifest_pins
        elif path.name.endswith(GENERATED_SUFFIX):
            manifest_errors.append(
                f"{path.relative_to(REPO_ROOT).as_posix()}: generated lock file has no `gh-aw-manifest` header."
            )
        references.extend(file_references)
        structural_errors.extend(manifest_errors)
        structural_errors.extend(file_errors)

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
    errors.extend(audit_manifest_consistency(references, manifest_pins))
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
