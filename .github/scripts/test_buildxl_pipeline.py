#!/usr/bin/env python3
"""Focused CI contracts for native BuildXL builds and retained build evidence."""

from __future__ import annotations

import base64
import hashlib
import json
from pathlib import Path
import re
import shutil
import subprocess
import sys
import tempfile
import unittest
import uuid
import xml.etree.ElementTree as ET

import yaml


ROOT = Path(__file__).resolve().parents[2]
JOURNAL_STEP = "Initialize BuildXL change journal"
CACHE_STEP = "Cache BuildXL actions"
BUILD_STEP = "Build repository with BuildXL"
TEST_STEP = "Run Bazel-matched unit tests with BuildXL outputs"
COLLECT_STEP = "Collect BuildXL engine and project logs"
PUBLISH_STEP = "Publish BuildXL build logs"
MAC_CACHE_HELPER = "Initialize-VisualStudioTelemetryMacCache"
GETMAC_OUTPUT = "\n".join(
    [
        "",
        "Physical Address    Transport Name",
        "=================== ==========================================================",
        "00-00-00-00-00-00   Media disconnected",
        r"3C-52-82-AB-CD-EF   \Device\Tcpip_{6B29FC40-CA47-1067-B31D-00DD010662DA}",
        r"0A-1B-2C-3D-4E-5F   \Device\Tcpip_{0F0E0D0C-0B0A-0908-0706-050403020100}",
    ]
)
# Visual Studio telemetry hashes the first non-zero address exactly as getmac prints it.
SEEDED_MAC_HASH = "s:" + hashlib.sha256(b"3C-52-82-AB-CD-EF").hexdigest()
CACHED_HASH = "0123456789abcdef" * 4
PACK_ONLY_PROPERTIES = {
    "TestFxBuildXLPack": "true",
    "ArtifactsPackagesDir": "PACKAGES\\",
    "CustomAfterMicrosoftCommonCrossTargetingTargets": "MSBUILD.targets",
}


def mappings(value):
    """Visit parsed YAML mappings, including compile-time conditional branches."""
    if isinstance(value, dict):
        yield value
        for child in value.values():
            yield from mappings(child)
    elif isinstance(value, list):
        for child in value:
            yield from mappings(child)


def file_contents(root: Path) -> dict[str, bytes]:
    return {
        path.relative_to(root).as_posix(): path.read_bytes()
        for path in root.rglob("*")
        if path.is_file()
    }


class BuildXLPipelineTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls) -> None:
        cls.pipeline = yaml.safe_load(
            (ROOT / "azure-pipelines.yml").read_text(encoding="utf-8")
        )
        cls.template = yaml.safe_load(
            (ROOT / "eng" / "pipelines" / "jobs" / "buildxl.yml").read_text(
                encoding="utf-8"
            )
        )
        cls.job = cls.template["jobs"][0]
        cls.raw_steps = cls.job["steps"]
        # Flatten compile-time conditional step groups so steps can be found and ordered by position.
        cls.steps = []
        for entry in cls.raw_steps:
            keys = list(entry)
            if len(keys) == 1 and keys[0].startswith("${{"):
                cls.steps.extend(entry[keys[0]])
            else:
                cls.steps.append(entry)

    def step(self, display_name: str) -> dict:
        matches = [
            step for step in self.steps if step.get("displayName") == display_name
        ]
        self.assertEqual(1, len(matches), f"Expected one step named {display_name!r}.")
        return matches[0]

    def test_buildxl_is_enabled_by_default_independently_of_skip_tests(self) -> None:
        parameters = [
            parameter
            for parameter in self.pipeline["parameters"]
            if parameter["name"] == "EnableBuildXL"
        ]
        self.assertEqual(1, len(parameters))
        self.assertEqual("boolean", parameters[0]["type"])
        self.assertIs(True, parameters[0]["default"])
        build_stage = next(
            stage for stage in self.pipeline["stages"] if stage.get("stage") == "build"
        )
        gate = "${{ if eq(parameters.EnableBuildXL, true) }}"
        imports = [
            entry[gate] for entry in build_stage["jobs"] if gate in entry
        ]
        # A direct build-stage gate cannot be nested under SkipTests; SkipTests is forwarded to the test step only.
        self.assertEqual(
            [
                [
                    {
                        "template": "/eng/pipelines/jobs/buildxl.yml",
                        "parameters": {"SkipTests": "${{ parameters.SkipTests }}"},
                    }
                ]
            ],
            imports,
        )
        all_imports = [
            node["template"]
            for node in mappings(self.pipeline)
            if node.get("template") == "/eng/pipelines/jobs/buildxl.yml"
        ]
        self.assertEqual(["/eng/pipelines/jobs/buildxl.yml"], all_imports)
        self.assertNotIn("SkipTests", self.job.get("condition", ""))

    def test_skip_tests_removes_only_the_unit_test_step(self) -> None:
        self.assertEqual(
            [{"name": "SkipTests", "type": "boolean", "default": False}],
            self.template["parameters"],
        )
        gate = "${{ if eq(parameters.SkipTests, false) }}"
        self.assertEqual(
            [[self.step(TEST_STEP)]],
            [entry[gate] for entry in self.raw_steps if gate in entry],
        )
        for name in (BUILD_STEP, COLLECT_STEP, PUBLISH_STEP):
            self.assertIn(self.step(name), self.raw_steps, name)

    def test_pipeline_does_not_import_the_legacy_benchmark(self) -> None:
        names = [parameter["name"] for parameter in self.pipeline["parameters"]]
        self.assertNotIn("EnableBuildXLPerformance", names)
        for node in mappings(self.pipeline):
            if "template" in node:
                self.assertNotRegex(
                    node["template"], r"(?i)buildxl-performance|benchmark"
                )

    def test_buildxl_job_uses_debug_and_release_matrix(self) -> None:
        self.assertEqual(["BuildXL"], [job["job"] for job in self.template["jobs"]])
        self.assertEqual(
            {
                "Debug": {"BuildXLConfiguration": "Debug"},
                "Release": {"BuildXLConfiguration": "Release"},
            },
            self.job["strategy"]["matrix"],
        )

    def test_buildxl_job_gate_includes_product_or_sample_changes_and_fails_open(self) -> None:
        # Source mapping: DetectChanges job -> detect step -> named output variables.
        self.assertEqual("DetectChanges", self.job["dependsOn"])
        # Comparing with 'false', rather than requiring 'true', keeps missing outputs fail-open.
        expected_condition = """
            and(
                succeededOrFailed(),
                ne(dependencies.DetectChanges.outputs['detect.infrastructureOnly'], 'true'),
                ne(dependencies.DetectChanges.outputs['detect.xmlDocsOnly'], 'true'),
                or(
                    ne(dependencies.DetectChanges.outputs['detect.hasProductChanges'], 'false'),
                    ne(dependencies.DetectChanges.outputs['detect.hasSamplesChanges'], 'false')
                )
            )
        """
        self.assertEqual(
            "".join(expected_condition.split()),
            "".join(self.job["condition"].split()),
        )

    def test_checkout_and_nuget_cache_are_job_isolated(self) -> None:
        checkouts = [step for step in self.steps if "checkout" in step]
        self.assertEqual(1, len(checkouts))
        self.assertEqual("self", checkouts[0]["checkout"])
        self.assertIs(True, checkouts[0]["clean"])
        self.assertIs(False, checkouts[0]["persistCredentials"])
        self.assertEqual(
            r"$(Build.SourcesDirectory)\.packages",
            self.job["variables"]["NUGET_PACKAGES"],
        )

    def test_change_journal_is_initialized_before_cache_and_build(self) -> None:
        journal = self.step(JOURNAL_STEP)
        checkout = next(step for step in self.steps if "checkout" in step)
        self.assertIs(True, journal.get("enabled", True))
        self.assertNotIn("condition", journal)
        self.assertLess(self.steps.index(checkout), self.steps.index(journal))
        self.assertLess(self.steps.index(journal), self.steps.index(self.step(CACHE_STEP)))
        self.assertLess(self.steps.index(journal), self.steps.index(self.step(BUILD_STEP)))

    def test_action_cache_uses_qualified_immutable_key_and_compatible_restore_prefix(self) -> None:
        inputs = self.step(CACHE_STEP)["inputs"]
        prefix = [
            '"buildxl-actions-v1"',
            '"$(Agent.OS)"',
            '"$(BuildXLConfiguration)"',
            "global.json",
            r"eng\buildxl.ps1,eng\buildxl\config.dsc,eng\buildxl\msbuild.targets",
        ]
        self.assertEqual(
            prefix + ['"$(Build.BuildId)"', '"$(System.JobAttempt)"'],
            inputs["key"].split(" | "),
        )
        self.assertEqual(
            [" | ".join(prefix)], inputs["restoreKeys"].strip().splitlines()
        )

    def test_action_cache_restores_only_actions_between_checkout_and_native_build(self) -> None:
        cache = self.step(CACHE_STEP)
        self.assertEqual("Cache@2", cache["task"])
        self.assertEqual(
            [cache],
            [
                step for step in self.steps
                if step.get("task", "").partition("@")[0] == "Cache"
            ],
        )
        self.assertEqual(
            {"key", "restoreKeys", "path", "cacheHitVar"}, set(cache["inputs"])
        )
        self.assertEqual(
            r"$(Build.SourcesDirectory)\artifacts\buildxl\cache",
            cache["inputs"]["path"],
        )
        self.assertEqual("BuildXLCacheRestored", cache["inputs"]["cacheHitVar"])
        self.assertIs(True, cache.get("enabled", True))
        self.assertNotIn("condition", cache)
        checkout = next(step for step in self.steps if "checkout" in step)
        self.assertLess(self.steps.index(checkout), self.steps.index(cache))
        self.assertLess(self.steps.index(cache), self.steps.index(self.step(BUILD_STEP)))

    def test_action_cache_hit_never_skips_native_build(self) -> None:
        build = self.step(BUILD_STEP)
        self.assertIs(True, build.get("enabled", True))
        self.assertNotIn("condition", build)
        for node in mappings(self.job):
            self.assertNotIn("BuildXLCacheRestored", node.get("condition", ""))

    def test_public_project_retains_codeql_and_cg_injection_policy(self) -> None:
        self.assertEqual(
            {
                "Codeql.SkipTaskAutoInjection": True,
                "skipComponentGovernanceDetection": True,
            },
            self.job["variables"][
                "${{ if eq(variables['System.TeamProject'], 'public') }}"
            ],
        )

    def test_native_build_targets_full_solution_and_preserves_exit_code(self) -> None:
        # Pin the complete command: no redirection, wrapper, or fallback can replace it.
        self.assertEqual(
            [
                r".\eng\buildxl.ps1 -Project TestFx.slnx -Configuration '$(BuildXLConfiguration)'",
                "exit $LASTEXITCODE",
            ],
            self.step(BUILD_STEP)["pwsh"].strip().splitlines(),
        )

    def test_buildxl_failures_are_blocking_without_benchmark_or_msbuild_fallback(self) -> None:
        for node in mappings(self.job):
            self.assertNotIn("continueOnError", node)
            for key in ("pwsh", "powershell", "script", "bash"):
                if key in node:
                    self.assertNotRegex(
                        node[key],
                        r"(?i)benchmark\.ps1|\s-(?:Samples|TargetFramework|PrepareOnly)\b"
                        r"|(?:^|[\s;&|])(?:dotnet\s+)?msbuild(?:\.exe)?\b",
                    )

    def test_native_build_retry_is_bounded_and_preserves_failure(self) -> None:
        build = self.step(BUILD_STEP)
        self.assertEqual(1, build.get("retryCountOnTaskFailure", 0))
        self.assertNotIn("continueOnError", build)
        self.assertEqual("exit $LASTEXITCODE", build["pwsh"].strip().splitlines()[-1])
        self.assertEqual(
            [build],
            [step for step in self.steps if step.get("retryCountOnTaskFailure", 0)],
        )

    def test_buildxl_does_not_publish_synthetic_test_results(self) -> None:
        for node in mappings(self.job):
            self.assertNotRegex(node.get("task", ""), r"(?i)^PublishTestResults(?:@|$)")

    def test_unit_tests_run_only_after_a_successful_native_build_and_block_the_job(self) -> None:
        tests = self.step(TEST_STEP)
        # Pin the complete command: the build step's retry must not rerun tests and a failure must fail the job.
        self.assertEqual(
            [
                r".\eng\buildxl\run-unit-tests.ps1 -Configuration '$(BuildXLConfiguration)'",
                "exit $LASTEXITCODE",
            ],
            tests["pwsh"].strip().splitlines(),
        )
        self.assertIs(True, tests.get("enabled", True))
        # The default condition is succeeded(): never test outputs from a failed or partial build.
        self.assertNotIn("condition", tests)
        self.assertNotIn("continueOnError", tests)
        self.assertNotIn("retryCountOnTaskFailure", tests)
        # The step timeout leaves the job budget for evidence collection and publication.
        self.assertLess(tests["timeoutInMinutes"], self.job["timeoutInMinutes"])
        build_index = self.steps.index(self.step(BUILD_STEP))
        tests_index = self.steps.index(tests)
        self.assertLess(build_index, tests_index)
        self.assertLess(tests_index, self.steps.index(self.step(COLLECT_STEP)))

    def test_unit_test_runner_uses_the_bazel_dogfood_workload_and_matches_its_assemblies(self) -> None:
        workload = json.loads(
            (ROOT / "eng" / "bazel" / "dogfood-workload.json").read_text(encoding="utf-8")
        )
        runner = (ROOT / "eng" / "buildxl" / "run-unit-tests.ps1").read_text(encoding="utf-8")
        self.assertIn("eng\\bazel\\dogfood-workload.json", runner)
        self.assertIn("eng\\bazel\\dogfood-results.psm1", runner)
        self.assertEqual("net10.0", workload["targetFramework"])
        self.assertEqual(13, len(workload["tests"]))
        for test in workload["tests"]:
            project = ROOT / "test" / "UnitTests" / test["project"] / f"{test['project']}.csproj"
            self.assertTrue(project.is_file(), project)
        solution = (ROOT / "TestFx.slnx").read_text(encoding="utf-8")
        for test in workload["tests"]:
            self.assertIn(f"{test['project']}.csproj", solution, test["label"])

    def run_unit_test_runner(self, *arguments: str) -> subprocess.CompletedProcess:
        if sys.platform != "win32":
            self.skipTest("The BuildXL unit-test runner is Windows-only.")

        return subprocess.run(
            [
                "pwsh", "-NoLogo", "-NoProfile", "-NonInteractive", "-OutputFormat", "Text",
                "-File", str(ROOT / "eng" / "buildxl" / "run-unit-tests.ps1"), *arguments,
            ],
            cwd=ROOT,
            capture_output=True,
            text=True,
            timeout=120,
            check=False,
        )

    def test_unit_test_runner_fails_closed_without_build_output(self) -> None:
        output_id = "runner-missing-" + uuid.uuid4().hex[:12]
        output = ROOT / "artifacts" / "buildxl-runs" / output_id
        self.assertFalse(output.exists())
        process = self.run_unit_test_runner("-Configuration", "Debug", "-OutputId", output_id)
        self.assertEqual(1, process.returncode, process.stdout + process.stderr)
        self.assertIn("toolchain.json", process.stdout + process.stderr)
        self.assertFalse(output.exists(), "A failed precondition must not create evidence.")

    def test_unit_test_runner_rejects_build_output_without_a_successful_build(self) -> None:
        output_id = "runner-partial-" + uuid.uuid4().hex[:12]
        output = ROOT / "artifacts" / "buildxl-runs" / output_id
        revision = subprocess.run(
            ["git", "-C", str(ROOT), "rev-parse", "HEAD"],
            capture_output=True, text=True, check=True,
        ).stdout.strip()
        try:
            (output / "logs").mkdir(parents=True)
            (output / "toolchain.json").write_text(
                json.dumps(
                    {
                        "SourceRevision": revision,
                        "Sdk": "0.0.0",
                        "Engine": "BuildXL",
                        "Configuration": "Debug",
                        "DotNetRoot": str(ROOT / ".dotnet"),
                        "Project": "TestFx.slnx",
                        "TargetFramework": "Project defaults",
                    }
                ),
                encoding="utf-8",
            )
            (output / "logs" / "phase-timings.json").write_text(
                json.dumps({"RestoreExitCode": 0, "BuildExitCode": 1}),
                encoding="utf-8",
            )
            process = self.run_unit_test_runner("-Configuration", "Debug", "-OutputId", output_id)
            self.assertEqual(1, process.returncode, process.stdout + process.stderr)
            self.assertIn("The build did not succeed", process.stdout + process.stderr)
            self.assertFalse(
                (output / "logs" / "unit-tests").exists(),
                "No suite may run against a failed build.",
            )
        finally:
            shutil.rmtree(output, ignore_errors=True)

    def test_unit_test_runner_rejects_build_output_narrower_than_the_workload(self) -> None:
        revision = subprocess.run(
            ["git", "-C", str(ROOT), "rev-parse", "HEAD"],
            capture_output=True, text=True, check=True,
        ).stdout.strip()
        cases = {
            "project": ("src\\TestFramework\\TestFramework\\TestFramework.csproj", "Project defaults"),
            "framework": ("TestFx.slnx", "netstandard2.0"),
        }
        for case, (project, framework) in cases.items():
            with self.subTest(case=case):
                output_id = "runner-scope-" + uuid.uuid4().hex[:12]
                output = ROOT / "artifacts" / "buildxl-runs" / output_id
                try:
                    (output / "logs").mkdir(parents=True)
                    (output / "toolchain.json").write_text(
                        json.dumps(
                            {
                                "SourceRevision": revision,
                                "Sdk": "0.0.0",
                                "Engine": "MSBuild",
                                "Configuration": "Debug",
                                "DotNetRoot": str(ROOT / ".dotnet"),
                                "Project": project,
                                "TargetFramework": framework,
                            }
                        ),
                        encoding="utf-8",
                    )
                    (output / "logs" / "phase-timings.json").write_text(
                        json.dumps({"RestoreExitCode": 0, "BuildExitCode": 0}),
                        encoding="utf-8",
                    )
                    process = self.run_unit_test_runner("-Configuration", "Debug", "-OutputId", output_id)
                    self.assertEqual(1, process.returncode, process.stdout + process.stderr)
                    self.assertIn(
                        f"The build output covers {project} ({framework}), not the workload.",
                        process.stdout + process.stderr,
                    )
                    self.assertFalse(
                        (output / "logs" / "unit-tests").exists(),
                        "No suite may run against assemblies outside the recorded build scope.",
                    )
                finally:
                    shutil.rmtree(output, ignore_errors=True)

    def test_unit_test_summary_reports_pack_from_the_recorded_build_targets(self) -> None:
        if sys.platform != "win32":
            self.skipTest("The BuildXL unit-test runner is Windows-only.")
        dotnet_root = ROOT / ".dotnet"
        if not (dotnet_root / "dotnet.exe").is_file():
            self.skipTest("The repository-local .NET SDK is not restored.")
        runtimes = subprocess.run(
            [str(dotnet_root / "dotnet.exe"), "--list-runtimes"],
            capture_output=True, text=True, check=True,
        ).stdout
        major = re.search(r"^Microsoft\.NETCore\.App (\d+)\.", runtimes, re.MULTILINE).group(1)
        revision = subprocess.run(
            ["git", "-C", str(ROOT), "rev-parse", "HEAD"],
            capture_output=True, text=True, check=True,
        ).stdout.strip()
        cases = {
            "Build;Pack": ("| Build (includes Pack) | 34.5 |", "| Pack | included in Build |"),
            "Build": ("| Build | 34.5 |", "| Pack | not run |"),
            None: ("| Build | 34.5 |", "| Pack | not run |"),
        }
        for targets, (build_row, pack_row) in cases.items():
            with self.subTest(targets=targets):
                output_id = "runner-summary-" + uuid.uuid4().hex[:12]
                output = ROOT / "artifacts" / "buildxl-runs" / output_id
                try:
                    # A one-suite workload over a placeholder assembly reaches the summary without real build outputs;
                    # the suite itself fails because the placeholder is not a test host.
                    assembly = output / "bin" / "Placeholder.UnitTests" / "Debug" / "net10.0"
                    assembly.mkdir(parents=True)
                    (assembly / "Placeholder.UnitTests.dll").write_bytes(b"")
                    workload = output / "workload.json"
                    workload.write_text(
                        json.dumps(
                            {
                                "schemaVersion": 1,
                                "name": "summary",
                                "identityPolicy": "trx",
                                "targetFramework": "net10.0",
                                "tests": [
                                    {
                                        "label": "//:placeholder_tests",
                                        "project": "Placeholder.UnitTests",
                                        "assembly": "Placeholder.UnitTests",
                                        "runtime": "sdk",
                                    }
                                ],
                            }
                        ),
                        encoding="utf-8",
                    )
                    (output / "toolchain.json").write_text(
                        json.dumps(
                            {
                                "SourceRevision": revision,
                                "Sdk": f"{major}.0.100",
                                "Engine": "BuildXL",
                                "Configuration": "Debug",
                                "DotNetRoot": str(dotnet_root),
                                "Project": "TestFx.slnx",
                                "TargetFramework": "Project defaults",
                            }
                        ),
                        encoding="utf-8",
                    )
                    phases = {"RestoreExitCode": 0, "BuildExitCode": 0, "RestoreSeconds": 12.5, "BuildSeconds": 34.5}
                    if targets is not None:
                        phases["Targets"] = targets
                    (output / "logs").mkdir()
                    (output / "logs" / "phase-timings.json").write_text(json.dumps(phases), encoding="utf-8")
                    process = self.run_unit_test_runner(
                        "-Configuration", "Debug", "-OutputId", output_id, "-Workload", str(workload)
                    )
                    self.assertEqual(1, process.returncode, process.stdout + process.stderr)
                    summary = (output / "logs" / "unit-tests" / "unit-test-summary.md").read_text(
                        encoding="utf-8-sig"
                    ).replace("\r\n", "\n")
                    phase_table = re.sub(
                        r"(?m)^(\| Unit tests \(1 suites, 2 at a time\) \| )[\d.]+ \|$",
                        r"\1<seconds> |",
                        summary.split("\n\n")[1],
                    )
                    self.assertEqual(
                        "\n".join(
                            [
                                "| Phase | Seconds |",
                                "| --- | ---: |",
                                "| Restore | 12.5 |",
                                build_row,
                                "| Runtime bootstrap (outside the test phase) | 0 |",
                                "| Unit tests (1 suites, 2 at a time) | <seconds> |",
                                pack_row,
                            ]
                        ),
                        phase_table.strip(),
                    )
                finally:
                    shutil.rmtree(output, ignore_errors=True)

    def test_collector_runs_always_after_native_build(self) -> None:
        collector = self.step(COLLECT_STEP)
        self.assertEqual("always()", collector["condition"])
        self.assertLess(self.steps.index(self.step(BUILD_STEP)), self.steps.index(collector))
        self.assertLess(self.steps.index(collector), self.steps.index(self.step(PUBLISH_STEP)))

    def test_publication_requires_evidence_and_uses_configuration_attempt_identity(self) -> None:
        publisher = self.step(PUBLISH_STEP)
        self.assertEqual(
            [publisher],
            [step for step in self.steps if step.get("task") == "PublishPipelineArtifact@1"],
        )
        self.assertEqual(
            "and(always(), eq(variables.BuildXLHasEvidence, 'True'))",
            publisher["condition"],
        )
        self.assertEqual(
            r"$(Build.ArtifactStagingDirectory)\buildxl-"
            r"$(BuildXLConfiguration)-Attempt$(System.JobAttempt)",
            self.job["variables"]["BuildXLArtifacts"],
        )
        self.assertEqual("$(BuildXLArtifacts)", publisher["inputs"]["targetPath"])
        self.assertEqual(
            "buildxl-$(BuildXLConfiguration)-Attempt$(System.JobAttempt)",
            publisher["inputs"]["artifact"],
        )

    def initialize_change_journal(
        self, checkout: str, replies: list[dict]
    ) -> tuple[subprocess.CompletedProcess, list[list[str]]]:
        # With the pre-fix YAML, no setup runs and the journal remains untouched.
        setup = "\n".join(
            step["pwsh"] for step in self.steps
            if step.get("displayName") == JOURNAL_STEP
        )
        fixture = base64.b64encode(
            json.dumps(
                {"Checkout": checkout, "Replies": replies, "Setup": setup}
            ).encode("utf-8")
        ).decode("ascii")
        script = r"""
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version 2.0
$fixture = [Text.Encoding]::UTF8.GetString(
    [Convert]::FromBase64String('__FIXTURE__')) | ConvertFrom-Json -AsHashtable
$env:BUILD_SOURCESDIRECTORY = $fixture.Checkout
$calls = [Collections.Generic.List[object]]::new()
$global:LASTEXITCODE = 0
function fsutil.exe {
    $script:calls.Add([string[]] $args)
    if ($script:calls.Count -gt $script:fixture.Replies.Count) {
        throw 'Unexpected journal command.'
    }

    $reply = $script:fixture.Replies[$script:calls.Count - 1]
    $global:LASTEXITCODE = $reply.ExitCode
    Write-Output $reply.Output
}
try {
    & ([scriptblock]::Create($fixture.Setup))
}
finally {
    $result = @{ Calls = @($calls.ToArray()) }
    Write-Output ('BUILDXL_JOURNAL_RESULT=' + ($result | ConvertTo-Json -Depth 4 -Compress))
}
"""
        command = base64.b64encode(
            script.replace("__FIXTURE__", fixture).encode("utf-16-le")
        ).decode("ascii")
        process = subprocess.run(
            [
                "pwsh", "-NoLogo", "-NoProfile", "-NonInteractive",
                "-OutputFormat", "Text", "-EncodedCommand", command,
            ],
            cwd=ROOT,
            capture_output=True,
            text=True,
            timeout=30,
            check=False,
        )
        marker = "BUILDXL_JOURNAL_RESULT="
        results = [
            line.removeprefix(marker)
            for line in process.stdout.splitlines() if line.startswith(marker)
        ]
        self.assertEqual(1, len(results), process.stdout + process.stderr)
        return process, json.loads(results[0])["Calls"]

    def test_existing_change_journal_is_preserved(self) -> None:
        process, calls = self.initialize_change_journal(
            r"D:\checkout with spaces",
            [{"ExitCode": 0, "Output": "USN-Journal-ID : 0x56789"}],
        )
        self.assertEqual(0, process.returncode, process.stdout + process.stderr)
        self.assertEqual("", process.stderr.strip())
        self.assertEqual([["usn", "queryjournal", "D:"]], calls)

    def test_missing_change_journal_is_created_and_verified(self) -> None:
        for case, checkout, volume, diagnostic in (
            (
                "hosted-workspace", r"D:\a\_work\1\s", "D:",
                "Error 1179: The volume change journal is not active.",
            ),
            (
                "different-drive", r"T:\CI checkout's workspace\src", "T:",
                "Fehler 1179: Das Journal ist nicht aktiv.",
            ),
        ):
            with self.subTest(case=case):
                process, calls = self.initialize_change_journal(
                    checkout,
                    [
                        {"ExitCode": 1, "Output": diagnostic},
                        {"ExitCode": 0, "Output": "Journal created."},
                        {"ExitCode": 0, "Output": "USN-Journal-ID : 0x56789"},
                    ],
                )
                self.assertEqual(0, process.returncode, process.stdout + process.stderr)
                self.assertEqual("", process.stderr.strip())
                self.assertEqual(
                    [
                        ["usn", "queryjournal", volume],
                        ["usn", "createjournal", "m=0x20000000", "a=0x8000000", volume],
                        ["usn", "queryjournal", volume],
                    ],
                    calls,
                )

    def test_change_journal_query_failure_preserves_existing_journal(self) -> None:
        for case, diagnostic in (
            ("access-denied", "Error 5: Access is denied."),
            ("deletion-in-progress", "Error 1178: The volume change journal is being deleted."),
            ("unrecognized-output", "The volume change journal is not active."),
            ("journal-id-collision", "Usn Journal ID : 1179"),
        ):
            with self.subTest(case=case):
                process, calls = self.initialize_change_journal(
                    r"D:\a\_work\1\s",
                    [
                        {"ExitCode": 1, "Output": diagnostic},
                        {"ExitCode": 0, "Output": "An existing journal was resized."},
                        {"ExitCode": 0, "Output": "USN-Journal-ID : 0x56789"},
                    ],
                )
                self.assertNotEqual(0, process.returncode)
                self.assertEqual([["usn", "queryjournal", "D:"]], calls)
                self.assertIn(diagnostic, process.stdout)
                self.assertIn("exit 1", process.stderr)

    def test_change_journal_creation_failure_blocks_build(self) -> None:
        process, calls = self.initialize_change_journal(
            r"D:\a\_work\1\s",
            [
                {"ExitCode": 1, "Output": "Error 1179: The volume change journal is not active."},
                {"ExitCode": 5, "Output": "Access is denied."},
            ],
        )
        self.assertNotEqual(0, process.returncode)
        self.assertIn("Access is denied.", process.stdout)
        self.assertIn("exit 5", process.stderr)
        self.assertEqual(
            [
                ["usn", "queryjournal", "D:"],
                ["usn", "createjournal", "m=0x20000000", "a=0x8000000", "D:"],
            ],
            calls,
        )

    def test_change_journal_verification_failure_blocks_build(self) -> None:
        process, calls = self.initialize_change_journal(
            r"D:\a\_work\1\s",
            [
                {"ExitCode": 1, "Output": "Error 1179: The volume change journal is not active."},
                {"ExitCode": 0, "Output": "Journal created."},
                {"ExitCode": 1, "Output": "Journal is still unavailable."},
            ],
        )
        self.assertNotEqual(0, process.returncode)
        self.assertIn("Journal is still unavailable.", process.stdout)
        self.assertIn("exit 1", process.stderr)
        self.assertEqual(
            [
                ["usn", "queryjournal", "D:"],
                ["usn", "createjournal", "m=0x20000000", "a=0x8000000", "D:"],
                ["usn", "queryjournal", "D:"],
            ],
            calls,
        )

    def file_system_mode(
        self, paths: list[str], journals: dict[str, dict], no_journal: bool = False
    ) -> tuple[dict, str]:
        fixture = base64.b64encode(
            json.dumps(
                {"Paths": paths, "Journals": journals, "NoJournal": no_journal}
            ).encode("utf-8")
        ).decode("ascii")
        script = r"""
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version 2.0
$tokens = $null
$parseErrors = $null
$ast = [System.Management.Automation.Language.Parser]::ParseFile(
    '__DRIVER__', [ref] $tokens, [ref] $parseErrors)
if ($parseErrors.Count -ne 0) { throw 'The driver must parse before extracting its helper.' }
$helpers = @($ast.FindAll({
    param($node)
    $node -is [System.Management.Automation.Language.FunctionDefinitionAst] -and
        $node.Name -eq 'Get-BuildXLFileSystemMode'
}, $true))
if ($helpers.Count -ne 1) { throw 'Expected exactly one Get-BuildXLFileSystemMode helper.' }
# Load only the actual helper; never execute driver setup, restore or build code.
. ([scriptblock]::Create($helpers[0].Extent.Text))
$fixture = [Text.Encoding]::UTF8.GetString(
    [Convert]::FromBase64String('__FIXTURE__')) | ConvertFrom-Json -AsHashtable
$queries = [Collections.Generic.List[string]]::new()
$global:LASTEXITCODE = 0
function fsutil.exe {
    if ($args.Count -ne 3 -or $args[0] -cne 'usn' -or $args[1] -cne 'queryjournal') {
        throw 'Only read-only fsutil usn queryjournal calls are permitted.'
    }

    $volume = [string] $args[2]
    $script:queries.Add($volume)
    if (-not $script:fixture.Journals.Contains($volume)) {
        throw "Unexpected journal volume: $volume"
    }

    $reply = $script:fixture.Journals[$volume]
    $global:LASTEXITCODE = $reply.ExitCode
    Write-Output $reply.Output
}
$mode = Get-BuildXLFileSystemMode -Paths $fixture.Paths -NoJournal:$fixture.NoJournal
$result = [ordered]@{ Mode = $mode; Queries = @($queries.ToArray()) }
Write-Output ('BUILDXL_FILE_SYSTEM_RESULT=' + ($result | ConvertTo-Json -Compress))
"""
        script = script.replace(
            "__DRIVER__", str(ROOT / "eng" / "buildxl.ps1").replace("'", "''")
        ).replace("__FIXTURE__", fixture)
        command = base64.b64encode(script.encode("utf-16-le")).decode("ascii")
        process = subprocess.run(
            [
                "pwsh", "-NoLogo", "-NoProfile", "-NonInteractive",
                "-OutputFormat", "Text", "-EncodedCommand", command,
            ],
            cwd=ROOT,
            capture_output=True,
            text=True,
            timeout=30,
            check=False,
        )
        self.assertEqual(0, process.returncode, process.stdout + process.stderr)
        self.assertEqual("", process.stderr.strip())
        marker = "BUILDXL_FILE_SYSTEM_RESULT="
        results = [
            line.removeprefix(marker)
            for line in process.stdout.splitlines() if line.startswith(marker)
        ]
        self.assertEqual(1, len(results), process.stdout)
        diagnostics = "\n".join(
            line for line in process.stdout.splitlines() if not line.startswith(marker)
        )
        return json.loads(results[0]), diagnostics

    def test_journal_mode_checks_every_relevant_volume_read_only(self) -> None:
        result, _ = self.file_system_mode(
            [
                r"C:\checkout with spaces", r"D:\pinned sdk", r"E:\packages",
                r"C:\checkout with spaces\artifacts\buildxl",
            ],
            {
                "C:\\": {"ExitCode": 0, "Output": "Usn Journal ID : 0x01234"},
                "D:\\": {"ExitCode": 0, "Output": "USN-Journal-ID : 0x56789"},
                "E:\\": {"ExitCode": 0, "Output": ""},
            },
        )
        self.assertEqual("Journal", result["Mode"])
        self.assertCountEqual(["C:\\", "D:\\", "E:\\"], result["Queries"])

    def test_missing_journal_falls_back_with_diagnostics(self) -> None:
        cases = (
            ("inactive-journal", 1, "The volume change journal is not active."),
            ("access-denied", 5, "Access is denied."),
            ("nonzero-with-journal-output", 1, "Usn Journal ID : 0x01234"),
        )
        for name, exit_code, output in cases:
            with self.subTest(case=name):
                result, diagnostics = self.file_system_mode(
                    [r"C:\checkout", r"D:\pinned sdk"],
                    {
                        "C:\\": {"ExitCode": 0, "Output": "Usn Journal ID : 0x56789"},
                        "D:\\": {"ExitCode": exit_code, "Output": output},
                    },
                )
                self.assertEqual("FullHashing", result["Mode"])
                self.assertIn("D:\\", result["Queries"])
                self.assertIn("D:\\", diagnostics)
                self.assertIn(f"exit {exit_code}", diagnostics)
                self.assertIn(output, diagnostics)

    def test_no_journal_explicitly_forces_full_hashing(self) -> None:
        result, diagnostics = self.file_system_mode(
            [r"C:\checkout", r"D:\pinned sdk"],
            {
                "C:\\": {"ExitCode": 0, "Output": "Usn Journal ID : 0x01234"},
                "D:\\": {"ExitCode": 0, "Output": "Usn Journal ID : 0x56789"},
            },
            no_journal=True,
        )
        self.assertEqual("FullHashing", result["Mode"])
        self.assertEqual([], result["Queries"])
        self.assertIn("-NoJournal", diagnostics)

    def test_wrapper_seeds_telemetry_mac_cache_only_before_native_buildxl(self) -> None:
        lines = [
            line.strip()
            for line in (ROOT / "eng" / "buildxl.ps1").read_text(encoding="utf-8").splitlines()
        ]
        calls = [index for index, line in enumerate(lines) if line == MAC_CACHE_HELPER]
        self.assertEqual(1, len(calls))
        prepare_only = lines.index("if ($PrepareOnly) {")
        msbuild_engine = lines.index("if ($Engine -eq 'MSBuild') {")
        native_build = [
            index for index, line in enumerate(lines) if line.startswith("& $bxl ")
        ]
        self.assertEqual(1, len(native_build))
        self.assertLess(prepare_only, msbuild_engine)
        self.assertLess(msbuild_engine, calls[0])
        self.assertLess(calls[0], native_build[0])

    def driver_settings(self, pack: bool) -> dict:
        script = r"""
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version 2.0
$tokens = $null
$parseErrors = $null
$ast = [System.Management.Automation.Language.Parser]::ParseFile(
    '__DRIVER__', [ref] $tokens, [ref] $parseErrors)
if ($parseErrors.Count -ne 0) { throw 'The driver must parse before extracting its settings.' }
$assignments = @($ast.FindAll({
    param($node)
    $node -is [System.Management.Automation.Language.AssignmentStatementAst]
}, $true))
$initial = @($assignments | Where-Object { $_.Left.Extent.Text -eq '$properties' -and $_.Operator -eq 'Equals' })
if ($initial.Count -ne 1) { throw 'Expected exactly one $properties initialization.' }
# Only the if statements that directly append to $properties; never execute restore or build code.
$updates = @($ast.FindAll({
    param($node)
    $node -is [System.Management.Automation.Language.IfStatementAst] -and $null -eq $node.ElseClause -and
        @($node.Clauses | ForEach-Object { $_.Item2.Statements } | Where-Object {
            $_ -is [System.Management.Automation.Language.AssignmentStatementAst] -and
                $_.Left.Extent.Text -eq '$properties' -and $_.Operator -eq 'PlusEquals'
        }).Count -gt 0
}, $true) | Sort-Object { $_.Extent.StartOffset })
function Get-AssignedValue([string] $Target) {
    $found = @($assignments | Where-Object { $_.Left.Extent.Text -eq $Target })
    if ($found.Count -ne 1) { throw "Expected exactly one assignment to $Target." }
    return & ([scriptblock]::Create($found[0].Right.Extent.Text))
}
$Configuration = 'Release'
$bin = 'BIN\'
$obj = 'OBJ\'
$msbuildTargets = 'MSBUILD.targets'
$packages = 'PACKAGES\'
$TargetFramework = ''
$AdditionalTargets = ''
$Pack = [switch]::new('__PACK__' -eq 'true')
. ([scriptblock]::Create($initial[0].Extent.Text))
foreach ($update in $updates) {
    . ([scriptblock]::Create($update.Extent.Text))
}
$targets = @($ast.FindAll({
    param($node)
    $node -is [System.Management.Automation.Language.HashtableAst]
}, $true) | ForEach-Object { $_.KeyValuePairs } | Where-Object { $_.Item1.Extent.Text -eq 'Targets' } |
    ForEach-Object { & ([scriptblock]::Create($_.Item2.Extent.Text)) })
$msbuildTargetArguments = @($ast.FindAll({
    param($node)
    $node -is [System.Management.Automation.Language.ExpandableStringExpressionAst] -and $node.Value.StartsWith('/t:')
}, $true) | ForEach-Object { & ([scriptblock]::Create($_.Extent.Text)) })
$result = [ordered]@{
    Properties = @($properties)
    PackEnvironment = Get-AssignedValue '$env:TESTFX_BUILDXL_PACK'
    PackagesEnvironment = Get-AssignedValue '$env:TESTFX_BUILDXL_PACKAGES'
    Targets = $targets
    MSBuildTargets = $msbuildTargetArguments
}
Write-Output ('BUILDXL_DRIVER_SETTINGS=' + ($result | ConvertTo-Json -Compress))
"""
        script = script.replace(
            "__DRIVER__", str(ROOT / "eng" / "buildxl.ps1").replace("'", "''")
        ).replace("__PACK__", "true" if pack else "false")
        command = base64.b64encode(script.encode("utf-16-le")).decode("ascii")
        process = subprocess.run(
            [
                "pwsh", "-NoLogo", "-NoProfile", "-NonInteractive",
                "-OutputFormat", "Text", "-EncodedCommand", command,
            ],
            cwd=ROOT,
            capture_output=True,
            text=True,
            timeout=30,
            check=False,
        )
        self.assertEqual(0, process.returncode, process.stdout + process.stderr)
        self.assertEqual("", process.stderr.strip())
        marker = "BUILDXL_DRIVER_SETTINGS="
        results = [
            line.removeprefix(marker)
            for line in process.stdout.splitlines() if line.startswith(marker)
        ]
        self.assertEqual(1, len(results), process.stdout)
        settings = json.loads(results[0])
        self.assertTrue(
            all(item.startswith("/p:") and "=" in item for item in settings["Properties"]),
            settings["Properties"],
        )
        settings["Properties"] = dict(
            item.removeprefix("/p:").split("=", 1) for item in settings["Properties"]
        )
        return settings

    def test_pack_is_opt_in_and_every_driver_path_agrees_on_targets(self) -> None:
        build = self.driver_settings(pack=False)
        pack = self.driver_settings(pack=True)

        self.assertEqual("false", build["PackEnvironment"])
        self.assertEqual(["Build", "Build"], build["Targets"])
        self.assertEqual(["/t:Build"], build["MSBuildTargets"])
        self.assertEqual("false", build["Properties"]["UpdateXlfOnBuild"])
        self.assertFalse(set(PACK_ONLY_PROPERTIES) & set(build["Properties"]))

        self.assertEqual("true", pack["PackEnvironment"])
        self.assertEqual(["Build;Pack", "Build;Pack"], pack["Targets"])
        self.assertEqual(["/t:Build;Pack"], pack["MSBuildTargets"])
        self.assertEqual({**build["Properties"], **PACK_ONLY_PROPERTIES}, pack["Properties"])
        self.assertEqual(
            pack["Properties"]["ArtifactsPackagesDir"], pack["PackagesEnvironment"]
        )

    def test_native_config_mirrors_the_driver_build_and_pack_settings(self) -> None:
        config = (ROOT / "eng" / "buildxl" / "config.dsc").read_text(encoding="utf-8")
        initial_targets = re.search(
            r'initialTargets: Environment\.getStringValue\("TESTFX_BUILDXL_PACK"\) === "true"'
            r" \? \[([^\]]*)\] : \[([^\]]*)\],",
            config,
        )
        self.assertIsNotNone(initial_targets, "initialTargets must be selected by TESTFX_BUILDXL_PACK.")
        build = self.driver_settings(pack=False)
        pack = self.driver_settings(pack=True)
        self.assertEqual(
            pack["Targets"][0].split(";"), re.findall(r'"(\w+)"', initial_targets.group(1))
        )
        self.assertEqual(
            build["Targets"][0].split(";"), re.findall(r'"(\w+)"', initial_targets.group(2))
        )

        block = config.split("globalProperties: Map.empty<string, string>().addRange(", 1)[1]
        block = block.split("logVerbosity:", 1)[0]
        unconditional, *conditional = block.split("...addIf<[string, string]>(")
        groups = {}
        for chunk in conditional:
            condition = re.match(
                r'Environment\.getStringValue\("(\w+)"\) (===|!==) "([^"]*)",', chunk
            )
            self.assertIsNotNone(condition, chunk)
            groups[condition.groups()] = chunk

        def names(text: str) -> set[str]:
            return set(re.findall(r'\[\s*"(\w+)",', text))

        def literals(text: str) -> dict[str, str]:
            return dict(re.findall(r'\[\s*"(\w+)",\s*"([^"]*)"\]', text))

        pack_group = groups[("TESTFX_BUILDXL_PACK", "===", "true")]
        self.assertEqual(set(build["Properties"]), names(unconditional))
        self.assertEqual(set(PACK_ONLY_PROPERTIES), names(pack_group))
        for name, value in {**literals(unconditional), **literals(pack_group)}.items():
            with self.subTest(property=name):
                self.assertEqual(pack["Properties"][name], value)
        self.assertIn(
            '["ArtifactsPackagesDir", Environment.getStringValue("TESTFX_BUILDXL_PACKAGES")]',
            pack_group,
        )

    def test_pack_targets_load_only_in_pack_mode_and_address_graph_nodes(self) -> None:
        buildxl = ROOT / "eng" / "buildxl"
        imports = ET.parse(buildxl / "msbuild.targets").getroot().findall("Import")
        self.assertEqual(
            [("pack.targets", "'$(TestFxBuildXLPack)' == 'true'")],
            [(item.get("Project"), " ".join(item.get("Condition", "").split())) for item in imports],
        )

        pack = ET.parse(buildxl / "pack.targets").getroot()
        # A packing node exempted from isolation against itself re-runs its inner builds and double-writes obj files.
        self.assertIn(
            "$(MSBuildProjectFullPath)",
            [item.get("Remove") for item in pack.iter("GraphIsolationExemptReference")],
        )
        address = pack.find("Target[@Name='_TestFxBuildXLAddressGraphInnerBuilds']")
        self.assertIsNotNone(address)
        self.assertEqual("_SdkPrepareProjectFlavorMatrix", address.get("AfterTargets"))
        self.assertEqual(
            ["%(_SdkProjectFlavorMatrixItem.UndefineProperties);BuildProjectReferences"],
            [item.text for item in address.iter("UndefineProperties")],
        )

    def test_pack_overrides_track_the_product_targets_they_replace(self) -> None:
        def normalized(element: ET.Element | None, attribute: str) -> str:
            self.assertIsNotNone(element)
            return " ".join(element.get(attribute, "").split())

        overrides = ET.parse(ROOT / "eng" / "buildxl" / "pack.targets").getroot()
        analyzers = ET.parse(
            ROOT / "src" / "Analyzers" / "MSTest.Analyzers.Package" / "MSTest.Analyzers.Package.csproj"
        ).getroot()
        product = analyzers.find("Target[@Name='_GenerateGlobalConfigs']/Exec")
        override = overrides.find("Target[@Name='_GenerateGlobalConfigs']")
        self.assertIsNotNone(override)
        generator = override.find("PropertyGroup/_TestFxGlobalConfigsGenerator")
        self.assertEqual(
            normalized(product, "EnvironmentVariables"),
            normalized(override.find("Exec"), "EnvironmentVariables"),
        )
        self.assertTrue(normalized(product, "WorkingDirectory").endswith(r"\MSTest.GlobalConfigsGenerator"))
        self.assertEqual(
            r"$(ArtifactsBinDir)MSTest.GlobalConfigsGenerator\$(Configuration)\$(NetCurrent)"
            r"\MSTest.GlobalConfigsGenerator.dll",
            generator.text,
        )
        generator_project = ET.parse(
            ROOT / "src" / "Analyzers" / "MSTest.GlobalConfigsGenerator" / "MSTest.GlobalConfigsGenerator.csproj"
        ).getroot()
        self.assertEqual("$(NetCurrent)", generator_project.findtext("PropertyGroup/TargetFramework"))
        solution = ET.parse(ROOT / "TestFx.slnx").getroot()
        self.assertEqual(
            ["src/Analyzers/MSTest.GlobalConfigsGenerator/MSTest.GlobalConfigsGenerator.csproj"],
            [
                dependency.get("Project")
                for dependency in solution.findall(
                    ".//Project[@Path='src/Analyzers/MSTest.Analyzers.Package/MSTest.Analyzers.Package.csproj']"
                    "/BuildDependency"
                )
            ],
        )

        packaged_app = ET.parse(
            ROOT / "src" / "Platform" / "Microsoft.Testing.Extensions.PackagedApp.MSBuild"
            / "Microsoft.Testing.Extensions.PackagedApp.MSBuild.csproj"
        ).getroot()
        product = packaged_app.find("Target[@Name='_AddPackagedAppControllerToPackage']")
        override = overrides.find("Target[@Name='_AddPackagedAppControllerToPackage']")
        self.assertEqual(normalized(product, "Condition"), normalized(override, "Condition"))
        self.assertIsNotNone(product.find("MSBuild"))
        self.assertIsNone(override.find("MSBuild"))
        for path, attribute in (
            ("ItemGroup/_PackagedAppControllerFramework", "Include"),
            ("Error", "Condition"),
            ("Error", "Text"),
            ("ItemGroup/_PackagedAppControllerPackageFile", "Include"),
            ("ItemGroup/_PackagedAppControllerPackageFile", "Exclude"),
            ("ItemGroup/TfmSpecificPackageFile", "Include"),
        ):
            with self.subTest(element=path, attribute=attribute):
                self.assertEqual(
                    normalized(product.find(path), attribute),
                    normalized(override.find(path), attribute),
                )
        for path in (
            "ItemGroup/_PackagedAppControllerPackageFile/ControllerTargetFramework",
            "ItemGroup/TfmSpecificPackageFile/PackagePath",
        ):
            with self.subTest(element=path):
                self.assertEqual(product.findtext(path), override.findtext(path))

    def telemetry_mac_cache(
        self, initial: dict | None, getmac: dict
    ) -> tuple[dict, str]:
        fixture = base64.b64encode(
            json.dumps({"Initial": initial, "GetMac": getmac}).encode("utf-8")
        ).decode("ascii")
        script = r"""
$ErrorActionPreference = 'Stop'
$PSNativeCommandUseErrorActionPreference = $false
Set-StrictMode -Version 2.0
$tokens = $null
$parseErrors = $null
$ast = [System.Management.Automation.Language.Parser]::ParseFile(
    '__DRIVER__', [ref] $tokens, [ref] $parseErrors)
if ($parseErrors.Count -ne 0) { throw 'The driver must parse before extracting its helper.' }
$helpers = @($ast.FindAll({
    param($node)
    $node -is [System.Management.Automation.Language.FunctionDefinitionAst] -and
        $node.Name -eq '__HELPER__'
}, $true))
if ($helpers.Count -ne 1) { throw 'Expected exactly one __HELPER__ helper.' }
# Load only the actual helper; never execute driver setup, restore or build code.
. ([scriptblock]::Create($helpers[0].Extent.Text))
$fixture = [Text.Encoding]::UTF8.GetString(
    [Convert]::FromBase64String('__FIXTURE__')) | ConvertFrom-Json -AsHashtable
# A unique per-test key keeps the real Visual Studio telemetry cache untouched.
$keyName = 'Software\TestFx.BuildXL.Tests.' + [Guid]::NewGuid().ToString('N')
$getmacCalls = 0
$global:LASTEXITCODE = 0
function getmac.exe {
    if ($args.Count -ne 0) { throw 'Visual Studio telemetry runs getmac without arguments.' }
    $script:getmacCalls++
    if ($script:fixture.GetMac.Throw) { throw 'getmac is unavailable.' }
    $global:LASTEXITCODE = $script:fixture.GetMac.ExitCode
    Write-Output ($script:fixture.GetMac.Output -split "`n")
}
try {
    if ($null -ne $fixture.Initial) {
        $key = [Microsoft.Win32.Registry]::CurrentUser.CreateSubKey($keyName)
        try {
            $kind = [Microsoft.Win32.RegistryValueKind] $fixture.Initial.Kind
            $key.SetValue('mac.address', $fixture.Initial.Value, $kind)
        }
        finally {
            $key.Dispose()
        }
    }

    __HELPER__ -KeyName $keyName
    $result = [ordered]@{ KeyExists = $false; Value = $null; Kind = $null; GetMacCalls = $getmacCalls }
    $key = [Microsoft.Win32.Registry]::CurrentUser.OpenSubKey($keyName)
    if ($null -ne $key) {
        try {
            $result.KeyExists = $true
            if ($key.GetValueNames() -contains 'mac.address') {
                $result.Value = $key.GetValue('mac.address')
                $result.Kind = $key.GetValueKind('mac.address').ToString()
            }
        }
        finally {
            $key.Dispose()
        }
    }
}
finally {
    [Microsoft.Win32.Registry]::CurrentUser.DeleteSubKeyTree($keyName, $false)
}
Write-Output ('BUILDXL_MAC_CACHE_RESULT=' + ($result | ConvertTo-Json -Compress))
"""
        script = (
            script.replace(
                "__DRIVER__", str(ROOT / "eng" / "buildxl.ps1").replace("'", "''")
            )
            .replace("__HELPER__", MAC_CACHE_HELPER)
            .replace("__FIXTURE__", fixture)
        )
        command = base64.b64encode(script.encode("utf-16-le")).decode("ascii")
        process = subprocess.run(
            [
                "pwsh", "-NoLogo", "-NoProfile", "-NonInteractive",
                "-OutputFormat", "Text", "-EncodedCommand", command,
            ],
            cwd=ROOT,
            capture_output=True,
            text=True,
            timeout=30,
            check=False,
        )
        self.assertEqual(0, process.returncode, process.stdout + process.stderr)
        self.assertEqual("", process.stderr.strip())
        marker = "BUILDXL_MAC_CACHE_RESULT="
        results = [
            line.removeprefix(marker)
            for line in process.stdout.splitlines() if line.startswith(marker)
        ]
        self.assertEqual(1, len(results), process.stdout)
        diagnostics = "\n".join(
            line for line in process.stdout.splitlines() if not line.startswith(marker)
        )
        return json.loads(results[0]), diagnostics

    @unittest.skipUnless(sys.platform == "win32", "Uses the current user's registry.")
    def test_missing_telemetry_mac_cache_is_seeded_with_visual_studio_hash(self) -> None:
        result, diagnostics = self.telemetry_mac_cache(
            None, {"Throw": False, "ExitCode": 0, "Output": GETMAC_OUTPUT}
        )
        self.assertEqual(
            {
                "KeyExists": True, "Value": SEEDED_MAC_HASH,
                "Kind": "String", "GetMacCalls": 1,
            },
            result,
        )
        self.assertIn("Cached the Visual Studio telemetry MAC hash", diagnostics)

    @unittest.skipUnless(sys.platform == "win32", "Uses the current user's registry.")
    def test_accepted_telemetry_mac_cache_is_preserved_without_getmac(self) -> None:
        cases = (
            ("string-prefix", "s:" + CACHED_HASH),
            ("repeated-string-prefix", "s:s:" + CACHED_HASH),
            ("unprefixed", CACHED_HASH),
            ("non-numeric-double-prefix", "d:" + CACHED_HASH),
        )
        for name, value in cases:
            with self.subTest(case=name):
                result, diagnostics = self.telemetry_mac_cache(
                    {"Kind": "String", "Value": value},
                    {"Throw": False, "ExitCode": 0, "Output": GETMAC_OUTPUT},
                )
                self.assertEqual(
                    {"KeyExists": True, "Value": value, "Kind": "String", "GetMacCalls": 0},
                    result,
                )
                self.assertEqual("", diagnostics.strip())

    @unittest.skipUnless(sys.platform == "win32", "Uses the current user's registry.")
    def test_rejected_telemetry_mac_cache_is_replaced(self) -> None:
        cases = (
            ("uppercase-hash", "String", "s:" + CACHED_HASH.upper()),
            ("short-hash", "String", "s:" + CACHED_HASH[:-1]),
            ("numeric-double", "String", "d:" + "1" * 64),
            ("dword", "DWord", 42),
        )
        for name, kind, value in cases:
            with self.subTest(case=name):
                result, _ = self.telemetry_mac_cache(
                    {"Kind": kind, "Value": value},
                    {"Throw": False, "ExitCode": 0, "Output": GETMAC_OUTPUT},
                )
                self.assertEqual(
                    {
                        "KeyExists": True, "Value": SEEDED_MAC_HASH,
                        "Kind": "String", "GetMacCalls": 1,
                    },
                    result,
                )

    @unittest.skipUnless(sys.platform == "win32", "Uses the current user's registry.")
    def test_unusable_getmac_result_writes_nothing_and_continues(self) -> None:
        cases = (
            ("nonzero-exit", {"Throw": False, "ExitCode": 1, "Output": GETMAC_OUTPUT},
             "getmac exited 1"),
            ("zero-address-only", {
                "Throw": False, "ExitCode": 0,
                "Output": "00-00-00-00-00-00   Media disconnected",
            }, "getmac exited 0"),
            ("empty-output", {"Throw": False, "ExitCode": 0, "Output": ""},
             "getmac exited 0"),
            ("unavailable", {"Throw": True, "ExitCode": 0, "Output": ""},
             "getmac is unavailable."),
        )
        for name, getmac, diagnostic in cases:
            with self.subTest(case=name):
                result, diagnostics = self.telemetry_mac_cache(None, getmac)
                self.assertEqual(
                    {"KeyExists": False, "Value": None, "Kind": None, "GetMacCalls": 1},
                    result,
                )
                self.assertIn("Visual Studio telemetry MAC hash not cached", diagnostics)
                self.assertIn(diagnostic, diagnostics)

    def assert_collected(
        self,
        source_files: dict[str, bytes],
        expected_files: dict[str, bytes],
        expected_evidence: bool,
        collections: int = 1,
    ) -> None:
        with tempfile.TemporaryDirectory(prefix="buildxl-pipeline-tests-") as directory:
            root = Path(directory)
            sources = root / "source checkout with spaces"
            sources.mkdir()
            buildxl = sources / "artifacts" / "buildxl"
            evidence = root / "artifact staging with spaces" / "buildxl-Debug-Attempt2"
            for relative, content in source_files.items():
                path = buildxl / relative
                path.parent.mkdir(parents=True, exist_ok=True)
                path.write_bytes(content)

            # Execute the shipping collector, not a Python implementation of its allowlist.
            script = self.step(COLLECT_STEP)["pwsh"]
            for variable, value in {
                "$(Build.SourcesDirectory)": sources,
                "$(BuildXLArtifacts)": evidence,
            }.items():
                script = script.replace(variable, str(value).replace("'", "''"))
            self.assertNotRegex(script, r"\$\([A-Za-z][A-Za-z0-9.]*\)")
            command = base64.b64encode(
                ("$ErrorActionPreference = 'Stop'\n" + script).encode("utf-16-le")
            ).decode("ascii")
            for collection in range(1, collections + 1):
                with self.subTest(collection=collection):
                    if collection > 1:
                        self.assertTrue(evidence.is_dir(), "Reuse the populated staging tree.")
                    process = subprocess.run(
                        [
                            "pwsh", "-NoLogo", "-NoProfile", "-NonInteractive",
                            "-OutputFormat", "Text", "-EncodedCommand", command,
                        ],
                        cwd=root,
                        capture_output=True,
                        text=True,
                        timeout=30,
                        check=False,
                    )
                    self.assertEqual(0, process.returncode, process.stdout + process.stderr)
                    self.assertEqual("", process.stderr.strip())
                    marker = "##vso[task.setvariable variable=BuildXLHasEvidence]"
                    self.assertEqual(
                        [marker + ("True" if expected_evidence else "False")],
                        [line for line in process.stdout.splitlines() if line.startswith(marker)],
                    )
                    self.assertEqual(expected_files, file_contents(evidence))
                    self.assertEqual(expected_evidence, evidence.exists())
                    for excluded in (
                        "bin", "cache", "Out/Bin", "Out/Cache",
                        "logs/logs", "Out/Logs/MSBuild/MSBuild",
                    ):
                        self.assertFalse((evidence / excluded).exists(), excluded)
                    self.assertEqual(
                        source_files, file_contents(buildxl), "Sources must be copied, not moved."
                    )
                    if not source_files:
                        self.assertFalse(buildxl.exists(), "A missing output root must remain absent.")

    def test_collector_retains_exact_complete_outputs_and_excludes_bin_and_cache(self) -> None:
        self.assert_collected(
            source_files={
                "toolchain.json": b'{"engine":"fixture","configuration":"Debug"}\n',
                "config.dsc": b"export const projects = ['TestFx.slnx'];\r\n",
                "logs/BuildXL.log": b"\x00engine log\r\n\xff",
                "logs/worker 01/BuildXL.stats": b"pip-count=17\n",
                "Out/Logs/MSBuild/graph.binlog": b"\x00\x01graph\xfe",
                "Out/Logs/MSBuild/project with spaces/build.log": b"project built\n",
                "bin/Debug/product.dll": b"not evidence: binary",
                "cache/actions/state": b"not evidence: action cache",
                "Out/Bin/product.dll": b"not evidence: engine output",
                "Out/Cache/fingerprints": b"not evidence: fingerprints",
                "Out/Logs/unrelated.log": b"not evidence: sibling log",
                "unrelated.json": b'{"not":"evidence"}',
            },
            expected_files={
                "toolchain.json": b'{"engine":"fixture","configuration":"Debug"}\n',
                "config.dsc": b"export const projects = ['TestFx.slnx'];\r\n",
                "logs/BuildXL.log": b"\x00engine log\r\n\xff",
                "logs/worker 01/BuildXL.stats": b"pip-count=17\n",
                "Out/Logs/MSBuild/graph.binlog": b"\x00\x01graph\xfe",
                "Out/Logs/MSBuild/project with spaces/build.log": b"project built\n",
            },
            expected_evidence=True,
        )

    def test_collector_repeated_collection_does_not_nest_log_directories(self) -> None:
        self.assert_collected(
            source_files={
                "toolchain.json": b'{"engine":"repeated-collection"}\n',
                "config.dsc": b"repeated configuration\r\n",
                "logs/BuildXL.log": b"\x00engine log\xff",
                "logs/worker 01/BuildXL.stats": b"pip-count=23\n",
                "Out/Logs/MSBuild/graph.binlog": b"\x00repeated graph\xfe",
                "Out/Logs/MSBuild/project with spaces/build.log": b"project built again\n",
                "bin/Debug/product.dll": b"excluded binary",
                "cache/state": b"excluded cache",
            },
            expected_files={
                "toolchain.json": b'{"engine":"repeated-collection"}\n',
                "config.dsc": b"repeated configuration\r\n",
                "logs/BuildXL.log": b"\x00engine log\xff",
                "logs/worker 01/BuildXL.stats": b"pip-count=23\n",
                "Out/Logs/MSBuild/graph.binlog": b"\x00repeated graph\xfe",
                "Out/Logs/MSBuild/project with spaces/build.log": b"project built again\n",
            },
            expected_evidence=True,
            collections=2,
        )

    def test_collector_retains_partial_failure_outputs_with_missing_directories(self) -> None:
        cases = (
            (
                "toolchain-only",
                {"toolchain.json": b'{"restore":"failed"}\n', "bin/drop.dll": b"excluded"},
                {"toolchain.json": b'{"restore":"failed"}\n'},
            ),
            (
                "configuration-only",
                {"config.dsc": b"partial configuration\r\n", "cache/state": b"excluded"},
                {"config.dsc": b"partial configuration\r\n"},
            ),
            (
                "engine-logs-only",
                {"logs/engine failure.log": b"\x00failed before project graph\xff"},
                {"logs/engine failure.log": b"\x00failed before project graph\xff"},
            ),
            (
                "project-logs-only",
                {"Out/Logs/MSBuild/failure.binlog": b"\x00project failure\xfe"},
                {"Out/Logs/MSBuild/failure.binlog": b"\x00project failure\xfe"},
            ),
        )
        for name, source_files, expected_files in cases:
            with self.subTest(case=name):
                self.assert_collected(source_files, expected_files, expected_evidence=True)

    def test_collector_handles_absent_outputs_without_secondary_failure(self) -> None:
        self.assert_collected({}, {}, expected_evidence=False)

    def test_collector_ignores_bin_and_cache_only_outputs(self) -> None:
        self.assert_collected(
            source_files={
                "bin/Release/product.dll": b"binary only",
                "cache/engine.state": b"cache only",
                "Out/Bin/native.dll": b"native binary only",
                "Out/Cache/fingerprints": b"fingerprints only",
                "Out/Logs/not-msbuild.log": b"unrelated log only",
            },
            expected_files={},
            expected_evidence=False,
        )


if __name__ == "__main__":
    unittest.main(verbosity=2)
