# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT license. See LICENSE file in the project root for full license information.

#requires -Version 7.2

[CmdletBinding()]
param(
    [switch] $Resolve
)

$ErrorActionPreference = 'Stop'
$root = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$bazel = Join-Path $root 'eng\bazel.ps1'
$targets = @('//eng/bazel:paket', '@rules_dotnet//tools/paket2bazel')

& $bazel build @targets --noshow_progress --show_result=0
if ($LASTEXITCODE -ne 0) {
    throw 'Failed to build the pinned dependency tools.'
}

$executionRoot = & $bazel info execution_root
if ($LASTEXITCODE -ne 0) {
    throw 'Failed to locate the Bazel execution root.'
}

$paths = @(& $bazel cquery 'set(//eng/bazel:paket @rules_dotnet//tools/paket2bazel)' --output=starlark '--starlark:expr=providers(target)["DefaultInfo"].files_to_run.executable.path' --noshow_progress)
if ($LASTEXITCODE -ne 0 -or $paths.Count -ne 2) {
    throw 'Expected exactly two executable dependency tools.'
}

$paket = @($paths | Where-Object { $_ -match '/eng/bazel/paket\.(bat|sh)$' })
$generator = @($paths | Where-Object { $_ -match '/publish/[^/]+/paket2bazel_bin(\.exe)?$' })
if ($paket.Count -ne 1 -or $generator.Count -ne 1) {
    throw 'The resolved dependency tools do not match the expected executables.'
}

$oldPackages = $env:NUGET_PACKAGES
$env:NUGET_PACKAGES = Join-Path $root 'artifacts\bazel-nuget'
Push-Location $PSScriptRoot
try {
    if ($Resolve) {
        & (Join-Path $executionRoot $paket[0]) update --no-install
        if ($LASTEXITCODE -ne 0) {
            throw 'Paket dependency resolution failed.'
        }
    }

    & (Join-Path $executionRoot $generator[0]) --dependencies-file (Join-Path $PSScriptRoot 'paket.dependencies') --output-folder (Join-Path $PSScriptRoot 'nuget')
    if ($LASTEXITCODE -ne 0) {
        throw 'Bazel dependency generation failed.'
    }
}
finally {
    Pop-Location
    $env:NUGET_PACKAGES = $oldPackages
}
