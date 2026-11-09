#!/usr/bin/env python3
"""Offline contracts for the unchanged gh-aw-actions v1.0.1 setup/js runtime.

Run after security review:
  python .github/scripts/test_gh_aw_runtime_contracts.py --actions-dir PATH -v
Unittest selectors are accepted; --workflow-root supports retained-test baselines.
Requires Python, existing PyYAML, and Node (CI: Node24). No downloads or live APIs.
Prompt checks are static, not evidence of Actions execution or authorization.
"""

import argparse
import json
import os
import pathlib
import re
import shlex
import shutil
import subprocess
import tempfile
import unittest

import yaml


WORKFLOW_ROOT = pathlib.Path(__file__).resolve().parents[1] / "workflows"
ACTIONS_DIR = None
CONSUMERS = ("review.agent", "review-on-open.agent", "review-after-autofix.agent")
WORKFLOW_NAME = "Fixture workflow"
RUN_URL = "https://github.com/fixture/workflows/actions/runs/420"
HISTORY_URL = "https://github.com/fixture/workflows/issues?q=label%3Afixture"
REVIEW_BODY = "Review summary.\n\nNo blocking findings."
REVIEW_URL = "https://github.com/fixture/workflows/pull/73#pullrequestreview-9001"
REVIEWED_SHA = "a" * 40
PR_EXPRESSION = (
    "${{ github.event.pull_request.number || github.event.issue.number || "
    "fromJSON(github.event.inputs.aw_context || github.event.client_payload.aw_context || '{}').item_number }}"
)

# Only external I/O/context is doubled. Real renderer, buffer, metadata, retry and
# guard modules are required directly, never copied, rewritten or loaded in a VM.
NODE_DRIVER = r"""
const assert = require("node:assert/strict");
const fs = require("node:fs");
const path = require("node:path");
const input = JSON.parse(fs.readFileSync(0, "utf8"));
const calls = [], unexpected = [], errors = [], summaries = [], fsReads = [], outputs = {};
// These pinned diagnostic readers have no environment override. Redirect only
// their shared-root filesystem reads, never runtime/rendering module exports.
const sharedRoot = path.resolve("/tmp/gh-aw");
for (const method of ["existsSync", "readdirSync", "readFileSync", "openSync", "statSync", "lstatSync"]) {
  const actual = fs[method].bind(fs);
  fs[method] = (file, ...args) => {
    if (typeof file === "string") {
      const relative = path.relative(sharedRoot, path.resolve(file));
      if (relative !== ".." && !relative.startsWith(".." + path.sep) && !path.isAbsolute(relative)) {
        fsReads.push({ method, path: "/tmp/gh-aw" + (relative ? "/" + relative.split(path.sep).join("/") : "") });
        file = path.join(input.fixtureRoot, "diagnostics", relative);
      }
    }
    return actual(file, ...args);
  };
}
function forbidden(name) {
  unexpected.push(name);
  throw new Error(`Offline operation forbidden: ${name}`);
}
for (const [module, methods] of [
  ["http", ["request", "get"]], ["https", ["request", "get"]],
  ["net", ["connect", "createConnection"]], ["tls", ["connect"]],
  ["dgram", ["createSocket"]],
  ["child_process", ["spawn", "spawnSync", "exec", "execSync", "execFile", "execFileSync", "fork"]],
]) {
  for (const method of methods) require(module)[method] = () => forbidden(`${module}.${method}`);
}
require("net").Socket.prototype.connect = () => forbidden("socket.connect");
globalThis.fetch = () => forbidden("fetch");
function closed(values, name) {
  return new Proxy(values, {
    get(target, key) {
      if (!Object.hasOwn(target, key)) return forbidden(`${name}.${String(key)}`);
      return target[key];
    },
  });
}
const core = {
  info() {}, warning() {}, debug() {},
  error(message) { errors.push(String(message)); },
  setOutput(name, value) { outputs[name] = value; },
  summary: { addRaw(text) { summaries.push(text); return this; }, async write() {} },
};
const target = { owner: "fixture", repo: "workflows", pull_number: 73 };
const reads = {
  "repos.get": { owner: "fixture", repo: "workflows" },
  "pulls.get": target,
  "pulls.listReviews": { ...target, per_page: 100 },
  "pulls.listFiles": { ...target, per_page: 100, page: 1 },
  "pulls.listCommentsForReview": { ...target, review_id: 9001, per_page: 100 },
};
function record(operation, params) {
  calls.push({ operation, params: structuredClone(params) });
  if (operation !== "pulls.createReview") {
    try { assert.deepEqual(params, reads[operation]); }
    catch (error) { unexpected.push(`${operation}:arguments`); throw error; }
  }
}
const pullRequest = {
  head: { sha: "b".repeat(40) },
  requested_reviewers: [{ login: "reviewer" }], requested_teams: [{ slug: "maintainers" }],
};
const review = {
  id: 9001, state: "COMMENTED", user: { login: "offline-bot" },
  html_url: "https://github.com/fixture/workflows/pull/73#pullrequestreview-9001",
};
let submitted = false;
const pulls = closed({
  async get(params) { record("pulls.get", params); return { data: pullRequest }; },
  async listReviews(params) { record("pulls.listReviews", params); return { data: submitted ? [review] : [] }; },
  async listFiles(params) {
    record("pulls.listFiles", params); return { data: [{ filename: "src/Fixture.cs" }] };
  },
  async listCommentsForReview(params) {
    record("pulls.listCommentsForReview", params);
    return { data: [{
      id: 9002, html_url: review.html_url + "-comment", path: "src/Fixture.cs",
      line: 41, side: "RIGHT", body: "Guard this null input.",
    }] };
  },
  async createReview(params) {
    record("pulls.createReview", params);
    if (input.reject) throw Object.assign(new Error("Fixture review rejected"), { status: 422 });
    submitted = true;
    return { data: review };
  },
}, "pulls");
const repos = closed({
  async get(params) {
    record("repos.get", params); return { data: { private: false, visibility: "public" } };
  },
}, "repos");
const github = closed({ rest: closed({ pulls, repos }, "rest") }, "github");
const context = { repo: { owner: "fixture", repo: "workflows" } };
Object.assign(globalThis, { core, github, context });
const load = name => require(path.join(input.actionsDir, name + ".cjs"));
async function main() {
  let result = null;
  if (input.template !== undefined) process.env.GH_AW_SAFE_OUTPUT_MESSAGES = JSON.stringify({ footer: input.template });
  if (input.action === "footer") {
    result = load("messages_footer").getFooterMessage({
      workflowName: input.workflowName, runUrl: input.runUrl, historyUrl: input.historyUrl,
    });
  } else if (input.action === "guard") {
    await load("determine_automatic_lockdown")(github, context, core);
  } else if (input.action === "cli-arguments") {
    const tools = JSON.parse(fs.readFileSync(path.join(input.actionsDir, "safe_outputs_tools.json"), "utf8"));
    const tool = tools.find(item => item.name === input.tool);
    assert.ok(tool, `Missing shipped tool schema: ${input.tool}`);
    const parsed = load("mcp_cli_bridge").parseToolArgs(
      input.arguments, tool.inputSchema.properties, null,
      { rejectPositionalArguments: true, omitBlankOptionalFields: true, requiredFields: tool.inputSchema.required },
    );
    result = {
      args: parsed.args,
      missingFields: load("schema-validation").validateRequiredFields(parsed.args, tool.inputSchema),
      unexpectedFields: Object.keys(parsed.args).filter(key => !Object.hasOwn(tool.inputSchema.properties, key)),
    };
  } else if (input.action === "review") {
    const buffer = load("pr_review_buffer").createReviewBuffer();
    buffer.setReviewContext({ repo: "fixture/workflows", repoParts: context.repo, pullRequestNumber: 73, pullRequest });
    buffer.setFooterContext({
      workflowName: input.workflowName, runUrl: input.runUrl, historyUrl: input.historyUrl,
    });
    buffer.setFooterMode(input.footerMode);
    if (input.body !== undefined) buffer.setReviewMetadata(input.body, "COMMENT");
    if (input.inline) buffer.addComment({
      path: "src/Fixture.cs", line: 41, side: "RIGHT", body: "Guard this null input.",
    });
    if (input.staged) buffer.setStaged(true);
    result = await buffer.submitReview();
  } else throw new Error("Unknown driver action");
  process.stdout.write(JSON.stringify({ result, calls, unexpected, errors, summaries, fsReads, outputs }));
}
main().catch(error => { console.error(error.stack); process.exitCode = 1; });
"""


def authored(name: str) -> tuple[dict, str]:
    text = (WORKFLOW_ROOT / name).read_text(encoding="utf-8")
    parts = re.split(r"(?m)^---[ \t]*\n", text, maxsplit=2)
    if len(parts) != 3 or parts[0]:
        raise AssertionError(f"Missing authored frontmatter: {name}")
    return yaml.safe_load(parts[1]), parts[2]


def compiled(name: str) -> dict:
    return yaml.safe_load((WORKFLOW_ROOT / (name + ".lock.yml")).read_text(encoding="utf-8"))


def step(document: dict, job: str, key: str, value: str) -> dict:
    matches = [item for item in document["jobs"][job]["steps"] if item.get(key) == value]
    if len(matches) != 1:
        raise AssertionError(f"Expected exactly one {job} step with {key}={value}")
    return matches[0]


class GhAwRuntimeContractTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls) -> None:
        if ACTIONS_DIR is None:
            raise AssertionError("Invoke this script with required --actions-dir")
        for module in ("messages_footer", "report_markdown", "pr_review_buffer",
                       "safe_output_execution_metadata", "determine_automatic_lockdown"):
            if not (ACTIONS_DIR / (module + ".cjs")).is_file():
                raise AssertionError(f"Missing shipped runtime module: {module}")
        cls.node = shutil.which("node")
        if cls.node is None:
            raise AssertionError("Node is required on task-local PATH; no runtime is downloaded")

    def run_runtime(self, action: str, policy: dict | None = None, **data) -> dict:
        with tempfile.TemporaryDirectory(prefix="gh-aw-runtime-") as directory:
            root = pathlib.Path(directory)
            home, temporary = root / "home", root / "tmp"
            home.mkdir()
            temporary.mkdir()
            for name in ("sandbox/firewall/logs/access.log", "mcp-logs/gateway.jsonl"):
                fixture = temporary / "diagnostics" / name
                fixture.parent.mkdir(parents=True, exist_ok=True)
                fixture.write_text("", encoding="utf-8")
            # An allowlist, not a credential-name denylist: Node hooks, proxy,
            # GH/GITHUB/OTEL and arbitrary CI credentials cannot leak through.
            env = {key: value for key, value in os.environ.items()
                   if key.upper() in ("PATH", "SYSTEMROOT", "WINDIR")}
            env.update({
                "HOME": str(home), "USERPROFILE": str(home), "XDG_CONFIG_HOME": str(home),
                "GH_CONFIG_DIR": str(home / "gh"), "GIT_CONFIG_GLOBAL": str(home / "gitconfig"),
                "GIT_CONFIG_NOSYSTEM": "1", "TEMP": str(temporary), "TMP": str(temporary),
                "TMPDIR": str(temporary), "RUNNER_TEMP": str(temporary), "GH_AW_TMP_DIR": str(temporary),
                "GITHUB_REPOSITORY": "fixture/workflows", "GITHUB_SERVER_URL": "https://github.com",
                "GH_AW_AIC": "2.25", "GH_AW_HEAD_SHA": REVIEWED_SHA,
            })
            if policy:
                self.assertLessEqual(set(policy), {
                    "GH_AW_GITHUB_MIN_INTEGRITY", "GH_AW_GITHUB_REPOS", "GH_AW_PRIVATE_TO_PUBLIC_FLOWS",
                })
                env.update(policy)
            payload = dict(action=action, actionsDir=str(ACTIONS_DIR), workflowName=WORKFLOW_NAME,
                           runUrl=RUN_URL, fixtureRoot=str(temporary), footerMode="none")
            payload.update(data)
            process = subprocess.run(
                [self.node, "-e", NODE_DRIVER], input=json.dumps(payload),
                cwd=root, env=env, text=True, encoding="utf-8", capture_output=True, timeout=30,
            )
            self.assertEqual(0, process.returncode, process.stderr)
            result = json.loads(process.stdout)
            self.assertEqual([], result["unexpected"])
            if action in ("footer", "cli-arguments"):
                self.assertEqual([], result["calls"])
                self.assertEqual([], result["errors"])
            return result

    def test_resource_lock_terminal_examples_use_pinned_cli_argument_parser(self) -> None:
        prompt = authored("resource-lock-refactoring.md")[1]
        commands = re.findall(r"(?m)^safeoutputs (noop|report_incomplete|create_pull_request) ('[^\n]*')$", prompt)
        expected = {
            "noop": {"message": "No high-confidence bounded ResourceLock refactoring was found.\nNo files were changed."},
            "report_incomplete": {
                "reason": "The intended safe output could not be queued.",
                "details": "<failed command and concrete error>",
            },
            "create_pull_request": {
                "branch": "resource-lock/<short-project-slug>",
                "title": "<final refactoring title>",
                "body": "## Summary\n<complete final Markdown body>",
            },
        }
        self.assertCountEqual(expected, [name for name, _ in commands],
                              "Every terminal path needs a standalone CLI example, not a native-tool call or helper prefix")
        execution = step(compiled("resource-lock-refactoring"), "agent", "id", "agentic_execution")["run"]
        self.assertIn("shell(safeoutputs:*)", execution)
        for name, payload in commands:
            with self.subTest(case=name):
                arguments = shlex.split("safeoutputs " + name + " " + payload)
                self.assertEqual(["safeoutputs", name], arguments[:2])
                self.assertEqual(3, len(arguments), "The complete JSON payload must remain one shell argument")
                result = self.run_runtime("cli-arguments", tool=name, arguments=arguments[2:])["result"]
                self.assertEqual({"args": expected[name], "missingFields": [], "unexpectedFields": []}, result)

    def footer_inventory(self) -> list[tuple[str, str]]:
        inventory = []
        files = sorted(WORKFLOW_ROOT.rglob("*.md"))
        self.assertTrue(files, "Missing authored workflow inventory")
        for path in files:
            if not path.read_text(encoding="utf-8").startswith("---\n"):
                continue  # README/documentation, not an authored workflow.
            name = path.relative_to(WORKFLOW_ROOT).as_posix()
            source, _ = authored(name)
            template = (source.get("safe-outputs") or {}).get("messages", {}).get("footer")
            if template is not None:
                self.assertIsInstance(template, str)
                self.assertTrue(template)
                inventory.append((name, template))
        self.assertTrue(inventory, "No custom footers discovered")
        self.assertLessEqual({
            "resource-lock-refactoring.md", "pipeline-test-triage.md",
            "shared/test-reviewer-shared.md", "test-reviewer.agent.md",
        }, {name for name, _ in inventory})
        return inventory

    def assert_authored_footers(self, history: bool) -> None:
        for index, (name, template) in enumerate(self.footer_inventory()):
            with self.subTest(case=f"footer_{index:02d}"):
                rendered = self.run_runtime("footer", template=template,
                                            historyUrl=HISTORY_URL if history else None)["result"]
                attribution = ("> Automated by the " if name == "resource-lock-refactoring.md" else
                               "> 🤖 **Automated content by GitHub Copilot.** Generated by the ")
                expected = attribution + f"[{WORKFLOW_NAME}]({RUN_URL}/agentic_workflow) workflow. · \\~3 AIC"
                has_history = history and name != "pipeline-test-triage.md"
                link = f"[◷]({HISTORY_URL})"
                self.assertEqual(expected + (" · " + link if has_history else ""), rendered)
                self.assertEqual(int(has_history), rendered.count(link))
                self.assertNotRegex(rendered, r"\{[a-z_]+\}")

    def test_all_authored_footers_render_without_history(self) -> None:
        self.assert_authored_footers(False)

    def test_all_authored_footers_render_history_once(self) -> None:
        self.assert_authored_footers(True)

    def assert_published_review(self, inline: bool) -> dict:
        output = self.run_runtime("review", body=REVIEW_BODY, inline=inline)
        expected = dict(owner="fixture", repo="workflows", pull_number=73,
                        commit_id=REVIEWED_SHA, event="COMMENT", body=REVIEW_BODY)
        if inline:
            expected["comments"] = [dict(path="src/Fixture.cs", line=41, side="RIGHT", body="Guard this null input.")]
        self.assertEqual([expected], [call["params"] for call in output["calls"]
                                     if call["operation"] == "pulls.createReview"])
        operations = (["pulls.listFiles"] if inline else []) + [
            "pulls.get", "pulls.listReviews", "pulls.createReview", "pulls.get", "pulls.listReviews",
        ] + (["pulls.listCommentsForReview"] if inline else [])
        self.assertEqual(operations, [call["operation"] for call in output["calls"]])
        self.assertEqual([], output["errors"])
        result = output["result"]
        self.assertIs(True, result["success"])
        self.assertEqual((REVIEW_URL, "COMMENT", int(inline)),
                         (result["review_url"], result["event"], result["comment_count"]))
        before = dict(requested_reviewers=["reviewer"], requested_team_reviewers=["maintainers"], reviews=[])
        after = dict(before, reviews=[dict(id=9001, user="offline-bot", state="COMMENTED")])
        self.assertEqual(before, result["before_state"])
        self.assertEqual(after, result["after_state"])
        return result

    def test_body_only_review_publishes_expected_advisory_payload(self) -> None:
        self.assertEqual([], self.assert_published_review(False)["review_comments"])

    def test_inline_review_preserves_comment_and_reviewed_commit_associations(self) -> None:
        self.assertEqual([dict(
            id=9002, url=REVIEW_URL + "-comment", repo="fixture/workflows", pull_request_number=73,
            metadata=dict(review_id=9001, path="src/Fixture.cs", line=41, side="RIGHT"),
        )], self.assert_published_review(True)["review_comments"])

    def test_authored_test_review_footer_publishes_complete_advisory_review(self) -> None:
        source, _ = authored("shared/test-reviewer-shared.md")
        mode = source["safe-outputs"]["submit-pull-request-review"]["footer"]
        self.assertEqual("always", mode)
        output = self.run_runtime(
            "review", body=REVIEW_BODY, footerMode=mode, historyUrl=None,
            template=source["safe-outputs"]["messages"]["footer"],
        )
        body = (
            REVIEW_BODY + "\n\n> 🤖 **Automated content by GitHub Copilot.** Generated by the "
            f"[{WORKFLOW_NAME}]({RUN_URL}/agentic_workflow) workflow. · \\~3 AIC\n\n"
            f"<!-- gh-aw-agentic-workflow: {WORKFLOW_NAME}, run: {RUN_URL} -->"
        )
        requests = [call["params"] for call in output["calls"] if call["operation"] == "pulls.createReview"]
        self.assertEqual([dict(owner="fixture", repo="workflows", pull_number=73,
                               commit_id=REVIEWED_SHA, event="COMMENT", body=body)], requests)
        self.assertEqual(["pulls.get", "pulls.listReviews", "pulls.createReview", "pulls.get", "pulls.listReviews"],
                         [call["operation"] for call in output["calls"]])
        self.assertIs(True, output["result"]["success"])
        self.assertEqual([], output["errors"])
        self.assertNotIn("[◷]", requests[0]["body"])
        self.assertNotRegex(requests[0]["body"], r"\{[a-z_]+\}")
        self.assertEqual({
            "/tmp/gh-aw/sandbox/firewall/logs", "/tmp/gh-aw/sandbox/firewall/logs/access.log",
            "/tmp/gh-aw/mcp-logs/gateway.jsonl",
            "/tmp/gh-aw/mcp-config/gateway-output.json", "/tmp/gh-aw/mcp-config/mcp-servers.json",
            "/tmp/gh-aw/mcp-config/config.toml", "/tmp/gh-aw/pi-agent-dir/mcp.json",
        }, {read["path"] for read in output["fsReads"]})

    def test_authored_test_review_scorecard_publishes_as_markdown_table(self) -> None:
        source, prompt = authored("shared/test-reviewer-shared.md")
        table = prompt.split("Do not emit an empty table. Otherwise emit the table below. -->", 1)[1]
        table = table.split("<!-- When it explains a finding", 1)[0].strip()
        expected_table = """| Result | Quality | Test | Notes | How to improve |
| --- | --- | --- | --- | --- |
| Pass | B (80–89) | new `ClassName.Method_WhenSomething_ReturnsValue` | One exact assertion protects the claimed result. | None |
| Failed | C (70–79) | mod `ClassName.OtherMethod` | The test only checks non-null, not the claimed order total. | Assert Total is 10 for the arranged 50-unit order. |
| Uncertain | — | mod `ClassName.MissingMethod` | The requested method body could not be resolved. | Supply the method body and its referenced fixture. |"""
        expected_body = (
            expected_table + "\n\n> 🤖 **Automated content by GitHub Copilot.** Generated by the "
            f"[{WORKFLOW_NAME}]({RUN_URL}/agentic_workflow) workflow. · \\~3 AIC\n\n"
            f"<!-- gh-aw-agentic-workflow: {WORKFLOW_NAME}, run: {RUN_URL} -->"
        )
        for consumer in ("test-reviewer-on-pr.agent.md", "test-reviewer.agent.md"):
            with self.subTest(consumer=consumer):
                configuration, _ = authored(consumer)
                self.assertIn("shared/test-reviewer-shared.md", configuration["imports"])
                output = self.run_runtime(
                    "review", body=table,
                    footerMode=source["safe-outputs"]["submit-pull-request-review"]["footer"],
                    template=configuration["safe-outputs"]["messages"]["footer"],
                )
                requests = [call["params"] for call in output["calls"] if call["operation"] == "pulls.createReview"]
                self.assertEqual([dict(owner="fixture", repo="workflows", pull_number=73,
                                       commit_id=REVIEWED_SHA, event="COMMENT", body=expected_body)], requests)
                self.assertIs(True, output["result"]["success"])
                self.assertEqual([], output["errors"])

    def test_review_table_preserves_escaped_cells_and_collapsed_rows(self) -> None:
        body = r"""<details>
<summary>Additional tests</summary>

| Result | Quality | Test | Notes | How to improve |
| --- | --- | --- | --- | --- |
| Uncertain | — | mod `ClassName.Method_With_A_Long_Name_And_Generic_Input<Value>` | Compare `left \| right` with `<value> & "quotes"`. | Supply the missing fixture. |

</details>"""
        output = self.run_runtime("review", body=body)
        requests = [call["params"] for call in output["calls"] if call["operation"] == "pulls.createReview"]
        self.assertEqual([dict(owner="fixture", repo="workflows", pull_number=73,
                               commit_id=REVIEWED_SHA, event="COMMENT", body=body)], requests)
        self.assertIs(True, output["result"]["success"])
        self.assertEqual([], output["errors"])

    def test_staged_review_previews_without_publication(self) -> None:
        output = self.run_runtime("review", body=REVIEW_BODY, staged=True)
        self.assertEqual(dict(success=True, staged=True), output["result"])
        self.assertEqual([], output["calls"])
        self.assertEqual([], output["errors"])
        self.assertEqual(1, len(output["summaries"]))
        self.assertIn("## 🎭 Staged Mode: PR Review Preview", output["summaries"][0])
        self.assertIn("**Review Body:**\n" + REVIEW_BODY, output["summaries"][0])

    def test_no_output_does_not_publish_a_review(self) -> None:
        output = self.run_runtime("review")
        self.assertEqual(dict(success=True, skipped=True), output["result"])
        self.assertEqual([], output["calls"])

    def test_empty_review_body_is_rejected_without_publication(self) -> None:
        output = self.run_runtime("review", body="")
        self.assertIs(False, output["result"]["success"])
        self.assertEqual("Empty review: review body is empty and no inline comments are present. "
                         "Skipping POST to avoid 422.", output["result"]["error"])
        self.assertEqual([], output["calls"])

    def test_review_api_rejection_is_reported_as_failure(self) -> None:
        output = self.run_runtime("review", body=REVIEW_BODY, reject=True)
        self.assertIs(False, output["result"]["success"])
        self.assertIn("Fixture review rejected", output["result"]["error"])
        self.assertNotIn("review_url", output["result"])
        self.assertEqual(["pulls.get", "pulls.listReviews", "pulls.createReview"],
                         [call["operation"] for call in output["calls"]])
        self.assertTrue(any("Fixture review rejected" in error for error in output["errors"]))

    def test_public_review_resolver_honors_explicit_public_only_policy(self) -> None:
        github = authored("shared/review-shared.md")[0]["tools"]["github"]
        output = self.run_runtime("guard", policy={
            "GH_AW_GITHUB_MIN_INTEGRITY": github.get("min-integrity", ""),
            "GH_AW_GITHUB_REPOS": github.get("allowed-repos", ""),
        })
        self.assertEqual(dict(visibility="public", min_integrity="none", repos="public"), output["outputs"])
        self.assertEqual(["repos.get"], [call["operation"] for call in output["calls"]])
        self.assertEqual([], output["errors"])

    def test_compiled_review_tools_remain_read_only_scoped_and_bash_disabled(self) -> None:
        source, _ = authored("shared/review-shared.md")
        self.assertIs(False, source["tools"]["bash"])
        self.assertEqual({"toolsets": ["pull_requests", "repos"], "min-integrity": "none", "allowed-repos": "public"},
                         source["tools"]["github"])
        self.assertEqual({"contents": "read", "pull-requests": "read"}, source["permissions"])
        self.assertEqual({"allowed": ["defaults", "dotnet", "github.com"]}, source["network"])
        self.assertIs(False, source["safe-outputs"]["footer"])
        self.assertEqual({"max": 1, "allowed-events": ["COMMENT", "REQUEST_CHANGES"], "footer": "none"},
                         source["safe-outputs"]["submit-pull-request-review"])
        for index, name in enumerate(CONSUMERS):
            with self.subTest(case=f"consumer_{index}"):
                document = compiled(name)
                gateway = step(document, "agent", "id", "start-mcp-gateway")
                marker = '"github": '
                fragment = gateway["run"].split(marker, 1)[1]
                guard_tokens = {
                    "approval-labels": "${GH_AW_GUARD_APPROVAL_LABELS}",
                    "blocked-users": "${GH_AW_GUARD_BLOCKED_USERS}",
                    "trusted-users": "${GH_AW_GUARD_TRUSTED_USERS}",
                }
                # Preserve known bare shell tokens as DATA; do not execute or
                # expand them, nor transform the min-integrity/repos constants.
                for field, token in guard_tokens.items():
                    fragment = fragment.replace(f'"{field}": {token}', f'"{field}": {json.dumps(token)}')
                server, _ = json.JSONDecoder().raw_decode(fragment)
                self.assertEqual({
                    "GITHUB_FEATURES": "fields_param", "GITHUB_HOST": "${GITHUB_SERVER_URL}",
                    "GITHUB_PERSONAL_ACCESS_TOKEN": "${GITHUB_MCP_SERVER_TOKEN}",
                    "GITHUB_READ_ONLY": "1", "GITHUB_TOOLSETS": "pull_requests,repos",
                }, server["env"])
                self.assertEqual(
                    "${{ secrets.GH_AW_GITHUB_MCP_SERVER_TOKEN || secrets.GH_AW_GITHUB_TOKEN || secrets.GITHUB_TOKEN }}",
                    gateway["env"]["GITHUB_MCP_SERVER_TOKEN"],
                )
                self.assertEqual({"allow-only": {
                    "min-integrity": "none", "repos": "public", **guard_tokens,
                }}, server["guard-policies"])
                self.assertEqual({"contents": "read", "pull-requests": "read", "copilot-requests": "write"},
                                 document["jobs"]["agent"]["permissions"])
                command = step(document, "agent", "id", "agentic_execution")["run"]
                self.assertEqual({"github", "safeoutputs", "write"},
                                 set(re.findall(r"--allow-tool\s+([\w-]+)", command)))
                self.assertNotIn("--allow-all-tools", command)
                self.assertIn("--deny-tool workflow", command)

    def assert_shared_prompt_delivery(self, document: dict) -> None:
        """Static registration check only; never execute generated Actions scripts."""
        prompt = step(document, "activation", "name", "Create prompt with built-in context")
        config = json.loads(prompt["env"]["GH_AW_PROMPT_CONFIG"])
        contents = [prompt["env"][item["content_env"]] for item in config["items"] if "content_env" in item]
        self.assertIn("{{#runtime-import .github/workflows/shared/review-shared.md}}\n", contents)

    def test_centralized_review_number_is_delivered_to_each_compiled_prompt(self) -> None:
        source, _ = authored("review.agent.md")
        self.assertEqual("centralized", source.get("on", source.get(True))["slash_command"]["strategy"])
        shared, prompt = authored("shared/review-shared.md")
        self.assertEqual(PR_EXPRESSION, shared["env"]["REVIEW_PR_NUMBER"])
        self.assertIn("Review pull request #${{ env.REVIEW_PR_NUMBER }}", prompt)
        self.assertNotIn("fromJSON(", prompt)  # Not allowed in v1.0.1 runtime-imported prompt text.
        for index, name in enumerate(CONSUMERS):
            with self.subTest(case=f"consumer_{index}"):
                document = compiled(name)
                self.assert_shared_prompt_delivery(document)
                for job in ("activation", "agent"):
                    effective = {**document.get("env", {}), **document["jobs"][job].get("env", {})}
                    self.assertEqual(PR_EXPRESSION, effective["REVIEW_PR_NUMBER"])
                activation_env = {**document.get("env", {}), **document["jobs"]["activation"].get("env", {})}
                for title in ("Create prompt with built-in context", "Interpolate variables and render templates",
                              "Substitute placeholders"):
                    binding = step(document, "activation", "name", title)
                    effective = {**activation_env, **binding.get("env", {})}
                    self.assertEqual(PR_EXPRESSION, effective["REVIEW_PR_NUMBER"])
                    self.assertEqual("${{ env.REVIEW_PR_NUMBER }}", binding["env"]["GH_AW_ENV_REVIEW_PR_NUMBER"])
                    if title == "Substitute placeholders":
                        self.assertIn("GH_AW_ENV_REVIEW_PR_NUMBER: process.env.GH_AW_ENV_REVIEW_PR_NUMBER",
                                      binding["with"]["script"])
                creator = step(document, "activation", "name", "Create prompt with built-in context")
                interpolator = step(document, "activation", "name", "Interpolate variables and render templates")
                self.assertIn("'interpolate_prompt.cjs'", interpolator["with"]["script"])
                self.assertEqual(creator["env"]["GH_AW_PROMPT"], interpolator["env"]["GH_AW_PROMPT"])

    def test_required_diff_failure_has_terminal_incomplete_prompt_delivery(self) -> None:
        prompt = " ".join(authored("shared/review-shared.md")[1].split())
        self.assertIn("If the required diff cannot be read, call `report_missing_data` with the concrete reason, "
                      "then `report_incomplete`, and stop without claiming that the review was delegated or completed.", prompt)
        for index, name in enumerate(CONSUMERS):
            with self.subTest(case=f"consumer_{index}"):
                document = compiled(name)
                self.assert_shared_prompt_delivery(document)
                config = next(item["env"]["GH_AW_SAFE_OUTPUTS_CONFIG"]
                              for item in document["jobs"]["agent"]["steps"]
                              if "GH_AW_SAFE_OUTPUTS_CONFIG" in item.get("env", {}))
                for tool in ("report_missing_data", "report_incomplete"):
                    self.assertEqual({}, json.loads(config)[tool])


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--actions-dir", type=pathlib.Path, required=True, help="Unmodified pinned setup/js directory.")
    parser.add_argument("--workflow-root", type=pathlib.Path, default=WORKFLOW_ROOT)
    arguments, unittest_arguments = parser.parse_known_args()
    ACTIONS_DIR = arguments.actions_dir.resolve()
    WORKFLOW_ROOT = arguments.workflow_root.resolve()
    unittest.main(argv=[__file__, *unittest_arguments])
