# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT license. See LICENSE file in the project root for full license information.

from datetime import datetime, timedelta, timezone
import json
from pathlib import Path
import subprocess
import sys
import tempfile
import unittest
import xml.etree.ElementTree as ET

from verify_execution import TRX_NAMESPACE, verify_execution


def tag(name):
    return f"{{{TRX_NAMESPACE}}}{name}"


class VerifyExecutionTests(unittest.TestCase):
    def setUp(self):
        self.directory = tempfile.TemporaryDirectory(prefix="testfx-execution-proof-")
        self.addCleanup(self.directory.cleanup)
        self.trx = Path(self.directory.name) / "run.trx"
        self.expected = Path(self.directory.name) / "expected.json"
        self.started = datetime.now(timezone.utc) - timedelta(seconds=1)
        self.root = ET.Element(tag("TestRun"))
        self.times = ET.SubElement(
            self.root, tag("Times"),
            start=self.started.isoformat(), finish=datetime.now(timezone.utc).isoformat(),
        )
        self.results = ET.SubElement(self.root, tag("Results"))
        self.summary = ET.SubElement(self.root, tag("ResultSummary"), outcome="Completed")
        self.counters = ET.SubElement(
            self.summary, tag("Counters"),
            total="1", executed="1", passed="1", failed="0", notExecuted="0", timeout="0",
        )
        self.row = ET.SubElement(
            self.results, tag("UnitTestResult"),
            testName="RequiredRegression", executionId="execution-1", outcome="Passed",
        )
        self.expected.write_text(json.dumps(["RequiredRegression"]), encoding="utf-8")

    def write_trx(self):
        ET.ElementTree(self.root).write(self.trx, encoding="utf-8", xml_declaration=True)

    def verify(self, exit_code=0):
        self.write_trx()
        return verify_execution(self.trx, self.expected, self.started.isoformat(), exit_code)

    def test_passing_execution_reports_distinct_counts(self):
        report = self.verify()
        self.assertEqual(
            (report["planned"], report["selected"], report["executed"],
             report["passed"], report["failed"], report["skipped"]),
            (1, 1, 1, 1, 0, 0),
        )

    def test_repeated_display_names_require_the_planned_multiplicity(self):
        self.expected.write_text(json.dumps(["RequiredRegression"] * 2), encoding="utf-8")
        ET.SubElement(
            self.results, tag("UnitTestResult"),
            testName="RequiredRegression", executionId="execution-2", outcome="Passed",
        )
        self.counters.attrib.update(total="2", executed="2", passed="2")
        self.assertEqual(2, self.verify()["executed"])
        self.results.remove(self.row)
        with self.assertRaisesRegex(ValueError, "Selected cases differ"):
            self.verify()

    def test_failed_skipped_and_unknown_outcomes_never_count_as_proof(self):
        for outcome in ("Failed", "NotExecuted", "Inconclusive", "Timeout", "Aborted", ""):
            with self.subTest(outcome=outcome):
                self.row.set("outcome", outcome)
                with self.assertRaisesRegex(ValueError, "Required cases did not pass"):
                    self.verify()

    def test_all_29_skipped_with_exit_zero_is_rejected(self):
        self.results.clear()
        names = [f"RequiredRegression ({index})" for index in range(29)]
        for index, name in enumerate(names):
            ET.SubElement(
                self.results, tag("UnitTestResult"),
                testName=name, executionId=str(index), outcome="NotExecuted",
            )
        self.expected.write_text(json.dumps(names), encoding="utf-8")
        self.counters.attrib.update(total="29", executed="0", passed="0", notExecuted="29")
        with self.assertRaisesRegex(ValueError, "executed=0, passed=0, failed=0, skipped=29"):
            self.verify()

    def test_same_count_different_identity_is_rejected(self):
        self.row.set("testName", "UnrelatedRegression")
        with self.assertRaisesRegex(ValueError, "Selected cases differ"):
            self.verify()

    def test_empty_selection_and_unexpected_extra_case_are_rejected(self):
        self.results.remove(self.row)
        with self.assertRaisesRegex(ValueError, "Selected cases differ"):
            self.verify()
        self.results.append(self.row)
        ET.SubElement(
            self.results, tag("UnitTestResult"),
            testName="ExtraRegression", executionId="execution-2", outcome="Passed",
        )
        with self.assertRaisesRegex(ValueError, "Selected cases differ"):
            self.verify()

    def test_nonzero_parent_exit_is_rejected_despite_passing_rows(self):
        with self.assertRaisesRegex(ValueError, "exit=8"):
            self.verify(exit_code=8)

    def test_missing_and_duplicate_execution_ids_are_rejected(self):
        self.row.attrib.pop("executionId")
        with self.assertRaisesRegex(ValueError, "execution IDs"):
            self.verify()
        self.row.set("executionId", "duplicate")
        self.expected.write_text(json.dumps(["RequiredRegression"] * 2), encoding="utf-8")
        self.results.append(ET.fromstring(ET.tostring(self.row)))
        with self.assertRaisesRegex(ValueError, "execution IDs"):
            self.verify()

    def test_missing_inconsistent_and_failure_counters_are_rejected(self):
        original = dict(self.counters.attrib)
        for change in ({"executed": "0"}, {"total": "29"}, {"timeout": "1"},
                       {"failed": "-1"}, {"passed": "not-an-integer"}):
            with self.subTest(change=change):
                self.counters.attrib.clear()
                self.counters.attrib.update(original)
                self.counters.attrib.update(change)
                with self.assertRaises(ValueError):
                    self.verify()
        self.counters.attrib.clear()
        with self.assertRaisesRegex(ValueError, "missing required"):
            self.verify()

    def test_stale_inverted_naive_and_missing_timestamps_are_rejected(self):
        original = dict(self.times.attrib)
        for change in (
            {"start": (self.started - timedelta(days=1)).isoformat()},
            {"finish": (self.started - timedelta(seconds=1)).isoformat()},
            {"start": "2026-01-01T00:00:00"},
            {"start": ""},
        ):
            with self.subTest(change=change):
                self.times.attrib.clear()
                self.times.attrib.update(original)
                self.times.attrib.update(change)
                with self.assertRaises(ValueError):
                    self.verify()
        self.root.remove(self.times)
        with self.assertRaisesRegex(ValueError, "missing run timestamps"):
            self.verify()

    def test_missing_failed_summary_and_run_level_error_are_rejected(self):
        self.summary.set("outcome", "Failed")
        with self.assertRaisesRegex(ValueError, "successful run summary"):
            self.verify()
        self.summary.set("outcome", "Completed")
        infos = ET.SubElement(self.summary, tag("RunInfos"))
        ET.SubElement(infos, tag("RunInfo"), outcome="Error")
        with self.assertRaisesRegex(ValueError, "run-level failure"):
            self.verify()
        self.root.remove(self.summary)
        with self.assertRaisesRegex(ValueError, "successful run summary"):
            self.verify()

    def test_invalid_and_empty_plans_are_rejected(self):
        for plan in ([], {}, "RequiredRegression", [""], [" "], [123], [None]):
            with self.subTest(plan=plan):
                self.expected.write_text(json.dumps(plan), encoding="utf-8")
                with self.assertRaises(ValueError):
                    self.verify()

    def test_bom_plan_and_z_timestamps_are_supported(self):
        self.expected.write_text(json.dumps(["RequiredRegression"]), encoding="utf-8-sig")
        self.times.set("start", self.started.isoformat().replace("+00:00", "Z"))
        self.assertEqual(1, self.verify()["passed"])

    def test_wrong_namespace_is_rejected(self):
        self.root.tag = "TestRun"
        with self.assertRaisesRegex(ValueError, "namespace"):
            self.verify()

    def test_malformed_input_and_missing_counter_element_are_rejected(self):
        self.write_trx()
        self.trx.write_text("<incomplete", encoding="utf-8")
        with self.assertRaises(ET.ParseError):
            verify_execution(self.trx, self.expected, self.started.isoformat(), 0)
        self.expected.write_text("[incomplete", encoding="utf-8")
        with self.assertRaises(json.JSONDecodeError):
            self.verify()
        self.expected.write_text(json.dumps(["RequiredRegression"]), encoding="utf-8")
        self.summary.remove(self.counters)
        with self.assertRaisesRegex(ValueError, "missing run counters"):
            self.verify()

    def test_cli_success_failure_and_missing_input_have_truthful_exits(self):
        self.write_trx()
        command = [
            sys.executable, str(Path(__file__).with_name("verify_execution.py")),
            "--trx", str(self.trx), "--expected-tests", str(self.expected),
            "--started-after", self.started.isoformat(), "--process-exit-code",
        ]
        passed = subprocess.run(command + ["0"], capture_output=True, text=True, check=False)
        self.assertEqual(0, passed.returncode, passed.stderr)
        self.assertEqual(1, json.loads(passed.stdout)["executed"])
        failed = subprocess.run(command + ["1"], capture_output=True, text=True, check=False)
        self.assertEqual(1, failed.returncode)
        self.assertIn("Outer acceptance process failed", failed.stderr)
        self.trx.unlink()
        missing = subprocess.run(command + ["0"], capture_output=True, text=True, check=False)
        self.assertEqual(1, missing.returncode)
        self.assertIn("Acceptance execution proof failed", missing.stderr)
        self.assertEqual("", missing.stdout)


if __name__ == "__main__":
    unittest.main()
