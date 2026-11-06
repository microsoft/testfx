# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT license. See LICENSE file in the project root for full license information.

#requires -Version 7.2

[CmdletBinding()]
param(
    [switch] $Resolve
)

$ErrorActionPreference = 'Stop'

function Update-PreviewIntegrity {
    $feed = 'https://pkgs.dev.azure.com/dnceng/public/_packaging/dotnet-tools/nuget/v3/index.json'
    $packages = @{}
    $source = ''
    foreach ($line in Get-Content -LiteralPath (Join-Path $PSScriptRoot 'paket.lock')) {
        if ($line -match '^  remote: (.+)$') {
            $source = $Matches[1]
        }
        elseif ($source -eq $feed -and $line -match '^    ([^ ]+) \(([^)]+)\)') {
            $id = $Matches[1].ToLowerInvariant()
            $version = $Matches[2].ToLowerInvariant()
            if ($version -notmatch '^\d+\.\d+\.\d+(-[^+]+)?$') {
                throw "Expected a normalized preview package version, found $id $version."
            }
            $packages["$id/$version"] = @{ Id = $id; Version = $version }
        }
    }

    $digests = [ordered]@{}
    if ($packages.Count -gt 0) {
        $index = Invoke-RestMethod -Uri $feed
        $addresses = @($index.resources | Where-Object { $_.'@type' -eq 'PackageBaseAddress/3.0.0' })
        if ($addresses.Count -ne 1) {
            throw 'Expected one preview-feed package base address.'
        }
        $base = $addresses[0].'@id'.TrimEnd('/')
        $downloads = Join-Path $env:NUGET_PACKAGES 'integrity-downloads'
        New-Item -ItemType Directory -Path $downloads -Force | Out-Null
        foreach ($key in @($packages.Keys | Sort-Object)) {
            $package = $packages[$key]
            $download = Join-Path $downloads ([Guid]::NewGuid().ToString('N') + '.nupkg')
            try {
                Invoke-WebRequest -Uri "$base/$key/$($package.Id).$($package.Version).nupkg" -OutFile $download -TimeoutSec 120
                $hash = (Get-FileHash -LiteralPath $download -Algorithm SHA512).Hash
                $digests[$key] = 'sha512-' + [Convert]::ToBase64String([Convert]::FromHexString($hash))
            }
            finally {
                if (Test-Path -LiteralPath $download) {
                    Remove-Item -LiteralPath $download
                }
            }
        }
    }

    $digests | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $PSScriptRoot 'package-integrity.json') -Encoding utf8
    Write-Host "Recorded SHA-512 digests for $($digests.Count) preview packages."
}

$root = Split-Path (Split-Path (Split-Path $PSScriptRoot -Parent) -Parent) -Parent
$bazel = Join-Path $root 'eng\bazel.ps1'
& $bazel build //eng/bazel:paket --jobs=2 --noshow_progress --show_result=0
if ($LASTEXITCODE -ne 0) {
    throw 'Failed to build the pinned Paket resolver.'
}

$executionRoot = & $bazel info execution_root
if ($LASTEXITCODE -ne 0) {
    throw 'Failed to locate the Bazel execution root.'
}

$paths = @(& $bazel cquery //eng/bazel:paket --output=starlark '--starlark:expr=providers(target)["DefaultInfo"].files_to_run.executable.path' --noshow_progress)
if ($LASTEXITCODE -ne 0 -or $paths.Count -ne 1) {
    throw 'Expected exactly one native Paket launcher.'
}

$oldPackages = $env:NUGET_PACKAGES
$env:NUGET_PACKAGES = if ($env:TESTFX_BAZEL_OUTPUT_ROOT) {
    Join-Path $env:TESTFX_BAZEL_OUTPUT_ROOT 'nuget-tests-support'
}
else {
    Join-Path $root 'artifacts\bazel-tests-support\nuget'
}
Push-Location $PSScriptRoot
try {
    if ($Resolve) {
        & (Join-Path $executionRoot $paths[0]) update --no-install
        if ($LASTEXITCODE -ne 0) {
            throw 'Test-support dependency resolution failed.'
        }
        Update-PreviewIntegrity
    }

    & $bazel mod deps --lockfile_mode=update
    if ($LASTEXITCODE -ne 0) {
        throw 'Bazel test-support dependency metadata resolution failed.'
    }
}
finally {
    Pop-Location
    $env:NUGET_PACKAGES = $oldPackages
}
