# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT license. See LICENSE file in the project root for full license information.

#requires -Version 7.2

[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string] $InputRoot,

    [Parameter(Mandatory)]
    [string] $OutputRoot,

    [string] $TimelineFile,
    [string] $HistoryRoot,
    [string] $CollectionUri,
    [string] $Project,
    [int] $BuildId,
    [switch] $RequireControls
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
Import-Module (Join-Path $PSScriptRoot 'dogfood-results.psm1') -Force
New-Item -ItemType Directory -Path $OutputRoot -Force | Out-Null
$problems = [Collections.Generic.List[string]]::new()
$rows = [Collections.Generic.List[object]]::new()
$runs = @()
$timeline = $null
$comparison = [ordered]@{
    Schema = 1; BuildId = $BuildId; CreatedUtc = [DateTimeOffset]::UtcNow.ToString('o')
    Complete = $false; Comparable = $false; Problems = $problems; Runs = $rows
    Savings = @(); Trends = @()
    Note = 'Unit-workload experiment only. Job time includes setup and cache transfer, not agent queue delay. Cached cases are not new executions.'
}

function Get-SelectionFingerprint($Run) {
    $values = [string[]]@($Run.Tests | ForEach-Object {
        "$($_.Label)|$($_.TargetFramework)|$($_.Runtime)|$($_.SelectionHash)|$($_.OutcomeHash)"
    })
    [Array]::Sort($values, [StringComparer]::Ordinal)
    return $values -join "`n"
}

function Get-Stats([double[]] $Values) {
    [Array]::Sort($Values)
    $middle = [int][math]::Floor($Values.Count / 2)
    $median = if ($Values.Count % 2) { $Values[$middle] } else { ($Values[$middle - 1] + $Values[$middle]) / 2 }
    [pscustomobject]@{
        Samples = $Values.Count
        MedianSeconds = [math]::Round($median, 3)
        P95Seconds = [math]::Round($Values[[math]::Ceiling($Values.Count * 0.95) - 1], 3)
    }
}

function Get-CaseSum([object[]] $Tests, [string] $Property) {
    [long] $sum = 0
    foreach ($test in $Tests) {
        $count = $test.$Property
        if ($null -eq $count -or $count -lt 0) { throw "Missing or invalid $Property count." }
        $sum += $count
    }
    return $sum
}

try {
    if (-not (Test-Path -LiteralPath $InputRoot -PathType Container)) { throw 'No dogfood evidence directory was downloaded.' }
    $files = @(Get-ChildItem -LiteralPath $InputRoot -Filter 'result.json' -Recurse -File)
    if (-not $files.Count) { throw 'No dogfood result.json files were produced.' }
    $runs = @($files | ForEach-Object { Get-Content -LiteralPath $_.FullName -Raw | ConvertFrom-Json })
    if (@($runs | Where-Object { $_.Schema -ne 1 -or $_.Mode -notin @('Cold', 'BuildCache', 'Cached', 'MSBuild') }).Count) {
        throw 'Unknown dogfood evidence schema or mode.'
    }
    if (@($runs | Group-Object Mode | Where-Object Count -ne 1).Count) { throw 'Duplicate mode evidence is ambiguous.' }
    $expectedModes = if ($RequireControls) { @('Cold', 'BuildCache', 'Cached', 'MSBuild') } else { @('Cached') }
    foreach ($mode in $expectedModes) {
        if ($mode -notin $runs.Mode) { $problems.Add("Missing $mode evidence.") }
    }

    if ($TimelineFile) {
        $timeline = Get-Content -LiteralPath $TimelineFile -Raw | ConvertFrom-Json
    }
    elseif ($BuildId) {
        if ($CollectionUri -ne 'https://dev.azure.com/dnceng/' -or $Project -ne 'public') {
            throw 'Live timeline collection is restricted to the public dnceng project.'
        }
        $uri = "${CollectionUri}public/_apis/build/builds/$BuildId/timeline?api-version=7.1"
        $headers = @{}
        if ($env:SYSTEM_ACCESSTOKEN -and $env:SYSTEM_ACCESSTOKEN -notmatch '^\$\(') {
            $headers.Authorization = "Bearer $env:SYSTEM_ACCESSTOKEN"
        }
        try {
            $timeline = Invoke-RestMethod -Uri $uri -Headers $headers -Method Get -MaximumRedirection 0 -TimeoutSec 60
            $timeline | ConvertTo-Json -Depth 30 | Set-Content -LiteralPath (Join-Path $OutputRoot 'timeline.json') -Encoding utf8
        }
        catch {
            $problems.Add("Azure timing is unavailable: $($_.Exception.Message)")
        }
    }

    $reference = $runs[0]
    $selectionFingerprint = Get-SelectionFingerprint $reference
    foreach ($run in $runs) {
        $consistent = $run.Source.Revision -eq $reference.Source.Revision -and
            $run.Source.PatchHash -eq $reference.Source.PatchHash -and
            $run.ScopeHash -eq $reference.ScopeHash -and
            $run.CacheKey -eq $reference.CacheKey -and
            (Get-SelectionFingerprint $run) -ceq $selectionFingerprint
        if (-not $consistent) { $problems.Add("$($run.Mode) has different source, host, workload, runtime, test identities or outcomes.") }
        $tests = @($run.Tests)
        $targetEvidenceComplete = $tests.Count -eq @($run.Scope.Targets).Count -and
            @($tests | Group-Object Label | Where-Object Count -ne 1).Count -eq 0 -and
            @($run.Scope.Targets | Where-Object { $_ -notin $tests.Label }).Count -eq 0
        if (-not $targetEvidenceComplete) {
            $problems.Add("$($run.Mode) has incomplete or duplicate target evidence.")
        }
        $successful = $run.Completed -and $run.Success -and $tests.Count -gt 0 -and
            @($tests | Where-Object { $_.ExitCode -ne 0 -or $_.Total -le 0 -or $_.Failed -gt 0 -or $_.Other -gt 0 }).Count -eq 0
        if (-not $successful) { $problems.Add("$($run.Mode) failed or did not finish; excluded from successful timing samples.") }
        if ($run.Mode -ne 'Cached' -and @($tests | Where-Object Cached).Count) {
            $problems.Add("$($run.Mode) unexpectedly reused test results.")
        }
        $timing = $null
        if ($null -ne $timeline) {
            $job = if ($run.JobId) { $run.JobId } else { $run.JobName }
            if (-not $job) { throw "No Azure job identity in $($run.Mode) evidence." }
            $timing = Get-BazelDogfoodTimeline -Timeline $timeline -JobName $job
            if (-not $timing.Complete) { $problems.Add("$($run.Mode) has incomplete Azure timing: $($timing.Missing -join ', ').") }
        }
        elseif ($BuildId) {
            $problems.Add("$($run.Mode) has no Azure timeline; cache-transfer-inclusive timing is unavailable.")
        }
        $rows.Add([pscustomobject]@{
            Mode = $run.Mode; Revision = $run.Source.Revision; PatchHash = $run.Source.PatchHash
            ScopeHash = $run.ScopeHash; CacheKey = $run.CacheKey
            Success = $successful; SelectionMatches = $consistent
            BuildSeconds = $run.BuildSeconds; TestSeconds = $run.TestSeconds; HarnessSeconds = $run.HarnessSeconds
            JobSeconds = if ($null -ne $timing -and $timing.Complete) { $timing.JobSeconds } else { $null }
            CacheRestoreSeconds = if ($null -ne $timing) { $timing.CacheRestoreSeconds } else { $null }
            CacheSaveSeconds = if ($null -ne $timing) { $timing.CacheSaveSeconds } else { $null }
            BuildActions = $run.BuildEvents
            TestActions = $run.TestEvents
            Targets = $tests.Count
            ExpectedTargets = @($run.Scope.Targets).Count
            ExecutedTargets = if ($targetEvidenceComplete) { @($tests | Where-Object { -not $_.Cached }).Count } else { $null }
            CachedTargets = if ($targetEvidenceComplete) { @($tests | Where-Object Cached).Count } else { $null }
            SelectedCases = if ($targetEvidenceComplete) { Get-CaseSum $tests 'Total' } else { $null }
            NewlyExecutedCases = if ($targetEvidenceComplete) { Get-CaseSum @($tests | Where-Object { -not $_.Cached }) 'Executed' } else { $null }
            ReusedCases = if ($targetEvidenceComplete) { Get-CaseSum @($tests | Where-Object Cached) 'Total' } else { $null }
            SkippedCasesInEvidence = if ($targetEvidenceComplete) { Get-CaseSum $tests 'Skipped' } else { $null }
            QueueSeconds = $null
        })
    }
    $comparison.Complete = $problems.Count -eq 0
    $comparison.Comparable = $comparison.Complete -and $RequireControls -and $null -ne $timeline
    if ($comparison.Comparable) {
        $cached = @($rows | Where-Object Mode -eq 'Cached')[0]
        foreach ($mode in @('MSBuild', 'Cold', 'BuildCache')) {
            $baseline = @($rows | Where-Object Mode -eq $mode)[0]
            if ($baseline.JobSeconds -le 0) { throw 'Cannot calculate a speedup from an empty baseline duration.' }
            $comparison.Savings += [pscustomobject]@{
                Baseline = $mode; Candidate = 'Cached'
                Seconds = [math]::Round($baseline.JobSeconds - $cached.JobSeconds, 3)
                Percent = [math]::Round(100 * ($baseline.JobSeconds - $cached.JobSeconds) / $baseline.JobSeconds, 2)
            }
        }
    }

    $history = @()
    if ($HistoryRoot) {
        New-Item -ItemType Directory -Path $HistoryRoot -Force | Out-Null
        $historyFile = Join-Path $HistoryRoot 'history.json'
        if (Test-Path -LiteralPath $historyFile) {
            $history = @(Get-Content -LiteralPath $historyFile -Raw | ConvertFrom-Json)
            foreach ($entry in $history) {
                if ($entry.Mode -notin @('Cold', 'BuildCache', 'Cached', 'MSBuild') -or
                    $entry.Revision -notmatch '^[0-9a-f]{40}$' -or
                    $entry.ScopeHash -notmatch '^[0-9A-F]{64}$' -or $entry.CacheKey -notmatch '^[0-9A-F]{64}$' -or
                    -not [double]::IsFinite([double]$entry.JobSeconds) -or $entry.JobSeconds -le 0) {
                    throw 'Invalid cached performance history.'
                }
            }
        }
        if ($comparison.Complete -and $null -ne $timeline) {
            $history = @($history | Where-Object BuildId -ne $BuildId)
            foreach ($row in $rows) {
                $history += [pscustomobject]@{
                    BuildId = $BuildId; Mode = $row.Mode; Revision = $row.Revision
                    ScopeHash = $row.ScopeHash; CacheKey = $row.CacheKey
                    JobSeconds = $row.JobSeconds; CreatedUtc = $comparison.CreatedUtc
                }
            }
            $history = @($history | Select-Object -Last 120)
            ConvertTo-Json -InputObject $history -Depth 6 | Set-Content -LiteralPath $historyFile -Encoding utf8
        }
        foreach ($mode in @('MSBuild', 'Cold', 'BuildCache', 'Cached')) {
            $matching = @($history | Where-Object {
                $_.Mode -eq $mode -and $_.ScopeHash -eq $reference.ScopeHash -and $_.CacheKey -eq $reference.CacheKey
            })
            if (-not $matching.Count) { continue }
            $stats = Get-Stats ([double[]]$matching.JobSeconds)
            $comparison.Trends += [pscustomobject]@{
                Mode = $mode; Samples = $stats.Samples
                DistinctRevisions = @($matching.Revision | Select-Object -Unique).Count
                MedianJobSeconds = $stats.MedianSeconds; P95JobSeconds = $stats.P95Seconds
            }
        }
    }
}
catch {
    $comparison.Complete = $false
    $comparison.Comparable = $false
    $problems.Add($_.Exception.Message)
}
finally {
    $comparison | ConvertTo-Json -Depth 20 | Set-Content -LiteralPath (Join-Path $OutputRoot 'comparison.json') -Encoding utf8
    $rows | Export-Csv -LiteralPath (Join-Path $OutputRoot 'timings.csv') -NoTypeInformation
    $lines = [Collections.Generic.List[string]]::new()
    $lines.Add('# Bazel dogfood: measured unit workload')
    $lines.Add('')
    $lines.Add('Job time includes cache restore/save and setup. Queue delay is not measured. Cached results are historical evidence, not new executions.')
    $lines.Add('')
    $lines.Add('| Mode | Result | Job seconds | Build seconds | Test seconds | Cache restore/save seconds | Executed/cached targets | Newly executed/reused cases |')
    $lines.Add('| --- | --- | ---: | ---: | ---: | --- | --- | --- |')
    foreach ($row in $rows) {
        $job = if ($null -ne $row.JobSeconds) { $row.JobSeconds } else { 'unknown' }
        $restore = if ($null -ne $row.CacheRestoreSeconds) { $row.CacheRestoreSeconds } else { 'unknown' }
        $save = if ($null -ne $row.CacheSaveSeconds) { $row.CacheSaveSeconds } else { 'unknown' }
        $state = if ($row.Success) { 'passed' } else { 'failed/incomplete' }
        $lines.Add("| $($row.Mode) | $state | $job | $($row.BuildSeconds) | $($row.TestSeconds) | $restore / $save | $($row.ExecutedTargets) / $($row.CachedTargets) | $($row.NewlyExecutedCases) / $($row.ReusedCases) |")
    }
    foreach ($saving in $comparison.Savings) {
        $lines.Add('')
        $lines.Add("Cached versus $($saving.Baseline): $($saving.Seconds) seconds saved ($($saving.Percent)%). Negative means slower.")
    }
    if ($comparison.Trends.Count) {
        $lines.Add('')
        $lines.Add('| Mode | Successful samples | Distinct revisions | Median job seconds | P95 job seconds |')
        $lines.Add('| --- | ---: | ---: | ---: | ---: |')
        foreach ($trend in $comparison.Trends) {
            $lines.Add("| $($trend.Mode) | $($trend.Samples) | $($trend.DistinctRevisions) | $($trend.MedianJobSeconds) | $($trend.P95JobSeconds) |")
        }
        $lines.Add('')
        $lines.Add('Trends only combine identical workload/toolchain/host contracts. A small or single-revision sample is not a reliable CI savings estimate.')
    }
    if (-not $comparison.Comparable) {
        $lines.Add('')
        $lines.Add('No like-for-like end-to-end speedup is claimed for this run: complete matched controls and Azure timing are required.')
    }
    foreach ($problem in $problems) { $lines.Add('Problem: ' + $problem.Replace("`n", ' ')) }
    $summary = Join-Path $OutputRoot 'summary.md'
    [IO.File]::WriteAllLines($summary, $lines)
    if ($env:TF_BUILD -eq 'True') { Write-Host "##vso[task.uploadsummary]$summary" }
    Write-Host ($lines -join [Environment]::NewLine)
}
if (-not $comparison.Complete) { exit 1 }
exit 0
