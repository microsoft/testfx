#!/usr/bin/env python3

"""Plan, record, and aggregate repository mutation-testing runs."""

from __future__ import annotations

import argparse
import html
import json
import shutil
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
    known_target_count = manifest.get("knownTargetCount")
    if (
        not isinstance(known_target_count, int)
        or isinstance(known_target_count, bool)
        or known_target_count < len(modules)
    ):
        raise ValueError(
            "Mutation manifest knownTargetCount must cover every configured module"
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


def build_matrix(manifest: dict[str, Any], repository_root: Path) -> dict[str, Any]:
    modules = enabled_modules(manifest)
    for module in modules:
        validate_module_paths(module, repository_root)
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
) -> dict[str, Any]:
    module = find_module(manifest, module_id)
    report_path = find_report(artifact_dir)
    counts = empty_counts()
    status = "failed" if exit_code != 0 else "missing-report"
    error: str | None = None

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
            problems.append(f"`{module_id}` completed with status `{status}`.")
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

    check_parser = subparsers.add_parser("check-module")
    check_parser.add_argument("--result", type=Path, required=True)

    aggregate_parser = subparsers.add_parser("aggregate")
    aggregate_parser.add_argument("--manifest", type=Path, required=True)
    aggregate_parser.add_argument("--artifacts-dir", type=Path, required=True)
    aggregate_parser.add_argument("--output-dir", type=Path, required=True)
    aggregate_parser.add_argument("--source-commit", required=True)

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
    except (OSError, json.JSONDecodeError, TypeError, ValueError) as error:
        print(f"error: {error}", file=sys.stderr)
        return 1

    raise AssertionError(f"Unhandled command: {args.command}")


if __name__ == "__main__":
    raise SystemExit(main())
