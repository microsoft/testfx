<#
.SYNOPSIS
    Reminder when a PR touches an ICommandLineOptionsProvider implementation
    without updating the corresponding --help / --info acceptance-test
    expectations (per .github/copilot-instructions.md).

.DESCRIPTION
    This script does NOT decide whether the change actually affects the rendered
    help output — only a human (or the acceptance tests at CI time) can know that
    for sure. What it does is the cheap, deterministic part: flag every PR that
    changes a provider file without also touching any of the four expectation
    files, so the author and reviewer get an early, in-PR reminder.

    Exit codes:
        0  — non-blocking by design. Either no provider files changed, OR
             provider files changed and at least one help-expectation file
             is in the diff (contract plausibly satisfied), OR provider
             files changed without expectation-file changes (a refactor
             that doesn't touch help output is common; the reminder is
             surfaced via stdout + GITHUB_STEP_SUMMARY + a workflow
             `::notice` annotation but the workflow stays green).
        2  — usage / IO error.

    The "always exit 0" choice mirrors how the upstream policy reads: the
    acceptance tests are the authoritative gate. This script's job is to make
    the policy visible *before* a reviewer has to remember it.

.PARAMETER Base
    Git ref to diff against. Defaults to $env:BASE_REF, then $env:BASE_SHA
    (for backward compatibility), then 'origin/main'.

.PARAMETER DiffFile
    Read the diff (unified or name-only) from this file instead of running
    `git diff`.

.EXAMPLE
    pwsh .github/scripts/check-cli-help-drift.ps1

.EXAMPLE
    pwsh .github/scripts/check-cli-help-drift.ps1 -Base origin/main

.EXAMPLE
    pwsh .github/scripts/check-cli-help-drift.ps1 -DiffFile pr.diff
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

# Files that hold the `--help` / `--info` expectations enumerated in
# `.github/copilot-instructions.md` (CLI options guidelines section).
$ExpectationFiles = @(
    'test/IntegrationTests/Microsoft.Testing.Platform.Acceptance.IntegrationTests/HelpInfoTests.cs',
    'test/IntegrationTests/Microsoft.Testing.Platform.Acceptance.IntegrationTests/HelpInfoAllExtensionsTests.cs',
    'test/IntegrationTests/Microsoft.Testing.Platform.Acceptance.IntegrationTests/MSBuild.KnownExtensionRegistration.cs',
    'test/IntegrationTests/MSTest.Acceptance.IntegrationTests/HelpInfoTests.cs'
)

# File-name patterns that indicate a CLI-options provider has been touched.
# Names rather than paths so the check stays robust against folder moves.
# Patterns require the filename to END at `Provider.cs` so test files like
# `*CommandLineOptionsProviderTests.cs` do not trigger false positives.
$ProviderPatterns = @(
    [regex]::new('CommandLineOptionsProvider\.cs$', 'IgnoreCase'),
    [regex]::new('^PlatformCommandLineProvider\.cs$', 'IgnoreCase'),
    [regex]::new('^MSTestExtension\.cs$')
)

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

    # Accepts either a `git diff --name-only` listing or a unified diff.
    $paths = [System.Collections.Generic.List[string]]::new()

    foreach ($raw in $DiffLines) {
        $line = $raw.Trim()
        if (-not $line) { continue }

        if ($line.StartsWith('diff --git ')) {
            # `diff --git a/<path> b/<path>` — take the b-side.
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

        # name-only mode: a bare path per line.
        if ($line.Contains('/') -or $line.EndsWith('.cs')) {
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

function Split-Paths {
    param([System.Collections.Generic.IEnumerable[string]]$Paths)

    $providers = [System.Collections.Generic.List[string]]::new()
    $expectations = [System.Collections.Generic.List[string]]::new()

    foreach ($path in $Paths) {
        $norm = $path -replace '\\', '/'
        if ($ExpectationFiles -contains $norm) {
            $expectations.Add($norm)
            continue
        }
        $name = [System.IO.Path]::GetFileName($norm)
        foreach ($pattern in $ProviderPatterns) {
            if ($pattern.IsMatch($name)) {
                $providers.Add($norm)
                break
            }
        }
    }

    return [pscustomobject]@{
        Providers    = $providers
        Expectations = $expectations
    }
}

function Format-Reminder {
    param([System.Collections.Generic.IList[string]]$Providers)

    $lines = @(
        '⚠️ CLI help-text expectation reminder',
        '',
        'This PR changes one or more CLI-options provider files but does not',
        'touch any of the four `--help` / `--info` acceptance-test expectation',
        'files documented in `.github/copilot-instructions.md`:',
        ''
    )

    foreach ($f in ($ExpectationFiles | Sort-Object)) {
        $lines += "  - ``$f``"
    }

    $lines += @(
        '',
        'If this PR adds, renames, or changes the description/arguments of any',
        'CLI option, the matching `--help` and `--info` blocks in those files',
        'MUST be updated in the same change. If the provider edit is a pure',
        'refactor with no observable change to the rendered help output, you',
        'can ignore this reminder — the acceptance tests in CI are the final',
        'word.',
        '',
        'Changed provider files in this PR:',
        ''
    )

    foreach ($p in $Providers) {
        $lines += "  - ``$p``"
    }

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
$classified = Split-Paths -Paths $paths

if ($classified.Providers.Count -eq 0) {
    Write-Output '✅ No CLI-options provider files changed in this PR.'
    exit 0
}

if ($classified.Expectations.Count -gt 0) {
    Write-Output ('✅ CLI-options provider files were changed and at least one help/' +
        'info expectation file is in the diff — contract plausibly satisfied. ' +
        "Acceptance tests in CI are the final word.`n")
    Write-Output 'Changed providers:'
    foreach ($p in $classified.Providers) {
        Write-Output "  - $p"
    }
    Write-Output "`nChanged expectation files:"
    foreach ($e in $classified.Expectations) {
        Write-Output "  - $e"
    }
    exit 0
}

$reminder = Format-Reminder -Providers $classified.Providers
Write-Output $reminder
Write-StepSummary -Text $reminder

# Also emit a workflow `notice` annotation so it shows up in the PR
# Checks tab as a soft signal (yellow ⚠ icon) rather than a hard red X.
Write-Output ('::notice title=CLI help-text expectation reminder::' +
    'CLI-options provider files changed without touching any ' +
    'HelpInfo*.cs / MSBuild.KnownExtensionRegistration.cs. See ' +
    'the workflow summary for details.')

exit 0
