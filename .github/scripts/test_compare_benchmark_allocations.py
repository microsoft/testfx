#!/usr/bin/env python3

import importlib.util
import json
import pathlib
import tempfile
import unittest
import zipfile


SCRIPT_PATH = pathlib.Path(__file__).with_name("compare_benchmark_allocations.py")
SPEC = importlib.util.spec_from_file_location("compare_benchmark_allocations", SCRIPT_PATH)
assert SPEC is not None
assert SPEC.loader is not None
COMPARE_BENCHMARK_ALLOCATIONS = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(COMPARE_BENCHMARK_ALLOCATIONS)


ENVIRONMENT = {
    "BenchmarkDotNetVersion": "0.15.8",
    "OsVersion": "Linux",
    "RuntimeVersion": "10.0.0",
    "Architecture": "X64",
}


def benchmark_report(
    full_name: str = "Example.Benchmarks.Run",
    mean: float = 100,
    allocated: float = 16,
) -> dict:
    return {
        "HostEnvironmentInfo": ENVIRONMENT,
        "Benchmarks": [
            {
                "FullName": full_name,
                "Statistics": {"Mean": mean},
                "Memory": {"BytesAllocatedPerOperation": allocated},
            }
        ],
    }


class CompareBenchmarkAllocationsTests(unittest.TestCase):
    def test_load_current_results_reads_time_and_allocations(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            report_path = pathlib.Path(directory) / "first-report-full-compressed.json"
            report_path.write_text(json.dumps(benchmark_report()), encoding="utf-8")

            benchmarks, environment = (
                COMPARE_BENCHMARK_ALLOCATIONS.load_current_results(pathlib.Path(directory))
            )

        self.assertEqual(ENVIRONMENT, environment)
        self.assertEqual(
            {
                "meanTimeNanoseconds": 100.0,
                "bytesAllocatedPerOperation": 16.0,
            },
            benchmarks["Example.Benchmarks.Run"],
        )

    def test_load_current_results_rejects_inconsistent_environments(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            root = pathlib.Path(directory)
            (root / "first-report-full-compressed.json").write_text(
                json.dumps(benchmark_report("Example.First")),
                encoding="utf-8",
            )
            changed_environment = {**ENVIRONMENT, "Architecture": "Arm64"}
            second = benchmark_report("Example.Second")
            second["HostEnvironmentInfo"] = changed_environment
            (root / "second-report-full-compressed.json").write_text(
                json.dumps(second),
                encoding="utf-8",
            )

            with self.assertRaisesRegex(ValueError, "environment does not match"):
                COMPARE_BENCHMARK_ALLOCATIONS.load_current_results(root)

    def test_load_baseline_reads_only_baseline_from_archive(self) -> None:
        baseline = {
            "schemaVersion": 1,
            "runs": [
                {
                    "environment": ENVIRONMENT,
                    "benchmarks": {
                        "Example.Benchmarks.Run": {
                            "meanTimeNanoseconds": 100,
                            "bytesAllocatedPerOperation": 16,
                        }
                    },
                }
            ],
        }
        with tempfile.TemporaryDirectory() as directory:
            archive_path = pathlib.Path(directory) / "Baseline.zip"
            with zipfile.ZipFile(archive_path, "w") as archive:
                archive.writestr("Baseline.json", json.dumps(baseline))
                archive.writestr("Summary.md", "summary")

            runs = COMPARE_BENCHMARK_ALLOCATIONS.load_baseline(archive_path)

        self.assertEqual(baseline["runs"], runs)

    def test_build_summary_uses_rolling_median_and_reports_each_regressed_metric(self) -> None:
        runs = [
            {
                "environment": ENVIRONMENT,
                "benchmarks": {
                    "Example.Benchmarks.Run": {
                        "meanTimeNanoseconds": mean,
                        "bytesAllocatedPerOperation": allocated,
                    }
                },
            }
            for mean, allocated in ((100, 16), (110, 16), (120, 16))
        ]

        summary, regressions = COMPARE_BENCHMARK_ALLOCATIONS.build_summary(
            {
                "Example.Benchmarks.Run": {
                    "meanTimeNanoseconds": 156,
                    "bytesAllocatedPerOperation": 20,
                }
            },
            runs,
            ENVIRONMENT,
            15,
            3,
        )

        self.assertIn("| +41.8% |", summary)
        self.assertIn("| +25.0% |", summary)
        self.assertIn("mean time +41.8%", regressions[0])
        self.assertIn("allocated bytes +25.0%", regressions[0])

    def test_build_summary_marks_missing_reports_without_failing(self) -> None:
        summary, regressions = COMPARE_BENCHMARK_ALLOCATIONS.build_summary(
            {}, [], None, 15, 3
        )

        self.assertIn("No BenchmarkDotNet JSON reports were found", summary)
        self.assertEqual([], regressions)

    def test_build_summary_detects_allocation_regression_independently(self) -> None:
        runs = [
            {
                "environment": ENVIRONMENT,
                "benchmarks": {
                    "Example.Benchmarks.Run": {
                        "meanTimeNanoseconds": 100,
                        "bytesAllocatedPerOperation": 16,
                    }
                },
            }
            for _ in range(3)
        ]

        summary, regressions = COMPARE_BENCHMARK_ALLOCATIONS.build_summary(
            {
                "Example.Benchmarks.Run": {
                    "meanTimeNanoseconds": 100,
                    "bytesAllocatedPerOperation": 20,
                }
            },
            runs,
            ENVIRONMENT,
            15,
            3,
        )

        self.assertIn("| +0.0% |", summary)
        self.assertIn("| +25.0% |", summary)
        self.assertIn("allocated bytes +25.0%", regressions[0])
        self.assertNotIn("mean time", regressions[0])

    def test_metric_change_detects_allocation_increase_from_zero(self) -> None:
        self.assertEqual(
            float("inf"),
            COMPARE_BENCHMARK_ALLOCATIONS.metric_change(1, 0),
        )


if __name__ == "__main__":
    unittest.main()
