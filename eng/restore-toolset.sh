#!/usr/bin/env bash

# Installs .NET SDK workloads needed by projects in this repository.
#
# This script is dot-sourced by eng/common/build.sh's InitializeCustomToolset
# AFTER InitializeDotNetCli has bootstrapped the repo-local .dotnet/ SDK, so
# $repo_root and the helpers from eng/common/tools.sh are in scope.
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
