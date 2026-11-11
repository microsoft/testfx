#!/usr/bin/env python3

"""Build an offline cost/coverage snapshot from retained TestFx OTel archives."""

import argparse
import collections
import hashlib
import html
import json
import math
import re
import statistics
import sys
import zipfile
from datetime import datetime, timezone
from pathlib import Path


TICKS_PER_MS = 10_000
MAX_FILE_BYTES = 256 * 1024 * 1024
MAX_TOTAL_BYTES = 512 * 1024 * 1024
MAX_LINE_BYTES = 2 * 1024 * 1024
MAX_FILES = 2_000
ZERO_SPAN = "0000000000000000"
FIXTURE_NAMES = {
    "MSTest.AssemblyInitialize",
    "MSTest.AssemblyCleanup",
    "MSTest.ClassInitialize",
    "MSTest.ClassCleanup",
    "MSTest.TestInitialize",
    "MSTest.TestCleanup",
}
ENVIRONMENT_KEYS = (
    "test.assembly.name",
    "testfx.module.mvid",
    "testfx.target.framework",
    "testfx.build.configuration",
    "os.type",
    "os.description",
    "process.arch",
    "process.runtime.description",
    "testfx.sdk.version",
    "host.cpu.logical_count",
    "testfx.ci.image.name",
    "testfx.ci.image.version",
    "testfx.trace.collection",
)
TIMESTAMP = re.compile(
    r"(\d{4}-\d\d-\d\dT\d\d:\d\d:\d\d)(?:\.(\d{1,7}))?Z"
)


def attributes(record: dict) -> dict:
    items = record.get("attributes", [])
    if not isinstance(items, list):
        raise ValueError("attributes must be an array")
    result = {}
    for item in items:
        if not isinstance(item, dict) or not isinstance(item.get("key"), str):
            raise ValueError("invalid attribute entry")
        if item["key"] in result:
            raise ValueError(f"duplicate attribute: {item['key']}")
        result[item["key"]] = item.get("value")
    return result


def timestamp_ticks(value: str) -> int:
    match = TIMESTAMP.fullmatch(value) if isinstance(value, str) else None
    if match is None:
        raise ValueError("expected an exact UTC timestamp")
    second = datetime.strptime(match[1], "%Y-%m-%dT%H:%M:%S").replace(
        tzinfo=timezone.utc
    )
    epoch = datetime(1970, 1, 1, tzinfo=timezone.utc)
    elapsed = second - epoch
    return (
        (elapsed.days * 86400 + elapsed.seconds) * 10_000_000
        + int((match[2] or "").ljust(7, "0"))
    )


def duration_ms(value) -> float:
    if (
        not isinstance(value, (int, float))
        or isinstance(value, bool)
        or not math.isfinite(value)
        or value < 0
    ):
        raise ValueError("duration must be a nonnegative finite number")
    return value


def interval_union(intervals: list[tuple[int, int]]) -> int:
    end = None
    total = 0
    for start, stop in sorted(intervals):
        if end is None or start >= end:
            total += stop - start
        elif stop > end:
            total += stop - end
        end = stop if end is None else max(end, stop)
    return total


def distribution(values: list[float]) -> dict:
    if not values:
        return {"count": 0, "sumMs": 0, "p50Ms": None, "p95Ms": None, "maxMs": None}
    ordered = sorted(values)
    return {
        "count": len(values),
        "sumMs": math.fsum(values),
        "p50Ms": statistics.median(ordered),
        "p95Ms": ordered[math.ceil(0.95 * len(ordered)) - 1],
        "maxMs": ordered[-1],
    }


def trace_inputs(paths: list[Path]):
    for path in paths:
        if path.is_dir():
            yield from trace_inputs(sorted(path.rglob("*.jsonl")))
        elif path.suffix.lower() == ".zip":
            with zipfile.ZipFile(path) as archive:
                entries = archive.infolist()
                if len(entries) > 20_000:
                    raise ValueError(f"{path.name}: too many ZIP entries")
                traces = [
                    entry for entry in entries if entry.filename.endswith(".jsonl")
                ]
                scoped = [
                    entry
                    for entry in traces
                    if "OpenTelemetry" in entry.filename.replace("\\", "/").split("/")
                ]
                for entry in scoped or traces:
                    if entry.file_size > MAX_FILE_BYTES:
                        raise ValueError(f"{entry.filename}: trace file is too large")
                    with archive.open(entry) as stream:
                        yield f"{path.name}!{entry.filename}", stream, entry.file_size
        else:
            if not path.is_file():
                raise ValueError(f"input does not exist: {path}")
            if path.stat().st_size > MAX_FILE_BYTES:
                raise ValueError(f"{path.name}: trace file is too large")
            with path.open("rb") as stream:
                yield path.name, stream, path.stat().st_size


def artifact_root_parts(members: list[tuple[str, ...]]) -> tuple[str, ...]:
    if not members:
        return ()
    wrapper = members[0][0]
    if wrapper in ("OpenTelemetry", "merged") or any(
        len(parts) == 1 or parts[0] != wrapper for parts in members
    ):
        return ()
    return (wrapper,)


def published_results(paths: list[Path]) -> dict | None:
    seen = set()
    statuses = collections.Counter()
    count = 0
    input_bytes = 0

    def consume(stream):
        nonlocal count, input_bytes
        data = stream.read(32 * 1024 * 1024 + 1)
        if len(data) > 32 * 1024 * 1024:
            raise ValueError("published CTRF report exceeds the size limit")
        input_bytes += len(data)
        if input_bytes > MAX_TOTAL_BYTES or len(seen) >= MAX_FILES:
            raise ValueError("published CTRF inputs exceed the total limit")
        digest = hashlib.sha256(data).hexdigest()
        if digest in seen:
            return
        document = json.loads(data)
        results = document.get("results") if isinstance(document, dict) else None
        tests = results.get("tests") if isinstance(results, dict) else None
        if not isinstance(tests, list) or any(
            not isinstance(test, dict) or not isinstance(test.get("status"), str)
            for test in tests
        ):
            raise ValueError("invalid published CTRF test records")
        seen.add(digest)
        count += len(tests)
        statuses.update(test["status"] for test in tests)

    for path in paths:
        if path.suffix.lower() == ".zip":
            with zipfile.ZipFile(path) as archive:
                members = [
                    (tuple(entry.filename.replace("\\", "/").split("/")), entry)
                    for entry in archive.infolist() if not entry.is_dir()
                ]
                root = artifact_root_parts([parts for parts, _ in members])
                for parts, entry in members:
                    if parts[:-1] == root and parts[-1].endswith(".ctrf.json"):
                        with archive.open(entry) as stream:
                            consume(stream)
        elif path.is_dir():
            members = [
                (result.relative_to(path).parts, result)
                for result in sorted(path.rglob("*")) if result.is_file()
            ]
            root = artifact_root_parts([parts for parts, _ in members])
            for parts, result in members:
                if parts[:-1] == root and parts[-1].endswith(".ctrf.json"):
                    with result.open("rb") as stream:
                        consume(stream)
    if not seen:
        return None
    return {"reports": len(seen), "tests": count, "statuses": dict(statuses)}


def load_archive(stream) -> dict:
    resource = None
    completion = None
    spans = {}
    digest = hashlib.sha256()
    size = 0
    for line_number, line in enumerate(iter(lambda: stream.readline(MAX_LINE_BYTES + 1), b""), 1):
        size += len(line)
        if len(line) > MAX_LINE_BYTES or size > MAX_FILE_BYTES:
            raise ValueError("trace exceeds the record/file size limit")
        digest.update(line)
        record = json.loads(line)
        if not isinstance(record, dict):
            raise ValueError(f"line {line_number}: expected an object")
        kind = record.get("recordType")
        if line_number == 1:
            if (
                kind != "resource"
                or record.get("format") != "testfx-otel-jsonl"
                or record.get("schemaVersion") != 1
                or not isinstance(record.get("runId"), str)
                or not record["runId"]
            ):
                raise ValueError("missing or unsupported archive header")
            resource = record
            continue
        if completion is not None:
            raise ValueError("records follow the completion marker")
        if kind == "completion":
            completion = record
            continue
        if kind != "span":
            raise ValueError(f"line {line_number}: unexpected record type")
        trace_id, span_id = record.get("traceId"), record.get("spanId")
        if (
            not isinstance(trace_id, str)
            or re.fullmatch(r"[0-9a-f]{32}", trace_id) is None
            or int(trace_id, 16) == 0
            or not isinstance(span_id, str)
            or re.fullmatch(r"[0-9a-f]{16}", span_id) is None
            or span_id == ZERO_SPAN
            or not isinstance(record.get("parentSpanId"), str)
            or re.fullmatch(r"[0-9a-f]{16}", record["parentSpanId"]) is None
        ):
            raise ValueError("invalid trace/span identity")
        key = (trace_id, span_id)
        if key in spans:
            raise ValueError("duplicate span identity within an archive")
        ticks = record.get("durationTicks")
        if not isinstance(ticks, int) or isinstance(ticks, bool) or ticks < 0:
            raise ValueError("durationTicks must be a nonnegative integer")
        start = timestamp_ticks(record.get("startTimeUtc"))
        end = timestamp_ticks(record.get("endTimeUtc"))
        if end - start != ticks:
            raise ValueError("span timestamps do not match durationTicks")
        if not isinstance(record.get("operationName"), str):
            raise ValueError("missing span operation name")
        values = attributes(record)
        for name in (
            "test.case.id",
            "test.case.name",
            "test.case.result.status",
            "code.function.name",
        ):
            if name in values and not isinstance(values[name], str):
                raise ValueError(f"{name} must be a string")
        for name, value in values.items():
            if name.startswith("test.step.") and name.endswith(".duration"):
                duration_ms(value)
        for name in (
            "test.run.exit_code",
            "test.run.total",
            "test.run.failed",
            "test.run.skipped",
        ):
            if name in values and type(values[name]) is not int:
                raise ValueError(f"{name} must be an integer")
        spans[key] = {
            "key": key,
            "parent": (trace_id, record["parentSpanId"]),
            "operation": record["operationName"],
            "start": start,
            "end": end,
            "ms": ticks / TICKS_PER_MS,
            "attributes": values,
            "status": record.get("status"),
        }
    if resource is None:
        raise ValueError("empty archive")
    if (
        completion is None
        or completion.get("status") != "complete"
        or type(completion.get("failedExports")) is not int
        or completion.get("failedExports") != 0
        or type(completion.get("spanCount")) is not int
        or completion["spanCount"] != len(spans)
    ):
        raise ValueError("incomplete/failed archive or span-count mismatch")
    return {
        "id": resource["runId"],
        "resource": attributes(resource),
        "spans": spans,
        "sha256": digest.hexdigest(),
        "bytes": size,
    }


def lineage(span: dict, spans: dict) -> list[dict]:
    result = []
    seen = {span["key"]}
    parent = span["parent"]
    while parent in spans:
        if parent in seen or len(result) >= 128:
            raise ValueError("cyclic or excessive span ancestry")
        seen.add(parent)
        ancestor = spans[parent]
        result.append(ancestor)
        parent = ancestor["parent"]
    return result


def execution_scopes(archive: dict) -> tuple[list[dict], int]:
    spans = archive["spans"]
    ancestry = {key: lineage(span, spans) for key, span in spans.items()}
    host_types = {
        span["attributes"]["HostType"]
        for key, span in spans.items()
        if span["operation"] == "TestHostBuilder"
        and isinstance(span["attributes"].get("HostType"), str)
        and not any(
            parent["operation"] in ("TestHost", "TestHostController")
            or "test.case.id" in parent["attributes"]
            for parent in ancestry[key]
        )
    }
    host_type = next(iter(host_types)) if len(host_types) == 1 else None
    hosts = [
        span
        for key, span in spans.items()
        if span["operation"] == "TestHost"
        and "test.run.exit_code" in span["attributes"]
        and not any(
            parent["operation"] in ("TestHost", "TestHostController")
            or "test.case.id" in parent["attributes"]
            for parent in ancestry[key]
        )
    ]
    scopes = []
    for host in hosts:
        frameworks = {
            key
            for key, span in spans.items()
            if span["operation"] == "TestFramework"
            and host in ancestry[key]
            and not any(
                parent["operation"] in ("TestHost", "TestHostController")
                and parent["key"] != host["key"]
                or "test.case.id" in parent["attributes"]
                for parent in ancestry[key][: ancestry[key].index(host)]
            )
        }
        tests = [
            span
            for span in spans.values()
            if "test.case.id" in span["attributes"]
            and span["parent"] in frameworks
        ]
        test_keys = {span["key"] for span in tests}
        fixtures = []
        for key, span in spans.items():
            if span["operation"] not in FIXTURE_NAMES:
                continue
            if span["parent"] in frameworks or span["parent"] in test_keys:
                fixtures.append(span)
        scopes.append({
            "host": host, "hostType": host_type, "tests": tests, "fixtures": fixtures,
        })
    selected = sum(len(scope["tests"]) for scope in scopes)
    raw_tests = sum("test.case.id" in span["attributes"] for span in spans.values())
    return scopes, raw_tests - selected


def shorten_fixture(name: str) -> str:
    if "[[" in name:
        return name.split("[[", 1)[1].split(",", 1)[0].replace("+", ".")
    return name


def build_report(paths: list[Path], top: int = 15) -> dict:
    report = {
        "schemaVersion": 1,
        "kind": "snapshot",
        "coverage": {
            "archives": 0,
            "completeArchives": 0,
            "duplicateCopies": 0,
            "roles": {},
            "excludedTestSpans": 0,
            "rejected": [],
            "bytes": 0,
        },
        "cohorts": [],
        "executions": [],
        "fixtures": [],
        "slowestOccurrences": [],
        "steps": [],
        "publishedResults": published_results(paths),
    }
    coverage = report["coverage"]
    seen = {}
    seen_hosts = set()
    cohorts = {}
    fixture_samples = collections.defaultdict(list)
    step_samples = collections.defaultdict(list)
    slowest = []
    builds = set()
    revisions = set()
    roles = collections.Counter()
    for source, stream, input_bytes in trace_inputs(paths):
        coverage["bytes"] += input_bytes
        if coverage["bytes"] > MAX_TOTAL_BYTES:
            raise ValueError("total trace input exceeds the byte limit")
        coverage["archives"] += 1
        if coverage["archives"] > MAX_FILES:
            raise ValueError("too many trace files")
        try:
            archive = load_archive(stream)
            if archive["id"] in seen:
                if seen[archive["id"]] != archive["sha256"]:
                    raise ValueError("conflicting copies of an archive identity")
                coverage["duplicateCopies"] += 1
                continue
            resource = archive["resource"]
            module = resource.get("test.assembly.name")
            if not isinstance(module, str) or not module:
                raise ValueError("missing test module identity")
            scopes, excluded = execution_scopes(archive)
        except (ValueError, UnicodeError, zipfile.BadZipFile) as error:
            coverage["rejected"].append({"source": source, "reason": str(error)})
            continue
        seen[archive["id"]] = archive["sha256"]
        coverage["completeArchives"] += 1
        coverage["excludedTestSpans"] += excluded
        if not scopes:
            role = (
                "controller"
                if any(
                    span["operation"] == "TestHostController"
                    for span in archive["spans"].values()
                )
                else "non-execution"
            )
            roles[role] += 1
            continue
        if not any(scope["tests"] for scope in scopes):
            roles["zero-result"] += 1
            continue
        roles["execution"] += 1
        environment = {key: resource.get(key) for key in ENVIRONMENT_KEYS}
        environment["hostType"] = scopes[0]["hostType"]
        environment_key = json.dumps(environment, sort_keys=True)
        if environment_key not in cohorts:
            cohort = {
                "id": f"C{len(cohorts) + 1:02}",
                "environment": environment,
                "builds": set(),
                "executions": 0,
            }
            cohorts[environment_key] = cohort
        cohort = cohorts[environment_key]
        build = resource.get("cicd.pipeline.run.id")
        revision = resource.get("vcs.ref.head.revision")
        if build is not None:
            builds.add(str(build))
            cohort["builds"].add(str(build))
        if revision is not None:
            revisions.add(str(revision))
        for scope in scopes:
            host, tests, fixtures = scope["host"], scope["tests"], scope["fixtures"]
            if not tests:
                continue
            if host["key"] in seen_hosts:
                raise ValueError("same execution observed in distinct archive identities")
            seen_hosts.add(host["key"])
            cohort["executions"] += 1
            statuses = collections.Counter(
                test["attributes"].get("test.case.result.status", "unknown")
                for test in tests
            )
            with_steps = 0
            for test in tests:
                attrs = test["attributes"]
                steps = {
                    key[10:-9]: duration_ms(value)
                    for key, value in attrs.items()
                    if key.startswith("test.step.") and key.endswith(".duration")
                }
                with_steps += bool(steps)
                for step, value in steps.items():
                    step_samples[(cohort["id"], step)].append(value)
                if attrs.get("test.case.result.status") != "skipped":
                    slowest.append(
                        {
                            "cohort": cohort["id"],
                            "module": module,
                            "name": attrs.get("test.case.name", test["operation"]),
                            "function": attrs.get("code.function.name"),
                            "testId": attrs["test.case.id"],
                            "traceId": test["key"][0],
                            "spanId": test["key"][1],
                            "status": attrs.get("test.case.result.status", "unknown"),
                            "elapsedMs": test["ms"],
                            "stepsMs": steps,
                        }
                    )
            for fixture in fixtures:
                owner = fixture["attributes"].get("test.suite.name")
                if not isinstance(owner, str):
                    owner = "(owner not recorded)"
                fixture_samples[(cohort["id"], fixture["operation"], owner)].append(
                    fixture["ms"]
                )
            intervals = [
                (max(host["start"], span["start"]), min(host["end"], span["end"]))
                for span in fixtures
                if span["start"] < host["end"] and span["end"] > host["start"]
            ]
            observed = len(tests) - statuses["skipped"]
            declared = host["attributes"].get("test.run.total")
            declared_skipped = host["attributes"].get("test.run.skipped")
            report["executions"].append(
                {
                    "cohort": cohort["id"],
                    "module": module,
                    "hostType": scope["hostType"],
                    "archive": source,
                    "archiveId": archive["id"],
                    "archiveSha256": archive["sha256"],
                    "build": build,
                    "revision": revision,
                    "jobId": resource.get("cicd.pipeline.task.run.id"),
                    "jobAttempt": resource.get("testfx.ci.job.attempt"),
                    "processAttempt": resource.get("testfx.run.attempt"),
                    "traceId": host["key"][0],
                    "spanId": host["key"][1],
                    "hostMs": host["ms"],
                    "exitCode": host["attributes"]["test.run.exit_code"],
                    "tests": len(tests),
                    "statuses": dict(statuses),
                    "withSteps": with_steps,
                    "occurrenceTiming": distribution(
                        [
                            test["ms"]
                            for test in tests
                            if test["attributes"].get("test.case.result.status")
                            != "skipped"
                        ]
                    ),
                    "fixtureInvocations": len(fixtures),
                    "fixtureWorkMs": (
                        math.fsum(span["ms"] for span in fixtures) if fixtures else None
                    ),
                    "fixtureActiveMs": (
                        interval_union(intervals) / TICKS_PER_MS if fixtures else None
                    ),
                    "reportedExecuted": declared,
                    "reportedSkipped": declared_skipped,
                    "countsMatch": (
                        None
                        if declared is None or declared_skipped is None
                        else observed == declared
                        and statuses["skipped"] == declared_skipped
                    ),
                }
            )
    if coverage["archives"] == 0:
        raise ValueError("no TestFx JSONL trace archives found")
    report["cohorts"] = [
        {**cohort, "builds": sorted(cohort["builds"])} for cohort in cohorts.values()
    ]
    report["fixtures"] = sorted(
        [
            {
                "cohort": cohort,
                "phase": phase,
                "owner": owner,
                **distribution(values),
            }
            for (cohort, phase, owner), values in fixture_samples.items()
        ],
        key=lambda item: (-item["sumMs"], item["cohort"], item["owner"]),
    )
    report["steps"] = [
        {"cohort": cohort, "step": step, **distribution(values)}
        for (cohort, step), values in sorted(step_samples.items())
    ]
    report["slowestOccurrences"] = sorted(
        slowest, key=lambda item: (-item["elapsedMs"], item["cohort"], str(item["testId"]))
    )[:top]
    coverage["roles"] = dict(roles)
    coverage["builds"] = sorted(builds)
    coverage["revisions"] = sorted(revisions)
    coverage["selectedOccurrences"] = sum(row["tests"] for row in report["executions"])
    coverage["withSteps"] = sum(row["withSteps"] for row in report["executions"])
    coverage["controlledWorkers"] = sum(
        row["hostType"] == "TestHostControlledHost" for row in report["executions"]
    )
    coverage["controlledOccurrences"] = sum(
        row["tests"] for row in report["executions"]
        if row["hostType"] == "TestHostControlledHost"
    )
    coverage["standaloneWorkers"] = sum(
        row["hostType"] == "ConsoleTestHost" for row in report["executions"]
    )
    coverage["standaloneOccurrences"] = sum(
        row["tests"] for row in report["executions"]
        if row["hostType"] == "ConsoleTestHost"
    )
    coverage["unreconciledExecutions"] = sum(
        row["countsMatch"] is False for row in report["executions"]
    )
    report["top"] = top
    return report


def display(value) -> str:
    text = "unknown" if value is None else str(value)
    return "".join(character if character.isprintable() else " " for character in text)


def html_text(value) -> str:
    return html.escape(display(value))


def markdown_text(value) -> str:
    text = re.sub(r"([\\`*_{}\[\]()#+.!$~-])", r"\\\1", display(value))
    return html.escape(text, quote=False).replace("|", "&#124;")


def cohort_label(cohort: dict) -> str:
    env = cohort["environment"]
    tfm = display(env["testfx.target.framework"]).replace(",Version=v", " ")
    return (
        f"{display(env['os.type'])} / {display(env['testfx.build.configuration'])}"
        f" / {tfm} / {display(env['process.arch'])}"
        + (" / standalone" if env["hostType"] == "ConsoleTestHost" else "")
    )


def report_sections(report: dict) -> list[tuple[str, list[str], list[list]]]:
    cohorts = {row["id"]: row for row in report["cohorts"]}

    def label(identity):
        return f"{identity}: {cohort_label(cohorts[identity])}"

    executions = sorted(report["executions"], key=lambda row: -row["hostMs"])
    summary = [
        [
            label(row["cohort"]),
            row["module"],
            row["traceId"][:10],
            f"{row['hostMs'] / 1000:.2f}",
            row["tests"],
            sum(
                count for status, count in row["statuses"].items()
                if status not in ("pass", "skipped")
            ),
            row["statuses"].get("skipped", 0),
            (
                f"{row['fixtureWorkMs'] / 1000:.2f}"
                if row["fixtureWorkMs"] is not None else "not observed"
            ),
            (
                f"{row['fixtureActiveMs'] / 1000:.2f}"
                if row["fixtureActiveMs"] is not None else "not observed"
            ),
            row["withSteps"],
        ]
        for row in executions
    ]
    fixtures = [
        [
            label(row["cohort"]),
            row["phase"].removeprefix("MSTest."),
            shorten_fixture(row["owner"]),
            row["count"],
            f"{row['sumMs'] / 1000:.2f}",
            f"{row['maxMs'] / 1000:.2f}",
        ]
        for row in report["fixtures"][: report["top"]]
    ]
    slowest = [
        [
            label(row["cohort"]),
            row["function"] or row["name"],
            row["name"],
            row["status"],
            f"{row['elapsedMs'] / 1000:.2f}",
        ]
        for row in report["slowestOccurrences"]
    ]
    steps = [
        [
            label(row["cohort"]),
            row["step"],
            row["count"],
            f"{row['sumMs'] / 1000:.3f}",
            f"{row['p50Ms']:.3f}",
            f"{row['p95Ms']:.3f}",
        ]
        for row in report["steps"]
    ]
    environments = [
        [
            row["id"],
            cohort_label(row),
            row["environment"]["test.assembly.name"],
            row["environment"]["host.cpu.logical_count"],
            len(row["builds"]),
            row["executions"],
        ]
        for row in cohorts.values()
    ]
    return [
        (
            "Largest measured fixture costs",
            ["Cohort", "Phase", "Owner", "Invocations", "Work s", "Largest s"],
            fixtures,
        ),
        (
            "Slowest non-skipped test occurrences (not body-only timing)",
            ["Cohort", "Function", "Display name", "Result", "Elapsed s"],
            slowest,
        ),
        (
            "Reported step timing (missing steps are not zero)",
            ["Cohort", "Step", "Samples", "Work s", "Median ms", "p95 ms"],
            steps,
        ),
        (
            "Execution cost by worker (not a sum of overlapping test spans)",
            ["Cohort", "Module", "Trace", "Host s", "Occurrences", "Non-pass", "Skipped", "Fixture work s", "Fixture active s", "With steps"],
            summary,
        ),
        (
            "Separate observation cohorts",
            ["Cohort", "Environment", "Module", "Logical CPUs", "Builds", "Workers"],
            environments,
        ),
    ]


def introduction(report: dict, *, markdown: bool = True) -> list[str]:
    coverage = report["coverage"]
    text = markdown_text if markdown else html_text
    emphasis = "**" if markdown else ""
    result = [
        "# TestFx trace value report",
        (
            f"Descriptive snapshot: {len(coverage['builds'])} independent CI build(s), "
            f"{len(report['executions'])} executing workers, "
            f"{coverage['selectedOccurrences']:,} attributed test occurrences."
        ),
        (
            f"Inputs: {coverage['completeArchives']} complete archives; "
            f"{coverage['duplicateCopies']} duplicate copies ignored; "
            f"{len(coverage['rejected'])} rejected archives; "
            f"{coverage['excludedTestSpans']} test spans outside attributed frameworks excluded. "
            f"Archive roles: {text(json.dumps(coverage['roles'], sort_keys=True))}."
        ),
        (
            f"Controlled-protocol workers: {coverage['controlledWorkers']} processes / "
            f"{coverage['controlledOccurrences']:,} occurrences. Standalone workers: "
            f"{coverage['standaloneWorkers']} processes / "
            f"{coverage['standaloneOccurrences']:,} occurrences. Standalone processes "
            "and controlled-protocol processes can both be filtered subprocesses "
            "launched by tests; protocol type does not establish process ownership. "
            "These are execution occurrences, not distinct suite-test counts."
        ),
        (
            "Build IDs: " + (", ".join(text(value) for value in coverage["builds"]) or "not recorded")
            + ". Source revisions: "
            + (", ".join(text(value) for value in coverage["revisions"]) or "not recorded") + "."
        ),
        (
            f"{emphasis}No regression or machine-load-adjusted trend is claimed.{emphasis} "
            "Different OS/TFM/configuration cohorts are not baselines for each other. "
            "Binary MVIDs keep otherwise indistinguishable target-platform variants "
            "separate in this snapshot; they are not stable cross-build cohort keys. "
            "Fixture work is the sum of unique invocation durations; fixture active "
            "time is their interval union within that worker. Neither is exclusive "
            "overhead or CPU time, and neither should be added to test elapsed time."
        ),
        (
            f"Phase coverage: {coverage['withSteps']:,} of "
            f"{coverage['selectedOccurrences']:,} occurrences have reported steps. "
            "Native body-only timing is unavailable; TestContainer step.test is "
            "reported separately. Missing CPU pressure, coverage/parallelism settings "
            "and sometimes runner-image metadata limit comparability. "
            f"{coverage['unreconciledExecutions']} worker count reconciliation(s) differ "
            "from the host's reported executed/skipped counts."
        ),
    ]
    if report["fixtures"]:
        largest = max(report["fixtures"], key=lambda row: row["maxMs"])
        cohort = next(row for row in report["cohorts"] if row["id"] == largest["cohort"])
        result.insert(
            2,
            "Largest measured fixture invocation: "
            f"{text(shorten_fixture(largest['owner']))}, "
            f"{text(largest['phase'].removeprefix('MSTest.'))} "
            f"{largest['maxMs'] / 1000:.2f} s ({text(cohort_label(cohort))}). "
            "This identifies an investigation target, not a proven regression "
            "or a claim that the work is avoidable.",
        )
    if report["publishedResults"] is not None:
        published = report["publishedResults"]
        difference = coverage["selectedOccurrences"] - published["tests"]
        result.insert(
            3,
            f"Independent result coverage: {published['tests']:,} results in "
            f"{published['reports']} root CTRF reports; OTel occurrence-count "
            f"difference: {difference:+,}. Repeated subprocess executions/retries "
            "can add observations; missing trace/report coverage can also cause "
            "a difference. This is not a count of additional independent tests.",
        )
    return result


def markdown_report(report: dict) -> str:
    parts = introduction(report)
    for title, headers, rows in report_sections(report):
        parts.append(f"## {title}")
        if not rows:
            parts.append("No observations in the supplied archives.")
            continue

        parts.append(
            "\n".join([
                "| " + " | ".join(headers) + " |",
                "| " + " | ".join("---" for _ in headers) + " |",
                *["| " + " | ".join(markdown_text(value) for value in row) + " |" for row in rows],
            ])
        )
    if report["coverage"]["rejected"]:
        parts.append("## Rejected input (excluded from all cost rankings)")
        parts.extend(
            f"- {markdown_text(row['source'])}: {markdown_text(row['reason'])}"
            for row in report["coverage"]["rejected"]
        )
    return "\n\n".join(parts) + "\n"


def html_report(report: dict) -> str:
    parts = [
        '<!doctype html><html lang="en"><meta charset="utf-8">',
        '<meta name="viewport" content="width=device-width, initial-scale=1">',
        "<title>TestFx trace value report</title>",
        "<style>body{font:15px/1.5 system-ui,sans-serif;max-width:1280px;margin:auto;padding:24px;color:#172033}"
        "h1,h2{line-height:1.2}h2{margin-top:36px}.scroll{overflow-x:auto}"
        "table{border-collapse:collapse;width:100%;min-width:900px;font-size:13px}th,td{text-align:left;padding:8px;"
        "border-bottom:1px solid #d5dce5;vertical-align:top;overflow-wrap:anywhere;min-width:55px;max-width:400px}"
        "th{background:#edf2f7}tr:nth-child(even){background:#f8fafc}"
        "p{overflow-wrap:anywhere}code{white-space:pre-wrap}"
        "summary{cursor:pointer;padding:12px;background:#edf2f7;font-weight:600}"
        "details{margin-top:24px}</style><main>",
    ]
    for index, text in enumerate(introduction(report, markdown=False)):
        if index == 0:
            parts.append("<h1>TestFx trace value report</h1>")
        else:
            parts.append("<p>" + text + "</p>")
    for title, headers, rows in report_sections(report):
        collapsed = len(rows) > 20
        if collapsed:
            parts.append(
                "<details><summary>" + html.escape(title)
                + f" ({len(rows)} rows)</summary>"
            )
        else:
            parts.append("<h2>" + html.escape(title) + "</h2>")
        if not rows:
            parts.append("<p>No observations in the supplied archives.</p>")
            continue
        parts.append(
            '<div class="scroll" tabindex="0" role="region" aria-label="'
            + html.escape(title, quote=True) + '"><table><thead><tr>'
        )
        parts.extend("<th scope=\"col\">" + html.escape(header) + "</th>" for header in headers)
        parts.append("</tr></thead><tbody>")
        for row in rows:
            parts.append("<tr>")
            parts.extend("<td>" + html_text(value) + "</td>" for value in row)
            parts.append("</tr>")
        parts.append("</tbody></table></div>")
        if collapsed:
            parts.append("</details>")
    for rejected in report["coverage"]["rejected"]:
        parts.append(
            f"<p>Rejected: {html_text(rejected['source'])}: {html_text(rejected['reason'])}</p>"
        )
    parts.append("</main></html>")
    return "\n".join(parts) + "\n"


def main(argv=None) -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--input", type=Path, action="append", required=True)
    parser.add_argument("--output", type=Path, required=True, help="output filename prefix")
    parser.add_argument("--top", type=int, default=15)
    args = parser.parse_args(argv)
    if not 1 <= args.top <= 100:
        parser.error("--top must be between 1 and 100")
    try:
        report = build_report(args.input, args.top)
        args.output.parent.mkdir(parents=True, exist_ok=True)
        Path(str(args.output) + ".json").write_text(
            json.dumps(report, indent=2, allow_nan=False) + "\n", encoding="utf-8"
        )
        Path(str(args.output) + ".md").write_text(markdown_report(report), encoding="utf-8")
        Path(str(args.output) + ".html").write_text(html_report(report), encoding="utf-8")
    except (OSError, ValueError, zipfile.BadZipFile) as error:
        print(f"Trace report failed: {error}", file=sys.stderr)
        return 2
    coverage = report["coverage"]
    print(
        f"{len(report['executions'])} executing workers; "
        f"{coverage['selectedOccurrences']} attributed occurrences; "
        f"{len(coverage['rejected'])} rejected archives."
    )
    return 2 if coverage["rejected"] or not report["executions"] else 0


if __name__ == "__main__":
    sys.exit(main())
