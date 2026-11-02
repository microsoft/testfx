#!/usr/bin/env python3

import argparse
import json
import pathlib
import subprocess
import tempfile
import unittest

import yaml


ROOT = pathlib.Path(__file__).resolve().parents[2]
SHARED_WORKFLOW = ROOT / ".github/workflows/shared/test-reviewer-shared.md"
FRONTMATTER = yaml.safe_load(SHARED_WORKFLOW.read_text(encoding="utf-8").split("---", 2)[1])
CORRECTION = next(
    step["with"]["script"]
    for step in FRONTMATTER["safe-outputs"]["steps"]
    if step["name"] == "Correct provisional CONNECT firewall reporting"
)

NODE_HARNESS = r"""
const fs = require("fs");
const path = require("path");
const vm = require("vm");
const input = JSON.parse(fs.readFileSync(0, "utf8"));
const modulePath = path.join(input.directory, "gh-aw", "actions", "firewall_blocked_domains.cjs");
fs.mkdirSync(path.dirname(modulePath), { recursive: true });
fs.writeFileSync(modulePath, input.source);
const logsDir = path.join(input.directory, "logs");
fs.mkdirSync(logsDir);
fs.writeFileSync(path.join(logsDir, "access.log"), input.log);
const originalLog = fs.readFileSync(path.join(logsDir, "access.log"), "utf8");
try {
  const apply = new Function("require", "process", "core", input.correction);
  for (let i = 0; i < input.applications; i++) {
    apply(require, { env: { RUNNER_TEMP: input.directory } }, { info() {} });
  }
} catch (error) {
  console.log(JSON.stringify({
    error: error.message,
    unchanged: fs.readFileSync(modulePath, "utf8") === input.source,
  }));
  process.exit(0);
}
const sandbox = {
  module: { exports: {} },
  __dirname: path.dirname(modulePath),
  require(name) {
    if (["fs", "path"].includes(name)) return require(name);
    if (name === "./sanitize_content_core.cjs") {
      return { sanitizeDomainName: value => value.replace(/[^a-zA-Z0-9.]/g, "") };
    }
    if (["./messages_core.cjs", "./render_template.cjs"].includes(name)) return {};
    throw new Error(`Unexpected upstream dependency: ${name}`);
  },
};
vm.runInNewContext(fs.readFileSync(modulePath, "utf8"), sandbox, { filename: modulePath });
const footer = sandbox.module.exports;
console.log(JSON.stringify({
  domains: footer.getBlockedDomains(logsDir),
  emptyFooter: footer.generateBlockedDomainsSection([]),
  logUnchanged: fs.readFileSync(path.join(logsDir, "access.log"), "utf8") === originalLog,
  decisions: input.decisions.map(([decision, status]) => footer.isRequestBlocked(decision, status)),
}));
"""


def entry(decision: str, status: int, domain: str = "github.com") -> str:
    return (
        f'1791806856.284 172.30.0.20:44806 {domain}:443 -:- 1.1 '
        f'CONNECT {status} {decision}:HIER_NONE {domain}:443 "git/2.55.0"\n'
    )


class GhAwFirewallFooterTests(unittest.TestCase):
    def run_module(
        self,
        log: str = "",
        decisions: tuple[tuple[str, str], ...] = (),
        source: str | None = None,
        applications: int = 1,
    ) -> dict:
        with tempfile.TemporaryDirectory() as directory:
            result = subprocess.run(
                ["node", "-e", NODE_HARNESS],
                input=json.dumps(
                    {
                        "directory": directory,
                        "source": SOURCE if source is None else source,
                        "log": log,
                        "decisions": decisions,
                        "correction": "" if WITHOUT_CORRECTION else CORRECTION,
                        "applications": applications,
                    }
                ),
                text=True,
                capture_output=True,
                check=True,
                timeout=30,
            )
            return json.loads(result.stdout)

    def test_successful_connect_has_no_blocked_domain(self) -> None:
        log = entry("NONE_NONE", 200) + entry("TCP_TUNNEL", 200)
        result = self.run_module(log)
        self.assertEqual([], result["domains"])
        self.assertEqual("", result["emptyFooter"])
        self.assertTrue(result["logUnchanged"])

    def test_explicit_denial_is_not_hidden_by_status_or_later_success(self) -> None:
        log = entry("TCP_DENIED", 200) + entry("TCP_TUNNEL", 200)
        self.assertEqual(["github.com"], self.run_module(log)["domains"])

    def test_authentication_and_unknown_outcomes_still_warn(self) -> None:
        log = entry("NONE_NONE", 403, "blocked.example")
        log += entry("NONE_NONE", 407, "proxy.example")
        log += entry("NONE_NONE", 0, "unknown.example")
        self.assertEqual(
            ["blocked.example", "proxy.example", "unknown.example"],
            self.run_module(log)["domains"],
        )

    def test_decision_matrix_preserves_success_and_failure_semantics(self) -> None:
        cases = [
            ("NONE_NONE", "200", False),
            ("NONE_NONE", "206", False),
            ("NONE_NONE", "304", False),
            ("TCP_DENIED", "200", True),
            ("TCP_DENIED", "403", True),
            ("TCP_TUNNEL", "200", False),
            ("TCP_TUNNEL", "403", True),
            ("TCP_TUNNEL", "407", True),
            ("NONE_NONE", "0", True),
            ("NONE_NONE", "500", True),
        ]
        result = self.run_module(decisions=tuple((decision, status) for decision, status, _ in cases))
        self.assertEqual([blocked for _, _, blocked in cases], result["decisions"])

    def test_empty_and_internal_healthcheck_logs_do_not_warn(self) -> None:
        self.assertEqual([], self.run_module()["domains"])
        log = '1791806749.004 ::1:57446 - -:- 0.0 - 0 NONE_NONE:HIER_NONE error:transaction-end-before-headers "-"\n'
        self.assertEqual([], self.run_module(log)["domains"])

    def test_reapplying_correction_is_idempotent(self) -> None:
        self.assertEqual([], self.run_module(entry("NONE_NONE", 200), applications=2)["domains"])

    def test_changed_upstream_module_fails_without_modifying_it(self) -> None:
        result = self.run_module(source=SOURCE + "\n// changed upstream module\n")
        self.assertEqual(
            "The gh-aw firewall footer module changed; revalidate or remove the v0.89.21 correction.",
            result["error"],
        )
        self.assertTrue(result["unchanged"])


if __name__ == "__main__":
    parser = argparse.ArgumentParser()
    parser.add_argument("--module-path", type=pathlib.Path, required=True)
    parser.add_argument("--without-correction", action="store_true")
    args, unittest_args = parser.parse_known_args()
    SOURCE = args.module_path.read_bytes().decode("utf-8")
    WITHOUT_CORRECTION = args.without_correction
    unittest.main(argv=[__file__, *unittest_args])
