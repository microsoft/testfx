#!/usr/bin/env python3

import argparse
import json
import os
import re
import shutil
import stat
import sys
import xml.etree.ElementTree as ET
import zipfile
from decimal import Decimal, InvalidOperation

MAX_XML_REPORT_BYTES = 32 * 1024 * 1024


def is_selected_artifact(path: str) -> bool:
    lower = path.lower()
    return (
        lower.endswith(".ctrf.json")
        or lower.endswith(".trx")
        or lower.endswith(".xml")
        or lower.endswith(".dmp")
        or lower.endswith(".core")
        or (lower.endswith(".json") and "crash" in lower)
        or (lower.endswith((".log", ".txt")) and "sequence" in lower)
    )


def normalized_entry_name(entry: zipfile.ZipInfo, seen_names: set[str]) -> str:
    normalized = entry.filename.replace("\\", "/")
    parts = [part for part in normalized.split("/") if part not in ("", ".")]
    is_unsafe = (
        normalized.startswith("/")
        or re.match(r"^[A-Za-z]:", normalized) is not None
        or ".." in parts
        or any(character in normalized for character in ("\0", "\r", "\n", "\t"))
        or normalized in seen_names
        or stat.S_ISLNK(entry.external_attr >> 16)
        or bool(entry.flag_bits & 0x1)
    )
    if is_unsafe:
        raise ValueError(f"Unsafe archive entry: {entry.filename!r}")

    seen_names.add(normalized)
    return normalized


def inspect_archive(archive: str) -> None:
    safe_count = 0
    selected_size = 0
    try:
        with zipfile.ZipFile(archive) as zip_file:
            seen_names: set[str] = set()
            for entry in zip_file.infolist():
                try:
                    normalized = normalized_entry_name(entry, seen_names)
                except ValueError:
                    print(f"{safe_count}\t1\t{selected_size}")
                    return

                safe_count += 1
                if is_selected_artifact(normalized):
                    selected_size += entry.file_size
    except (OSError, zipfile.BadZipFile):
        raise SystemExit(1)

    print(f"{safe_count}\t0\t{selected_size}")


def extract_archive(archive: str, destination: str) -> None:
    destination_root = os.path.abspath(destination)
    try:
        with zipfile.ZipFile(archive) as zip_file:
            seen_names: set[str] = set()
            for entry in zip_file.infolist():
                normalized = normalized_entry_name(entry, seen_names)
                if entry.is_dir() or not is_selected_artifact(normalized):
                    continue

                parts = [part for part in normalized.split("/") if part not in ("", ".")]
                target = os.path.abspath(os.path.join(destination_root, *parts))
                if os.path.commonpath((destination_root, target)) != destination_root:
                    raise ValueError(f"Archive entry escapes destination: {entry.filename!r}")

                os.makedirs(os.path.dirname(target), exist_ok=True)
                with zip_file.open(entry) as source, open(target, "xb") as output:
                    shutil.copyfileobj(source, output, length=1024 * 1024)
    except (OSError, RuntimeError, ValueError, zipfile.BadZipFile):
        raise SystemExit(1)


def artifact_name(path: str, artifact_dir: str) -> str:
    relative = os.path.relpath(path, artifact_dir).replace("\\", "/")
    return relative.split("/", 1)[0]


def local_name(tag: str) -> str:
    return tag.rsplit("}", 1)[-1]


def duration_from_trx(value: str | None) -> int | None:
    if not value:
        return None
    try:
        days = 0
        if "." in value.split(":", 1)[0]:
            day_text, value = value.split(".", 1)
            days = int(day_text)
        hours, minutes, seconds = value.split(":")
        total_seconds = (
            Decimal(days * 86400)
            + Decimal(hours) * 3600
            + Decimal(minutes) * 60
            + Decimal(seconds)
        )
        return int(total_seconds * 1000) if total_seconds.is_finite() else None
    except (InvalidOperation, OverflowError, ValueError):
        return None


def duration_from_junit(value: str | None) -> int | None:
    if not value:
        return None
    try:
        seconds = Decimal(value)
        return int(seconds * 1000) if seconds.is_finite() else None
    except (InvalidOperation, OverflowError, ValueError):
        return None


def first_child(element: ET.Element | None, name: str) -> ET.Element | None:
    if element is None:
        return None
    return next((child for child in element if local_name(child.tag) == name), None)


def normalize_reports(ctrf_path: str, artifact_dir: str, output_path: str) -> None:
    results: list[dict[str, object]] = []
    seen: set[tuple[str, str]] = set()

    def report_identity(source_file: str) -> str:
        relative = source_file.replace("\\", "/").split("/", 1)[-1]
        lower = relative.lower()
        for suffix in (".ctrf.json", ".trx", ".xml"):
            if lower.endswith(suffix):
                return relative[: -len(suffix)].casefold()
        return relative.casefold()

    def add_result(record: dict[str, object], artifact: str) -> None:
        name = record.get("name")
        source_file = record.get("sourceFile")
        if not isinstance(name, str) or not name or not isinstance(source_file, str):
            return
        key = (artifact.casefold(), report_identity(source_file), name.casefold())
        if key in seen:
            return
        seen.add(key)
        results.append(record)

    with open(ctrf_path, encoding="utf-8") as ctrf_file:
        for line in ctrf_file:
            if not line.strip():
                continue
            record = json.loads(line)
            add_result(record, str(record["sourceFile"]).split("/", 1)[0])

    report_paths = []
    for root, directories, files in os.walk(artifact_dir):
        directories.sort()
        for file_name in sorted(files):
            lower_name = file_name.lower()
            if lower_name.endswith(".trx") or lower_name.endswith(".xml"):
                report_paths.append(os.path.join(root, file_name))
    report_paths.sort(key=lambda path: (not path.lower().endswith(".trx"), path))

    for path in report_paths:
        if os.path.getsize(path) > MAX_XML_REPORT_BYTES:
            continue
        try:
            document = ET.parse(path).getroot()
        except (ET.ParseError, OSError):
            continue

        source_file = os.path.relpath(path, artifact_dir).replace("\\", "/")
        artifact = artifact_name(path, artifact_dir)

        if local_name(document.tag) == "TestRun":
            normalize_trx(document, source_file, artifact, add_result)
        elif local_name(document.tag) in ("testsuite", "testsuites"):
            normalize_junit(document, source_file, artifact, add_result)

    with open(output_path, "w", encoding="utf-8") as output_file:
        json.dump(results, output_file, separators=(",", ":"))


def normalize_trx(
    document: ET.Element,
    source_file: str,
    artifact: str,
    add_result,
) -> None:
    definitions: dict[str, str] = {}
    for element in document.iter():
        if local_name(element.tag) != "UnitTest":
            continue
        method = next(
            (child for child in element.iter() if local_name(child.tag) == "TestMethod"),
            None,
        )
        if method is None:
            continue
        class_name = method.attrib.get("className", "")
        method_name = method.attrib.get("name", "")
        definitions[element.attrib.get("id", "")] = f"{class_name}.{method_name}".strip(".")

    status_map = {
        "passed": "passed",
        "failed": "failed",
        "notexecuted": "skipped",
        "not executed": "skipped",
        "skipped": "skipped",
    }
    for element in document.iter():
        if local_name(element.tag) != "UnitTestResult":
            continue
        error_info = first_child(first_child(element, "Output"), "ErrorInfo")
        message_element = first_child(error_info, "Message")
        trace_element = first_child(error_info, "StackTrace")
        outcome = element.attrib.get("outcome", "Other")
        name = definitions.get(
            element.attrib.get("testId", ""),
            element.attrib.get("testName", ""),
        )
        add_result(
            {
                "sourceFile": source_file,
                "reportFormat": "TRX",
                "name": name,
                "status": status_map.get(outcome.casefold(), "other"),
                "duration": duration_from_trx(element.attrib.get("duration")),
                "message": message_element.text if message_element is not None else None,
                "trace": trace_element.text if trace_element is not None else None,
                "flaky": None,
                "retryAttempts": None,
                "extra": {
                    "displayName": element.attrib.get("testName"),
                    "outcome": outcome,
                    "executionId": element.attrib.get("executionId"),
                },
            },
            artifact,
        )


def normalize_junit(
    document: ET.Element,
    source_file: str,
    artifact: str,
    add_result,
) -> None:
    for element in document.iter():
        if local_name(element.tag) != "testcase":
            continue
        class_name = element.attrib.get("classname", "")
        test_name = element.attrib.get("name", "")
        name = f"{class_name}.{test_name}".strip(".")
        failure = next(
            (
                child
                for child in element
                if local_name(child.tag) in ("failure", "error")
            ),
            None,
        )
        skipped = first_child(element, "skipped")
        status = "failed" if failure is not None else "skipped" if skipped is not None else "passed"
        add_result(
            {
                "sourceFile": source_file,
                "reportFormat": "JUnit",
                "name": name,
                "status": status,
                "duration": duration_from_junit(element.attrib.get("time")),
                "message": failure.attrib.get("message") if failure is not None else None,
                "trace": failure.text if failure is not None else None,
                "flaky": None,
                "retryAttempts": None,
                "extra": {
                    "displayName": test_name,
                    "className": class_name,
                },
            },
            artifact,
        )


def main() -> None:
    parser = argparse.ArgumentParser()
    subparsers = parser.add_subparsers(dest="command", required=True)

    inspect_parser = subparsers.add_parser("inspect")
    inspect_parser.add_argument("archive")

    extract_parser = subparsers.add_parser("extract")
    extract_parser.add_argument("archive")
    extract_parser.add_argument("destination")

    normalize_parser = subparsers.add_parser("normalize")
    normalize_parser.add_argument("ctrf_ndjson")
    normalize_parser.add_argument("artifact_dir")
    normalize_parser.add_argument("output")

    args = parser.parse_args()
    if args.command == "inspect":
        inspect_archive(args.archive)
    elif args.command == "extract":
        extract_archive(args.archive, args.destination)
    else:
        normalize_reports(args.ctrf_ndjson, args.artifact_dir, args.output)


if __name__ == "__main__":
    main()
