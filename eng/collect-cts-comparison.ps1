[CmdletBinding()]
param(
    [switch]$SelfTest,
    [string]$Repository = (Split-Path $PSScriptRoot -Parent),
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Release",
    [int]$DegreeOfParallelism = [Environment]::ProcessorCount,
    [string]$ResultsDirectory
)

$ErrorActionPreference = "Stop"
. (Join-Path $PSScriptRoot "collection-test-modules.ps1")

function Invoke-CTSModuleSelectionSelfTest {
    $fixture = Join-Path -Path ([System.IO.Path]::GetTempPath()) -ChildPath ("testfx-cts-selection-" + [guid]::NewGuid().ToString("N"))
    New-Item -ItemType Directory -Path $fixture | Out-Null
    try {
        foreach ($framework in @("net8.0", "net9.0", "net462")) {
            $directory = Join-Path -Path $fixture -ChildPath "artifacts" -AdditionalChildPath @("bin", "Example.UnitTests", "Release", $framework)
            New-Item -ItemType Directory -Path $directory -Force | Out-Null
            Set-Content -LiteralPath (Join-Path $directory "Example.UnitTests.exe") -Value "same apphost"
            Set-Content -LiteralPath (Join-Path $directory "Microsoft.Testing.Extensions.AffectedTests.dll") -Value "provider"
            if ($framework -ne "net462") {
                Set-Content -LiteralPath (Join-Path $directory "Example.UnitTests.dll") -Value $framework
                Set-Content -LiteralPath (Join-Path $directory "Example.UnitTests.runtimeconfig.json") -Value "{}"
            }
        }

        $modules = @(Get-CTSComparisonModule $fixture "Release")
        $expected = @(
            "artifacts/bin/Example.UnitTests/Release/net462/Example.UnitTests.exe",
            "artifacts/bin/Example.UnitTests/Release/net8.0/Example.UnitTests.dll",
            "artifacts/bin/Example.UnitTests/Release/net9.0/Example.UnitTests.dll"
        )
        if (($modules.RelativePath -join "`n") -ne ($expected -join "`n")) {
            throw "ManagedModulesAvoidApphostDeduplication: expected all three framework modules, using DLLs for .NET and an EXE for .NET Framework."
        }

        Write-Output "PASS ManagedModulesAvoidApphostDeduplication"

        $helper = Join-Path -Path $fixture -ChildPath "artifacts" -AdditionalChildPath @("bin", "Example.UnitTests", "Release", "net8.0", "TestHelper.exe")
        Set-Content -LiteralPath $helper -Value "helper"
        if (@(Get-CTSComparisonModule $fixture "Release").Count -ne 3) {
            throw "HelpersAreNotTestModules: helper executables must not participate."
        }

        Write-Output "PASS HelpersAreNotTestModules"

        $unregistered = Join-Path -Path $fixture -ChildPath "artifacts" -AdditionalChildPath @("bin", "Other.UnitTests", "Release", "net8.0")
        New-Item -ItemType Directory -Path $unregistered -Force | Out-Null
        Set-Content -LiteralPath (Join-Path $unregistered "Other.UnitTests.exe") -Value "unregistered"
        if (@(Get-CTSComparisonModule $fixture "Release").Count -ne 3) {
            throw "OnlyAffectedTestCapableModulesParticipate: unrelated test hosts must not change the comparison scope."
        }

        Write-Output "PASS OnlyAffectedTestCapableModulesParticipate"

        $rejected = $false
        try {
            Get-CTSComparisonModule $fixture "Debug" | Out-Null
        } catch {
            if (-not $_.Exception.Message.StartsWith("No affected-test-capable Debug test modules")) {
                throw
            }

            $rejected = $true
        }

        if (-not $rejected) {
            throw "EmptySelectionIsRejected: a missing configuration must not produce a successful zero-module comparison."
        }

        Write-Output "PASS EmptySelectionIsRejected"
        Write-Output "CTS module-selection self-tests: 4 passed."
    } finally {
        Remove-Item -LiteralPath $fixture -Recurse -Force
    }
}

function Assert-CTSComparisonArtifact {
    param(
        [object[]]$Modules,
        [string]$ResultsDirectory,
        [string]$MapDirectory
    )

    $reports = @(Get-ChildItem -LiteralPath $ResultsDirectory -Filter "*.trx" -File)
    $committedMaps = @(Get-ChildItem -LiteralPath $MapDirectory -Filter "committed" -Recurse -File)
    if ($reports.Count -ne $Modules.Count -or $committedMaps.Count -ne $Modules.Count) {
        throw "CTS comparison is incomplete: $($Modules.Count) module inputs, $($reports.Count) TRX reports, $($committedMaps.Count) committed maps."
    }

    $expectedMapModules = [System.Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
    foreach ($module in $Modules) {
        if ((Get-FileHash -LiteralPath $module.Path -Algorithm SHA256).Hash -ne $module.SHA256) {
            throw "CTS comparison input '$($module.Path)' changed after module selection."
        }
        $relativePath = $module.RelativePath.Replace('/', [System.IO.Path]::DirectorySeparatorChar)
        $nativeModule = [System.IO.Path]::GetFullPath((Join-Path (Join-Path $MapDirectory "K1") $relativePath))
        if (-not $expectedMapModules.Add($nativeModule)) {
            throw "CTS comparison input contains duplicate module identity '$($module.RelativePath)'."
        }
    }

    $mappedModules = [System.Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
    foreach ($map in $committedMaps) {
        $tagDirectory = $map.Directory.Parent
        $moduleDirectory = $tagDirectory.Parent
        if ($tagDirectory.Name -ne "testfx-cts-comparison" -or
            -not $expectedMapModules.Contains($moduleDirectory.FullName)) {
            throw "CTS comparison map '$($map.FullName)' does not identify an expected input module."
        }
        if (-not $mappedModules.Add($moduleDirectory.FullName)) {
            throw "CTS comparison map duplicates input module '$($moduleDirectory.FullName)'."
        }
    }
    if (-not $mappedModules.SetEquals($expectedMapModules)) {
        throw "CTS comparison map inventory does not contain every input module exactly once."
    }

    $reportedModules = @{}
    foreach ($report in $reports) {
        [xml]$trx = Get-Content -LiteralPath $report.FullName -Raw
        $sources = @($trx.TestRun.TestDefinitions.UnitTest.TestMethod.codeBase | Sort-Object -Unique)
        $results = @($trx.TestRun.Results.UnitTestResult)
        if ($sources.Count -ne 1 -or $results.Count -eq 0 -or
            [int]$trx.TestRun.ResultSummary.Counters.total -ne $results.Count -or
            $reportedModules.ContainsKey($sources[0])) {
            throw "CTS comparison report '$($report.FullName)' does not identify one unique module with consistent test results."
        }

        $reportedModules[$sources[0]] = $true
        $timingAvailable = @($results | Where-Object {
            [datetimeoffset]$_.startTime -eq [datetimeoffset]::MinValue -or
            [datetimeoffset]$_.endTime -eq [datetimeoffset]::MinValue
        }).Count -eq 0
        [pscustomobject]@{
            ModulePath = $sources[0]
            Reported = [int]$trx.TestRun.ResultSummary.Counters.total
            Executed = [int]$trx.TestRun.ResultSummary.Counters.executed
            Passed = [int]$trx.TestRun.ResultSummary.Counters.passed
            Failed = [int]$trx.TestRun.ResultSummary.Counters.failed
            NotExecuted = [int]$trx.TestRun.ResultSummary.Counters.notExecuted
            TestTimestampsAvailable = $timingAvailable
        }
    }

    foreach ($module in $Modules) {
        if (-not $reportedModules.ContainsKey($module.Path)) {
            throw "CTS comparison did not report input module '$($module.Path)'."
        }
    }
}

function Invoke-CTSArtifactSelfTest {
    $fixture = Join-Path -Path ([System.IO.Path]::GetTempPath()) -ChildPath ("testfx-cts-artifacts-" + [guid]::NewGuid().ToString("N"))
    $resultsDirectory = Join-Path $fixture "results"
    $mapDirectory = Join-Path $fixture "maps"
    New-Item -ItemType Directory -Path $resultsDirectory, $mapDirectory -Force | Out-Null
    try {
        $modulePath = Join-Path $fixture "Example.UnitTests.dll"
        Set-Content -LiteralPath $modulePath -Value "first module"
        $modules = @([pscustomobject]@{
            Path = $modulePath
            RelativePath = "Example.UnitTests.dll"
            SHA256 = (Get-FileHash -LiteralPath $modulePath -Algorithm SHA256).Hash
        })
        $reportPath = Join-Path $resultsDirectory "example.trx"
        $committedPath = Join-Path $mapDirectory "K1\Example.UnitTests.dll\testfx-cts-comparison\first-run\committed"
        New-Item -ItemType Directory -Path (Split-Path $committedPath -Parent) -Force | Out-Null
        $escapedPath = [System.Security.SecurityElement]::Escape($modulePath)
        $report = @"
<TestRun>
  <Results><UnitTestResult outcome="Passed" startTime="2026-10-31T10:00:00+00:00" endTime="2026-10-31T10:00:01+00:00" /></Results>
  <TestDefinitions><UnitTest><TestMethod codeBase="$escapedPath" /></UnitTest></TestDefinitions>
  <ResultSummary><Counters total="1" executed="1" passed="1" failed="0" notExecuted="0" /></ResultSummary>
</TestRun>
"@
        Set-Content -LiteralPath $reportPath -Value $report
        New-Item -ItemType File -Path $committedPath | Out-Null
        $summary = @(Assert-CTSComparisonArtifact -Modules $modules -ResultsDirectory $resultsDirectory -MapDirectory $mapDirectory)
        if ($summary.Count -ne 1 -or $summary[0].Executed -ne 1 -or -not $summary[0].TestTimestampsAvailable) {
            throw "CTSCompleteArtifactsAreVerified: expected one executed test with valid timestamps."
        }

        Write-Output "PASS CTSCompleteArtifactsAreVerified"
        foreach ($case in @(
            @{ Name = "CTSMissingReportIsRejected"; File = $reportPath; Text = $null; Failure = "CTS comparison is incomplete:" },
            @{ Name = "CTSUncommittedMapIsRejected"; File = $committedPath; Text = $null; Failure = "CTS comparison is incomplete:" },
            @{ Name = "CTSUnexpectedReportModuleIsRejected"; File = $reportPath; Text = $report.Replace($escapedPath, "other-module.dll"); Failure = "CTS comparison did not report input module" }
        )) {
            if ($null -eq $case.Text) {
                Remove-Item -LiteralPath $case.File
            } else {
                Set-Content -LiteralPath $case.File -Value $case.Text
            }

            $rejected = $false
            try {
                Assert-CTSComparisonArtifact -Modules $modules -ResultsDirectory $resultsDirectory -MapDirectory $mapDirectory | Out-Null
            } catch {
                if (-not $_.Exception.Message.StartsWith($case.Failure)) {
                    throw
                }

                $rejected = $true
            }

            if (-not $rejected) {
                throw "Self-test '$($case.Name)' accepted incomplete CTS artifacts."
            }

            Write-Output "PASS $($case.Name)"
            Set-Content -LiteralPath $reportPath -Value $report
            if (-not (Test-Path -LiteralPath $committedPath)) {
                New-Item -ItemType File -Path $committedPath | Out-Null
            }
        }

        $missingTiming = $report.Replace("2026-10-31T10:00:00+00:00", "0001-01-01T00:00:00+00:00")
        Set-Content -LiteralPath $reportPath -Value $missingTiming
        $summary = @(Assert-CTSComparisonArtifact -Modules $modules -ResultsDirectory $resultsDirectory -MapDirectory $mapDirectory)
        if ($summary[0].TestTimestampsAvailable) {
            throw "CTSMissingTimestampsAreReported: unavailable timing must not appear valid."
        }

        Write-Output "PASS CTSMissingTimestampsAreReported"
        Set-Content -LiteralPath $reportPath -Value $report
        $secondPath = Join-Path $fixture "Other.UnitTests.dll"
        Set-Content -LiteralPath $secondPath -Value "second module"
        $modules += [pscustomobject]@{
            Path = $secondPath
            RelativePath = "Other.UnitTests.dll"
            SHA256 = (Get-FileHash -LiteralPath $secondPath -Algorithm SHA256).Hash
        }
        Set-Content -LiteralPath (Join-Path $resultsDirectory "other.trx") `
            -Value $report.Replace($escapedPath, [System.Security.SecurityElement]::Escape($secondPath))
        $secondMap = Join-Path $mapDirectory "K1\Other.UnitTests.dll\testfx-cts-comparison\first-run\committed"
        New-Item -ItemType Directory -Path (Split-Path $secondMap -Parent) -Force | Out-Null
        New-Item -ItemType File -Path $secondMap | Out-Null
        if (@(Assert-CTSComparisonArtifact -Modules $modules -ResultsDirectory $resultsDirectory -MapDirectory $mapDirectory).Count -ne 2) {
            throw "CTSOneMapPerModuleIsAccepted: expected two independently identified maps."
        }
        Write-Output "PASS CTSOneMapPerModuleIsAccepted"
        Remove-Item -LiteralPath $secondMap
        $mapFailures = [System.Collections.Generic.List[string]]::new()
        foreach ($case in @(
            @{ Name = "CTSDuplicateMapCannotReplaceMissingModule"; RelativePath = "Example.UnitTests.dll" },
            @{ Name = "CTSUnexpectedMapCannotReplaceMissingModule"; RelativePath = "Unexpected.UnitTests.dll" }
        )) {
            $replacement = Join-Path $mapDirectory ("K1\" + $case.RelativePath + "\testfx-cts-comparison\replacement-run\committed")
            New-Item -ItemType Directory -Path (Split-Path $replacement -Parent) -Force | Out-Null
            New-Item -ItemType File -Path $replacement | Out-Null
            try {
                $rejected = $false
                try {
                    Assert-CTSComparisonArtifact -Modules $modules -ResultsDirectory $resultsDirectory -MapDirectory $mapDirectory | Out-Null
                } catch {
                    if (-not $_.Exception.Message.StartsWith("CTS comparison map")) {
                        throw
                    }
                    $rejected = $true
                }
                if (-not $rejected) {
                    throw "$($case.Name): two committed markers must not hide a module with no map."
                }
                Write-Output "PASS $($case.Name)"
            } catch {
                $mapFailures.Add($_.Exception.Message)
                Write-Output "FAIL $($case.Name): $($_.Exception.Message)"
            } finally {
                Remove-Item -LiteralPath $replacement
            }
        }
        New-Item -ItemType File -Path $secondMap | Out-Null
        Set-Content -LiteralPath $secondPath -Value "changed module"
        $rejected = $false
        try {
            Assert-CTSComparisonArtifact -Modules $modules -ResultsDirectory $resultsDirectory -MapDirectory $mapDirectory | Out-Null
        } catch {
            if (-not $_.Exception.Message.StartsWith("CTS comparison input")) {
                throw
            }
            $rejected = $true
        }
        if ($rejected) {
            Write-Output "PASS CTSChangedInputHashIsRejected"
        } else {
            $mapFailures.Add("CTSChangedInputHashIsRejected: stale module fingerprints must not validate.")
            Write-Output "FAIL CTSChangedInputHashIsRejected: stale module fingerprints must not validate."
        }
        if ($mapFailures.Count -gt 0) {
            throw ($mapFailures -join [Environment]::NewLine)
        }
        Write-Output "CTS artifact self-tests: 9 passed."
    } finally {
        Remove-Item -LiteralPath $fixture -Recurse -Force
    }
}

if ($SelfTest) {
    Invoke-CTSModuleSelectionSelfTest
    Invoke-CTSArtifactSelfTest
    exit 0
}

if ($DegreeOfParallelism -le 0) {
    throw "CTS degree of parallelism must be greater than zero."
}

foreach ($name in @("DOTNET_CLI_ENABLE_AFFECTED_TESTS", "DOTNET_CLI_TEST_AFFECTED_TESTS_MODE", "TESTINGPLATFORM_EXITCODE_IGNORE")) {
    if (-not [string]::IsNullOrEmpty([Environment]::GetEnvironmentVariable($name))) {
        throw "CTS comparison must not inherit '$name' from affected-test execution."
    }
}

$Repository = [System.IO.Path]::GetFullPath($Repository)
$modules = @(Get-CTSComparisonModule $Repository $Configuration)
$manifestDirectory = Join-Path -Path $Repository -ChildPath ".config" -AdditionalChildPath "cts"
$manifest = Get-Content -LiteralPath (Join-Path $manifestDirectory "dotnet-tools.json") -Raw | ConvertFrom-Json
$resultsRoot = if ([string]::IsNullOrEmpty($ResultsDirectory)) {
    Join-Path -Path $Repository -ChildPath "artifacts" -AdditionalChildPath @("TestResults", $Configuration, "CTS")
} else {
    [System.IO.Path]::GetFullPath($ResultsDirectory)
}
$runDirectory = Join-Path $resultsRoot ([guid]::NewGuid().ToString("N"))
$mapDirectory = Join-Path $runDirectory "maps"
$resultsDirectory = Join-Path $runDirectory "results"
$logsDirectory = Join-Path $runDirectory "logs"
New-Item -ItemType Directory -Path $mapDirectory, $resultsDirectory, $logsDirectory -Force | Out-Null
$configFile = Join-Path $runDirectory "cts-config.json"
@{
    Modules = @{ Include = @(".*"); Exclude = @() }
    SourceCodeFiles = @{ Include = @("**/*.cs", "**/*.fs", "**/*.vb"); Exclude = @() }
    Files = @{ Exclude = @("artifacts/**", ".dotnet/**", ".packages/**") }
    Filter = @{ Include = @($modules.RelativePath); Exclude = @() }
} | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $configFile -Encoding utf8

$sourceRevision = & git -C $Repository rev-parse HEAD
if ($LASTEXITCODE -ne 0) {
    throw "Could not read the CTS comparison source revision."
}

$arguments = @(
    "collect", "testingplatform",
    "--rootPath", $Repository,
    "--config", $configFile,
    "--storage-type", "filesystem",
    "--storage-type-filesystem-dir", $mapDirectory,
    "--tag", "testfx-cts-comparison",
    "--dop", $DegreeOfParallelism,
    "--parallelize-same-module-batches",
    "--results-directory", $resultsDirectory,
    "--logs-directory", $logsDirectory,
    "--report-trx"
)
Write-Host "CTS $($manifest.tools.cts.version): $($modules.Count) module/framework inputs, $DegreeOfParallelism workers."
Write-Host "CTS comparison artifacts: $runDirectory"
$exitCode = 1
$nativeExitCode = $null
$reportSummaries = @()
$stopwatch = [System.Diagnostics.Stopwatch]::StartNew()
Push-Location $manifestDirectory
try {
    & dotnet tool run cts -- @arguments 2>&1 | Tee-Object -FilePath (Join-Path $logsDirectory "cts-console.log")
    $exitCode = $LASTEXITCODE
    $nativeExitCode = $exitCode
    $stopwatch.Stop()
    if ($exitCode -eq 0) {
        $exitCode = 1
        $reportSummaries = @(Assert-CTSComparisonArtifact -Modules $modules -ResultsDirectory $resultsDirectory -MapDirectory $mapDirectory)
        if (@($reportSummaries | Where-Object { -not $_.TestTimestampsAvailable }).Count -gt 0) {
            Write-Warning "CTS did not receive per-test UTC timestamps. Wall-clock duration and test results remain available, but its setup-cost-aware batching metrics are not reliable."
        }

        $exitCode = 0
    }
} finally {
    $stopwatch.Stop()
    Pop-Location
    @{
        SourceRevision = $sourceRevision
        ExpectedCTSVersion = $manifest.tools.cts.version
        Configuration = $Configuration
        DegreeOfParallelism = $DegreeOfParallelism
        ParallelizeSameModuleBatches = $true
        ModuleCount = $modules.Count
        Modules = $modules
        Arguments = $arguments
        NativeExitCode = $nativeExitCode
        ExitCode = $exitCode
        Reports = $reportSummaries
        DurationSeconds = $stopwatch.Elapsed.TotalSeconds
    } | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $runDirectory "comparison.json") -Encoding utf8
    Write-Host "CTS comparison exit: $exitCode; collection duration: $($stopwatch.Elapsed)."
}

exit $exitCode
