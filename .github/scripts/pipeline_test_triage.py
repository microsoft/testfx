#!/usr/bin/env python3

import argparse
import datetime
import json
import math
import os
import re
import shutil
import stat
import tempfile
import xml.etree.ElementTree as ET
import zipfile
from decimal import Decimal, InvalidOperation
from urllib.parse import urlencode, urlparse
from urllib.request import HTTPRedirectHandler, Request, build_opener

MAX_XML_REPORT_BYTES = 32 * 1024 * 1024
MAX_ARCHIVE_ENTRIES = 10_000
MAX_SELECTED_ARCHIVE_ENTRIES = 5_000
MAX_HISTORY_DOWNLOAD_BYTES = 512 * 1024 * 1024
MAX_HISTORY_ARTIFACT_BYTES = 256 * 1024 * 1024
MAX_HISTORY_EXTRACTED_BYTES = 1024 * 1024 * 1024
MAX_JSON_BYTES = 10 * 1024 * 1024
ALLOWED_AZURE_HOSTS = ("dev.azure.com", "artifacts.visualstudio.com")


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


def archive_stats(archive: str) -> tuple[int, int]:
    safe_count = 0
    selected_count = 0
    selected_size = 0
    with zipfile.ZipFile(archive) as zip_file:
        seen_names: set[str] = set()
        for entry in zip_file.infolist():
            normalized = normalized_entry_name(entry, seen_names)
            safe_count += 1
            if safe_count > MAX_ARCHIVE_ENTRIES:
                raise ValueError("Archive exceeds entry-count limit")
            if is_selected_artifact(normalized):
                selected_count += 1
                if selected_count > MAX_SELECTED_ARCHIVE_ENTRIES:
                    raise ValueError("Archive exceeds selected-entry limit")
                selected_size += entry.file_size
    return safe_count, selected_size


def inspect_archive(archive: str) -> None:
    try:
        safe_count, selected_size = archive_stats(archive)
    except ValueError:
        print("0\t1\t0")
        return
    except (OSError, zipfile.BadZipFile):
        raise SystemExit(1)

    print(f"{safe_count}\t0\t{selected_size}")


def extract_archive(archive: str, destination: str) -> None:
    destination_root = os.path.abspath(destination)
    try:
        with zipfile.ZipFile(archive) as zip_file:
            seen_names: set[str] = set()
            entry_count = 0
            selected_count = 0
            for entry in zip_file.infolist():
                normalized = normalized_entry_name(entry, seen_names)
                entry_count += 1
                if entry_count > MAX_ARCHIVE_ENTRIES:
                    raise ValueError("Archive exceeds entry-count limit")
                if entry.is_dir() or not is_selected_artifact(normalized):
                    continue
                selected_count += 1
                if selected_count > MAX_SELECTED_ARCHIVE_ENTRIES:
                    raise ValueError("Archive exceeds selected-entry limit")

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


def record_aliases(record: dict[str, object]) -> set[str]:
    aliases = set()
    name = record.get("name")
    if isinstance(name, str) and name:
        aliases.add(name.casefold())
    extra = record.get("extra")
    if isinstance(extra, dict):
        display_name = extra.get("displayName")
        if isinstance(display_name, str) and display_name:
            aliases.add(display_name.casefold())
    return aliases


def report_identity(source_file: str) -> str:
    normalized = source_file.replace("\\", "/")
    artifact, separator, relative = normalized.partition("/")
    if not separator:
        relative = artifact
    artifact = re.sub(r"_Attempt[0-9]+$", "", artifact, flags=re.IGNORECASE)
    lower = relative.lower()
    for suffix in (".ctrf.json", ".trx", ".xml"):
        if lower.endswith(suffix):
            relative = relative[: -len(suffix)]
            break
    return f"{artifact.casefold()}/{relative.casefold()}"


def deduplicate_records(records: list[dict[str, object]]) -> list[dict[str, object]]:
    deduplicated = []
    seen: set[tuple[str, str, str, str]] = set()
    names_by_artifact_and_format: dict[tuple[str, str], set[str]] = {}

    for record in records:
        name = record.get("name")
        source_file = record.get("sourceFile")
        report_format = record.get("reportFormat")
        if (
            not isinstance(name, str)
            or not name
            or not isinstance(source_file, str)
            or not isinstance(report_format, str)
        ):
            continue

        artifact = source_file.replace("\\", "/").split("/", 1)[0].casefold()
        aliases = record_aliases(record)
        if report_format == "TRX":
            higher_priority_aliases = names_by_artifact_and_format.get((artifact, "CTRF"), set())
        elif report_format == "JUnit":
            higher_priority_aliases = (
                names_by_artifact_and_format.get((artifact, "CTRF"), set())
                | names_by_artifact_and_format.get((artifact, "TRX"), set())
            )
        else:
            higher_priority_aliases = set()
        if aliases & higher_priority_aliases:
            continue

        extra = record.get("extra")
        execution_id = extra.get("executionId") if isinstance(extra, dict) else None
        discriminator = str(execution_id) if execution_id else ""
        key = (artifact, report_identity(source_file), name.casefold(), discriminator)
        if key in seen:
            continue

        seen.add(key)
        names_by_artifact_and_format.setdefault((artifact, report_format), set()).update(aliases)
        deduplicated.append(record)

    return deduplicated


def normalize_reports(ctrf_path: str, artifact_dir: str, output_path: str) -> None:
    results: list[dict[str, object]] = []

    def add_result(record: dict[str, object], _artifact: str) -> None:
        name = record.get("name")
        source_file = record.get("sourceFile")
        if not isinstance(name, str) or not name or not isinstance(source_file, str):
            return
        results.append(record)

    with open(ctrf_path, encoding="utf-8") as ctrf_file:
        for line in ctrf_file:
            if not line.strip():
                continue
            record = json.loads(line)
            add_result(record, str(record["sourceFile"]).split("/", 1)[0])

    report_paths = []
    for root, directories, files in os.walk(artifact_dir):
        directories[:] = sorted(directory for directory in directories if directory.casefold() != "merged")
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
        json.dump(deduplicate_records(results), output_file, separators=(",", ":"))


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


def is_allowed_azure_url(url: str) -> bool:
    parsed = urlparse(url)
    host = (parsed.hostname or "").lower()
    return (
        parsed.scheme == "https"
        and any(host == allowed or host.endswith(f".{allowed}") for allowed in ALLOWED_AZURE_HOSTS)
        and parsed.username is None
        and parsed.password is None
    )


class SafeAzureRedirectHandler(HTTPRedirectHandler):
    def redirect_request(self, request, file_pointer, code, message, headers, new_url):
        if not is_allowed_azure_url(new_url):
            raise ValueError(f"Refusing redirect to untrusted URL: {new_url}")
        return super().redirect_request(request, file_pointer, code, message, headers, new_url)


AZURE_OPENER = build_opener(SafeAzureRedirectHandler())


class ArtifactDownloadError(Exception):
    def __init__(self, message: str, downloaded_bytes: int):
        super().__init__(message)
        self.downloaded_bytes = downloaded_bytes


def open_azure_url(url: str):
    if not is_allowed_azure_url(url):
        raise ValueError(f"Refusing untrusted Azure DevOps URL: {url}")
    return AZURE_OPENER.open(Request(url, headers={"User-Agent": "testfx-pipeline-test-triage"}), timeout=60)


def fetch_json(url: str) -> dict[str, object]:
    with open_azure_url(url) as response:
        content_length = response.headers.get("Content-Length")
        if content_length and int(content_length) > MAX_JSON_BYTES:
            raise ValueError("JSON response exceeds size limit")
        payload = response.read(MAX_JSON_BYTES + 1)
    if len(payload) > MAX_JSON_BYTES:
        raise ValueError("JSON response exceeds size limit")
    document = json.loads(payload)
    if not isinstance(document, dict):
        raise ValueError("Expected a JSON object")
    return document


def download_artifact(url: str, destination: str, maximum_bytes: int) -> int:
    downloaded = 0
    try:
        with open_azure_url(url) as response, open(destination, "xb") as output:
            content_length = response.headers.get("Content-Length")
            if content_length and int(content_length) > maximum_bytes:
                raise ArtifactDownloadError("Artifact exceeds size limit", downloaded)
            while True:
                chunk = response.read(min(1024 * 1024, maximum_bytes - downloaded + 1))
                if not chunk:
                    break
                downloaded += len(chunk)
                if downloaded > maximum_bytes:
                    raise ArtifactDownloadError("Artifact exceeds size limit", downloaded)
                output.write(chunk)
    except ArtifactDownloadError:
        raise
    except (OSError, ValueError) as error:
        raise ArtifactDownloadError(str(error), downloaded) from error
    return downloaded


def records_from_archive(archive: str, artifact: str) -> list[dict[str, object]]:
    records: list[dict[str, object]] = []

    def add_result(record: dict[str, object], _artifact: str) -> None:
        records.append(record)

    with zipfile.ZipFile(archive) as zip_file:
        report_entries = [
            entry
            for entry in zip_file.infolist()
            if not entry.is_dir() and is_selected_artifact(entry.filename)
        ]
        individual_ctrf = [
            entry
            for entry in report_entries
            if entry.filename.lower().endswith(".ctrf.json")
            and "/merged/" not in entry.filename.replace("\\", "/").lower()
        ]
        ctrf_entries = individual_ctrf or [
            entry for entry in report_entries if entry.filename.lower().endswith(".ctrf.json")
        ]

        for entry in ctrf_entries:
            if entry.file_size > MAX_XML_REPORT_BYTES:
                continue
            try:
                document = json.loads(zip_file.read(entry))
            except (json.JSONDecodeError, UnicodeDecodeError):
                continue
            if not isinstance(document, dict):
                continue
            normalized_name = entry.filename.replace("\\", "/")
            source_file = f"{artifact}/{normalized_name}"
            ctrf_results = document.get("results")
            if not isinstance(ctrf_results, dict):
                continue
            tests = ctrf_results.get("tests")
            if not isinstance(tests, list):
                continue
            for test in tests:
                if not isinstance(test, dict):
                    continue
                duration = test.get("duration")
                if not isinstance(duration, (int, float)) or not math.isfinite(duration):
                    duration = None
                retry_attempts = test.get("retryAttempts")
                if not isinstance(retry_attempts, list):
                    retry_attempts = []
                records.append(
                    {
                        "sourceFile": source_file,
                        "reportFormat": "CTRF",
                        "name": test.get("name"),
                        "status": test.get("status"),
                        "duration": duration,
                        "message": test.get("message"),
                        "trace": test.get("trace"),
                        "flaky": test.get("flaky") is True,
                        "retryAttempts": retry_attempts,
                        "extra": test.get("extra"),
                    }
                )

        all_xml_entries = [
            entry
            for entry in report_entries
            if entry.filename.lower().endswith((".trx", ".xml"))
            and entry.file_size <= MAX_XML_REPORT_BYTES
        ]
        individual_xml_entries = [
            entry
            for entry in all_xml_entries
            if "/merged/" not in entry.filename.replace("\\", "/").lower()
        ]
        xml_entries = sorted(
            individual_xml_entries or all_xml_entries,
            key=lambda entry: (not entry.filename.lower().endswith(".trx"), entry.filename),
        )
        for entry in xml_entries:
            try:
                document = ET.fromstring(zip_file.read(entry))
            except (ET.ParseError, UnicodeDecodeError):
                continue
            normalized_name = entry.filename.replace("\\", "/")
            source_file = f"{artifact}/{normalized_name}"
            if local_name(document.tag) == "TestRun":
                normalize_trx(document, source_file, artifact, add_result)
            elif local_name(document.tag) in ("testsuite", "testsuites"):
                normalize_junit(document, source_file, artifact, add_result)

    return deduplicate_records(records)


def find_slow_regressions(
    current_results: list[dict[str, object]],
    historical_results: list[dict[str, object]],
) -> list[dict[str, object]]:
    regressions = []
    for current in current_results:
        current_duration = current.get("duration")
        current_source = current.get("sourceFile")
        if (
            not isinstance(current_duration, (int, float))
            or current_duration < 60000
            or not isinstance(current_source, str)
        ):
            continue

        aliases = record_aliases(current)
        identity = report_identity(current_source)
        durations = sorted(
            duration
            for result in historical_results
            if record_aliases(result) & aliases
            and isinstance(result.get("sourceFile"), str)
            and report_identity(result["sourceFile"]) == identity
            and isinstance((duration := result.get("duration")), (int, float))
            and duration > 0
        )
        if len(durations) < 10:
            continue

        percentile_index = math.ceil(len(durations) * 0.95) - 1
        p95 = durations[percentile_index]
        if current_duration >= 3 * p95:
            regressions.append(
                {
                    "name": current.get("name"),
                    "sourceFile": current_source,
                    "currentDurationMs": current_duration,
                    "historicalP95Ms": p95,
                    "sampleCount": len(durations),
                }
            )
    return regressions


def collect_history(
    api_base: str,
    definition_id: str,
    source_branch: str,
    current_build_id: str,
    current_results_path: str,
    output_path: str,
) -> None:
    with open(current_results_path, encoding="utf-8") as current_file:
        current_results = json.load(current_file)

    candidate_names: set[str] = set()
    for result in current_results:
        if not isinstance(result, dict):
            continue
        status = result.get("status")
        duration = result.get("duration")
        is_candidate = (
            status in ("failed", "other")
            or result.get("flaky") is True
            or bool(result.get("retryAttempts"))
            or isinstance(duration, (int, float)) and duration >= 60000
        )
        if not is_candidate:
            continue
        extra = result.get("extra")
        display_name = extra.get("displayName") if isinstance(extra, dict) else None
        for name in (result.get("name"), display_name):
            if isinstance(name, str) and name:
                candidate_names.add(name.casefold())

    history = {
        "branch": "refs/heads/main" if source_branch.startswith("refs/pull/") else source_branch,
        "candidateNames": sorted(candidate_names),
        "builds": [],
        "incomplete": False,
        "downloadedBytes": 0,
        "selectedUncompressedBytes": 0,
        "slowRegressions": [],
    }
    if not candidate_names:
        with open(output_path, "w", encoding="utf-8") as output_file:
            json.dump(history, output_file, separators=(",", ":"))
        return

    minimum_time = (datetime.datetime.now(datetime.UTC) - datetime.timedelta(days=30)).isoformat()
    query = urlencode(
        {
            "definitions": definition_id,
            "branchName": history["branch"],
            "statusFilter": "completed",
            "queryOrder": "finishTimeDescending",
            "minTime": minimum_time,
            "$top": "100",
            "api-version": "7.1",
        }
    )

    try:
        builds_payload = fetch_json(f"{api_base.rstrip('/')}/build/builds?{query}")
    except (OSError, ValueError, json.JSONDecodeError):
        history["incomplete"] = True
        builds_payload = {"value": []}

    with tempfile.TemporaryDirectory(prefix="testfx-triage-history-") as temporary_directory:
        for build in builds_payload.get("value", []):
            if len(history["builds"]) >= 12:
                break
            build_id = str(build.get("id", ""))
            if not build_id.isdigit() or build_id == current_build_id:
                continue

            build_record = {
                "id": build_id,
                "buildNumber": build.get("buildNumber"),
                "result": build.get("result"),
                "sourceVersion": build.get("sourceVersion"),
                "finishTime": build.get("finishTime"),
                "results": [],
            }
            try:
                artifacts_payload = fetch_json(
                    f"{api_base.rstrip('/')}/build/builds/{build_id}/artifacts?api-version=7.1"
                )
            except (OSError, ValueError, json.JSONDecodeError):
                history["incomplete"] = True
                history["builds"].append(build_record)
                continue

            all_artifacts = sorted(
                [
                    artifact
                    for artifact in artifacts_payload.get("value", [])
                    if re.match(
                        r"^(TestResults_|Windows_App_Model_Diagnostics_)",
                        str(artifact.get("name", "")),
                        re.IGNORECASE,
                    )
                ],
                key=lambda artifact: str(artifact.get("name", "")),
            )
            if len(all_artifacts) > 8:
                history["incomplete"] = True
            artifacts = all_artifacts[:8]
            if not artifacts:
                continue

            for index, artifact in enumerate(artifacts):
                remaining_download = MAX_HISTORY_DOWNLOAD_BYTES - int(history["downloadedBytes"])
                if remaining_download <= 0:
                    history["incomplete"] = True
                    break
                maximum_download = min(MAX_HISTORY_ARTIFACT_BYTES, remaining_download)
                archive = os.path.join(temporary_directory, f"{build_id}-{index}.zip")
                try:
                    downloaded = download_artifact(
                        str(artifact.get("resource", {}).get("downloadUrl", "")),
                        archive,
                        maximum_download,
                    )
                    history["downloadedBytes"] = int(history["downloadedBytes"]) + downloaded
                    _, selected_size = archive_stats(archive)
                    if (
                        int(history["selectedUncompressedBytes"]) + selected_size
                        > MAX_HISTORY_EXTRACTED_BYTES
                    ):
                        history["incomplete"] = True
                        os.remove(archive)
                        break
                    history["selectedUncompressedBytes"] = (
                        int(history["selectedUncompressedBytes"]) + selected_size
                    )
                    records = records_from_archive(archive, str(artifact.get("name", "")))
                except ArtifactDownloadError as error:
                    history["downloadedBytes"] = min(
                        MAX_HISTORY_DOWNLOAD_BYTES,
                        int(history["downloadedBytes"]) + error.downloaded_bytes,
                    )
                    history["incomplete"] = True
                    continue
                except (
                    OSError,
                    RuntimeError,
                    ValueError,
                    zipfile.BadZipFile,
                ):
                    history["incomplete"] = True
                    continue
                finally:
                    if os.path.exists(archive):
                        os.remove(archive)

                for record in records:
                    aliases = [record.get("name")]
                    extra = record.get("extra")
                    if isinstance(extra, dict):
                        aliases.append(extra.get("displayName"))
                    if any(
                        isinstance(alias, str) and alias.casefold() in candidate_names
                        for alias in aliases
                    ):
                        build_record["results"].append(record)
                        if len(build_record["results"]) >= 500:
                            history["incomplete"] = True
                            break

            history["builds"].append(build_record)

    historical_results = [
        result
        for build in history["builds"]
        for result in build["results"]
        if isinstance(result, dict)
    ]
    history["slowRegressions"] = find_slow_regressions(
        [result for result in current_results if isinstance(result, dict)],
        historical_results,
    )

    with open(output_path, "w", encoding="utf-8") as output_file:
        json.dump(history, output_file, separators=(",", ":"))


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

    download_parser = subparsers.add_parser("download")
    download_parser.add_argument("url")
    download_parser.add_argument("destination")
    download_parser.add_argument("maximum_bytes", type=int)

    history_parser = subparsers.add_parser("history")
    history_parser.add_argument("api_base")
    history_parser.add_argument("definition_id")
    history_parser.add_argument("source_branch")
    history_parser.add_argument("current_build_id")
    history_parser.add_argument("current_results")
    history_parser.add_argument("output")

    args = parser.parse_args()
    if args.command == "inspect":
        inspect_archive(args.archive)
    elif args.command == "extract":
        extract_archive(args.archive, args.destination)
    elif args.command == "normalize":
        normalize_reports(args.ctrf_ndjson, args.artifact_dir, args.output)
    elif args.command == "download":
        try:
            downloaded = download_artifact(args.url, args.destination, args.maximum_bytes)
        except ArtifactDownloadError as error:
            print(error.downloaded_bytes)
            raise SystemExit(1)
        print(downloaded)
    else:
        collect_history(
            args.api_base,
            args.definition_id,
            args.source_branch,
            args.current_build_id,
            args.current_results,
            args.output,
        )


if __name__ == "__main__":
    main()
