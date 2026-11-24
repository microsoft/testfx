#!/usr/bin/env python3

import json
import pathlib
import unittest

import yaml


ROOT = pathlib.Path(__file__).resolve().parents[2]
WORKFLOWS = ("test-reviewer-on-pr.agent", "test-reviewer.agent")
SHARED_WORKFLOW = "shared/test-reviewer-shared"


def source_footer(workflow: str) -> str:
    path = ROOT / ".github/workflows" / f"{workflow}.md"
    frontmatter = yaml.safe_load(path.read_text(encoding="utf-8").split("---", 2)[1])
    return frontmatter["safe-outputs"]["messages"]["footer"]


def message_configs(node):
    if isinstance(node, dict):
        if "GH_AW_SAFE_OUTPUT_MESSAGES" in node:
            yield json.loads(node["GH_AW_SAFE_OUTPUT_MESSAGES"])
        for value in node.values():
            yield from message_configs(value)
    elif isinstance(node, list):
        for value in node:
            yield from message_configs(value)


class GhAwHistoryFooterTests(unittest.TestCase):
    def assert_inline_history(self, footer: str) -> None:
        self.assertEqual(1, footer.count("{history_link}"))
        self.assertRegex(footer, r"\{history_link\}$")
        self.assertNotRegex(footer, r"\[[^\]]*\]\(\s*\{history_link\}\s*\)")

    def test_sources_use_history_as_an_inline_fragment(self) -> None:
        for workflow in (*WORKFLOWS, SHARED_WORKFLOW):
            with self.subTest(workflow=workflow):
                self.assert_inline_history(source_footer(workflow))

    def test_review_workflows_use_the_shared_footer(self) -> None:
        for workflow in WORKFLOWS:
            with self.subTest(workflow=workflow):
                self.assertEqual(source_footer(SHARED_WORKFLOW), source_footer(workflow))

    def test_compiled_workflows_preserve_the_inline_fragment(self) -> None:
        for workflow in WORKFLOWS:
            with self.subTest(workflow=workflow):
                path = ROOT / ".github/workflows" / f"{workflow}.lock.yml"
                configs = list(message_configs(yaml.safe_load(path.read_text(encoding="utf-8"))))
                self.assertGreater(len(configs), 0)
                for config in configs:
                    self.assert_inline_history(config["footer"])
                    self.assertEqual(source_footer(workflow), config["footer"])

    def test_compilation_preserves_the_configured_setup_node_pin(self) -> None:
        pins = json.loads((ROOT / ".github/aw/actions-lock.json").read_text(encoding="utf-8"))
        expected = {
            entry["sha"]
            for entry in pins["entries"].values()
            if entry["repo"] == "actions/setup-node"
        }
        self.assertEqual(1, len(expected))
        for workflow in WORKFLOWS:
            with self.subTest(workflow=workflow):
                path = ROOT / ".github/workflows" / f"{workflow}.lock.yml"
                compiled = yaml.safe_load(path.read_text(encoding="utf-8"))
                actual = {
                    step["uses"].split("@", 1)[1].split()[0]
                    for job in compiled["jobs"].values()
                    for step in job.get("steps", [])
                    if step.get("uses", "").startswith("actions/setup-node@")
                }
                self.assertEqual(expected, actual)


if __name__ == "__main__":
    unittest.main()
