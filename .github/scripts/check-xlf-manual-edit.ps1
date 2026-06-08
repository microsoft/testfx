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

# Tight regex for repo-relative path safety: only the character classes that
# actually appear in tracked file paths in this repo (alnum, dot, underscore,
# slash, hyphen, plus). Spaces, quotes, tabs, control chars, and any non-ASCII
# punctuation are rejected, so a unified-diff context line ending in `.xlf`
# (a docstring, a code comment, etc.) cannot be mistaken for a path.
$PathSafe = [regex]'^[A-Za-z0-9._/+\-]+$'

function Get-ChangedPaths {
    param([string[]]$DiffLines)

    $paths = [System.Collections.Generic.List[string]]::new()

    foreach ($raw in $DiffLines) {
        if (-not $raw) { continue }

        # Unified-diff headers: paths are unambiguous, accept after sanity check.
        $trimmed = $raw.Trim()
        if ($trimmed.StartsWith('diff --git ')) {
            $idx = $trimmed.IndexOf(' b/')
            if ($idx -ge 0) {
                $p = $trimmed.Substring($idx + 3).Trim()
                if ($PathSafe.IsMatch($p)) { $paths.Add($p) }
            }
            continue
        }
        if ($trimmed.StartsWith('+++ ')) {
            $p = $trimmed.Substring(4).Trim()
            if ($p -eq '/dev/null') { continue }
            if ($p.StartsWith('b/')) { $p = $p.Substring(2) }
            if ($PathSafe.IsMatch($p)) { $paths.Add($p) }
            continue
        }

        # Hunk headers and content/context lines: identified by the RAW prefix
        # (untrimmed!) so we do not accidentally promote a context line like
        # `    "path/to/something.xlf"` (whose trimmed form starts with `"`)
        # — or `+something/elsewhere` — into a path.
        if ($raw.StartsWith('---') -or $raw.StartsWith('@@') -or
            $raw.StartsWith('+') -or $raw.StartsWith('-') -or
            $raw.StartsWith(' ') -or $raw.StartsWith("`t")) {
            continue
        }

        # Otherwise treat as a name-only entry. Require the line to (a) match
        # the path-safe alphabet (no spaces, quotes, etc.) AND (b) actually
        # look like a path of interest (has a slash or one of our extensions).
        if ($PathSafe.IsMatch($trimmed) -and
            ($trimmed.Contains('/') -or $trimmed.EndsWith('.xlf') -or $trimmed.EndsWith('.resx'))) {
            $paths.Add($trimmed)
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

function Get-XlfBasename {
    <#
    .DESCRIPTION
        Strip the `.<locale>.xlf` suffix (or fall back to bare stem) from a
        `.xlf` filename.

            Resource.zh-Hans.xlf → Resource
            Strings.xlf          → Strings
    #>
    param([string]$XlfFileName)

    $m = $XlfLocale.Match($XlfFileName)
    if ($m.Success) {
        return $XlfFileName.Substring(0, $m.Index)
    }
    return [System.IO.Path]::GetFileNameWithoutExtension($XlfFileName)
}

function Get-XlfExpectedResx {
    <#
    .DESCRIPTION
        Return the expected `.resx` path for a given `.xlf` path, using the
        repo-standard layout convention:

            <dir>/Resources/xlf/<Base>.<locale>.xlf
                  ↳ <dir>/Resources/<Base>.resx

        Returns $null when the file is NOT directly under a `xlf/` directory;
        the caller then falls back to a basename match.

        Strict path-based matching is essential because some `.resx` basenames
        (e.g. `ExtensionResources.resx`) repeat across projects in this repo,
        and a basename-only match would let a PR hand-edit one project's
        `.xlf` while touching an unrelated other project's `.resx`.
    #>
    param([string]$XlfPath)

    $normalized = $XlfPath -replace '\\', '/'
    $name = [System.IO.Path]::GetFileName($normalized)
    $base = Get-XlfBasename -XlfFileName $name

    $parts = $normalized -split '/'
    if ($parts.Length -ge 2 -and $parts[-2] -eq 'xlf') {
        $parentParts = $parts[0..($parts.Length - 3)]
        if ($parentParts.Length -gt 0) {
            return ($parentParts -join '/') + "/$base.resx"
        }
        return "$base.resx"
    }

    return $null
}

function Find-Violations {
    param([System.Collections.Generic.IEnumerable[string]]$Paths)

    $normPaths = foreach ($p in $Paths) { $p -replace '\\', '/' }

    $resxFullPaths = [System.Collections.Generic.HashSet[string]]::new()
    $resxBasenames = [System.Collections.Generic.HashSet[string]]::new()
    foreach ($p in $normPaths) {
        if ($p.EndsWith('.resx')) {
            $null = $resxFullPaths.Add($p)
            $null = $resxBasenames.Add([System.IO.Path]::GetFileNameWithoutExtension($p))
        }
    }

    $violations = [System.Collections.Generic.List[object]]::new()
    foreach ($p in $normPaths) {
        if (-not $p.EndsWith('.xlf')) { continue }

        $expected = Get-XlfExpectedResx -XlfPath $p
        if ($expected) {
            # Standard `Resources/xlf/<Base>.*.xlf` layout: strict full-path match.
            if (-not $resxFullPaths.Contains($expected)) {
                $violations.Add([pscustomobject]@{ Xlf = $p; ExpectedResx = $expected })
            }
        }
        else {
            # Non-standard layout: best-effort basename fallback so the guard
            # still catches obvious hand-edits in any future unusual project.
            $base = Get-XlfBasename -XlfFileName ([System.IO.Path]::GetFileName($p))
            if (-not $resxBasenames.Contains($base)) {
                $violations.Add([pscustomobject]@{ Xlf = $p; ExpectedResx = "$base.resx" })
            }
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
