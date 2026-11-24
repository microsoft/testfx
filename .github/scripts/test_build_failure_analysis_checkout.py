#!/usr/bin/env python3

import os
import pathlib
import shutil
import subprocess
import tempfile
import unittest

import yaml


BASH = "bash"
if os.name == "nt":
    git_path = shutil.which("git")
    if git_path is None:
        raise RuntimeError("Git for Windows is required to exercise the checkout workflow.")
    # The WSL bash launcher does not inherit these per-test environment values.
    candidates = (
        pathlib.Path(git_path).resolve().parents[1] / "bin" / "bash.exe",
        pathlib.Path(os.environ["ProgramFiles"]) / "Git" / "bin" / "bash.exe",
    )
    git_bash = next((candidate for candidate in candidates if candidate.is_file()), None)
    if git_bash is None:
        raise RuntimeError("Git for Windows Bash is required to exercise the checkout workflow.")
    BASH = str(git_bash)


ROOT = pathlib.Path(__file__).resolve().parents[2]
WORKFLOW_PATH = pathlib.Path(
    os.environ.get(
        "BUILD_ANALYSIS_WORKFLOW",
        ROOT / ".github/workflows/build-failure-analysis.md",
    )
)
WORKFLOW = yaml.safe_load(WORKFLOW_PATH.read_text(encoding="utf-8").split("---", 2)[1])
ATTACH_SCRIPT = next(
    (
        step["run"]
        for step in WORKFLOW["pre-agent-steps"]
        if step["name"] == "Attach the verified analysis revision"
    ),
    "",
)


class BuildFailureAnalysisCheckoutTests(unittest.TestCase):
    def setUp(self) -> None:
        self.temporary = tempfile.TemporaryDirectory()
        self.addCleanup(self.temporary.cleanup)
        self.root = pathlib.Path(self.temporary.name)
        self.origin = self.root / "origin"
        self.workspace = self.root / "workspace"
        self.environment = {
            **os.environ,
            "GIT_CONFIG_COUNT": "2",
            "GIT_CONFIG_KEY_0": "user.name",
            "GIT_CONFIG_VALUE_0": "Checkout Test",
            "GIT_CONFIG_KEY_1": "user.email",
            "GIT_CONFIG_VALUE_1": "checkout-test@example.invalid",
        }
        self.git("init", "--quiet", "--initial-branch=main", str(self.origin))
        self.git("-C", str(self.origin), "commit", "--quiet", "--allow-empty", "-m", "Base")
        self.git("-C", str(self.origin), "switch", "--quiet", "--create", "analysis-target")
        self.git("-C", str(self.origin), "commit", "--quiet", "--allow-empty", "-m", "PR head")
        self.head = self.git("-C", str(self.origin), "rev-parse", "HEAD")
        self.git("-C", str(self.origin), "update-ref", "refs/pull/42/head", self.head)
        self.git("-C", str(self.origin), "switch", "--quiet", "main")
        self.git("init", "--quiet", str(self.workspace))
        self.git("-C", str(self.workspace), "remote", "add", "origin", self.origin.as_uri())

    def git(self, *arguments: str) -> str:
        result = subprocess.run(
            ["git", *arguments],
            env=self.environment,
            text=True,
            capture_output=True,
            timeout=30,
            check=True,
        )
        return result.stdout.strip()

    def checkout(self, checkout_ref: str = "analysis-target") -> None:
        values = {
            "${{ needs.fetch-binlog.outputs.pr-head-sha }}": self.head,
            "${{ needs.fetch-binlog.outputs.pr-checkout-ref }}": checkout_ref,
        }
        ref = values[WORKFLOW["checkout"][0]["ref"]]
        result = subprocess.run(
            ["git", "-C", str(self.workspace), "fetch", "--quiet", "--depth=1", "origin", ref],
            env=self.environment,
            text=True,
            capture_output=True,
            timeout=30,
        )
        self.assertEqual(0, result.returncode, result.stderr)
        if ref == self.head or ref.startswith("refs/pull/"):
            self.git("-C", str(self.workspace), "checkout", "--quiet", "--detach", "FETCH_HEAD")
        else:
            self.git("-C", str(self.workspace), "checkout", "--quiet", "-b", ref, "FETCH_HEAD")

    def attach(self, checkout_ref: str = "analysis-target", expected_sha: str | None = None) -> subprocess.CompletedProcess:
        return subprocess.run(
            [BASH, "-c", ATTACH_SCRIPT],
            cwd=self.workspace,
            env={
                **self.environment,
                "EXPECTED_HEAD_SHA": self.head if expected_sha is None else expected_sha,
                "ANALYSIS_CHECKOUT_REF": checkout_ref,
            },
            text=True,
            capture_output=True,
            timeout=30,
        )

    def assert_attached_revision(self) -> None:
        result = self.attach()
        self.assertEqual(0, result.returncode, result.stdout + result.stderr)
        self.assertEqual(self.head, self.git("-C", str(self.workspace), "rev-parse", "HEAD"))
        self.assertEqual(
            "analysis-target",
            self.git("-C", str(self.workspace), "branch", "--show-current"),
        )
        self.assertEqual(
            self.head,
            self.git("-C", str(self.workspace), "rev-parse", "refs/remotes/origin/analysis-target"),
        )

    def test_deleted_branch_checks_out_validated_commit(self) -> None:
        self.git("-C", str(self.origin), "branch", "--delete", "--force", "analysis-target")

        self.checkout()

        self.assert_attached_revision()
        branches = self.git("-C", str(self.origin), "for-each-ref", "--format=%(refname)", "refs/heads")
        self.assertEqual("refs/heads/main", branches)

    def test_moving_branch_does_not_change_analysis_revision(self) -> None:
        self.git("-C", str(self.origin), "switch", "--quiet", "analysis-target")
        self.git("-C", str(self.origin), "commit", "--quiet", "--allow-empty", "-m", "Newer head")

        self.checkout()

        self.assert_attached_revision()

    def test_same_repo_patch_baseline_is_original_head(self) -> None:
        self.checkout()
        self.assert_attached_revision()

        self.git("-C", str(self.workspace), "commit", "--quiet", "--allow-empty", "-m", "Agent fix")

        self.assertEqual(
            "1",
            self.git("-C", str(self.workspace), "rev-list", "--count", "origin/analysis-target..HEAD"),
        )

    def test_fork_remains_detached_without_same_named_origin_branch(self) -> None:
        self.checkout("refs/pull/42/head")

        result = self.attach("refs/pull/42/head")

        self.assertEqual(0, result.returncode, result.stdout + result.stderr)
        self.assertEqual(self.head, self.git("-C", str(self.workspace), "rev-parse", "HEAD"))
        self.assertEqual("", self.git("-C", str(self.workspace), "branch", "--show-current"))
        self.assertEqual(
            "",
            self.git("-C", str(self.workspace), "for-each-ref", "--format=%(refname)", "refs/remotes"),
        )

    def test_mismatched_checkout_is_rejected_before_creating_refs(self) -> None:
        self.checkout()

        result = self.attach(expected_sha="0" * 40)

        self.assertNotEqual(0, result.returncode)
        self.assertIn("The checkout does not match the revision", result.stdout)
        self.assertEqual("", self.git("-C", str(self.workspace), "branch", "--show-current"))

    def test_invalid_branch_is_rejected_before_creating_refs(self) -> None:
        self.checkout()

        result = self.attach("--orphan")

        self.assertNotEqual(0, result.returncode)
        self.assertEqual("", self.git("-C", str(self.workspace), "branch", "--show-current"))
        self.assertEqual(
            "",
            self.git("-C", str(self.workspace), "for-each-ref", "--format=%(refname)", "refs/remotes"),
        )


if __name__ == "__main__":
    unittest.main()
