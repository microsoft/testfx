# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT license. See LICENSE file in the project root for full license information.

#requires -Version 7.2

[CmdletBinding()]
param([string] $EvidenceRoot)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$repo = (git rev-parse --show-toplevel).Trim()
if ($LASTEXITCODE -ne 0) { throw 'Run this check from the TestFx checkout.' }
$summaryScript = Join-Path $repo 'eng\bazel\summarize-dogfood.ps1'
$root = if ($EvidenceRoot) { $EvidenceRoot } else { Join-Path ([IO.Path]::GetTempPath()) ('dogfood-comparison-' + [guid]::NewGuid().ToString('N')) }
if (Test-Path -LiteralPath $root) { throw 'Use a new evidence directory.' }
New-Item -ItemType Directory -Path $root | Out-Null
$results = [Collections.Generic.List[object]]::new()
$pwsh = Join-Path $PSHOME 'pwsh.exe'
$revision = (git rev-parse HEAD).Trim()

function Assert-Equal($Expected, $Actual) {
    if ($Expected -cne $Actual) { throw "Expected <$Expected>, got <$Actual>." }
}

function Assert-Problem($Report, [string] $Text) {
    if (-not @($Report.Problems | Where-Object { $_.Contains($Text, [StringComparison]::Ordinal) }).Count) {
        throw "The intended guard did not report '$Text'. Actual: $($Report.Problems -join '; ')"
    }
}

function New-Fixture([string] $Name) {
    $directory = Join-Path $root $Name
    $records = [Collections.Generic.List[object]]::new()
    $index = 0
    foreach ($mode in @('MSBuild', 'Cold', 'BuildCache', 'Cached')) {
        $id = [guid]::NewGuid().ToString()
        $start = [DateTimeOffset]::Parse('2026-01-01T00:00:00Z').AddMinutes($index * 10)
        $duration = @(100, 90, 80, 40)[$index++]
        $finish = $start.AddSeconds($duration)
        $records.Add([ordered]@{
            id = $id; identifier = $mode; name = $mode; type = 'Job'
            state = 'completed'; result = 'succeeded'; parentId = $null
            startTime = $start.ToString('o'); finishTime = $finish.ToString('o')
        })
        foreach ($task in @(
            @{ name = 'Cache Bazel downloads'; start = $start.AddSeconds(1); finish = $start.AddSeconds(3) },
            @{ name = 'Post-job: Cache Bazel downloads'; start = $finish.AddSeconds(-4); finish = $finish.AddSeconds(-2) }
        )) {
            $records.Add([ordered]@{
                id = [guid]::NewGuid().ToString(); parentId = $id; name = $task.name; type = 'Task'
                state = 'completed'; result = 'succeeded'
                startTime = $task.start.ToString('o'); finishTime = $task.finish.ToString('o')
            })
        }
        $run = [ordered]@{
            Schema = 1; Mode = $mode
            Source = @{ Revision = $revision; PatchHash = 'D' * 64 }
            Scope = @{ Targets = @('//:fixture') }; ScopeHash = 'A' * 64; CacheKey = 'B' * 64
            Completed = $true; Success = $true; JobId = $id; JobName = $mode
            BuildSeconds = 10; TestSeconds = 20; HarnessSeconds = 35
            BuildEvents = $null; TestEvents = $null
            Tests = @(@{
                Label = '//:fixture'; TargetFramework = 'net10.0'; Runtime = '10.0.14'
                SelectionHash = '1' * 64; OutcomeHash = '2' * 64
                Total = 2; Executed = 2; Passed = 2; Failed = 0; Skipped = 0; Other = 0
                Cached = $mode -eq 'Cached'; ExitCode = 0
            })
        }
        $folder = Join-Path $directory "input\$mode"
        New-Item -ItemType Directory -Path $folder -Force | Out-Null
        $run | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath (Join-Path $folder 'result.json') -Encoding utf8
    }
    @{ records = $records } | ConvertTo-Json -Depth 10 |
        Set-Content -LiteralPath (Join-Path $directory 'timeline.json') -Encoding utf8
    return $directory
}

function Invoke-Comparison([string] $Directory, [int] $ExpectedExit) {
    & $pwsh -NoProfile -File $summaryScript -InputRoot (Join-Path $Directory 'input') `
        -OutputRoot (Join-Path $Directory 'output') -HistoryRoot (Join-Path $Directory 'history') `
        -TimelineFile (Join-Path $Directory 'timeline.json') -BuildId 42 -RequireControls *> (Join-Path $Directory 'execution.log')
    Assert-Equal $ExpectedExit $LASTEXITCODE
    Get-Content -LiteralPath (Join-Path $Directory 'output\comparison.json') -Raw | ConvertFrom-Json
}

function Test-Case([string] $Name, [scriptblock] $Body) {
    try {
        & $Body
        $results.Add([pscustomobject]@{ Name = $Name; Passed = $true; Error = $null })
        Write-Host "PASS $Name"
    }
    catch {
        $results.Add([pscustomobject]@{ Name = $Name; Passed = $false; Error = $_.Exception.Message })
        Write-Host "FAIL $Name`: $($_.Exception.Message)"
    }
}

Test-Case 'MatchedControlsMeasureNetJobSavingsAndKeepReuseSeparate' {
    $directory = New-Fixture 'matched'
    $report = Invoke-Comparison $directory 0
    Assert-Equal $true $report.Comparable
    Assert-Equal 4 @($report.Runs).Count
    $cached = @($report.Runs | Where-Object Mode -eq 'Cached')[0]
    Assert-Equal 40 $cached.JobSeconds
    Assert-Equal 2 $cached.CacheRestoreSeconds
    Assert-Equal 2 $cached.CacheSaveSeconds
    Assert-Equal 0 $cached.NewlyExecutedCases
    Assert-Equal 2 $cached.ReusedCases
    $saving = @($report.Savings | Where-Object Baseline -eq 'MSBuild')[0]
    Assert-Equal 60 $saving.Seconds
    Assert-Equal 60 $saving.Percent
    Assert-Equal 4 @($report.Trends).Count
    $trend = @($report.Trends | Where-Object Mode -eq 'Cached')[0]
    Assert-Equal 1 $trend.Samples
    Assert-Equal 1 $trend.DistinctRevisions
    Assert-Equal 40 $trend.MedianJobSeconds
    Assert-Equal 40 $trend.P95JobSeconds
}

Test-Case 'EqualCountsDoNotHideDifferentTestIdentities' {
    $directory = New-Fixture 'identity'
    $file = Join-Path $directory 'input\MSBuild\result.json'
    $data = Get-Content -LiteralPath $file -Raw | ConvertFrom-Json
    $data.Tests[0].SelectionHash = '3' * 64
    $data | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath $file -Encoding utf8
    $report = Invoke-Comparison $directory 1
    Assert-Equal $false $report.Comparable
    Assert-Equal 0 @($report.Savings).Count
    Assert-Problem $report 'test identities or outcomes.'
}

Test-Case 'MissingControlsCannotProduceASpeedup' {
    $directory = New-Fixture 'missing'
    Remove-Item -LiteralPath (Join-Path $directory 'input\MSBuild\result.json')
    $report = Invoke-Comparison $directory 1
    Assert-Equal $false $report.Complete
    Assert-Equal 0 @($report.Savings).Count
    Assert-Problem $report 'Missing MSBuild evidence.'
}

Test-Case 'ForcedExecutionControlRejectsCachedTests' {
    $directory = New-Fixture 'forced'
    $file = Join-Path $directory 'input\BuildCache\result.json'
    $data = Get-Content -LiteralPath $file -Raw | ConvertFrom-Json
    $data.Tests[0].Cached = $true
    $data | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath $file -Encoding utf8
    $report = Invoke-Comparison $directory 1
    Assert-Equal $false $report.Comparable
    Assert-Equal 0 @($report.Savings).Count
    Assert-Problem $report 'BuildCache unexpectedly reused test results.'
}

Test-Case 'IncompleteTimelineRemainsUnknownRatherThanZero' {
    $directory = New-Fixture 'timing'
    $file = Join-Path $directory 'timeline.json'
    $data = Get-Content -LiteralPath $file -Raw | ConvertFrom-Json
    $job = @($data.records | Where-Object { $_.type -eq 'Job' -and $_.name -eq 'Cached' })[0]
    $job.state = 'inProgress'
    $job.finishTime = $null
    $data | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath $file -Encoding utf8
    $report = Invoke-Comparison $directory 1
    Assert-Equal $false $report.Comparable
    $cached = @($report.Runs | Where-Object Mode -eq 'Cached')[0]
    Assert-Equal $null $cached.JobSeconds
    Assert-Equal 0 @($report.Savings).Count
    Assert-Problem $report 'Cached has incomplete Azure timing:'
}

[ordered]@{ Total = $results.Count; Passed = @($results | Where-Object Passed).Count; Tests = $results } |
    ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $root 'results.json') -Encoding utf8
Write-Host "Comparison evidence: $root"
if ($results.Count -ne 5 -or @($results | Where-Object { -not $_.Passed }).Count) { exit 1 }
exit 0
