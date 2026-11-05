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
$root = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$bazel = Join-Path $root 'eng\bazel.ps1'
$run = [DateTime]::UtcNow.ToString('yyyyMMdd-HHmmss') + '-' + [Guid]::NewGuid().ToString('N').Substring(0, 8)
$directory = if ($EvidenceRoot) { Join-Path $EvidenceRoot $run } else { Join-Path $root "artifacts\bazel-validation\$run" }
$reports = Join-Path $directory 'reports with spaces!'
$testSuite = if ($IsWindows) { '//:portable_tests' } else { '//:portable_non_windows_tests' }
$unsupportedSuites = @(if (-not $IsWindows) { '//:platform_services_tests (Windows filesystem contract; also excluded by NonWindowsTests.slnf)' })
if ($unsupportedSuites.Count) { Write-Host "Unsupported on this host: $($unsupportedSuites -join ', ')" }
New-Item -ItemType Directory -Path $reports -Force | Out-Null
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
    return Get-Content -LiteralPath $log -Raw
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

Push-Location $root
try {
    $null = Invoke-BazelStep 'managed-build' @('build', '//:managed_all', "--jobs=$Jobs", '--noshow_progress', '--show_result=0')
    $null = Invoke-BazelStep 'cached-build' @('build', '//:managed_all', "--jobs=$Jobs", '--noshow_progress', '--show_result=0')
    $null = Invoke-BazelStep 'portable-tests' @('test', $testSuite, "--jobs=$Jobs", '--nocache_test_results', '--noshow_progress', '--show_result=0')

    $testLogs = & $bazel info bazel-testlogs
    if ($LASTEXITCODE -ne 0) {
        throw 'Could not locate the baseline test reports.'
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
        '--filter-uid', 'TestingPlatformExplorer.UnitTests.SomeTests.TestMethod2'
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

    $aggregate = Invoke-BazelStep 'mixed-results' @(
        'test', '//:simple1_test', '//:explorer_failure_probe',
        '--nocache_test_results', '--noshow_progress', '--show_result=0'
    ) 3
    if ($aggregate -notmatch '(?m)^//:simple1_test\s+PASSED' -or $aggregate -notmatch '(?m)^//:explorer_failure_probe\s+FAILED') {
        throw 'Bazel did not retain both the passing and failing module results.'
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
        TargetFrameworks = @('net8.0', 'net9.0', 'net10.0', 'net11.0')
        TestCounts = $counts
        TestSuite = $testSuite
        UnsupportedSuites = @($unsupportedSuites)
        DeclaredCompilerActions = $compilerActions.Count
        Steps = $steps
    } | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $directory 'validation.json') -Encoding utf8
    Write-Host "Bazel portable matrix and composed-product validation passed. Evidence: $directory"
}
finally {
    Pop-Location
}
