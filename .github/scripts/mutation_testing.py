#!/usr/bin/env python3

"""Plan, record, and aggregate repository mutation-testing runs."""

from __future__ import annotations

import argparse
import html
import json
import shutil
import subprocess
import sys
from pathlib import Path
from typing import Any
from xml.sax.saxutils import escape


SCHEMA_VERSION = 1
MUTANT_STATUSES = (
    "Killed",
    "Survived",
    "Timeout",
    "NoCoverage",
    "CompileError",
    "RuntimeError",
    "Ignored",
)


def load_manifest(path: Path) -> dict[str, Any]:
    manifest = json.loads(path.read_text(encoding="utf-8"))
    if not isinstance(manifest, dict):
        raise ValueError("Mutation manifest root must be an object")
    if manifest.get("schemaVersion") != SCHEMA_VERSION:
        raise ValueError(
            f"Unsupported mutation manifest schema: {manifest.get('schemaVersion')!r}"
        )

    modules = manifest.get("modules")
    if not isinstance(modules, list) or not modules:
        raise ValueError("Mutation manifest must contain at least one module")
    excluded_projects = manifest.get("excludedProjects", [])
    if not isinstance(excluded_projects, list):
        raise ValueError("Mutation manifest excludedProjects must be a list")
    excluded_paths: set[str] = set()
    for excluded in excluded_projects:
        if not isinstance(excluded, dict) or not all(
            isinstance(excluded.get(field), str) and excluded[field].strip()
            for field in ("project", "reason")
        ):
            raise ValueError("Excluded mutation projects require a project and reason")
        if excluded["project"] in excluded_paths:
            raise ValueError(f"Duplicate excluded mutation project: {excluded['project']}")
        excluded_paths.add(excluded["project"])

    known_target_count = manifest.get("knownTargetCount")
    if (
        not isinstance(known_target_count, int)
        or isinstance(known_target_count, bool)
        or known_target_count < len(modules) + len(excluded_projects)
    ):
        raise ValueError(
            "Mutation manifest knownTargetCount must cover every module and exclusion"
        )

    module_ids: set[str] = set()
    legacy_modules = 0
    for index, module in enumerate(modules):
        if not isinstance(module, dict):
            raise ValueError(f"Mutation module {index} must be an object")

        for field in ("id", "displayName", "project"):
            value = module.get(field)
            if not isinstance(value, str) or not value:
                raise ValueError(f"Mutation module {index} has an invalid {field}")

        module_id = module["id"]
        if module["project"] in excluded_paths:
            raise ValueError(f"Mutation project is both configured and excluded: {module['project']}")
        if module_id in module_ids:
            raise ValueError(f"Duplicate mutation module id: {module_id}")
        module_ids.add(module_id)

        test_projects = module.get("testProjects")
        if (
            not isinstance(test_projects, list)
            or not test_projects
            or not all(isinstance(project, str) and project for project in test_projects)
        ):
            raise ValueError(
                f"Mutation module {module_id} must have one or more test projects"
            )

        timeout = module.get("timeoutMinutes")
        if not isinstance(timeout, int) or isinstance(timeout, bool) or timeout <= 0:
            raise ValueError(
                f"Mutation module {module_id} has an invalid timeoutMinutes"
            )

        enabled = module.get("enabled")
        if not isinstance(enabled, bool):
            raise ValueError(f"Mutation module {module_id} has an invalid enabled flag")

        legacy_report = module.get("legacyReport", False)
        if not isinstance(legacy_report, bool):
            raise ValueError(
                f"Mutation module {module_id} has an invalid legacyReport flag"
            )
        if enabled and legacy_report:
            legacy_modules += 1

    if legacy_modules != 1:
        raise ValueError(
            "Exactly one enabled mutation module must have legacyReport set to true"
        )

    return manifest


def enabled_modules(manifest: dict[str, Any]) -> list[dict[str, Any]]:
    return [module for module in manifest["modules"] if module["enabled"]]


def find_module(manifest: dict[str, Any], module_id: str) -> dict[str, Any]:
    for module in manifest["modules"]:
        if module["id"] == module_id:
            return module
    raise ValueError(f"Unknown mutation module: {module_id}")


def validate_module_paths(module: dict[str, Any], repository_root: Path) -> None:
    paths = [module["project"], *module["testProjects"]]
    missing = [path for path in paths if not (repository_root / path).is_file()]
    if missing:
        raise ValueError(
            f"Mutation module {module['id']} references missing projects: "
            + ", ".join(missing)
        )


def validate_project_coverage(
    manifest: dict[str, Any], repository_root: Path
) -> None:
    source_root = repository_root / "src"
    source_projects = {
        project.relative_to(repository_root).as_posix()
        for project in source_root.rglob("*.csproj")
    }
    manifest_projects = [
        module["project"] for module in manifest["modules"]
    ] + [
        excluded["project"] for excluded in manifest.get("excludedProjects", [])
    ]
    configured_projects = set(manifest_projects)
    unconfigured_projects = sorted(source_projects - configured_projects)
    unknown_projects = sorted(configured_projects - source_projects)
    duplicate_projects = sorted(
        project
        for project in configured_projects
        if manifest_projects.count(project) > 1
    )

    if unconfigured_projects or unknown_projects or duplicate_projects:
        details = []
        if unconfigured_projects:
            details.append("unconfigured src projects: " + ", ".join(unconfigured_projects))
        if unknown_projects:
            details.append("projects not found under src: " + ", ".join(unknown_projects))
        if duplicate_projects:
            details.append("duplicate manifest projects: " + ", ".join(duplicate_projects))
        raise ValueError("Mutation manifest project inventory mismatch (" + "; ".join(details) + ")")

    if manifest["knownTargetCount"] != len(source_projects):
        raise ValueError(
            "Mutation manifest knownTargetCount must match the "
            f"{len(source_projects)} projects under src"
        )


def build_matrix(manifest: dict[str, Any], repository_root: Path) -> dict[str, Any]:
    validate_project_coverage(manifest, repository_root)
    modules = enabled_modules(manifest)
    for module in modules:
        validate_module_paths(module, repository_root)
    for excluded in manifest.get("excludedProjects", []):
        if not (repository_root / excluded["project"]).is_file():
            raise ValueError(f"Excluded mutation project does not exist: {excluded['project']}")
    return {"include": modules}


def write_solution(module: dict[str, Any], output: Path) -> None:
    project_paths = [module["project"], *module["testProjects"]]
    lines = ["<Solution>"]
    lines.extend(f'  <Project Path="{escape(path)}" />' for path in project_paths)
    lines.append("</Solution>")
    output.parent.mkdir(parents=True, exist_ok=True)
    output.write_text("\n".join(lines) + "\n", encoding="utf-8")


def empty_counts() -> dict[str, int]:
    return {status: 0 for status in MUTANT_STATUSES} | {"Other": 0}


def parse_report(report_path: Path) -> dict[str, int]:
    report = json.loads(report_path.read_text(encoding="utf-8-sig"))
    if not isinstance(report, dict):
        raise ValueError(f"{report_path} root must be an object")
    files = report.get("files")
    if not isinstance(files, dict):
        raise ValueError(f"{report_path} has no files object")

    counts = empty_counts()
    for file_path, file_report in files.items():
        if not isinstance(file_report, dict):
            raise ValueError(f"{report_path} has invalid data for {file_path}")
        mutants = file_report.get("mutants")
        if not isinstance(mutants, list):
            raise ValueError(f"{report_path} has no mutant list for {file_path}")
        for mutant in mutants:
            if not isinstance(mutant, dict):
                raise ValueError(f"{report_path} contains an invalid mutant")
            status = mutant.get("status")
            if status in counts:
                counts[status] += 1
            else:
                counts["Other"] += 1

    return counts


def mutation_score(counts: dict[str, int]) -> float | None:
    detected = counts["Killed"] + counts["Timeout"]
    denominator = detected + counts["Survived"] + counts["NoCoverage"]
    return None if denominator == 0 else detected / denominator * 100


def format_score(score: float | None) -> str:
    return "n/a" if score is None else f"{score:.2f}%"


def find_report(artifact_dir: Path) -> Path | None:
    reports = sorted(artifact_dir.rglob("mutation-report.json"))
    if not reports:
        return None
    if len(reports) > 1:
        raise ValueError(
            f"Multiple mutation reports found under {artifact_dir}: "
            + ", ".join(str(report) for report in reports)
        )
    return reports[0]


def record_module(
    manifest: dict[str, Any],
    module_id: str,
    artifact_dir: Path,
    source_commit: str,
    exit_code: int,
    duration_seconds: int,
    failure_reason: str | None = None,
) -> dict[str, Any]:
    module = find_module(manifest, module_id)
    report_path = find_report(artifact_dir)
    counts = empty_counts()
    status = "failed" if exit_code != 0 else "missing-report"
    error = failure_reason

    if report_path is not None:
        try:
            counts = parse_report(report_path)
            if exit_code == 0:
                status = "success"
        except (OSError, json.JSONDecodeError, TypeError, ValueError) as exception:
            status = "invalid-report"
            error = str(exception)

    result = {
        "schemaVersion": SCHEMA_VERSION,
        "module": {
            "id": module["id"],
            "displayName": module["displayName"],
            "project": module["project"],
            "testProjects": module["testProjects"],
            "legacyReport": module.get("legacyReport", False),
        },
        "sourceCommit": source_commit,
        "status": status,
        "exitCode": exit_code,
        "durationSeconds": duration_seconds,
        "reportPath": (
            report_path.relative_to(artifact_dir).as_posix()
            if report_path is not None
            else None
        ),
        "counts": counts,
        "mutationScore": mutation_score(counts),
        "error": error,
    }
    artifact_dir.mkdir(parents=True, exist_ok=True)
    (artifact_dir / "module-result.json").write_text(
        json.dumps(result, indent=2, sort_keys=True) + "\n",
        encoding="utf-8",
    )
    (artifact_dir / "module-summary.md").write_text(
        build_module_summary(result),
        encoding="utf-8",
    )
    return result


def build_module_summary(result: dict[str, Any]) -> str:
    counts = result["counts"]
    return "\n".join(
        [
            f"## Mutation testing: {result['module']['displayName']}",
            "",
            f"Status: **{result['status']}**; mutation score: "
            f"**{format_score(result['mutationScore'])}**; duration: "
            f"**{result['durationSeconds']}s**.",
            "",
            *([f"Failure: {result['error']}", ""] if result["error"] else []),
            "| Killed | Survived | No coverage | Timeout | Compile error | Runtime error | Ignored |",
            "| ---: | ---: | ---: | ---: | ---: | ---: | ---: |",
            f"| {counts['Killed']} | {counts['Survived']} | "
            f"{counts['NoCoverage']} | {counts['Timeout']} | "
            f"{counts['CompileError']} | {counts['RuntimeError']} | "
            f"{counts['Ignored']} |",
            "",
        ]
    )


def load_module_results(artifacts_dir: Path) -> dict[str, tuple[dict[str, Any], Path]]:
    results: dict[str, tuple[dict[str, Any], Path]] = {}
    for result_path in sorted(artifacts_dir.rglob("module-result.json")):
        result = json.loads(result_path.read_text(encoding="utf-8"))
        if not isinstance(result, dict) or result.get("schemaVersion") != SCHEMA_VERSION:
            raise ValueError(f"Invalid module result: {result_path}")
        module = result.get("module")
        module_id = module.get("id") if isinstance(module, dict) else None
        if not isinstance(module_id, str) or not module_id:
            raise ValueError(f"Module result has no module id: {result_path}")
        if module_id in results:
            raise ValueError(f"Duplicate module result for {module_id}")
        results[module_id] = (result, result_path.parent)
    return results


def sum_counts(results: list[dict[str, Any]]) -> dict[str, int]:
    totals = empty_counts()
    for result in results:
        for status in totals:
            totals[status] += int(result["counts"].get(status, 0))
    return totals


def build_aggregate_summary(report: dict[str, Any]) -> str:
    completion = "Complete" if report["complete"] else "Partial"
    totals = report["totals"]
    lines = [
        "## Weekly mutation testing report",
        "",
        f"Status: **{completion}**; source commit: `{report['sourceCommit']}`; "
        f"modules: **{report['successfulModules']}/{report['expectedModules']} "
        f"successful**; onboarding coverage: "
        f"**{report['expectedModules']}/{report['knownTargetCount']} discovered "
        f"targets**.",
        "",
        f"Aggregate mutation score for available results: "
        f"**{format_score(report['mutationScore'])}**.",
        "",
        "| Module | Status | Score | Duration | Killed | Survived | No coverage | Timeout | Invalid |",
        "| --- | --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: |",
    ]
    for module in report["modules"]:
        counts = module["counts"]
        invalid = counts["CompileError"] + counts["RuntimeError"] + counts["Other"]
        lines.append(
            f"| `{module['id']}` | {module['status']} | "
            f"{format_score(module['mutationScore'])} | "
            f"{module['durationSeconds']}s | {counts['Killed']} | "
            f"{counts['Survived']} | {counts['NoCoverage']} | "
            f"{counts['Timeout']} | {invalid} |"
        )

    lines.extend(
        [
            "",
            "| Total killed | Total survived | Total no coverage | Total timeout |",
            "| ---: | ---: | ---: | ---: |",
            f"| {totals['Killed']} | {totals['Survived']} | "
            f"{totals['NoCoverage']} | {totals['Timeout']} |",
            "",
        ]
    )
    if report["problems"]:
        lines.extend(["### Incomplete modules", ""])
        lines.extend(f"- {problem}" for problem in report["problems"])
        lines.append("")
    if report["excludedProjects"]:
        lines.extend(["### Explicitly excluded targets", ""])
        lines.extend(
            f"- `{excluded['project']}`: {excluded['reason']}"
            for excluded in report["excludedProjects"]
        )
        lines.append("")
    lines.append(
        "Download the `mutation-testing-full-report` artifact for the aggregate "
        "JSON, HTML index, module logs, and detailed Stryker reports."
    )
    lines.append("")
    return "\n".join(lines)


def build_aggregate_html(report: dict[str, Any]) -> str:
    rows = []
    for module in report["modules"]:
        report_link = module.get("reportLink")
        module_name = html.escape(module["displayName"])
        if report_link is not None:
            module_name = (
                f'<a href="{html.escape(report_link)}">{module_name}</a>'
            )
        rows.append(
            "<tr>"
            f"<td>{module_name}</td>"
            f"<td>{html.escape(module['status'])}</td>"
            f"<td>{format_score(module['mutationScore'])}</td>"
            f"<td>{module['durationSeconds']}s</td>"
            f"<td>{module['counts']['Killed']}</td>"
            f"<td>{module['counts']['Survived']}</td>"
            f"<td>{module['counts']['NoCoverage']}</td>"
            f"<td>{module['counts']['Timeout']}</td>"
            "</tr>"
        )

    problems = "".join(f"<li>{html.escape(problem)}</li>" for problem in report["problems"])
    problem_section = f"<h2>Incomplete modules</h2><ul>{problems}</ul>" if problems else ""
    exclusions = "".join(
        f"<li><code>{html.escape(excluded['project'])}</code>: "
        f"{html.escape(excluded['reason'])}</li>"
        for excluded in report["excludedProjects"]
    )
    exclusion_section = (
        f"<h2>Explicitly excluded targets</h2><ul>{exclusions}</ul>" if exclusions else ""
    )
    completion = "Complete" if report["complete"] else "Partial"
    return f"""<!doctype html>
<html lang="en">
<head>
  <meta charset="utf-8">
  <title>Weekly mutation testing report</title>
  <style>
    body {{ font-family: sans-serif; margin: 2rem; }}
    table {{ border-collapse: collapse; width: 100%; }}
    th, td {{ border: 1px solid #bbb; padding: 0.4rem; text-align: right; }}
    th:first-child, td:first-child, th:nth-child(2), td:nth-child(2) {{ text-align: left; }}
  </style>
</head>
<body>
  <h1>Weekly mutation testing report</h1>
  <p>Status: <strong>{completion}</strong>; source commit:
  <code>{html.escape(report['sourceCommit'])}</code>; aggregate score:
  <strong>{format_score(report['mutationScore'])}</strong>; onboarding coverage:
  <strong>{report['expectedModules']}/{report['knownTargetCount']}</strong>
  discovered targets.</p>
  <table>
    <thead><tr><th>Module</th><th>Status</th><th>Score</th><th>Duration</th>
    <th>Killed</th><th>Survived</th><th>No coverage</th><th>Timeout</th></tr></thead>
    <tbody>{''.join(rows)}</tbody>
  </table>
  {problem_section}
  {exclusion_section}
</body>
</html>
"""


def aggregate_results(
    manifest: dict[str, Any],
    artifacts_dir: Path,
    output_dir: Path,
    source_commit: str,
) -> dict[str, Any]:
    expected = enabled_modules(manifest)
    loaded_results = load_module_results(artifacts_dir)
    output_dir.mkdir(parents=True, exist_ok=True)

    modules: list[dict[str, Any]] = []
    problems: list[str] = []
    successful_results: list[dict[str, Any]] = []
    for expected_module in expected:
        module_id = expected_module["id"]
        loaded = loaded_results.get(module_id)
        if loaded is None:
            problems.append(f"`{module_id}` did not upload a module result.")
            modules.append(
                {
                    "id": module_id,
                    "displayName": expected_module["displayName"],
                    "status": "missing",
                    "durationSeconds": 0,
                    "counts": empty_counts(),
                    "mutationScore": None,
                    "reportLink": None,
                }
            )
            continue

        result, artifact_root = loaded
        module_copy = output_dir / "modules" / module_id
        shutil.copytree(artifact_root, module_copy, dirs_exist_ok=True)
        result_commit = result.get("sourceCommit")
        status = result.get("status")
        if result_commit != source_commit:
            status = "commit-mismatch"
            problems.append(
                f"`{module_id}` reported commit `{result_commit}` instead of "
                f"`{source_commit}`."
            )
        elif status != "success":
            detail = f" {result['error']}" if result.get("error") else ""
            problems.append(f"`{module_id}` completed with status `{status}`.{detail}")
        else:
            successful_results.append(result)

        report_path = result.get("reportPath")
        report_link = (
            f"modules/{module_id}/{report_path}"
            if isinstance(report_path, str) and report_path
            else None
        )
        modules.append(
            {
                "id": module_id,
                "displayName": expected_module["displayName"],
                "status": status,
                "durationSeconds": result.get("durationSeconds", 0),
                "counts": result.get("counts", empty_counts()),
                "mutationScore": result.get("mutationScore"),
                "reportLink": (
                    report_link.removesuffix(".json") + ".html"
                    if report_link is not None
                    else None
                ),
            }
        )

    unexpected = sorted(set(loaded_results) - {module["id"] for module in expected})
    problems.extend(f"Unexpected module result: `{module_id}`." for module_id in unexpected)

    totals = sum_counts(successful_results)
    report = {
        "schemaVersion": SCHEMA_VERSION,
        "sourceCommit": source_commit,
        "complete": not problems,
        "expectedModules": len(expected),
        "knownTargetCount": manifest["knownTargetCount"],
        "excludedProjects": manifest.get("excludedProjects", []),
        "successfulModules": len(successful_results),
        "totals": totals,
        "mutationScore": mutation_score(totals),
        "modules": modules,
        "problems": problems,
    }
    (output_dir / "mutation-testing-summary.json").write_text(
        json.dumps(report, indent=2, sort_keys=True) + "\n",
        encoding="utf-8",
    )
    (output_dir / "summary.md").write_text(
        build_aggregate_summary(report),
        encoding="utf-8",
    )
    (output_dir / "index.html").write_text(
        build_aggregate_html(report),
        encoding="utf-8",
    )
    return report


def gh_json(*arguments: str) -> Any:
    result = subprocess.run(
        ["gh", *arguments],
        check=True,
        capture_output=True,
        text=True,
        encoding="utf-8",
        timeout=60,
    )
    return json.loads(result.stdout)


def prepare_improver_input(
    repository: str, event_name: str, event: dict[str, Any], output_dir: Path
) -> dict[str, Any]:
    if event_name == "workflow_run":
        run_id = event["workflow_run"]["id"]
    elif event_name == "workflow_dispatch":
        runs = gh_json(
            "run", "list", "--repo", repository, "--workflow", "mutation-testing.yml",
            "--branch", "main", "--status", "completed", "--limit", "1",
            "--json", "databaseId",
        )
        if not isinstance(runs, list) or len(runs) != 1:
            raise ValueError("No completed main-branch mutation run was found")
        run_id = runs[0]["databaseId"]
    else:
        raise ValueError(f"Unsupported mutation improver event: {event_name}")

    if not isinstance(run_id, int) or isinstance(run_id, bool) or run_id <= 0:
        raise ValueError("Invalid upstream mutation run id")
    run = gh_json("api", f"repos/{repository}/actions/runs/{run_id}")
    if (
        run.get("id") != run_id
        or run.get("path") != ".github/workflows/mutation-testing.yml"
        or run.get("repository", {}).get("full_name") != repository
        or run.get("head_repository", {}).get("full_name") != repository
        or run.get("head_branch") != "main"
        or run.get("status") != "completed"
    ):
        raise ValueError("Upstream run is not a completed repository/main mutation run")
    source_commit = run.get("head_sha")
    if (
        not isinstance(source_commit, str)
        or len(source_commit) != 40
        or any(character not in "0123456789abcdef" for character in source_commit)
    ):
        raise ValueError("Upstream run has an invalid source commit")

    inventory = gh_json(
        "api", f"repos/{repository}/actions/runs/{run_id}/artifacts?per_page=100"
    )
    if (
        not isinstance(inventory.get("artifacts"), list)
        or not isinstance(inventory.get("total_count"), int)
        or inventory["total_count"] != len(inventory["artifacts"])
    ):
        raise ValueError("Incomplete upstream artifact inventory (limit: 100)")
    artifacts = [
        artifact for artifact in inventory["artifacts"]
        if artifact["name"] == "mutation-testing-report"
    ]
    if len(artifacts) > 1:
        raise ValueError("Multiple mutation-testing-report artifacts were found")
    if output_dir.exists():
        raise ValueError("Mutation input directory must be fresh")
    output_dir.mkdir(parents=True)
    prepared = {
        "repository": repository,
        "runId": run_id,
        "runAttempt": run["run_attempt"],
        "sourceCommit": source_commit,
        "conclusion": run["conclusion"],
        "artifactId": artifacts[0]["id"] if artifacts else None,
        "artifactStatus": "unavailable",
        "reportPath": None,
        "error": None,
    }
    if not artifacts or artifacts[0]["expired"]:
        prepared["error"] = "Mutation report artifact is missing or expired"
    else:
        artifact_dir = output_dir / "stryker-report"
        try:
            # Runs on the trusted runner before gh-aw starts its text-only CLI proxy.
            subprocess.run(
                [
                    "gh", "run", "download", str(run_id), "--repo", repository,
                    "--name", "mutation-testing-report", "--dir", str(artifact_dir),
                ],
                check=True,
                capture_output=True,
                timeout=180,
            )
            module_result_path = artifact_dir / "module-result.json"
            if module_result_path.is_file():
                module_result = json.loads(module_result_path.read_text(encoding="utf-8"))
                if (
                    not isinstance(module_result, dict)
                    or module_result.get("schemaVersion") != SCHEMA_VERSION
                    or module_result.get("status") != "success"
                    or module_result.get("sourceCommit") != source_commit
                    or not isinstance(module_result.get("module"), dict)
                    or module_result["module"].get("id") != "server-mode-client"
                ):
                    raise ValueError("ServerMode module failed or source identity mismatched")
            elif run["conclusion"] != "success":
                raise ValueError("Failed upstream run has no successful module metadata")
            report_path = find_report(artifact_dir)
            if report_path is None:
                raise ValueError("Mutation report JSON is missing")
            parse_report(report_path)
            prepared["artifactStatus"] = "available"
            prepared["reportPath"] = report_path.relative_to(output_dir).as_posix()
        except subprocess.CalledProcessError as error:
            prepared["error"] = f"Mutation artifact download failed (exit {error.returncode})"
        except subprocess.TimeoutExpired:
            prepared["error"] = "Mutation artifact download timed out"
        except (OSError, json.JSONDecodeError, TypeError, ValueError) as error:
            prepared["error"] = f"Invalid mutation artifact: {error}"
        if prepared["error"]:
            print(f"warning: {prepared['error']}", file=sys.stderr)
    (output_dir / "input.json").write_text(
        json.dumps(prepared, indent=2) + "\n", encoding="utf-8"
    )
    return prepared


def main() -> int:
    parser = argparse.ArgumentParser()
    subparsers = parser.add_subparsers(dest="command", required=True)

    matrix_parser = subparsers.add_parser("matrix")
    matrix_parser.add_argument("--manifest", type=Path, required=True)
    matrix_parser.add_argument("--repository-root", type=Path, default=Path.cwd())

    solution_parser = subparsers.add_parser("create-solution")
    solution_parser.add_argument("--manifest", type=Path, required=True)
    solution_parser.add_argument("--module", required=True)
    solution_parser.add_argument("--output", type=Path, required=True)

    record_parser = subparsers.add_parser("record-module")
    record_parser.add_argument("--manifest", type=Path, required=True)
    record_parser.add_argument("--module", required=True)
    record_parser.add_argument("--artifact-dir", type=Path, required=True)
    record_parser.add_argument("--source-commit", required=True)
    record_parser.add_argument("--exit-code", type=int, required=True)
    record_parser.add_argument("--duration-seconds", type=int, required=True)
    record_parser.add_argument("--failure-reason")

    check_parser = subparsers.add_parser("check-module")
    check_parser.add_argument("--result", type=Path, required=True)

    aggregate_parser = subparsers.add_parser("aggregate")
    aggregate_parser.add_argument("--manifest", type=Path, required=True)
    aggregate_parser.add_argument("--artifacts-dir", type=Path, required=True)
    aggregate_parser.add_argument("--output-dir", type=Path, required=True)
    aggregate_parser.add_argument("--source-commit", required=True)

    prepare_parser = subparsers.add_parser("prepare-improver")
    prepare_parser.add_argument("--repository", required=True)
    prepare_parser.add_argument("--event-name", required=True)
    prepare_parser.add_argument("--event-path", type=Path, required=True)
    prepare_parser.add_argument("--output-dir", type=Path, required=True)

    args = parser.parse_args()
    try:
        if args.command == "matrix":
            manifest = load_manifest(args.manifest)
            print(
                json.dumps(
                    build_matrix(manifest, args.repository_root),
                    separators=(",", ":"),
                )
            )
            return 0

        if args.command == "create-solution":
            manifest = load_manifest(args.manifest)
            write_solution(find_module(manifest, args.module), args.output)
            return 0

        if args.command == "record-module":
            manifest = load_manifest(args.manifest)
            record_module(
                manifest,
                args.module,
                args.artifact_dir,
                args.source_commit,
                args.exit_code,
                args.duration_seconds,
                args.failure_reason,
            )
            return 0

        if args.command == "check-module":
            result = json.loads(args.result.read_text(encoding="utf-8"))
            return 0 if result.get("status") == "success" else 1

        if args.command == "aggregate":
            manifest = load_manifest(args.manifest)
            report = aggregate_results(
                manifest,
                args.artifacts_dir,
                args.output_dir,
                args.source_commit,
            )
            return 0 if report["complete"] else 1
        if args.command == "prepare-improver":
            event = json.loads(args.event_path.read_text(encoding="utf-8"))
            prepare_improver_input(
                args.repository, args.event_name, event, args.output_dir
            )
            return 0
    except subprocess.CalledProcessError as error:
        print(f"error: GitHub lookup failed (exit {error.returncode})", file=sys.stderr)
        return 1
    except subprocess.TimeoutExpired:
        print("error: GitHub lookup timed out", file=sys.stderr)
        return 1
    except (OSError, json.JSONDecodeError, TypeError, ValueError) as error:
        print(f"error: {error}", file=sys.stderr)
        return 1

    raise AssertionError(f"Unhandled command: {args.command}")


if __name__ == "__main__":
    raise SystemExit(main())
