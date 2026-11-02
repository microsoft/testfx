# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT license. See LICENSE file in the project root for full license information.

#requires -Version 7.2

# Quote '--' when calling this PowerShell script so PowerShell forwards the separator.
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$version = (Get-Content -LiteralPath (Join-Path $root '.bazelversion') -Raw).Trim()
if ($version -ne '9.1.1') {
    throw 'Update the verified Bazel bootstrap hashes when changing .bazelversion.'
}

if (-not ($IsWindows -or $IsMacOS -or $IsLinux)) {
    throw 'The Bazel bootstrap supports Windows, Linux, and macOS.'
}

$platform = if ($IsWindows) { 'windows-x86_64.exe' } elseif ($IsMacOS) { 'darwin-arm64' } else { 'linux-x86_64' }
$architecture = [System.Runtime.InteropServices.RuntimeInformation]::OSArchitecture.ToString()
if (($IsMacOS -and $architecture -ne 'Arm64') -or (-not $IsMacOS -and $architecture -ne 'X64')) {
    throw "The Bazel bootstrap does not support this OS/architecture ($architecture). Install Bazelisk and use .bazelversion."
}

$hashes = @{
    'windows-x86_64.exe' = '65f3484025c95a9f93647fd6edb84b19c15c67d5265d364f17843fd12c5d28fe'
    'linux-x86_64' = '857bed5d2756b4d998d3caebf2d941d13d434c4eda4b1d6d7dda205736c25a93'
    'darwin-arm64' = '2db883718453f0437a7bcb408e889dbf8539cdc4d61c8ebc3807a1a88d02ff08'
}
$tools = Join-Path $root 'artifacts\bazel-tools'
$executable = Join-Path $tools "bazel-$version-$platform"
if (-not (Test-Path -LiteralPath $executable)) {
    New-Item -ItemType Directory -Path $tools -Force | Out-Null
    $download = "$executable.$([Guid]::NewGuid().ToString('N')).download"
    try {
        Invoke-WebRequest "https://github.com/bazelbuild/bazel/releases/download/$version/bazel-$version-$platform" -OutFile $download
        if ((Get-FileHash -LiteralPath $download -Algorithm SHA256).Hash -ne $hashes[$platform]) {
            throw 'Downloaded Bazel failed SHA-256 verification.'
        }

        Move-Item -LiteralPath $download -Destination $executable -Force
    }
    finally {
        if (Test-Path -LiteralPath $download) {
            Remove-Item -LiteralPath $download
        }
    }
}

if ((Get-FileHash -LiteralPath $executable -Algorithm SHA256).Hash -ne $hashes[$platform]) {
    throw "Bazel failed SHA-256 verification: $executable"
}

if (-not $IsWindows) {
    & chmod +x $executable
    if ($LASTEXITCODE -ne 0) {
        throw 'Unable to make the Bazel executable runnable.'
    }
}

Push-Location $root
try {
    # Windows cannot execute long .bat paths; isolate each worktree under a short root.
    $identity = [Convert]::ToHexString([System.Security.Cryptography.SHA256]::HashData([System.Text.Encoding]::UTF8.GetBytes($root))).Substring(0, 12).ToLowerInvariant()
    $outputRoot = if ($env:TESTFX_BAZEL_OUTPUT_ROOT) {
        $env:TESTFX_BAZEL_OUTPUT_ROOT
    }
    elseif ($IsWindows) {
        Join-Path ([System.IO.Path]::GetPathRoot($root)) "tfb\$identity"
    }
    else {
        Join-Path ([System.IO.Path]::GetTempPath()) "tfb-$identity"
    }
    & $executable "--output_user_root=$outputRoot" --max_idle_secs=120 @args
    $exitCode = $LASTEXITCODE
}
finally {
    Pop-Location
}

exit $exitCode
