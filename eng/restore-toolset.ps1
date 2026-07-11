# Installs .NET SDK workloads needed by projects in this repository.
#
# This script is dot-sourced by eng/common/build.ps1's InitializeCustomToolset
# AFTER InitializeDotNetCli has bootstrapped the repo-local .dotnet/ SDK, so
# $RepoRoot and the helpers from eng/common/tools.ps1 are in scope.
#
# Required by:
# - samples/WasiPlayground/WasiPlayground.csproj (net10.0 with
#   <UsingWasiRuntimeWorkload>true</UsingWasiRuntimeWorkload>) -> wasi-experimental-net10.
# - the browser-wasm build/publish path exercised by
#   test/IntegrationTests/.../BrowserWasmExecutionTests.cs and samples/BrowserPlayground
#   -> wasm-tools-net10. Installing it here means CI actually exercises the browser-wasm
#   build (and, where 'node' is present, the end-to-end run) instead of the acceptance
#   test silently going Inconclusive on every leg.
# Both would otherwise fail with NETSDK1147.

$RequiredWorkloads = @('wasi-experimental-net10', 'wasm-tools-net10')

$dotnetRoot = if (-not [string]::IsNullOrEmpty($env:DOTNET_INSTALL_DIR)) {
  $env:DOTNET_INSTALL_DIR
} else {
  Join-Path $RepoRoot '.dotnet'
}

$dotnet = Join-Path $dotnetRoot (GetExecutableFileName 'dotnet')

if (-not (Test-Path $dotnet)) {
  Write-PipelineTelemetryError -Category 'InitializeToolset' -Message "restore-toolset.ps1: dotnet executable not found at '$dotnet'."
  ExitWithExitCode 1
}

# Cheap, network-free probe: parse `dotnet workload list` text to skip already-installed workloads.
$listOutput = & $dotnet workload list 2>&1 | Out-String
$missing = @($RequiredWorkloads | Where-Object {
  -not ($listOutput -match "(?m)^\s*$([regex]::Escape($_))\s")
})

if ($missing.Count -eq 0) {
  Write-Host "All required workloads already installed: $($RequiredWorkloads -join ', ')"
  return
}

Write-Host "Installing .NET SDK workloads: $($missing -join ', ')"
& $dotnet workload install @missing
if ($LASTEXITCODE -ne 0) {
  Write-PipelineTelemetryError -Category 'InitializeToolset' -Message "Failed to install workloads '$($missing -join ', ')' (dotnet workload install exit code $LASTEXITCODE)."
  ExitWithExitCode $LASTEXITCODE
}
