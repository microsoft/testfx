[CmdletBinding()]
param(
    [string]$Repository = (Split-Path $PSScriptRoot -Parent),
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Release",
    [ValidateRange(1, 4)]
    [int]$ChildBudget = [Math]::Min([Environment]::ProcessorCount, 4),
    [ValidateRange(1, 2)]
    [int]$MaximumParallelModules = 2,
    [string]$ResultsDirectory,
    [switch]$DisablePackageCacheReuse
)

$ErrorActionPreference = "Stop"
. (Join-Path $PSScriptRoot "collection-test-modules.ps1")
. (Join-Path $PSScriptRoot "collection-module-pool.ps1")

$Repository = [System.IO.Path]::GetFullPath($Repository)
$globalJson = Get-Content -LiteralPath (Join-Path $Repository "global.json") -Raw | ConvertFrom-Json
$concurrency = Get-CollectionModuleConcurrency -ConfiguredBatches $globalJson.test.affectedTests.maximumConcurrentBatches `
    -ChildBudget $ChildBudget -MaximumParallelModules $MaximumParallelModules
$batchesPerModule = $concurrency.BatchesPerModule
$effectiveBudget = $concurrency.ChildBudget
$slots = $concurrency.MaximumParallelModules
$modules = @(Get-OrderedCollectionModule -Modules @(Get-MappableCollectionModule -Modules @(
    Get-CTSComparisonModule -Repository $Repository -Configuration $Configuration)))
$dotnetPath = (Get-Command dotnet -CommandType Application | Select-Object -First 1).Source
$resultsRoot = if ([string]::IsNullOrEmpty($ResultsDirectory)) {
    Join-Path -Path $Repository -ChildPath "artifacts" -AdditionalChildPath @("TestResults", $Configuration, "AffectedCollection")
} else {
    [System.IO.Path]::GetFullPath($ResultsDirectory)
}
$resultsDirectory = Join-Path $resultsRoot ([guid]::NewGuid().ToString("N"))
$moduleNoun = if ($modules.Count -eq 1) { "module" } else { "modules" }
Write-Host "Collecting test maps for $($modules.Count) $moduleNoun (up to $slots at a time, at most $($slots * $batchesPerModule) instrumentation children)."
$timer = [System.Diagnostics.Stopwatch]::StartNew()
$sourceRevision = & git -C $Repository rev-parse HEAD
if ($LASTEXITCODE -ne 0) {
    throw "Could not read the collection source revision."
}

$result = $null
$packageCacheRoot = if ($DisablePackageCacheReuse) {
    $null
} else {
    Join-Path -Path $Repository -ChildPath "artifacts" -AdditionalChildPath @("tmp", $Configuration, "collection-cache", [guid]::NewGuid().ToString("N"))
}
try {
    $result = Invoke-CollectionModulePool -Modules $modules -Repository $Repository -ResultsDirectory $resultsDirectory `
        -DotnetPath $dotnetPath -MaximumParallelModules $slots -PackageCacheRoot $packageCacheRoot
} finally {
    $timer.Stop()
    $metadataError = $null
    try {
        @{
            SourceRevision = $sourceRevision
            ChildBudget = $effectiveBudget
            BatchesPerModule = $batchesPerModule
            PackageCacheReuse = -not $DisablePackageCacheReuse
            DurationSeconds = $timer.Elapsed.TotalSeconds
            Result = $result
            CompletedNormally = $null -ne $result
        } | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $resultsDirectory "collection.json") -Encoding utf8
    } catch {
        $metadataError = $_
        Write-Warning "Could not write collection metadata: $($_.Exception.Message)"
    }
    if ($null -ne $result -and $result.TeardownConfirmed -and $null -ne $packageCacheRoot -and (Test-Path -LiteralPath $packageCacheRoot)) {
        try {
            Remove-Item -LiteralPath $packageCacheRoot -Recurse -Force
        } catch {
            if ($result.ExitCode -eq 0) {
                throw
            }
            Write-Warning "Collection failed with exit $($result.ExitCode); owned cache cleanup also failed: $($_.Exception.Message)"
        }
    } elseif ($null -ne $packageCacheRoot -and (Test-Path -LiteralPath $packageCacheRoot)) {
        Write-Warning "Collection failed before confirming child teardown. Preserving the owned cache '$packageCacheRoot' for diagnosis."
    }
    if ($null -ne $metadataError -and $null -ne $result -and $result.ExitCode -eq 0) {
        throw $metadataError
    }
}
$verdict = if ($result.ExitCode -eq 0) { "Completed!" } else { "Failed! (exit code $($result.ExitCode))" }
Write-Host ""
Write-Host "Test map collection summary: $verdict"
Write-Host "  modules: $($result.Completed)/$($result.Planned)"
$canceledCount = @($result.CanceledModules | Where-Object { $_.Canceled }).Count
if ($canceledCount -gt 0) {
    Write-Host "  canceled: $canceledCount"
}
Write-Host "  tests mapped: $($result.TestsMapped)"
Write-Host "  duration: $(Format-CollectionDuration -Duration $timer.Elapsed)"
Write-Host ""
Write-Host "  Module logs: $resultsDirectory"
exit $result.ExitCode
