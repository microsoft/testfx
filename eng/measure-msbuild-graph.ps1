<#
.SYNOPSIS
Compares classic and static-graph builds without changing the repository's build defaults.
.DESCRIPTION
Bootstrap and build NonWindowsTests.slnf with the repository build scripts first.
Each pair uses fresh, task-owned outputs, warmed package caches, identical options,
and alternating execution order. Timings include a cold-output restore/build,
a no-change build, and a timestamp-only source change. This does not benchmark pack or tests.
Only the experiment's work directory is deleted; normal artifacts and caches are preserved.
.EXAMPLE
pwsh -File eng\measure-msbuild-graph.ps1 -Iterations 3 -MaxCpuCount 8
#>
[CmdletBinding()]
param(
    [ValidateRange(1, 10)]
    [int] $Iterations = 3,
    [ValidateRange(1, 1024)]
    [int] $MaxCpuCount = [Math]::Min(8, [Environment]::ProcessorCount),
    [ValidateSet('Debug', 'Release')]
    [string] $Configuration = 'Release'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$PSNativeCommandUseErrorActionPreference = $false
$repoRoot = Split-Path -Parent $PSScriptRoot
$sdkRoot = (Get-Content -LiteralPath (Join-Path $repoRoot 'artifacts\toolset\sdk.txt') -Raw).Trim()
$dotnet = Join-Path $sdkRoot $(if ($IsWindows) { 'dotnet.exe' } else { 'dotnet' })
$env:DOTNET_ROOT = $sdkRoot
$env:DOTNET_INSTALL_DIR = $sdkRoot
$env:PATH = $sdkRoot + [IO.Path]::PathSeparator + $env:PATH
$sdkVersion = & $dotnet --version
if ($LASTEXITCODE -ne 0) {
    throw 'The repository-local SDK is unavailable. Bootstrap with the repository build scripts first.'
}

$globalJson = Get-Content -LiteralPath (Join-Path $repoRoot 'global.json') -Raw | ConvertFrom-Json
if ($sdkVersion -ne $globalJson.tools.dotnet) {
    throw "Expected pinned SDK $($globalJson.tools.dotnet), found $sdkVersion."
}

$experimentRoot = Join-Path $repoRoot 'artifacts\msbuild-graph-performance'
$resultRoot = Join-Path $experimentRoot ([Guid]::NewGuid().ToString('N'))
$null = New-Item -ItemType Directory -Path $resultRoot
$workRoot = Join-Path $resultRoot 'work'
$resultFile = Join-Path $resultRoot 'results.json'
$sourceFile = Join-Path $repoRoot 'src\TestFramework\TestFramework\Assertions\Assert.cs'
$sourceTimestamp = [IO.File]::GetLastWriteTimeUtc($sourceFile)
$runs = [Collections.Generic.List[object]]::new()
$manifests = [Collections.Generic.List[object]]::new()
$solution = 'NonWindowsTests.slnf'
$report = [ordered]@{
    sourceRevision = (& git -C $repoRoot rev-parse HEAD)
    sourceStatus = @(& git -C $repoRoot status --short)
    sdkVersion = $sdkVersion
    msbuildVersion = (& $dotnet msbuild -version -nologo | Out-String).Trim()
    operatingSystem = [Runtime.InteropServices.RuntimeInformation]::OSDescription
    processors = [Environment]::ProcessorCount
    maxCpuCount = $MaxCpuCount
    configuration = $Configuration
    solution = $solution
    selectedProjects = (Get-Content -LiteralPath (Join-Path $repoRoot $solution) -Raw | ConvertFrom-Json).solution.projects.Count
    continuousIntegrationBuild = ($env:GITHUB_ACTIONS -eq 'true')
    cacheState = 'Fresh outputs per mode/iteration; package, SDK and filesystem caches warmed by bootstrap.'
    sourceChange = 'Timestamp-only touch of src\TestFramework\TestFramework\Assertions\Assert.cs; bytes unchanged.'
    skipLegacyUap = $IsWindows
    startedUtc = [DateTime]::UtcNow.ToString('O')
    completedUtc = $null
    runs = $runs
    outputChecks = $manifests
    summary = @()
}

function Save-Report {
    $report | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath $resultFile -Encoding utf8
}

function Invoke-MeasuredBuild([string] $Mode, [int] $Iteration, [string] $Scenario) {
    $name = "$Iteration-$Mode-$Scenario"
    $logFile = Join-Path $resultRoot "$name.log"
    $binlogFile = Join-Path $resultRoot "$name.binlog"
    $buildArguments = @(
        'msbuild'
        (Join-Path $repoRoot $report.solution)
        '-target:Build'
        "-graphBuild:$($Mode -eq 'graph')"
        "-maxCpuCount:$MaxCpuCount"
        '-multithreaded'
        '-nodeReuse:false'
        '-nologo'
        '-verbosity:minimal'
        '-warnAsError'
        '-warnNotAsError:NU1901;NU1902;NU1903;NU1904'
        '-property:TreatWarningsAsErrors=true'
        '-property:AdditionalWarningsNotAsErrors=NU1901%3BNU1902%3BNU1903%3BNU1904'
        "-property:Configuration=$Configuration"
        "-property:ContinuousIntegrationBuild=$($report.continuousIntegrationBuild)"
        '-property:NonWindowsBuild=true'
        "-property:ArtifactsDir=$workRoot$([IO.Path]::DirectorySeparatorChar)"
        "-binaryLogger:$binlogFile;ProjectImports=None"
        '-detailedSummary'
    )
    if ($IsWindows) {
        $buildArguments += '-property:SkipUapTargetFramework=true'
    }
    if ($Scenario -eq 'cold-output-restore-build') {
        $buildArguments += '-restore'
    }

    Write-Host "Starting $name"
    $timer = [Diagnostics.Stopwatch]::StartNew()
    & $dotnet @buildArguments *> $logFile
    $exitCode = $LASTEXITCODE
    $timer.Stop()
    $run = [pscustomobject]@{
        mode = $Mode
        iteration = $Iteration
        scenario = $Scenario
        seconds = $timer.Elapsed.TotalSeconds
        exitCode = $exitCode
        command = '"' + $dotnet + '" ' + (($buildArguments | ForEach-Object { '"' + $_ + '"' }) -join ' ')
        logFile = "$name.log"
        binlogFile = "$name.binlog"
    }
    $runs.Add($run)
    Save-Report
    Write-Host ("Finished {0}: {1:N3}s, exit {2}" -f $name, $run.seconds, $exitCode)
    if ($exitCode -ne 0) {
        Get-Content -LiteralPath $logFile -Tail 65 | Write-Host
        throw "$name failed with exit $exitCode; failed builds are not performance samples."
    }
    if (-not (Test-Path -LiteralPath $binlogFile) -or (Get-Item -LiteralPath $binlogFile).Length -eq 0) {
        throw "Missing or empty binary log for $name."
    }
}

function Get-OutputManifest {
    $binRoot = Join-Path $workRoot 'bin'
    $files = @(Get-ChildItem -LiteralPath $binRoot -Recurse -File |
        Where-Object { $_.Extension -in '.dll', '.exe', '.pdb' -or $_.Name -like '*.deps.json' -or $_.Name -like '*.runtimeconfig.json' } |
        Sort-Object FullName)
    if ($files.Count -eq 0 -or -not ($files.Name -contains 'Microsoft.Testing.Platform.dll') -or
        -not ($files.Name -contains 'MSTest.TestFramework.dll')) {
        throw 'Required product assemblies are missing from the fresh output directory.'
    }
    foreach ($file in $files) {
        [pscustomobject]@{
            path = [IO.Path]::GetRelativePath($binRoot, $file.FullName)
            sha256 = (Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash
        }
    }
}

$referenceManifest = $null
Push-Location $repoRoot
try {
    for ($iteration = 1; $iteration -le $Iterations; $iteration++) {
        $modes = if ($iteration % 2 -eq 1) { @('classic', 'graph') } else { @('graph', 'classic') }
        foreach ($mode in $modes) {
            # This directory was created by this invocation, never borrowed from a normal build.
            if (Test-Path -LiteralPath $workRoot) {
                Remove-Item -LiteralPath $workRoot -Recurse -Force
            }
            Invoke-MeasuredBuild $mode $iteration 'cold-output-restore-build'
            Invoke-MeasuredBuild $mode $iteration 'no-change'
            try {
                [IO.File]::SetLastWriteTimeUtc($sourceFile, [DateTime]::UtcNow)
                Invoke-MeasuredBuild $mode $iteration 'source-touch'
            }
            finally {
                [IO.File]::SetLastWriteTimeUtc($sourceFile, $sourceTimestamp)
            }

            $manifest = @(Get-OutputManifest)
            $manifestFile = "$iteration-$mode.manifest.json"
            $manifest | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $resultRoot $manifestFile) -Encoding utf8
            if ($null -eq $referenceManifest) {
                $referenceManifest = $manifest
            }
            $differences = @(Compare-Object -ReferenceObject $referenceManifest -DifferenceObject $manifest -Property path, sha256)
            $manifests.Add([pscustomobject]@{
                mode = $mode
                iteration = $iteration
                fileCount = $manifest.Count
                matchesClassic = ($differences.Count -eq 0)
                manifestFile = $manifestFile
                differences = $differences
            })
            Save-Report
            if ($differences.Count -ne 0) {
                throw "Output hashes differ from the first classic build. Inspect $manifestFile; speed alone is insufficient."
            }
        }
    }
}
finally {
    [IO.File]::SetLastWriteTimeUtc($sourceFile, $sourceTimestamp)
    Pop-Location
    $report.completedUtc = [DateTime]::UtcNow.ToString('O')
    $report.summary = @(
        $runs | Where-Object exitCode -EQ 0 | Group-Object mode, scenario | ForEach-Object {
            $values = @($_.Group.seconds | Sort-Object)
            $middle = [int][Math]::Floor($values.Count / 2)
            $median = if ($values.Count % 2 -eq 0) { ($values[$middle - 1] + $values[$middle]) / 2 } else { $values[$middle] }
            [pscustomobject]@{
                mode = $_.Group[0].mode
                scenario = $_.Group[0].scenario
                count = $values.Count
                medianSeconds = $median
                minSeconds = $values[0]
                maxSeconds = $values[-1]
            }
        }
    )
    Save-Report
    $report.summary | Format-Table -AutoSize | Out-Host
    Write-Host "Results: $resultFile"
}
