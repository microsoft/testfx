#!/usr/bin/env python3
"""Focused CI contracts for native BuildXL builds and retained build evidence."""

from __future__ import annotations

import base64
import json
from pathlib import Path
import subprocess
import tempfile
import unittest

import yaml


ROOT = Path(__file__).resolve().parents[2]
JOURNAL_STEP = "Initialize BuildXL change journal"
CACHE_STEP = "Cache BuildXL actions"
BUILD_STEP = "Build repository with BuildXL"
COLLECT_STEP = "Collect BuildXL engine and project logs"
PUBLISH_STEP = "Publish BuildXL build logs"


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
        cls.steps = cls.job["steps"]

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
        # A direct build-stage gate cannot be nested under SkipTests.
        self.assertEqual(
            [[{"template": "/eng/pipelines/jobs/buildxl.yml"}]], imports
        )
        all_imports = [
            node["template"]
            for node in mappings(self.pipeline)
            if node.get("template") == "/eng/pipelines/jobs/buildxl.yml"
        ]
        self.assertEqual(["/eng/pipelines/jobs/buildxl.yml"], all_imports)
        self.assertNotIn("SkipTests", self.job.get("condition", ""))

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
