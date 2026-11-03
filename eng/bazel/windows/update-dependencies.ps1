# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT license. See LICENSE file in the project root for full license information.

#requires -Version 7.2

[CmdletBinding()]
param([switch] $Resolve)

$ErrorActionPreference = 'Stop'
$root = Split-Path (Split-Path (Split-Path $PSScriptRoot -Parent) -Parent) -Parent
$bazel = Join-Path $root 'eng\bazel.ps1'
& $bazel build //eng/bazel:paket --jobs=1 --noshow_progress --show_result=0
if ($LASTEXITCODE -ne 0) { throw 'Failed to build the pinned dependency resolver.' }
$executionRoot = & $bazel info execution_root
if ($LASTEXITCODE -ne 0) { throw 'Could not locate the native execution root.' }
$outputBase = & $bazel info output_base
if ($LASTEXITCODE -ne 0) { throw 'Could not locate the isolated Bazel output root.' }
$paths = @(& $bazel cquery //eng/bazel:paket --output=starlark '--starlark:expr=providers(target)["DefaultInfo"].files_to_run.executable.path' --noshow_progress)
if ($LASTEXITCODE -ne 0 -or $paths.Count -ne 1) { throw 'Expected one pinned Paket launcher.' }
if ($IsWindows) {
    $executionRoot = $executionRoot.Replace('/', '\')
    $outputBase = $outputBase.Replace('/', '\')
}
$resolver = Join-Path $executionRoot $paths[0].Replace('/', [IO.Path]::DirectorySeparatorChar)
$cache = Join-Path (Split-Path $outputBase -Parent) 'windows-dependency-cache'
$previous = @{}
foreach ($name in @('NUGET_PACKAGES', 'NUGET_HTTP_CACHE_PATH', 'PAKET_CACHE_PATH')) {
    $previous[$name] = [Environment]::GetEnvironmentVariable($name)
}
Push-Location $PSScriptRoot
try {
    $env:NUGET_PACKAGES = Join-Path $cache 'packages'
    $env:NUGET_HTTP_CACHE_PATH = Join-Path $cache 'http'
    $env:PAKET_CACHE_PATH = Join-Path $cache 'paket'
    if ($Resolve) {
        & $resolver update --no-install
        if ($LASTEXITCODE -ne 0) { throw 'Windows Paket resolution failed.' }
    }
    & $bazel mod deps --lockfile_mode=update
    if ($LASTEXITCODE -ne 0) { throw 'Windows Bazel dependency metadata resolution failed.' }
}
finally {
    Pop-Location
    foreach ($name in $previous.Keys) {
        [Environment]::SetEnvironmentVariable($name, $previous[$name])
    }
}
