#!/usr/bin/env bash

# Installs .NET SDK workloads needed by projects in this repository.
#
# This script is dot-sourced by eng/common/build.sh's InitializeCustomToolset
# AFTER InitializeDotNetCli has bootstrapped the repo-local .dotnet/ SDK, so
# $repo_root and the helpers from eng/common/tools.sh are in scope.
#
# Currently required by samples/WasiPlayground/WasiPlayground.csproj which
# targets net10.0 with <UsingWasiRuntimeWorkload>true</UsingWasiRuntimeWorkload>
# and would otherwise fail with NETSDK1147 in CI.

required_workloads=('wasi-experimental-net10')

# Note: `wasm-tools-net10` is documented in samples/WasiPlayground/README.md and
# samples/BrowserPlayground/README.md as a prerequisite for `dotnet publish`
# (and, for browser-wasm, even for `dotnet build`). It is not needed by the repo's
# default `dotnet build` (which does not build the browser-wasm sample), so we keep
# the CI install minimal and leave `wasm-tools-net10` as a manual/opt-in install.
# The BrowserWasmExecutionTests acceptance test skips itself (Inconclusive) when the
# `wasm-tools` workload or `node` is unavailable.

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
