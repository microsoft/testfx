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
$samplesFolder = Join-Path $repoRoot "samples" "public"

Write-Host "Building samples in: $samplesFolder" -ForegroundColor Cyan
Write-Host "Configuration: $Configuration" -ForegroundColor Cyan
Write-Host ""

$failed = $false
$successCount = 0
$failureCount = 0

# Find all solution files in samples/public
$solutions = Get-ChildItem -Path $samplesFolder -Filter "*.sln" -Recurse

foreach ($solution in $solutions) {
    Write-Host "Building solution: $($solution.FullName)" -ForegroundColor Yellow
    
    $buildArgs = @(
        "build",
        $solution.FullName,
        "--configuration", $Configuration,
        "/p:TreatWarningsAsErrors=$TreatWarningsAsErrors"
    )
    
    & dotnet $buildArgs
    
    if ($LASTEXITCODE -ne 0) {
        Write-Host "ERROR: Failed to build $($solution.Name)" -ForegroundColor Red
        $failed = $true
        $failureCount++
    }
    else {
        Write-Host "SUCCESS: Built $($solution.Name)" -ForegroundColor Green
        $successCount++
    }
    
    Write-Host ""
}

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Build Summary:" -ForegroundColor Cyan
Write-Host "  Total solutions: $($solutions.Count)" -ForegroundColor Cyan
Write-Host "  Succeeded: $successCount" -ForegroundColor Green
Write-Host "  Failed: $failureCount" -ForegroundColor Red
Write-Host "========================================" -ForegroundColor Cyan

if ($failed) {
    Write-Host "One or more samples failed to build" -ForegroundColor Red
    exit 1
}

Write-Host "All samples built successfully!" -ForegroundColor Green
exit 0
