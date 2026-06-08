<#
.SYNOPSIS
    Guard rail: forbid manual `.xlf` edits in human-authored PRs.

.DESCRIPTION
    Repository policy (`.github/copilot-instructions.md` → Localization Guidelines):

        Add a corresponding entry in the resource file (.resx).
        NEVER manually modify *.xlf files. Instead, regenerate them by running
        `dotnet msbuild <project>.csproj /t:UpdateXlf` on the owning project.

    The OneLocBuild bot owns the bulk update path. A human PR may legitimately
    touch `.xlf` files only when those changes are the **output of**
    `dotnet msbuild /t:UpdateXlf` after a `.resx` edit — in that case the same PR
    will also contain a `.resx` change. A PR that hand-edits a `.xlf` without any
    `.resx` change is the defect this guard catches.

    This script focuses solely on diff analysis: given a list of changed paths,
    flag every `.xlf` with no matching `.resx` in the same diff. Exempting bot
    PRs (OneLocBuild, Maestro, etc.) is handled by the calling workflow via a
    job-level `if:`, not here — see `.github/workflows/xlf-manual-edit-guard.yml`.

    Exit codes:
        0 — no `.xlf` changes, OR every changed `.xlf` has a matching `.resx`
            change in the same PR.
        1 — a `.xlf` file was modified without a corresponding `.resx` change.
        2 — usage / IO error.

.PARAMETER Base
    Git ref to diff against. Defaults to $env:BASE_REF, then $env:BASE_SHA
    (for backward compatibility), then 'origin/main'.

.PARAMETER DiffFile
    Read the diff (unified or name-only) from this file instead of running
    `git diff`.

.EXAMPLE
    pwsh .github/scripts/check-xlf-manual-edit.ps1

.EXAMPLE
    pwsh .github/scripts/check-xlf-manual-edit.ps1 -Base origin/main

.EXAMPLE
    pwsh .github/scripts/check-xlf-manual-edit.ps1 -DiffFile pr.diff
#>
[CmdletBinding()]
param(
    [string]$Base,
    [string]$DiffFile
)

$ErrorActionPreference = 'Stop'

if (-not $Base) {
    $Base = if ($env:BASE_REF) { $env:BASE_REF }
            elseif ($env:BASE_SHA) { $env:BASE_SHA }
            else { 'origin/main' }
}

# Matches the trailing `.<locale>.xlf` suffix (e.g. `.de.xlf`, `.zh-Hans.xlf`,
# `.cs.xlf`). Used to strip the locale segment when computing the resx basename.
$XlfLocale = [regex]'\.([a-zA-Z]{2,3}(?:-[A-Za-z0-9]+)*)\.xlf$'

function Get-DiffFromGit {
    param([string]$BaseRef)

    $output = & git diff --no-color --name-only "$BaseRef...HEAD" 2>&1
    if ($LASTEXITCODE -ne 0) {
        [Console]::Error.WriteLine("git diff failed (exit $LASTEXITCODE): $output")
        exit 2
    }

    return $output
}

function Get-ChangedPaths {
    param([string[]]$DiffLines)

    $paths = [System.Collections.Generic.List[string]]::new()

    foreach ($raw in $DiffLines) {
        $line = $raw.Trim()
        if (-not $line) { continue }

        if ($line.StartsWith('diff --git ')) {
            $idx = $line.IndexOf(' b/')
            if ($idx -ge 0) {
                $paths.Add($line.Substring($idx + 3).Trim())
            }
            continue
        }
        if ($line.StartsWith('+++ ')) {
            $path = $line.Substring(4).Trim()
            if ($path -eq '/dev/null') { continue }
            if ($path.StartsWith('b/')) { $path = $path.Substring(2) }
            $paths.Add($path)
            continue
        }
        if ($line.StartsWith('---') -or $line.StartsWith('@@') -or
            $line.StartsWith('+') -or $line.StartsWith('-')) {
            continue
        }

        if ($line.Contains('/') -or
            $line.EndsWith('.xlf') -or $line.EndsWith('.resx') -or
            $line.EndsWith('.cs')) {
            $paths.Add($line)
        }
    }

    # De-duplicate while preserving order.
    $seen = [System.Collections.Generic.HashSet[string]]::new()
    $out = [System.Collections.Generic.List[string]]::new()
    foreach ($p in $paths) {
        if ($seen.Add($p)) {
            $out.Add($p)
        }
    }
    return , $out
}

function Get-XlfResxBasename {
    <#
    .DESCRIPTION
        Return the bare resource basename for a `.xlf` file.

        `path/to/Strings.de.xlf` → `Strings`
        `Resources/xlf/Resource.cs.xlf` → `Resource`
        `Strings.xlf` (no-locale variant) → `Strings`

        Basename matching is used instead of strict path matching because testfx
        places `.xlf` files under a `xlf/` subdirectory while the matching
        `.resx` lives in the parent directory (e.g.
        `Resources/Resource.resx` ↔ `Resources/xlf/Resource.cs.xlf`), and other
        projects may use yet other layouts. A basename match is permissive
        enough to handle every layout without false positives, since the only
        way it can miss a hand-edit is if the PR also happens to change a
        completely unrelated `.resx` with the same basename — extremely unlikely
        in practice.
    #>
    param([string]$XlfPath)

    $name = [System.IO.Path]::GetFileName(($XlfPath -replace '\\', '/'))
    $m = $XlfLocale.Match($name)
    if ($m.Success) {
        return $name.Substring(0, $m.Index)
    }
    return [System.IO.Path]::GetFileNameWithoutExtension($name)
}

function Find-Violations {
    param([System.Collections.Generic.IEnumerable[string]]$Paths)

    $normPaths = foreach ($p in $Paths) { $p -replace '\\', '/' }

    $resxBasenames = [System.Collections.Generic.HashSet[string]]::new()
    foreach ($p in $normPaths) {
        if ($p.EndsWith('.resx')) {
            $null = $resxBasenames.Add([System.IO.Path]::GetFileNameWithoutExtension($p))
        }
    }

    $violations = [System.Collections.Generic.List[object]]::new()
    foreach ($p in $normPaths) {
        if (-not $p.EndsWith('.xlf')) { continue }
        $base = Get-XlfResxBasename -XlfPath $p
        if (-not $resxBasenames.Contains($base)) {
            $violations.Add([pscustomobject]@{ Xlf = $p; ExpectedResx = "$base.resx" })
        }
    }

    return , $violations
}

function Format-Report {
    param($Violations)

    $lines = @(
        '❌ Localization policy violation: manual `.xlf` edits detected.',
        '',
        'Repository policy (`.github/copilot-instructions.md` → Localization',
        'Guidelines):',
        '',
        '  > NEVER manually modify `*.xlf` files. Instead, regenerate them by',
        '  > running `dotnet msbuild <project>.csproj /t:UpdateXlf` on the',
        '  > owning project.',
        '',
        'The following `.xlf` files were modified without a corresponding',
        '`.resx` change in the same PR. If this PR is a translation update,',
        'it should go through the OneLocBuild bot path; if it is a source-string',
        'update, edit the `.resx` and re-run `/t:UpdateXlf`.',
        ''
    )

    foreach ($v in $Violations) {
        $lines += "  - ``$($v.Xlf)`` (expected a matching ``$($v.ExpectedResx)`` change in this PR)"
    }

    $lines += @(
        '',
        'How to fix:',
        '',
        '  1. Edit the `.resx` source string.',
        '  2. Run:',
        '       dotnet msbuild <owning-project>.csproj /t:UpdateXlf',
        '  3. Commit both the `.resx` and the regenerated `.xlf` together.'
    )

    return ($lines -join "`n")
}

function Write-StepSummary {
    param([string]$Text)

    $summaryPath = $env:GITHUB_STEP_SUMMARY
    if (-not $summaryPath) { return }

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
$paths = Get-ChangedPaths -DiffLines $diffLines
$violations = Find-Violations -Paths $paths

if ($violations.Count -eq 0) {
    Write-Output '✅ No manual `.xlf` edits detected.'
    exit 0
}

$report = Format-Report -Violations $violations
Write-Output $report
Write-StepSummary -Text $report
exit 1
