#!/usr/bin/env python3

"""Compare performance timings with a rolling baseline."""

import argparse
import json
import math
import re
import statistics
from datetime import datetime, timezone
from pathlib import Path


SCHEMA_VERSION = 3
TIME_PATTERN = re.compile(
    r"^(?:(?P<days>\d+)\.)?(?P<hours>\d{2}):(?P<minutes>\d{2}):"
    r"(?P<seconds>\d{2})(?:\.(?P<fraction>\d{1,7}))?$"
)


def parse_duration(value: str) -> float:
    match = TIME_PATTERN.fullmatch(value)
    if match is None:
        raise ValueError(f"Invalid TimeSpan value: {value!r}")

    days = int(match.group("days") or 0)
    hours = int(match.group("hours"))
    minutes = int(match.group("minutes"))
    seconds = int(match.group("seconds"))
    fraction = (match.group("fraction") or "").ljust(7, "0")
    ticks = int(fraction or 0)
    return days * 86400 + hours * 3600 + minutes * 60 + seconds + ticks / 10_000_000


def load_current_results(
    current_dir: Path,
) -> tuple[dict[str, dict[str, float]], dict[str, object]]:
    pipelines: dict[str, dict[str, float]] = {}
    environment: dict[str, object] | None = None
    result_files = sorted(current_dir.rglob("Result.json"))
    if not result_files:
        raise ValueError(f"No Result.json files found under {current_dir}")

    for result_file in result_files:
        report = json.loads(result_file.read_text(encoding="utf-8-sig"))
        if not isinstance(report, dict):
            raise ValueError(f"{result_file} does not contain a named performance report")

        pipeline_name = report.get("Pipeline")
        if pipeline_name is None:
            pipeline_name = report.get("PipelineName")
        if not isinstance(pipeline_name, str) or not pipeline_name:
            raise ValueError(f"{result_file} has no pipeline name")
        if pipeline_name in pipelines:
            raise ValueError(f"Duplicate performance report for {pipeline_name}")

        summary = report.get("Summary")
        if isinstance(summary, dict):
            elapsed_milliseconds = summary.get("MedianElapsedMilliseconds")
            processor_milliseconds = summary.get("MedianProcessorMilliseconds")
            if (
                not isinstance(elapsed_milliseconds, (int, float))
                or isinstance(elapsed_milliseconds, bool)
                or not math.isfinite(elapsed_milliseconds)
                or elapsed_milliseconds <= 0
            ):
                raise ValueError(f"{result_file} has an invalid elapsed-time median")
            metrics = {"elapsedTimeSeconds": elapsed_milliseconds / 1000}
            if processor_milliseconds is not None:
                if (
                    not isinstance(processor_milliseconds, (int, float))
                    or isinstance(processor_milliseconds, bool)
                    or not math.isfinite(processor_milliseconds)
                    or processor_milliseconds <= 0
                ):
                    raise ValueError(f"{result_file} has an invalid CPU-time median")
                metrics["totalProcessorTimeSeconds"] = processor_milliseconds / 1000

            report_environment = {
                "operatingSystem": report.get("OperatingSystem"),
                "processArchitecture": report.get("ProcessArchitecture"),
                "processorCount": report.get("ProcessorCount"),
                "workerCount": report.get("WorkerCount"),
                "runnerRuntimeVersion": report.get("RunnerRuntimeVersion"),
                "targetFramework": report.get("TargetFramework"),
                "configuration": report.get("Configuration"),
                "ciImage": report.get("CiImage"),
                "ciImageVersion": report.get("CiImageVersion"),
            }
            validate_environment(report_environment, str(result_file))
            if environment is None:
                environment = report_environment
            elif environment != report_environment:
                raise ValueError(
                    f"{result_file} environment does not match the other current results"
                )

            pipelines[pipeline_name] = metrics
            continue

        measurements = report.get("Measurements")
        if not isinstance(measurements, list) or not measurements:
            raise ValueError(f"{result_file} has neither Summary nor Measurements")

        elapsed_times: list[float] = []
        processor_times: list[float] = []
        legacy_processor_counts: set[int] = set()
        for measurement in measurements:
            if not isinstance(measurement, dict):
                raise ValueError(f"{result_file} contains an invalid measurement")

            elapsed_times.append(parse_duration(measurement["ElapsedTime"]))
            processor_times.append(parse_duration(measurement["TotalProcessorTime"]))
            legacy_processor_counts.add(int(measurement["ProcessorCount"]))

        if len(legacy_processor_counts) != 1:
            raise ValueError(
                f"{result_file} contains multiple processor counts: "
                f"{sorted(legacy_processor_counts)}"
            )
        legacy_processor_count = legacy_processor_counts.pop()
        report_environment = {
            "operatingSystem": "unknown",
            "processArchitecture": "unknown",
            "processorCount": legacy_processor_count,
            "workerCount": legacy_processor_count,
            "runnerRuntimeVersion": "unknown",
            "targetFramework": "unknown",
            "configuration": "unknown",
            "ciImage": None,
            "ciImageVersion": None,
        }
        if environment is None:
            environment = report_environment
        elif environment != report_environment:
            raise ValueError(
                f"{result_file} environment does not match the other current results"
            )

        pipelines[pipeline_name] = {
            "elapsedTimeSeconds": statistics.median(elapsed_times),
            "totalProcessorTimeSeconds": statistics.median(processor_times),
        }

    if environment is None:
        raise ValueError(f"No usable Result.json files found under {current_dir}")

    return pipelines, environment


def validate_environment(environment: dict[str, object], source: str) -> None:
    processor_count = environment.get("processorCount")
    if not isinstance(processor_count, int) or processor_count <= 0:
        raise ValueError(f"{source} has an invalid processor count")
    worker_count = environment.get("workerCount")
    if not isinstance(worker_count, int) or worker_count <= 0:
        raise ValueError(f"{source} has an invalid worker count")

    for field in (
        "operatingSystem",
        "processArchitecture",
        "runnerRuntimeVersion",
        "targetFramework",
        "configuration",
    ):
        value = environment.get(field)
        if not isinstance(value, str) or not value:
            raise ValueError(f"{source} has an invalid {field}")

    for field in ("ciImage", "ciImageVersion"):
        value = environment.get(field)
        if value is not None and (not isinstance(value, str) or not value):
            raise ValueError(f"{source} has an invalid {field}")


def load_baseline(path: Path) -> list[dict]:
    if not path.is_file():
        return []

    try:
        baseline = json.loads(path.read_text(encoding="utf-8"))
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

            environment = run.get("environment")
            if not isinstance(environment, dict):
                raise ValueError(f"Baseline run {run_index} has no environment")
            validate_environment(environment, f"Baseline run {run_index}")

            pipelines = run.get("pipelines")
            if not isinstance(pipelines, dict):
                raise ValueError(f"Baseline run {run_index} pipelines must be an object")
            for pipeline_name, metrics in pipelines.items():
                if not isinstance(pipeline_name, str) or not pipeline_name:
                    raise ValueError(
                        f"Baseline run {run_index} has an invalid pipeline name"
                    )
                if not isinstance(metrics, dict):
                    raise ValueError(
                        f"Baseline metrics for {pipeline_name} must be an object"
                    )
                for metric_name in ("elapsedTimeSeconds",):
                    metric = metrics.get(metric_name)
                    if (
                        not isinstance(metric, (int, float))
                        or isinstance(metric, bool)
                        or not math.isfinite(metric)
                        or metric <= 0
                    ):
                        raise ValueError(
                            f"Baseline metric {pipeline_name}.{metric_name} is invalid"
                        )
                processor_time = metrics.get("totalProcessorTimeSeconds")
                if processor_time is not None and (
                    not isinstance(processor_time, (int, float))
                    or isinstance(processor_time, bool)
                    or not math.isfinite(processor_time)
                    or processor_time <= 0
                ):
                    raise ValueError(
                        f"Baseline metric {pipeline_name}.totalProcessorTimeSeconds "
                        "is invalid"
                    )
    except (OSError, json.JSONDecodeError, TypeError, ValueError) as error:
        message = f"Ignoring invalid baseline {path}: {error}"
        escaped = (
            message.replace("%", "%25").replace("\r", "%0D").replace("\n", "%0A")
        )
        print(f"::warning title=Invalid performance baseline::{escaped}")
        return []

    return runs


def metric_baseline(
    runs: list[dict],
    pipeline_name: str,
    metric_name: str,
    environment: dict[str, object],
) -> list[float]:
    values = []
    for run in runs:
        if run.get("environment") != environment:
            continue

        pipeline = run.get("pipelines", {}).get(pipeline_name)
        if isinstance(pipeline, dict) and metric_name in pipeline:
            values.append(float(pipeline[metric_name]))

    return values


def format_seconds(value: float | None) -> str:
    return "n/a" if value is None else f"{value:.3f}s"


def format_change(value: float | None) -> str:
    return "n/a" if value is None else f"{value:+.1f}%"


def build_summary(
    pipelines: dict[str, dict[str, float]],
    runs: list[dict],
    environment: dict[str, object],
    threshold_percent: float,
    minimum_baseline_runs: int,
) -> tuple[str, list[str]]:
    lines = [
        "## PlainProcess performance regression report",
        "",
        f"Rolling baseline threshold: **>{threshold_percent:g}%**; "
        f"environment: **{environment['operatingSystem']} / "
        f"{environment['processArchitecture']} / "
        f"{environment['processorCount']} processors / "
        f"{environment['workerCount']} workers / "
        f"{environment['targetFramework']} / "
        f"{environment['configuration']}**.",
        "",
        "| Pipeline | Wall clock | Baseline | Change | CPU time | Baseline | Change | Status |",
        "| --- | ---: | ---: | ---: | ---: | ---: | ---: | --- |",
    ]
    regressions = []

    for pipeline_name in sorted(pipelines):
        current = pipelines[pipeline_name]
        elapsed_values = metric_baseline(
            runs, pipeline_name, "elapsedTimeSeconds", environment
        )
        current_processor_time = current.get("totalProcessorTimeSeconds")
        processor_values = (
            metric_baseline(
                runs, pipeline_name, "totalProcessorTimeSeconds", environment
            )
            if current_processor_time is not None
            else []
        )
        baseline_count = len(elapsed_values)

        if baseline_count < minimum_baseline_runs:
            elapsed_baseline = None
            processor_baseline = None
            elapsed_change = None
            processor_change = None
            status = f"Collecting baseline ({baseline_count}/{minimum_baseline_runs})"
        else:
            elapsed_baseline = statistics.median(elapsed_values)
            elapsed_change = (
                current["elapsedTimeSeconds"] / elapsed_baseline - 1
            ) * 100
            if (
                current_processor_time is not None
                and len(processor_values) >= minimum_baseline_runs
            ):
                processor_baseline = statistics.median(processor_values)
                processor_change = (
                    current_processor_time / processor_baseline - 1
                ) * 100
            else:
                processor_baseline = None
                processor_change = None
            regressed_metrics = []
            if elapsed_change > threshold_percent:
                regressed_metrics.append(f"wall clock {elapsed_change:+.1f}%")
            if (
                processor_change is not None
                and processor_change > threshold_percent
            ):
                regressed_metrics.append(f"CPU time {processor_change:+.1f}%")

            if regressed_metrics:
                status = "Regression"
                regressions.append(f"{pipeline_name}: {', '.join(regressed_metrics)}")
            else:
                status = "Within threshold"

        lines.append(
            f"| `{pipeline_name}` "
            f"| {format_seconds(current['elapsedTimeSeconds'])} "
            f"| {format_seconds(elapsed_baseline)} "
            f"| {format_change(elapsed_change)} "
            f"| {format_seconds(current_processor_time)} "
            f"| {format_seconds(processor_baseline)} "
            f"| {format_change(processor_change)} "
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
    environment: dict[str, object],
    pipelines: dict[str, dict[str, float]],
    window_size: int,
) -> None:
    runs = [run for run in prior_runs if str(run.get("runId")) != run_id]
    runs.append(
        {
            "runId": run_id,
            "createdAt": created_at,
            "environment": environment,
            "pipelines": pipelines,
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
    parser.add_argument("--threshold-percent", type=float, default=10.0)
    parser.add_argument("--window-size", type=int, default=7)
    parser.add_argument("--minimum-baseline-runs", type=int, default=3)
    args = parser.parse_args()

    if args.window_size < args.minimum_baseline_runs:
        parser.error("--window-size must be at least --minimum-baseline-runs")

    pipelines, environment = load_current_results(args.current_dir)
    prior_runs = [
        run
        for run in load_baseline(args.baseline)
        if str(run.get("runId")) != args.run_id
    ]
    summary, regressions = build_summary(
        pipelines,
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
        pipelines,
        args.window_size,
    )

    for regression in regressions:
        escaped = regression.replace("%", "%25").replace("\r", "%0D").replace("\n", "%0A")
        print(f"::warning title=Performance regression::{escaped}")

    return 0


if __name__ == "__main__":
    raise SystemExit(main())
