# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT license. See LICENSE file in the project root for full license information.

<#
.SYNOPSIS
Runs the unit suites that the Bazel dogfood workload runs against the outputs of eng\buildxl.ps1.

.DESCRIPTION
Consumes the assemblies and toolchain.json produced by eng\buildxl.ps1 and executes exactly the suites listed in the
Bazel dogfood workload (eng\bazel\dogfood-workload.json), uncached, so the restore, build and test phases of the two
build systems can be compared. Test identity comes from each suite's TRX through eng\bazel\dogfood-results.psm1, the
same parser the Bazel dogfood harness uses, so selection and outcome hashes are comparable across both systems.
Evidence is written below <output>\logs\unit-tests, which the BuildXL job already retains.
#>

#Requires -Version 7.0

[CmdletBinding(PositionalBinding = $false)]
param(
    [ValidateSet('Debug', 'Release')]
    [Alias('c')]
    [string] $Configuration = 'Debug',
    [ValidatePattern('^$|^[a-z0-9][a-z0-9-]{0,80}$')]
    [string] $OutputId = '',
    [string] $Workload = 'eng\bazel\dogfood-workload.json',
    [ValidateRange(1, 16)]
    [int] $TestJobs = 2,
    [ValidateRange(1, 86400)]
    [int] $TestTimeoutSeconds = 1800
)

$ErrorActionPreference = 'Stop'
$PSNativeCommandUseErrorActionPreference = $false
Set-StrictMode -Version 2.0

function Get-TextHash([string] $Text) {
    return [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData([Text.Encoding]::UTF8.GetBytes($Text)))
}

function Get-InstalledRuntimes([string] $DotNet) {
    $lines = @(& $DotNet --list-runtimes)
    if ($LASTEXITCODE -ne 0) {
        throw "'$DotNet --list-runtimes' exited $LASTEXITCODE."
    }

    foreach ($line in $lines) {
        if ($line -match '^Microsoft\.NETCore\.App (?<version>(?<core>\d+\.\d+\.\d+)(?<pre>-\S+)?) \[') {
            [pscustomobject]@{
                Version = $Matches.version
                Core = [version] $Matches.core
                Stable = -not $Matches.pre
            }
        }
    }
}

function Select-Runtime([object[]] $Runtimes, [int] $Major, [string] $Purpose) {
    $match = @($Runtimes |
        Where-Object { $_.Core.Major -eq $Major } |
        Sort-Object @{ Expression = { $_.Core }; Descending = $true }, @{ Expression = { $_.Stable }; Descending = $true }, @{ Expression = { $_.Version }; Descending = $true } |
        Select-Object -First 1)
    if ($match.Count -eq 0) {
        throw "No installed Microsoft.NETCore.App $Major.x runtime for $Purpose. Installed: $(@($Runtimes | ForEach-Object Version) -join ', ')."
    }

    return $match[0].Version
}

if ($env:OS -ne 'Windows_NT' -or $env:PROCESSOR_ARCHITECTURE -ne 'AMD64') {
    throw 'The BuildXL unit-test run requires Windows x64 and PowerShell 7.'
}

$repo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..'))
$output = if ($OutputId) { Join-Path $repo "artifacts\buildxl-runs\$OutputId" } else { Join-Path $repo 'artifacts\buildxl' }
$logs = Join-Path $output 'logs'
$evidence = Join-Path $logs 'unit-tests'
$trxDirectory = Join-Path $evidence 'trx'
$workloadPath = [IO.Path]::GetFullPath($Workload, $repo)

$report = [ordered]@{
    SchemaVersion = 1
    Configuration = $Configuration
    Workload = $null
    IdentityPolicy = $null
    Engine = $null
    SourceRevision = $null
    Sdk = $null
    DotNetRoot = $null
    TestJobs = $TestJobs
    TestTimeoutSeconds = $TestTimeoutSeconds
    RuntimeInstallSeconds = 0
    TestSeconds = $null
    Success = $false
    Errors = @()
    Suites = @()
}
$suites = [Collections.Generic.List[object]]::new()
$children = [Collections.Generic.List[object]]::new()
$phases = $null

try {
    $toolchainPath = Join-Path $output 'toolchain.json'
    if (-not (Test-Path -LiteralPath $toolchainPath -PathType Leaf)) {
        throw "Missing $toolchainPath. Build with eng\buildxl.ps1 first."
    }

    $toolchain = Get-Content -LiteralPath $toolchainPath -Raw | ConvertFrom-Json
    $toolchainProperties = @($toolchain.PSObject.Properties.Name)
    foreach ($name in @('SourceRevision', 'Sdk', 'Engine', 'Configuration', 'DotNetRoot', 'Project', 'TargetFramework')) {
        if ($name -notin $toolchainProperties -or [string]::IsNullOrWhiteSpace([string] $toolchain.$name)) {
            throw "toolchain.json does not record '$name'; rebuild with the current eng\buildxl.ps1."
        }
    }

    if ($toolchain.Configuration -ne $Configuration) {
        throw "The build output is $($toolchain.Configuration), not $Configuration."
    }

    $head = (& git -C $repo rev-parse HEAD).Trim()
    if ($LASTEXITCODE -ne 0 -or $head -ne $toolchain.SourceRevision) {
        throw "The build output is for revision $($toolchain.SourceRevision), but the checkout is at $head."
    }

    $timingsPath = Join-Path $logs 'phase-timings.json'
    if (-not (Test-Path -LiteralPath $timingsPath -PathType Leaf)) {
        throw "Missing $timingsPath. The build did not complete."
    }

    $phases = Get-Content -LiteralPath $timingsPath -Raw | ConvertFrom-Json
    if ($null -eq $phases.BuildExitCode -or $phases.BuildExitCode -ne 0) {
        throw "The build did not succeed (build exit code: $($phases.BuildExitCode))."
    }

    $report.Engine = $toolchain.Engine
    $report.SourceRevision = $toolchain.SourceRevision
    $report.Sdk = $toolchain.Sdk
    $report.DotNetRoot = $toolchain.DotNetRoot

    if (-not (Test-Path -LiteralPath $workloadPath -PathType Leaf)) {
        throw "Missing workload: $workloadPath"
    }

    $workloadDefinition = Get-Content -LiteralPath $workloadPath -Raw | ConvertFrom-Json
    if ($workloadDefinition.schemaVersion -ne 1 -or $workloadDefinition.targetFramework -notmatch '^net(?<major>\d+)\.0$' -or
        @($workloadDefinition.tests).Count -eq 0) {
        throw 'Unsupported workload definition.'
    }

    $frameworkMajor = [int] $Matches.major

    # A narrower build into the same output rewrites toolchain.json but leaves assemblies from an earlier, wider build in
    # place, so the revision check alone would attribute those stale assemblies to the current revision.
    if ($toolchain.Project -ne 'TestFx.slnx' -or $toolchain.TargetFramework -notin @('Project defaults', $workloadDefinition.targetFramework)) {
        throw "The build output covers $($toolchain.Project) ($($toolchain.TargetFramework)), not the workload. Build TestFx.slnx with the project default target frameworks or $($workloadDefinition.targetFramework)."
    }

    $report.Workload = $workloadDefinition.name
    $report.IdentityPolicy = $workloadDefinition.identityPolicy
    $report.WorkloadSha256 = (Get-FileHash -LiteralPath $workloadPath -Algorithm SHA256).Hash

    $selection = @($workloadDefinition.tests)
    $labels = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
    foreach ($test in $selection) {
        if ($test.label -notmatch '^//:[a-z0-9_]+$' -or $test.project -notmatch '^[A-Za-z0-9._-]+$' -or
            $test.assembly -notmatch '^[A-Za-z0-9._-]+$' -or $test.runtime -cnotin @('sdk', 'selfContained') -or
            -not $labels.Add($test.label)) {
            throw "Invalid or duplicate workload entry '$($test.label)'."
        }
    }

    $bin = Join-Path $output 'bin'
    $missing = @($selection | ForEach-Object {
        $dll = Join-Path $bin "$($_.project)\$Configuration\$($workloadDefinition.targetFramework)\$($_.assembly).dll"
        if (-not (Test-Path -LiteralPath $dll -PathType Leaf)) { $dll }
    })
    if ($missing.Count) {
        throw "Missing selected test assemblies: $($missing -join '; ')"
    }

    $dotnet = Join-Path $toolchain.DotNetRoot 'dotnet.exe'
    if (-not (Test-Path -LiteralPath $dotnet -PathType Leaf)) {
        throw "Missing $dotnet."
    }

    # Bazel hands the analyzer suite its own built global-config generator through an environment variable, because
    # that suite cannot find it in the MSBuild artifacts layout. Supply the generator BuildXL built through the same
    # hook. It targets the repository's current framework, so the SDK root hosts it.
    $suiteTools = @{ '//:analyzer_tests' = @{ Variable = 'TESTFX_BAZEL_GLOBAL_CONFIGS_GENERATOR'; Project = 'MSTest.GlobalConfigsGenerator' } }
    foreach ($label in @($suiteTools.Keys)) {
        if ($label -notin $labels) {
            $suiteTools.Remove($label)
            continue
        }

        $tool = $suiteTools[$label]
        $candidates = @(Get-ChildItem -LiteralPath (Join-Path $bin "$($tool.Project)\$Configuration") -Directory -ErrorAction SilentlyContinue |
            Where-Object Name -match '^net\d+\.\d+$' |
            ForEach-Object { Join-Path $_.FullName "$($tool.Project).dll" } |
            Where-Object { Test-Path -LiteralPath $_ -PathType Leaf })
        if ($candidates.Count -ne 1) {
            throw "Expected exactly one built $($tool.Project) under $bin\$($tool.Project)\$Configuration for $label, found $($candidates.Count)."
        }

        $tool.Path = $candidates[0]
    }

    # Bazel runs these net10.0 suites on the SDK's bundled runtime, except the self-contained ones, which it runs on a
    # runtime installed privately at the version pinned by the workload. Do the same so the hosts match.
    $runtimes = @(Get-InstalledRuntimes $dotnet)
    $sdkMajor = [int] ($toolchain.Sdk -split '\.')[0]
    $sdkRuntime = Select-Runtime $runtimes $sdkMajor 'the SDK-bundled suites'
    $sdkRoot = Split-Path $dotnet -Parent
    $selfContainedRuntime = $null
    $selfContainedRoot = $null
    if (@($selection | Where-Object runtime -ceq 'selfContained').Count) {
        $selfContainedRuntime = [string] $workloadDefinition.selfContainedRuntime
        if ($selfContainedRuntime -notmatch '^\d+\.\d+\.\d+$' -or ([version] $selfContainedRuntime).Major -ne $frameworkMajor) {
            throw "The workload's selfContainedRuntime '$selfContainedRuntime' is not a $frameworkMajor.x release version."
        }

        $selfContainedRoot = Join-Path $repo "artifacts\buildxl-tools\runtimes\$selfContainedRuntime"
    }

    Import-Module (Join-Path $repo 'eng\bazel\dogfood-results.psm1') -Force

    if (Test-Path -LiteralPath $evidence) {
        Remove-Item -LiteralPath $evidence -Recurse -Force
    }

    New-Item -ItemType Directory -Path $trxDirectory -Force | Out-Null

    if ($selfContainedRuntime) {
        $coreLib = Join-Path $selfContainedRoot "shared\Microsoft.NETCore.App\$selfContainedRuntime\System.Private.CoreLib.dll"
        if (-not (Test-Path -LiteralPath $coreLib -PathType Leaf)) {
            $installWatch = [Diagnostics.Stopwatch]::StartNew()
            $installLog = Join-Path $evidence 'runtime-install.log'
            & (Get-Process -Id $PID).Path -NoProfile -NonInteractive -File (Join-Path $repo 'eng\common\dotnet-install.ps1') `
                -runtime dotnet -version $selfContainedRuntime -dotnetPath $selfContainedRoot -architecture x64 *> $installLog
            $installExitCode = $LASTEXITCODE
            $installWatch.Stop()
            $report.RuntimeInstallSeconds = [math]::Round($installWatch.Elapsed.TotalSeconds, 3)
            if ($installExitCode -ne 0 -or -not (Test-Path -LiteralPath $coreLib -PathType Leaf)) {
                throw "Runtime $selfContainedRuntime could not be installed privately (exit code $installExitCode); see $installLog."
            }
        }

        if (-not (Test-Path -LiteralPath (Join-Path $selfContainedRoot 'dotnet.exe') -PathType Leaf)) {
            throw "The private runtime at $selfContainedRoot has no dotnet.exe host."
        }
    }

    function Start-Suite($Test) {
        $name = $Test.label.Substring(3)
        $runtime = if ($Test.runtime -ceq 'sdk') { $sdkRuntime } else { $selfContainedRuntime }
        $root = if ($Test.runtime -ceq 'sdk') { $sdkRoot } else { $selfContainedRoot }
        $dll = Join-Path $bin "$($Test.project)\$Configuration\$($workloadDefinition.targetFramework)\$($Test.assembly).dll"
        $info = [Diagnostics.ProcessStartInfo]::new((Join-Path $root 'dotnet.exe'))
        $info.WorkingDirectory = $repo
        $info.UseShellExecute = $false
        $info.RedirectStandardOutput = $true
        $info.RedirectStandardError = $true
        foreach ($argument in @(
            'exec', '--fx-version', $runtime, $dll, '--minimum-expected-tests', '1',
            '--results-directory', $trxDirectory, '--report-trx', '--report-trx-filename', "$name.trx")) {
            $info.ArgumentList.Add($argument)
        }

        # The repository's SDK ships only a newer runtime than the net10.0 assemblies target, and the MSBuild-built
        # runtimeconfig has no roll-forward policy (Bazel-built ones carry LatestMajor), so the SDK-runtime suites
        # must roll forward by major; the privately installed runtime matches the target and only needs patch roll-forward.
        $rollForward = if ($Test.runtime -ceq 'sdk') { 'LatestMajor' } else { 'LatestPatch' }
        $dotnetRoot = $root
        $tool = $suiteTools[$Test.label]
        if ($tool) {
            $info.Environment[$tool.Variable] = $tool.Path
            $dotnetRoot = $sdkRoot
        }

        $info.Environment['DOTNET_ROOT'] = $dotnetRoot
        $info.Environment['DOTNET_ROOT_X64'] = $dotnetRoot
        $info.Environment['DOTNET_ROLL_FORWARD'] = $rollForward
        $info.Environment['DOTNET_MULTILEVEL_LOOKUP'] = '0'
        $info.Environment['DOTNET_NOLOGO'] = '1'
        $info.Environment['DOTNET_CLI_TELEMETRY_OPTOUT'] = '1'
        $info.Environment['PATH'] = "$root;$env:PATH"

        $stdoutPath = Join-Path $evidence "$name.stdout.log"
        $stderrPath = Join-Path $evidence "$name.stderr.log"
        $stdout = [IO.FileStream]::new($stdoutPath, [IO.FileMode]::CreateNew, [IO.FileAccess]::Write, [IO.FileShare]::Read, 1, [IO.FileOptions]::Asynchronous)
        $stderr = [IO.FileStream]::new($stderrPath, [IO.FileMode]::CreateNew, [IO.FileAccess]::Write, [IO.FileShare]::Read, 1, [IO.FileOptions]::Asynchronous)
        try {
            $process = [Diagnostics.Process]::Start($info)
        }
        catch {
            $stdout.Dispose()
            $stderr.Dispose()
            throw
        }

        $child = [pscustomobject]@{
            Test = $Test; Name = $name; Runtime = $runtime; Process = $process; Watch = [Diagnostics.Stopwatch]::StartNew()
            RollForward = $rollForward; Tool = if ($tool) { $tool.Path }
            Output = $process.StandardOutput.BaseStream.CopyToAsync($stdout)
            ErrorOutput = $process.StandardError.BaseStream.CopyToAsync($stderr)
            OutputStream = $stdout; ErrorStream = $stderr; TimedOut = $false
            Trx = Join-Path $trxDirectory "$name.trx"; Stdout = $stdoutPath; Stderr = $stderrPath
        }
        $children.Add($child)
        return $child
    }

    function Complete-Suite($Child) {
        $Child.Process.WaitForExit()
        $Child.Watch.Stop()
        try {
            $null = $Child.Output.GetAwaiter().GetResult()
            $null = $Child.ErrorOutput.GetAwaiter().GetResult()
        }
        finally {
            $Child.OutputStream.Dispose()
            $Child.ErrorStream.Dispose()
        }

        $suite = [ordered]@{
            Label = $Child.Test.label
            Project = $Child.Test.project
            Assembly = $Child.Test.assembly
            TargetFramework = $workloadDefinition.targetFramework
            RuntimePolicy = $Child.Test.runtime
            Runtime = $Child.Runtime
            RollForward = $Child.RollForward
            Tool = $Child.Tool
            BazelRuntime = if ($Child.Test.runtime -ceq 'sdk') { 'SDK-bundled' } else { $workloadDefinition.selfContainedRuntime }
            ExitCode = if ($Child.TimedOut) { -1 } else { $Child.Process.ExitCode }
            TimedOut = $Child.TimedOut
            Seconds = [math]::Round($Child.Watch.Elapsed.TotalSeconds, 3)
            Total = $null; Executed = $null; Passed = $null; Failed = $null; Skipped = $null; Other = $null
            SelectionHash = $null; RawSelectionHash = $null; OutcomeHash = $null
            Trx = $Child.Trx; TrxSha256 = $null
            Stdout = $Child.Stdout; Stderr = $Child.Stderr
            Success = $false
            Error = $null
        }
        $children.Remove($Child) | Out-Null
        $Child.Process.Dispose()

        try {
            if ($Child.TimedOut) {
                throw "Exceeded the $TestTimeoutSeconds-second limit."
            }

            if (-not (Test-Path -LiteralPath $Child.Trx -PathType Leaf)) {
                throw "No TRX was produced (exit code $($suite.ExitCode))."
            }

            $parsed = Read-BazelDogfoodTrx -Path $Child.Trx -IdentityPolicy $workloadDefinition.identityPolicy
            $outcomes = [string[]] @($parsed.Cases | ForEach-Object { "$($_.Key)|$($_.Outcome)" })
            [Array]::Sort($outcomes, [StringComparer]::Ordinal)
            $suite.Total = $parsed.Total; $suite.Executed = $parsed.Executed; $suite.Passed = $parsed.Passed
            $suite.Failed = $parsed.Failed; $suite.Skipped = $parsed.Skipped; $suite.Other = $parsed.Other
            $suite.SelectionHash = $parsed.SelectionHash; $suite.RawSelectionHash = $parsed.RawSelectionHash
            $suite.OutcomeHash = Get-TextHash ($outcomes -join "`n")
            $suite.TrxSha256 = (Get-FileHash -LiteralPath $Child.Trx -Algorithm SHA256).Hash
            if ($suite.ExitCode -ne 0) {
                throw "The test host exited $($suite.ExitCode)."
            }

            if ($parsed.Failed -gt 0 -or $parsed.Other -gt 0 -or $parsed.Passed -eq 0) {
                throw "Failed=$($parsed.Failed), Other=$($parsed.Other), Passed=$($parsed.Passed)."
            }

            $suite.Success = $true
        }
        catch {
            $suite.Error = $_.Exception.Message
        }

        $state = if ($suite.Success) { 'passed' } else { "FAILED: $($suite.Error)" }
        Write-Host "$($suite.Label): $state ($($suite.Seconds)s, $($suite.Passed)/$($suite.Total) passed)"
        $suites.Add([pscustomobject] $suite)
    }

    $pending = [Collections.Generic.Queue[object]]::new()
    foreach ($test in $selection) {
        $pending.Enqueue($test)
    }

    $running = [Collections.Generic.List[object]]::new()
    $testWatch = [Diagnostics.Stopwatch]::StartNew()
    while ($pending.Count -or $running.Count) {
        while ($pending.Count -and $running.Count -lt $TestJobs) {
            $running.Add((Start-Suite $pending.Dequeue()))
        }

        foreach ($child in @($running.ToArray())) {
            if (-not $child.Process.HasExited -and $child.Watch.Elapsed.TotalSeconds -gt $TestTimeoutSeconds) {
                $child.TimedOut = $true
                $child.Process.Kill($true)
            }

            if ($child.Process.HasExited) {
                Complete-Suite $child
                $running.Remove($child) | Out-Null
            }
        }

        if ($running.Count) {
            Start-Sleep -Milliseconds 100
        }
    }

    $testWatch.Stop()
    $report.TestSeconds = [math]::Round($testWatch.Elapsed.TotalSeconds, 3)
    $report.Success = $suites.Count -eq $selection.Count -and @($suites | Where-Object { -not $_.Success }).Count -eq 0
    if (-not $report.Success) {
        $report.Errors = @($suites | Where-Object { -not $_.Success } | ForEach-Object { "$($_.Label): $($_.Error)" })
    }
}
catch {
    $report.Success = $false
    $report.Errors = @($report.Errors) + $_.Exception.Message
    Write-Host "BuildXL unit-test run failed: $($_.Exception.Message)"
}
finally {
    foreach ($child in $children.ToArray()) {
        if (-not $child.Process.HasExited) {
            $child.Process.Kill($true)
        }

        $child.Process.WaitForExit()
        $child.OutputStream.Dispose()
        $child.ErrorStream.Dispose()
    }

    $report.Suites = $suites.ToArray()
    if (Test-Path -LiteralPath $evidence -PathType Container) {
        $report | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $evidence 'unit-test-timings.json') -Encoding utf8

        $lines = [Collections.Generic.List[string]]::new()
        $lines.Add("### BuildXL unit-test run ($Configuration): $(if ($report.Success) { 'passed' } else { 'FAILED' })")
        $lines.Add('')
        $lines.Add('| Phase | Seconds |')
        $lines.Add('| --- | ---: |')
        # eng\buildxl.ps1 -Pack runs Build;Pack as one MSBuild invocation, so packing is timed inside the Build phase.
        $packed = $null -ne $phases -and 'Targets' -in $phases.PSObject.Properties.Name -and
            @(([string] $phases.Targets) -split ';') -contains 'Pack'
        if ($null -ne $phases) {
            $lines.Add("| Restore | $($phases.RestoreSeconds) |")
            $lines.Add("| $(if ($packed) { 'Build (includes Pack)' } else { 'Build' }) | $($phases.BuildSeconds) |")
        }

        $lines.Add("| Runtime bootstrap (outside the test phase) | $($report.RuntimeInstallSeconds) |")
        $lines.Add("| Unit tests ($($suites.Count) suites, $TestJobs at a time) | $($report.TestSeconds) |")
        $lines.Add("| Pack | $(if ($packed) { 'included in Build' } else { 'not run' }) |")
        $lines.Add('')
        $lines.Add('| Suite | Runtime | Seconds | Total | Passed | Failed | Skipped | Selection hash |')
        $lines.Add('| --- | --- | ---: | ---: | ---: | ---: | ---: | --- |')
        foreach ($suite in @($suites | Sort-Object Label)) {
            $hash = if ($suite.SelectionHash) { $suite.SelectionHash.Substring(0, 12) } else { 'n/a' }
            $lines.Add("| $($suite.Label) | $($suite.Runtime) | $($suite.Seconds) | $($suite.Total) | $($suite.Passed) | $($suite.Failed) | $($suite.Skipped) | $hash |")
        }

        foreach ($errorText in @($report.Errors)) {
            $lines.Add('')
            $lines.Add("- $errorText")
        }

        $summary = Join-Path $evidence 'unit-test-summary.md'
        $lines | Set-Content -LiteralPath $summary -Encoding utf8
        if ($env:TF_BUILD) {
            Write-Host "##vso[task.uploadsummary]$summary"
        }

        Write-Host "Unit-test evidence: $evidence"
    }
}

exit $(if ($report.Success) { 0 } else { 1 })
