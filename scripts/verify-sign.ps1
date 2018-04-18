# Copyright (c) Microsoft. All rights reserved.
# Build script for Test Platform.

[CmdletBinding()]
Param(
    [Parameter(Mandatory=$false)]
    [ValidateSet("Debug", "Release")]
    [Alias("c")]
    [System.String] $Configuration = "Debug"
)

. $PSScriptRoot\common.lib.ps1

#
# Variables
#
$rootDirectory = (Get-Item (Split-Path $MyInvocation.MyCommand.Path)).Parent.FullName

#
# Signing configuration
#
Write-Verbose "Setup build configuration."
$TPB_Configuration = $Configuration

function Verify-NugetPackages
{
    Write-Log "Verify-NugetPackages: Start"

    $nugetInstallPath = Locate-NuGet

	Write-Log "Using nuget.exe installed at $nugetInstallPath"
    
    $artifactsDirectory = Join-Path $rootDirectory "artifacts"
    $artifactsConfigDirectory = Join-Path $artifactsDirectory $TPB_Configuration
    $packagesDirectory = Join-Path $artifactsConfigDirectory "MSTestPackages"
    Get-ChildItem -Filter *.nupkg  $packagesDirectory | % {
    & $nugetInstallPath verify -signature -CertificateFingerprint 3F9001EA83C560D712C24CF213C3D312CB3BFF51EE89435D3430BD06B5D0EECE $_.FullName
    }
    
    Write-Log "Verify-NugetPackages: Complete"
}

function Write-Log ([string] $message)
{
    $currentColor = $Host.UI.RawUI.ForegroundColor
    $Host.UI.RawUI.ForegroundColor = "Green"
    if ($message)
    {
        Write-Output "... $message"
    }
    $Host.UI.RawUI.ForegroundColor = $currentColor
}

Verify-NugetPackages
