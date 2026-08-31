#!/usr/bin/env bash

# Installs .NET SDK workloads needed by projects in this repository.
#
# This script is dot-sourced by eng/common/build.sh's InitializeCustomToolset
# AFTER InitializeDotNetCli has bootstrapped the repo-local .dotnet/ SDK, so
# $repo_root and the helpers from eng/common/tools.sh are in scope.
#
# Required by the wasm acceptance tests (and the browser-wasm sample):
#   - test/IntegrationTests/Microsoft.Testing.Platform.Acceptance.IntegrationTests/WasmExecutionTests.cs
#   - test/IntegrationTests/MSTest.Acceptance.IntegrationTests/WasmExecutionTests.cs
#   - test/IntegrationTests/Microsoft.Testing.Platform.Acceptance.IntegrationTests/BrowserWasmExecutionTests.cs
#   - samples/BrowserPlayground
# `wasi-experimental-net10` is needed by the always-on `dotnet build -r wasi-wasm` assertions, whose
# generated asset sets <UsingWasiRuntimeWorkload>true</UsingWasiRuntimeWorkload> (see WasmExecutionTests.cs);
# otherwise NETSDK1147 in CI. `wasm-tools-net10` is needed by the gated `dotnet publish -r wasi-wasm` step the
# wasi execution tests perform AND by the browser-wasm build/publish path (BrowserWasmExecutionTests /
# BrowserPlayground) — for browser-wasm it is required even by `dotnet build`. Installing it here means CI
# actually exercises the browser-wasm build (and, where 'node' is present, the end-to-end run) instead of the
# acceptance test silently going Inconclusive on every leg. Running the wasi execution tests to completion
# additionally needs `wasmtime` on PATH; when it (or `node`, for browser-wasm) is absent the tests mark
# themselves inconclusive.

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
