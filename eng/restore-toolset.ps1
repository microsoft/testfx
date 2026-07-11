# Installs .NET SDK workloads needed by projects in this repository.
#
# This script is dot-sourced by eng/common/build.ps1's InitializeCustomToolset
# AFTER InitializeDotNetCli has bootstrapped the repo-local .dotnet/ SDK, so
# $RepoRoot and the helpers from eng/common/tools.ps1 are in scope.
#
# Required by the wasm acceptance tests and samples:
#   - test/IntegrationTests/Microsoft.Testing.Platform.Acceptance.IntegrationTests/WasmExecutionTests.cs
#   - test/IntegrationTests/MSTest.Acceptance.IntegrationTests/WasmExecutionTests.cs
#   - test/IntegrationTests/Microsoft.Testing.Platform.Acceptance.IntegrationTests/BrowserWasmExecutionTests.cs
#   - samples/WasiPlayground, samples/BrowserPlayground
# `wasi-experimental-net10` is needed by the always-on `dotnet build -r wasi-wasm` assertions and the
# WasiPlayground sample (<UsingWasiRuntimeWorkload>true</UsingWasiRuntimeWorkload>); otherwise NETSDK1147
# in CI. `wasm-tools-net10` is needed by the gated `dotnet publish -r wasi-wasm` step the wasi execution
# tests perform AND by the browser-wasm build/publish path (BrowserWasmExecutionTests / BrowserPlayground) —
# for browser-wasm it is required even by `dotnet build`. Installing it here means CI actually exercises the
# browser-wasm build (and, where 'node' is present, the end-to-end run) instead of the acceptance test
# silently going Inconclusive on every leg. Running the wasi execution tests to completion additionally needs
# `wasmtime` on PATH; when it (or `node`, for browser-wasm) is absent the tests mark themselves inconclusive.

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
