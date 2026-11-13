# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT license. See LICENSE file in the project root for full license information.

#requires -Version 7.2

[CmdletBinding()]
param(
    [ValidateRange(1, 2147483647)]
    [int] $Jobs = 8,

    [ValidateScript({ [System.IO.Path]::IsPathFullyQualified($_) })]
    [string] $EvidenceRoot
)

$ErrorActionPreference = 'Stop'
& (Join-Path $PSScriptRoot 'verify-roslyn-dependencies.ps1') -SelfTest
. (Join-Path $PSScriptRoot 'console-output.ps1') -SelfTest
$root = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$bazel = Join-Path $root 'eng\bazel.ps1'
$run = [DateTime]::UtcNow.ToString('yyyyMMdd-HHmmss') + '-' + [Guid]::NewGuid().ToString('N').Substring(0, 8)
$directory = if ($EvidenceRoot) { Join-Path $EvidenceRoot $run } else { Join-Path $root "artifacts\bazel-validation\$run" }
$reports = Join-Path $directory 'reports with spaces!'
$testEvidence = Join-Path $directory 'tests'

# `bazel test //...` must run every automatic test. Bazel reports host-incompatible tests as SKIPPED.
$expectedHostSkips = [ordered]@{}
if ($IsWindows) { $expectedHostSkips['//:aot_publish_test'] = 'The pinned rules_dotnet links NativeAOT only for Linux and macOS.' }
else { $expectedHostSkips['//:platform_services_tests'] = 'Windows filesystem contract; also excluded by NonWindowsTests.slnf.' }
# A floor so that deleting or excluding test targets fails validation even though the expected set is queried.
$minimumTests = 35

# Manual targets that no automatic `//...` target depends on. Every other manual target, such as the
# per-framework and Windows variants, is built as a dependency of an automatic wrapper target. A manual
# target that is not in this list, or a listed target that no longer exists, fails validation.
$unreachableManualTargets = [ordered]@{
    '//:custom_report_failure_probe' = 'Intentionally failing sample; asserted by the mixed-results step.'
    '//:ensure_failure_probe' = 'Intentionally failing sample; asserted by the mixed-results step.'
    '//:explorer_failure_probe' = 'Intentionally failing sample; asserted by the mixed-results step.'
    '//:reference_alias_duplicate' = 'Intentional analysis failure; asserted by the reference-alias-failures step.'
    '//:reference_alias_empty' = 'Intentional analysis failure; asserted by the reference-alias-failures step.'
    '//:reference_alias_unknown' = 'Intentional analysis failure; asserted by the reference-alias-failures step.'
}
foreach ($entry in $expectedHostSkips.GetEnumerator()) { Write-Host "Unsupported on this host: $($entry.Key) ($($entry.Value))" }
New-Item -ItemType Directory -Path $reports, $testEvidence -Force | Out-Null
$steps = [System.Collections.Generic.List[object]]::new()

function Invoke-BazelStep([string] $Name, [string[]] $Arguments, [int] $ExpectedExit = 0) {
    $log = Join-Path $directory "$Name.log"
    & $bazel @Arguments *> $log
    $code = $LASTEXITCODE
    $steps.Add([pscustomobject]@{ Name = $Name; Arguments = $Arguments; ExpectedExit = $ExpectedExit; ActualExit = $code; Log = "$Name.log" })
    if ($code -ne $ExpectedExit) {
        Get-Content -LiteralPath $log -Tail 50 | Write-Host
        throw "$Name returned $code, expected $ExpectedExit. See $log"
    }

    Write-Host "$Name`: exit $code"
    return Read-BazelPlainLog $log
}

function Assert-Trx([string] $Path, [int] $ExpectedCount = 0) {
    [xml]$report = Get-Content -LiteralPath $Path -Raw
    $counters = $report.SelectSingleNode('//*[local-name()="Counters"]')
    $results = @($report.SelectNodes('//*[local-name()="UnitTestResult"]'))
    if (-not $counters -or $results.Count -eq 0 -or [int]$counters.total -ne $results.Count -or
        [int]$counters.executed -ne $results.Count -or [int]$counters.passed -ne $results.Count -or
        @($results | Where-Object { $_.outcome -ne 'Passed' }).Count -ne 0 -or
        ($ExpectedCount -gt 0 -and $results.Count -ne $ExpectedCount)) {
        throw "Missing, skipped, failed, or inconsistent test results: $Path"
    }

    return $results.Count
}

function Invoke-BazelQuery([string] $Name, [string] $Expression) {
    # A query file keeps quoting identical across PowerShell, cmd and POSIX shells and records the exact expression.
    $queryFile = Join-Path $directory "$Name.query"
    [IO.File]::WriteAllText($queryFile, $Expression)
    $output = Invoke-BazelStep $Name @('query', "--query_file=$queryFile", '--output=label', '--noshow_progress')
    return @($output -split '\r?\n' | Where-Object { $_ -match '^//\S*$' } | Sort-Object -Unique)
}

function Get-TestOutcomes([string] $Output) {
    $outcomes = [ordered]@{}
    $pattern = '(?m)^(//\S+)\s+(?:\(cached\)\s+)?(PASSED|FAILED TO BUILD|FAILED|SKIPPED|NO STATUS|TIMEOUT|FLAKY|INCOMPLETE)\b'
    foreach ($match in [regex]::Matches($Output, $pattern)) {
        $outcomes[$match.Groups[1].Value] = $match.Groups[2].Value
    }

    return $outcomes
}

function Get-TestLogPath([string] $TestLogs, [string] $Label, [string] $Relative) {
    return Join-Path (Join-Path $TestLogs $Label.Substring(2).Replace(':', '/').TrimStart('/')) $Relative
}

function Get-TrxSummary([string] $Path) {
    # Counters and XPath counts keep the 100,000-case scale fixture cheap to inspect.
    [xml]$report = Get-Content -LiteralPath $Path -Raw
    $counters = $report.SelectSingleNode('//*[local-name()="Counters"]')
    $total = $report.SelectNodes('//*[local-name()="UnitTestResult"]').Count
    $failed = $report.SelectNodes('//*[local-name()="UnitTestResult"][@outcome="Failed" or @outcome="Error" or @outcome="Timeout" or @outcome="Aborted"]').Count
    if (-not $counters -or $total -eq 0 -or [int]$counters.total -ne $total -or [int]$counters.executed -eq 0 -or $failed -ne 0 -or
        ([int]$counters.failed + [int]$counters.error + [int]$counters.timeout + [int]$counters.aborted) -ne 0) {
        throw "Missing, failed, or inconsistent test results: $Path"
    }

    return [ordered]@{ Total = $total; Executed = [int]$counters.executed; Passed = [int]$counters.passed }
}

Push-Location $root
try {
    $null = Invoke-BazelStep 'build-all' @('build', '//...', '--keep_going', "--jobs=$Jobs", '--noshow_progress', '--show_result=0')
    $null = Invoke-BazelStep 'cached-build' @('build', '//...', "--jobs=$Jobs", '--noshow_progress', '--show_result=0')

    $manualQuery = 'attr(tags, "\bmanual\b", //...)'
    $unreachableManual = Invoke-BazelQuery 'unreachable-manual-targets' "$manualQuery except deps(//... except $manualQuery)"
    $unlisted = @($unreachableManual | Where-Object { -not $unreachableManualTargets.Contains($_) })
    $stale = @($unreachableManualTargets.Keys | Where-Object { $_ -notin $unreachableManual })
    if ($unlisted.Count -or $stale.Count) {
        throw "Manual targets outside //... changed. Unlisted: $($unlisted -join ', '); no longer unreachable: $($stale -join ', '). " +
            'Build new targets through an automatic target or document why they are excluded.'
    }

    $expectedTests = Invoke-BazelQuery 'expected-tests' "tests(//...) except $manualQuery"
    if ($expectedTests.Count -lt $minimumTests) {
        throw "Only $($expectedTests.Count) automatic test targets were found; expected at least $minimumTests."
    }

    $testLogs = & $bazel info bazel-testlogs
    if ($LASTEXITCODE -ne 0) {
        throw 'Could not locate the baseline test reports.'
    }

    $testStart = [DateTime]::UtcNow
    try {
        $allTests = Invoke-BazelStep 'all-tests' @('test', '//...', '--keep_going', "--jobs=$Jobs", '--nocache_test_results', '--noshow_progress', '--show_result=0')
    }
    finally {
        # Preserve this run's Bazel results before validating outcomes so failures remain in the uploaded evidence.
        foreach ($label in $expectedTests) {
            $testXml = Get-TestLogPath $testLogs $label 'test.xml'
            try {
                if ((Test-Path -LiteralPath $testXml) -and (Get-Item -LiteralPath $testXml).LastWriteTimeUtc -ge $testStart) {
                    Copy-Item -LiteralPath $testXml -Destination (Join-Path $testEvidence ($label.Substring(2).Replace(':', '_').Replace('/', '_').TrimStart('_') + '.xml'))
                }
            }
            catch {
                Write-Warning "Could not preserve $testXml`: $_"
            }
        }
    }

    $outcomes = Get-TestOutcomes $allTests
    $unexpectedOutcomes = @($outcomes.Keys | Where-Object { $_ -notin $expectedTests })
    $wrongOutcomes = @($expectedTests | Where-Object {
        $expected = if ($expectedHostSkips.Contains($_)) { 'SKIPPED' } else { 'PASSED' }
        $outcomes[$_] -cne $expected
    } | ForEach-Object { "$_ ($(if ($outcomes.Contains($_)) { $outcomes[$_] } else { 'no result' }))" })
    if ($unexpectedOutcomes.Count -or $wrongOutcomes.Count) {
        throw "Not every automatic test ran with its expected outcome. Unexpected: $($unexpectedOutcomes -join ', '); wrong or missing: $($wrongOutcomes -join ', ')"
    }

    $trxSummaries = [ordered]@{}
    foreach ($label in $expectedTests) {
        $trx = Get-TestLogPath $testLogs $label 'test.outputs/tests.trx'
        if (-not $expectedHostSkips.Contains($label) -and (Test-Path -LiteralPath $trx)) {
            $trxSummaries[$label] = Get-TrxSummary $trx
        }
    }

    $counts = [ordered]@{}
    $reportTargets = @('framework_tests', 'adapter_tests', 'build_tools_tests', 'simple1_test', 'protocol_contract_tests', 'source_generation_tests', 'vstest_bridge_tests')
    if ($IsWindows) { $reportTargets += 'platform_services_tests' }
    foreach ($target in $reportTargets) {
        $path = Join-Path $testLogs "$target\test.outputs\tests.trx"
        $counts[$target] = Assert-Trx $path
        Copy-Item -LiteralPath $path -Destination (Join-Path $directory "$target.trx")
    }
    foreach ($target in @('explorer_test', 'explorer_released_test')) {
        Copy-Item -LiteralPath (Join-Path $testLogs "$target\test.xml") -Destination (Join-Path $directory "$target.xml")
    }

    $discovery = Invoke-BazelStep 'discovery' @('run', '//:explorer', '--noshow_progress', '--show_result=0', '--', '--list-tests')
    foreach ($name in @('TestMethod1', 'TestMethod2', 'TestMethod3', 'TestMethod4')) {
        if (-not $discovery.Contains($name)) {
            throw "Discovery did not include $name."
        }
    }

    $null = Invoke-BazelStep 'reports' @(
        'run', '//:simple1', '--noshow_progress', '--show_result=0', '--',
        '--results-directory', $reports,
        '--report-trx', '--report-trx-filename', 'simple1.trx',
        '--report-html', '--report-html-filename', 'simple1.html',
        '--report-junit', '--report-junit-filename', 'simple1.xml',
        '--report-ctrf', '--report-ctrf-filename', 'simple1.json'
    )
    $null = Assert-Trx (Join-Path $reports 'simple1.trx') 1
    [xml]$junit = Get-Content -LiteralPath (Join-Path $reports 'simple1.xml') -Raw
    if (@($junit.SelectNodes('//testcase')).Count -ne 1 -or @($junit.SelectNodes('//failure | //error | //skipped')).Count -ne 0) {
        throw 'The JUnit report does not contain exactly one passing test.'
    }

    $ctrf = Get-Content -LiteralPath (Join-Path $reports 'simple1.json') -Raw | ConvertFrom-Json
    if ($ctrf.results.summary.tests -ne 1 -or $ctrf.results.summary.passed -ne 1 -or
        @($ctrf.results.tests).Count -ne 1 -or $ctrf.results.tests[0].status -ne 'passed') {
        throw 'The CTRF report does not contain exactly one passing test.'
    }

    $html = Get-Content -LiteralPath (Join-Path $reports 'simple1.html') -Raw
    if (-not $html.Contains('<html') -or -not $html.Contains('TestMethod1')) {
        throw 'The HTML template or its test payload was not included in the report.'
    }

    $failure = Invoke-BazelStep 'failed-test' @(
        'run', '//:explorer', '--noshow_progress', '--show_result=0', '--',
        '--filter-uid', 'TestingPlatformExplorer.UnitTests.SomeTests.TestMethod2', '--ansi', 'on'
    ) 2
    if ($failure -notmatch 'failed TestMethod2') {
        throw 'The expected failing sample did not execute.'
    }

    $empty = Invoke-BazelStep 'empty-selection' @(
        'run', '//:explorer', '--noshow_progress', '--show_result=0', '--',
        '--filter-uid', 'NoMatchingTest', '--minimum-expected-tests', '1'
    ) 9
    if ($empty -notmatch 'tests ran 0, minimum expected 1') {
        throw 'The zero-result policy was not exercised.'
    }

    $failureProbes = @('//:explorer_failure_probe', '//:custom_report_failure_probe', '//:ensure_failure_probe')
    $aggregate = Invoke-BazelStep 'mixed-results' (@('test', '//:simple1_test') + $failureProbes + @(
        '--keep_going', '--nocache_test_results', '--noshow_progress', '--show_result=0'
    )) 3
    $aggregateOutcomes = Get-TestOutcomes $aggregate
    if ($aggregateOutcomes['//:simple1_test'] -cne 'PASSED' -or @($failureProbes | Where-Object { $aggregateOutcomes[$_] -cne 'FAILED' }).Count) {
        throw 'Bazel did not retain both the passing and failing module results.'
    }

    # Each fixture is analyzed separately because one analysis failure can hide another's message.
    $aliasFailures = [ordered]@{
        '//:reference_alias_unknown' = 'Reference aliases name assemblies absent from this compilation: missing.assembly'
        '//:reference_alias_empty' = 'Reference aliases must be nonempty and unique for Bazel.Aliases.First'
        '//:reference_alias_duplicate' = 'Reference aliases must be nonempty and unique for Bazel.Aliases.First'
    }
    foreach ($entry in $aliasFailures.GetEnumerator()) {
        $aliasFailure = Invoke-BazelStep ('reference-alias-failure-' + $entry.Key.Substring(3)) @('build', $entry.Key, '--noshow_progress', '--show_result=0') 1
        if (-not $aliasFailure.Contains($entry.Value, [StringComparison]::Ordinal)) {
            throw "$($entry.Key) did not fail analysis with: $($entry.Value)"
        }
    }

    $graphPath = Join-Path $directory 'actions.json'
    & $bazel aquery 'deps(//:managed_all)' --output=jsonproto --noshow_progress > $graphPath 2> (Join-Path $directory 'actions.log')
    if ($LASTEXITCODE -ne 0) {
        throw 'Could not inspect the native action graph.'
    }

    $graph = Get-Content -LiteralPath $graphPath -Raw | ConvertFrom-Json -AsHashtable
    $compilerActions = @($graph.actions | Where-Object { $_.mnemonic -eq 'CSharpCompile' })
    if ($compilerActions.Count -lt 35) {
        throw 'The action graph did not contain the managed compiler actions.'
    }

    foreach ($action in $graph.actions) {
        foreach ($argument in $action.arguments) {
            if ($argument -match '(?i)(^|[\s/\\])(MSBuild(\.exe|\.dll)?|dotnet(\.exe)?\s+(build|test|msbuild))(\s|$)') {
                throw "An MSBuild invocation was found in the Bazel action graph: $argument"
            }
        }
    }

    $toolchainOutput = Invoke-BazelStep 'toolchain-provenance' @(
        'cquery', '@rules_dotnet//dotnet:resolved_toolchain', '--output=starlark', '--noshow_progress',
        '--starlark:expr="TESTFX_TOOLCHAIN|" + providers(target)["ToolchainInfo"].dotnetinfo.sdk_version + "|" + providers(target)["ToolchainInfo"].dotnetinfo.runtime_version'
    )
    $toolchain = [regex]::Match($toolchainOutput, '(?m)^TESTFX_TOOLCHAIN\|([^|\r\n]+)\|([^|\r\n]+)')
    if (-not $toolchain.Success) {
        throw 'Could not identify the resolved producer SDK and runtime.'
    }

    $revision = git rev-parse HEAD
    if ($LASTEXITCODE -ne 0) {
        throw 'Could not record the source revision.'
    }

    [pscustomobject]@{
        BaseRevision = $revision
        WorkingTree = @(git status --porcelain)
        Bazel = (Get-Content -LiteralPath .bazelversion -Raw).Trim()
        Jobs = $Jobs
        RulesDotnet = 'add5c92b30888a70aa951de1744a770b8f8ef436'
        Sdk = $toolchain.Groups[1].Value
        Runtime = $toolchain.Groups[2].Value
        TargetFrameworks = @('net10.0', 'net11.0')
        TestCounts = $counts
        ExpectedTests = $expectedTests
        TestOutcomes = $outcomes
        ExpectedHostSkips = $expectedHostSkips
        TrxSummaries = $trxSummaries
        UnreachableManualTargets = $unreachableManualTargets
        DeclaredCompilerActions = $compilerActions.Count
        Steps = $steps
    } | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $directory 'validation.json') -Encoding utf8
    Write-Host "Bazel built //..., ran $($expectedTests.Count) automatic tests ($($expectedHostSkips.Count) host-incompatible), and passed composed-product validation. Evidence: $directory"
}
finally {
    Pop-Location
}
