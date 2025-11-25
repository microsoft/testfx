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
    [switch]$TreatWarningsAsErrors
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$samplesFolder = "$repoRoot/samples/public"

# Source the arcade tools to get access to InitializeDotNetCli
. "$PSScriptRoot/common/tools.ps1"

# Initialize .NET CLI to ensure correct SDK version is available
$dotnetRoot = InitializeDotNetCli -install:$true
$dotnetPath = "$dotnetRoot/dotnet.exe"

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

    # UWP projects require MSBuild instead of dotnet build
    $isUwpSolution = $solution.Name -eq "BlankUwpNet9App.sln"

    if ($isUwpSolution) {
        # Restore NuGet packages first for UWP projects
        $restoreArgs = @(
            "restore",
            $solution.FullName,
            "/p:Configuration=$Configuration",
            "/p:Platform=x64"
        )

        & $dotnetPath $restoreArgs

        if ($LASTEXITCODE -ne 0) {
            Write-Host "ERROR: Failed to restore packages for $($solution.Name)"
            $failed = $true
            $failureCount++
            continue
        }

        $msbuildPath = InitializeVisualStudioMSBuild -install:$true

        $buildArgs = @(
            $solution.FullName,
            "/p:Configuration=$Configuration",
            "/p:TreatWarningsAsErrors=$TreatWarningsAsErrors",
            "/p:Platform=x64",
            "/v:minimal"
        )

        & $msbuildPath $buildArgs
    }
    else {
        $buildArgs = @(
            "build",
            $solution.FullName,
            "--configuration", $Configuration,
            "/p:TreatWarningsAsErrors=$TreatWarningsAsErrors"
        )

        & $dotnetPath $buildArgs
    }

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
