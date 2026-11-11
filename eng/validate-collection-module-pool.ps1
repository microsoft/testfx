[CmdletBinding()]
param()

$ErrorActionPreference = "Stop"
. (Join-Path $PSScriptRoot "collection-module-pool.ps1")
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
        -not $fixtureScript.Contains('CollectionPackageCache.Select(')) {
        throw "CollectionPoolWiringPreservesBudgets: Windows collection must use the bounded pool, prioritize known long workloads and retain the cache opt-in contract."
    }
    Write-Output "PASS CollectionPoolWiringPreservesBudgets"

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
    Write-Output "PASS ModulePoolNeverMultipliesChildBudget"

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
    Write-Output "PASS ModulePoolRejectsInvalidOrOversubscribedConfiguration"

    $ordered = @(Get-OrderedCollectionModule -Modules @(
        [pscustomobject]@{ Project = "Small.UnitTests"; RelativePath = "a" },
        [pscustomobject]@{ Project = "MSTest.Analyzers.UnitTests"; RelativePath = "b" },
        [pscustomobject]@{ Project = "MSTest.Acceptance.IntegrationTests"; RelativePath = "c" },
        [pscustomobject]@{ Project = "Microsoft.Testing.Platform.Acceptance.IntegrationTests"; RelativePath = "d" }
    ))
    if (($ordered.RelativePath -join ",") -ne "c,d,b,a") {
        throw "ModulePoolStartsLargestKnownWorkloadsFirst: long acceptance/analyzer work must not be delayed behind small modules."
    }
    Write-Output "PASS ModulePoolStartsLargestKnownWorkloadsFirst"

    $outputCases = @(
        @{
            Name = "ModuleOutputDoesNotRepeatAffectedTestContext"
            Line = "[affected-tests] [Microsoft.Testing.Platform.Acceptance.IntegrationTests | .NETCoreApp,Version=v12.0 | .NET 12.0.0-alpha.1.26509.101 | win-x64] Test-map collection child failed: MSTest v5.0.0-preview.26553.4"
            Expected = "[affected-tests] [Microsoft.Testing.Platform.Acceptance.IntegrationTests | .NETCoreApp,Version=v12.0 | .NET 12.0.0-alpha.1.26509.101 | win-x64] Test-map collection child failed: MSTest v5.0.0-preview.26553.4"
        },
        @{
            Name = "ModuleOutputPreservesAlternateAssemblyContext"
            Line = "[affected-tests] [Shared.TestAssembly | .NETFramework,Version=v4.6.2 | .NET Framework 4.8 | win-x64] Discovered 2 tests."
            Expected = "[affected-tests] [Shared.TestAssembly | .NETFramework,Version=v4.6.2 | .NET Framework 4.8 | win-x64] Discovered 2 tests."
        },
        @{
            Name = "ModuleOutputPreservesBracketedModuleContext"
            Line = '[affected-tests] [Sample.Tests[Rows](Debug)+Long.Component.Name | net12.0 | .NET 12.0 | win-x64] Child failed: "quoted <value> & details".'
            Expected = '[affected-tests] [Sample.Tests[Rows](Debug)+Long.Component.Name | net12.0 | .NET 12.0 | win-x64] Child failed: "quoted <value> & details".'
        },
        @{
            Name = "ModuleOutputDoesNotRepeatRuntimeAndRid"
            Line = "[affected-tests] [Microsoft.Testing.Platform.Acceptance.IntegrationTests | .NETCoreApp,Version=v12.0 | .NET 12.0.0-alpha.1.26509.101 | win-x64] Test-map collection child failed: MSTest v5.0.0-preview.26553.4 (UTC 11/4/2026) [win-x64 - .NET 12.0.0-alpha.1.26509.101]"
            Expected = "[affected-tests] [Microsoft.Testing.Platform.Acceptance.IntegrationTests | .NETCoreApp,Version=v12.0 | .NET 12.0.0-alpha.1.26509.101 | win-x64] Test-map collection child failed: MSTest v5.0.0-preview.26553.4 (UTC 11/4/2026)"
        },
        @{
            Name = "ModuleOutputDoesNotRepeatRuntimeAndRidForBracketedAssembly"
            Line = "[affected-tests] [Sample.Tests[Rows](Debug) | net12.0 | .NET 12.0 | win-x64] Test-map collection child failed: MSTest v5.0.0 [win-x64 - .NET 12.0]"
            Expected = "[affected-tests] [Sample.Tests[Rows](Debug) | net12.0 | .NET 12.0 | win-x64] Test-map collection child failed: MSTest v5.0.0"
        },
        @{
            Name = "ModuleOutputPreservesDifferentChildRuntime"
            Line = "[affected-tests] [Sample.Tests | net12.0 | .NET 12.0 | win-x64] Test-map collection child failed: MSTest v5.0.0 [win-x64 - .NET 11.0]"
            Expected = "[affected-tests] [Sample.Tests | net12.0 | .NET 12.0 | win-x64] Test-map collection child failed: MSTest v5.0.0 [win-x64 - .NET 11.0]"
        },
        @{
            Name = "ModuleOutputPreservesDifferentChildRid"
            Line = "[affected-tests] [Sample.Tests | net12.0 | .NET 12.0 | win-x64] Test-map collection child failed: MSTest v5.0.0 [win-arm64 - .NET 12.0]"
            Expected = "[affected-tests] [Sample.Tests | net12.0 | .NET 12.0 | win-x64] Test-map collection child failed: MSTest v5.0.0 [win-arm64 - .NET 12.0]"
        },
        @{
            Name = "ModuleOutputPreservesUnscopedRuntimeAndRid"
            Line = "Test-map collection child failed: MSTest v5.0.0 [win-x64 - .NET 12.0]"
            Expected = "[Sample.Tests | net12.0] Test-map collection child failed: MSTest v5.0.0 [win-x64 - .NET 12.0]"
        },
        @{
            Name = "ModuleOutputPreservesRuntimeAndRidWithoutFullContext"
            Line = "[affected-tests] [Sample.Tests | net12.0] Test-map collection child failed: MSTest v5.0.0 [win-x64 - .NET 12.0]"
            Expected = "[affected-tests] [Sample.Tests | net12.0] Test-map collection child failed: MSTest v5.0.0 [win-x64 - .NET 12.0]"
        },
        @{
            Name = "ModuleOutputPreservesEmbeddedBannerAfterShortContext"
            Line = "[affected-tests] [Sample.Tests | net12.0] Child output: [worker | .NET 12.0 | win-x64] Test-map collection child failed: MSTest v5.0.0 [win-x64 - .NET 12.0]"
            Expected = "[affected-tests] [Sample.Tests | net12.0] Child output: [worker | .NET 12.0 | win-x64] Test-map collection child failed: MSTest v5.0.0 [win-x64 - .NET 12.0]"
        },
        @{
            Name = "ModuleOutputPreservesNonBannerRuntimeAndRid"
            Line = "[affected-tests] [Sample.Tests | net12.0 | .NET 12.0 | win-x64] Test-map collection child failed: Assertion expected [win-x64 - .NET 12.0]"
            Expected = "[affected-tests] [Sample.Tests | net12.0 | .NET 12.0 | win-x64] Test-map collection child failed: Assertion expected [win-x64 - .NET 12.0]"
        },
        @{
            Name = "ModuleOutputPreservesBannerFollowedByDiagnostics"
            Line = "[affected-tests] [Sample.Tests | net12.0 | .NET 12.0 | win-x64] Test-map collection child failed: MSTest v5.0.0 [win-x64 - .NET 12.0] Additional failure details."
            Expected = "[affected-tests] [Sample.Tests | net12.0 | .NET 12.0 | win-x64] Test-map collection child failed: MSTest v5.0.0 [win-x64 - .NET 12.0] Additional failure details."
        },
        @{
            Name = "ModuleOutputPrefixesUnscopedDiagnostics"
            Line = "Test run failed with exit code 37."
            Expected = "[Sample.Tests | net12.0] Test run failed with exit code 37."
        },
        @{
            Name = "ModuleOutputPrefixesUnscopedAffectedTestDiagnostics"
            Line = "[affected-tests] Resolving test-map storage."
            Expected = "[Sample.Tests | net12.0] [affected-tests] Resolving test-map storage."
        },
        @{
            Name = "ModuleOutputPrefixesBracketedNonModuleDiagnostics"
            Line = "[affected-tests] [warning] No test map is available."
            Expected = "[Sample.Tests | net12.0] [affected-tests] [warning] No test map is available."
        },
        @{
            Name = "ModuleOutputPrefixesDiagnosticsContainingBracketedText"
            Line = "[affected-tests] [warning] saw [a | b] in output"
            Expected = "[Sample.Tests | net12.0] [affected-tests] [warning] saw [a | b] in output"
        },
        @{
            Name = "ModuleOutputPreservesEmbeddedBannerInUnscopedDiagnostic"
            Line = "[affected-tests] [warning] saw [Sample.Tests | net12.0 | .NET 12.0 | win-x64] Test-map collection child failed: MSTest v5.0.0 [win-x64 - .NET 12.0]"
            Expected = "[Sample.Tests | net12.0] [affected-tests] [warning] saw [Sample.Tests | net12.0 | .NET 12.0 | win-x64] Test-map collection child failed: MSTest v5.0.0 [win-x64 - .NET 12.0]"
        },
        @{
            Name = "ModuleOutputPrefixesIncompleteModuleContext"
            Line = "[affected-tests] [Sample.Tests | net12.0"
            Expected = "[Sample.Tests | net12.0] [affected-tests] [Sample.Tests | net12.0"
        },
        @{
            Name = "ModuleOutputPrefixesEmbeddedModuleContext"
            Line = "Child output: [affected-tests] [Sample.Tests | net12.0] Failed."
            Expected = "[Sample.Tests | net12.0] Child output: [affected-tests] [Sample.Tests | net12.0] Failed."
        },
        @{
            Name = "ModuleOutputPrefixesBlankLines"
            Line = ""
            Expected = "[Sample.Tests | net12.0] "
        }
    )
    $outputFailures = [System.Collections.Generic.List[string]]::new()
    foreach ($case in $outputCases) {
        $previousFailureCount = $outputFailures.Count
        foreach ($stream in @("Output", "Error")) {
            $reader = [System.IO.StringReader]::new($case.Line + [Environment]::NewLine)
            $writer = [System.IO.StringWriter]::new()
            try {
                $state = [pscustomobject]@{
                    Module = [pscustomobject]@{ Project = "Sample.Tests"; Framework = "net12.0" }
                    Writer = $writer
                    OutputReader = $null
                    ErrorReader = $null
                    OutputTask = $null
                    ErrorTask = $null
                }
                $state."${stream}Reader" = $reader
                $state."${stream}Task" = $reader.ReadLineAsync()
                $output = @(Read-CollectionModuleOutput -State $state 6>&1)
                if ($output.Count -ne 1 -or $output[0].ToString() -cne $case.Expected) {
                    $outputFailures.Add("$($case.Name) ($stream): expected '$($case.Expected)', got '$($output -join " | ")'.")
                }
                if ($writer.ToString() -cne ($case.Line + [Environment]::NewLine) -or
                    $null -ne $state.OutputTask -or $null -ne $state.ErrorTask) {
                    $outputFailures.Add("$($case.Name) ($stream): the original log line must be preserved and the stream drained.")
                }
            } finally {
                $reader.Dispose()
                $writer.Dispose()
            }
        }
        if ($outputFailures.Count -eq $previousFailureCount) {
            Write-Output "PASS $($case.Name)"
        }
    }
    if ($outputFailures.Count -gt 0) {
        throw ($outputFailures -join [Environment]::NewLine)
    }

    $worker = Join-Path $fixture "worker.ps1"
    @'
$ErrorActionPreference = "Stop"
if ($args.Count -gt 0 -and $args[0] -eq "orphan-middle") {
    $child = Start-Process -FilePath (Get-Process -Id $PID).Path `
        -ArgumentList @("-NoProfile", "-File", "`"$PSCommandPath`"", "grandchild", "`"$($args[1])`"") `
        -WindowStyle Hidden -PassThru `
        -RedirectStandardOutput (Join-Path $args[1] "grandchild-output.log") `
        -RedirectStandardError (Join-Path $args[1] "grandchild-error.log")
    try {
        $deadline = [datetimeoffset]::UtcNow.AddSeconds(10)
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
    Set-Content -LiteralPath (Join-Path $args[1] "grandchild.started") -Value $PID
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
        if (-not $child.WaitForExit(10000) -or $child.ExitCode -ne 0) {
            throw "The intermediate parent did not finish before SDK completion."
        }
    } finally {
        $child.Dispose()
    }
    Set-Content -LiteralPath (Join-Path $root "orphan.ready") -Value $PID
    $deadline = [datetimeoffset]::UtcNow.AddSeconds(10)
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
    $deadline = [datetimeoffset]::UtcNow.AddSeconds(10)
    while (-not (Test-Path -LiteralPath (Join-Path $root "grandchild.started"))) {
        if ([datetimeoffset]::UtcNow -ge $deadline) {
            throw "The child never started its owned descendant."
        }
        Start-Sleep -Milliseconds 10
    }
}
Write-Output "$module started"
Set-Content -LiteralPath (Join-Path $root "$module.started") -Value $PID
if ($module -eq "failure") {
    if (Test-Path -LiteralPath (Join-Path $root "cancel-peer-mode")) {
        $deadline = [datetimeoffset]::UtcNow.AddSeconds(10)
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
    $deadline = [datetimeoffset]::UtcNow.AddSeconds(10)
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
'@ | Set-Content -LiteralPath $worker -Encoding utf8
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

    Write-Output "PASS ModulePoolRefillsWithoutWaveBarrier"

    $modules = @("failure", "never-started") | ForEach-Object {
        [pscustomobject]@{ Project = $_; Framework = "fake"; RelativePath = $_ }
    }
    $result = Invoke-CollectionModulePool -Modules $modules -Repository $fixture -ResultsDirectory (Join-Path $fixture "failure") `
        -DotnetPath $powershellPath -MaximumParallelModules 1 -CommandPrefix $prefix
    $expectedFailureLog = @'
failure started

'@
    if ($result.ExitCode -ne 37 -or $result.Started -ne 1 -or
        (Test-Path -LiteralPath (Join-Path $fixture "never-started.started")) -or
        (Get-Content -LiteralPath $result.Modules[0].LogPath -Raw).Replace("`r`n", "`n") -ne $expectedFailureLog.Replace("`r`n", "`n")) {
        throw "ModulePoolPreservesFailure: a native failure must stop new launches and preserve its exit code."
    }

    Write-Output "PASS ModulePoolPreservesFailure"

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
    Write-Output "PASS ModulePoolPreservesBufferedErrorOutput"

    $savedRead = (Get-Command Read-CollectionModuleOutput).ScriptBlock
    try {
        function Read-CollectionModuleOutput {
            param($State)
            $deadline = [datetimeoffset]::UtcNow.AddSeconds(10)
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
    Write-Output "PASS ModulePoolPreservesNativeFailureDuringOutputError"

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
        (Get-Process -Id ([int]$pidText) -ErrorAction SilentlyContinue) -or
        (Get-Process -Id $grandchildPid -ErrorAction SilentlyContinue) -or
        (Get-Content -LiteralPath $result.CanceledModules[0].LogPath -Raw).Replace("`r`n", "`n") -ne $expectedCanceledLog.Replace("`r`n", "`n")) {
        throw "ModulePoolDrainsFailedPeers: the still-running child must terminate and retain its log before pool ownership is released."
    }

    Write-Output "PASS ModulePoolDrainsFailedPeers"

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
    Write-Output "PASS ModulePoolPreservesFailureDuringCleanupError"

    $savedRead = (Get-Command Read-CollectionModuleOutput).ScriptBlock
    $script:injectOutputFailure = $true
    try {
        function Read-CollectionModuleOutput {
            param($State)
            if ($script:injectOutputFailure) {
                $deadline = [datetimeoffset]::UtcNow.AddSeconds(10)
                while (-not (Test-Path -LiteralPath (Join-Path $fixture "cleanup-first.started")) -or
                    -not (Test-Path -LiteralPath (Join-Path $fixture "cleanup-second.started"))) {
                    if ([datetimeoffset]::UtcNow -ge $deadline) {
                        throw "Cleanup peers did not start."
                    }
                    Start-Sleep -Milliseconds 10
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
        $peerIds = @("cleanup-first", "cleanup-second") | ForEach-Object {
            [int](Get-Content -LiteralPath (Join-Path $fixture "$_.started") -Raw)
        }
        if ($null -eq $caught -or -not $caught.Exception.Message.Contains("Injected primary output failure.") -or
            @($caught.Exception.Data["CollectionCleanupErrors"]).Count -ne 1 -or
            @($peerIds | ForEach-Object { Get-Process -Id $_ -ErrorAction SilentlyContinue }).Count -ne 0) {
            throw "ModulePoolDrainsEveryPeerAfterCleanupError: a cleanup failure must not mask the primary error or abandon another owned child."
        }
    } finally {
        Set-Item -LiteralPath function:Read-CollectionModuleOutput -Value $savedRead
        Set-Item -LiteralPath function:Stop-CollectionModuleProcess -Value $savedStop
        foreach ($name in @("cleanup-first", "cleanup-second")) {
            $marker = Join-Path $fixture "$name.started"
            if (Test-Path -LiteralPath $marker) {
                $process = Get-Process -Id ([int](Get-Content -LiteralPath $marker -Raw)) -ErrorAction SilentlyContinue
                if ($null -ne $process) {
                    Stop-CollectionModuleProcess -State ([pscustomobject]@{ Process = $process })
                    $process.Dispose()
                }
            }
        }
    }
    Write-Output "PASS ModulePoolDrainsEveryPeerAfterCleanupError"

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

    Write-Output "PASS ModulePoolPreservesLaunchError"
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
                        $deadline = [datetimeoffset]::UtcNow.AddSeconds(10)
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
                Write-Output "PASS $($case.Name)"
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
    $passedCount = $(if ($IsWindows) { 14 } else { 12 }) + $outputCases.Count
    Write-Output "Collection module-pool self-tests: $passedCount passed."
} finally {
    Remove-Item -LiteralPath $fixture -Recurse -Force
}
