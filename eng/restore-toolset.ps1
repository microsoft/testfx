# Installs .NET SDK workloads needed by projects in this repository.
#
# This script is dot-sourced by eng/common/build.ps1's InitializeCustomToolset
# AFTER InitializeDotNetCli has bootstrapped the repo-local .dotnet/ SDK, so
# $RepoRoot and the helpers from eng/common/tools.ps1 are in scope.
#
# Required by the wasi-wasm acceptance tests:
#   - test/IntegrationTests/Microsoft.Testing.Platform.Acceptance.IntegrationTests/WasmExecutionTests.cs
#   - test/IntegrationTests/MSTest.Acceptance.IntegrationTests/WasmExecutionTests.cs
# Their generated assets set <UsingWasiRuntimeWorkload>true</UsingWasiRuntimeWorkload>; the
# `wasi-experimental-net10` workload is needed by the always-on `dotnet build -r wasi-wasm` assertions
# (otherwise NETSDK1147 in CI), and `wasm-tools-net10` is needed by the gated `dotnet publish -r
# wasi-wasm` step the execution tests perform (otherwise the publish fails and they self-skip).
# Running the execution tests to completion additionally needs `wasmtime` on PATH; when it is absent
# the tests mark themselves inconclusive.

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
