import json
import os
from pathlib import Path
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

    def extract(self, base=None, head=None, expected_exit=0):
        output = self.root / "outputs.txt"
        runner_temp = self.root / "runner-temp"
        runner_temp.mkdir()
        env = {
            **self.env,
            "BASE_SHA": base or self.base,
            "HEAD_SHA": head or self.git("rev-parse", "HEAD"),
            "RUNNER_TEMP": runner_temp.as_posix(),
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

    def test_invalid_revision_is_failure_not_empty_success(self):
        self.extract(base="not-a-revision", expected_exit=1)


class ReportingContractTests(unittest.TestCase):
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
        self.assertIn("test-value-evidence:${{ steps.resolve.outputs.head_sha }}", source)
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
