<#
.SYNOPSIS
    Guard rail: forbid new `init` accessors on public API.

.DESCRIPTION
    Repository policy (`.github/copilot-instructions.md` and overarching principle
    #2 in `.github/agents/expert-reviewer.agent.md`):

        Public API for MSTest and Microsoft.Testing.Platform MUST NOT use
        `init` accessors. Existing MTP `init` accessors are grandfathered;
        no new ones may be introduced.

    This script scans the PR diff (or a supplied diff file) for *additions* to
    any `PublicAPI.Unshipped.txt` file whose right-hand side is `.init -> void`,
    which is how the public-API analyzer records a property setter with the
    `init` accessor. Additions to `PublicAPI.Shipped.txt` are ignored — those
    are the grandfathered set.

.PARAMETER Base
    Base ref to diff against. Defaults to $env:BASE_SHA or 'origin/main'.

.PARAMETER DiffFile
    Read the unified diff from this file instead of running `git diff`.
    Useful when running locally against `git diff -U0 main...HEAD > pr.diff`.

.EXAMPLE
    pwsh .github/scripts/check-public-api-init.ps1

.EXAMPLE
    pwsh .github/scripts/check-public-api-init.ps1 -Base origin/main

.EXAMPLE
    pwsh .github/scripts/check-public-api-init.ps1 -DiffFile pr.diff

.NOTES
    Exit codes:
        0 — no violations
        1 — at least one new `.init -> void` line was added
        2 — usage / IO error
#>
[CmdletBinding()]
param(
    [string]$Base = $(if ($env:BASE_SHA) { $env:BASE_SHA } else { 'origin/main' }),
    [string]$DiffFile
)

$ErrorActionPreference = 'Stop'

$InitLine = [regex]'\.init\s*->\s*void\s*$'
$UnshippedBaseName = 'PublicAPI.Unshipped.txt'

function Get-DiffFromGit {
    param([string]$BaseRef)

    # --no-color and --unified=0 give us only added/removed lines with no
    # context and no ANSI escapes that would confuse the parser.
    $output = & git diff --no-color --unified=0 "$BaseRef...HEAD" 2>&1
    if ($LASTEXITCODE -ne 0) {
        [Console]::Error.WriteLine("git diff failed (exit $LASTEXITCODE): $output")
        exit 2
    }

    return $output
}

function Get-Violations {
    param([string[]]$DiffLines)

    $violations = [System.Collections.Generic.List[object]]::new()
    $currentFile = $null
    $inUnshipped = $false

    foreach ($raw in $DiffLines) {
        if ($raw.StartsWith('diff --git ')) {
            $currentFile = $null
            $inUnshipped = $false
            continue
        }

        if ($raw.StartsWith('+++ ')) {
            # `+++ b/path/to/file` — strip the `b/` prefix
            $path = $raw.Substring(4).Trim()
            if ($path -eq '/dev/null') {
                $currentFile = $null
                $inUnshipped = $false
                continue
            }
            if ($path.StartsWith('b/')) {
                $path = $path.Substring(2)
            }
            $currentFile = $path
            $inUnshipped = ([System.IO.Path]::GetFileName($path) -eq $UnshippedBaseName)
            continue
        }

        if (-not $inUnshipped -or $null -eq $currentFile) {
            continue
        }

        # Added lines start with a single '+' but not '+++'.
        if ($raw.StartsWith('+') -and -not $raw.StartsWith('+++')) {
            $added = $raw.Substring(1)
            $stripped = $added.Trim()

            # Public-API entries are one symbol per line. Comments (`#`) and
            # the *REMOVED* / *NULLABILITY* sentinel lines are not API.
            if (-not $stripped -or $stripped.StartsWith('#') -or $stripped.StartsWith('*')) {
                continue
            }

            if ($InitLine.IsMatch($stripped)) {
                $violations.Add([pscustomobject]@{ File = $currentFile; Line = $stripped })
            }
        }
    }

    return , $violations
}

function Format-Report {
    param($Violations)

    $lines = @(
        '❌ Public-API policy violation: new `init` accessors detected.',
        '',
        'Repository policy (`.github/copilot-instructions.md`) forbids `init`',
        'accessors on **new** public API for MSTest and Microsoft.Testing.Platform.',
        'Existing entries in `PublicAPI.Shipped.txt` are grandfathered, but every',
        'new line added to `PublicAPI.Unshipped.txt` must use a regular setter.',
        '',
        'Offending additions:',
        ''
    )

    foreach ($v in $Violations) {
        $lines += "  - ``$($v.File)`` → ``$($v.Line)``"
    }

    $lines += @(
        '',
        'To fix: change the property to use a regular `set` accessor',
        '(or make the setter `internal` / drop the setter entirely) and',
        'regenerate the `PublicAPI.Unshipped.txt` entry.'
    )

    return ($lines -join "`n")
}

function Write-StepSummary {
    param([string]$Text)

    $summaryPath = $env:GITHUB_STEP_SUMMARY
    if (-not $summaryPath) {
        return
    }

    try {
        Add-Content -LiteralPath $summaryPath -Value $Text -Encoding utf8
        Add-Content -LiteralPath $summaryPath -Value '' -Encoding utf8
    }
    catch {
        [Console]::Error.WriteLine("Could not write GITHUB_STEP_SUMMARY: $_")
    }
}

# --- main ---

if ($PSBoundParameters.ContainsKey('DiffFile') -and $DiffFile) {
    try {
        $diffText = Get-Content -LiteralPath $DiffFile -Raw -Encoding utf8
    }
    catch {
        [Console]::Error.WriteLine("Could not read diff file ${DiffFile}: $_")
        exit 2
    }
}
else {
    $diffText = Get-DiffFromGit -BaseRef $Base
}

$diffLines = if ($diffText) { $diffText -split "`r?`n" } else { @() }
$violations = Get-Violations -DiffLines $diffLines

if ($violations.Count -eq 0) {
    Write-Output '✅ No new `init` accessors detected in PublicAPI.Unshipped.txt additions.'
    exit 0
}

$report = Format-Report -Violations $violations
Write-Output $report
Write-StepSummary -Text $report
exit 1
