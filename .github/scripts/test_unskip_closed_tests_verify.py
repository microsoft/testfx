#!/usr/bin/env python3

import importlib.util
import os
import pathlib
import subprocess
import tempfile
import unittest
from unittest import mock


SCRIPT_PATH = pathlib.Path(__file__).with_name("unskip_closed_tests_verify.py")
SPEC = importlib.util.spec_from_file_location("unskip_closed_tests_verify", SCRIPT_PATH)
assert SPEC is not None
assert SPEC.loader is not None
VERIFY = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(VERIFY)


class UnskipClosedTestsVerifyTests(unittest.TestCase):
    def test_parse_request_requires_exact_source_and_result_identities(self) -> None:
        candidate_id, source_commit, tests = VERIFY.parse_request(
            {
                "schema_version": "1",
                "source_commit": "a" * 40,
                "candidate": {"candidate_id": "candidate-1"},
                "tests": [
                    {
                        "fqn": "Example.Tests.TestOne",
                        "source_path": "test/UnitTests/Example/Tests.cs",
                        "result_file": "results/TestOne.trx",
                    }
                ],
            }
        )

        self.assertEqual("candidate-1", candidate_id)
        self.assertEqual("a" * 40, source_commit)
        self.assertEqual("Example.Tests.TestOne", tests[0]["fqn"])
        self.assertEqual("test/UnitTests/Example/Tests.cs", tests[0]["source_path"])
        self.assertEqual("results/TestOne.trx", tests[0]["result_file"])

    def test_parse_request_rejects_fabricated_or_duplicate_test_identity(self) -> None:
        for fqn in ("Invented", "Example.Tests.Test One"):
            with self.subTest(fqn=fqn), self.assertRaisesRegex(
                VERIFY.VerificationError, "supported test identity"
            ):
                VERIFY.parse_request(
                    {
                        "schema_version": "1",
                        "source_commit": "a" * 40,
                        "candidate": {"candidate_id": "candidate-1"},
                        "tests": [
                            {
                                "fqn": fqn,
                                "source_path": "test/Example/Tests.cs",
                                "result_file": "results/test.trx",
                            }
                        ],
                    }
                )

        duplicate = {
            "fqn": "Example.Tests.TestOne",
            "source_path": "test/Example/Tests.cs",
            "result_file": "results/test.trx",
        }
        with self.assertRaisesRegex(VERIFY.VerificationError, "duplicate"):
            VERIFY.parse_request(
                {
                    "schema_version": "1",
                    "source_commit": "a" * 40,
                    "candidate": {"candidate_id": "candidate-1"},
                    "tests": [duplicate, duplicate],
                }
            )

    def test_find_project_uses_nearest_unambiguous_project(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            root = pathlib.Path(directory)
            source = root / "test" / "UnitTests" / "Example" / "Tests.cs"
            source.parent.mkdir(parents=True)
            source.write_text("class Tests {}", encoding="utf-8")
            project = source.parent / "Example.csproj"
            project.write_text("<Project />", encoding="utf-8")

            self.assertEqual(
                project,
                VERIFY.find_project(
                    root, "test/UnitTests/Example/Tests.cs"
                ),
            )

    def test_find_project_rejects_ambiguous_or_unmapped_source(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            root = pathlib.Path(directory)
            source = root / "test" / "Example" / "Tests.cs"
            source.parent.mkdir(parents=True)
            source.write_text("class Tests {}", encoding="utf-8")
            (source.parent / "One.csproj").write_text("<Project />", encoding="utf-8")
            (source.parent / "Two.csproj").write_text("<Project />", encoding="utf-8")

            with self.assertRaisesRegex(VERIFY.VerificationError, "ambiguous"):
                VERIFY.find_project(root, "test/Example/Tests.cs")

        with tempfile.TemporaryDirectory() as directory:
            root = pathlib.Path(directory)
            source = root / "test" / "Example" / "Tests.cs"
            source.parent.mkdir(parents=True)
            source.write_text("class Tests {}", encoding="utf-8")

            with self.assertRaisesRegex(VERIFY.VerificationError, "No owning"):
                VERIFY.find_project(root, "test/Example/Tests.cs")

    def test_select_target_framework_prefers_portable_net8(self) -> None:
        self.assertEqual(
            "net8.0",
            VERIFY.select_target_framework(("net462", "net8.0", "net10.0")),
        )
        self.assertEqual(
            "net9.0",
            VERIFY.select_target_framework(("net9.0", "net10.0")),
        )
        with self.assertRaisesRegex(VERIFY.VerificationError, "No portable"):
            VERIFY.select_target_framework(("net462", "net8.0-windows"))

    def test_repository_bootstrap_runs_pack_once_for_acceptance(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            root = pathlib.Path(directory)
            project = (
                root
                / "test"
                / "IntegrationTests"
                / "Example.Acceptance.IntegrationTests"
                / "Example.Acceptance.IntegrationTests.csproj"
            )
            project.parent.mkdir(parents=True)
            project.write_text("<Project />", encoding="utf-8")
            build_script = root / ("build.cmd" if os.name == "nt" else "build.sh")
            build_script.write_text("", encoding="utf-8")
            commands: list[list[str]] = []

            def runner(
                command: list[str], **kwargs: object
            ) -> subprocess.CompletedProcess[str]:
                commands.append(command)
                return subprocess.CompletedProcess(command, 0, stdout="", stderr="")

            with mock.patch.object(
                VERIFY, "validate_revision", return_value=None
            ), mock.patch.dict(os.environ, {"RUNNER_TEMP": str(root / "runner-temp")}):
                VERIFY.ensure_repository_built(root, "a" * 40, True, 30, runner)
                VERIFY.ensure_repository_built(root, "a" * 40, True, 30, runner)

            expected = (
                [
                    [
                        os.environ.get("COMSPEC", "cmd.exe"),
                        "/d",
                        "/c",
                        str(build_script),
                        "-pack",
                    ]
                ]
                if os.name == "nt"
                else [[str(build_script), "-pack"]]
            )
            self.assertEqual(expected, commands)

    def test_verify_tests_runs_exact_fqn_and_requested_trx_path(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            root = pathlib.Path(directory)
            source = root / "test" / "Example" / "Tests.cs"
            source.parent.mkdir(parents=True)
            source.write_text("class Tests {}", encoding="utf-8")
            project = source.parent / "Example.csproj"
            project.write_text("<Project />", encoding="utf-8")
            result = root / "results" / "TestOne.trx"
            commands: list[list[str]] = []

            def property_runner(
                command: list[str], **kwargs: object
            ) -> subprocess.CompletedProcess[str]:
                return subprocess.CompletedProcess(
                    command,
                    0,
                    stdout=(
                        '{"Properties":{"TargetFrameworks":"net8.0;net10.0",'
                        '"TargetFramework":"","OutputType":"Exe"}}'
                    ),
                    stderr="",
                )

            def command_runner(
                command: list[str], **kwargs: object
            ) -> subprocess.CompletedProcess[str]:
                commands.append(command)
                if "--report-trx-filename" in command:
                    result.parent.mkdir(parents=True, exist_ok=True)
                    result.write_text("<TestRun />", encoding="utf-8")
                return subprocess.CompletedProcess(command, 0, stdout="", stderr="")

            with mock.patch.object(
                VERIFY, "validate_revision", return_value=None
            ), mock.patch.object(
                VERIFY, "ensure_repository_built", return_value=None
            ), mock.patch.dict(os.environ, {"RUNNER_TEMP": str(root)}):
                VERIFY.verify_tests(
                    root,
                    "a" * 40,
                    (
                        {
                            "fqn": "Example.Tests.TestOne",
                            "source_path": "test/Example/Tests.cs",
                            "result_file": str(result),
                        },
                    ),
                    30,
                    property_runner=property_runner,
                    command_runner=command_runner,
                )

            self.assertEqual("build", commands[0][1])
            self.assertIn("-p:EnableCodeCoverage=False", commands[0])
            self.assertIn("-bl:{}", commands[0])
            self.assertIn("--filter-uid", commands[1])
            self.assertIn("-p:EnableCodeCoverage=False", commands[1])
            self.assertIn("-bl:{}", commands[1])
            self.assertEqual(
                "Example.Tests.TestOne",
                commands[1][commands[1].index("--filter-uid") + 1],
            )
            self.assertEqual(
                result.name,
                commands[1][commands[1].index("--report-trx-filename") + 1],
            )
            self.assertTrue(
                pathlib.Path(
                    commands[1][commands[1].index("--results-directory") + 1]
                ).samefile(result.parent)
            )

    def test_verify_tests_rejects_missing_requested_result(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            root = pathlib.Path(directory)
            source = root / "test" / "Example" / "Tests.cs"
            source.parent.mkdir(parents=True)
            source.write_text("class Tests {}", encoding="utf-8")
            (source.parent / "Example.csproj").write_text(
                "<Project />", encoding="utf-8"
            )

            def property_runner(
                command: list[str], **kwargs: object
            ) -> subprocess.CompletedProcess[str]:
                return subprocess.CompletedProcess(
                    command,
                    0,
                    stdout=(
                        '{"Properties":{"TargetFrameworks":"net8.0",'
                        '"TargetFramework":"","OutputType":"Exe"}}'
                    ),
                    stderr="",
                )

            def command_runner(
                command: list[str], **kwargs: object
            ) -> subprocess.CompletedProcess[str]:
                return subprocess.CompletedProcess(command, 0, stdout="", stderr="")

            with mock.patch.object(
                VERIFY, "validate_revision", return_value=None
            ), mock.patch.object(
                VERIFY, "ensure_repository_built", return_value=None
            ), mock.patch.dict(os.environ, {"RUNNER_TEMP": str(root)}):
                with self.assertRaisesRegex(
                    VERIFY.VerificationError, "did not create requested TRX"
                ):
                    VERIFY.verify_tests(
                        root,
                        "a" * 40,
                        (
                            {
                                "fqn": "Example.Tests.TestOne",
                                "source_path": "test/Example/Tests.cs",
                                "result_file": str(root / "results" / "missing.trx"),
                            },
                        ),
                        30,
                        property_runner=property_runner,
                        command_runner=command_runner,
                    )


if __name__ == "__main__":
    unittest.main()
