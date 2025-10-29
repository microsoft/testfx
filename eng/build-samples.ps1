#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Builds all sample projects in the samples/public folder.

.DESCRIPTION
    This script iterates through all solution files in samples/public and builds them.
    It can be used both locally by developers and in CI pipelines.

.PARAMETER Configuration
    The build configuration to use (default: Release).

.PARAMETER TreatWarningsAsErrors
    Whether to treat warnings as errors (default: false).

.EXAMPLE
    .\eng\build-samples.ps1
    Builds all samples in Release configuration.

.EXAMPLE
    .\eng\build-samples.ps1 -Configuration Debug
    Builds all samples in Debug configuration.
#>

[CmdletBinding()]
param(
    [string]$Configuration = "Release",
    [bool]$TreatWarningsAsErrors = $false
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$samplesFolder = Join-Path (Join-Path $repoRoot "samples") "public"

# Source the arcade tools to get access to InitializeDotNetCli
. (Join-Path $PSScriptRoot "common\tools.ps1")

# Initialize .NET CLI to ensure correct SDK version is available
$dotnetPath = InitializeDotNetCli -install:$true

# Detect if running in CI to disable colors
$isCI = $env:TF_BUILD -eq 'true' -or $env:CI -eq 'true'

Write-Host "Building samples in: $samplesFolder"
Write-Host "Configuration: $Configuration"
Write-Host ""

$failed = $false
$successCount = 0
$failureCount = 0

# Find all solution files in samples/public
$solutions = Get-ChildItem -Path $samplesFolder -Filter "*.sln" -Recurse

foreach ($solution in $solutions) {
    Write-Host "Building solution: $($solution.FullName)"

    $buildArgs = @(
        "build",
        $solution.FullName,
        "--configuration", $Configuration,
        "/p:TreatWarningsAsErrors=$TreatWarningsAsErrors"
    )

    & $dotnetPath $buildArgs

    if ($LASTEXITCODE -ne 0) {
        Write-Host "ERROR: Failed to build $($solution.Name)"
        $failed = $true
        $failureCount++
    }
    else {
        Write-Host "SUCCESS: Built $($solution.Name)"
        $successCount++
    }

    Write-Host ""
}

Write-Host "========================================"
Write-Host "Build Summary:"
Write-Host "  Total solutions: $($solutions.Count)"
Write-Host "  Succeeded: $successCount"
Write-Host "  Failed: $failureCount"
Write-Host "========================================"

if ($failed) {
    Write-Host "One or more samples failed to build"
    exit 1
}

Write-Host "All samples built successfully!"
exit 0
