$script:CollectionPackageCacheEnvironmentVariable = "TESTFX_COLLECTION_NUGET_CACHE"
$script:CollectionPackageCacheMarkerFileName = ".testfx-collection-cache"
$script:CollectionPackageCacheMarkerContent = "testfx-collection-cache-v1"

if ($IsWindows -and $null -eq ("TestFx.Collection.WindowsCollectionProcess" -as [type])) {
    $repositoryRoot = Split-Path $PSScriptRoot -Parent
    Add-Type -Path @(
        (Join-Path $PSScriptRoot "collection-windows-process.cs"),
        (Join-Path $repositoryRoot "src\MTP\Microsoft.Testing.Platform\Helpers\PasteArguments.cs"),
        (Join-Path $repositoryRoot "src\Polyfills\EmbeddedAttribute.cs")
    ) -CompilerOptions "/langversion:12.0"
}

function Get-CollectionModuleConcurrency {
    param(
        $ConfiguredBatches,
        [ValidateRange(1, 4)][int]$ChildBudget,
        [ValidateRange(1, 2)][int]$MaximumParallelModules,
        [ValidateRange(1, [int]::MaxValue)][int]$ProcessorCount = [Environment]::ProcessorCount
    )

    if ($null -ne $ConfiguredBatches -and
        (($ConfiguredBatches -isnot [int] -and $ConfiguredBatches -isnot [long]) -or $ConfiguredBatches -notin 1, 2)) {
        throw "The affected-tests collector supports maximumConcurrentBatches only as an integer in [1,2]."
    }

    $batchesPerModule = [Math]::Min($(if ($null -eq $ConfiguredBatches) { 2 } else { $ConfiguredBatches }), $ProcessorCount)
    $effectiveBudget = [Math]::Min($ChildBudget, $ProcessorCount)
    if ($effectiveBudget -lt $batchesPerModule) {
        throw "Child budget $effectiveBudget cannot contain the configured $batchesPerModule batches per module. Set test.affectedTests.maximumConcurrentBatches to 1 before collecting with a one-child budget."
    }

    return [pscustomobject]@{
        ChildBudget = $effectiveBudget
        BatchesPerModule = $batchesPerModule
        MaximumParallelModules = [Math]::Min($MaximumParallelModules, [int][Math]::Floor($effectiveBudget / $batchesPerModule))
    }
}

# The pinned collector refuses to persist a map larger than its fixed 128-MiB limit and fails that module.
# These module/TFM maps reached 127.64 MiB at 1,620 tests and exceeded the limit at 1,626, so they are not collected;
# without a shard, PR selection conservatively runs all of their tests. See docs/affected-test-selection.md.
$script:CollectionMapSizeExclusions = @(
    [pscustomobject]@{ Project = "MSTestAdapter.PlatformServices.UnitTests"; Frameworks = @("net462", "net48") }
)

function Get-MappableCollectionModule {
    param([object[]]$Modules)

    foreach ($module in $Modules) {
        $excluded = @($script:CollectionMapSizeExclusions | Where-Object {
            $_.Project -eq $module.Project -and $_.Frameworks -contains $module.Framework
        }).Count -gt 0
        if ($excluded) {
            Write-Host "Skipping map collection for '$($module.RelativePath)': its map exceeds the collector's 128-MiB limit, so selection runs all of its tests."
            continue
        }

        $module
    }
}

function Get-OrderedCollectionModule {
    param([object[]]$Modules)

    $Modules | Sort-Object @{
        Expression = {
            if ($_.Project -like "*Acceptance.IntegrationTests") { 0 }
            elseif ($_.Project -eq "MSTest.Analyzers.UnitTests") { 1 }
            else { 2 }
        }
    }, RelativePath
}

# Console presentation mirrors the Microsoft.Testing.Platform terminal reporter (TestFrameworkDisplay,
# HumanReadableDurationFormatter, and TargetFrameworkParser). Each module's collection.log keeps the raw output.
function Format-CollectionDuration {
    param([TimeSpan]$Duration)

    $invariant = [System.Globalization.CultureInfo]::InvariantCulture
    $parts = [System.Collections.Generic.List[string]]::new()
    if ($Duration.Days -gt 0) {
        $parts.Add($Duration.Days.ToString($invariant) + "d")
    }

    foreach ($part in @(
        [pscustomobject]@{ Value = $Duration.Hours; Suffix = "h" },
        [pscustomobject]@{ Value = $Duration.Minutes; Suffix = "m" },
        [pscustomobject]@{ Value = $Duration.Seconds; Suffix = "s" }
    )) {
        if ($part.Value -gt 0 -or $parts.Count -gt 0) {
            $value = if ($parts.Count -gt 0) { $part.Value.ToString("D2", $invariant) } else { $part.Value.ToString($invariant) }
            $parts.Add($value + $part.Suffix)
        }
    }

    $milliseconds = if ($parts.Count -gt 0) { $Duration.Milliseconds.ToString("D3", $invariant) } else { $Duration.Milliseconds.ToString($invariant) }
    $parts.Add($milliseconds + "ms")
    return $parts -join " "
}

function ConvertTo-CollectionShortFramework {
    param([string]$Framework)

    if ($Framework -cmatch '^\.NETFramework,Version=v(?<major>\d+)\.(?<minor>\d+)(?:\.(?<patch>\d+))?$') {
        return "net$($Matches.major)$($Matches.minor)$($Matches.patch)"
    }

    if ($Framework -cmatch '^\.NETCoreApp,Version=v(?<major>\d+)\.(?<minor>\d+)$') {
        $prefix = if ([int]$Matches.major -ge 5) { "net" } else { "netcoreapp" }
        return "$prefix$($Matches.major).$($Matches.minor)"
    }

    if ($Framework -cmatch '^\.NETStandard,Version=v(?<version>\d+\.\d+)$') {
        return "netstandard$($Matches.version)"
    }

    return $Framework
}

function ConvertTo-CollectionRuntimeFramework {
    param([string]$Description)

    if ([string]::IsNullOrEmpty($Description)) {
        return $Description
    }

    $netFramework = ".NET Framework"
    if ($Description.StartsWith($netFramework, [StringComparison]::Ordinal)) {
        if ($Description.Length -lt $netFramework.Length + 4) {
            return $Description
        }

        $version = "$($Description[$netFramework.Length + 1])$($Description[$netFramework.Length + 3])"
        if ($Description.Length -ge $netFramework.Length + 6) {
            $patchVersion = $version + $Description[$netFramework.Length + 5]
            if ($patchVersion -cin @("462", "471", "472", "481")) {
                $version = $patchVersion
            }
        }

        return "net$version"
    }

    $netCore = ".NET Core"
    if ($Description.StartsWith($netCore, [StringComparison]::Ordinal)) {
        if ($Description.Length -lt $netCore.Length + 4) {
            return $Description
        }

        return "netcoreapp$($Description[$netCore.Length + 1]).$($Description[$netCore.Length + 3])"
    }

    if ($Description.StartsWith(".NET", [StringComparison]::Ordinal)) {
        $versionStart = 5
        $firstDot = $Description.IndexOf(".", [Math]::Min($versionStart, $Description.Length), [StringComparison]::Ordinal)
        if ($firstDot -le $versionStart -or $firstDot -eq $Description.Length - 1) {
            return $Description
        }

        return "net$($Description.Substring($versionStart, $firstDot - $versionStart)).$($Description[$firstDot + 1])"
    }

    return $Description
}

function Test-CollectionKnownRuntimeFramework {
    param([string]$Framework)

    $version = $null
    if ($Framework.StartsWith("netcoreapp", [StringComparison]::OrdinalIgnoreCase)) {
        return [Version]::TryParse($Framework.Substring(10), [ref]$version)
    }

    if (-not $Framework.StartsWith("net", [StringComparison]::OrdinalIgnoreCase)) {
        return $false
    }

    $suffix = $Framework.Substring(3)
    return [Version]::TryParse($suffix, [ref]$version) -or ($suffix -cmatch '^4[0-9]{1,2}$')
}

function Get-CollectionModuleDisplay {
    param([string]$Framework, [string]$Rid, [string]$Runtime)

    $architecture = if ([string]::IsNullOrWhiteSpace($Rid)) { "" } else { $Rid.Substring($Rid.LastIndexOf("-") + 1) }
    $primary = @($Framework, $architecture | Where-Object { -not [string]::IsNullOrWhiteSpace($_) }) -join "|"
    if ([string]::IsNullOrWhiteSpace($Runtime)) {
        return $primary
    }

    if (-not [string]::IsNullOrWhiteSpace($Framework) -and (Test-CollectionKnownRuntimeFramework -Framework $Runtime)) {
        $baseFramework = $Framework.Split("-")[0]
        if ([string]::Equals($baseFramework, $Runtime, [StringComparison]::OrdinalIgnoreCase)) {
            return $primary
        }
    }

    $runtimeDisplay = "runtime: $Runtime"
    return $(if ($primary.Length -eq 0) { $runtimeDisplay } else { "$primary, $runtimeDisplay" })
}

function Get-CollectionModuleLabel {
    param($Module)

    $parts = @($Module.Project, $Module.Framework | Where-Object { -not [string]::IsNullOrWhiteSpace($_) })
    return "[$($parts -join " ")] "
}

function Get-CollectionModulePath {
    param($Module)

    foreach ($candidate in @($Module.Path, $Module.RelativePath, $Module.Project)) {
        if (-not [string]::IsNullOrWhiteSpace($candidate)) {
            return $candidate
        }
    }

    return ""
}

function Test-CollectionModuleAssembly {
    param($Module, [string]$Assembly)

    foreach ($candidate in @($Module.Path, $Module.RelativePath)) {
        if (-not [string]::IsNullOrWhiteSpace($candidate) -and
            [string]::Equals([System.IO.Path]::GetFileNameWithoutExtension($candidate), $Assembly, [StringComparison]::OrdinalIgnoreCase)) {
            return $true
        }
    }

    return [string]::Equals($Module.Project, $Assembly, [StringComparison]::OrdinalIgnoreCase)
}

function New-CollectionOutputPresenter {
    return [pscustomobject]@{ TelemetryShown = $false }
}

function Get-CollectionOutputPresentation {
    param($State)

    if ($null -eq $State.PSObject.Properties["Presenter"]) {
        $State | Add-Member -NotePropertyName Presenter -NotePropertyValue $null
    }
    if ($null -eq $State.Presenter) {
        $State.Presenter = New-CollectionOutputPresenter
    }
    if ($null -eq $State.PSObject.Properties["Presentation"]) {
        $State | Add-Member -NotePropertyName Presentation -NotePropertyValue ([pscustomobject]@{
            Output = [pscustomobject]@{ PendingBlank = $false; InCompletedBlock = $false }
            Error = [pscustomobject]@{ PendingBlank = $false; InCompletedBlock = $false }
            Discovered = $null
            Tests = $null
            SourcePaths = $null
            MapUri = $null
            Runtime = $null
            Rid = $null
        })
    }

    return $State.Presentation
}

function Get-CollectionConsoleText {
    param($State, $Presentation, [string]$Line)

    # Pipeline commands must stay at the start of their line, as in MTP's contextual module output.
    if ($Line.Contains("##vso[") -or $Line.Contains("##[") -or $Line.TrimStart().StartsWith("::", [StringComparison]::Ordinal)) {
        return $Line
    }

    if ($Line.StartsWith("Telemetry is: ", [StringComparison]::Ordinal)) {
        if ($State.Presenter.TelemetryShown) {
            return $null
        }

        $State.Presenter.TelemetryShown = $true
        return $Line
    }

    if ($Line -cmatch '^Collecting test maps for \d+ module\(s\)\. Child test-result reporting is suppressed\.$') {
        return $null
    }

    $moduleLabel = Get-CollectionModuleLabel -Module $State.Module
    if ($Line -cmatch '^\[affected-tests\] \[(?<assembly>(?:[^|\[\]]|\[[^|\[\]]*\])+) \| (?<framework>[^|\[\]]+) \| (?<runtime>[^|\[\]]+) \| (?<rid>[^|\[\]]+)\] (?<message>.*)$') {
        $assembly = $Matches.assembly
        $framework = $Matches.framework
        $runtime = $Matches.runtime
        $rid = $Matches.rid
        $message = $Matches.message
        $isModule = Test-CollectionModuleAssembly -Module $State.Module -Assembly $assembly
        if ($isModule) {
            $Presentation.Runtime = $runtime
            $Presentation.Rid = $rid
            if ($message -cmatch '^Experimental test-map collection: ' -or
                $message -cmatch '^Mapping timing: \d+ measured batch\(es\), 0 conservative fallback batch\(es\)\.$') {
                return $null
            }

            if ($message -cmatch '^Discovered (?<discovered>\d+) tests; planning to run all \k<discovered> for test-map collection \(not execution results\)\.$') {
                $Presentation.Discovered = [long]$Matches.discovered
                return $null
            }

            if ($message -cmatch "^Collected test map '(?<uri>[^']+)': (?<tests>\d+) discovered test UID\(s\) in map, (?<paths>\d+) source paths, ") {
                $Presentation.MapUri = $Matches.uri
                $Presentation.Tests = [long]$Matches.tests
                $Presentation.SourcePaths = [long]$Matches.paths
                return $null
            }
        }

        if ($message.StartsWith("Test-map collection child failed: MSTest v", [StringComparison]::Ordinal)) {
            $suffix = " [$rid - $runtime]"
            if ($message.EndsWith($suffix, [StringComparison]::Ordinal)) {
                $message = $message.Substring(0, $message.Length - $suffix.Length)
            }
        }

        $label = if ($isModule) { $moduleLabel } else { "[$assembly $(ConvertTo-CollectionShortFramework -Framework $framework)] " }
        return "$label$message"
    }

    if ($Line -cmatch '^\[affected-tests\] \[(?<assembly>(?:[^|\[\]]|\[[^|\[\]]*\])+) \| (?<framework>[^|\[\]]+)\] (?<message>.*)$') {
        $label = if (Test-CollectionModuleAssembly -Module $State.Module -Assembly $Matches.assembly) {
            $moduleLabel
        } else {
            "[$($Matches.assembly) $(ConvertTo-CollectionShortFramework -Framework $Matches.framework)] "
        }
        return "$label$($Matches.message)"
    }

    # Other module-context shapes are not produced today; keep their context verbatim rather than guess.
    if ($Line -cmatch '^\[affected-tests\] \[(?:[^|\[\]]|\[[^|\[\]]*\])+ \| [^\[\]]+\] ') {
        return $Line
    }

    if ($Line.StartsWith("[affected-tests] ", [StringComparison]::Ordinal)) {
        $Line = $Line.Substring("[affected-tests] ".Length)
    }

    return "$moduleLabel$Line"
}

function ConvertTo-CollectionConsoleLine {
    param($State, [string]$Stream, [string]$Line)

    $presentation = Get-CollectionOutputPresentation -State $State
    $streamState = $presentation.$Stream
    if ($streamState.InCompletedBlock) {
        # The module completion line replaces the SDK's per-module summary block.
        if ($Line -cmatch '^  modules finished: \d+/\d+$' -or $Line -cmatch '^  Module completion is not a test verdict\.') {
            return
        }

        $streamState.InCompletedBlock = $false
        if ($Line -cmatch '^  duration: .+$') {
            return
        }
    }

    if ([string]::IsNullOrWhiteSpace($Line)) {
        if ($streamState.PendingBlank) {
            ""
        }
        $streamState.PendingBlank = $true
        return
    }

    if ($Line -ceq "Test map collection: Completed.") {
        $streamState.PendingBlank = $false
        $streamState.InCompletedBlock = $true
        return
    }

    $text = Get-CollectionConsoleText -State $State -Presentation $presentation -Line $Line
    if ($null -eq $text) {
        return
    }

    if ($streamState.PendingBlank) {
        $streamState.PendingBlank = $false
        ""
    }
    $text
}

function Write-CollectionModuleCompletion {
    param($State, [int]$ExitCode, [switch]$Canceled)

    $presentation = Get-CollectionOutputPresentation -State $State
    $display = Get-CollectionModuleDisplay -Framework $State.Module.Framework -Rid $presentation.Rid `
        -Runtime (ConvertTo-CollectionRuntimeFramework -Description $presentation.Runtime)
    $identity = Get-CollectionModulePath -Module $State.Module
    if ($display.Length -gt 0) {
        $identity += " ($display)"
    }

    $outcome = if ($Canceled) {
        "canceled"
    } elseif ($ExitCode -ne 0) {
        "failed with exit code $ExitCode"
    } elseif ($null -ne $presentation.Tests) {
        $tests = if ($presentation.Tests -eq 1) { "1 test" } else { "$($presentation.Tests) tests" }
        $paths = if ($presentation.SourcePaths -eq 1) { "1 source path" } else { "$($presentation.SourcePaths) source paths" }
        "mapped $tests to $paths"
    } else {
        "completed without a test map"
    }

    Write-Host ""
    Write-Host "$identity $outcome ($(Format-CollectionDuration -Duration $State.Timer.Elapsed))"
}

function Read-CollectionModuleOutput {
    param($State)

    foreach ($stream in @("Output", "Error")) {
        $taskName = "${stream}Task"
        $readerName = "${stream}Reader"
        while ($null -ne $State.$taskName -and $State.$taskName.IsCompleted) {
            $line = $State.$taskName.GetAwaiter().GetResult()
            if ($null -eq $line) {
                $State.$taskName = $null
                $streamState = (Get-CollectionOutputPresentation -State $State).$stream
                $streamState.PendingBlank = $false
                $streamState.InCompletedBlock = $false
                break
            }

            $State.Writer.WriteLine($line)
            foreach ($text in @(ConvertTo-CollectionConsoleLine -State $State -Stream $stream -Line $line)) {
                Write-Host $text
            }
            $State.$taskName = $State.$readerName.ReadLineAsync()
        }
    }
}

function Stop-CollectionModuleProcess {
    param($State)

    if ($State.JobOwned) {
        $State.Process.StopTree(15000)
        return
    }

    $descendants = [System.Collections.Generic.List[System.Diagnostics.Process]]::new()
    $captureError = $null
    $timer = [System.Diagnostics.Stopwatch]::StartNew()
    try {
        try {
            if ($IsWindows -and -not $State.Process.HasExited) {
                $snapshot = @(Get-CimInstance Win32_Process -Property ProcessId, ParentProcessId, CreationDate)
                $ownedIds = [System.Collections.Generic.HashSet[int]]::new()
                $ownedIds.Add($State.Process.Id) | Out-Null
                do {
                    $added = $false
                    foreach ($child in $snapshot) {
                        if (-not $ownedIds.Contains([int]$child.ParentProcessId) -or $ownedIds.Contains([int]$child.ProcessId)) {
                            continue
                        }

                        $ownedIds.Add([int]$child.ProcessId) | Out-Null
                        $added = $true
                        try {
                            $process = [System.Diagnostics.Process]::GetProcessById([int]$child.ProcessId)
                        } catch [System.ArgumentException] {
                            continue
                        }

                        try {
                            if ([Math]::Abs(($process.StartTime.ToUniversalTime() - $child.CreationDate.ToUniversalTime()).TotalMilliseconds) -gt 1) {
                                $process.Dispose()
                                continue
                            }

                            # Retain the original handle: a reused PID must not be joined or killed.
                            $null = $process.Handle
                            $descendants.Add($process)
                        } catch [System.InvalidOperationException] {
                            $process.Dispose()
                        }
                    }
                } while ($added)
            }
        } catch {
            $captureError = $_
        }

        if (-not $State.Process.HasExited) {
            try {
                $State.Process.Kill($true)
            } catch [System.InvalidOperationException] {
                if (-not $State.Process.HasExited) {
                    throw
                }
            }
        }

        foreach ($process in $descendants) {
            if (-not $process.HasExited) {
                try {
                    $process.Kill($true)
                } catch [System.InvalidOperationException] {
                    if (-not $process.HasExited) {
                        throw
                    }
                }
            }
        }

        foreach ($process in @($State.Process) + @($descendants.ToArray())) {
            $remaining = [Math]::Max(0, 15000 - [int]$timer.ElapsedMilliseconds)
            if (-not $process.WaitForExit($remaining)) {
                throw "Collection child PID $($process.Id) did not terminate after cancellation."
            }
        }

        if ($null -ne $captureError) {
            throw $captureError
        }
    } finally {
        foreach ($process in $descendants) {
            $process.Dispose()
        }
    }
}

function Complete-CollectionModuleOutput {
    param($State)

    $drain = [System.Diagnostics.Stopwatch]::StartNew()
    while ($null -ne $State.OutputTask -or $null -ne $State.ErrorTask) {
        Read-CollectionModuleOutput -State $State
        if ($drain.Elapsed.TotalSeconds -ge 15) {
            throw "Collection output for PID $($State.Process.Id) did not drain after process termination."
        }

        if ($null -ne $State.OutputTask -or $null -ne $State.ErrorTask) {
            Start-Sleep -Milliseconds 10
        }
    }
}

function Get-CollectionModuleFailure {
    param($States, $Failure)

    foreach ($state in $States) {
        if ($state.Process.HasExited -and $state.Process.ExitCode -ne 0 -and
            ($null -eq $Failure -or $state.Index -lt $Failure.Index)) {
            $Failure = [pscustomobject]@{
                Index = $state.Index
                ExitCode = $state.Process.ExitCode
            }
        }
    }

    return $Failure
}

function Invoke-CollectionModulePool {
    param(
        [object[]]$Modules,
        [string]$Repository,
        [string]$ResultsDirectory,
        [string]$DotnetPath,
        [ValidateRange(1, 2)][int]$MaximumParallelModules,
        [string[]]$CommandPrefix = @(),
        [string]$PackageCacheRoot
    )

    if ($Modules.Count -eq 0 -or $MaximumParallelModules -le 0) {
        throw "Collection requires at least one module and a positive concurrency limit."
    }

    New-Item -ItemType Directory -Path $ResultsDirectory -Force | Out-Null
    $active = [System.Collections.Generic.List[object]]::new()
    $completed = [System.Collections.Generic.List[object]]::new()
    $nextIndex = 0
    $failure = $null
    $poolError = $null
    $cleanupErrors = [System.Collections.Generic.List[string]]::new()
    $canceled = [System.Collections.Generic.List[object]]::new()
    $presenter = New-CollectionOutputPresenter
    try {
        while ($nextIndex -lt $Modules.Count -or $active.Count -gt 0) {
            $failure = Get-CollectionModuleFailure -States $active -Failure $failure
            while ($null -eq $failure -and $nextIndex -lt $Modules.Count -and $active.Count -lt $MaximumParallelModules) {
                $module = $Modules[$nextIndex]
                $moduleDirectory = Join-Path $ResultsDirectory ("module-{0:D3}" -f $nextIndex)
                New-Item -ItemType Directory -Path $moduleDirectory | Out-Null
                $startInfo = [System.Diagnostics.ProcessStartInfo]::new()
                $startInfo.FileName = $DotnetPath
                $startInfo.WorkingDirectory = $Repository
                $startInfo.UseShellExecute = $false
                $startInfo.CreateNoWindow = $true
                $startInfo.RedirectStandardOutput = $true
                $startInfo.RedirectStandardError = $true
                foreach ($argument in $CommandPrefix + @(
                    "test", "--test-modules", $module.RelativePath,
                    "--root-directory", $Repository,
                    "--no-build", "--collect-test-map",
                    "--results-directory", $moduleDirectory,
                    "--show-test-results", "failed", "--progress", "off",
                    # The native SDK owns this option; MTP's --ansi off does not configure its renderer.
                    "--no-ansi"
                )) {
                    $startInfo.ArgumentList.Add($argument)
                }

                $startInfo.Environment["DOTNET_CLI_ENABLE_AFFECTED_TESTS"] = "1"
                $startInfo.Environment.Remove($script:CollectionPackageCacheEnvironmentVariable) | Out-Null
                if (-not [string]::IsNullOrEmpty($PackageCacheRoot)) {
                    $cacheDirectory = Join-Path $PackageCacheRoot ("module-{0:D3}" -f $nextIndex)
                    New-Item -ItemType Directory -Path $cacheDirectory -Force | Out-Null
                    Set-Content -LiteralPath (Join-Path $cacheDirectory $script:CollectionPackageCacheMarkerFileName) -Value $script:CollectionPackageCacheMarkerContent -Encoding utf8
                    $startInfo.Environment[$script:CollectionPackageCacheEnvironmentVariable] = $cacheDirectory
                }
                foreach ($variable in @("DOTNET_CLI_TEST_AFFECTED_TESTS_MODE", "TESTINGPLATFORM_EXITCODE_IGNORE")) {
                    $startInfo.Environment.Remove($variable) | Out-Null
                }

                $process = $null
                # Completion and error cleanup both dispose the writer, flushing its buffered output.
                $writer = [System.IO.StreamWriter]::new((Join-Path $moduleDirectory "collection.log"), $false, [System.Text.UTF8Encoding]::new($false))
                $state = [pscustomobject]@{
                    Index = $nextIndex
                    Module = $module
                    Process = $process
                    ProcessId = $null
                    Presenter = $presenter
                    JobOwned = $IsWindows
                    Writer = $writer
                    OutputReader = $null
                    ErrorReader = $null
                    OutputTask = $null
                    ErrorTask = $null
                    Started = [datetimeoffset]::UtcNow
                    Timer = [System.Diagnostics.Stopwatch]::StartNew()
                    Directory = $moduleDirectory
                }
                try {
                    if ($IsWindows) {
                        $process = [TestFx.Collection.WindowsCollectionProcess]::Start($startInfo)
                    } else {
                        $process = [System.Diagnostics.Process]::new()
                        $process.StartInfo = $startInfo
                        if (-not $process.Start()) {
                            throw "Could not start collection for '$($module.RelativePath)'."
                        }
                    }
                } catch {
                    $writer.Dispose()
                    if ($null -ne $process) {
                        $process.Dispose()
                    }
                    throw
                }

                $state.Process = $process
                $state.ProcessId = $process.Id
                $active.Add($state)
                $state.OutputReader = $process.StandardOutput
                $state.ErrorReader = $process.StandardError
                $state.OutputTask = $process.StandardOutput.ReadLineAsync()
                $state.ErrorTask = $process.StandardError.ReadLineAsync()
                $startDisplay = Get-CollectionModuleDisplay -Framework $module.Framework
                $startIdentity = Get-CollectionModulePath -Module $module
                if ($startDisplay.Length -gt 0) {
                    $startIdentity += " ($startDisplay)"
                }
                Write-Host "Collecting test map from $startIdentity"
                $nextIndex++
            }

            foreach ($state in @($active.ToArray())) {
                Read-CollectionModuleOutput -State $state
                if (-not $state.Process.HasExited) {
                    continue
                }

                $state.Process.WaitForExit()
                if ($state.JobOwned) {
                    $state.Process.StopTree(15000)
                }
                Complete-CollectionModuleOutput -State $state

                $state.Timer.Stop()
                Write-CollectionModuleCompletion -State $state -ExitCode $state.Process.ExitCode
                $presentation = Get-CollectionOutputPresentation -State $state
                $result = [pscustomobject]@{
                    Index = $state.Index
                    Module = $state.Module
                    ProcessId = $state.ProcessId
                    ExitCode = $state.Process.ExitCode
                    DurationSeconds = $state.Timer.Elapsed.TotalSeconds
                    Started = $state.Started
                    Finished = [datetimeoffset]::UtcNow
                    LogPath = Join-Path $state.Directory "collection.log"
                    Discovered = $presentation.Discovered
                    Tests = $presentation.Tests
                    SourcePaths = $presentation.SourcePaths
                    MapUri = $presentation.MapUri
                    Runtime = $presentation.Runtime
                    Rid = $presentation.Rid
                }
                $result | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $state.Directory "module.json") -Encoding utf8
                $completed.Add($result)
                if ($result.ExitCode -ne 0 -and ($null -eq $failure -or $result.Index -lt $failure.Index)) {
                    $failure = $result
                }

                $active.Remove($state) | Out-Null
                try {
                    $state.Writer.Dispose()
                } finally {
                    $state.Process.Dispose()
                }
            }

            if ($null -ne $failure) {
                break
            }

            if ($active.Count -gt 0) {
                Start-Sleep -Milliseconds 10
            }
        }
    } catch {
        $poolError = $_
        $failure = Get-CollectionModuleFailure -States $active -Failure $failure
    } finally {
        foreach ($state in $active) {
            try {
                # A child that already failed natively is the reported failure, not a canceled peer.
                $nativeFailure = $state.Process.HasExited -and $state.Process.ExitCode -ne 0
                Stop-CollectionModuleProcess -State $state
                Complete-CollectionModuleOutput -State $state
                $state.Timer.Stop()
                Write-CollectionModuleCompletion -State $state -ExitCode $state.Process.ExitCode -Canceled:(-not $nativeFailure)
                $presentation = Get-CollectionOutputPresentation -State $state
                $result = [pscustomobject]@{
                    Index = $state.Index
                    Module = $state.Module
                    ProcessId = $state.ProcessId
                    ExitCode = $state.Process.ExitCode
                    DurationSeconds = $state.Timer.Elapsed.TotalSeconds
                    Started = $state.Started
                    Finished = [datetimeoffset]::UtcNow
                    LogPath = Join-Path $state.Directory "collection.log"
                    Discovered = $presentation.Discovered
                    Tests = $presentation.Tests
                    SourcePaths = $presentation.SourcePaths
                    MapUri = $presentation.MapUri
                    Runtime = $presentation.Runtime
                    Rid = $presentation.Rid
                    Canceled = -not $nativeFailure
                }
                $result | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $state.Directory "module.json") -Encoding utf8
                $canceled.Add($result)
            } catch {
                $message = "Collection cleanup for PID $($state.Process.Id) failed: $($_.Exception.Message)"
                $cleanupErrors.Add($message)
                Write-Warning $message
            } finally {
                try {
                    $state.Writer.Dispose()
                } catch {
                    $cleanupErrors.Add($_.Exception.Message)
                    Write-Warning "Collection log cleanup failed: $($_.Exception.Message)"
                } finally {
                    $state.Process.Dispose()
                }
            }
        }
    }

    if ($null -ne $poolError) {
        if ($null -eq $failure) {
            $poolError.Exception.Data["CollectionCleanupErrors"] = @($cleanupErrors.ToArray())
            throw $poolError
        }

        $cleanupErrors.Add($poolError.Exception.Message)
        Write-Warning "Collection failed with exit $($failure.ExitCode); additional orchestration error: $($poolError.Exception.Message)"
    }

    return [pscustomobject]@{
        ExitCode = $(if ($null -ne $failure) { $failure.ExitCode } else { 0 })
        Modules = @($completed.ToArray() | Sort-Object Index)
        TestsMapped = [long](@($completed.ToArray() | Where-Object { $null -ne $_.Tests } | ForEach-Object { $_.Tests }) | Measure-Object -Sum).Sum
        Planned = $Modules.Count
        Started = $nextIndex
        Completed = $completed.Count
        CanceledModules = @($canceled.ToArray())
        TeardownConfirmed = $cleanupErrors.Count -eq 0
        CleanupErrors = @($cleanupErrors.ToArray())
        MaximumParallelModules = $MaximumParallelModules
    }
}
