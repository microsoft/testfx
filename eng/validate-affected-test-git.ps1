[CmdletBinding()]
param(
    [string]$TemplatePath = (Join-Path $PSScriptRoot "pipelines/steps/test-affected-tests.yml")
)

$ErrorActionPreference = "Stop"
$template = Get-Content -LiteralPath $TemplatePath -Raw
$body = [regex]::Match($template, '(?ms)^- pwsh: \|\r?\n(?<body>.*?)(?=^  \$\{\{)').Groups["body"].Value
if ([string]::IsNullOrWhiteSpace($body)) {
    throw "The shared affected-test PowerShell body could not be found."
}
$body = $body -replace '(?m)^    ', ""
$fixture = Join-Path ([System.IO.Path]::GetTempPath()) ("testfx-affected-git-" + [guid]::NewGuid().ToString("N"))
$null = New-Item -ItemType Directory -Path $fixture
$hooks = Join-Path $fixture "empty-hooks"
$null = New-Item -ItemType Directory -Path $hooks
$passed = 0
$primaryError = $null

function Invoke-FixtureGit {
    param([string[]]$Arguments)

    $output = & git -c "user.name=TestFx Git fixture" -c "user.email=testfx@example.invalid" `
        -c "commit.gpgsign=false" -c "core.hooksPath=$hooks" @Arguments 2>&1
    if ($LASTEXITCODE -ne 0) {
        throw "Fixture git command failed ($LASTEXITCODE): $output"
    }

    return ($output -join "`n").Trim()
}

function Invoke-AffectedStep {
    param(
        [string]$Repository,
        [int]$DotnetExit = 0
    )

    $script = @'
function dotnet {
    Write-Output "AFFECTED_GIT_PROBE_DOTNET"
    $global:LASTEXITCODE = [int]$env:TESTFX_AFFECTED_GIT_DOTNET_EXIT
}
'@ + [Environment]::NewLine + $body.Replace('${{ parameters.affectedTestsMode }}', "run").
        Replace('${{ parameters.solution }}', "TestFx.slnx").
        Replace('$(Build.SourcesDirectory)', $Repository).
        Replace('$(_BuildConfig)', "Release")
    $scriptPath = Join-Path $fixture ("step-" + [guid]::NewGuid().ToString("N") + ".ps1")
    [System.IO.File]::WriteAllText($scriptPath, $script)
    $startInfo = [System.Diagnostics.ProcessStartInfo]::new((Get-Process -Id $PID).Path)
    $startInfo.WorkingDirectory = $Repository
    $startInfo.UseShellExecute = $false
    $startInfo.RedirectStandardOutput = $true
    $startInfo.RedirectStandardError = $true
    foreach ($argument in @("-NoProfile", "-File", $scriptPath)) {
        $startInfo.ArgumentList.Add($argument)
    }
    $startInfo.Environment["TESTFX_AFFECTED_GIT_DOTNET_EXIT"] = "$DotnetExit"
    $startInfo.Environment["GIT_TERMINAL_PROMPT"] = "0"
    $process = [System.Diagnostics.Process]::Start($startInfo)
    try {
        $output = $process.StandardOutput.ReadToEndAsync()
        $errorOutput = $process.StandardError.ReadToEndAsync()
        # The local Git fixture has no network work; allow loaded CI agents time to start PowerShell.
        $completed = $process.WaitForExit(60000)
        if (-not $completed) {
            $process.Kill($true)
            $process.WaitForExit()
        }
        $standardOutput = $output.GetAwaiter().GetResult()
        $standardError = $errorOutput.GetAwaiter().GetResult()
        if (-not $completed) {
            throw "Affected-test Git fixture did not terminate: $standardOutput $standardError"
        }

        return [pscustomobject]@{
            ExitCode = $process.ExitCode
            Output = $standardOutput
            ErrorOutput = $standardError
        }
    } finally {
        $process.Dispose()
    }
}

function Assert-StepResult {
    param(
        [string]$Name,
        $Result,
        [int]$ExitCode = 0,
        [bool]$RunsDotnet = $true,
        [bool]$Succeeded = $true
    )

    if ($Result.ExitCode -ne $ExitCode -or
        $Result.Output.Contains("AFFECTED_GIT_PROBE_DOTNET") -ne $RunsDotnet -or
        $Result.Output.Contains("##vso[task.setvariable variable=AffectedTestsSucceeded]true") -ne $Succeeded) {
        throw "$Name`: unexpected exit or selection/fallback state: $($Result | ConvertTo-Json -Compress)"
    }
}

try {
    $source = Join-Path $fixture "source with spaces"
    $null = Invoke-FixtureGit @("init", "--quiet", "--initial-branch=main", $source)
    [System.IO.File]::WriteAllText((Join-Path $source "fixture.txt"), "original")
    $null = Invoke-FixtureGit @("-C", $source, "add", "fixture.txt")
    $trailer = "Co-authored-by: Copilot App <223556219+Copilot@users.noreply.github.com>"
    $null = Invoke-FixtureGit @("-C", $source, "commit", "--quiet", "-m", "Fixture baseline", "-m", $trailer)
    $null = Invoke-FixtureGit @("-C", $source, "switch", "--quiet", "--create", "change")
    $null = Invoke-FixtureGit @("-C", $source, "commit", "--quiet", "--allow-empty", "-m", "Fixture change", "-m", $trailer)
    $null = Invoke-FixtureGit @("-C", $source, "switch", "--quiet", "main")
    $null = Invoke-FixtureGit @("-C", $source, "commit", "--quiet", "--allow-empty", "-m", "Fixture target", "-m", $trailer)
    $baseline = Invoke-FixtureGit @("-C", $source, "rev-parse", "HEAD")
    $null = Invoke-FixtureGit @("-C", $source, "switch", "--quiet", "--create", "pr")
    $null = Invoke-FixtureGit @("-C", $source, "merge", "--quiet", "--no-ff", "change", "-m", "Fixture PR merge", "-m", $trailer)
    $sourceUri = ([uri]($source + [System.IO.Path]::DirectorySeparatorChar)).AbsoluteUri

    $shallow = Join-Path $fixture "shallow checkout"
    $null = Invoke-FixtureGit @("clone", "--quiet", "--depth=1", "--single-branch", "--branch", "pr", $sourceUri, $shallow)
    $null = Invoke-FixtureGit @("-C", $shallow, "switch", "--quiet", "--detach")
    [System.IO.File]::WriteAllText((Join-Path $shallow "fixture.txt"), "uncommitted")
    [System.IO.File]::WriteAllText((Join-Path $shallow "untracked.txt"), "untracked")
    $head = Invoke-FixtureGit @("-C", $shallow, "rev-parse", "HEAD")
    $status = Invoke-FixtureGit @("-C", $shallow, "status", "--porcelain")
    $result = Invoke-AffectedStep -Repository $shallow
    Assert-StepResult -Name "AffectedGitHistoryRepairsShallowMergeCheckout" -Result $result
    $remoteMain = Invoke-FixtureGit @("-C", $shallow, "for-each-ref", "--format=%(objectname)", "refs/remotes/origin/main")
    if ($remoteMain -ne $baseline) {
        throw "AffectedGitHistoryRepairsShallowMergeCheckout: expected main '$baseline', got '$remoteMain'."
    }
    $common = Invoke-FixtureGit @("-C", $shallow, "merge-base", "HEAD", "refs/remotes/origin/main")
    if ($remoteMain -ne $baseline -or $common -ne $baseline -or
        (Invoke-FixtureGit @("-C", $shallow, "rev-parse", "--is-shallow-repository")) -ne "false") {
        throw "AffectedGitHistoryRepairsShallowMergeCheckout: main and complete merge ancestry were not prepared."
    }
    Write-Output "PASS AffectedGitHistoryRepairsShallowMergeCheckout"
    $passed++
    if ((Invoke-FixtureGit @("-C", $shallow, "rev-parse", "HEAD")) -ne $head -or
        (Invoke-FixtureGit @("-C", $shallow, "status", "--porcelain")) -cne $status) {
        throw "AffectedGitHistoryPreservesCheckout: history preparation changed HEAD or user work."
    }
    Write-Output "PASS AffectedGitHistoryPreservesCheckout"
    $passed++

    $result = Invoke-AffectedStep -Repository $shallow
    Assert-StepResult -Name "AffectedGitHistoryPreparationIsIdempotent" -Result $result
    Write-Output "PASS AffectedGitHistoryPreparationIsIdempotent"
    $passed++

    $complete = Join-Path $fixture "complete checkout"
    $null = Invoke-FixtureGit @("clone", "--quiet", "--single-branch", "--branch", "pr", $sourceUri, $complete)
    $result = Invoke-AffectedStep -Repository $complete
    Assert-StepResult -Name "AffectedGitHistoryHandlesFullCheckout" -Result $result
    if ((Invoke-FixtureGit @("-C", $complete, "merge-base", "HEAD", "refs/remotes/origin/main")) -ne $baseline) {
        throw "AffectedGitHistoryHandlesFullCheckout: main was not fetched into the complete checkout."
    }
    Write-Output "PASS AffectedGitHistoryHandlesFullCheckout"
    $passed++

    foreach ($case in @(
        @{ Name = "AffectedGitHistoryPreservesTestFailureExit"; Exit = 2; Expected = 2 },
        @{ Name = "AffectedGitHistoryPreservesNoTestsExit"; Exit = 8; Expected = 8 },
        @{ Name = "AffectedGitHistoryPreservesInfrastructureFallback"; Exit = 1; Expected = 0 }
    )) {
        $result = Invoke-AffectedStep -Repository $complete -DotnetExit $case.Exit
        Assert-StepResult -Name $case.Name -Result $result -ExitCode $case.Expected -Succeeded $false
        Write-Output "PASS $($case.Name)"
        $passed++
    }

    $null = Invoke-FixtureGit @("-C", $complete, "remote", "set-url", "origin", (Join-Path $fixture "missing remote"))
    $result = Invoke-AffectedStep -Repository $complete
    Assert-StepResult -Name "AffectedGitHistoryMissingRemoteFallsBack" -Result $result -RunsDotnet $false -Succeeded $false
    if (-not $result.Output.Contains("##vso[task.logissue type=warning]") -or
        -not $result.Output.Contains("running the full test suite")) {
        throw "AffectedGitHistoryMissingRemoteFallsBack: baseline failure must visibly request full testing."
    }
    Write-Output "PASS AffectedGitHistoryMissingRemoteFallsBack"
    $passed++

    $unrelated = Join-Path $fixture "unrelated source"
    $null = Invoke-FixtureGit @("init", "--quiet", "--initial-branch=main", $unrelated)
    $null = Invoke-FixtureGit @("-C", $unrelated, "commit", "--quiet", "--allow-empty", "-m", "Unrelated baseline", "-m", $trailer)
    $null = Invoke-FixtureGit @("-C", $complete, "remote", "set-url", "origin", $unrelated)
    $result = Invoke-AffectedStep -Repository $complete
    Assert-StepResult -Name "AffectedGitHistoryUnrelatedBaselineFallsBack" -Result $result -RunsDotnet $false -Succeeded $false
    if (-not $result.Output.Contains("##vso[task.logissue type=warning]") -or
        -not $result.Output.Contains("running the full test suite")) {
        throw "AffectedGitHistoryUnrelatedBaselineFallsBack: unrelated history must visibly request full testing."
    }
    Write-Output "PASS AffectedGitHistoryUnrelatedBaselineFallsBack"
    $passed++

    Write-Output "Affected-test Git self-tests: $passed passed."
} catch {
    $primaryError = $_
    throw
} finally {
    try {
        Get-ChildItem -LiteralPath $fixture -Recurse -Force -File | ForEach-Object { $_.IsReadOnly = $false }
        Remove-Item -LiteralPath $fixture -Recurse -Force
    } catch {
        if ($null -eq $primaryError) {
            throw
        }
        Write-Warning "Additional cleanup failure for '$fixture': $_"
    }
}
