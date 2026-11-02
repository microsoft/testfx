import json
import os
from pathlib import Path
import re
import shutil
import subprocess
import tempfile
import textwrap
import unittest


WORKFLOWS = Path(__file__).resolve().parents[1]
REPO = WORKFLOWS.parents[1]
SOURCE = Path(os.environ.get(
    "TEST_VALUE_REVIEW_SOURCE", WORKFLOWS / "shared" / "test-reviewer-shared.md"
))


def production_extractor():
    lines = SOURCE.read_text(encoding="utf-8").splitlines()
    step = lines.index("  - name: Extract changed test review inputs")
    start = lines.index("    run: |", step) + 1
    end = next(
        index for index in range(start, len(lines))
        if lines[index] and not lines[index].startswith("      ")
    )
    return textwrap.dedent("\n".join(lines[start:end])) + "\n"


class ReviewInputTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.bash = (
            r"C:\Program Files\Git\bin\bash.exe"
            if os.name == "nt" else shutil.which("bash")
        )
        if not cls.bash or not Path(cls.bash).is_file():
            raise RuntimeError("Bash is required to exercise the workflow's actual extractor.")

    def setUp(self):
        self.directory = tempfile.TemporaryDirectory(prefix="test-value-evidence-")
        self.addCleanup(self.directory.cleanup)
        self.root = Path(self.directory.name)
        self.env = {
            key: value for key, value in os.environ.items()
            if not key.startswith("GIT_")
        }
        self.env.update(GIT_CONFIG_NOSYSTEM="1", GIT_CONFIG_GLOBAL=os.devnull)
        self.git("init", "--quiet", "--initial-branch=main")
        self.git("config", "user.name", "Fixture")
        self.git("config", "user.email", "fixture@example.invalid")
        self.git("config", "core.autocrlf", "false")
        self.write("src/Product.cs", "class Product\n{\n    int Value => 1;\n}\n")
        self.write("src/Removed.cs", "class Removed { }\n")
        self.write("src/Resources.resx", "<root><value>old</value></root>\n")
        self.write("test/ProductTests.cs", "class ProductTests\n{\n    void Test() { }\n}\n")
        self.write("test/RemovedTests.cs", "class RemovedTests { }\n")
        self.write("README.md", "Documentation.\n")
        self.base = self.commit()

    def git(self, *args):
        result = subprocess.run(
            ["git", "--no-pager", *args], cwd=self.root, env=self.env,
            text=True, capture_output=True, timeout=30,
        )
        self.assertEqual(0, result.returncode, result.stderr)
        return result.stdout.strip()

    def write(self, path, content):
        target = self.root / path
        target.parent.mkdir(parents=True, exist_ok=True)
        target.write_text(
            content, encoding="utf-8-sig" if target.suffix == ".cs" else "utf-8"
        )

    def commit(self):
        self.git("add", "--all")
        self.git("commit", "--quiet", "-m", "Fixture revision")
        return self.git("rev-parse", "HEAD")

    def extract(self, base=None, head=None, expected_exit=0, pr_number="11927"):
        output = self.root / "outputs.txt"
        runner_temp = self.root / "runner-temp"
        runner_temp.mkdir()
        self.inputs_root = runner_temp / "test-review"
        env = {
            **self.env,
            "BASE_SHA": base or self.base,
            "HEAD_SHA": head or self.git("rev-parse", "HEAD"),
            "PR_NUMBER": pr_number,
            "GITHUB_REPOSITORY": "microsoft/testfx",
            "RUNNER_TEMP": runner_temp.as_posix(),
            "INPUTS_DIR": self.inputs_root.as_posix(),
            "GITHUB_OUTPUT": output.as_posix(),
        }
        result = subprocess.run(
            [self.bash, "--noprofile", "--norc", "-s"],
            input=production_extractor(), cwd=self.root, env=env,
            text=True, capture_output=True, timeout=30,
        )
        if expected_exit != 0:
            self.assertNotEqual(0, result.returncode)
            self.assertFalse(output.exists(), "Failed resolution must not emit success outputs.")
            return {}
        self.assertEqual(0, result.returncode, result.stderr)
        outputs = dict(line.split("=", 1) for line in output.read_text().splitlines())
        files = {
            key: Path(value).read_text().splitlines()
            for key, value in outputs.items() if key.endswith("_path")
        }
        return {**outputs, **files}

    def inputs_manifest(self):
        manifest = self.inputs_root / "review-inputs.json"
        self.assertTrue(manifest.is_file(), "The agent-visible review input manifest is missing.")
        return json.loads(manifest.read_text(encoding="utf-8"))

    def test_manifest_preserves_revision_identity_and_review_paths(self):
        self.write("src/Product.cs", "class Product { int Value => 2; }\n")
        self.write("test/ProductTests.cs", "class ProductTests { void ChangedTest() { } }\n")
        self.write("Directory.Build.props", "<Project />\n")
        head = self.commit()
        self.extract()
        self.assertEqual(
            {
                "schema_version": 1,
                "repository": "microsoft/testfx",
                "pr_number": 11927,
                "base_sha": self.base,
                "merge_base": self.base,
                "head_sha": head,
                "test_count": 1,
                "src_count": 1,
                "config_count": 1,
                "has_parallel_scope": True,
                "changed_files_path": (self.inputs_root / "changed-review-files.txt").as_posix(),
                "test_files_path": (self.inputs_root / "changed-test-files.txt").as_posix(),
                "test_regions_path": (self.inputs_root / "changed-test-regions.tsv").as_posix(),
                "src_files_path": (self.inputs_root / "changed-src-files.txt").as_posix(),
                "config_files_path": (self.inputs_root / "changed-config-files.txt").as_posix(),
            },
            self.inputs_manifest(),
        )

    def test_inputs_remain_readable_when_prompt_is_rendered_before_extraction(self):
        source = SOURCE.read_text(encoding="utf-8")
        prompt = source.split("---", 2)[2]
        early_prompt = re.sub(r"\$\{\{\s*steps\.[^}]+\}\}", "", prompt)
        self.assertTrue(
            "      INPUTS_DIR: /tmp/gh-aw/test-review\n" in source,
            "The extractor must write inputs inside the agent-visible tree.",
        )
        self.assertTrue(
            "`/tmp/gh-aw/test-review/review-inputs.json`" in early_prompt,
            "The early-rendered prompt must retain the agent-visible manifest path.",
        )
        self.assertFalse(
            "${{ steps." in prompt,
            "The prompt must not interpolate outputs from custom steps that have not run.",
        )
        self.write("src/Product.cs", "class Product { int Value => 2; }\n")
        head = self.commit()
        self.extract()
        manifest = self.inputs_manifest()
        self.assertEqual(head, manifest["head_sha"])
        self.assertEqual(
            ["src/Product.cs"],
            Path(manifest["changed_files_path"]).read_text().splitlines(),
        )
        for key, value in manifest.items():
            if key.endswith("_path"):
                self.assertTrue(Path(value).is_file(), key)

    def test_missing_pr_identity_does_not_publish_a_manifest_or_success_outputs(self):
        self.extract(expected_exit=1, pr_number="")
        self.assertFalse((self.inputs_root / "review-inputs.json").exists())

    def test_source_only_change_keeps_evidence_scope_without_parallel_audit(self):
        self.write("src/Product.cs", "class Product\n{\n    int Value => 2;\n}\n")
        self.commit()
        outputs = self.extract()
        self.assertEqual(["src/Product.cs"], outputs["src_files_path"])
        self.assertEqual("0", outputs["test_count"])
        self.assertEqual("false", outputs["has_parallel_scope"])
        self.assertEqual(["src/Product.cs"], outputs.get("changed_files_path"))

    def test_non_csharp_product_change_is_not_dropped(self):
        self.write("src/Resources.resx", "<root><value>new</value></root>\n")
        self.commit()
        self.assertEqual(["src/Resources.resx"], self.extract()["src_files_path"])

    def test_deleted_source_is_reviewed_on_old_side(self):
        (self.root / "src" / "Removed.cs").unlink()
        self.commit()
        self.assertEqual(["src/Removed.cs"], self.extract()["src_files_path"])

    def test_source_rename_preserves_both_paths(self):
        self.git("mv", "src/Product.cs", "src/Renamed.cs")
        self.commit()
        self.assertEqual(
            ["src/Product.cs", "src/Renamed.cs"], self.extract()["src_files_path"]
        )

    def test_deleted_test_is_not_misreported_as_no_test_change(self):
        (self.root / "test" / "RemovedTests.cs").unlink()
        self.commit()
        outputs = self.extract()
        self.assertEqual(["test/RemovedTests.cs"], outputs["test_files_path"])
        self.assertEqual([], outputs["test_regions_path"])
        self.assertEqual("true", outputs["has_parallel_scope"])

    def test_test_rename_has_only_existing_head_ranges(self):
        self.git("mv", "test/ProductTests.cs", "test/RenamedTests.cs")
        self.commit()
        outputs = self.extract()
        self.assertEqual(
            ["test/ProductTests.cs", "test/RenamedTests.cs"], outputs["test_files_path"]
        )
        self.assertEqual(["test/RenamedTests.cs\t1-4"], outputs["test_regions_path"])

    def test_root_configuration_change_still_gets_parallel_audit(self):
        self.write("Directory.Build.props", "<Project />\n")
        self.commit()
        outputs = self.extract()
        self.assertEqual(["Directory.Build.props"], outputs["config_files_path"])
        self.assertEqual("true", outputs["has_parallel_scope"])
        self.assertEqual(["Directory.Build.props"], outputs.get("changed_files_path"))

    def test_behavior_outside_src_and_csharp_is_in_complete_inventory(self):
        self.write("eng/example.ps1", "exit 1\n")
        self.write(".github/scripts/example.py", "raise RuntimeError('example')\n")
        self.commit()
        self.assertEqual(
            [".github/scripts/example.py", "eng/example.ps1"],
            self.extract().get("changed_files_path"),
        )

    def test_documentation_only_change_retains_inventory_without_gradable_tests(self):
        self.write("README.md", "Updated documentation.\n")
        self.commit()
        outputs = self.extract()
        self.assertEqual(["README.md"], outputs.get("changed_files_path"))
        self.assertEqual("0", outputs["test_count"])
        self.assertEqual("false", outputs["has_parallel_scope"])

    def test_merge_base_excludes_unrelated_base_branch_changes(self):
        self.git("checkout", "--quiet", "-b", "base-moved")
        self.write("src/Unrelated.cs", "class Unrelated { }\n")
        base_tip = self.commit()
        self.git("checkout", "--quiet", "main")
        self.write("src/Product.cs", "class Product { int Value => 2; }\n")
        head = self.commit()
        outputs = self.extract(base=base_tip, head=head)
        self.assertEqual(self.base, outputs["merge_base"])
        self.assertEqual(["src/Product.cs"], outputs["src_files_path"])

    def test_empty_diff_is_a_real_empty_result(self):
        outputs = self.extract()
        self.assertEqual([], outputs["src_files_path"])
        self.assertEqual([], outputs["test_files_path"])
        self.assertEqual([], outputs.get("changed_files_path"))
        self.assertEqual("false", outputs["has_parallel_scope"])
        manifest = self.inputs_manifest()
        self.assertEqual(self.base, manifest["head_sha"])
        self.assertEqual(0, manifest["test_count"])
        self.assertEqual(0, manifest["src_count"])
        self.assertEqual(0, manifest["config_count"])
        self.assertFalse(manifest["has_parallel_scope"])
        for key, value in manifest.items():
            if key.endswith("_path"):
                self.assertEqual("", Path(value).read_text(), key)

    def test_invalid_revision_is_failure_not_empty_success(self):
        self.extract(base="not-a-revision", expected_exit=1)


class ReportingContractTests(unittest.TestCase):
    def test_readme_clean_review_policy_matches_evidence_publication(self):
        readme = (REPO / ".github" / "workflows" / "README.md").read_text(
            encoding="utf-8"
        )
        contract = readme.split("### Test-quality skill contract", 1)[1].split(
            "### Firewall access", 1
        )[0]
        contract = " ".join(contract.split())
        self.assertIn(
            "Clean automatic runs still publish revision-bound test-value evidence",
            contract,
        )
        self.assertIn("complete same-head duplicate", contract)
        self.assertNotIn("Clean automatic runs are silent", contract)

    def test_delivery_template_preserves_decision_and_evidence_sections(self):
        template = (REPO / ".github" / "PULL_REQUEST_TEMPLATE.md").read_text(
            encoding="utf-8"
        )
        headings = [
            "# Pull request",
            "## Summary",
            "## Design and decision record",
            "## Demonstration",
            "## Validation and regression proof",
            "### Test-value evidence",
        ]
        for heading in headings:
            self.assertIn(heading, template)
        positions = [template.index(heading) for heading in headings]
        self.assertEqual(sorted(positions), positions)
        self.assertIn(".github/skills/feature-delivery/SKILL.md", template)
        self.assertIn(".github/skills/testfx-test-value-evidence/SKILL.md", template)

    def test_result_assessment_retains_clean_review_evidence(self):
        source = SOURCE.read_text(encoding="utf-8")
        self.assertIn("<th>Result</th><th>Quality</th>", source)
        self.assertIn("Otherwise publish the evidence even when all results are `Pass`", source)
        self.assertIn("Likely killed (inferred)", source)
        self.assertIn("Candidate survivor (unverified)", source)
        self.assertIn("#### Test-value evidence", source)
        self.assertNotIn("A clean automatic run is silent.", source)

    def test_canonical_survival_rule_distinguishes_measured_and_static(self):
        canonical = REPO / ".agents" / "skills" / "test-value-evidence" / "SKILL.md"
        body = canonical.read_text(encoding="utf-8").split("---", 2)[2]
        guidance = " ".join(body.split())
        self.assertIn(
            "Report **executed: survived** only for an executed, non-equivalent fault",
            guidance,
        )
        self.assertIn("**static: predicts survival**", guidance)

    def test_canonical_description_preserves_identified_test_change_guard(self):
        canonical = REPO / ".agents" / "skills" / "test-value-evidence" / "SKILL.md"
        frontmatter = canonical.read_text(encoding="utf-8").split("---", 2)[1]
        description = " ".join(frontmatter.split())
        self.assertIn("identified code or test change", description)
        self.assertIn("DO NOT USE without an identified change", description)

    def test_upstream_skill_is_revision_pinned_and_matches_its_blob(self):
        manifest = json.loads((REPO / "eng" / "vendored-files.json").read_text(encoding="utf-8"))
        entries = [entry for entry in manifest["entries"] if entry["id"] == "test-value-evidence"]
        self.assertEqual(1, len(entries))
        entry = entries[0]
        self.assertEqual(".agents/skills/test-value-evidence/SKILL.md", entry["local_path"])
        self.assertEqual(1, len(entry["sources"]))
        source = entry["sources"][0]
        self.assertEqual("dotnet/skills", source["repo"])
        self.assertEqual(
            "plugins/dotnet-test/skills/test-value-evidence/SKILL.md", source["path"]
        )
        self.assertEqual(source["baseline_ref_sha"], source["ref"])
        self.assertRegex(source["ref"], r"^[a-f0-9]{40}$")
        result = subprocess.run(
            ["git", "hash-object", "--path=" + entry["local_path"], entry["local_path"]],
            cwd=REPO, text=True, capture_output=True, timeout=30,
        )
        self.assertEqual(0, result.returncode, result.stderr)
        self.assertEqual(source["baseline_blob_sha"], result.stdout.strip())

    def test_testfx_adapter_does_not_shadow_or_fork_canonical_skill(self):
        canonical = REPO / ".agents" / "skills" / "test-value-evidence" / "SKILL.md"
        adapter = REPO / ".github" / "skills" / "testfx-test-value-evidence" / "SKILL.md"
        canonical_text = canonical.read_text(encoding="utf-8")
        adapter_text = adapter.read_text(encoding="utf-8")
        self.assertIn("name: test-value-evidence\n", canonical_text)
        self.assertIn("name: testfx-test-value-evidence\n", adapter_text)
        self.assertNotIn("TestFx", canonical_text)
        self.assertNotIn("testfx-acceptance-validation", canonical_text)
        self.assertFalse(
            (REPO / ".github" / "skills" / "test-value-evidence" / "SKILL.md").exists()
        )
        self.assertIn(
            "../../../.agents/skills/test-value-evidence/SKILL.md", adapter_text
        )
        self.assertIn("testfx-acceptance-validation", adapter_text)
        self.assertIn("testfx-parallel-safety-preflight", adapter_text)

    def test_reviewer_loads_canonical_skill_and_testfx_adapter(self):
        source = SOURCE.read_text(encoding="utf-8")
        self.assertIn("Read `.agents/skills/test-value-evidence/SKILL.md`", source)
        self.assertIn("`.github/skills/testfx-test-value-evidence/SKILL.md`", source)

    def test_automatic_trigger_does_not_require_test_paths(self):
        source = (WORKFLOWS / "test-reviewer-on-pr.agent.md").read_text(encoding="utf-8")
        trigger = source.split("on:\n", 1)[1].split("\n# Skip draft", 1)[0]
        self.assertNotIn("paths:", trigger)
        for event in ("opened", "reopened", "synchronize", "ready_for_review"):
            self.assertIn(event, trigger)

    def test_report_is_revision_bound_and_static_by_default(self):
        source = SOURCE.read_text(encoding="utf-8")
        self.assertIn("### Step 0 — Assess counterfactual test-value evidence", source)
        self.assertIn("#### Test-value evidence", source)
        self.assertTrue(
            "test-value-evidence:<head_sha>" in source,
            "Report markers must use the runtime-resolved head SHA.",
        )
        self.assertTrue(
            "Replace `<pr_number>`, `<merge_base>`, and `<head_sha>`" in source,
            "Report placeholders must be replaced with exact manifest values.",
        )
        self.assertIn("Do not execute PR code, builds, tests, or mutation tools.", source)
        self.assertIn("static predictions, not executed mutation results", source)

    def test_live_head_and_duplicate_checks_precede_inline_publication(self):
        source = SOURCE.read_text(encoding="utf-8").split(
            "### Step 5 — Submit one combined test-quality review", 1
        )[1]
        self.assertLess(
            source.index("read the PR's live state and head"),
            source.index("then publish qualifying prepared inline"),
        )
        self.assertLess(
            source.index("suppress only a\nduplicate"),
            source.index("then publish qualifying prepared inline"),
        )
        self.assertNotIn("every graded test earned A;", source)
        self.assertIn("Queued test quality review", source)


if __name__ == "__main__":
    unittest.main()
