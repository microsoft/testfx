#!/usr/bin/env python3
"""Offline regression tests for the four workflow-warning runtime contracts.

Run without arguments in CI, or use --workflow-root for retained-test baseline
comparisons. WORKFLOW_WARNING_TMP optionally places owned fixtures in task
artifact storage. No test calls a live GitHub API or writes to this checkout.
"""

import argparse
import hashlib
import json
import os
import pathlib
import re
import shlex
import shutil
import subprocess
import sys
import tempfile
import unittest

import yaml


ROOT = pathlib.Path(__file__).resolve().parents[2]
WORKFLOW_ROOT = pathlib.Path(
    os.environ.get("WORKFLOW_WARNING_ROOT", ROOT / ".github/workflows")
)
BASH = shutil.which("bash")
if os.name == "nt":
    # Do not accidentally select the WSL launcher, which loses fixture env.
    BASH = str(pathlib.Path(os.environ["ProgramFiles"]) / "Git/bin/bash.exe")
REAL_GIT = shutil.which("git")
REAL_GH = shutil.which("gh")
TIMEOUT = 45
FIXTURE_TOKEN = "workflow-warning-fixture-not-a-secret"
REPOSITORY = "fixture/workflows"
BRANCH = "automation/unskip-closed-tests-420-2"
PR_URL = "https://github.invalid/fixture/workflows/pull/91"


def authored(name: str) -> tuple[dict, str]:
    _, frontmatter, body = (WORKFLOW_ROOT / name).read_text(encoding="utf-8").split("---", 2)
    return yaml.safe_load(frontmatter), body


def compiled(name: str) -> dict:
    return yaml.safe_load((WORKFLOW_ROOT / name).read_text(encoding="utf-8"))


def manifest(name: str) -> dict:
    lines = (WORKFLOW_ROOT / name).read_text(encoding="utf-8").splitlines()
    return json.loads(next(line.removeprefix("# gh-aw-manifest: ") for line in lines
                           if line.startswith("# gh-aw-manifest: ")))


def step_with_id(steps: list[dict], step_id: str) -> dict:
    return next(step for step in steps if step.get("id") == step_id)


def publisher(frontmatter: dict) -> dict:
    return frontmatter["safe-outputs"]["jobs"]["apply-verified-unskips"]


def checkout_steps(job: dict) -> list[dict]:
    return [step for step in job["steps"]
            if step.get("uses", "").startswith("actions/checkout@")]


def publisher_step(job: dict) -> dict:
    return next(step for step in job["steps"]
                if step.get("name") == "Publish one verified draft pull request")


# This stub handles only the authored scripts' exact GitHub operations. Unknown
# commands fail closed. Credential protocol requests alone go to the real gh.
OFFLINE_GH = r'''
import json
import os
import pathlib
import sys

sys.stdout.reconfigure(newline="\n")
root = pathlib.Path(os.environ["FIXTURE_ROOT"])
mode, *args = sys.argv[1:]
state_path = root / "state.json"
state = json.loads(state_path.read_text(encoding="utf-8"))
event = {"kind": mode, "args": args}
if mode == "manifest_move":
    event["manifest"] = json.loads(pathlib.Path(args[0]).read_text(encoding="utf-8"))
if mode == "gh" and args[:2] == ["pr", "create"]:
    body_file = args[args.index("--body-file") + 1]
    event.update(body_file=body_file, body=pathlib.Path(body_file).read_text(encoding="utf-8"))
    state["created_body"] = event["body"]
with (root / "events.jsonl").open("a", encoding="utf-8") as log:
    log.write(json.dumps(event) + "\n")
if mode != "gh":
    sys.exit(0)
if args[:2] == ["pr", "view"] and "baseRefOid,headRefOid" in args:
    if state.get("api_exit"):
        sys.exit(state["api_exit"])
    if not args[2].isdigit() or int(args[2]) <= 0:
        sys.exit(1)
    print(json.dumps(state["pr_response"]))
elif args[:1] == ["api"] and args[1] == "repos/" + os.environ["EXPECTED_REPOSITORY"]:
    print("main")
elif args[:1] == ["api"] and "/commits/main" in args[1]:
    index = state.get("head_reads", 0)
    print(state["heads"][min(index, len(state["heads"]) - 1)])
    state["head_reads"] = index + 1
elif args[:2] == ["pr", "list"]:
    index = state.get("list_reads", 0)
    print(state["duplicates"][min(index, len(state["duplicates"]) - 1)])
    state["list_reads"] = index + 1
elif args[:2] == ["pr", "create"]:
    print(os.environ["FIXTURE_PR_URL"])
elif args[:2] == ["pr", "close"]:
    state["closed"] = True
elif args[:2] == ["pr", "view"] and "state" in args and "--jq" in args:
    print("CLOSED" if state.get("closed") else "OPEN")
elif args[:2] == ["pr", "view"] and "baseRefName,baseRefOid,body,isDraft,headRefName,number,state,url" in args:
    print(json.dumps({
        "baseRefName": "main", "baseRefOid": state["live_base"],
        "body": state["created_body"], "isDraft": True,
        "headRefName": os.environ["FIXTURE_BRANCH"], "number": 91,
        "state": "OPEN", "url": os.environ["FIXTURE_PR_URL"],
    }))
else:
    sys.exit("Unexpected offline gh command: " + repr(args))
state_path.write_text(json.dumps(state), encoding="utf-8")
'''


class Fixture:
    """Owns all mutable resources; real Git operates only inside this directory."""

    def __init__(self, case: unittest.TestCase) -> None:
        temporary = tempfile.TemporaryDirectory(
            prefix="gh-aw-warning-", dir=os.environ.get("WORKFLOW_WARNING_TMP")
        )
        case.addCleanup(temporary.cleanup)
        self.root = pathlib.Path(temporary.name)
        self.repo = self.root / "repo"
        self.bin = self.root / "bin"
        self.runner = self.root / "runner"
        for directory in (self.bin, self.runner, self.root / "home", self.root / "gh-config"):
            directory.mkdir()
        self.global_config = self.root / "gitconfig"
        self.global_config.write_text("", encoding="utf-8")
        self.env = {
            key: value for key, value in os.environ.items()
            if not key.startswith(("GIT_", "GH_", "GITHUB_"))
            and key not in ("BASH_ENV", "ENV")
        }
        self.env.update({
            "PATH": str(self.bin) + os.pathsep + os.environ["PATH"],
            "HOME": (self.root / "home").as_posix(),
            "GH_CONFIG_DIR": (self.root / "gh-config").as_posix(),
            "GIT_CONFIG_GLOBAL": self.global_config.as_posix(),
            "GIT_CONFIG_NOSYSTEM": "1",
            "GIT_TERMINAL_PROMPT": "0",
            "GH_PROMPT_DISABLED": "1",
            "GH_TOKEN": FIXTURE_TOKEN,
            "GITHUB_REPOSITORY": REPOSITORY,
            "EXPECTED_REPOSITORY": REPOSITORY,
            "RUNNER_TEMP": self.runner.as_posix(),
            "GITHUB_RUN_ID": "420",
            "GITHUB_RUN_ATTEMPT": "2",
            "FIXTURE_ROOT": self.root.as_posix(),
            "FIXTURE_BIN": self.bin.as_posix(),
            "FIXTURE_REAL_GIT": pathlib.Path(REAL_GIT).as_posix(),
            "FIXTURE_REAL_GH": pathlib.Path(REAL_GH).as_posix(),
            "FIXTURE_PYTHON": pathlib.Path(sys.executable).as_posix(),
            "FIXTURE_STUB": (self.root / "offline.py").as_posix(),
            "FIXTURE_BRANCH": BRANCH,
            "FIXTURE_PR_URL": PR_URL,
            "PYTHONDONTWRITEBYTECODE": "1",
        })
        (self.root / "offline.py").write_text(OFFLINE_GH, encoding="utf-8")
        self.wrapper("gh", '''
if [[ "$1" == auth && "$2" == git-credential ]]; then
  exec "$FIXTURE_REAL_GH" "$@"
fi
exec "$FIXTURE_PYTHON" "$FIXTURE_STUB" gh "$@"
''')
        self.wrapper("git", '''
for argument in "$@"; do
  if [[ "$argument" == push ]]; then
    "$FIXTURE_PYTHON" "$FIXTURE_STUB" push "$@" || exit
    break
  fi
done
exec "$FIXTURE_REAL_GIT" "$@"
''')
        self.wrapper("mv", '''
"$FIXTURE_PYTHON" "$FIXTURE_STUB" manifest_move "$@" || exit
exec /usr/bin/mv "$@"
''')
        if os.name == "nt":
            # Native jq otherwise writes CRLF, unlike the Linux workflow host;
            # that changes bytewise cmp/hash inputs. --binary is jq's documented
            # MSYS mode, not a transformation of the authored script or data.
            self.env["FIXTURE_REAL_JQ"] = pathlib.Path(shutil.which("jq")).as_posix()
            self.wrapper("jq", 'exec "$FIXTURE_REAL_JQ" --binary "$@"\n')
        self.state = {}
        self.save_state()
        self.git("init", "--quiet", "--initial-branch=main", str(self.repo), cwd=self.root)
        self.git("config", "user.name", "github-actions[bot]")
        self.git("config", "user.email", "41898282+github-actions[bot]@users.noreply.github.com")
        self.git("config", "core.autocrlf", "false")
        self.git("config", "commit.gpgsign", "false")

    def wrapper(self, name: str, script: str) -> None:
        path = self.bin / name
        path.write_text("#!/bin/bash\nset -euo pipefail\n" + script, encoding="utf-8")
        path.chmod(0o755)

    def save_state(self) -> None:
        (self.root / "state.json").write_text(json.dumps(self.state), encoding="utf-8")

    def events(self, kind: str) -> list[dict]:
        path = self.root / "events.jsonl"
        if not path.exists():
            return []
        return [event for line in path.read_text(encoding="utf-8").splitlines()
                if (event := json.loads(line))["kind"] == kind]

    def run(self, *args: str, cwd: pathlib.Path | None = None,
            input_text: str | None = None) -> subprocess.CompletedProcess:
        return subprocess.run(
            list(args), cwd=self.repo if cwd is None else cwd, env=self.env,
            input=input_text, text=True, capture_output=True, timeout=TIMEOUT,
        )

    def git(self, *args: str, cwd: pathlib.Path | None = None) -> str:
        result = self.run(str(REAL_GIT), *args, cwd=cwd)
        if result.returncode:
            raise AssertionError(f"Fixture git {args!r}: {result.stdout}{result.stderr}")
        return result.stdout.strip()

    def bash(self, script: str) -> subprocess.CompletedProcess:
        # Git for Windows truncates sufficiently long -c arguments. stdin also
        # preserves authored quoting exactly and works with the Linux CI runner.
        # MSYS prepends its own tools at startup; put fixture observers first
        # using a shell-native path (not a Windows drive's colon in $PATH).
        prelude = 'export PATH="$(cd "$FIXTURE_BIN" && pwd):$PATH"\n'
        return self.run(str(BASH), "--noprofile", "--norc", "-s", input_text=prelude + script)

    def write(self, name: str, content: str) -> None:
        path = self.repo / name
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_bytes(content.encode("utf-8"))

    def commit(self, message: str) -> str:
        self.git("add", "--all")
        self.git("commit", "--quiet", "--allow-empty", "-m", message)
        return self.git("rev-parse", "HEAD")


class CompiledWarningContractTests(unittest.TestCase):
    def assert_noop_detection_gate(self, name: str) -> None:
        source, _ = authored(name + ".md")
        jobs = compiled(name + ".lock.yml")["jobs"]
        gate = ("(needs.agent.outputs.output_types != '' && "
                "needs.agent.outputs.output_types != 'noop') || "
                "needs.agent.outputs.has_patch == 'true'")
        for label, expected, actual in (
            ("authored", "${{ " + gate + " }}",
             source["safe-outputs"]["threat-detection"]["enabled"]),
            ("compiled", "always() && needs.agent.result != 'skipped' && (" + gate + ")",
             " ".join(jobs["detection"]["if"].split())),
        ):
            with self.subTest(case=label):
                self.assertEqual(expected, actual,
                                 "Skip only empty/noop outputs without a patch; retain real-output and patch detection")

    def test_pipeline_triage_detection_skips_noop_only_runs(self) -> None:
        self.assert_noop_detection_gate("pipeline-test-triage")

    def test_resource_lock_detection_skips_noop_only_runs(self) -> None:
        self.assert_noop_detection_gate("resource-lock-refactoring")

    def assert_detection_security_controls(self, name: str) -> None:
        source, _ = authored(name + ".md")
        jobs = compiled(name + ".lock.yml")["jobs"]
        detection = source["safe-outputs"]["threat-detection"]
        self.assertEqual("detection", detection["model"])
        self.assertEqual({"id": "copilot"}, detection["engine"])
        self.assertEqual({"contents": "read", "copilot-requests": "write"},
                         jobs["detection"]["permissions"])
        execution = step_with_id(jobs["detection"]["steps"], "detection_agentic_execution")
        self.assertEqual("detection", execution["env"]["COPILOT_MODEL"])
        self.assertIn("steps.detection_setup.outputs.ready == 'true'", execution["if"])
        self.assertIn("steps.threat_detect_install.outcome == 'success'", execution["if"])
        self.assertEqual(
            "always() && (!cancelled()) && needs.agent.result != 'skipped' && "
            "(needs.detection.result == 'success' || needs.detection.result == 'skipped')",
            " ".join(jobs["safe_outputs"]["if"].split()),
        )

    def test_pipeline_triage_noop_skip_preserves_detection_security_controls(self) -> None:
        self.assert_detection_security_controls("pipeline-test-triage")

    def test_resource_lock_noop_skip_preserves_detection_security_controls(self) -> None:
        self.assert_detection_security_controls("resource-lock-refactoring")

    def test_build_publisher_checkouts_have_direct_identity_dependency(self) -> None:
        jobs = compiled("build-failure-analysis.lock.yml")["jobs"]
        output = jobs["safe_outputs"]
        self.assertEqual(
            {"activation", "agent", "detection", "fetch-binlog"}, set(output["needs"]),
            "Publisher identity refs must resolve through a direct fetch-binlog dependency",
        )
        self.assertEqual([
            "${{ needs.fetch-binlog.outputs.pr-head-sha }}",
            "${{ needs.fetch-binlog.outputs.base-ref || github.event.repository.default_branch }}",
        ], [step["with"]["ref"] for step in checkout_steps(output)])
        self.assertEqual(".gh-aw-base-config", checkout_steps(output)[1]["with"]["path"])

    def test_build_activation_and_output_guards_remain_bounded(self) -> None:
        source, _ = authored("build-failure-analysis.md")
        jobs = compiled("build-failure-analysis.lock.yml")["jobs"]
        gate = ("needs.fetch-binlog.outputs.binlog-found == 'true' && "
                "needs.fetch-binlog.outputs.push-blocked != 'true'")
        self.assertEqual(gate, source["if"])
        for job in ("activation", "agent"):
            self.assertIn(gate, jobs[job]["if"])
        self.assertEqual(
            {"contents": "read", "pull-requests": "read", "copilot-requests": "write"},
            jobs["agent"]["permissions"],
        )
        self.assertEqual(
            {"contents": "write", "issues": "write", "pull-requests": "write"},
            jobs["safe_outputs"]["permissions"],
        )
        self.assertIn("needs.agent.result != 'skipped'", jobs["safe_outputs"]["if"])
        self.assertIn("needs.detection.result == 'success'", jobs["safe_outputs"]["if"])
        outputs = source["safe-outputs"]
        self.assertEqual(
            "${{ needs.agent.outputs.output_types != '' || needs.agent.outputs.has_patch == 'true' }}",
            outputs["threat-detection"]["enabled"],
        )
        self.assertEqual(5, outputs["add-comment"]["max"])
        self.assertEqual(25, outputs["create-pull-request-review-comment"]["max"])
        self.assertEqual({
            "max": 1,
            "target": "${{ github.event.check_run.pull_requests[0].number || github.event.inputs['pr-number'] }}",
            "allowed-files": ["src/**", "test/**"],
            "commit-title-suffix": " [build-failure-analysis]",
            "patch-format": "am",
            "if-no-changes": "ignore",
            "ignore-missing-branch-failure": True,
            "fallback-as-pull-request": False,
            "check-branch-protection": False,
        }, outputs["push-to-pull-request-branch"])

    def test_dependabot_compiled_inventory_preserves_all_existing_reads(self) -> None:
        inventory = manifest("dependabot-pr-bundler.lock.yml")
        tools = next(server["tools"] for server in inventory["mcp_servers"]
                     if server["name"] == "github")
        expected = set("""
            actions_get actions_list get_code_scanning_alert get_commit
            get_dependabot_alert get_discussion get_discussion_comments
            get_file_contents get_job_logs get_label get_latest_release get_me
            get_notification_details get_pull_request get_pull_request_comments
            get_pull_request_diff get_pull_request_files
            get_pull_request_review_comments get_pull_request_reviews
            get_pull_request_status get_release_by_tag get_secret_scanning_alert
            get_tag issue_read list_branches list_code_scanning_alerts list_commits
            list_dependabot_alerts list_discussion_categories list_discussions
            list_issue_types list_issues list_label list_notifications
            list_pull_requests list_releases list_secret_scanning_alerts
            list_starred_repositories list_tags pull_request_read search_code
            search_issues search_orgs search_pull_requests search_repositories search_users
        """.split())
        self.assertEqual(expected, set(tools),
                         "Compiled GitHub inventory must retain all reads and both Dependabot tools")
        self.assertEqual(len(expected), len(tools))
        self.assertEqual([
            "GH_AW_CI_TRIGGER_TOKEN", "GH_AW_DEFAULT_OTLP_ENDPOINT",
            "GH_AW_DEFAULT_OTLP_HEADERS", "GH_AW_GITHUB_MCP_SERVER_TOKEN",
            "GH_AW_GITHUB_TOKEN", "GITHUB_TOKEN",
        ], inventory["secrets"])

    def test_dependabot_compiled_server_remains_read_only(self) -> None:
        job = compiled("dependabot-pr-bundler.lock.yml")["jobs"]["agent"]
        gateway = next(step for step in job["steps"] if step.get("id") == "start-mcp-gateway")
        script = gateway["run"]
        marker = '"github": '
        server, _ = json.JSONDecoder().raw_decode(script[script.index(marker) + len(marker):])
        self.assertEqual("ghcr.io/github/github-mcp-server:v2.0.3", server["container"])
        self.assertEqual("1", server["env"]["GITHUB_READ_ONLY"])
        self.assertEqual("all,dependabot", server["env"]["GITHUB_TOOLSETS"])
        self.assertEqual("${GITHUB_MCP_SERVER_TOKEN}", server["env"]["GITHUB_PERSONAL_ACCESS_TOKEN"])
        self.assertEqual(
            "${{ secrets.GH_AW_GITHUB_MCP_SERVER_TOKEN || secrets.GH_AW_GITHUB_TOKEN || secrets.GITHUB_TOKEN }}",
            gateway["env"]["GITHUB_MCP_SERVER_TOKEN"],
        )
        permissions = dict.fromkeys((
            "actions", "attestations", "checks", "contents", "deployments",
            "discussions", "issues", "models", "packages", "pages", "pull-requests",
            "repository-projects", "security-events", "statuses", "vulnerability-alerts",
        ), "read")
        self.assertEqual({**permissions, "copilot-requests": "write"}, job["permissions"])

    def test_unskip_checkout_uses_exact_sha_without_persisted_credentials(self) -> None:
        source, _ = authored("unskip-closed-tests-prepare.md")
        source_job = publisher(source)
        compiled_job = compiled("unskip-closed-tests.lock.yml")["jobs"]["apply_verified_unskips"]
        for label, job in (("authored", source_job), ("compiled", compiled_job)):
            with self.subTest(case=label):
                checkouts = checkout_steps(job)
                self.assertEqual(1, len(checkouts))
                self.assertEqual("${{ github.sha }}", checkouts[0]["with"]["ref"])
                self.assertEqual(0, checkouts[0]["with"]["fetch-depth"])
                self.assertIs(False, checkouts[0]["with"]["persist-credentials"],
                              "Publisher checkout must not persist credentials")
                self.assertEqual(
                    {"contents": "write", "issues": "read", "pull-requests": "write"},
                    job["permissions"],
                )
                self.assertNotIn("GH_TOKEN", job.get("env", {}))
                self.assertEqual("${{ github.token }}", publisher_step(job)["env"]["GH_TOKEN"])
                self.assertIn("needs.detection.outputs.detection_success == 'true'", job["if"])
                self.assertIn("needs.agent.result == 'success'", job["if"])
                self.assertIn("contains(needs.agent.outputs.output_types, 'apply_verified_unskips')", job["if"])
        self.assertEqual({"agent", "detection", "safe_outputs"}, set(compiled_job["needs"]))
        verification = compiled("unskip-closed-tests.lock.yml")["jobs"]["verify-selected-unskips"]
        self.assertEqual({"agent", "detection"}, set(verification["needs"]))
        self.assertEqual(
            {"contents": "read", "issues": "read", "pull-requests": "read"},
            verification["permissions"],
        )


class ParallelAuditContractTests(unittest.TestCase):
    def setUp(self) -> None:
        self.source, self.prompt = authored("shared/parallel-safety-audit-shared.md")
        self.resolve_step = step_with_id(self.source["steps"], "resolve")
        self.extract_step = step_with_id(self.source["steps"], "extract")

    def history(self, initial: dict[str, str], changes: dict[str, str | None]) -> Fixture:
        fixture = Fixture(self)
        fixture.write("marker.txt", "Shared ancestor\n")
        for name, content in initial.items():
            fixture.write(name, content)
        fixture.merge_base = fixture.commit("Common ancestor")
        fixture.git("switch", "--quiet", "--create", "pull")
        for name, content in changes.items():
            if content is None:
                (fixture.repo / name).unlink()
            else:
                fixture.write(name, content)
        fixture.head = fixture.commit("PR delta")
        fixture.git("switch", "--quiet", "main")
        fixture.write("test/BaseOnly.cs", "// Not part of this PR\n")
        fixture.write("src/BaseOnly.cs", "// Not part of this PR\n")
        fixture.base_tip = fixture.commit("Unrelated base advance")
        fixture.git("switch", "--quiet", "--detach", fixture.head)
        fixture.inputs = fixture.root / "inputs"
        fixture.state = {"pr_response": {
            "baseRefOid": fixture.base_tip, "headRefOid": fixture.head,
        }}
        fixture.save_state()
        return fixture

    def resolve(self, fixture: Fixture, event: str = "workflow_dispatch",
                context: dict | None = None, issue_number: str = "73") -> tuple[subprocess.CompletedProcess, dict]:
        # Actions evaluates the declared fromJSON(...).item_number expression.
        # The Bash is executed verbatim, not a reimplementation of PR resolution.
        context = {"item_number": 73} if context is None else context
        fixture.env.update({
            "EVENT_NAME": event,
            "EVENT_PR_BASE_SHA": fixture.base_tip if event == "pull_request" else "",
            "EVENT_PR_HEAD_SHA": fixture.head if event == "pull_request" else "",
            "EVENT_PR_NUMBER": "73" if event == "pull_request" else "",
            "EVENT_ISSUE_NUMBER": issue_number if event == "issue_comment" else "",
            "EVENT_DISPATCH_PR_NUMBER": str(context.get("item_number", "")),
            "GITHUB_OUTPUT": (fixture.root / "resolve.out").as_posix(),
        })
        result = fixture.bash(self.resolve_step["run"])
        output = fixture.root / "resolve.out"
        values = dict(line.split("=", 1) for line in output.read_text(encoding="utf-8").splitlines()) \
            if output.exists() else {}
        return result, values

    def extract(self, fixture: Fixture, resolved: dict) -> subprocess.CompletedProcess:
        fixture.env.update({
            "BASE_SHA": resolved["base_sha"], "HEAD_SHA": resolved["head_sha"],
            "PR_NUMBER": resolved["pr_number"], "INPUTS_DIR": fixture.inputs.as_posix(),
            "GITHUB_OUTPUT": (fixture.root / "extract.out").as_posix(),
        })
        return fixture.bash(self.extract_step["run"])

    def audit(self, fixture: Fixture, tests: list[str], sources: list[str],
              configs: list[str], regions: list[str]) -> dict:
        result, resolved = self.resolve(fixture)
        self.assertEqual(0, result.returncode, result.stdout + result.stderr)
        self.assertEqual({
            "base_sha": fixture.base_tip, "head_sha": fixture.head, "pr_number": "73",
        }, resolved)
        result = self.extract(fixture, resolved)
        self.assertEqual(0, result.returncode, result.stdout + result.stderr)
        path = fixture.inputs / "audit-inputs.json"
        self.assertTrue(path.is_file(),
                        "Actual extract succeeded but did not publish audit-inputs.json")
        data = json.loads(path.read_text(encoding="utf-8"))
        expected = {
            "schema_version": 1, "repository": REPOSITORY, "pr_number": 73,
            "base_sha": fixture.base_tip, "merge_base": fixture.merge_base,
            "head_sha": fixture.head, "test_count": len(tests), "src_count": len(sources),
            "config_count": len(configs), "has_changed_tests": bool(tests or configs),
            "test_files_path": (fixture.inputs / "changed-test-files.txt").as_posix(),
            "test_regions_path": (fixture.inputs / "changed-test-regions.tsv").as_posix(),
            "src_files_path": (fixture.inputs / "changed-src-files.txt").as_posix(),
            "config_files_path": (fixture.inputs / "changed-config-files.txt").as_posix(),
        }
        self.assertEqual(expected, data)
        for key in ("schema_version", "pr_number", "test_count", "src_count", "config_count"):
            self.assertIs(int, type(data[key]), key)
        self.assertIs(bool, type(data["has_changed_tests"]))
        for key, contents in (
            ("test_files_path", tests), ("src_files_path", sources),
            ("config_files_path", configs), ("test_regions_path", regions),
        ):
            file_path = pathlib.Path(data[key])
            self.assertEqual(fixture.inputs, file_path.parent)
            self.assertEqual(contents, file_path.read_text(encoding="utf-8").splitlines(), key)
        moves = fixture.events("manifest_move")
        self.assertEqual(1, len(moves))
        self.assertEqual([
            (fixture.inputs / "audit-inputs.json.tmp").as_posix(), path.as_posix(),
        ], moves[0]["args"])
        self.assertEqual(data, moves[0]["manifest"], "Rename must publish complete JSON")
        self.assertFalse((fixture.inputs / "audit-inputs.json.tmp").exists())
        self.assertEqual([], list(fixture.runner.iterdir()))
        outputs = dict(line.split("=", 1) for line in
                       (fixture.root / "extract.out").read_text(encoding="utf-8").splitlines())
        for key in ("test_count", "src_count", "config_count"):
            self.assertEqual(str(expected[key]), outputs[key])
        self.assertEqual(str(expected["has_changed_tests"]).lower(), outputs["has_changed_tests"])
        self.assertEqual(fixture.head, fixture.git("rev-parse", "HEAD"))
        return data

    def test_prompt_consumes_agent_visible_runtime_manifest(self) -> None:
        expressions = re.findall(r"\$\{\{[^}]*steps\.(?:resolve|extract)\.[^}]*\}\}", self.prompt)
        self.assertEqual([], expressions, "Prompt inputs must come from the runtime manifest")
        self.assertEqual("/tmp/gh-aw/parallel-audit", self.extract_step["env"].get("INPUTS_DIR"))
        self.assertEqual("${{ steps.resolve.outputs.pr_number }}", self.extract_step["env"].get("PR_NUMBER"))
        self.assertEqual(
            "${{ fromJSON(github.event.inputs.aw_context || github.event.client_payload.aw_context || '{}').item_number }}",
            self.resolve_step["env"]["EVENT_DISPATCH_PR_NUMBER"],
        )
        prompt = " ".join(self.prompt.split())
        for required in (
            "/tmp/gh-aw/parallel-audit/audit-inputs.json",
            "Require `schema_version` 1", "a positive `pr_number`",
            "and all four input files",
            "Verify `git rev-parse HEAD` matches `head_sha`",
            "the file counts match the manifest",
            "Missing or malformed required inputs are incomplete analysis, never an empty scope or an all-clear.",
            "`pr_number` from the runtime manifest",
        ):
            self.assertIn(required, prompt)
        for key in ("test_files_path", "test_regions_path", "src_files_path", "config_files_path"):
            self.assertIn(f"`{key}`", self.prompt)
        self.assertEqual({"contents": "read", "pull-requests": "read"}, self.source["permissions"])
        self.assertEqual({"allowed": ["defaults"]}, self.source["network"])
        review = self.source["safe-outputs"]["submit-pull-request-review"]
        self.assertEqual(1, review["max"])
        self.assertEqual(["COMMENT"], review["allowed-events"])
        self.assertEqual(
            "${{ (needs.agent.outputs.output_types != '' && needs.agent.outputs.output_types != 'noop') || needs.agent.outputs.has_patch == 'true' }}",
            self.source["safe-outputs"]["threat-detection"]["enabled"],
        )

    def test_dispatch_manifest_preserves_snapshot_lists_and_exact_head_regions(self) -> None:
        numbered = "".join(f"// {number}\n" for number in range(1, 11))
        changed = numbered.replace("// 2\n", "// changed 2\n").replace(
            "// 8\n", "// inserted a\n// inserted b\n// 8\n"
        )
        fixture = self.history({
            "test/Modified.cs": numbered,
            "test/RemovedLock.cs": "// before\n// anchor\n[ResourceLock]\n// after\n",
            "test/LeadingDeletion.cs": "[DoNotParallelize]\n// first remaining\n// last\n",
            "test/Deleted.cs": "// A pure test deletion stays outside AMR\n",
            "src/Worker.cs": "class Worker {}\n",
            "Directory.Build.props": "<Project />\n",
            "test/Unit.csproj": "<Project />\n",
        }, {
            "test/Modified.cs": changed,
            "test/RemovedLock.cs": "// before\n// anchor\n// after\n",
            "test/LeadingDeletion.cs": "// first remaining\n// last\n",
            "test/Added.cs": "// a\n// b\n// c\n",
            "test/Deleted.cs": None,
            "src/Worker.cs": "class ChangedWorker {}\n",
            "Directory.Build.props": "<Project><!-- parallel --></Project>\n",
            "test/Unit.csproj": "<Project><!-- test config --></Project>\n",
        })
        self.audit(
            fixture,
            ["test/Added.cs", "test/LeadingDeletion.cs", "test/Modified.cs", "test/RemovedLock.cs"],
            ["src/Worker.cs"], ["Directory.Build.props", "test/Unit.csproj"],
            ["test/Added.cs\t1-3", "test/LeadingDeletion.cs\t1-1",
             "test/Modified.cs\t2-2,8-9", "test/RemovedLock.cs\t2-2"],
        )
        self.assertNotEqual(fixture.base_tip, fixture.merge_base)
        self.assertEqual([[
            "pr", "view", "73", "--repo", REPOSITORY, "--json", "baseRefOid,headRefOid",
        ]], [event["args"] for event in fixture.events("gh")])

    def test_pull_request_resolution_uses_event_snapshot_without_api(self) -> None:
        fixture = self.history({}, {})
        result, resolved = self.resolve(fixture, event="pull_request", context={"item_number": 999})
        self.assertEqual(0, result.returncode, result.stderr)
        self.assertEqual({
            "base_sha": fixture.base_tip, "head_sha": fixture.head, "pr_number": "73",
        }, resolved)
        self.assertEqual([], fixture.events("gh"))

    def test_issue_comment_resolution_uses_issue_identity(self) -> None:
        fixture = self.history({}, {})
        result, resolved = self.resolve(
            fixture, event="issue_comment", context={"item_number": 999}, issue_number="84"
        )
        self.assertEqual(0, result.returncode, result.stderr)
        self.assertEqual("84", resolved["pr_number"])
        self.assertEqual(fixture.head, resolved["head_sha"])
        self.assertEqual("84", fixture.events("gh")[0]["args"][2])

    def test_config_only_changes_remain_in_scope(self) -> None:
        fixture = self.history(
            {"Directory.Build.props": "<Project />\n"},
            {"Directory.Build.props": "<Project><!-- enables parallelism --></Project>\n"},
        )
        self.audit(fixture, [], [], ["Directory.Build.props"], [])

    def test_test_only_changes_remain_in_scope(self) -> None:
        fixture = self.history({}, {"test/Only.cs": "// Only changed test\n"})
        self.audit(fixture, ["test/Only.cs"], [], [], ["test/Only.cs\t1-1"])

    def test_empty_changes_produce_valid_empty_scope(self) -> None:
        self.audit(self.history({}, {}), [], [], [], [])

    def test_source_only_changes_do_not_enable_test_audit(self) -> None:
        fixture = self.history(
            {"src/Worker.cs": "class Worker {}\n"},
            {"src/Worker.cs": "class UpdatedWorker {}\n"},
        )
        self.audit(fixture, [], ["src/Worker.cs"], [], [])

    def test_config_rename_and_deletion_keep_both_sides(self) -> None:
        settings = "<RunSettings>\n<!-- unchanged content -->\n</RunSettings>\n"
        fixture = self.history({
            "test/old.runsettings": settings, "test/deleted.props": "<Project />\n",
        }, {
            "test/old.runsettings": None, "test/new.runsettings": settings,
            "test/deleted.props": None,
        })
        self.audit(
            fixture, [], [],
            ["test/deleted.props", "test/new.runsettings", "test/old.runsettings"], [],
        )

    def test_assembly_attribute_candidates_keep_base_and_head_evidence(self) -> None:
        comments = "".join(f"// unchanged {number}\n" for number in range(8))
        fixture = self.history({
            "test/OldAssembly.cs": "[assembly: DoNotParallelize]\n" + comments,
            "test/RemovedAssembly.cs": "[assembly: DoNotParallelize]\n",
            "test/Program.cs": "[assembly: Parallelize(\n    Workers = 0)]\n",
        }, {
            "test/OldAssembly.cs": None, "test/NewAssembly.cs": comments,
            "test/RemovedAssembly.cs": None,
            "test/Program.cs": "[assembly: Parallelize(\n    Workers = 2)]\n",
            "test/AddedAssembly.cs": "using P = ParallelizeAttribute;\n[assembly: P]\n",
        })
        self.audit(
            fixture, ["test/AddedAssembly.cs", "test/NewAssembly.cs", "test/Program.cs"], [],
            ["test/AddedAssembly.cs", "test/OldAssembly.cs", "test/Program.cs", "test/RemovedAssembly.cs"],
            ["test/AddedAssembly.cs\t1-2", "test/NewAssembly.cs\t1-8", "test/Program.cs\t2-2"],
        )

    def test_missing_invalid_and_failed_pr_resolution_are_not_empty_scope(self) -> None:
        for label in ("missing_dispatch_pr", "invalid_issue_pr", "auth_required", "invalid_metadata"):
            with self.subTest(case=label):
                fixture = self.history({}, {})
                if label == "auth_required":
                    fixture.state["api_exit"] = 4
                if label == "invalid_metadata":
                    fixture.state["pr_response"] = {}
                fixture.save_state()
                result, resolved = self.resolve(
                    fixture,
                    event="issue_comment" if label == "invalid_issue_pr" else "workflow_dispatch",
                    context={} if label == "missing_dispatch_pr" else None,
                    issue_number="not-a-pr" if label == "invalid_issue_pr" else "73",
                )
                if result.returncode == 0:
                    result = self.extract(fixture, resolved)
                self.assertNotEqual(0, result.returncode, label)
                if label == "auth_required":
                    self.assertEqual(4, result.returncode)
                self.assertFalse((fixture.inputs / "audit-inputs.json").exists())


class UnskipPublisherContractTests(unittest.TestCase):
    def setUp(self) -> None:
        source, _ = authored("unskip-closed-tests-prepare.md")
        self.script = publisher_step(publisher(source))["run"]

    def package(self, has_changes: bool = True) -> Fixture:
        fixture = Fixture(self)
        fixture.changed_path = "test/Unit/Regression Tests.cs"
        fixture.original = '// Fixture\n[Ignore("closed tracking item")]\nvoid Reenabled() {}\n'
        fixture.verified = "// Fixture\nvoid Reenabled() {}\n"
        fixture.write(fixture.changed_path, fixture.original)
        fixture.write("src/Untouched.cs", "// Must never be changed by publisher\n")
        fixture.source_commit = fixture.commit("Analyzed revision")
        fixture.remote = fixture.root / "origin.git"
        fixture.git("init", "--quiet", "--bare", str(fixture.remote), cwd=fixture.root)
        fixture.git("remote", "add", "origin", fixture.remote.as_uri())
        fixture.git("push", "--quiet", "origin", "HEAD:refs/heads/main")
        digest = "a" * 64
        content_sha = hashlib.sha256(fixture.verified.encode("utf-8")).hexdigest()
        fixture.trusted = {
            "schema_version": "1", "repository": REPOSITORY,
            "source_commit": fixture.source_commit, "manifest_digest": digest,
            "candidates": [{
                "candidate_id": "candidate-1", "path": fixture.changed_path,
                "source_sha256": hashlib.sha256(fixture.original.encode("utf-8")).hexdigest(),
                "decision": {"eligible": True},
                "owner": {"test_fqns": ["Fixture.Reenabled"]},
            }],
        }
        fixture.result = {
            "schema_version": "1", "source_commit": fixture.source_commit,
            "manifest_digest": digest, "has_changes": has_changes,
            "retained_candidates": [{
                "candidate_id": "candidate-1", "path": fixture.changed_path,
                "test_fqns": ["Fixture.Reenabled"],
            }] if has_changes else [],
            "changed_files": [{
                "path": fixture.changed_path, "content_sha256": content_sha,
            }] if has_changes else [],
            "changed_paths": [fixture.changed_path] if has_changes else [],
            "pr_title": "[unskip-closed-tests] Re-enable verified test" if has_changes else "",
            "pr_body": (
                f"<!-- unskip-closed-tests:v1;source={fixture.source_commit};manifest={digest} -->"
                "\n\nRe-enabled Fixture.Reenabled after deterministic verification."
            ) if has_changes else "",
        }
        fixture.package_dir = fixture.root / "package"
        (fixture.package_dir / "files").mkdir(parents=True)
        fixture.blob = fixture.package_dir / "files" / content_sha
        if has_changes:
            fixture.blob.write_bytes(fixture.verified.encode("utf-8"))
        fixture.env.update({
            "EXPECTED_COMMIT": fixture.source_commit,
            "ORIGINAL_MANIFEST": (fixture.root / "manifest.json").as_posix(),
            "PACKAGE_DIRECTORY": fixture.package_dir.as_posix(),
            "RESULT_PATH": (fixture.package_dir / "result.json").as_posix(),
            "GITHUB_WORKSPACE": fixture.repo.as_posix(),
        })
        fixture.state = {
            "heads": [fixture.source_commit], "duplicates": [0],
            "live_base": fixture.source_commit,
        }
        self.save_package(fixture)
        fixture.config_before = (fixture.repo / ".git/config").read_bytes()
        return fixture

    def save_package(self, fixture: Fixture) -> None:
        (fixture.root / "manifest.json").write_text(json.dumps(fixture.trusted), encoding="utf-8")
        (fixture.package_dir / "result.json").write_text(json.dumps(fixture.result), encoding="utf-8")
        fixture.save_state()

    def assert_no_credential_persistence(self, fixture: Fixture) -> None:
        self.assertEqual(fixture.config_before, (fixture.repo / ".git/config").read_bytes())
        self.assertEqual(b"", fixture.global_config.read_bytes())
        self.assertEqual([], list((fixture.root / "gh-config").rglob("*")))
        for path in fixture.root.rglob("*"):
            if path.is_file():
                self.assertNotIn(FIXTURE_TOKEN.encode("utf-8"), path.read_bytes(), str(path))

    def assert_pushes(self, fixture: Fixture, cleanup: bool = False) -> list[str]:
        options = ["-c", "credential.helper=", "-c", "credential.helper=!gh auth git-credential"]
        expected = [options + ["push", "origin", "HEAD:refs/heads/" + BRANCH]]
        if cleanup:
            expected.append(options + ["push", "origin", "--delete", BRANCH])
        self.assertEqual(expected, [event["args"] for event in fixture.events("push")],
                         "Every executed push must carry only process-scoped authentication")
        return options

    def assert_no_publication(self, fixture: Fixture) -> None:
        self.assertEqual([], fixture.events("push"))
        self.assertEqual([], [event for event in fixture.events("gh")
                              if event["args"][:2] == ["pr", "create"]])
        self.assertEqual(fixture.source_commit, fixture.git("rev-parse", "HEAD"))
        self.assertEqual(
            "", fixture.git("for-each-ref", "--format=%(refname)", "refs/heads/" + BRANCH,
                            cwd=fixture.remote),
        )
        self.assert_no_credential_persistence(fixture)

    def publish(self, fixture: Fixture) -> subprocess.CompletedProcess:
        result = fixture.bash(self.script)
        self.assertEqual(0, result.returncode, result.stdout + result.stderr)
        return result

    def assert_cleaned_branch(self, fixture: Fixture) -> None:
        self.assertEqual(
            "", fixture.git("for-each-ref", "--format=%(refname)", "refs/heads/" + BRANCH,
                            cwd=fixture.remote),
        )
        # A real commit and initial push preceded the real local deletion.
        self.assertEqual(fixture.source_commit, fixture.git("rev-parse", "HEAD^"))
        self.assertEqual(fixture.changed_path,
                         fixture.git("diff", "--name-only", fixture.source_commit, "HEAD"))
        self.assert_pushes(fixture, cleanup=True)
        self.assert_no_credential_persistence(fixture)

    def test_verified_package_pushes_exact_commit_and_draft_with_ephemeral_auth(self) -> None:
        fixture = self.package()
        self.publish(fixture)
        pushed = fixture.git("rev-parse", "refs/heads/" + BRANCH, cwd=fixture.remote)
        self.assertEqual(fixture.git("rev-parse", "HEAD"), pushed)
        self.assertEqual(fixture.source_commit, fixture.git("rev-parse", pushed + "^"))
        self.assertEqual(fixture.changed_path,
                         fixture.git("diff", "--name-only", fixture.source_commit, pushed))
        self.assertEqual(
            fixture.verified,
            fixture.run(str(REAL_GIT), "show", pushed + ":" + fixture.changed_path).stdout,
        )
        self.assertEqual(
            fixture.verified.encode("utf-8"),
            (fixture.repo / fixture.changed_path).read_bytes(),
        )
        creates = [event for event in fixture.events("gh") if event["args"][:2] == ["pr", "create"]]
        self.assertEqual(1, len(creates))
        body_path = (fixture.runner / "unskip-closed-tests-pr-body.md").as_posix()
        self.assertEqual([
            "pr", "create", "--repo", REPOSITORY, "--base", "main", "--head", BRANCH,
            "--draft", "--title", fixture.result["pr_title"], "--body-file", body_path,
        ], creates[0]["args"])
        self.assertEqual(body_path, creates[0]["body_file"])
        self.assertEqual(fixture.result["pr_body"] + "\n", creates[0]["body"])
        self.assertEqual(fixture.result["pr_body"] + "\n",
                         pathlib.Path(body_path).read_text(encoding="utf-8"))
        self.assertEqual(1, len([event for event in fixture.events("gh")
                                if event["args"][:3] == ["pr", "view", PR_URL]]))
        options = self.assert_pushes(fixture)

        # Use the exact config observed on the real push, but exercise Git's
        # credential protocol locally: gh resolves GH_TOKEN without an API call.
        command = shlex.join(["git", *options, "credential", "fill"])
        response = fixture.bash(
            "printf '%s\\n' protocol=https host=github.com '' | " + command
        )
        self.assertEqual(0, response.returncode, response.stderr)
        credential = dict(line.split("=", 1) for line in response.stdout.splitlines())
        self.assertEqual("x-access-token", credential["username"])
        self.assertEqual(FIXTURE_TOKEN, credential["password"])
        protocol = (
            "protocol=https\nhost=github.com\nusername=x-access-token\n"
            f"password={FIXTURE_TOKEN}\n\n"
        )
        for operation in ("approve", "reject"):
            response = fixture.run(str(REAL_GIT), *options, "credential", operation, input_text=protocol)
            self.assertEqual(0, response.returncode, response.stderr)
        self.assert_no_credential_persistence(fixture)

    def test_base_movement_after_push_deletes_unpublished_branch(self) -> None:
        fixture = self.package()
        fixture.state["heads"] = [fixture.source_commit, "0" * 40]
        fixture.save_state()
        result = self.publish(fixture)
        self.assertIn("Default branch advanced before PR creation", result.stdout)
        self.assertEqual([], [event for event in fixture.events("gh")
                              if event["args"][:2] == ["pr", "create"]])
        self.assert_cleaned_branch(fixture)

    def test_duplicate_after_push_deletes_unpublished_branch(self) -> None:
        fixture = self.package()
        fixture.state["duplicates"] = [0, 1]
        fixture.save_state()
        result = self.publish(fixture)
        self.assertIn("Another unskip pull request opened", result.stdout)
        self.assertEqual([], [event for event in fixture.events("gh")
                              if event["args"][:2] == ["pr", "create"]])
        self.assert_cleaned_branch(fixture)

    def test_base_movement_during_creation_closes_draft_and_deletes_branch(self) -> None:
        fixture = self.package()
        fixture.state["live_base"] = "0" * 40
        fixture.save_state()
        result = self.publish(fixture)
        self.assertIn("Default branch advanced during PR creation", result.stdout)
        closes = [event["args"] for event in fixture.events("gh")
                  if event["args"][:2] == ["pr", "close"]]
        self.assertEqual([["pr", "close", "91", "--repo", REPOSITORY]], closes)
        state = json.loads((fixture.root / "state.json").read_text(encoding="utf-8"))
        self.assertIs(True, state["closed"])
        self.assert_cleaned_branch(fixture)

    def test_prepublication_base_movement_and_duplicate_do_not_push(self) -> None:
        for label in ("already_advanced", "already_open"):
            with self.subTest(case=label):
                fixture = self.package()
                if label == "already_advanced":
                    fixture.state["heads"] = ["0" * 40]
                else:
                    fixture.state["duplicates"] = [1]
                fixture.save_state()
                result = self.publish(fixture)
                self.assertIn(
                    "Default branch advanced" if label == "already_advanced"
                    else "An unskip pull request is already open", result.stdout,
                )
                self.assert_no_publication(fixture)
                self.assertEqual(fixture.original.encode("utf-8"),
                                 (fixture.repo / fixture.changed_path).read_bytes())

    def test_no_change_package_does_not_query_or_publish(self) -> None:
        fixture = self.package(has_changes=False)
        result = self.publish(fixture)
        self.assertIn("No selected candidate passed verification", result.stdout)
        self.assertEqual([], fixture.events("gh"))
        self.assertEqual("", fixture.git("status", "--porcelain"))
        self.assert_no_publication(fixture)

    def test_mismatched_source_content_is_rejected_before_push(self) -> None:
        fixture = self.package()
        fixture.write(fixture.changed_path, fixture.original + "// Unverified drift\n")
        result = fixture.bash(self.script)
        self.assertEqual(20, result.returncode, result.stdout + result.stderr)
        self.assertIn("Source content changed for " + fixture.changed_path, result.stdout)
        self.assert_no_publication(fixture)

    def test_tampered_verified_blob_is_rejected_before_push(self) -> None:
        fixture = self.package()
        fixture.blob.write_bytes(b"// Unverified package content\n")
        result = fixture.bash(self.script)
        self.assertEqual(20, result.returncode, result.stdout + result.stderr)
        self.assertIn("Verified package content changed for " + fixture.changed_path, result.stdout)
        self.assert_no_publication(fixture)

    def test_invalid_package_identity_and_eligibility_are_rejected_before_push(self) -> None:
        for label in ("source_identity", "manifest_identity", "ineligible_candidate",
                      "extra_blob", "unsafe_path"):
            with self.subTest(case=label):
                fixture = self.package()
                if label == "source_identity":
                    fixture.result["source_commit"] = "0" * 40
                elif label == "manifest_identity":
                    fixture.result["manifest_digest"] = "b" * 64
                elif label == "ineligible_candidate":
                    fixture.trusted["candidates"][0]["decision"]["eligible"] = False
                elif label == "extra_blob":
                    (fixture.package_dir / "files" / ("b" * 64)).write_bytes(b"unexpected")
                else:
                    unsafe = "test/../Unverified.cs"
                    fixture.trusted["candidates"][0]["path"] = unsafe
                    fixture.result["retained_candidates"][0]["path"] = unsafe
                    fixture.result["changed_files"][0]["path"] = unsafe
                    fixture.result["changed_paths"] = [unsafe]
                self.save_package(fixture)
                result = fixture.bash(self.script)
                self.assertEqual(20 if label == "unsafe_path" else 1, result.returncode,
                                 result.stdout + result.stderr)
                if label in ("source_identity", "manifest_identity", "ineligible_candidate"):
                    self.assertEqual([], fixture.events("gh"), "Identity/eligibility fails before API reads")
                elif label == "extra_blob":
                    self.assertIn("blobs.txt", result.stdout + result.stderr)
                self.assert_no_publication(fixture)
                self.assertEqual(fixture.original.encode("utf-8"),
                                 (fixture.repo / fixture.changed_path).read_bytes())


if __name__ == "__main__":
    parser = argparse.ArgumentParser(add_help=False)
    parser.add_argument("--workflow-root", type=pathlib.Path, default=WORKFLOW_ROOT)
    arguments, unittest_arguments = parser.parse_known_args()
    WORKFLOW_ROOT = arguments.workflow_root
    unittest.main(argv=[__file__, *unittest_arguments])
