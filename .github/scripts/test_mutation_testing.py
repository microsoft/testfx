#!/usr/bin/env python3

import importlib.util
import json
import pathlib
import subprocess
import tempfile
import unittest
import xml.etree.ElementTree as ET
from unittest.mock import call, patch


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
            if target["enabled"]
            and target["project"].startswith(
                "src/Platform/Microsoft.Testing.Extensions."
            )
        }
        extension_projects = {
            project.relative_to(REPOSITORY_ROOT).as_posix()
            for project in REPOSITORY_ROOT.glob(
                "src/Platform/Microsoft.Testing.Extensions.*/*.csproj"
            )
        }
        excluded_projects = {
            excluded["project"]
            for excluded in target_manifest.get("excludedProjects", [])
            if excluded["project"].startswith(
                "src/Platform/Microsoft.Testing.Extensions."
            )
        }

        self.assertEqual(extension_projects, configured_projects | excluded_projects)
        self.assertEqual(set(), configured_projects & excluded_projects)

    def test_repository_manifest_covers_every_source_project(self) -> None:
        target_manifest = MUTATION_TESTING.load_manifest(
            REPOSITORY_ROOT / "eng" / "mutation-testing" / "modules.json"
        )
        source_projects = {
            project.relative_to(REPOSITORY_ROOT).as_posix()
            for project in (REPOSITORY_ROOT / "src").rglob("*.csproj")
        }
        manifest_projects = {
            module["project"] for module in target_manifest["modules"]
        } | {
            excluded["project"]
            for excluded in target_manifest.get("excludedProjects", [])
        }

        self.assertEqual(source_projects, manifest_projects)
        self.assertEqual(len(source_projects), target_manifest["knownTargetCount"])

    def test_repository_platform_core_uses_net9_direct_reference_tests(self) -> None:
        target_manifest = MUTATION_TESTING.load_manifest(
            REPOSITORY_ROOT / "eng" / "mutation-testing" / "modules.json"
        )
        matrix = MUTATION_TESTING.build_matrix(target_manifest, REPOSITORY_ROOT)
        core = next(target for target in matrix["include"] if target["id"] == "platform-core")
        direct_test_project = (
            "test/UnitTests/MSTest.SelfRealExamples.UnitTests/"
            "MSTest.SelfRealExamples.UnitTests.csproj"
        )

        self.assertEqual("net9.0", core.get("targetFramework"))
        self.assertIn(direct_test_project, core["testProjects"])
        project = ET.parse(REPOSITORY_ROOT / direct_test_project).getroot()
        frameworks = project.findtext("PropertyGroup/TargetFrameworks")
        self.assertIsNotNone(frameworks)
        self.assertIn(core["targetFramework"], frameworks.split(";"))
        self.assertIn(
            "$(RepoRoot)" + core["project"].replace("/", "\\"),
            [
                reference.get("Include")
                for reference in project.findall("ItemGroup/ProjectReference")
            ],
        )

    def test_workflow_uses_module_target_framework(self) -> None:
        workflow = (
            REPOSITORY_ROOT / ".github" / "workflows" / "mutation-testing.yml"
        ).read_text(encoding="utf-8")
        framework_options = [
            line.strip()
            for line in workflow.splitlines()
            if line.strip().startswith("--target-framework")
        ]

        self.assertEqual(
            ['--target-framework "$MutationTestingTargetFramework" \\'],
            framework_options,
        )
        self.assertIn(
            "MutationTestingTargetFramework: ${{ matrix.targetFramework }}",
            workflow,
        )

    def test_mutation_build_uses_module_target_framework(self) -> None:
        project = ET.parse(REPOSITORY_ROOT / "test" / "Directory.Build.targets").getroot()
        framework = next(
            property
            for property in project.findall("PropertyGroup/TargetFrameworks")
            if property.get("Condition") == "'$(MutationTesting)' == 'true'"
        )

        self.assertEqual("$(MutationTestingTargetFramework)", framework.text)
        default = project.find("PropertyGroup/MutationTestingTargetFramework")
        self.assertIsNotNone(default)
        self.assertEqual("net8.0", default.text)
        self.assertEqual(
            "'$(MutationTesting)' == 'true' AND '$(MutationTestingTargetFramework)' == ''",
            default.get("Condition"),
        )

    def test_mutation_build_retains_mstest_analyzers_from_source(self) -> None:
        project = ET.parse(REPOSITORY_ROOT / "test" / "Directory.Build.targets").getroot()
        analyzer_project = (
            "$(RepoRoot)src\\Analyzers\\MSTest.Analyzers\\MSTest.Analyzers.csproj"
        )
        references = [
            reference.attrib
            for group in project.findall("ItemGroup")
            if group.get("Condition") == "'$(_AddMSTestSourceReferenceForMutation)' == 'true'"
            for reference in group.findall("ProjectReference")
            if reference.get("Include") == analyzer_project
        ]

        self.assertEqual(
            [{
                "Include": analyzer_project,
                "IsTestInfrastructure": "true",
                "PrivateAssets": "all",
                "ReferenceOutputAssembly": "false",
                "OutputItemType": "Analyzer",
            }],
            references,
        )

    def test_mutation_build_delay_signs_platform_core(self) -> None:
        project = ET.parse(REPOSITORY_ROOT / "Directory.Build.targets").getroot()
        condition = (
            "'$(MutationTesting)' == 'true' and "
            "'$(MSBuildProjectName)' == 'Microsoft.Testing.Platform'"
        )
        signing_options = {
            property.tag: property.text
            for group in project.findall("PropertyGroup")
            if group.get("Condition") == condition
            for property in group
            if property.tag in ("PublicSign", "DelaySign")
        }

        self.assertEqual(
            {"PublicSign": "false", "DelaySign": "true"},
            signing_options,
        )

    def test_build_matrix_rejects_unconfigured_source_projects(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            root = pathlib.Path(directory)
            target = module("configured", legacy=True)
            for path in [target["project"], *target["testProjects"]]:
                project = root / path
                project.parent.mkdir(parents=True, exist_ok=True)
                project.touch()
            unconfigured = root / "src" / "unconfigured" / "unconfigured.csproj"
            unconfigured.parent.mkdir(parents=True, exist_ok=True)
            unconfigured.touch()

            with self.assertRaisesRegex(
                ValueError, "unconfigured src projects: src/unconfigured/unconfigured.csproj"
            ):
                MUTATION_TESTING.build_matrix(manifest(target), root)

    def test_build_matrix_rejects_unknown_source_projects(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            root = pathlib.Path(directory)
            target = module("unknown", legacy=True)
            for path in target["testProjects"]:
                project = root / path
                project.parent.mkdir(parents=True, exist_ok=True)
                project.touch()

            with self.assertRaises(ValueError) as error:
                MUTATION_TESTING.build_matrix(manifest(target), root)

            self.assertEqual(
                "Mutation manifest project inventory mismatch "
                "(projects not found under src: src/unknown/unknown.csproj)",
                str(error.exception),
            )

    def test_build_matrix_rejects_duplicate_source_projects(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            root = pathlib.Path(directory)
            target = module("configured", legacy=True)
            duplicate = module("duplicate") | {"project": target["project"]}
            for path in [
                target["project"],
                *target["testProjects"],
                *duplicate["testProjects"],
            ]:
                project = root / path
                project.parent.mkdir(parents=True, exist_ok=True)
                project.touch()

            with self.assertRaises(ValueError) as error:
                MUTATION_TESTING.build_matrix(manifest(target, duplicate), root)

            self.assertEqual(
                "Mutation manifest project inventory mismatch "
                "(duplicate manifest projects: src/configured/configured.csproj)",
                str(error.exception),
            )

    def test_build_matrix_rejects_stale_known_target_count(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            root = pathlib.Path(directory)
            target = module("configured", legacy=True)
            for path in [target["project"], *target["testProjects"]]:
                project = root / path
                project.parent.mkdir(parents=True, exist_ok=True)
                project.touch()

            with self.assertRaisesRegex(ValueError, "knownTargetCount must match"):
                MUTATION_TESTING.build_matrix(
                    manifest(target) | {"knownTargetCount": 2},
                    root,
                )

    def test_load_manifest_validates_explicit_exclusions(self) -> None:
        target = module("one", legacy=True)
        for exclusions, count, message in [
            ([{"project": "excluded.csproj", "reason": ""}], 2, "project and reason"),
            ([{"project": target["project"], "reason": "Unsupported"}], 2, "both configured"),
            ([{"project": "excluded.csproj", "reason": "Unsupported"}], 1, "every module and exclusion"),
            ([{"project": "excluded.csproj", "reason": "Unsupported"}] * 2, 3, "Duplicate excluded"),
        ]:
            with self.subTest(message=message), tempfile.TemporaryDirectory() as directory:
                data = manifest(target) | {
                    "excludedProjects": exclusions,
                    "knownTargetCount": count,
                }
                path = pathlib.Path(directory) / "manifest.json"
                path.write_text(json.dumps(data), encoding="utf-8")
                with self.assertRaisesRegex(ValueError, message):
                    MUTATION_TESTING.load_manifest(path)

    def test_load_manifest_rejects_invalid_target_framework(self) -> None:
        for case, framework in [
            ("null", None),
            ("empty", ""),
            ("whitespace", " "),
            ("leading-whitespace", " net9.0"),
            ("trailing-whitespace", "net9.0 "),
            ("number", 9),
            ("boolean", True),
            ("list", ["net9.0"]),
        ]:
            with self.subTest(case=case), tempfile.TemporaryDirectory() as directory:
                target = module("one", legacy=True) | {"targetFramework": framework}
                path = pathlib.Path(directory) / "manifest.json"
                path.write_text(json.dumps(manifest(target)), encoding="utf-8")

                with self.assertRaisesRegex(ValueError, "invalid targetFramework"):
                    MUTATION_TESTING.load_manifest(path)

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

            self.assertEqual(
                [enabled | {"targetFramework": "net8.0"}],
                matrix["include"],
            )
            self.assertNotIn("targetFramework", enabled)

    def test_build_matrix_uses_module_target_framework(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            root = pathlib.Path(directory)
            target = module("example", legacy=True) | {"targetFramework": "net9.0"}
            for path in [target["project"], *target["testProjects"]]:
                project = root / path
                project.parent.mkdir(parents=True, exist_ok=True)
                project.touch()
            path = root / "manifest.json"
            path.write_text(json.dumps(manifest(target)), encoding="utf-8")

            matrix = MUTATION_TESTING.build_matrix(
                MUTATION_TESTING.load_manifest(path),
                root,
            )

            self.assertEqual([target], matrix["include"])

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

    def test_record_module_retains_pre_stryker_failure_diagnostics(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            root = pathlib.Path(directory)
            artifact_dir = root / "artifacts" / "example"
            target_manifest = manifest(module("example", legacy=True))
            reason = "Mutation execution did not complete (build: failure)."
            result = MUTATION_TESTING.record_module(
                target_manifest, "example", artifact_dir, "abc123", 1, 0, reason
            )
            report = MUTATION_TESTING.aggregate_results(
                target_manifest, root / "artifacts", root / "full", "abc123"
            )

            self.assertEqual("failed", result["status"])
            self.assertEqual(reason, result["error"])
            self.assertIsNone(result["reportPath"])
            self.assertFalse(report["complete"])
            self.assertEqual(0, report["successfulModules"])
            self.assertIsNone(report["mutationScore"])
            self.assertIn(reason, report["problems"][0])
            self.assertIn(reason, (artifact_dir / "module-summary.md").read_text())

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

    def test_aggregate_results_reports_excluded_targets(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            root = pathlib.Path(directory)
            exclusions = [{"project": "src/Windows.csproj", "reason": "Windows only"}]
            target_manifest = manifest(module("example", legacy=True)) | {
                "knownTargetCount": 2,
                "excludedProjects": exclusions,
            }
            report = MUTATION_TESTING.aggregate_results(
                target_manifest, root / "artifacts", root / "full", "abc123"
            )
            self.assertEqual(exclusions, report["excludedProjects"])
            self.assertEqual(2, report["knownTargetCount"])
            self.assertIn("`src/Windows.csproj`: Windows only", (root / "full" / "summary.md").read_text())
            self.assertIn("<code>src/Windows.csproj</code>: Windows only", (root / "full" / "index.html").read_text())

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

    def prepare_input(
        self, root: pathlib.Path, *, manual: bool = False,
        conclusion: str = "success", expired: bool = False,
        missing: bool = False, metadata: dict | list | None = None,
        corrupt: bool = False, download_error: Exception | None = None,
        run_changes: dict | None = None,
    ) -> tuple[dict, object, object]:
        run = {
            "id": 42,
            "run_attempt": 2,
            "path": ".github/workflows/mutation-testing.yml",
            "repository": {"full_name": "microsoft/testfx"},
            "head_repository": {"full_name": "microsoft/testfx"},
            "head_branch": "main",
            "head_sha": "a" * 40,
            "status": "completed",
            "conclusion": conclusion,
        } | (run_changes or {})
        inventory = {
            "total_count": 0 if missing else 1,
            "artifacts": [] if missing else [
                {"id": 99, "name": "mutation-testing-report", "expired": expired}
            ],
        }
        responses = ([{"databaseId": 42}], run, inventory) if manual else (run, inventory)
        def download(arguments: list[str], **kwargs) -> None:
            if download_error:
                raise download_error
            artifact_dir = pathlib.Path(arguments[-1])
            report_path = artifact_dir / "reports" / "mutation-report.json"
            write_mutation_report(report_path, ["Killed", "Survived"])
            if corrupt:
                report_path.write_text("\ufffdnot a ZIP or JSON", encoding="utf-8")
            if metadata is not None:
                (artifact_dir / "module-result.json").write_text(json.dumps(metadata))

        with patch.object(MUTATION_TESTING, "gh_json", side_effect=responses) as lookup:
            with patch.object(MUTATION_TESTING.subprocess, "run", side_effect=download) as transfer:
                prepared = MUTATION_TESTING.prepare_improver_input(
                    "microsoft/testfx",
                    "workflow_dispatch" if manual else "workflow_run",
                    {} if manual else {"workflow_run": {"id": 42}},
                    root / "input",
                )
        self.assertEqual(prepared, json.loads((root / "input" / "input.json").read_text()))
        return prepared, lookup, transfer

    def test_prepare_improver_stages_legacy_report_for_exact_run(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            root = pathlib.Path(directory)
            prepared, lookup, transfer = self.prepare_input(root)
            self.assertEqual("available", prepared["artifactStatus"])
            self.assertEqual((42, 2, "a" * 40, 99), (
                prepared["runId"], prepared["runAttempt"], prepared["sourceCommit"], prepared["artifactId"]
            ))
            self.assertEqual("stryker-report/reports/mutation-report.json", prepared["reportPath"])
            self.assertEqual([
                call("api", "repos/microsoft/testfx/actions/runs/42"),
                call("api", "repos/microsoft/testfx/actions/runs/42/artifacts?per_page=100"),
            ], lookup.call_args_list)
            self.assertEqual([
                "gh", "run", "download", "42", "--repo", "microsoft/testfx",
                "--name", "mutation-testing-report", "--dir", str(root / "input" / "stryker-report"),
            ], transfer.call_args.args[0])

    def test_prepare_improver_resolves_manual_run_on_main(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            prepared, lookup, _ = self.prepare_input(pathlib.Path(directory), manual=True)
            self.assertEqual("available", prepared["artifactStatus"])
            self.assertEqual(call(
                "run", "list", "--repo", "microsoft/testfx", "--workflow", "mutation-testing.yml",
                "--branch", "main", "--status", "completed", "--limit", "1", "--json", "databaseId",
            ), lookup.call_args_list[0])

    def test_prepare_improver_accepts_successful_module_from_failed_aggregate(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            prepared, _, _ = self.prepare_input(
                pathlib.Path(directory), conclusion="failure", metadata={
                    "schemaVersion": 1, "module": {"id": "server-mode-client"},
                    "status": "success", "sourceCommit": "a" * 40,
                },
            )
            self.assertEqual("failure", prepared["conclusion"])
            self.assertEqual("available", prepared["artifactStatus"])

    def test_prepare_improver_rejects_unproven_or_mismatched_module(self) -> None:
        valid = {
            "schemaVersion": 1, "module": {"id": "server-mode-client"},
            "status": "success", "sourceCommit": "a" * 40,
        }
        for metadata in [
            None, [], valid | {"status": "failed"}, valid | {"sourceCommit": "b" * 40},
            valid | {"module": {"id": "other"}}, valid | {"schemaVersion": 2},
        ]:
            with self.subTest(metadata=metadata), tempfile.TemporaryDirectory() as directory:
                prepared, _, _ = self.prepare_input(
                    pathlib.Path(directory), conclusion="failure", metadata=metadata
                )
                self.assertEqual("unavailable", prepared["artifactStatus"])
                self.assertIsNone(prepared["reportPath"])
                self.assertIn("Invalid mutation artifact:", prepared["error"])

    def test_prepare_improver_missing_or_expired_artifact_is_not_downloaded(self) -> None:
        for changes in [{"missing": True}, {"expired": True}]:
            with self.subTest(changes=changes), tempfile.TemporaryDirectory() as directory:
                prepared, _, transfer = self.prepare_input(pathlib.Path(directory), **changes)
                self.assertEqual("unavailable", prepared["artifactStatus"])
                self.assertEqual("Mutation report artifact is missing or expired", prepared["error"])
                transfer.assert_not_called()

    def test_prepare_improver_download_failure_or_corruption_preserves_reporting(self) -> None:
        for changes, expected in [
            ({"download_error": subprocess.CalledProcessError(4, ["gh"])}, "download failed (exit 4)"),
            ({"download_error": subprocess.TimeoutExpired(["gh"], 180)}, "download timed out"),
            ({"corrupt": True}, "Invalid mutation artifact:"),
        ]:
            with self.subTest(changes=changes), tempfile.TemporaryDirectory() as directory:
                prepared, _, _ = self.prepare_input(pathlib.Path(directory), **changes)
                self.assertEqual("unavailable", prepared["artifactStatus"])
                self.assertIsNone(prepared["reportPath"])
                self.assertIn(expected, prepared["error"])
                self.assertEqual(42, prepared["runId"])

    def test_prepare_improver_rejects_wrong_run_identity(self) -> None:
        for changes in [
            {"head_branch": "other"}, {"head_sha": "not a commit"},
            {"head_repository": {"full_name": "other/fork"}},
            {"path": ".github/workflows/other.yml"}, {"status": "in_progress"},
        ]:
            with self.subTest(changes=changes), tempfile.TemporaryDirectory() as directory:
                root = pathlib.Path(directory)
                with self.assertRaisesRegex(ValueError, "Upstream run"):
                    self.prepare_input(root, run_changes=changes)
                self.assertFalse((root / "input").exists())

    def test_prepare_improver_required_lookup_failure_is_not_empty_data(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            root = pathlib.Path(directory)
            with patch.object(MUTATION_TESTING, "gh_json", side_effect=subprocess.CalledProcessError(4, ["gh"])):
                with self.assertRaises(subprocess.CalledProcessError):
                    MUTATION_TESTING.prepare_improver_input(
                        "microsoft/testfx", "workflow_run", {"workflow_run": {"id": 42}}, root / "input"
                    )
            self.assertFalse((root / "input").exists())


if __name__ == "__main__":
    unittest.main()
