$script:CollectionPackageCacheEnvironmentVariable = "TESTFX_COLLECTION_NUGET_CACHE"
$script:CollectionPackageCacheMarkerFileName = ".testfx-collection-cache"
$script:CollectionPackageCacheMarkerContent = "testfx-collection-cache-v1"

if ($IsWindows -and $null -eq ("TestFx.Collection.WindowsCollectionProcess" -as [type])) {
    $repositoryRoot = Split-Path $PSScriptRoot -Parent
    Add-Type -Path @(
        (Join-Path $PSScriptRoot "collection-windows-process.cs"),
        (Join-Path $repositoryRoot "src\Platform\Microsoft.Testing.Platform\Helpers\PasteArguments.cs"),
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

function Read-CollectionModuleOutput {
    param($State)

    foreach ($stream in @("Output", "Error")) {
        $taskName = "${stream}Task"
        $readerName = "${stream}Reader"
        while ($null -ne $State.$taskName -and $State.$taskName.IsCompleted) {
            $line = $State.$taskName.GetAwaiter().GetResult()
            if ($null -eq $line) {
                $State.$taskName = $null
                break
            }

            $State.Writer.WriteLine($line)
            Write-Host "[$($State.Module.Project) | $($State.Module.Framework)] $line"
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
                    "--show-test-results", "failed", "--no-progress", "--no-ansi"
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
                $active.Add($state)
                $state.OutputReader = $process.StandardOutput
                $state.ErrorReader = $process.StandardError
                $state.OutputTask = $process.StandardOutput.ReadLineAsync()
                $state.ErrorTask = $process.StandardError.ReadLineAsync()
                Write-Host "Starting collection $($nextIndex + 1)/$($Modules.Count), PID $($process.Id): $($module.RelativePath)"
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
                $result = [pscustomobject]@{
                    Index = $state.Index
                    Module = $state.Module
                    ExitCode = $state.Process.ExitCode
                    DurationSeconds = $state.Timer.Elapsed.TotalSeconds
                    Started = $state.Started
                    Finished = [datetimeoffset]::UtcNow
                    LogPath = Join-Path $state.Directory "collection.log"
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
                Stop-CollectionModuleProcess -State $state
                Complete-CollectionModuleOutput -State $state
                $state.Timer.Stop()
                $result = [pscustomobject]@{
                    Index = $state.Index
                    Module = $state.Module
                    ExitCode = $state.Process.ExitCode
                    DurationSeconds = $state.Timer.Elapsed.TotalSeconds
                    Started = $state.Started
                    Finished = [datetimeoffset]::UtcNow
                    LogPath = Join-Path $state.Directory "collection.log"
                    Canceled = $true
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
        Planned = $Modules.Count
        Started = $nextIndex
        Completed = $completed.Count
        CanceledModules = @($canceled.ToArray())
        TeardownConfirmed = $cleanupErrors.Count -eq 0
        CleanupErrors = @($cleanupErrors.ToArray())
        MaximumParallelModules = $MaximumParallelModules
    }
}
