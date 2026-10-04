#!/usr/bin/env python3

"""Compare BenchmarkDotNet results with a rolling allocation baseline."""

import argparse
import json
import math
import statistics
import zipfile
import zlib
from datetime import datetime, timezone
from pathlib import Path


SCHEMA_VERSION = 1
REPORT_PATTERN = "*-report-full-compressed.json"
MAX_BASELINE_BYTES = 10 * 1024 * 1024


def validate_metric(value: object, source: str, name: str, *, allow_zero: bool) -> float:
    if (
        not isinstance(value, (int, float))
        or isinstance(value, bool)
        or not math.isfinite(value)
        or value < 0
        or (not allow_zero and value == 0)
    ):
        raise ValueError(f"{source} has an invalid {name}")
    return float(value)


def escape_workflow_command(value: str) -> str:
    return (
        value.replace("%", "%25")
        .replace("\r", "%0D")
        .replace("\n", "%0A")
        .replace(":", "%3A")
        .replace(",", "%2C")
    )


def load_current_results(
    current_dir: Path,
) -> tuple[dict[str, dict[str, float]], dict[str, object] | None]:
    benchmarks: dict[str, dict[str, float]] = {}
    environment: dict[str, object] | None = None
    report_files = sorted(current_dir.rglob(REPORT_PATTERN))

    for report_file in report_files:
        report = json.loads(report_file.read_text(encoding="utf-8-sig"))
        if not isinstance(report, dict):
            raise ValueError(f"{report_file} does not contain a BenchmarkDotNet report")

        report_environment = report.get("HostEnvironmentInfo")
        if not isinstance(report_environment, dict) or not report_environment:
            raise ValueError(f"{report_file} has no host environment information")
        if environment is None:
            environment = report_environment
        elif environment != report_environment:
            raise ValueError(
                f"{report_file} environment does not match the other current reports"
            )

        report_benchmarks = report.get("Benchmarks")
        if not isinstance(report_benchmarks, list) or not report_benchmarks:
            raise ValueError(f"{report_file} has no benchmarks")

        for benchmark in report_benchmarks:
            if not isinstance(benchmark, dict):
                raise ValueError(f"{report_file} contains an invalid benchmark")
            full_name = benchmark.get("FullName")
            if not isinstance(full_name, str) or not full_name:
                raise ValueError(f"{report_file} contains a benchmark without a full name")
            if full_name in benchmarks:
                raise ValueError(f"Duplicate BenchmarkDotNet result for {full_name}")

            statistics_report = benchmark.get("Statistics")
            memory_report = benchmark.get("Memory")
            if not isinstance(statistics_report, dict) or not isinstance(memory_report, dict):
                raise ValueError(f"{report_file} has incomplete metrics for {full_name}")

            benchmarks[full_name] = {
                "meanTimeNanoseconds": validate_metric(
                    statistics_report.get("Mean"),
                    str(report_file),
                    f"{full_name} mean time",
                    allow_zero=False,
                ),
                "bytesAllocatedPerOperation": validate_metric(
                    memory_report.get("BytesAllocatedPerOperation"),
                    str(report_file),
                    f"{full_name} allocated bytes",
                    allow_zero=True,
                ),
            }

    return benchmarks, environment


def load_baseline(path: Path) -> list[dict]:
    if not path.is_file():
        return []

    try:
        if path.suffix.lower() == ".zip":
            with zipfile.ZipFile(path) as archive:
                baseline_files = [
                    member
                    for member in archive.infolist()
                    if member.filename == "Baseline.json" and not member.is_dir()
                ]
                if len(baseline_files) != 1:
                    raise ValueError("Baseline archive must contain one Baseline.json file")
                baseline_file = baseline_files[0]
                if baseline_file.file_size > MAX_BASELINE_BYTES:
                    raise ValueError("Baseline file exceeds the size limit")
                with archive.open(baseline_file) as baseline_stream:
                    baseline_contents = baseline_stream.read(MAX_BASELINE_BYTES + 1)
                if len(baseline_contents) > MAX_BASELINE_BYTES:
                    raise ValueError("Baseline file exceeds the size limit")
                baseline = json.loads(baseline_contents.decode("utf-8-sig"))
        else:
            if path.stat().st_size > MAX_BASELINE_BYTES:
                raise ValueError("Baseline file exceeds the size limit")
            baseline = json.loads(path.read_text(encoding="utf-8-sig"))
        if not isinstance(baseline, dict):
            raise ValueError("Baseline root must be an object")
        if baseline.get("schemaVersion") != SCHEMA_VERSION:
            raise ValueError(
                f"Unsupported baseline schema version: {baseline.get('schemaVersion')!r}"
            )

        runs = baseline.get("runs")
        if not isinstance(runs, list):
            raise ValueError("Baseline runs must be an array")
        for run_index, run in enumerate(runs):
            if not isinstance(run, dict):
                raise ValueError(f"Baseline run {run_index} must be an object")
            if not isinstance(run.get("environment"), dict):
                raise ValueError(f"Baseline run {run_index} has no environment")

            run_benchmarks = run.get("benchmarks")
            if not isinstance(run_benchmarks, dict):
                raise ValueError(f"Baseline run {run_index} benchmarks must be an object")
            for full_name, metrics in run_benchmarks.items():
                if not isinstance(full_name, str) or not full_name:
                    raise ValueError(f"Baseline run {run_index} has an invalid benchmark name")
                if not isinstance(metrics, dict):
                    raise ValueError(f"Baseline metrics for {full_name} must be an object")
                for metric_name, allow_zero in (
                    ("meanTimeNanoseconds", False),
                    ("bytesAllocatedPerOperation", True),
                ):
                    validate_metric(
                        metrics.get(metric_name),
                        f"Baseline run {run_index}",
                        f"{full_name}.{metric_name}",
                        allow_zero=allow_zero,
                    )
    except (OSError, json.JSONDecodeError, TypeError, ValueError) as error:
        message = f"Ignoring invalid benchmark baseline {path}: {error}"
        escaped = escape_workflow_command(message)
        print(f"::warning title=Invalid benchmark baseline::{escaped}")
        return []
    except (
        UnicodeDecodeError,
        RuntimeError,
        zipfile.BadZipFile,
        EOFError,
        zlib.error,
        NotImplementedError,
    ) as error:
        message = f"Ignoring invalid benchmark baseline {path}: {error}"
        print(f"::warning title=Invalid benchmark baseline::{escape_workflow_command(message)}")
        return []

    return runs


def metric_baseline(
    runs: list[dict],
    full_name: str,
    metric_name: str,
    environment: dict[str, object],
) -> list[float]:
    values = []
    for run in runs:
        if run.get("environment") != environment:
            continue

        metrics = run.get("benchmarks", {}).get(full_name)
        if isinstance(metrics, dict) and metric_name in metrics:
            values.append(float(metrics[metric_name]))

    return values


def metric_change(current: float, baseline: float) -> float:
    if baseline == 0:
        return 0.0 if current == 0 else math.inf
    return (current / baseline - 1) * 100


def format_value(value: float | None, suffix: str) -> str:
    return "n/a" if value is None else f"{value:.2f}{suffix}"


def format_change(value: float | None) -> str:
    if value is None:
        return "n/a"
    if math.isinf(value):
        return "+∞%"
    return f"{value:+.1f}%"


def build_summary(
    benchmarks: dict[str, dict[str, float]],
    runs: list[dict],
    environment: dict[str, object] | None,
    threshold_percent: float,
    minimum_baseline_runs: int,
) -> tuple[str, list[str]]:
    lines = [
        "## Allocation microbenchmark regression report",
        "",
        f"Rolling baseline threshold: **>{threshold_percent:g}%**.",
        "",
    ]
    if not benchmarks:
        lines.extend(["No BenchmarkDotNet JSON reports were found for this run.", ""])
        return "\n".join(lines), []

    assert environment is not None
    lines.extend(
        [
            f"Environment: `{json.dumps(environment, sort_keys=True)}`.",
            "",
            "| Benchmark | Mean time | Baseline | Change | Allocated bytes | Baseline | Change | Status |",
            "| --- | ---: | ---: | ---: | ---: | ---: | ---: | --- |",
        ]
    )
    regressions = []

    for full_name in sorted(benchmarks):
        current = benchmarks[full_name]
        metric_values = {
            metric_name: metric_baseline(runs, full_name, metric_name, environment)
            for metric_name in current
        }
        baseline_count = min(len(values) for values in metric_values.values())

        if baseline_count < minimum_baseline_runs:
            baselines = {"meanTimeNanoseconds": None, "bytesAllocatedPerOperation": None}
            changes = {"meanTimeNanoseconds": None, "bytesAllocatedPerOperation": None}
            status = f"Collecting baseline ({baseline_count}/{minimum_baseline_runs})"
        else:
            baselines = {
                metric_name: statistics.median(values)
                for metric_name, values in metric_values.items()
            }
            changes = {
                metric_name: metric_change(current[metric_name], baselines[metric_name])
                for metric_name in current
            }
            regressed_metrics = [
                f"{label} {changes[metric_name]:+.1f}%"
                if not math.isinf(changes[metric_name])
                else f"{label} increased from zero"
                for metric_name, label in (
                    ("meanTimeNanoseconds", "mean time"),
                    ("bytesAllocatedPerOperation", "allocated bytes"),
                )
                if changes[metric_name] > threshold_percent
            ]
            if regressed_metrics:
                status = "Regression"
                regressions.append(f"{full_name}: {', '.join(regressed_metrics)}")
            else:
                status = "Within threshold"

        lines.append(
            f"| `{full_name}` "
            f"| {format_value(current['meanTimeNanoseconds'], ' ns')} "
            f"| {format_value(baselines['meanTimeNanoseconds'], ' ns')} "
            f"| {format_change(changes['meanTimeNanoseconds'])} "
            f"| {format_value(current['bytesAllocatedPerOperation'], ' B/op')} "
            f"| {format_value(baselines['bytesAllocatedPerOperation'], ' B/op')} "
            f"| {format_change(changes['bytesAllocatedPerOperation'])} "
            f"| {status} |"
        )

    if regressions:
        lines.extend(["", "### Regressions", ""])
        lines.extend(f"- {regression}" for regression in regressions)

    lines.append("")
    return "\n".join(lines), regressions


def write_updated_baseline(
    output_path: Path,
    prior_runs: list[dict],
    run_id: str,
    created_at: str,
    environment: dict[str, object] | None,
    benchmarks: dict[str, dict[str, float]],
    window_size: int,
) -> None:
    runs = [run for run in prior_runs if str(run.get("runId")) != run_id]
    if environment is not None and benchmarks:
        runs.append(
            {
                "runId": run_id,
                "createdAt": created_at,
                "environment": environment,
                "benchmarks": benchmarks,
            }
        )
    baseline = {
        "schemaVersion": SCHEMA_VERSION,
        "windowSize": window_size,
        "runs": runs[-window_size:],
    }
    output_path.parent.mkdir(parents=True, exist_ok=True)
    output_path.write_text(
        json.dumps(baseline, indent=2, sort_keys=True) + "\n", encoding="utf-8"
    )


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--current-dir", type=Path, required=True)
    parser.add_argument("--baseline", type=Path, required=True)
    parser.add_argument("--output", type=Path, required=True)
    parser.add_argument("--summary", type=Path, required=True)
    parser.add_argument("--run-id", required=True)
    parser.add_argument(
        "--created-at",
        default=datetime.now(timezone.utc).isoformat().replace("+00:00", "Z"),
    )
    parser.add_argument("--threshold-percent", type=float, default=15.0)
    parser.add_argument("--window-size", type=int, default=7)
    parser.add_argument("--minimum-baseline-runs", type=int, default=3)
    args = parser.parse_args()

    if args.threshold_percent < 0 or not math.isfinite(args.threshold_percent):
        parser.error("--threshold-percent must be a finite non-negative number")
    if args.window_size < 1:
        parser.error("--window-size must be positive")
    if args.minimum_baseline_runs < 1:
        parser.error("--minimum-baseline-runs must be positive")
    if args.window_size < args.minimum_baseline_runs:
        parser.error("--window-size must be at least --minimum-baseline-runs")

    benchmarks, environment = load_current_results(args.current_dir)
    prior_runs = [
        run
        for run in load_baseline(args.baseline)
        if str(run.get("runId")) != args.run_id
    ]
    summary, regressions = build_summary(
        benchmarks,
        prior_runs,
        environment,
        args.threshold_percent,
        args.minimum_baseline_runs,
    )
    args.summary.parent.mkdir(parents=True, exist_ok=True)
    args.summary.write_text(summary, encoding="utf-8")
    write_updated_baseline(
        args.output,
        prior_runs,
        args.run_id,
        args.created_at,
        environment,
        benchmarks,
        args.window_size,
    )

    for regression in regressions:
        escaped = escape_workflow_command(regression)
        print(f"::warning title=Allocation microbenchmark regression::{escaped}")

    return 0


if __name__ == "__main__":
    raise SystemExit(main())
