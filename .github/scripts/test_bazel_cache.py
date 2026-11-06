#!/usr/bin/env python3
"""Focused launcher and workflow contracts for persistent Bazel CI caches."""

from __future__ import annotations

import base64
import fnmatch
import json
import os
from pathlib import Path
import re
import shlex
import subprocess
import tempfile
import unittest

import yaml


ROOT = Path(__file__).resolve().parents[2]
CONFIG_PROBE = r"""
$ErrorActionPreference = 'Stop'
$errors = $null
$ast = [Management.Automation.Language.Parser]::ParseFile(
    $env:BAZEL_CONFIG_SCRIPT, [ref]$null, [ref]$errors)
if ($errors.Count) { throw 'Bazel wrapper does not parse.' }
$definition = $ast.Find({
    param($node)
    $node -is [Management.Automation.Language.FunctionDefinitionAst] -and
        $node.Name -eq 'New-BazelInvocationConfig'
}, $true)
if ($null -eq $definition) { throw 'Missing invocation configuration function.' }
. ([scriptblock]::Create($definition.Extent.Text))
$config = New-BazelInvocationConfig -OutputRoot $env:BAZEL_CONFIG_OUTPUT `
    -CacheRoot $env:BAZEL_CONFIG_CACHE -ProfileRoot $env:BAZEL_CONFIG_PROFILES
ConvertTo-Json -InputObject @{ Path = $config } -Compress
"""

PRUNE_PROBE = r"""
$ErrorActionPreference = 'Stop'
$errors = $null
$ast = [Management.Automation.Language.Parser]::ParseInput(
    $env:BAZEL_PRUNE_SCRIPT, [ref]$null, [ref]$errors)
if ($errors.Count) { throw 'Snapshot maintenance does not parse.' }
$definition = $ast.Find({
    param($node)
    $node -is [Management.Automation.Language.FunctionDefinitionAst] -and
        $node.Name -eq 'Limit-BazelCacheStore'
}, $true)
if ($null -eq $definition) { throw 'Missing cache-store limiter.' }
. ([scriptblock]::Create($definition.Extent.Text))
Limit-BazelCacheStore $env:BAZEL_PRUNE_PATH ([long]$env:BAZEL_PRUNE_LIMIT) |
    ConvertTo-Json -Compress
"""


class SnapshotPruningTests(unittest.TestCase):
    def invoke(self, path: Path, limit: int) -> subprocess.CompletedProcess[str]:
        workflow = yaml.safe_load(
            (ROOT / ".github" / "workflows" / "bazel.yml").read_text(encoding="utf-8")
        )
        script = next(
            step["run"]
            for step in workflow["jobs"]["baseline"]["steps"]
            if step.get("name") == "Bound Bazel cache snapshots"
        )
        environment = os.environ.copy()
        environment.update(
            BAZEL_PRUNE_SCRIPT=script, BAZEL_PRUNE_PATH=str(path), BAZEL_PRUNE_LIMIT=str(limit)
        )
        command = base64.b64encode(PRUNE_PROBE.encode("utf-16-le")).decode("ascii")
        return subprocess.run(
            ["pwsh", "-NoLogo", "-NoProfile", "-NonInteractive", "-EncodedCommand", command],
            env=environment,
            capture_output=True,
            text=True,
            timeout=30,
            check=False,
        )

    def test_snapshot_limit_keeps_newest_files_and_bounds_payload(self) -> None:
        cases = (
            ("fits", (("ac/old", 4, 100), ("cas/new", 4, 200)), 8, {"ac/old", "cas/new"}),
            ("lru", (("cas/old", 8, 100), ("cas/new", 4, 200)), 4, {"cas/new"}),
            ("tie", (("ac/old", 4, 100), ("cas/new", 4, 100)), 4, {"cas/new"}),
            ("oversized", (("cas/large", 8, 100),), 4, set()),
            ("empty", (), 4, set()),
        )
        for name, entries, limit, expected in cases:
            with self.subTest(case=name), tempfile.TemporaryDirectory(
                prefix="bazel-snapshot-tests-"
            ) as directory:
                root = Path(directory)
                for relative, length, timestamp in entries:
                    path = root / relative
                    path.parent.mkdir(parents=True, exist_ok=True)
                    path.write_bytes(b"x" * length)
                    os.utime(path, (timestamp, timestamp))
                process = self.invoke(root, limit)
                self.assertEqual(0, process.returncode, process.stderr)
                actual = {path.relative_to(root).as_posix() for path in root.rglob("*") if path.is_file()}
                self.assertEqual(expected, actual)
                size = sum(path.stat().st_size for path in root.rglob("*") if path.is_file())
                self.assertLessEqual(size, limit)
                report = json.loads(process.stdout)
                self.assertEqual(sum(entry[1] for entry in entries), report["BytesBefore"])
                self.assertEqual(size, report["BytesAfter"])
                self.assertEqual(limit, report["Limit"])
                self.assertEqual(len(entries) - len(expected), report["Removed"])

    def test_missing_store_and_invalid_budget_fail_explicitly(self) -> None:
        with tempfile.TemporaryDirectory(prefix="bazel-snapshot-errors-") as directory:
            root = Path(directory)
            for name, path, limit in (
                ("missing", root / "missing", 4),
                ("invalid-budget", root, 0),
            ):
                with self.subTest(case=name):
                    process = self.invoke(path, limit)
                    self.assertNotEqual(0, process.returncode)
                    self.assertTrue(process.stderr.strip())

    def test_links_are_rejected_without_touching_the_target(self) -> None:
        for case in ("root", "entry"):
            with self.subTest(case=case), tempfile.TemporaryDirectory(
                prefix="bazel-snapshot-links-"
            ) as directory:
                root = Path(directory)
                target = root / "outside"
                target.mkdir()
                payload = target / "keep"
                payload.write_bytes(b"untouched")
                store = root / "actions"
                if case == "root":
                    store.symlink_to(target, target_is_directory=True)
                else:
                    store.mkdir()
                    (store / "link").symlink_to(target, target_is_directory=True)
                process = self.invoke(store, 1)
                self.assertNotEqual(0, process.returncode)
                self.assertIn("Persistent cache store contains a link", process.stderr)
                self.assertEqual(b"untouched", payload.read_bytes())


class InvocationConfigTests(unittest.TestCase):
    def setUp(self) -> None:
        self.temporary = tempfile.TemporaryDirectory(prefix="bazel-cache-tests-")
        self.addCleanup(self.temporary.cleanup)
        self.root = Path(self.temporary.name)
        self.output = self.root / "output"

    def invoke(
        self, cache: str | Path = "", profiles: str | Path = ""
    ) -> subprocess.CompletedProcess[str]:
        environment = os.environ.copy()
        environment.update(
            BAZEL_CONFIG_SCRIPT=str(ROOT / "eng" / "bazel.ps1"),
            BAZEL_CONFIG_OUTPUT=str(self.output),
            BAZEL_CONFIG_CACHE=str(cache),
            BAZEL_CONFIG_PROFILES=str(profiles),
        )
        command = base64.b64encode(CONFIG_PROBE.encode("utf-16-le")).decode("ascii")
        return subprocess.run(
            ["pwsh", "-NoLogo", "-NoProfile", "-NonInteractive", "-EncodedCommand", command],
            env=environment,
            capture_output=True,
            text=True,
            timeout=30,
            check=False,
        )

    def configuration(self, cache: Path | str = "", profiles: Path | str = "") -> Path:
        process = self.invoke(cache, profiles)
        self.assertEqual(0, process.returncode, process.stderr)
        return Path(json.loads(process.stdout)["Path"])

    def test_unconfigured_invocations_leave_default_paths_untouched(self) -> None:
        process = self.invoke()
        self.assertEqual(0, process.returncode, process.stderr)
        self.assertIsNone(json.loads(process.stdout)["Path"])
        self.assertFalse(self.output.exists())

    def test_only_downloads_and_actions_are_persistent_in_paths_with_spaces(self) -> None:
        cache = self.root / "cache with spaces!"
        config = self.configuration(cache)
        self.assertEqual(
            [
                ["common", f"--repository_cache={(cache / 'downloads').as_posix()}"],
                ["common", f"--repo_contents_cache={(self.output / 'repositories').as_posix()}"],
                ["build", f"--disk_cache={(cache / 'actions').as_posix()}"],
            ],
            [shlex.split(line) for line in config.read_text(encoding="utf-8").splitlines()],
        )
        self.assertEqual(self.output, config.parent)
        self.assertFalse(cache.exists(), "Bazel, not the launcher, materializes cache entries.")

    def test_profiles_are_unique_and_outside_persistent_stores(self) -> None:
        profiles = self.root / "profiles with spaces!"
        first = self.configuration(profiles=profiles)
        second = self.configuration(profiles=profiles)
        self.assertNotEqual(first, second)
        paths = []
        for config in (first, second):
            entries = [
                shlex.split(line) for line in config.read_text(encoding="utf-8").splitlines()
            ]
            self.assertEqual(["build", "build"], [entry[0] for entry in entries])
            self.assertTrue(entries[0][1].startswith("--profile="))
            self.assertTrue(entries[1][1].startswith("--build_event_json_file="))
            profile = Path(entries[0][1].split("=", 1)[1])
            events = Path(entries[1][1].split("=", 1)[1])
            self.assertEqual(profiles, profile.parent)
            self.assertEqual(profiles, events.parent)
            self.assertEqual(
                profile.name.removesuffix(".profile.gz"),
                events.name.removesuffix(".bep.jsonl"),
            )
            paths.extend((profile, events))
        self.assertEqual(4, len(set(paths)))

    def test_relative_and_multiline_roots_fail_before_writing_configuration(self) -> None:
        for field in ("cache", "profiles"):
            for name, value in (
                ("relative", "relative-cache"),
                ("multiline", str(self.root / "cache") + "\nbuild --cache_test_results=yes"),
            ):
                with self.subTest(field=field, case=name):
                    process = self.invoke(**{field: value})
                    self.assertNotEqual(0, process.returncode)
                    self.assertIn("absolute, single-line paths", process.stderr)
                    self.assertFalse(self.output.exists())


class WorkflowCacheTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls) -> None:
        workflow = yaml.safe_load(
            (ROOT / ".github" / "workflows" / "bazel.yml").read_text(encoding="utf-8")
        )
        cls.workflow = workflow
        # PyYAML's YAML 1.1 resolver reads the unquoted Actions `on` key as True.
        cls.triggers = workflow.get("on", workflow.get(True))
        cls.steps = workflow["jobs"]["baseline"]["steps"]

    def test_successful_main_seeds_and_prs_only_restore(self) -> None:
        self.assertEqual(["main"], self.triggers["push"]["branches"])
        self.assertEqual(
            self.triggers["pull_request"]["paths"], self.triggers["push"]["paths"]
        )
        self.assertEqual({"contents": "read"}, self.workflow["permissions"])
        saves = [step for step in self.steps if "/cache/save@" in step.get("uses", "")]
        self.assertEqual(2, len(saves))
        for save in saves:
            self.assertEqual(
                "success() && github.ref == 'refs/heads/main' && "
                "github.event_name != 'pull_request'",
                save["if"],
            )

    def test_snapshots_are_separate_and_reusable_across_source_revisions(self) -> None:
        restores = [step for step in self.steps if "/cache/restore@" in step.get("uses", "")]
        self.assertEqual(2, len(restores))
        for name in ("downloads", "actions"):
            restore = next(step for step in restores if step["id"] == name)
            settings = restore["with"]
            self.assertEqual(f"artifacts/bazel-cache/{name}", settings["path"])
            key = settings["key"]
            for expression in ("runner.os", "runner.arch", "github.run_id", "github.run_attempt"):
                self.assertIn("${{ " + expression + " }}", key)
            self.assertNotIn("github.sha", key)
            self.assertEqual(
                key.split("${{ github.run_id }}", 1)[0], settings["restore-keys"].strip()
            )
            self.assertNotIn("enableCrossOsArchive", settings)
            save = next(
                step for step in self.steps if step.get("name") == f"Save Bazel {name}"
            )
            self.assertEqual(settings["path"], save["with"]["path"])
            self.assertEqual(
                "${{ steps." + name + ".outputs.cache-primary-key }}", save["with"]["key"]
            )
        actions = next(step for step in restores if step["id"] == "actions")
        for source in (".bazelversion", "MODULE.bazel", ".bazelrc", "eng/bazel/patches/**"):
            self.assertIn(source, actions["with"]["restore-keys"])

    def test_cache_hits_never_skip_validation_or_test_execution(self) -> None:
        for name in (
            "Build, test, and validate native consumers",
            "Validate native packages and self-contained runtimes",
        ):
            step = next(step for step in self.steps if step.get("name") == name)
            self.assertNotIn("if", step)
        source = (ROOT / "eng" / "bazel" / "validate.ps1").read_text(encoding="utf-8")
        for name in ("portable-tests", "mixed-results"):
            arguments = re.search(
                rf"Invoke-BazelStep '{name}' @\((.*?)\)", source, re.DOTALL
            )
            self.assertIsNotNone(arguments)
            self.assertIn("'--nocache_test_results'", arguments.group(1))

    def test_server_stops_before_save_and_profiles_remain_evidence(self) -> None:
        stop = next(
            step for step in self.steps if step.get("name") == "Stop the worktree's Bazel server"
        )
        self.assertEqual("always()", stop["if"])
        for step in self.steps:
            if "/cache/save@" in step.get("uses", ""):
                self.assertLess(self.steps.index(stop), self.steps.index(step))
        upload = next(
            step for step in self.steps if step.get("name") == "Preserve native build evidence"
        )
        self.assertEqual("always()", upload["if"])
        self.assertIn("artifacts/bazel-profiles/", upload["with"]["path"].splitlines())
        self.assertNotIn("artifacts/bazel-cache", upload["with"]["path"])

    def test_snapshot_budgets_are_applied_after_shutdown_before_main_saves(self) -> None:
        prune = next(
            step for step in self.steps if step.get("name") == "Bound Bazel cache snapshots"
        )
        self.assertEqual(
            "success() && github.ref == 'refs/heads/main' && "
            "github.event_name != 'pull_request'",
            prune["if"],
        )
        stop = next(
            step for step in self.steps if step.get("name") == "Stop the worktree's Bazel server"
        )
        self.assertLess(self.steps.index(stop), self.steps.index(prune))
        for step in self.steps:
            if "/cache/save@" in step.get("uses", ""):
                self.assertLess(self.steps.index(prune), self.steps.index(step))
        self.assertIn(
            "Limit-BazelCacheStore (Join-Path $env:TESTFX_BAZEL_CACHE_ROOT 'downloads') 1536MB",
            prune["run"],
        )
        self.assertIn(
            "Limit-BazelCacheStore (Join-Path $env:TESTFX_BAZEL_CACHE_ROOT 'actions') 512MB",
            prune["run"],
        )

    def test_guard_runs_when_each_inspected_input_changes(self) -> None:
        guard = yaml.safe_load(
            (ROOT / ".github" / "workflows" / "check-action-pins.yml").read_text(encoding="utf-8")
        )
        triggers = guard.get("on", guard.get(True))
        for event in ("pull_request", "push"):
            for source in (
                ".github/scripts/test_bazel_cache.py",
                ".github/workflows/bazel.yml",
                "eng/bazel.ps1",
                "eng/bazel/validate.ps1",
            ):
                with self.subTest(event=event, source=source):
                    self.assertTrue(
                        any(fnmatch.fnmatchcase(source, pattern) for pattern in triggers[event]["paths"]),
                        f"{event} does not run the contract for {source}",
                    )


if __name__ == "__main__":
    unittest.main(verbosity=2)
