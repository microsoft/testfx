#!/usr/bin/env python3

"""Validate TestFx's generated RESX/XLIFF catalogs without modifying them."""

import argparse
import collections
import pathlib
import re
import subprocess
import sys
import xml.etree.ElementTree as ET
from typing import NamedTuple


XLIFF = "{urn:oasis:names:tc:xliff:document:1.2}"
LOCKED = re.compile(r'\{Locked="([^"]*)"\}')
FORMAT_ITEM = re.compile(r"\{([0-9]+) *(?:, *-?[0-9]+ *)?(?::[^{}]*)?\}")
COMPLETED_STATES = {"translated", "final", "signed-off"}


class Finding(NamedTuple):
    code: str
    resource: str
    unit: str
    detail: str
    catalog: str = ""


def format_indices(value: str) -> set[int] | None:
    """Recognize composite formats, not named templates, JSON or literal braces."""
    indices = set()
    position = 0
    while position < len(value):
        if value[position:position + 2] in ("{{", "}}"):
            position += 2
        elif value[position] == "{":
            match = FORMAT_ITEM.match(value, position)
            if match is None:
                return None
            indices.add(int(match[1]))
            position = match.end()
        elif value[position] == "}":
            return None
        else:
            position += 1
    return indices


def catalog_paths(repository: pathlib.Path) -> list[pathlib.Path]:
    result = subprocess.run(
        ["git", "ls-files", "-z", "--cached", "--others", "--exclude-standard", "--", "*.xlf"],
        cwd=repository,
        check=True,
        stdout=subprocess.PIPE,
    )
    return sorted({
        repository / path.decode("utf-8")
        for path in result.stdout.split(b"\0")
        if path
    })


def validate(repository: pathlib.Path, catalogs: list[pathlib.Path]) -> list[Finding]:
    repository = repository.resolve()
    findings = []
    resources = {}

    def relative(path: pathlib.Path) -> str:
        return path.relative_to(repository).as_posix()

    def load_xml(path: pathlib.Path) -> ET.Element | None:
        try:
            return ET.parse(path).getroot()
        except (ET.ParseError, OSError) as error:
            findings.append(Finding("xml", relative(path), "", str(error)))
            return None

    def load_resources(path: pathlib.Path) -> dict[str, tuple[str, str]] | None:
        if path in resources:
            return resources[path]
        entries = {}
        resources[path] = None
        root = load_xml(path)
        if root is None:
            return None
        if root.tag != "root":
            findings.append(Finding("resx-root", relative(path), "", "expected a RESX root element"))
            return None
        resources[path] = entries
        names = set()
        for data in root.findall("data"):
            name = data.get("name", "")
            if not name or name in names:
                findings.append(Finding("resource-id", relative(path), name, "missing or duplicate data name"))
            names.add(name)
            # Match XliffTasks ResxDocument.GetTranslatableNodes exclusions.
            if "mimetype" in data.attrib or "type" in data.attrib:
                continue
            values = data.findall("value")
            if len(values) != 1:
                findings.append(Finding("resource-value", relative(path), name, "expected one value element"))
                continue
            value = values[0].text or ""
            comment = data.findtext("comment", "")
            if (
                name.startswith(">>") or name.endswith(".LayoutSettings")
                or comment == "{Locked}" or not value.strip()
            ):
                continue
            entries[name] = (value, comment)
            for token in sorted(set(LOCKED.findall(comment))):
                if not token or token not in value:
                    findings.append(Finding(
                        "locked-source", relative(path), name,
                        f"locked token {token!r} must occur verbatim in the neutral value",
                    ))
        return entries

    for path in sorted(set(catalogs)):
        path = path.resolve()
        catalog = relative(path)
        root = load_xml(path)
        if root is None:
            continue
        if root.tag != XLIFF + "xliff" or root.get("version") != "1.2":
            findings.append(Finding("xliff-root", catalog, "", "expected XLIFF namespace and version 1.2"))
            continue
        files = root.findall(XLIFF + "file")
        if not files:
            findings.append(Finding("xliff-file", catalog, "", "expected at least one file element"))
        for file in files:
            original = file.get("original", "")
            neutral = (path.parent / original.replace("\\", "/")).resolve()
            if not original or not neutral.is_relative_to(repository) or neutral.suffix != ".resx":
                findings.append(Finding(
                    "original", catalog, "", "file original must resolve to a RESX inside the repository",
                ))
                continue
            resource = relative(neutral)
            entries = load_resources(neutral)

            def report(code: str, unit: str, detail: str) -> None:
                findings.append(Finding(code, resource, unit, detail, catalog))

            bodies = file.findall(XLIFF + "body")
            if len(bodies) != 1:
                report("xliff-body", "", "expected one body element")
                continue
            body = bodies[0]
            units = list(body.iter(XLIFF + "trans-unit"))
            # XLIFF 1.2's K_unit_id covers trans-unit and bin-unit within each file.
            ids = collections.Counter(
                unit.get("id", "")
                for unit in body.iter()
                if unit.tag in (XLIFF + "trans-unit", XLIFF + "bin-unit")
            )
            for name, count in sorted(ids.items()):
                if not name:
                    report("unit-id", name, "unit id is required")
                elif count > 1:
                    report("duplicate-unit", name, f"unit id occurs {count} times in one file")
            seen = set()
            for unit in units:
                name = unit.get("id", "")
                seen.add(name)
                if entries is not None and name not in entries:
                    report("orphan-unit", name, "unit id is absent from the neutral string resources")
                sources = unit.findall(XLIFF + "source")
                targets = unit.findall(XLIFF + "target")
                if len(sources) != 1:
                    report("source-count", name, f"expected one direct source; found {len(sources)}")
                if len(targets) > 1:
                    report("duplicate-target", name, f"expected zero or one direct target; found {len(targets)}")
                if entries is None or name not in entries or len(sources) != 1:
                    continue
                value, comment = entries[name]
                source = sources[0]
                notes = unit.findall(XLIFF + "note")
                # Inline XLIFF codes need producer-specific rendering, not itertext().
                if len(source) or any(len(target) for target in targets):
                    report("inline-content", name, "inline codes require manual content validation; not supported by this RESX guard")
                    continue
                if (source.text or "") != value:
                    report("stale-source", name, "source differs from the neutral value; regenerate with UpdateXlf")
                # Multiple notes are legal. OneLoc/UpdateXlf emits the RESX comment in one note.
                if not any((note.text or "") == comment for note in notes) and (comment or notes):
                    report("stale-note", name, "no note matches the neutral comment; regenerate with UpdateXlf")
                if len(targets) != 1:
                    continue
                target = targets[0]
                text = target.text or ""
                if not text and target.get("state") not in COMPLETED_STATES:
                    continue
                expected = format_indices(value)
                actual = format_indices(text)
                if expected is not None:
                    if actual is None and expected:
                        report("target-format", name, "target has invalid composite-format braces")
                    elif actual is not None and expected != actual:
                        report("placeholder-indices", name, f"expected format indices {sorted(expected)}; found {sorted(actual)}")
                for token in sorted(set(LOCKED.findall(comment))):
                    if token and token in value and token not in text:
                        report("locked-target", name, f"locked token {token!r} is missing from the target")
            if entries is not None:
                for name in sorted(entries.keys() - seen):
                    report("missing-unit", name, "neutral string resource has no unit; regenerate with UpdateXlf")
    return sorted(set(findings))


def print_findings(findings: list[Finding]) -> None:
    groups = collections.defaultdict(set)
    for finding in findings:
        groups[finding[:4]].add(finding.catalog)
    for (code, resource, unit, detail), catalogs in sorted(groups.items()):
        identity = f" :: {unit}" if unit else ""
        print(f"[{code}] {resource}{identity}: {detail}")
        paths = sorted(catalog for catalog in catalogs if catalog)
        if paths:
            print(f"  catalogs ({len(paths)}):")
            for path in paths:
                print(f"    {path}")
    print(f"{len(findings)} finding(s) in {len(groups)} root-cause group(s).")


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument(
        "--repository", type=pathlib.Path, default=pathlib.Path(__file__).resolve().parents[2],
        help="repository to check (defaults to the script's repository)",
    )
    args = parser.parse_args(argv)
    try:
        paths = catalog_paths(args.repository)
    except (OSError, subprocess.CalledProcessError) as error:
        print(f"Cannot enumerate localization catalogs: {error}", file=sys.stderr)
        return 2
    if not paths:
        print("No localization catalogs found; refusing to report a clean check.", file=sys.stderr)
        return 2
    findings = validate(args.repository, paths)
    print(f"Checked {len(paths)} localization catalog(s).")
    if findings:
        print_findings(findings)
        return 1
    print("Localization structure, neutral correspondence, placeholders and locked tokens are valid.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
