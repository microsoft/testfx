#!/usr/bin/env python3

import importlib.util
import json
import pathlib
import tempfile
import unittest


SCRIPT_PATH = pathlib.Path(__file__).with_name("mutation_testing.py")
REPOSITORY_ROOT = SCRIPT_PATH.parents[2]
SPEC = importlib.util.spec_from_file_location("mutation_testing", SCRIPT_PATH)
assert SPEC is not None
assert SPEC.loader is not None
MUTATION_TESTING = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(MUTATION_TESTING)


def module(module_id: str, *, legacy: bool = False) -> dict:
    return {
        "id": module_id,
        "displayName": module_id.replace("-", " ").title(),
        "project": f"src/{module_id}/{module_id}.csproj",
        "testProjects": [f"test/{module_id}.Tests/{module_id}.Tests.csproj"],
        "timeoutMinutes": 360,
        "enabled": True,
        "legacyReport": legacy,
    }


def manifest(*modules: dict) -> dict:
    return {
        "schemaVersion": 1,
        "knownTargetCount": len(modules),
        "modules": list(modules),
    }


def write_mutation_report(path: pathlib.Path, statuses: list[str]) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(
        json.dumps(
            {
                "files": {
                    "src/Example.cs": {
                        "mutants": [
                            {"id": str(index), "status": status}
                            for index, status in enumerate(statuses)
                        ]
                    }
                }
            }
        ),
        encoding="utf-8",
    )


class MutationTestingTests(unittest.TestCase):
    def test_repository_manifest_onboards_every_mtp_extension(self) -> None:
        target_manifest = MUTATION_TESTING.load_manifest(
            REPOSITORY_ROOT / "eng" / "mutation-testing" / "modules.json"
        )
        configured_projects = {
            target["project"]
            for target in target_manifest["modules"]
            if target["project"].startswith(
                "src/Platform/Microsoft.Testing.Extensions."
            )
        }
        extension_projects = {
            project.relative_to(REPOSITORY_ROOT).as_posix()
            for project in REPOSITORY_ROOT.glob(
                "src/Platform/Microsoft.Testing.Extensions.*/*.csproj"
            )
        }

        self.assertEqual(extension_projects, configured_projects)

    def test_load_manifest_requires_one_legacy_module(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            path = pathlib.Path(directory) / "manifest.json"
            path.write_text(
                json.dumps(manifest(module("one"), module("two"))),
                encoding="utf-8",
            )

            with self.assertRaisesRegex(ValueError, "Exactly one"):
                MUTATION_TESTING.load_manifest(path)

    def test_build_matrix_validates_and_returns_enabled_modules(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            root = pathlib.Path(directory)
            enabled = module("enabled", legacy=True)
            disabled = module("disabled")
            disabled["enabled"] = False
            for path in [
                enabled["project"],
                *enabled["testProjects"],
                disabled["project"],
                *disabled["testProjects"],
            ]:
                project = root / path
                project.parent.mkdir(parents=True, exist_ok=True)
                project.touch()

            matrix = MUTATION_TESTING.build_matrix(
                manifest(enabled, disabled),
                root,
            )

            self.assertEqual([enabled], matrix["include"])

    def test_write_solution_contains_source_and_test_projects(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            output = pathlib.Path(directory) / "MutationTesting.generated.slnx"
            target = module("example", legacy=True)

            MUTATION_TESTING.write_solution(target, output)

            self.assertEqual(
                [
                    "<Solution>",
                    '  <Project Path="src/example/example.csproj" />',
                    '  <Project Path="test/example.Tests/example.Tests.csproj" />',
                    "</Solution>",
                ],
                output.read_text(encoding="utf-8").splitlines(),
            )

    def test_record_module_computes_stryker_score(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            artifact_dir = pathlib.Path(directory)
            write_mutation_report(
                artifact_dir / "reports" / "mutation-report.json",
                ["Killed", "Timeout", "Survived", "NoCoverage", "CompileError"],
            )

            result = MUTATION_TESTING.record_module(
                manifest(module("example", legacy=True)),
                "example",
                artifact_dir,
                "abc123",
                0,
                42,
            )

            self.assertEqual("success", result["status"])
            self.assertAlmostEqual(50.0, result["mutationScore"])
            self.assertEqual(1, result["counts"]["CompileError"])
            self.assertTrue((artifact_dir / "module-result.json").is_file())

    def test_aggregate_results_sums_counts_and_copies_reports(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            root = pathlib.Path(directory)
            artifacts = root / "artifacts"
            first_dir = artifacts / "mutation-testing-module-first"
            second_dir = artifacts / "mutation-testing-module-second"
            target_manifest = manifest(
                module("first", legacy=True),
                module("second"),
            )
            write_mutation_report(
                first_dir / "reports" / "mutation-report.json",
                ["Killed", "Survived"],
            )
            write_mutation_report(
                second_dir / "reports" / "mutation-report.json",
                ["Killed", "Timeout", "NoCoverage"],
            )
            MUTATION_TESTING.record_module(
                target_manifest, "first", first_dir, "abc123", 0, 10
            )
            MUTATION_TESTING.record_module(
                target_manifest, "second", second_dir, "abc123", 0, 20
            )

            output = root / "full"
            report = MUTATION_TESTING.aggregate_results(
                target_manifest,
                artifacts,
                output,
                "abc123",
            )

            self.assertTrue(report["complete"])
            self.assertEqual(2, report["totals"]["Killed"])
            self.assertEqual(1, report["totals"]["Timeout"])
            self.assertAlmostEqual(60.0, report["mutationScore"])
            self.assertTrue(
                (
                    output
                    / "modules"
                    / "first"
                    / "reports"
                    / "mutation-report.json"
                ).is_file()
            )

    def test_aggregate_results_marks_missing_module_as_partial(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            root = pathlib.Path(directory)
            target_manifest = manifest(
                module("first", legacy=True),
                module("second"),
            )
            first_dir = root / "artifacts" / "mutation-testing-module-first"
            write_mutation_report(
                first_dir / "reports" / "mutation-report.json",
                ["Killed"],
            )
            MUTATION_TESTING.record_module(
                target_manifest, "first", first_dir, "abc123", 0, 10
            )

            report = MUTATION_TESTING.aggregate_results(
                target_manifest,
                root / "artifacts",
                root / "full",
                "abc123",
            )

            self.assertFalse(report["complete"])
            self.assertEqual(1, report["successfulModules"])
            self.assertIn("second", report["problems"][0])

    def test_aggregate_results_excludes_commit_mismatch_from_totals(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            root = pathlib.Path(directory)
            artifacts = root / "artifacts"
            matching_dir = artifacts / "mutation-testing-module-matching"
            mismatched_dir = artifacts / "mutation-testing-module-mismatched"
            target_manifest = manifest(
                module("matching", legacy=True),
                module("mismatched"),
            )
            write_mutation_report(
                matching_dir / "reports" / "mutation-report.json",
                ["Killed"],
            )
            write_mutation_report(
                mismatched_dir / "reports" / "mutation-report.json",
                ["Survived"],
            )
            MUTATION_TESTING.record_module(
                target_manifest, "matching", matching_dir, "abc123", 0, 10
            )
            MUTATION_TESTING.record_module(
                target_manifest, "mismatched", mismatched_dir, "different", 0, 20
            )

            report = MUTATION_TESTING.aggregate_results(
                target_manifest,
                artifacts,
                root / "full",
                "abc123",
            )

            self.assertFalse(report["complete"])
            self.assertEqual(1, report["successfulModules"])
            self.assertEqual(1, report["totals"]["Killed"])
            self.assertEqual(0, report["totals"]["Survived"])
            mismatched = next(
                module_result
                for module_result in report["modules"]
                if module_result["id"] == "mismatched"
            )
            self.assertEqual("commit-mismatch", mismatched["status"])
            self.assertIn("different", report["problems"][0])
            self.assertIn("abc123", report["problems"][0])


if __name__ == "__main__":
    unittest.main()
