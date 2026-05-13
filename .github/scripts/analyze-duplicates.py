"""
Analyze jscpd duplication report using GitHub Models API.

Reads the jscpd JSON report, extracts top findings, sends them to an LLM
for classification and refactoring suggestions, and creates/updates a GitHub issue.
"""

import json
import os
import subprocess
import sys
from pathlib import Path


def load_report(report_path: str) -> dict:
    with open(report_path) as f:
        return json.load(f)


def extract_top_findings(report: dict, top_n: int) -> list[dict]:
    duplicates = report.get("duplicates", [])
    duplicates.sort(key=lambda d: d.get("lines", 0), reverse=True)
    return duplicates[:top_n]


def read_file_lines(file_path: str, start: int, end: int) -> str:
    """Read specific lines from a file. Lines are 1-indexed."""
    try:
        path = Path(file_path)
        if not path.exists():
            # Try stripping absolute prefix for CI
            path = Path(file_path.lstrip("/"))
        if not path.exists():
            return f"[Could not read {file_path}]"
        lines = path.read_text(encoding="utf-8", errors="replace").splitlines()
        # Clamp to file bounds
        start = max(1, start)
        end = min(len(lines), end)
        selected = lines[start - 1 : end]
        return "\n".join(f"{start + i:4d} | {line}" for i, line in enumerate(selected))
    except Exception as e:
        return f"[Error reading {file_path}: {e}]"


def build_finding_context(finding: dict) -> str:
    """Build a context string for a single duplicate finding."""
    first = finding["firstFile"]
    second = finding["secondFile"]
    lines = finding.get("lines", 0)

    first_path = first["name"]
    first_start = first["startLoc"]["line"]
    first_end = first["endLoc"]["line"]

    second_path = second["name"]
    second_start = second["startLoc"]["line"]
    second_end = second["endLoc"]["line"]

    first_code = read_file_lines(first_path, first_start, first_end)
    second_code = read_file_lines(second_path, second_start, second_end)

    return f"""### Duplicate: {lines} lines
**File A**: `{first_path}` (lines {first_start}-{first_end})
```csharp
{first_code}
```

**File B**: `{second_path}` (lines {second_start}-{second_end})
```csharp
{second_code}
```
"""


def analyze_with_llm(findings_context: str, model: str) -> str:
    """Call GitHub Models API to analyze the findings."""
    from openai import OpenAI

    client = OpenAI(
        base_url="https://models.inference.ai.azure.com",
        api_key=os.environ["GITHUB_TOKEN"],
    )

    system_prompt = """You are a senior .NET developer analyzing code duplication in the MSTest/Microsoft.Testing.Platform repository.

For each duplicate finding, provide:
1. **Classification**: One of:
   - "Extract Method" — identical logic → shared helper
   - "Extract Base Class" — duplicated across classes with shared behavior
   - "Template Method" — same structure, minor variations → parameterize
   - "Intentional" — polyfills, cross-project isolation, or design choice
2. **Priority**: High / Medium / Low based on:
   - Lines duplicated (more = higher)
   - Whether it's in production code vs infrastructure
   - Whether extraction would reduce maintenance burden
3. **Suggested approach**: 1-2 sentences on how to refactor (or why to leave it)
4. **Risk**: Low / Medium / High — considers breaking changes, cross-project dependencies

IMPORTANT: Files under `src/Polyfills/` are intentionally duplicated across projects (they're compiled into each assembly). Mark those as "Intentional".
Files that are shared helpers duplicated across `MSTest.Analyzers` and `MSTest.SourceGeneration` are strong candidates since those projects could share code via a common project.

Output a markdown table with columns: Priority, File A, File B, Lines, Classification, Approach, Risk.
Then add a "Summary" section with overall statistics and the top 3 recommended refactorings to tackle first."""

    response = client.chat.completions.create(
        model=model,
        messages=[
            {"role": "system", "content": system_prompt},
            {
                "role": "user",
                "content": f"Analyze these duplicate code findings:\n\n{findings_context}",
            },
        ],
        temperature=0.2,
        max_tokens=4096,
    )

    return response.choices[0].message.content


def create_or_update_issue(title: str, body: str) -> None:
    """Create or update a GitHub issue with the analysis."""
    repo = os.environ.get("GITHUB_REPOSITORY", "")
    if not repo:
        print("GITHUB_REPOSITORY not set, skipping issue creation")
        return

    # Search for existing open issue with our title
    result = subprocess.run(
        ["gh", "issue", "list", "--repo", repo, "--state", "open", "--search", title, "--json", "number,title"],
        capture_output=True,
        text=True,
    )

    existing_issues = json.loads(result.stdout) if result.returncode == 0 else []
    matching = [i for i in existing_issues if i["title"] == title]

    if matching:
        issue_number = matching[0]["number"]
        subprocess.run(
            ["gh", "issue", "edit", str(issue_number), "--repo", repo, "--body", body],
            check=True,
        )
        print(f"Updated existing issue #{issue_number}")
    else:
        result = subprocess.run(
            ["gh", "issue", "create", "--repo", repo, "--title", title, "--body", body, "--label", "tech-debt"],
            capture_output=True,
            text=True,
        )
        if result.returncode == 0:
            print(f"Created new issue: {result.stdout.strip()}")
        else:
            # Label might not exist, retry without it
            subprocess.run(
                ["gh", "issue", "create", "--repo", repo, "--title", title, "--body", body],
                check=True,
            )


def main() -> None:
    report_path = "artifacts/jscpd/jscpd-report.json"
    if not Path(report_path).exists():
        print(f"Report not found at {report_path}")
        sys.exit(1)

    top_n = int(os.environ.get("TOP_N", "20"))
    model = os.environ.get("MODEL", "openai/gpt-4.1-mini")

    print(f"Loading report from {report_path}")
    report = load_report(report_path)

    total = report.get("statistics", {}).get("total", report.get("total", {}))
    total_clones = total.get("clones", 0)
    total_lines = total.get("duplicatedLines", 0)
    percentage = total.get("percentage", 0)
    sources = total.get("sources", 0)

    print(f"Found {total_clones} clones across {sources} files ({percentage}% duplication)")

    findings = extract_top_findings(report, top_n)
    if not findings:
        print("No duplicates found")
        sys.exit(0)

    print(f"Analyzing top {len(findings)} findings with {model}...")

    # Build context for each finding
    findings_context = "\n\n".join(build_finding_context(f) for f in findings)

    # Call LLM for analysis
    analysis = analyze_with_llm(findings_context, model)

    # Build full report
    report_md = f"""# Code Duplication Analysis

> Auto-generated by the [dedup-analysis workflow](../workflows/dedup-analysis.yml)

## Overview

| Metric | Value |
|--------|-------|
| Total clones | {total_clones} |
| Duplicated lines | {total_lines} |
| Duplication % | {percentage}% |
| Source files scanned | {sources} |
| Findings analyzed | {len(findings)} |

## Analysis

{analysis}

---

<sub>Generated by `dedup-analysis` workflow using jscpd + GitHub Models ({model})</sub>
"""

    # Save report
    output_path = "artifacts/jscpd/analysis-report.md"
    Path(output_path).write_text(report_md, encoding="utf-8")
    print(f"Report saved to {output_path}")

    # Create/update GitHub issue
    create_or_update_issue(
        title="[Tech Debt] Code Duplication Analysis",
        body=report_md,
    )


if __name__ == "__main__":
    main()
