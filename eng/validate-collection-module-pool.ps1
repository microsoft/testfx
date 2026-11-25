[CmdletBinding()]
param()

$ErrorActionPreference = "Stop"
. (Join-Path $PSScriptRoot "collection-module-pool.ps1")
$script:passedCheckCount = 0
function Write-PassedCheck([string]$Name) {
    $script:passedCheckCount++
    Write-Output "PASS $Name"
}

$fixture = Join-Path -Path ([System.IO.Path]::GetTempPath()) -ChildPath ("testfx-module-pool-" + [guid]::NewGuid().ToString("N"))
New-Item -ItemType Directory -Path $fixture | Out-Null
try {
    $sharedTemplate = Get-Content -LiteralPath (Join-Path $PSScriptRoot "pipelines/steps/test-affected-tests.yml") -Raw
    $entryScript = Get-Content -LiteralPath (Join-Path $PSScriptRoot "collect-affected-test-maps.ps1") -Raw
    $poolScript = Get-Content -LiteralPath (Join-Path $PSScriptRoot "collection-module-pool.ps1") -Raw
    $fixtureScript = Get-Content -LiteralPath (Join-Path (Split-Path $PSScriptRoot -Parent) "test/IntegrationTests/Microsoft.Testing.Platform.Acceptance.IntegrationTests/Helpers/AcceptanceFixture.cs") -Raw
    if (-not $sharedTemplate.Contains("if (`$affectedTestsMode -eq 'collect' -and `$IsWindows)") -or
        -not $sharedTemplate.Contains('& .\eng\collect-affected-test-maps.ps1 -Configuration') -or
        -not $entryScript.Contains('Get-CollectionModuleConcurrency') -or
        -not $poolScript.Contains('Floor($effectiveBudget / $batchesPerModule)') -or
        -not $entryScript.Contains('$result.TeardownConfirmed') -or
        -not $entryScript.Contains('Get-OrderedCollectionModule') -or
        -not $entryScript.Contains('Get-MappableCollectionModule') -or
        -not $fixtureScript.Contains('CollectionPackageCache.Select(')) {
        throw "CollectionPoolWiringPreservesBudgets: Windows collection must use the bounded pool, prioritize known long workloads and retain the cache opt-in contract."
    }
    Write-PassedCheck "CollectionPoolWiringPreservesBudgets"

    foreach ($case in @(
        @{ CPUs = 1; Budget = 4; Batches = $null; Slots = 1; Effective = 1 },
        @{ CPUs = 2; Budget = 4; Batches = $null; Slots = 1; Effective = 2 },
        @{ CPUs = 3; Budget = 4; Batches = 2; Slots = 1; Effective = 3 },
        @{ CPUs = 4; Budget = 4; Batches = 2; Slots = 2; Effective = 4 },
        @{ CPUs = 16; Budget = 4; Batches = 2; Slots = 2; Effective = 4 },
        @{ CPUs = 16; Budget = 1; Batches = 1; Slots = 1; Effective = 1 },
        @{ CPUs = 4; Budget = 2; Batches = 1; Slots = 2; Effective = 2 }
    )) {
        $limit = Get-CollectionModuleConcurrency -ConfiguredBatches $case.Batches -ChildBudget $case.Budget `
            -MaximumParallelModules 2 -ProcessorCount $case.CPUs
        if ($limit.MaximumParallelModules -ne $case.Slots -or $limit.ChildBudget -ne $case.Effective -or
            $limit.MaximumParallelModules * $limit.BatchesPerModule -gt $limit.ChildBudget) {
            throw "ModulePoolNeverMultipliesChildBudget: CPU and configured per-module limits must fit the global child budget."
        }
    }
    Write-PassedCheck "ModulePoolNeverMultipliesChildBudget"

    foreach ($batches in @(0, 3, -1, 2.5, "2", $true, $null)) {
        $rejected = $false
        try {
            Get-CollectionModuleConcurrency -ConfiguredBatches $batches -ChildBudget 1 `
                -MaximumParallelModules 2 -ProcessorCount 4 | Out-Null
        } catch {
            $rejected = $true
        }
        if (-not $rejected) {
            throw "ModulePoolRejectsInvalidOrOversubscribedConfiguration: invalid values or a default two-child module must not fit a one-child budget."
        }
    }
    Write-PassedCheck "ModulePoolRejectsInvalidOrOversubscribedConfiguration"

    $ordered = @(Get-OrderedCollectionModule -Modules @(
        [pscustomobject]@{ Project = "Small.UnitTests"; RelativePath = "a" },
        [pscustomobject]@{ Project = "MSTest.Analyzers.UnitTests"; RelativePath = "b" },
        [pscustomobject]@{ Project = "MSTest.Acceptance.IntegrationTests"; RelativePath = "c" },
        [pscustomobject]@{ Project = "Microsoft.Testing.Platform.Acceptance.IntegrationTests"; RelativePath = "d" }
    ))
    if (($ordered.RelativePath -join ",") -ne "c,d,b,a") {
        throw "ModulePoolStartsLargestKnownWorkloadsFirst: long acceptance/analyzer work must not be delayed behind small modules."
    }
    Write-PassedCheck "ModulePoolStartsLargestKnownWorkloadsFirst"

    $mappable = @(Get-MappableCollectionModule -Modules @(
        [pscustomobject]@{ Project = "MSTestAdapter.PlatformServices.UnitTests"; Framework = "net462"; RelativePath = "a" },
        [pscustomobject]@{ Project = "MSTestAdapter.PlatformServices.UnitTests"; Framework = "net48"; RelativePath = "b" },
        [pscustomobject]@{ Project = "MSTestAdapter.PlatformServices.UnitTests"; Framework = "net10.0-windows10.0.18362.0"; RelativePath = "c" },
        [pscustomobject]@{ Project = "MSTestAdapter.PlatformServices.UnitTests"; Framework = "net10.0"; RelativePath = "d" },
        [pscustomobject]@{ Project = "TestFramework.UnitTests"; Framework = "net10.0-windows10.0.18362.0"; RelativePath = "e" },
        [pscustomobject]@{ Project = "MSTestAdapter.PlatformServices.UnitTests"; Framework = "net11.0"; RelativePath = "f" }
    ) 6>$null)
    if (($mappable.RelativePath -join ",") -ne "e,f") {
        throw "ModulePoolSkipsOnlyOversizedMaps: only the module/TFM maps that exceed supported size limits may be skipped."
    }
    Write-PassedCheck "ModulePoolSkipsOnlyOversizedMaps"

    $mtpAcceptanceContext = "[affected-tests] [Microsoft.Testing.Platform.Acceptance.IntegrationTests | .NETCoreApp,Version=v12.0 | .NET 12.0.0-alpha.1.26509.101 | win-x64]"
    $sampleContext = "[affected-tests] [Sample.Tests | .NETCoreApp,Version=v12.0 | .NET 12.0.0-alpha.1.26570.125 | win-x64]"
    $analyzersModule = [pscustomobject]@{
        Project = "MSTest.Analyzers.UnitTests"
        Framework = "net10.0"
        RelativePath = "artifacts/bin/MSTest.Analyzers.UnitTests/Release/net10.0/MSTest.Analyzers.UnitTests.dll"
    }
    $analyzersContext = "[affected-tests] [MSTest.Analyzers.UnitTests | .NETCoreApp,Version=v10.0 | .NET 10.0.15 | win-x64]"
    $analyzersMap = "azdo://public/1695348/TestFx_AffectedTestsMaps/affected-tests/v2/6e9a61bde3d41e2c75a44a898550f5810ff47bba60ae06c2fdad553d84a8a654/17763394b8af3d4972f53efc536a64adb62ea1da90b77552fcc0769de2e16ec1.json"
    $outputCases = @(
        @{
            Name = "ModuleOutputReplacesModuleContextWithProjectAndFramework"
            Lines = @("$sampleContext Collector error: Pipe is broken.", "   at System.IO.Pipes.PipeStream.WriteCore()")
            Expected = @("[Sample.Tests net12.0] Collector error: Pipe is broken.", "[Sample.Tests net12.0]    at System.IO.Pipes.PipeStream.WriteCore()")
        },
        @{
            Name = "ModuleOutputDoesNotRepeatAffectedTestContext"
            Module = [pscustomobject]@{ Project = "Microsoft.Testing.Platform.Acceptance.IntegrationTests"; Framework = "net12.0" }
            Lines = @("$mtpAcceptanceContext Test-map collection child failed: MSTest v5.0.0-preview.26553.4")
            Expected = @("[Microsoft.Testing.Platform.Acceptance.IntegrationTests net12.0] Test-map collection child failed: MSTest v5.0.0-preview.26553.4")
        },
        @{
            Name = "ModuleOutputShortensAlternateAssemblyContext"
            Lines = @("[affected-tests] [Shared.TestAssembly | .NETFramework,Version=v4.6.2 | .NET Framework 4.8 | win-x64] Discovered 2 tests.")
            Expected = @("[Shared.TestAssembly net462|x64, runtime: net48] Discovered 2 tests.")
        },
        @{
            Name = "ModuleOutputShortensAlternateAssemblyPartialContext"
            Lines = @("[affected-tests] [Other.Tests | .NETCoreApp,Version=v12.0] Child failed.")
            Expected = @("[Other.Tests net12.0] Child failed.")
        },
        @{
            Name = "ModuleOutputPreservesBracketedModuleContext"
            Module = [pscustomobject]@{ Project = "Sample.Tests[Rows](Debug)+Long.Component.Name"; Framework = "net12.0" }
            Lines = @('[affected-tests] [Sample.Tests[Rows](Debug)+Long.Component.Name | net12.0 | .NET 12.0 | win-x64] Child failed: "quoted <value> & details".')
            Expected = @('[Sample.Tests[Rows](Debug)+Long.Component.Name net12.0] Child failed: "quoted <value> & details".')
        },
        @{
            Name = "ModuleOutputDoesNotRepeatRuntimeAndRid"
            Module = [pscustomobject]@{ Project = "Microsoft.Testing.Platform.Acceptance.IntegrationTests"; Framework = "net12.0" }
            Lines = @("$mtpAcceptanceContext Test-map collection child failed: MSTest v5.0.0-preview.26553.4 (UTC 11/4/2026) [win-x64 - .NET 12.0.0-alpha.1.26509.101]")
            Expected = @("[Microsoft.Testing.Platform.Acceptance.IntegrationTests net12.0] Test-map collection child failed: MSTest v5.0.0-preview.26553.4 (UTC 11/4/2026)")
        },
        @{
            Name = "ModuleOutputDoesNotRepeatRuntimeAndRidForBracketedAssembly"
            Module = [pscustomobject]@{ Project = "Sample.Tests[Rows](Debug)"; Framework = "net12.0" }
            Lines = @("[affected-tests] [Sample.Tests[Rows](Debug) | net12.0 | .NET 12.0 | win-x64] Test-map collection child failed: MSTest v5.0.0 [win-x64 - .NET 12.0]")
            Expected = @("[Sample.Tests[Rows](Debug) net12.0] Test-map collection child failed: MSTest v5.0.0")
        },
        @{
            Name = "ModuleOutputPreservesDifferentChildRuntime"
            Lines = @("[affected-tests] [Sample.Tests | net12.0 | .NET 12.0 | win-x64] Test-map collection child failed: MSTest v5.0.0 [win-x64 - .NET 11.0]")
            Expected = @("[Sample.Tests net12.0] Test-map collection child failed: MSTest v5.0.0 [win-x64 - .NET 11.0]")
        },
        @{
            Name = "ModuleOutputPreservesDifferentChildRid"
            Lines = @("[affected-tests] [Sample.Tests | net12.0 | .NET 12.0 | win-x64] Test-map collection child failed: MSTest v5.0.0 [win-arm64 - .NET 12.0]")
            Expected = @("[Sample.Tests net12.0] Test-map collection child failed: MSTest v5.0.0 [win-arm64 - .NET 12.0]")
        },
        @{
            Name = "ModuleOutputPreservesUnscopedRuntimeAndRid"
            Lines = @("Test-map collection child failed: MSTest v5.0.0 [win-x64 - .NET 12.0]")
            Expected = @("[Sample.Tests net12.0] Test-map collection child failed: MSTest v5.0.0 [win-x64 - .NET 12.0]")
        },
        @{
            Name = "ModuleOutputPreservesRuntimeAndRidWithoutFullContext"
            Lines = @("[affected-tests] [Sample.Tests | net12.0] Test-map collection child failed: MSTest v5.0.0 [win-x64 - .NET 12.0]")
            Expected = @("[Sample.Tests net12.0] Test-map collection child failed: MSTest v5.0.0 [win-x64 - .NET 12.0]")
        },
        @{
            Name = "ModuleOutputPreservesEmbeddedBannerAfterShortContext"
            Lines = @("[affected-tests] [Sample.Tests | net12.0] Child output: [worker | .NET 12.0 | win-x64] Test-map collection child failed: MSTest v5.0.0 [win-x64 - .NET 12.0]")
            Expected = @("[Sample.Tests net12.0] Child output: [worker | .NET 12.0 | win-x64] Test-map collection child failed: MSTest v5.0.0 [win-x64 - .NET 12.0]")
        },
        @{
            Name = "ModuleOutputPreservesNonBannerRuntimeAndRid"
            Lines = @("[affected-tests] [Sample.Tests | net12.0 | .NET 12.0 | win-x64] Test-map collection child failed: Assertion expected [win-x64 - .NET 12.0]")
            Expected = @("[Sample.Tests net12.0] Test-map collection child failed: Assertion expected [win-x64 - .NET 12.0]")
        },
        @{
            Name = "ModuleOutputPreservesBannerFollowedByDiagnostics"
            Lines = @("[affected-tests] [Sample.Tests | net12.0 | .NET 12.0 | win-x64] Test-map collection child failed: MSTest v5.0.0 [win-x64 - .NET 12.0] Additional failure details.")
            Expected = @("[Sample.Tests net12.0] Test-map collection child failed: MSTest v5.0.0 [win-x64 - .NET 12.0] Additional failure details.")
        },
        @{
            Name = "ModuleOutputPrefixesUnscopedDiagnostics"
            Lines = @("Test run failed with exit code 37.")
            Expected = @("[Sample.Tests net12.0] Test run failed with exit code 37.")
        },
        @{
            Name = "ModuleOutputReplacesUnscopedAffectedTestPrefix"
            Lines = @("[affected-tests] Resolving test-map storage.")
            Expected = @("[Sample.Tests net12.0] Resolving test-map storage.")
        },
        @{
            Name = "ModuleOutputPrefixesBracketedNonModuleDiagnostics"
            Lines = @("[affected-tests] [warning] No test map is available.")
            Expected = @("[Sample.Tests net12.0] [warning] No test map is available.")
        },
        @{
            Name = "ModuleOutputPrefixesDiagnosticsContainingBracketedText"
            Lines = @("[affected-tests] [warning] saw [a | b] in output")
            Expected = @("[Sample.Tests net12.0] [warning] saw [a | b] in output")
        },
        @{
            Name = "ModuleOutputPreservesEmbeddedBannerInUnscopedDiagnostic"
            Lines = @("[affected-tests] [warning] saw [Sample.Tests | net12.0 | .NET 12.0 | win-x64] Test-map collection child failed: MSTest v5.0.0 [win-x64 - .NET 12.0]")
            Expected = @("[Sample.Tests net12.0] [warning] saw [Sample.Tests | net12.0 | .NET 12.0 | win-x64] Test-map collection child failed: MSTest v5.0.0 [win-x64 - .NET 12.0]")
        },
        @{
            Name = "ModuleOutputPrefixesIncompleteModuleContext"
            Lines = @("[affected-tests] [Sample.Tests | net12.0")
            Expected = @("[Sample.Tests net12.0] [Sample.Tests | net12.0")
        },
        @{
            Name = "ModuleOutputPrefixesEmbeddedModuleContext"
            Lines = @("Child output: [affected-tests] [Sample.Tests | net12.0] Failed.")
            Expected = @("[Sample.Tests net12.0] Child output: [affected-tests] [Sample.Tests | net12.0] Failed.")
        },
        @{
            Name = "ModuleOutputDropsTrailingBlankLine"
            Lines = @("")
            Expected = @()
        },
        @{
            Name = "ModuleOutputKeepsBlankLinesBeforeOutput"
            Lines = @("", "", "Next.")
            Expected = @("", "", "[Sample.Tests net12.0] Next.")
        },
        @{
            Name = "ModuleOutputSummarizesModuleDiscoveryAndMap"
            Module = $analyzersModule
            Lines = @(
                "Telemetry is: Enabled",
                "Collecting test maps for 1 module(s). Child test-result reporting is suppressed.",
                "$analyzersContext Experimental test-map collection: child SDK reporting is suppressed; only this parent summary and test map are produced. Counts below describe discovery and planning, not execution results.",
                "$analyzersContext Discovered 3060 tests; planning to run all 3060 for test-map collection (not execution results).",
                "$analyzersContext Collected test map '$analyzersMap': 3060 discovered test UID(s) in map, 827 source paths, adaptive batches of 16-256, up to 2 concurrent (not execution results).",
                "$analyzersContext Mapping timing: 24 measured batch(es), 0 conservative fallback batch(es).",
                "",
                "Test map collection: Completed.",
                "  modules finished: 1/1",
                "  Module completion is not a test verdict. Discovery and mapping counts do not represent executed test outcomes.",
                "  duration: 5m 43s 632ms")
            Expected = @("Telemetry is: Enabled")
            Captured = @{ Discovered = 3060; Tests = 3060; SourcePaths = 827; MapUri = $analyzersMap; Runtime = ".NET 10.0.15"; Rid = "win-x64" }
        },
        @{
            Name = "ModuleOutputKeepsConservativeFallbackTiming"
            Lines = @("$sampleContext Mapping timing: 193 measured batch(es), 3 conservative fallback batch(es).")
            Expected = @("[Sample.Tests net12.0] Mapping timing: 193 measured batch(es), 3 conservative fallback batch(es).")
        },
        @{
            Name = "ModuleOutputMatchesModuleByAssemblyFileName"
            Module = [pscustomobject]@{
                Project = "TestFramework.UnitTests"
                Framework = "net48"
                RelativePath = "artifacts/bin/TestFramework.UnitTests/Release/net48/Microsoft.VisualStudio.TestPlatform.TestFramework.UnitTests.exe"
            }
            Lines = @(
                "[affected-tests] [Microsoft.VisualStudio.TestPlatform.TestFramework.UnitTests | .NETFramework,Version=v4.8 | .NET Framework 4.8.9363.0 | win-x64] Mapping timing: 13 measured batch(es), 0 conservative fallback batch(es).",
                "[affected-tests] [Microsoft.VisualStudio.TestPlatform.TestFramework.UnitTests | .NETFramework,Version=v4.8 | .NET Framework 4.8.9363.0 | win-x64] Collector error: Pipe is broken.")
            Expected = @("[TestFramework.UnitTests net48] Collector error: Pipe is broken.")
            Captured = @{ Runtime = ".NET Framework 4.8.9363.0"; Rid = "win-x64" }
        },
        @{
            Name = "ModuleOutputDoesNotSuppressAlternateAssemblyProgress"
            Lines = @("[affected-tests] [Other.Tests | .NETCoreApp,Version=v12.0 | .NET 12.0.0 | win-x64] Discovered 5 tests; planning to run all 5 for test-map collection (not execution results).")
            Expected = @("[Other.Tests net12.0|x64] Discovered 5 tests; planning to run all 5 for test-map collection (not execution results).")
            Captured = @{ Discovered = $null; Runtime = $null }
        },
        @{
            Name = "ModuleOutputDoesNotClaimMapFromAnotherTarget"
            Module = $analyzersModule
            Lines = @("[affected-tests] [MSTest.Analyzers.UnitTests | .NETCoreApp,Version=v11.0 | .NET 12.0.0 | win-arm64] Collected test map 'other': 7 discovered test UID(s) in map, 2 source paths, adaptive batches of 4-16, up to 2 concurrent (not execution results).",
                "[affected-tests] [MSTest.Analyzers.UnitTests | .NETCoreApp,Version=v11.0] Child failed.")
            Expected = @("[MSTest.Analyzers.UnitTests net11.0|arm64, runtime: net12.0] Collected test map 'other': 7 discovered test UID(s) in map, 2 source paths, adaptive batches of 4-16, up to 2 concurrent (not execution results).",
                "[MSTest.Analyzers.UnitTests net11.0] Child failed.")
            Captured = @{ Tests = $null; SourcePaths = $null; MapUri = $null; Runtime = $null; Rid = $null }
        },
        @{
            Name = "ModuleOutputCapturesQuotedMapPathForPlatformTarget"
            Module = [pscustomobject]@{ Project = "Sample.Tests"; Framework = "net12.0-windows" }
            Lines = @("$sampleContext Collected test map 'D:\O'Brien\map.json': 0 discovered test UID(s) in map, 0 source paths, adaptive batches of 4-16, up to 2 concurrent (not execution results).")
            Expected = @()
            Captured = @{ Tests = 0; SourcePaths = 0; MapUri = "D:\O'Brien\map.json" }
        },
        @{
            Name = "ModuleOutputPreservesUnrecognizedProviderNotice"
            Lines = @("$sampleContext Experimental test-map collection: no storage provider was configured.")
            Expected = @("[Sample.Tests net12.0] Experimental test-map collection: no storage provider was configured.")
        },
        @{
            Name = "ModuleOutputKeepsAdditionalMapDiagnostic"
            Lines = @("$sampleContext Collected test map 'map': 5 discovered test UID(s) in map, 2 source paths, storage is read-only on the replica.")
            Expected = @("[Sample.Tests net12.0] Collected test map 'map': 5 discovered test UID(s) in map, 2 source paths, storage is read-only on the replica.")
            Captured = @{ Tests = 5; SourcePaths = 2; MapUri = "map" }
        },
        @{
            Name = "ModuleOutputKeepsUnparseableMapCounts"
            Lines = @("$sampleContext Collected test map 'map': 5 discovered test UID(s) in map, 18446744073709551616 source paths, adaptive batches of 4-16, up to 2 concurrent (not execution results).")
            Expected = @("[Sample.Tests net12.0] Collected test map 'map': 5 discovered test UID(s) in map, 18446744073709551616 source paths, adaptive batches of 4-16, up to 2 concurrent (not execution results).")
            Captured = @{ Tests = $null; SourcePaths = $null; MapUri = $null }
        },
        @{
            Name = "ModuleOutputEndsCompletedBlockAtOtherOutput"
            Lines = @("Test map collection: Completed.", "  modules finished: 1/1", "Unexpected trailer.", "  duration: 1s 000ms")
            Expected = @("[Sample.Tests net12.0] Unexpected trailer.", "[Sample.Tests net12.0]   duration: 1s 000ms")
        },
        @{
            Name = "ModuleOutputKeepsFailedSummary"
            Lines = @("", "Test map collection: Failed.", "  modules finished: 0/1", "  duration: 2s 001ms")
            Expected = @("", "[Sample.Tests net12.0] Test map collection: Failed.", "[Sample.Tests net12.0]   modules finished: 0/1", "[Sample.Tests net12.0]   duration: 2s 001ms")
        },
        @{
            Name = "ModuleOutputPassesPipelineCommandsThrough"
            Lines = @("##vso[task.logissue type=warning]Map upload retried.", "##[warning]Map upload retried.", "  ::error::Map upload failed.")
            Expected = @("##vso[task.logissue type=warning]Map upload retried.", "##[warning]Map upload retried.", "  ::error::Map upload failed.")
        },
        @{
            Name = "ModuleOutputShowsTelemetryOnce"
            Lines = @("Telemetry is: Enabled", "Telemetry is: Enabled")
            Expected = @("Telemetry is: Enabled")
        }
    )
    $defaultOutputModule = [pscustomobject]@{ Project = "Sample.Tests"; Framework = "net12.0" }
    $outputFailures = [System.Collections.Generic.List[string]]::new()
    foreach ($case in $outputCases) {
        $previousFailureCount = $outputFailures.Count
        $expected = @($case.Expected)
        $caseInput = ($case.Lines | ForEach-Object { $_ + [Environment]::NewLine }) -join ""
        foreach ($stream in @("Output", "Error")) {
            $reader = [System.IO.StringReader]::new($caseInput)
            $writer = [System.IO.StringWriter]::new()
            try {
                $state = [pscustomobject]@{
                    Module = $(if ($case.ContainsKey("Module")) { $case.Module } else { $defaultOutputModule })
                    Writer = $writer
                    OutputReader = $null
                    ErrorReader = $null
                    OutputTask = $null
                    ErrorTask = $null
                }
                $state."${stream}Reader" = $reader
                $state."${stream}Task" = $reader.ReadLineAsync()
                $output = @(Read-CollectionModuleOutput -State $state 6>&1 | ForEach-Object { $_.ToString() })
                if ($output.Count -ne $expected.Count -or ($output -join "`n") -cne ($expected -join "`n")) {
                    $outputFailures.Add("$($case.Name) ($stream): expected '$($expected -join " | ")', got '$($output -join " | ")'.")
                }
                if ($writer.ToString() -cne $caseInput -or $null -ne $state.OutputTask -or $null -ne $state.ErrorTask) {
                    $outputFailures.Add("$($case.Name) ($stream): the original log lines must be preserved and the stream drained.")
                }
                if ($case.ContainsKey("Captured")) {
                    foreach ($key in $case.Captured.Keys) {
                        if ("$($state.Presentation.$key)" -cne "$($case.Captured[$key])") {
                            $outputFailures.Add("$($case.Name) ($stream): expected $key '$($case.Captured[$key])', got '$($state.Presentation.$key)'.")
                        }
                    }
                }
            } finally {
                $reader.Dispose()
                $writer.Dispose()
            }
        }
        if ($outputFailures.Count -eq $previousFailureCount) {
            Write-PassedCheck $case.Name
        }
    }
    if ($outputFailures.Count -gt 0) {
        throw ($outputFailures -join [Environment]::NewLine)
    }

    $durationFailures = foreach ($durationCase in @(
        @{ Duration = [timespan]::Zero; Expected = "0ms" },
        @{ Duration = [timespan]::FromMilliseconds(937); Expected = "937ms" },
        @{ Duration = [timespan]::FromMilliseconds(34016); Expected = "34s 016ms" },
        @{ Duration = [timespan]::FromMilliseconds(63559); Expected = "1m 03s 559ms" },
        @{ Duration = [timespan]::FromMilliseconds(4408379); Expected = "1h 13m 28s 379ms" },
        @{ Duration = [timespan]::FromMilliseconds(86405007); Expected = "1d 00h 00m 05s 007ms" }
    )) {
        $actual = Format-CollectionDuration -Duration $durationCase.Duration
        if ($actual -cne $durationCase.Expected) { "expected '$($durationCase.Expected)', got '$actual'" }
    }
    if (@($durationFailures).Count -gt 0) {
        throw "CollectionDurationMatchesTerminalReporter: $(@($durationFailures) -join '; ')."
    }
    Write-PassedCheck "CollectionDurationMatchesTerminalReporter"

    $displayFailures = foreach ($displayCase in @(
        @{ Framework = "net10.0"; Rid = "win-x64"; Runtime = ".NET 10.0.15"; Expected = "net10.0|x64" },
        @{ Framework = "net12.0"; Rid = "linux-arm64"; Runtime = ".NET 12.0.0-alpha.1.26570.125"; Expected = "net12.0|arm64" },
        @{ Framework = "net48"; Rid = "win-x64"; Runtime = ".NET Framework 4.8.9363.0"; Expected = "net48|x64" },
        @{ Framework = "net472"; Rid = "win-x64"; Runtime = ".NET Framework 4.8.9363.0"; Expected = "net472|x64, runtime: net48" },
        @{ Framework = "net10.0"; Rid = "win-x64"; Runtime = ".NET 11.0.0"; Expected = "net10.0|x64, runtime: net11.0" },
        @{ Framework = "net11.0-windows10.0.19041.0"; Rid = "win-x64"; Runtime = ".NET 11.0.0"; Expected = "net11.0-windows10.0.19041.0|x64" },
        @{ Framework = "net10.0"; Rid = "win-x64"; Runtime = "Mono 6.12"; Expected = "net10.0|x64, runtime: Mono 6.12" },
        @{ Framework = "net10.0"; Rid = $null; Runtime = $null; Expected = "net10.0" }
    )) {
        $runtimeFramework = ConvertTo-CollectionRuntimeFramework -Description $displayCase.Runtime
        $actual = Get-CollectionModuleDisplay -Framework $displayCase.Framework -Rid $displayCase.Rid -Runtime $runtimeFramework
        if ($actual -cne $displayCase.Expected) { "expected '$($displayCase.Expected)', got '$actual'" }
    }
    if (@($displayFailures).Count -gt 0) {
        throw "CollectionModuleDisplayOmitsMatchingRuntime: $(@($displayFailures) -join '; ')."
    }
    Write-PassedCheck "CollectionModuleDisplayOmitsMatchingRuntime"

    $completionModule = [pscustomobject]@{
        Project = "Sample.Tests"
        Framework = "net10.0"
        RelativePath = "artifacts/bin/Sample.Tests/Release/net10.0/Sample.Tests.dll"
    }
    $completionFailures = foreach ($completionCase in @(
        @{ ExitCode = 0; Tests = 3060; SourcePaths = 827; MapUri = "azdo://map"; Rid = "win-x64"; Expected = "artifacts/bin/Sample.Tests/Release/net10.0/Sample.Tests.dll (net10.0|x64) mapped 3060 tests to 827 source paths (0ms)" },
        @{ ExitCode = 0; Tests = 1; SourcePaths = 1; MapUri = "azdo://map"; Rid = "win-x64"; Expected = "artifacts/bin/Sample.Tests/Release/net10.0/Sample.Tests.dll (net10.0|x64) mapped 1 test to 1 source path (0ms)" },
        @{ ExitCode = 37; Tests = 3060; SourcePaths = 827; MapUri = "azdo://map"; Rid = "win-x64"; Expected = "artifacts/bin/Sample.Tests/Release/net10.0/Sample.Tests.dll (net10.0|x64) failed with exit code 37 (0ms)" },
        @{ ExitCode = 0; Canceled = $true; Rid = "win-x64"; Expected = "artifacts/bin/Sample.Tests/Release/net10.0/Sample.Tests.dll (net10.0|x64) canceled (0ms)" },
        @{ ExitCode = 0; Expected = "artifacts/bin/Sample.Tests/Release/net10.0/Sample.Tests.dll (net10.0) completed (map counts not reported) (0ms)" }
    )) {
        $completionState = [pscustomobject]@{ Module = $completionModule; Timer = [System.Diagnostics.Stopwatch]::new() }
        $presentation = Get-CollectionOutputPresentation -State $completionState
        $presentation.Tests = $completionCase.Tests
        $presentation.SourcePaths = $completionCase.SourcePaths
        $presentation.MapUri = $completionCase.MapUri
        $presentation.Rid = $completionCase.Rid
        $presentation.Runtime = $(if ($null -ne $completionCase.Rid) { ".NET 10.0.15" } else { $null })
        $actual = @(Write-CollectionModuleCompletion -State $completionState -ExitCode $completionCase.ExitCode -Canceled:($completionCase.Canceled -eq $true) 6>&1 |
            ForEach-Object { $_.ToString() })
        if ($actual.Count -ne 2 -or $actual[0] -cne "" -or $actual[1] -cne $completionCase.Expected) {
            "expected '| $($completionCase.Expected)', got '$($actual -join " | ")'"
        }
    }
    if (@($completionFailures).Count -gt 0) {
        throw "CollectionModuleCompletionReportsOutcome: $(@($completionFailures) -join '; ')."
    }
    Write-PassedCheck "CollectionModuleCompletionReportsOutcome"

    $sharedPresenter = New-CollectionOutputPresenter
    $telemetryOutput = foreach ($telemetryModule in @("First.Tests", "Second.Tests")) {
        $telemetryState = [pscustomobject]@{
            Module = [pscustomobject]@{ Project = $telemetryModule; Framework = "net12.0" }
            Presenter = $sharedPresenter
        }
        ConvertTo-CollectionConsoleLine -State $telemetryState -Stream Output -Line "Telemetry is: Enabled"
        ConvertTo-CollectionConsoleLine -State $telemetryState -Stream Output -Line "Ready."
    }
    if ((@($telemetryOutput) -join "`n") -cne "Telemetry is: Enabled`n[First.Tests net12.0] Ready.`n[Second.Tests net12.0] Ready.") {
        throw "CollectionTelemetryShownOncePerPool: expected one telemetry line across modules, got '$(@($telemetryOutput) -join " | ")'."
    }
    Write-PassedCheck "CollectionTelemetryShownOncePerPool"

    $worker = Join-Path $fixture "worker.ps1"
    # Rendezvous may include starting three pwsh processes and compiling the handle helper on a loaded agent.
    $workerStartupSeconds = 60
    $workerBody = @'
$ErrorActionPreference = "Stop"
function Write-StartedMarker([string]$Path) {
    # Publish the PID and the ready signal together, before the parent can terminate the writer.
    $pending = "$Path.$PID.tmp"
    [IO.File]::WriteAllText($pending, [string]$PID)
    [IO.File]::Move($pending, $Path, $true)
}
if ($args.Count -gt 0 -and $args[0] -eq "orphan-middle") {
    $child = Start-Process -FilePath (Get-Process -Id $PID).Path `
        -ArgumentList @("-NoProfile", "-File", "`"$PSCommandPath`"", "grandchild", "`"$($args[1])`"") `
        -WindowStyle Hidden -PassThru `
        -RedirectStandardOutput (Join-Path $args[1] "grandchild-output.log") `
        -RedirectStandardError (Join-Path $args[1] "grandchild-error.log")
    try {
        $deadline = [datetimeoffset]::UtcNow.AddSeconds($workerStartupSeconds)
        while (-not (Test-Path -LiteralPath (Join-Path $args[1] "grandchild.started"))) {
            if ([datetimeoffset]::UtcNow -ge $deadline) {
                throw "The independently launched descendant did not start."
            }
            Start-Sleep -Milliseconds 10
        }
    } finally {
        $child.Dispose()
    }
    exit 0
}
if ($args.Count -gt 0 -and $args[0] -eq "grandchild") {
    Write-StartedMarker (Join-Path $args[1] "grandchild.started")
    while ($true) {
        Start-Sleep -Seconds 1
    }
}
$moduleIndex = [Array]::IndexOf($args, "--test-modules")
$module = $args[$moduleIndex + 1]
$rootIndex = [Array]::IndexOf($args, "--root-directory")
$root = $args[$rootIndex + 1]
$progressIndex = [Array]::IndexOf($args, "--progress")
if (@($args | Where-Object { $_ -ceq "--progress" }).Count -ne 1 -or
    $progressIndex + 1 -ge $args.Count -or $args[$progressIndex + 1] -cne "off" -or
    $args -ccontains "--no-progress" -or
    @($args | Where-Object { $_ -ceq "--no-ansi" }).Count -ne 1) {
    throw "The module launch must pass separate '--progress' and 'off' tokens, omit '--no-progress', and retain SDK-owned '--no-ansi'."
}
if ($module -notlike "orphan-*" -and -not [string]::IsNullOrEmpty($env:TESTFX_COLLECTION_NUGET_CACHE)) {
    throw "A module without an owned cache inherited the collection-cache opt-in."
}
if ($module -eq "error-stream") {
    [Console]::Error.WriteLine("error stream first")
    [Console]::Error.WriteLine("error stream second")
    exit 0
}
if ($module -eq "mapped-peer") {
    Write-Output "[affected-tests] [mapped-peer | net10.0 | .NET 10.0.15 | win-x64] Collected test map 'map': 3 discovered test UID(s) in map, 2 source paths, adaptive batches of 4-16, up to 2 concurrent (not execution results)."
    exit 0
}
if ($module -like "orphan-*") {
    if ($args[0] -ne "argument with spaces" -or $args[1] -ne "" -or
        $args[2] -ne "trailing\" -or $args[3] -ne 'embedded"quote' -or
        $args[4] -ne ([string][char]0x00E9 + [char]0x6E2C) -or
        $env:TESTFX_COLLECTION_NUGET_CACHE -ne (Join-Path $root "cache\module-000") -or
        (Get-Content -LiteralPath (Join-Path $env:TESTFX_COLLECTION_NUGET_CACHE ".testfx-collection-cache") -Raw).Trim() -ne "testfx-collection-cache-v1") {
        throw "The module launch changed arguments or the Unicode cache environment."
    }
    Add-Type -TypeDefinition 'using System; using System.ComponentModel; using System.Runtime.InteropServices;
        public static class FixtureOutput {
            [DllImport("kernel32.dll", ExactSpelling = true, SetLastError = true)]
            private static extern IntPtr GetStdHandle(int stream);
            [DllImport("kernel32.dll", ExactSpelling = true, SetLastError = true)]
            private static extern int SetHandleInformation(IntPtr handle, uint mask, uint flags);
            public static void MakeChildOutputIndependent() {
                foreach (int stream in new[] { -11, -12 })
                    if (SetHandleInformation(GetStdHandle(stream), 1, 0) == 0)
                        throw new Win32Exception(Marshal.GetLastWin32Error());
            }
        }'
    [FixtureOutput]::MakeChildOutputIndependent()
    $child = Start-Process -FilePath (Get-Process -Id $PID).Path `
        -ArgumentList @("-NoProfile", "-File", "`"$PSCommandPath`"", "orphan-middle", "`"$root`"") `
        -WindowStyle Hidden -PassThru `
        -RedirectStandardOutput (Join-Path $root "middle-output.log") `
        -RedirectStandardError (Join-Path $root "middle-error.log")
    try {
        if (-not $child.WaitForExit(1000 * $workerStartupSeconds) -or $child.ExitCode -ne 0) {
            throw "The intermediate parent did not finish before SDK completion."
        }
    } finally {
        $child.Dispose()
    }
    Set-Content -LiteralPath (Join-Path $root "orphan.ready") -Value $PID
    $deadline = [datetimeoffset]::UtcNow.AddSeconds($workerStartupSeconds)
    while (-not (Test-Path -LiteralPath (Join-Path $root "orphan.release"))) {
        if ([datetimeoffset]::UtcNow -ge $deadline) {
            throw "The SDK completion rendezvous was not released."
        }
        Start-Sleep -Milliseconds 10
    }
    exit $(if ($module -eq "orphan-failure") { 37 } else { 0 })
}
if ($module -eq "slow") {
    $childInfo = [System.Diagnostics.ProcessStartInfo]::new()
    $childInfo.FileName = (Get-Process -Id $PID).Path
    foreach ($argument in @("-NoProfile", "-File", $PSCommandPath, "grandchild", $root)) {
        $childInfo.ArgumentList.Add($argument)
    }
    $childInfo.UseShellExecute = $false
    $child = [System.Diagnostics.Process]::Start($childInfo)
    $deadline = [datetimeoffset]::UtcNow.AddSeconds($workerStartupSeconds)
    while (-not (Test-Path -LiteralPath (Join-Path $root "grandchild.started"))) {
        if ([datetimeoffset]::UtcNow -ge $deadline) {
            throw "The child never started its owned descendant."
        }
        Start-Sleep -Milliseconds 10
    }
}
Write-Output "$module started"
Write-StartedMarker (Join-Path $root "$module.started")
if ($module -eq "failure") {
    if (Test-Path -LiteralPath (Join-Path $root "cancel-peer-mode")) {
        $deadline = [datetimeoffset]::UtcNow.AddSeconds($workerStartupSeconds)
        while (-not (Test-Path -LiteralPath (Join-Path $root "slow.started"))) {
            if ([datetimeoffset]::UtcNow -ge $deadline) {
                throw "The canceled-peer scenario never started its peer."
            }
            Start-Sleep -Milliseconds 10
        }
    }
    exit 37
}
if ($module -eq "never-started") {
    throw "A module was launched after a known failure."
}
$waitFor = switch ($module) {
    "first" { "second.started" }
    "second" { "third.started" }
    "slow" { "never-release" }
    "cleanup-first" { "never-release" }
    "cleanup-second" { "never-release" }
    default { $null }
}
if ($null -ne $waitFor) {
    $deadline = [datetimeoffset]::UtcNow.AddSeconds($workerStartupSeconds)
    while (-not (Test-Path -LiteralPath (Join-Path $root $waitFor))) {
        if ([datetimeoffset]::UtcNow -ge $deadline) {
            throw "Worker '$module' did not observe '$waitFor' before its rendezvous deadline."
        }
        Start-Sleep -Milliseconds 10
    }
}
Set-Content -LiteralPath (Join-Path $root "$module.finished") -Value $PID
Write-Output "$module finished"
exit 0
'@
    Set-Content -LiteralPath $worker -Value "`$workerStartupSeconds = $workerStartupSeconds", $workerBody -Encoding utf8
    $prefix = @("-NoProfile", "-File", $worker)
    $powershellPath = Join-Path $PSHOME $(if ($IsWindows) { "pwsh.exe" } else { "pwsh" })
    $modules = @("first", "second", "third") | ForEach-Object {
        [pscustomobject]@{ Project = $_; Framework = "fake"; RelativePath = $_ }
    }
    $result = Invoke-CollectionModulePool -Modules $modules -Repository $fixture -ResultsDirectory (Join-Path $fixture "refill") `
        -DotnetPath $powershellPath -MaximumParallelModules 2 -CommandPrefix $prefix
    if ($result.ExitCode -ne 0 -or $result.Completed -ne 3 -or
        -not (Test-Path -LiteralPath (Join-Path $fixture "second.finished")) -or
        -not (Test-Path -LiteralPath (Join-Path $fixture "third.started"))) {
        throw "ModulePoolRefillsWithoutWaveBarrier: both first workers must start and the third must refill while the second waits."
    }
    foreach ($completed in $result.Modules) {
        $expectedLog = @"
$($completed.Module.Project) started
$($completed.Module.Project) finished

"@
        if ((Get-Content -LiteralPath $completed.LogPath -Raw).Replace("`r`n", "`n") -ne $expectedLog.Replace("`r`n", "`n")) {
            throw "ModulePoolRefillsWithoutWaveBarrier: each completed module must preserve its entire output log."
        }
    }

    Write-PassedCheck "ModulePoolRefillsWithoutWaveBarrier"

    $modules = @("failure", "never-started") | ForEach-Object {
        [pscustomobject]@{ Project = $_; Framework = "fake"; RelativePath = $_ }
    }
    $failureConsole = @(Invoke-CollectionModulePool -Modules $modules -Repository $fixture -ResultsDirectory (Join-Path $fixture "failure") `
        -DotnetPath $powershellPath -MaximumParallelModules 1 -CommandPrefix $prefix 6>&1)
    $result = @($failureConsole | Where-Object { $_ -isnot [System.Management.Automation.InformationRecord] })[-1]
    $failureConsoleText = @($failureConsole | Where-Object { $_ -is [System.Management.Automation.InformationRecord] } |
        ForEach-Object { $_.ToString() }) -join "`n"
    $durationPattern = '(?:\d+d )?(?:\d+h )?(?:\d+m )?(?:\d+s )?\d+ms'
    if ($failureConsoleText -cnotmatch ('^Collecting test map from failure \(fake\)\n\[failure fake\] failure started\n\nfailure \(fake\) failed with exit code 37 \(' + $durationPattern + '\)$')) {
        throw "ModulePoolReportsModuleLikeTestRun: expected start, labeled output, and completion lines, got '$($failureConsoleText.Replace("`n", " | "))'."
    }
    Write-PassedCheck "ModulePoolReportsModuleLikeTestRun"
    $expectedFailureLog = @'
failure started

'@
    if ($result.ExitCode -ne 37 -or $result.Started -ne 1 -or
        (Test-Path -LiteralPath (Join-Path $fixture "never-started.started")) -or
        (Get-Content -LiteralPath $result.Modules[0].LogPath -Raw).Replace("`r`n", "`n") -ne $expectedFailureLog.Replace("`r`n", "`n")) {
        throw "ModulePoolPreservesFailure: a native failure must stop new launches and preserve its exit code."
    }

    Write-PassedCheck "ModulePoolPreservesFailure"

    $result = Invoke-CollectionModulePool -Modules @(
        [pscustomobject]@{ Project = "error-stream"; Framework = "fake"; RelativePath = "error-stream" }) `
        -Repository $fixture -ResultsDirectory (Join-Path $fixture "error-stream") `
        -DotnetPath $powershellPath -MaximumParallelModules 1 -CommandPrefix $prefix
    $expectedErrorLog = @'
error stream first
error stream second

'@
    if ($result.ExitCode -ne 0 -or $result.Completed -ne 1 -or
        (Get-Content -LiteralPath $result.Modules[0].LogPath -Raw).Replace("`r`n", "`n") -ne $expectedErrorLog.Replace("`r`n", "`n")) {
        throw "ModulePoolPreservesBufferedErrorOutput: process completion must preserve the entire stderr log."
    }
    Write-PassedCheck "ModulePoolPreservesBufferedErrorOutput"

    $savedRead = (Get-Command Read-CollectionModuleOutput).ScriptBlock
    try {
        function Read-CollectionModuleOutput {
            param($State)
            $deadline = [datetimeoffset]::UtcNow.AddSeconds($workerStartupSeconds)
            while (-not $State.Process.HasExited) {
                if ([datetimeoffset]::UtcNow -ge $deadline) {
                    throw "The native failure child did not exit."
                }
                Start-Sleep -Milliseconds 10
            }
            & $savedRead -State $State
            throw "Injected output failure after native exit."
        }
        $result = $null
        $caught = $null
        try {
            $result = Invoke-CollectionModulePool -Modules @(
                [pscustomobject]@{ Project = "failure"; Framework = "fake"; RelativePath = "failure" }) `
                -Repository $fixture -ResultsDirectory (Join-Path $fixture "output-error") `
                -DotnetPath $powershellPath -MaximumParallelModules 1 -CommandPrefix $prefix
        } catch {
            $caught = $_
        }
        if ($null -ne $caught -or $null -eq $result -or $result.ExitCode -ne 37 -or $result.TeardownConfirmed -or
            -not ($result.CleanupErrors -join " ").Contains("Injected output failure after native exit.") -or
            (Get-Content -LiteralPath (Join-Path $fixture "output-error\module-000\collection.log") -Raw).Replace("`r`n", "`n") -ne $expectedFailureLog.Replace("`r`n", "`n")) {
            throw "ModulePoolPreservesNativeFailureDuringOutputError: logging failure must not replace an observed native exit or lose captured output."
        }
    } finally {
        Set-Item -LiteralPath function:Read-CollectionModuleOutput -Value $savedRead
    }
    Write-PassedCheck "ModulePoolPreservesNativeFailureDuringOutputError"

    try {
        function Read-CollectionModuleOutput {
            param($State)
            $deadline = [datetimeoffset]::UtcNow.AddSeconds($workerStartupSeconds)
            while (-not $State.Process.HasExited -or $null -ne $State.OutputTask -or $null -ne $State.ErrorTask) {
                if ([datetimeoffset]::UtcNow -ge $deadline) {
                    throw "The native failure child did not exit and drain."
                }
                & $savedRead -State $State
                Start-Sleep -Milliseconds 10
            }
            throw "Injected output failure after native exit."
        }
        $nativeFailureConsole = @(Invoke-CollectionModulePool -Modules @(
            [pscustomobject]@{ Project = "failure"; Framework = "fake"; RelativePath = "failure" }) `
            -Repository $fixture -ResultsDirectory (Join-Path $fixture "drained-output-error") `
            -DotnetPath $powershellPath -MaximumParallelModules 1 -CommandPrefix $prefix 6>&1 3>$null)
        $result = @($nativeFailureConsole | Where-Object { $_ -isnot [System.Management.Automation.InformationRecord] })[-1]
        $nativeFailureText = @($nativeFailureConsole | Where-Object { $_ -is [System.Management.Automation.InformationRecord] } |
            ForEach-Object { $_.ToString() }) -join "`n"
        if ($result.ExitCode -ne 37 -or $result.Completed -ne 1 -or $result.Modules[0].ExitCode -ne 37 -or
            $result.CanceledModules.Count -ne 0 -or $null -ne $result.TestsMapped -or
            $nativeFailureText -cnotmatch ('\nfailure \(fake\) failed with exit code 37 \(' + $durationPattern + '\)$')) {
            throw "ModulePoolReportsNativeFailureDuringOutputError: expected 1 finished, 0 canceled, exit 37 and no reported map count; got $($result.Completed) finished, $($result.CanceledModules.Count) canceled, exit $($result.ExitCode), tests '$($result.TestsMapped)' and '$($nativeFailureText.Replace("`n", " | "))'."
        }
    } finally {
        Set-Item -LiteralPath function:Read-CollectionModuleOutput -Value $savedRead
    }
    Write-PassedCheck "ModulePoolReportsNativeFailureDuringOutputError"

    try {
        $script:injectAfterPeerExit = $true
        function Read-CollectionModuleOutput {
            param($State)
            if (-not $State.Process.WaitForExit(1000 * $workerStartupSeconds)) {
                throw "The collection worker did not exit before the fixture deadline."
            }
            if ($State.Index -eq 1 -and $script:injectAfterPeerExit) {
                $script:injectAfterPeerExit = $false
                throw "Injected reader error after successful peer exit."
            }
            & $savedRead -State $State
        }
        $peerDirectory = Join-Path $fixture "exited-peer"
        $peerConsole = @(Invoke-CollectionModulePool -Modules @(
            [pscustomobject]@{ Project = "failure"; Framework = "fake"; RelativePath = "failure" },
            [pscustomobject]@{ Project = "mapped-peer"; Framework = "net10.0"; RelativePath = "mapped-peer" }) `
            -Repository $fixture -ResultsDirectory $peerDirectory -DotnetPath $powershellPath -MaximumParallelModules 2 -CommandPrefix $prefix 6>&1 3>$null)
        $result = $peerConsole[-1]
        $peerMetadata = Get-Content -LiteralPath (Join-Path $peerDirectory "module-001\module.json") -Raw | ConvertFrom-Json
        $peerText = @($peerConsole | Where-Object { $_ -is [System.Management.Automation.InformationRecord] }) -join "`n"
        if ($result.Completed -ne 2 -or $result.CanceledModules.Count -ne 0 -or $result.TestsMapped -ne 3 -or
            $peerMetadata.Canceled -or $peerMetadata.Tests -ne 3 -or $peerMetadata.ExitCode -ne 0 -or
            $peerText -cnotmatch ('\nmapped-peer \(net10.0\|x64\) mapped 3 tests to 2 source paths \(' + $durationPattern + '\)$')) {
            throw "ModulePoolCountsAlreadyExitedPeer: expected both native exits and 3 mapped tests, got $($result.Completed) completed, $($result.CanceledModules.Count) canceled and '$($result.TestsMapped)' tests."
        }
        $summary = @(Write-CollectionRunSummary -Result $result -Duration ([TimeSpan]::Zero) -ResultsDirectory $peerDirectory 6>&1) -join "`n"
        $expectedSummary = @("", "Test map collection summary: Failed! (exit code 37)", "  modules: 2/2",
            "  tests mapped: 3 (reported by 1/2 finished modules)", "  duration: 0ms", "", "  Module logs: $peerDirectory") -join "`n"
        if ($summary -cne $expectedSummary) {
            throw "ModulePoolCountsAlreadyExitedPeer: expected '$expectedSummary', got '$summary'."
        }
    } finally {
        Set-Item -LiteralPath function:Read-CollectionModuleOutput -Value $savedRead
    }
    Write-PassedCheck "ModulePoolCountsAlreadyExitedPeer"

    $modules = @("slow", "failure") | ForEach-Object {
        [pscustomobject]@{ Project = $_; Framework = "fake"; RelativePath = $_ }
    }
    New-Item -ItemType File -Path (Join-Path $fixture "cancel-peer-mode") | Out-Null
    $result = Invoke-CollectionModulePool -Modules $modules -Repository $fixture -ResultsDirectory (Join-Path $fixture "cancel") `
        -DotnetPath $powershellPath -MaximumParallelModules 2 -CommandPrefix $prefix
    $pidText = Get-Content -LiteralPath (Join-Path $fixture "slow.started") -Raw
    $grandchildPid = [int](Get-Content -LiteralPath (Join-Path $fixture "grandchild.started") -Raw)
    $expectedCanceledLog = @'
slow started

'@
    if ($result.ExitCode -ne 37 -or -not $result.TeardownConfirmed -or $result.CanceledModules.Count -ne 1 -or
        -not $result.CanceledModules[0].Canceled -or
        (Get-Process -Id ([int]$pidText) -ErrorAction SilentlyContinue) -or
        (Get-Process -Id $grandchildPid -ErrorAction SilentlyContinue) -or
        (Get-Content -LiteralPath $result.CanceledModules[0].LogPath -Raw).Replace("`r`n", "`n") -ne $expectedCanceledLog.Replace("`r`n", "`n")) {
        throw "ModulePoolDrainsFailedPeers: the still-running child must terminate and retain its log before pool ownership is released."
    }

    Write-PassedCheck "ModulePoolDrainsFailedPeers"

    $savedStop = (Get-Command Stop-CollectionModuleProcess).ScriptBlock
    try {
        Remove-Item -LiteralPath (Join-Path $fixture "slow.started"), (Join-Path $fixture "grandchild.started")
        function Stop-CollectionModuleProcess {
            param($State)
            & $savedStop -State $State
            throw "Injected cleanup diagnostic after confirmed stop."
        }
        $result = Invoke-CollectionModulePool -Modules $modules -Repository $fixture -ResultsDirectory (Join-Path $fixture "cleanup") `
            -DotnetPath $powershellPath -MaximumParallelModules 2 -CommandPrefix $prefix
        $pidText = Get-Content -LiteralPath (Join-Path $fixture "slow.started") -Raw
        if ($result.ExitCode -ne 37 -or $result.TeardownConfirmed -or $result.CleanupErrors.Count -ne 1 -or
            (Get-Process -Id ([int]$pidText) -ErrorAction SilentlyContinue)) {
            throw "ModulePoolPreservesFailureDuringCleanupError: retain the native exit and expose unconfirmed teardown instead of deleting its cache."
        }
    } finally {
        Set-Item -LiteralPath function:Stop-CollectionModuleProcess -Value $savedStop
    }
    Write-PassedCheck "ModulePoolPreservesFailureDuringCleanupError"

    $savedRead = (Get-Command Read-CollectionModuleOutput).ScriptBlock
    $script:injectOutputFailure = $true
    $cleanupPeers = [System.Collections.Generic.List[System.Diagnostics.Process]]::new()
    try {
        function Read-CollectionModuleOutput {
            param($State)
            if ($script:injectOutputFailure) {
                $deadline = [datetimeoffset]::UtcNow.AddSeconds($workerStartupSeconds)
                while (-not (Test-Path -LiteralPath (Join-Path $fixture "cleanup-first.started")) -or
                    -not (Test-Path -LiteralPath (Join-Path $fixture "cleanup-second.started"))) {
                    if ([datetimeoffset]::UtcNow -ge $deadline) {
                        throw "Cleanup peers did not start."
                    }
                    Start-Sleep -Milliseconds 10
                }
                foreach ($name in @("cleanup-first", "cleanup-second")) {
                    $peer = Get-Process -Id ([int](Get-Content -LiteralPath (Join-Path $fixture "$name.started") -Raw))
                    $null = $peer.Handle
                    $cleanupPeers.Add($peer)
                }
                $script:injectOutputFailure = $false
                throw "Injected primary output failure."
            }
            & $savedRead -State $State
        }
        function Stop-CollectionModuleProcess {
            param($State)
            & $savedStop -State $State
            if ($State.Index -eq 0) {
                throw "Injected first-peer cleanup failure."
            }
        }
        $modules = @("cleanup-first", "cleanup-second") | ForEach-Object {
            [pscustomobject]@{ Project = $_; Framework = "fake"; RelativePath = $_ }
        }
        $caught = $null
        try {
            Invoke-CollectionModulePool -Modules $modules -Repository $fixture -ResultsDirectory (Join-Path $fixture "all-peers") `
                -DotnetPath $powershellPath -MaximumParallelModules 2 -CommandPrefix $prefix | Out-Null
        } catch {
            $caught = $_
        }
        if ($null -eq $caught -or -not $caught.Exception.Message.Contains("Injected primary output failure.") -or
            @($caught.Exception.Data["CollectionCleanupErrors"]).Count -ne 1 -or
            $cleanupPeers.Count -ne 2 -or @($cleanupPeers | Where-Object { -not $_.HasExited }).Count -ne 0) {
            throw "ModulePoolDrainsEveryPeerAfterCleanupError: expected the injected primary error, one cleanup failure and two exited peers; got '$caught', peer count $($cleanupPeers.Count)."
        }
    } finally {
        Set-Item -LiteralPath function:Read-CollectionModuleOutput -Value $savedRead
        Set-Item -LiteralPath function:Stop-CollectionModuleProcess -Value $savedStop
        foreach ($peer in $cleanupPeers) {
            try {
                if (-not $peer.HasExited) {
                    Stop-CollectionModuleProcess -State ([pscustomobject]@{ Process = $peer })
                }
            } finally {
                $peer.Dispose()
            }
        }
    }
    Write-PassedCheck "ModulePoolDrainsEveryPeerAfterCleanupError"

    $rejected = $false
    try {
        Invoke-CollectionModulePool -Modules @([pscustomobject]@{ Project = "missing"; Framework = "fake"; RelativePath = "missing" }) `
            -Repository $fixture -ResultsDirectory (Join-Path $fixture "launch") `
            -DotnetPath (Join-Path $fixture "missing.exe") -MaximumParallelModules 1 | Out-Null
    } catch {
        if ($_.Exception.Message.Contains("No process is associated")) {
            throw "ModulePoolPreservesLaunchError: cleanup masked the original launch failure."
        }

        $rejected = $true
    }

    if (-not $rejected) {
        throw "ModulePoolPreservesLaunchError: a missing executable must not become successful collection."
    }

    Write-PassedCheck "ModulePoolPreservesLaunchError"
    $orphanFailures = [System.Collections.Generic.List[string]]::new()
    if ($IsWindows) {
        foreach ($case in @(
            @{ Module = "orphan-success"; ExitCode = 0; Name = "ModulePoolDrainsOrphansAfterSdkCompletion" },
            @{ Module = "orphan-failure"; ExitCode = 37; Name = "ModulePoolDrainsOrphansAfterSdkFailure" }
        )) {
            $script:orphanRoot = Join-Path $fixture ($case.Module + " spaced-" + [char]0x00E9)
            New-Item -ItemType Directory -Path $script:orphanRoot | Out-Null
            $script:orphanChild = $null
            $savedRead = (Get-Command Read-CollectionModuleOutput).ScriptBlock
            try {
                function Read-CollectionModuleOutput {
                    param($State)
                    if ($null -eq $script:orphanChild) {
                        $deadline = [datetimeoffset]::UtcNow.AddSeconds($workerStartupSeconds)
                        while (-not (Test-Path -LiteralPath (Join-Path $script:orphanRoot "orphan.ready"))) {
                            if ([datetimeoffset]::UtcNow -ge $deadline) {
                                throw "The orphan fixture did not reach its completion rendezvous."
                            }
                            Start-Sleep -Milliseconds 10
                        }
                        $childId = [int](Get-Content -LiteralPath (Join-Path $script:orphanRoot "grandchild.started") -Raw)
                        $script:orphanChild = [System.Diagnostics.Process]::GetProcessById($childId)
                        $null = $script:orphanChild.Handle
                        New-Item -ItemType File -Path (Join-Path $script:orphanRoot "orphan.release") | Out-Null
                    }
                    & $savedRead -State $State
                }
                $result = Invoke-CollectionModulePool -Modules @(
                    [pscustomobject]@{ Project = $case.Module; Framework = "fake"; RelativePath = $case.Module }) `
                    -Repository $script:orphanRoot -ResultsDirectory (Join-Path $script:orphanRoot "results") `
                    -DotnetPath $powershellPath -MaximumParallelModules 1 `
                    -CommandPrefix ($prefix + @("argument with spaces", "", "trailing\", 'embedded"quote', ([string][char]0x00E9 + [char]0x6E2C))) `
                    -PackageCacheRoot (Join-Path $script:orphanRoot "cache")
                if ($result.ExitCode -ne $case.ExitCode -or $result.Completed -ne 1 -or
                    -not $result.TeardownConfirmed -or -not $script:orphanChild.WaitForExit(0)) {
                    throw "$($case.Name): SDK completion must not release its cache while an independently launched orphan is running (exit=$($result.ExitCode), completed=$($result.Completed), teardown=$($result.TeardownConfirmed), childExited=$($script:orphanChild.HasExited))."
                }
                Write-PassedCheck $case.Name
            } catch {
                $orphanFailures.Add($_.Exception.Message)
                Write-Output "FAIL $($case.Name): $($_.Exception.Message)"
            } finally {
                Set-Item -LiteralPath function:Read-CollectionModuleOutput -Value $savedRead
                if ($null -ne $script:orphanChild) {
                    if (-not $script:orphanChild.HasExited) {
                        $script:orphanChild.Kill($true)
                        if (-not $script:orphanChild.WaitForExit(10000)) {
                            throw "The owned orphan regression worker did not terminate during cleanup."
                        }
                    }
                    $script:orphanChild.Dispose()
                }
            }
        }
    }
    if ($orphanFailures.Count -gt 0) {
        throw ($orphanFailures -join [Environment]::NewLine)
    }
    Write-Output "Collection module-pool self-tests: $script:passedCheckCount passed."
} finally {
    Remove-Item -LiteralPath $fixture -Recurse -Force
}
