#!/usr/bin/env bash

# Installs .NET SDK workloads needed by projects in this repository.
#
# This script is dot-sourced by eng/common/build.sh's InitializeCustomToolset
# AFTER InitializeDotNetCli has bootstrapped the repo-local .dotnet/ SDK, so
# $repo_root and the helpers from eng/common/tools.sh are in scope.
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

required_workloads=('wasi-experimental-net10' 'wasm-tools-net10')

if [[ -n "${DOTNET_INSTALL_DIR:-}" ]]; then
  dotnet_root="$DOTNET_INSTALL_DIR"
else
  dotnet_root="${repo_root}.dotnet"
fi

dotnet_exe="$dotnet_root/dotnet"

if [[ ! -x "$dotnet_exe" ]]; then
  Write-PipelineTelemetryError -category 'InitializeToolset' "restore-toolset.sh: dotnet executable not found at '$dotnet_exe'."
  ExitWithExitCode 1
fi

# Cheap, network-free probe: parse `dotnet workload list` text to skip already-installed workloads.
list_output=$("$dotnet_exe" workload list 2>&1)
missing=()
for workload in "${required_workloads[@]}"; do
  if ! grep -qE "^[[:space:]]*${workload}[[:space:]]" <<<"$list_output"; then
    missing+=("$workload")
  fi
done

if [[ ${#missing[@]} -eq 0 ]]; then
  echo "All required workloads already installed: ${required_workloads[*]}"
  return 0
fi

echo "Installing .NET SDK workloads: ${missing[*]}"
"$dotnet_exe" workload install "${missing[@]}"
workload_install_exit_code=$?
if [[ $workload_install_exit_code -ne 0 ]]; then
  Write-PipelineTelemetryError -category 'InitializeToolset' "Failed to install workloads '${missing[*]}' (dotnet workload install exit code $workload_install_exit_code)."
  ExitWithExitCode $workload_install_exit_code
fi
