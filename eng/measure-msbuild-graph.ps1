<#
.SYNOPSIS
Compares classic and static-graph builds without changing the repository's build defaults.
.DESCRIPTION
Bootstrap and build NonWindowsTests.slnf with the repository build scripts first.
Each pair uses fresh, task-owned outputs, warmed package caches, identical options,
and alternating execution order. Timings include a cold-output restore/build,
a no-change build, and a timestamp-only source change. This does not benchmark pack or tests.
Only the experiment's bin/obj subdirectories are deleted; normal artifacts and caches are preserved.
Stops at the first failed build or output difference; partial results are not a successful comparison.
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
$sdkRoot = if ($env:DOTNET_GLOBAL_INSTALL_DIR) { $env:DOTNET_GLOBAL_INSTALL_DIR } else { Join-Path $repoRoot '.dotnet' }
$dotnet = Join-Path $sdkRoot $(if ($IsWindows) { 'dotnet.exe' } else { 'dotnet' })
if (-not (Test-Path -LiteralPath $dotnet)) {
    throw 'The repository-local SDK is unavailable. Bootstrap with the repository build scripts first.'
}
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

$packageCache = @(& $dotnet nuget locals global-packages --list --force-english-output)
if ($LASTEXITCODE -ne 0 -or $packageCache.Count -ne 1 -or -not $packageCache[0].StartsWith('global-packages: ')) {
    throw 'Unable to determine the effective NuGet package cache.'
}
# CI-mode Arcade evaluation otherwise defaults to .packages, unlike the bootstrap's global cache.
$env:NUGET_PACKAGES = $packageCache[0].Substring('global-packages: '.Length).Trim()
if (-not (Test-Path -LiteralPath $env:NUGET_PACKAGES -PathType Container)) {
    throw "The bootstrap's NuGet package cache is missing: $env:NUGET_PACKAGES"
}

$experimentRoot = Join-Path $repoRoot 'artifacts\msbuild-graph-performance'
$experimentId = [Guid]::NewGuid().ToString('N')
$resultRoot = Join-Path $experimentRoot $experimentId
$null = New-Item -ItemType Directory -Path $resultRoot
# Keep generated files under artifacts/obj so the repository's analyzer exclusions still apply.
$binRoot = Join-Path $repoRoot "artifacts\bin\msbuild-graph-performance\$experimentId"
$objRoot = Join-Path $repoRoot "artifacts\obj\msbuild-graph-performance\$experimentId"
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
    nugetPackages = $env:NUGET_PACKAGES
    msbuildVersion = (& $dotnet msbuild -version -nologo | Out-String).Trim()
    operatingSystem = [Runtime.InteropServices.RuntimeInformation]::OSDescription
    processors = [Environment]::ProcessorCount
    maxCpuCount = $MaxCpuCount
    iterations = $Iterations
    configuration = $Configuration
    solution = $solution
    selectedProjects = (Get-Content -LiteralPath (Join-Path $repoRoot $solution) -Raw | ConvertFrom-Json).solution.projects.Count
    binDirectory = $binRoot
    objDirectory = $objRoot
    continuousIntegrationBuild = ($env:GITHUB_ACTIONS -eq 'true')
    cacheState = 'Fresh outputs per mode/iteration; package, SDK and filesystem caches warmed by bootstrap.'
    sourceChange = 'Timestamp-only touch of src\TestFramework\TestFramework\Assertions\Assert.cs; bytes unchanged.'
    sourceTimestampUtc = $sourceTimestamp.ToString('O')
    sourceSha256 = (Get-FileHash -LiteralPath $sourceFile -Algorithm SHA256).Hash
    skipLegacyUap = $IsWindows
    startedUtc = [DateTime]::UtcNow.ToString('O')
    completedUtc = $null
    succeeded = $false
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
        "-property:ArtifactsBinDir=$binRoot$([IO.Path]::DirectorySeparatorChar)"
        "-property:ArtifactsObjDir=$objRoot$([IO.Path]::DirectorySeparatorChar)"
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
        Select-String -LiteralPath $logFile -Pattern ': (?:error|warning) [A-Z]+\d+' |
            Select-Object -First 5 | ForEach-Object { Write-Host $_.Line }
        Get-Content -LiteralPath $logFile -Tail 20 | Write-Host
        throw "$name failed with exit $exitCode; failed builds are not performance samples."
    }
    if (-not (Test-Path -LiteralPath $binlogFile) -or (Get-Item -LiteralPath $binlogFile).Length -eq 0) {
        throw "Missing or empty binary log for $name."
    }
}

function Get-OutputManifest {
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
            # These directories were created by this invocation, never borrowed from a normal build.
            foreach ($directory in @($binRoot, $objRoot)) {
                if (Test-Path -LiteralPath $directory) {
                    Remove-Item -LiteralPath $directory -Recurse -Force
                }
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
    $report.succeeded = $true
}
finally {
    [IO.File]::SetLastWriteTimeUtc($sourceFile, $sourceTimestamp)
    Pop-Location
    $report.completedUtc = [DateTime]::UtcNow.ToString('O')
    $validPairs = @($manifests | Where-Object matchesClassic | ForEach-Object { "$($_.mode)-$($_.iteration)" })
    $report.summary = @(
        $runs | Where-Object { $_.exitCode -eq 0 -and "$($_.mode)-$($_.iteration)" -in $validPairs } |
        Group-Object mode, scenario | ForEach-Object {
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
