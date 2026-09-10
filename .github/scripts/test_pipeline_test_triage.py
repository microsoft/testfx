#!/usr/bin/env python3

import importlib.util
import pathlib
import unittest


SCRIPT_PATH = pathlib.Path(__file__).with_name("pipeline_test_triage.py")
SPEC = importlib.util.spec_from_file_location("pipeline_test_triage", SCRIPT_PATH)
assert SPEC is not None
assert SPEC.loader is not None
PIPELINE_TEST_TRIAGE = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(PIPELINE_TEST_TRIAGE)


class PipelineTestTriageTests(unittest.TestCase):
    def test_artifact_family_removes_attempt_suffix(self) -> None:
        self.assertEqual(
            "testresults_linux_release",
            PIPELINE_TEST_TRIAGE.artifact_family("TestResults_Linux_Release_Attempt12"),
        )

    def test_artifact_family_preserves_non_attempt_suffix(self) -> None:
        self.assertEqual(
            "windows_app_model_diagnostics",
            PIPELINE_TEST_TRIAGE.artifact_family("Windows_App_Model_Diagnostics"),
        )

    def test_select_history_artifacts_keeps_matching_attempts_only(self) -> None:
        artifacts = [
            {"name": "TestResults_Windows_Release_Attempt2"},
            {"name": "TestResults_Linux_Release_Attempt1"},
            {"name": "Windows_App_Model_Diagnostics_Attempt1"},
            {"name": "Build_Logs"},
        ]

        selected = PIPELINE_TEST_TRIAGE.select_history_artifacts(
            artifacts,
            {"testresults_linux_release"},
        )

        self.assertEqual([{"name": "TestResults_Linux_Release_Attempt1"}], selected)

    def test_select_history_artifacts_keeps_all_test_artifacts_without_filter(self) -> None:
        artifacts = [
            {"name": "TestResults_Windows_Release_Attempt2"},
            {"name": "Windows_App_Model_Diagnostics_Attempt1"},
            {"name": "Build_Logs"},
        ]

        selected = PIPELINE_TEST_TRIAGE.select_history_artifacts(artifacts, set())

        self.assertEqual(
            [
                {"name": "TestResults_Windows_Release_Attempt2"},
                {"name": "Windows_App_Model_Diagnostics_Attempt1"},
            ],
            selected,
        )

    def test_history_candidates_excludes_slow_only_pr_results(self) -> None:
        names, artifact_families = PIPELINE_TEST_TRIAGE.history_candidates(
            [
                {
                    "name": "SlowTest",
                    "status": "passed",
                    "duration": 60000,
                    "sourceFile": "TestResults_Linux_Release_Attempt1/test.trx",
                }
            ],
            include_slow=False,
        )

        self.assertEqual(set(), names)
        self.assertEqual(set(), artifact_families)

    def test_history_candidates_includes_failures_when_slow_is_disabled(self) -> None:
        names, artifact_families = PIPELINE_TEST_TRIAGE.history_candidates(
            [
                {
                    "name": "FailedTest",
                    "status": "failed",
                    "duration": 10,
                    "sourceFile": "TestResults_Linux_Release_Attempt1/test.trx",
                    "extra": {"displayName": "Failed test"},
                }
            ],
            include_slow=False,
        )

        self.assertEqual({"failedtest", "failed test"}, names)
        self.assertEqual({"testresults_linux_release"}, artifact_families)


if __name__ == "__main__":
    unittest.main()
