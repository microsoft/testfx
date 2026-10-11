# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT license. See LICENSE file in the project root for full license information.

"""Require fresh, complete, passing outer acceptance execution, not just selection."""

import argparse
from collections import Counter
from datetime import datetime
import json
from pathlib import Path
import sys
import xml.etree.ElementTree as ET


TRX_NAMESPACE = "http://microsoft.com/schemas/VisualStudio/TeamTest/2010"
NS = {"t": TRX_NAMESPACE}


def parse_timestamp(value):
    timestamp = datetime.fromisoformat(value.replace("Z", "+00:00"))
    if timestamp.tzinfo is None:
        raise ValueError("Timestamps must include a UTC offset.")
    return timestamp


def verify_execution(trx_path, expected_path, started_after, process_exit_code):
    expected = json.loads(Path(expected_path).read_text(encoding="utf-8-sig"))
    if not isinstance(expected, list) or not expected:
        raise ValueError("Expected tests must be a nonempty JSON array.")
    if any(not isinstance(name, str) or not name.strip() for name in expected):
        raise ValueError("Every expected test must have an exact, nonempty testName.")

    root = ET.parse(trx_path).getroot()
    if root.tag != f"{{{TRX_NAMESPACE}}}TestRun":
        raise ValueError("Expected a TestFx TRX TestRun with the TeamTest/2010 namespace.")
    times = root.find("t:Times", NS)
    if times is None:
        raise ValueError("TRX is missing run timestamps.")
    start = parse_timestamp(times.attrib.get("start", ""))
    finish = parse_timestamp(times.attrib.get("finish", ""))
    if start < parse_timestamp(started_after) or finish < start:
        raise ValueError("TRX run timestamps are stale or inconsistent.")

    results = root.findall("t:Results/t:UnitTestResult", NS)
    outcomes = Counter(result.attrib.get("outcome", "") for result in results)
    report = {
        "trx": str(Path(trx_path).resolve()),
        "planned": len(expected),
        "selected": len(results),
        "executed": outcomes["Passed"] + outcomes["Failed"],
        "passed": outcomes["Passed"],
        "failed": outcomes["Failed"],
        "skipped": outcomes["NotExecuted"],
        "processExitCode": process_exit_code,
        "runStart": start.isoformat(),
        "runFinish": finish.isoformat(),
    }
    counts = ", ".join(
        f"{key}={report[key]}"
        for key in ("planned", "selected", "executed", "passed", "failed", "skipped")
    )
    if process_exit_code != 0:
        raise ValueError(f"Outer acceptance process failed: exit={process_exit_code}; {counts}.")

    names = Counter(result.attrib.get("testName", "") for result in results)
    if names != Counter(expected):
        missing = list((Counter(expected) - names).elements())
        unexpected = list((names - Counter(expected)).elements())
        raise ValueError(
            f"Selected cases differ from the independent plan: missing={missing}, "
            f"unexpected={unexpected}; {counts}."
        )
    execution_ids = [result.attrib.get("executionId", "") for result in results]
    if not all(execution_ids) or len(set(execution_ids)) != len(execution_ids):
        raise ValueError("TRX has missing or duplicate execution IDs.")
    if outcomes != Counter({"Passed": len(expected)}):
        unsuccessful = [
            (result.attrib.get("testName"), result.attrib.get("outcome"))
            for result in results
            if result.attrib.get("outcome") != "Passed"
        ]
        raise ValueError(f"Required cases did not pass: {unsuccessful}; {counts}.")

    summary = root.find("t:ResultSummary", NS)
    if summary is None or summary.attrib.get("outcome") not in ("Completed", "Passed"):
        raise ValueError("TRX is missing a successful run summary.")
    counters = summary.find("t:Counters", NS)
    if counters is None:
        raise ValueError("TRX is missing run counters.")
    expected_counters = {
        "total": len(expected),
        "executed": len(expected),
        "passed": len(expected),
        "failed": 0,
        "notExecuted": 0,
    }
    for name, value in counters.attrib.items():
        actual = int(value)
        if actual != expected_counters.get(name, 0):
            raise ValueError(f"TRX counter {name}={actual} contradicts passing execution.")
    if not expected_counters.keys() <= counters.attrib.keys():
        raise ValueError("TRX is missing required total/executed/passed/failed/notExecuted counters.")
    if any(
        info.attrib.get("outcome") in ("Error", "Failed", "Aborted", "Timeout")
        for info in summary.findall("t:RunInfos/t:RunInfo", NS)
    ):
        raise ValueError("TRX contains a run-level failure.")
    return report


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--trx", required=True, type=Path)
    parser.add_argument("--expected-tests", required=True, type=Path)
    parser.add_argument("--started-after", required=True, help="Run-start lower bound with UTC offset.")
    parser.add_argument("--process-exit-code", required=True, type=int)
    args = parser.parse_args()
    try:
        report = verify_execution(
            args.trx, args.expected_tests, args.started_after, args.process_exit_code
        )
    except (OSError, ValueError, ET.ParseError) as error:
        print(f"Acceptance execution proof failed: {error}", file=sys.stderr)
        return 1
    print(json.dumps(report, indent=2))
    return 0


if __name__ == "__main__":
    sys.exit(main())
