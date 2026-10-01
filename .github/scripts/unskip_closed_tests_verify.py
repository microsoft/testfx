#!/usr/bin/env python3

import argparse
import json
import os
import pathlib
import re
import subprocess
import sys
from collections.abc import Callable, Sequence
from typing import Any


MAX_REQUEST_BYTES = 4 * 1024 * 1024
FQN_PATTERN = re.compile(
    r"^(?:@?[A-Za-z_][A-Za-z0-9_]*\.)+@?[A-Za-z_][A-Za-z0-9_]*$"
)
TFM_PATTERN = re.compile(r"^net(?P<version>[0-9]+)(?:\.[0-9]+)?(?:-[A-Za-z0-9.-]+)?$")


class VerificationError(ValueError):
    pass


def require_dict(value: Any, context: str) -> dict[str, Any]:
    if not isinstance(value, dict):
        raise VerificationError(f"{context} must be an object")
    return value


def require_list(value: Any, context: str) -> list[Any]:
    if not isinstance(value, list):
        raise VerificationError(f"{context} must be an array")
    return value


def require_string(value: Any, context: str) -> str:
    if not isinstance(value, str) or not value:
        raise VerificationError(f"{context} must be a non-empty string")
    return value


def normalize_relative_path(value: Any, context: str) -> pathlib.PurePosixPath:
    text = require_string(value, context)
    if "\\" in text:
        raise VerificationError(f"{context} must use '/' separators")
    path = pathlib.PurePosixPath(text)
    if path.is_absolute() or ".." in path.parts or text.startswith("./"):
        raise VerificationError(f"{context} must be repository-relative")
    return path


def load_request(path: pathlib.Path) -> dict[str, Any]:
    try:
        if path.stat().st_size > MAX_REQUEST_BYTES:
            raise VerificationError("Verification request is too large")
        with path.open(encoding="utf-8") as stream:
            return require_dict(json.load(stream), "verification request")
    except VerificationError:
        raise
    except (OSError, UnicodeError, json.JSONDecodeError) as error:
        raise VerificationError(f"Cannot read verification request: {error}") from error


def parse_request(value: Any) -> tuple[str, str, tuple[dict[str, str], ...]]:
    request = require_dict(value, "verification request")
    if request.get("schema_version") != "1":
        raise VerificationError("verification request.schema_version must be '1'")
    candidate = require_dict(
        request.get("candidate"), "verification request.candidate"
    )
    candidate_id = require_string(
        candidate.get("candidate_id"),
        "verification request.candidate.candidate_id",
    )
    source_commit = require_string(
        request.get("source_commit"), "verification request.source_commit"
    )
    if not re.fullmatch(r"[0-9a-fA-F]{40,64}", source_commit):
        raise VerificationError(
            "verification request.source_commit must be a full object id"
        )

    tests = []
    for index, value in enumerate(
        require_list(request.get("tests"), "verification request.tests")
    ):
        context = f"verification request.tests[{index}]"
        test = require_dict(value, context)
        fqn = require_string(test.get("fqn"), f"{context}.fqn")
        if not FQN_PATTERN.fullmatch(fqn):
            raise VerificationError(f"{context}.fqn is not a supported test identity")
        source_path = normalize_relative_path(
            test.get("source_path"), f"{context}.source_path"
        )
        if source_path.suffix.casefold() != ".cs":
            raise VerificationError(f"{context}.source_path must identify C# source")
        result_file = require_string(
            test.get("result_file"), f"{context}.result_file"
        )
        tests.append(
            {
                "fqn": fqn,
                "source_path": source_path.as_posix(),
                "result_file": result_file,
            }
        )
    if not tests:
        raise VerificationError("verification request.tests must not be empty")
    if len({test["fqn"] for test in tests}) != len(tests):
        raise VerificationError("verification request contains duplicate test identities")
    return candidate_id, source_commit.lower(), tuple(tests)


def run_git(root: pathlib.Path, *arguments: str) -> str:
    try:
        completed = subprocess.run(
            ["git", *arguments],
            cwd=root,
            check=True,
            stdout=subprocess.PIPE,
            stderr=subprocess.PIPE,
            text=True,
            encoding="utf-8",
        )
    except subprocess.CalledProcessError as error:
        stderr = error.stderr.strip() if error.stderr else str(error)
        raise VerificationError(f"git {' '.join(arguments)} failed: {stderr}") from error
    return completed.stdout.strip()


def validate_revision(root: pathlib.Path, source_commit: str) -> None:
    head = run_git(root, "rev-parse", "HEAD").lower()
    if head != source_commit:
        raise VerificationError(
            f"Repository revision changed from {source_commit} to {head}"
        )


def find_project(root: pathlib.Path, source_path: str) -> pathlib.Path:
    source = root / pathlib.PurePosixPath(source_path)
    if not source.is_file():
        raise VerificationError(f"Source file does not exist: {source_path}")
    if root.resolve() not in source.resolve().parents:
        raise VerificationError(f"Source file escapes repository: {source_path}")

    directory = source.parent
    while directory != root.parent:
        projects = sorted(directory.glob("*.csproj"))
        if len(projects) == 1:
            return projects[0]
        if len(projects) > 1:
            raise VerificationError(
                f"Source project is ambiguous for {source_path}: "
                + ", ".join(project.name for project in projects)
            )
        if directory == root:
            break
        directory = directory.parent
    raise VerificationError(f"No owning test project found for {source_path}")


def dotnet_path(root: pathlib.Path) -> str:
    executable = "dotnet.exe" if os.name == "nt" else "dotnet"
    local = root / ".dotnet" / executable
    return str(local) if local.is_file() else "dotnet"


def parse_msbuild_properties(output: str) -> dict[str, str]:
    start = output.find("{")
    if start >= 0:
        try:
            value = json.loads(output[start:])
            properties = require_dict(value.get("Properties"), "MSBuild properties")
            return {
                str(name): str(property_value)
                for name, property_value in properties.items()
            }
        except (json.JSONDecodeError, VerificationError, AttributeError):
            pass

    properties: dict[str, str] = {}
    for line in output.splitlines():
        name, separator, value = line.partition("=")
        if separator and name in (
            "TargetFrameworks",
            "TargetFramework",
            "OutputType",
        ):
            properties[name] = value.strip()
    if not properties:
        raise VerificationError("Cannot parse evaluated test project properties")
    return properties


def evaluate_project(
    root: pathlib.Path,
    project: pathlib.Path,
    runner: Callable[..., subprocess.CompletedProcess[str]] = subprocess.run,
) -> tuple[str, str]:
    command = [
        dotnet_path(root),
        "msbuild",
        str(project),
        "-nologo",
        "-getProperty:TargetFrameworks",
        "-getProperty:TargetFramework",
        "-getProperty:OutputType",
    ]
    try:
        completed = runner(
            command,
            cwd=root,
            check=True,
            stdout=subprocess.PIPE,
            stderr=subprocess.PIPE,
            text=True,
            encoding="utf-8",
        )
    except subprocess.CalledProcessError as error:
        stderr = error.stderr.strip() if error.stderr else str(error)
        raise VerificationError(
            f"Cannot evaluate {project.relative_to(root)}: {stderr}"
        ) from error
    properties = parse_msbuild_properties(completed.stdout)
    output_type = properties.get("OutputType", "")
    if output_type.casefold() != "exe":
        raise VerificationError(
            f"{project.relative_to(root)} is not an executable test project"
        )
    frameworks = [
        framework
        for framework in properties.get(
            "TargetFrameworks", properties.get("TargetFramework", "")
        ).split(";")
        if framework
    ]
    target_framework = select_target_framework(frameworks)
    return target_framework, output_type


def select_target_framework(frameworks: Sequence[str]) -> str:
    if "net8.0" in frameworks:
        return "net8.0"
    candidates = []
    for framework in frameworks:
        match = TFM_PATTERN.fullmatch(framework)
        if match and "." in framework and "-" not in framework:
            candidates.append((int(match.group("version")), framework))
    if not candidates:
        raise VerificationError(
            "No portable .NET target framework is available for verification"
        )
    return min(candidates)[1]


def validate_result_path(root: pathlib.Path, value: str) -> pathlib.Path:
    path = pathlib.Path(value)
    if not path.is_absolute():
        path = root / pathlib.PurePosixPath(value)
    resolved = path.resolve()
    runner_temp = os.environ.get("RUNNER_TEMP")
    allowed_root = pathlib.Path(runner_temp).resolve() if runner_temp else root.resolve()
    if resolved != allowed_root and allowed_root not in resolved.parents:
        raise VerificationError(
            f"Requested result file is outside the trusted output root: {value}"
        )
    if resolved.suffix.casefold() != ".trx":
        raise VerificationError(f"Requested result file must use .trx: {value}")
    return resolved


def run_checked(
    command: list[str],
    root: pathlib.Path,
    timeout_seconds: int,
    runner: Callable[..., subprocess.CompletedProcess[str]] = subprocess.run,
) -> None:
    try:
        runner(
            command,
            cwd=root,
            check=True,
            timeout=timeout_seconds,
            stdout=sys.stdout,
            stderr=sys.stderr,
            text=True,
            encoding="utf-8",
            env={
                **os.environ,
                "DOTNET_ROLL_FORWARD": os.environ.get(
                    "DOTNET_ROLL_FORWARD", "Major"
                ),
                "DOTNET_ROLL_FORWARD_TO_PRERELEASE": os.environ.get(
                    "DOTNET_ROLL_FORWARD_TO_PRERELEASE", "1"
                ),
            },
        )
    except subprocess.TimeoutExpired as error:
        raise VerificationError(
            f"Command timed out after {timeout_seconds} seconds: {' '.join(command)}"
        ) from error
    except subprocess.CalledProcessError as error:
        raise VerificationError(
            f"Command exited with {error.returncode}: {' '.join(command)}"
        ) from error


def requires_packed_packages(root: pathlib.Path, project: pathlib.Path) -> bool:
    relative = project.relative_to(root)
    return any(
        part.endswith(".Acceptance.IntegrationTests") for part in relative.parts
    )


def ensure_repository_built(
    root: pathlib.Path,
    source_commit: str,
    requires_pack: bool,
    timeout_seconds: int,
    runner: Callable[..., subprocess.CompletedProcess[str]] = subprocess.run,
) -> None:
    runner_temp = pathlib.Path(os.environ.get("RUNNER_TEMP", root / "artifacts" / "tmp"))
    marker_kind = "packed" if requires_pack else "built"
    marker = runner_temp / "testfx-unskip" / f"{marker_kind}-{source_commit}"
    if marker.is_file():
        return

    build_script = root / ("build.cmd" if os.name == "nt" else "build.sh")
    if not build_script.is_file():
        raise VerificationError(f"Repository build script is missing: {build_script}")
    command = (
        [os.environ.get("COMSPEC", "cmd.exe"), "/d", "/c", str(build_script)]
        if os.name == "nt"
        else [str(build_script)]
    )
    if requires_pack:
        command.append("-pack")
    run_checked(
        command,
        root,
        timeout_seconds,
        runner,
    )
    validate_revision(root, source_commit)
    marker.parent.mkdir(parents=True, exist_ok=True)
    marker.write_text(source_commit + "\n", encoding="utf-8")
    if requires_pack:
        built_marker = marker.parent / f"built-{source_commit}"
        built_marker.write_text(source_commit + "\n", encoding="utf-8")


def verify_tests(
    root: pathlib.Path,
    source_commit: str,
    tests: tuple[dict[str, str], ...],
    timeout_seconds: int,
    *,
    property_runner: Callable[..., subprocess.CompletedProcess[str]] = subprocess.run,
    command_runner: Callable[..., subprocess.CompletedProcess[str]] = subprocess.run,
) -> None:
    validate_revision(root, source_commit)
    projects = {find_project(root, test["source_path"]) for test in tests}
    if len(projects) != 1:
        raise VerificationError(
            "A single verification request must map to exactly one test project"
        )
    project = projects.pop()
    ensure_repository_built(
        root,
        source_commit,
        requires_packed_packages(root, project),
        timeout_seconds,
        command_runner,
    )
    target_framework, _ = evaluate_project(root, project, property_runner)

    run_checked(
        [
            dotnet_path(root),
            "build",
            str(project),
            "-c",
            "Debug",
            "-f",
            target_framework,
            "--no-restore",
            "-p:EnableCodeCoverage=False",
            "-bl:{}",
        ],
        root,
        timeout_seconds,
        command_runner,
    )
    validate_revision(root, source_commit)

    for test in tests:
        result_file = validate_result_path(root, test["result_file"])
        result_file.parent.mkdir(parents=True, exist_ok=True)
        result_file.unlink(missing_ok=True)
        run_checked(
            [
                dotnet_path(root),
                "run",
                "--project",
                str(project),
                "-c",
                "Debug",
                "-f",
                target_framework,
                "--no-build",
                "--no-restore",
                "-p:EnableCodeCoverage=False",
                "-bl:{}",
                "--",
                "--filter-uid",
                test["fqn"],
                "--report-trx",
                "--report-trx-filename",
                result_file.name,
                "--results-directory",
                str(result_file.parent),
            ],
            root,
            timeout_seconds,
            command_runner,
        )
        if not result_file.is_file():
            raise VerificationError(
                f"Test runner did not create requested TRX: {result_file}"
            )
        validate_revision(root, source_commit)


def create_parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(
        description="Run one trusted TestFX unskip verification request."
    )
    parser.add_argument("request", type=pathlib.Path)
    parser.add_argument(
        "--repository-root",
        type=pathlib.Path,
        default=pathlib.Path.cwd(),
    )
    parser.add_argument("--timeout-seconds", type=int, default=900)
    return parser


def main() -> int:
    arguments = create_parser().parse_args()
    try:
        _, source_commit, tests = parse_request(load_request(arguments.request))
        verify_tests(
            arguments.repository_root.resolve(),
            source_commit,
            tests,
            arguments.timeout_seconds,
        )
    except VerificationError as error:
        print(f"error: {error}", file=sys.stderr)
        return 1
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
