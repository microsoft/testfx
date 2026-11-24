#!/usr/bin/env python3

import importlib.util
import json
import pathlib
import subprocess
import sys
import tempfile
import unittest
import zipfile
from datetime import datetime, timedelta, timezone
from unittest.mock import patch


SCRIPT = pathlib.Path(__file__).with_name("report_test_traces.py")
SPEC = importlib.util.spec_from_file_location("report_test_traces", SCRIPT)
assert SPEC is not None
assert SPEC.loader is not None
REPORT = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(REPORT)

# Allow process startup, file I/O and teardown on loaded CI; this is not a timing assertion.
CLI_TIMEOUT_SECONDS = 60


def attrs(values):
    return [{"key": key, "value": value} for key, value in values.items()]


def span(identity, parent, operation, start, end, values=None, trace="a" * 32):
    epoch = datetime(2026, 11, 10, tzinfo=timezone.utc)
    return {
        "recordType": "span",
        "traceId": trace,
        "spanId": f"{identity:016x}",
        "parentSpanId": f"{parent:016x}",
        "operationName": operation,
        "startTimeUtc": (epoch + timedelta(milliseconds=start)).isoformat().replace("+00:00", "Z"),
        "endTimeUtc": (epoch + timedelta(milliseconds=end)).isoformat().replace("+00:00", "Z"),
        "durationTicks": (end - start) * 10_000,
        "status": "Ok",
        "attributes": attrs(values or {}),
    }


def records(identity="archive", environment=None):
    resource = {
        "test.assembly.name": "Example.Tests",
        "testfx.target.framework": ".NETCoreApp,Version=v10.0",
        "testfx.build.configuration": "Release",
        "os.type": "linux",
        "process.arch": "X64",
        "process.runtime.description": ".NET 10",
        "host.cpu.logical_count": 4,
        "cicd.pipeline.run.id": "123",
        "vcs.ref.head.revision": "c" * 40,
        **(environment or {}),
    }
    return [
        {
            "recordType": "resource",
            "format": "testfx-otel-jsonl",
            "schemaVersion": 1,
            "runId": identity,
            "attributes": attrs(resource),
        },
        span(1, 0, "TestHost", 0, 10000, {
            "test.run.exit_code": 0, "test.run.total": 2, "test.run.skipped": 0,
        }),
        span(2, 1, "TestFramework", 0, 10000),
        span(3, 2, "First", 1000, 7000, {
            "test.case.id": "first",
            "test.case.name": "First",
            "code.function.name": "Example.First",
            "test.case.result.status": "pass",
            "test.step.init.duration": 100,
            "test.step.test.duration": 400,
            "test.step.cleanup.duration": 50,
        }),
        span(4, 2, "Second", 3000, 6000, {
            "test.case.id": "second",
            "test.case.name": "Second",
            "test.case.result.status": "fail",
        }),
        span(5, 2, "MSTest.ClassInitialize", 0, 4000, {
            "test.suite.name": "Example.SharedFixture",
        }),
        span(6, 3, "MSTest.TestInitialize", 2000, 3000, {
            "test.suite.name": "Example.PerTestFixture",
        }),
        span(7, 2, "MSTest.ClassCleanup", 8000, 9000, {
            "test.suite.name": "Example.SharedFixture",
        }),
        span(8, 3, "Synthetic", 4000, 5000, {
            "test.case.id": "probe", "test.case.result.status": "pass",
        }),
        span(9, 3, "TestHost", 1000, 6000, {
            "test.run.exit_code": 0, "test.run.total": 1, "test.run.skipped": 0,
        }),
        span(10, 9, "TestFramework", 1000, 6000),
        span(11, 10, "Nested", 2000, 5000, {
            "test.case.id": "nested", "test.case.result.status": "pass",
        }),
        span(12, 0, "TestHostBuilder", 0, 10, {"HostType": "TestHostControlledHost"}),
        {"recordType": "completion", "status": "complete", "spanCount": 12, "failedExports": 0},
    ]


def write_records(root, name, data):
    path = root / name
    path.write_text(
        "".join(json.dumps(record) + "\n" for record in data), encoding="utf-8"
    )
    return path


class ReportTestTracesTests(unittest.TestCase):
    def test_outer_execution_owns_counts_and_overlapping_fixture_intervals(self):
        with tempfile.TemporaryDirectory() as directory:
            path = write_records(pathlib.Path(directory), "trace.jsonl", records())
            report = REPORT.build_report([path])

        self.assertEqual(1, len(report["executions"]))
        execution = report["executions"][0]
        self.assertEqual(2, execution["tests"])
        self.assertEqual({"pass": 1, "fail": 1}, execution["statuses"])
        self.assertTrue(execution["countsMatch"])
        self.assertEqual(2, report["coverage"]["excludedTestSpans"])
        self.assertEqual(2, report["coverage"]["controlledOccurrences"])
        self.assertEqual(0, report["coverage"]["standaloneOccurrences"])
        self.assertEqual(10000, execution["hostMs"])
        self.assertEqual(3, execution["fixtureInvocations"])
        self.assertEqual(6000, execution["fixtureWorkMs"])
        self.assertEqual(5000, execution["fixtureActiveMs"])
        self.assertEqual(1, execution["withSteps"])
        self.assertEqual(
            {"init": 100, "test": 400, "cleanup": 50},
            {row["step"]: row["sumMs"] for row in report["steps"]},
        )
        self.assertEqual(
            [("first", 6000), ("second", 3000)],
            [(row["testId"], row["elapsedMs"]) for row in report["slowestOccurrences"]],
        )
        self.assertIn("not body-only timing", REPORT.markdown_report(report))

    def test_zip_controller_informative_and_duplicate_files_are_not_extra_runs(self):
        data = records()
        controller = [
            {**data[0], "runId": "controller"},
            span(20, 0, "TestHostController", 0, 12000, {"test.run.exit_code": 0}),
            {**data[-1], "spanCount": 1},
        ]
        informative = [
            {**data[0], "runId": "help"},
            span(21, 0, "TestHostBuilder", 0, 10, {"HostType": "InformativeCommandLineHost"}),
            {**data[-1], "spanCount": 1},
        ]
        with tempfile.TemporaryDirectory() as directory:
            archive = pathlib.Path(directory) / "results.zip"
            with zipfile.ZipFile(archive, "w") as zipped:
                for name, items in (
                    ("worker", data), ("copy", data),
                    ("controller", controller), ("help", informative),
                ):
                    zipped.writestr(
                        f"OpenTelemetry/{name}.jsonl",
                        "".join(json.dumps(row) + "\n" for row in items),
                    )
                zipped.writestr("not-a-trace.txt", "never executed")
                zipped.writestr("suite.ctrf.json", json.dumps({
                    "results": {"tests": [{"status": "passed"}, {"status": "failed"}]},
                }))
            report = REPORT.build_report([archive])

        coverage = report["coverage"]
        self.assertEqual(4, coverage["archives"])
        self.assertEqual(3, coverage["completeArchives"])
        self.assertEqual(1, coverage["duplicateCopies"])
        self.assertEqual({"execution": 1, "controller": 1, "non-execution": 1}, coverage["roles"])
        self.assertEqual(["123"], coverage["builds"])
        self.assertEqual(2, coverage["selectedOccurrences"])
        self.assertEqual(
            {"reports": 1, "tests": 2, "statuses": {"passed": 1, "failed": 1}},
            report["publishedResults"],
        )
        self.assertIn("difference: +0", REPORT.markdown_report(report))

    def test_incomplete_incompatible_and_invalid_inputs_remain_explicit_gaps(self):
        for case in (
            "partial", "schema", "count", "failed", "duplicate", "cycle",
            "timestamp", "step-type", "test-identity-type", "run-count-type",
        ):
            with self.subTest(case=case), tempfile.TemporaryDirectory() as directory:
                data = records("bad")
                if case == "partial":
                    data.pop()
                elif case == "schema":
                    data[0]["schemaVersion"] = 2
                elif case == "count":
                    data[-1]["spanCount"] = 11
                elif case == "failed":
                    data[-1]["failedExports"] = 1
                elif case == "duplicate":
                    data[-2] = data[1]
                elif case == "cycle":
                    data[2]["parentSpanId"] = data[2]["spanId"]
                elif case == "step-type":
                    data[3]["attributes"].append({
                        "key": "test.step.invalid.duration", "value": "not a number",
                    })
                elif case == "test-identity-type":
                    data[3]["attributes"][0]["value"] = ["not a string"]
                elif case == "run-count-type":
                    data[1]["attributes"][1]["value"] = True
                else:
                    data[1]["durationTicks"] += 1
                root = pathlib.Path(directory)
                good = write_records(root, "good.jsonl", records("good"))
                bad = write_records(root, "bad.jsonl", data)
                report = REPORT.build_report([good, bad])

            self.assertEqual(1, len(report["coverage"]["rejected"]))
            self.assertEqual(2, report["coverage"]["selectedOccurrences"])
            self.assertEqual(1, len(report["executions"]))
            self.assertIn("Rejected input", REPORT.markdown_report(report))

    def test_configuration_cohorts_are_not_independent_build_samples(self):
        with tempfile.TemporaryDirectory() as directory:
            root = pathlib.Path(directory)
            first = write_records(root, "release.jsonl", records())
            debug = records("debug", {"testfx.build.configuration": "Debug"})
            for record in debug:
                if record["recordType"] == "span":
                    record["traceId"] = "b" * 32
            second = write_records(root, "debug.jsonl", debug)
            report = REPORT.build_report([first, second])

        self.assertEqual(2, len(report["cohorts"]))
        self.assertEqual(["123"], report["coverage"]["builds"])
        self.assertEqual([["123"], ["123"]], [row["builds"] for row in report["cohorts"]])
        self.assertIn("1 independent CI build", REPORT.markdown_report(report))
        self.assertIn("No regression", REPORT.markdown_report(report))

    def test_binary_variants_and_standalone_processes_do_not_merge_into_suite_coverage(self):
        with tempfile.TemporaryDirectory() as directory:
            root = pathlib.Path(directory)
            paths = []
            for index, (binary, host_type) in enumerate((
                ("portable", "TestHostControlledHost"),
                ("windows-target", "TestHostControlledHost"),
                ("portable", "ConsoleTestHost"),
            )):
                data = records(str(index), {"testfx.module.mvid": binary})
                for record in data:
                    if record["recordType"] == "span":
                        record["traceId"] = str(index + 1) * 32
                        if record["operationName"] == "TestHostBuilder":
                            record["attributes"] = attrs({"HostType": host_type})
                paths.append(write_records(root, f"{index}.jsonl", data))
            report = REPORT.build_report(paths)
        self.assertEqual(3, len(report["cohorts"]))
        self.assertEqual(4, report["coverage"]["controlledOccurrences"])
        self.assertEqual(2, report["coverage"]["standaloneOccurrences"])
        self.assertEqual(["123"], report["coverage"]["builds"])

    def test_missing_fixture_and_step_timings_are_unknown_not_zero(self):
        data = records()
        data = [
            row for row in data
            if row.get("operationName") not in REPORT.FIXTURE_NAMES
        ]
        data[-1]["spanCount"] -= 3
        data[3]["attributes"] = [
            item for item in data[3]["attributes"]
            if not item["key"].startswith("test.step.")
        ]
        with tempfile.TemporaryDirectory() as directory:
            path = write_records(pathlib.Path(directory), "trace.jsonl", data)
            report = REPORT.build_report([path])
        self.assertIsNone(report["executions"][0]["fixtureWorkMs"])
        self.assertIsNone(report["executions"][0]["fixtureActiveMs"])
        self.assertEqual([], report["steps"])
        self.assertIn("not observed", REPORT.markdown_report(report))
        self.assertIn("No observations in the supplied archives.", REPORT.html_report(report))

    def test_input_budget_counts_rejected_archives_and_invalid_ctrf_fails_explicitly(self):
        with tempfile.TemporaryDirectory() as directory:
            root = pathlib.Path(directory)
            partial = write_records(root, "partial.jsonl", records("partial")[:-1])
            good = write_records(root, "good.jsonl", records())
            limit = max(partial.stat().st_size, good.stat().st_size)
            with patch.object(REPORT, "MAX_TOTAL_BYTES", limit):
                with self.assertRaisesRegex(ValueError, "total trace input"):
                    REPORT.build_report([partial, good])
            (root / "invalid.ctrf.json").write_text("[]", encoding="utf-8")
            with self.assertRaisesRegex(ValueError, "invalid published CTRF"):
                REPORT.build_report([root])

    def test_ctrf_coverage_uses_same_artifact_root_in_zip_and_directory_layouts(self):
        expected = {
            "reports": 2, "tests": 3, "statuses": {"passed": 2, "failed": 1},
        }
        for wrapped in (False, True):
            for zipped in (False, True):
                with self.subTest(wrapped=wrapped, zipped=zipped), tempfile.TemporaryDirectory() as directory:
                    root = pathlib.Path(directory)
                    extracted = root / "input"
                    artifact = extracted / "TestResults_Linux_Release_Attempt1" if wrapped else extracted
                    telemetry = artifact / "OpenTelemetry"
                    telemetry.mkdir(parents=True)
                    write_records(telemetry, "worker.jsonl", records())
                    for parts, statuses in (
                        (("first.ctrf.json",), ["passed", "failed"]),
                        (("second.ctrf.json",), ["passed"]),
                        (("child", "child.ctrf.json"), ["skipped"]),
                        (("merged", "merged.ctrf.json"), ["passed", "failed", "skipped"]),
                    ):
                        result = artifact.joinpath(*parts)
                        result.parent.mkdir(parents=True, exist_ok=True)
                        result.write_text(json.dumps({
                            "results": {"tests": [{"status": status} for status in statuses]},
                        }), encoding="utf-8")
                    source = extracted
                    if zipped:
                        source = root / "results.zip"
                        with zipfile.ZipFile(source, "w") as archive:
                            for member in sorted(extracted.rglob("*")):
                                if member.is_file():
                                    archive.write(member, member.relative_to(extracted).as_posix())
                    report = REPORT.build_report([source])

                    self.assertEqual(expected, report["publishedResults"])
                    self.assertEqual(1, report["coverage"]["completeArchives"])
                    self.assertEqual(2, report["coverage"]["selectedOccurrences"])
                    self.assertEqual([], report["coverage"]["rejected"])

    def test_cli_retains_healthy_members_and_names_crc_damaged_trace(self):
        with tempfile.TemporaryDirectory() as directory:
            root = pathlib.Path(directory)
            archive = root / "results.zip"
            damaged_name = "OpenTelemetry/damaged.jsonl"
            with zipfile.ZipFile(archive, "w", compression=zipfile.ZIP_STORED) as zipped:
                for name, identity in ((damaged_name, "damaged"), ("OpenTelemetry/healthy.jsonl", "healthy")):
                    zipped.writestr(
                        name, "".join(json.dumps(row) + "\n" for row in records(identity)),
                    )
                damaged = zipped.getinfo(damaged_name)
            contents = bytearray(archive.read_bytes())
            payload = damaged.header_offset + 30 + len(damaged.filename.encode("utf-8"))
            # Change valid JSON whitespace without updating the ZIP's recorded CRC.
            contents[payload + damaged.file_size - 1] = ord(" ")
            archive.write_bytes(contents)
            output = root / "report"
            result = subprocess.run(
                [sys.executable, str(SCRIPT), "--input", str(archive), "--output", str(output)],
                capture_output=True, text=True, timeout=CLI_TIMEOUT_SECONDS,
            )

            self.assertEqual(2, result.returncode, result.stderr)
            for suffix in (".json", ".md", ".html"):
                self.assertTrue(output.with_suffix(suffix).is_file(), result.stderr)
            report = json.loads(output.with_suffix(".json").read_text(encoding="utf-8"))
            coverage = report["coverage"]
            self.assertEqual(2, coverage["archives"])
            self.assertEqual(1, coverage["completeArchives"])
            self.assertEqual(2, coverage["selectedOccurrences"])
            self.assertEqual(
                [{"source": f"results.zip!{damaged_name}", "reason": f"Bad CRC-32 for file '{damaged_name}'"}],
                coverage["rejected"],
            )
            self.assertEqual(["healthy"], [row["archiveId"] for row in report["executions"]])
            self.assertEqual({"pass": 1, "fail": 1}, report["executions"][0]["statuses"])
            for suffix in (".md", ".html"):
                expected_name = (
                    r"OpenTelemetry/damaged\.jsonl" if suffix == ".md" else damaged_name
                )
                self.assertIn(expected_name, output.with_suffix(suffix).read_text(encoding="utf-8"))

    def test_exact_timestamp_ticks_and_percentiles_preserve_units(self):
        self.assertEqual(
            1_234_567,
            REPORT.timestamp_ticks("2026-11-10T00:00:00.1234567Z")
            - REPORT.timestamp_ticks("2026-11-10T00:00:00Z"),
        )
        self.assertEqual(
            {"count": 4, "sumMs": 1100, "p50Ms": 250, "p95Ms": 500, "maxMs": 500},
            REPORT.distribution([100, 200, 300, 500]),
        )
        self.assertEqual(8, REPORT.interval_union([(0, 4), (1, 3), (2, 6), (8, 10)]))
        for value in (float("nan"), float("inf"), -1, True):
            with self.subTest(value=repr(value)), self.assertRaises(ValueError):
                REPORT.duration_ms(value)

    def test_rendered_tables_keep_identity_and_escape_untrusted_names(self):
        from html.parser import HTMLParser

        name = (
            'First | <script>alert("x")</script>\n'
            r"**literal** _name_ ~~strike~~ `code` [link](target) \path"
        )
        data = records()
        data[3]["attributes"] = attrs({
            "test.case.id": "first",
            "test.case.name": name,
            "code.function.name": "Example." + "LongName" * 30,
            "test.case.result.status": "pass",
        })
        with tempfile.TemporaryDirectory() as directory:
            path = write_records(pathlib.Path(directory), "trace.jsonl", data)
            report = REPORT.build_report([path])
        original = json.dumps(report, sort_keys=True)
        markdown = REPORT.markdown_report(report)
        document = REPORT.html_report(report)
        self.assertNotIn("<script>", document)
        self.assertIn("&lt;script&gt;", document)
        self.assertIn(
            r'| First &#124; &lt;script&gt;alert\("x"\)&lt;/script&gt; '
            r'\*\*literal\*\* \_name\_ \~\~strike\~\~ \`code\` \[link\]\(target\) \\path | pass | 6\.00 |',
            markdown,
        )
        self.assertIn(r"| Second | Second | fail | 3\.00 |", markdown)
        self.assertIn(
            "| Cohort | Module | Trace | Host s | Occurrences | Non-pass | Skipped | Fixture work s | Fixture active s | With steps |\n| ---",
            markdown,
        )
        text = []
        parser = HTMLParser()
        parser.handle_data = text.append
        parser.feed(document)
        self.assertIn(name.replace("\n", " "), text)
        self.assertEqual(name, report["slowestOccurrences"][0]["name"])
        self.assertEqual(original, json.dumps(report, sort_keys=True))
        self.assertIn('overflow-x:auto', document)
        self.assertIn('min-width:900px', document)
        self.assertIn('tabindex="0" role="region"', document)
        self.assertIn('scope="col"', document)
        self.assertIn("LongName" * 30, document)

    def test_introduction_escapes_dynamic_labels_before_intentional_formatting(self):
        from html.parser import HTMLParser

        fixture = "Fixture **literal** & <b>name</b>\n# injected"
        build = "build **label** & <build>\n# injected"
        revision = r"`rev` [branch](target) \revision"
        data = records(environment={
            "os.type": "linux **literal** & <os>",
            "cicd.pipeline.run.id": build,
            "vcs.ref.head.revision": revision,
        })
        data[5]["attributes"] = attrs({"test.suite.name": fixture})
        with tempfile.TemporaryDirectory() as directory:
            path = write_records(pathlib.Path(directory), "trace.jsonl", data)
            report = REPORT.build_report([path])
        original = json.dumps(report, sort_keys=True)
        markdown = REPORT.markdown_report(report)
        document = REPORT.html_report(report)
        with self.subTest(surface="markdown"):
            self.assertIn(
                r"Largest measured fixture invocation: Fixture \*\*literal\*\* &amp; "
                r"&lt;b&gt;name&lt;/b&gt; \# injected, ClassInitialize 4.00 s "
                r"(linux \*\*literal\*\* &amp; &lt;os&gt; / Release / \.NETCoreApp 10\.0 / X64).",
                markdown,
            )
            self.assertIn(
                r"Build IDs: build \*\*label\*\* &amp; &lt;build&gt; \# injected. "
                r"Source revisions: \`rev\` \[branch\]\(target\) \\revision.",
                markdown,
            )
            self.assertTrue(markdown.startswith("# TestFx trace value report\n\n"))
            self.assertIn("**No regression or machine-load-adjusted trend is claimed.**", markdown)
        with self.subTest(surface="html"):
            text = []
            parser = HTMLParser()
            parser.handle_data = text.append
            parser.feed(document)
            self.assertIn(
                "Largest measured fixture invocation: Fixture **literal** & <b>name</b> "
                "# injected, ClassInitialize 4.00 s "
                "(linux **literal** & <os> / Release / .NETCoreApp 10.0 / X64). "
                "This identifies an investigation target, not a proven regression "
                "or a claim that the work is avoidable.",
                text,
            )
            self.assertIn(
                r"Build IDs: build **label** & <build> # injected. "
                r"Source revisions: `rev` [branch](target) \revision.",
                text,
            )
            self.assertNotIn("<b>", document)
            self.assertNotIn("<build>", document)
            self.assertNotIn("&amp;lt;", document)
        self.assertEqual(fixture, report["fixtures"][0]["owner"])
        self.assertEqual([build], report["coverage"]["builds"])
        self.assertEqual([revision], report["coverage"]["revisions"])
        self.assertEqual(original, json.dumps(report, sort_keys=True))

    def test_rejected_inputs_render_literal_source_and_reason(self):
        from html.parser import HTMLParser

        with tempfile.TemporaryDirectory() as directory:
            path = write_records(pathlib.Path(directory), "trace.jsonl", records())
            report = REPORT.build_report([path])
        source = "archive **literal** [link](target) | & <b>bad</b>\n# extra"
        reason = r"duplicate **key**: _field_ `code` [link](target) \path & <node>"
        report["coverage"]["rejected"].append({"source": source, "reason": reason})
        original = json.dumps(report, sort_keys=True)
        markdown = REPORT.markdown_report(report)
        document = REPORT.html_report(report)
        with self.subTest(surface="markdown"):
            self.assertIn(
                r"- archive \*\*literal\*\* \[link\]\(target\) &#124; &amp; "
                r"&lt;b&gt;bad&lt;/b&gt; \# extra: duplicate \*\*key\*\*: "
                r"\_field\_ \`code\` \[link\]\(target\) \\path &amp; &lt;node&gt;",
                markdown,
            )
            self.assertNotIn("\n# extra", markdown)
        with self.subTest(surface="html"):
            text = []
            parser = HTMLParser()
            parser.handle_data = text.append
            parser.feed(document)
            self.assertIn(
                "Rejected: archive **literal** [link](target) | & <b>bad</b> "
                r"# extra: duplicate **key**: _field_ `code` [link](target) \path & <node>",
                text,
            )
            self.assertNotIn("<b>", document)
            self.assertNotIn("<node>", document)
            self.assertNotIn("&amp;lt;", document)
        self.assertEqual({"source": source, "reason": reason}, report["coverage"]["rejected"][0])
        self.assertEqual(original, json.dumps(report, sort_keys=True))

    def test_cli_writes_real_outputs_and_reports_partial_input_with_nonzero_exit(self):
        with tempfile.TemporaryDirectory() as directory:
            root = pathlib.Path(directory)
            good = write_records(root, "good.jsonl", records())
            output = root / "report"
            command = [sys.executable, str(SCRIPT), "--input", str(good), "--output", str(output)]
            result = subprocess.run(
                command, capture_output=True, text=True, timeout=CLI_TIMEOUT_SECONDS
            )
            self.assertEqual(0, result.returncode, result.stderr)
            self.assertEqual(2, json.loads(output.with_suffix(".json").read_text())["coverage"]["selectedOccurrences"])
            self.assertIn("<table>", output.with_suffix(".html").read_text())
            bad = write_records(root, "bad.jsonl", records("partial")[:-1])
            result = subprocess.run(
                command + ["--input", str(bad)],
                capture_output=True, text=True, timeout=CLI_TIMEOUT_SECONDS,
            )
            self.assertEqual(2, result.returncode, result.stderr)
            parsed = json.loads(output.with_suffix(".json").read_text())
            self.assertEqual(1, len(parsed["coverage"]["rejected"]))
            self.assertEqual(2, parsed["coverage"]["selectedOccurrences"])


if __name__ == "__main__":
    unittest.main()
