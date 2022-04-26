# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT license. See LICENSE file in the project root for full license information.

[CmdletBinding()]
Param(
    [Parameter(Mandatory=$false)]
    [ValidateSet("Debug", "Release")]
    [Alias("c")]
    [string] $Configuration = "Debug",
    [string] $ArtifactsDirectory = "",
    [switch] $Force
)

. $PSScriptRoot\common.lib.ps1

#
# Variables
#
if(-not [string]::IsNullOrWhiteSpace($ArtifactsDirectory)) {
    $TF_OUT_DIR = $ArtifactsDirectory
}

#
# Signing configuration
#
Write-Verbose "Setup build configuration."

$TF_Configuration = $Configuration
$TF_AssembliesPattern = @("Microsoft.VisualStudio.TestPlatform.*.dll", "Microsoft.TestPlatform.*.dll")
$script:ErrorCount = 0

function Test-Assembly ([string] $Path)
{
    $signature = Get-AuthenticodeSignature -FilePath $Path

    if ($signature.Status -eq "Valid") {
        if ($signature.SignerCertificate.Subject -eq "CN=Microsoft Corporation, O=Microsoft Corporation, L=Redmond, S=Washington, C=US") {
            Write-Debug "Valid ($($signature.SignerCertificate.Thumbprint)): $Path"
        }
        elseif ($signature.SignerCertificate.Subject -eq "CN=Microsoft 3rd Party Application Component, O=Microsoft Corporation, L=Redmond, S=Washington, C=US") {
            Write-Debug "Valid ($($signature.SignerCertificate.Thumbprint)): $Path [3rd Party]"
        }
        else {
            # For legacy components
            # CN=Microsoft Corporation, OU=AOC, O=Microsoft Corporation, L=Redmond, S=Washington, C=US
            if ($signature.SignerCertificate.Thumbprint -eq "49D59D86505D82942A076388693F4FB7B21254EE") {
                Write-Debug "Valid ($($signature.SignerCertificate.Thumbprint)): $Path [Legacy Prod Signed]"
            }
            else {
                Write-FailLog "Invalid ($($signature.SignerCertificate.Thumbprint)). File: $Path. [$($signature.SignerCertificate.Subject)]"
            }
        }
    }
    else {
        Write-FailLog "Not signed. File: $Path."
    }
}

function Test-Assemblies ([string] $Path)
{
    foreach ($pattern in $TF_AssembliesPattern) {
        Get-ChildItem -Recurse -Include $pattern $Path | Where-Object { (!$_.PSIsContainer) } | ForEach-Object {
            Test-Assembly $_.FullName
        }
    }
}

function Test-NugetPackage ([string] $Path) {
    $packageFolder = [System.IO.Path]::GetDirectoryName($Path)
    $fileName = [System.IO.Path]::GetFileNameWithoutExtension($Path)
    $out = Join-Path $packageFolder $fileName

    try {
        Write-ToCI "Verifing assemblies in $Path" -type "group"
        Write-Debug "Extracting..."
        if (Test-Path $out) {
            if (-not $Force) {
                Write-FailLog "Folder already exists: $out"
                return
            }

            Remove-Item $out -Recurse -Force
        }

        Unzip $Path $out

        Test-Assemblies $out
    } finally {
        if (Test-Path $out) {
            Remove-Item $out -Recurse -Force
        }
        Write-ToCI -type "endgroup"
    }
}

function Test-NugetPackages
{
    Write-Debug  "Test-NugetPackages"

    $nugetInstallPath = Locate-NuGet
    Write-Debug  "Using nuget.exe installed at $nugetInstallPath"

    $artifactsConfigDirectory = Join-Path $TF_OUT_DIR $TF_Configuration
    $packagesDirectory = Join-Path $artifactsConfigDirectory "MSTestPackages"

    Get-ChildItem -Filter *.nupkg  $packagesDirectory | ForEach-Object {
        try {
            Write-ToCI "Verifing $($_.FullName)" -type "group"
            & $nugetInstallPath verify -signature -CertificateFingerprint "3F9001EA83C560D712C24CF213C3D312CB3BFF51EE89435D3430BD06B5D0EECE;AA12DA22A49BCE7D5C1AE64CC1F3D892F150DA76140F210ABD2CBFFCA2C18A27;" $_.FullName
            Test-NugetPackage -path $_.FullName
        } finally {
            Write-ToCI -type "endgroup"
        }
    }

    Write-Debug  "Test-NugetPackages: Complete"
}

function Write-FailLog ([string] $message)
{
    $script:ErrorCount = $script:ErrorCount + 1
    Write-ToCI -message $message -type "error"
}

function Write-Debug ([string] $message)
{
    Write-ToCI -message $message -type "debug"
}

function Write-ToCI ([string] $message, [string]$type, [switch]$vso)
{
    $currentColor = $Host.UI.RawUI.ForegroundColor

    if($type -eq "error") {
        $Host.UI.RawUI.ForegroundColor = "Red"
    }

    if ($message -or $vso -or $type)
    {
        $prefix = ""
        if ($vso) {
            $prefix = "vso"
        }

        Write-Output "##$prefix[$type]$message"
    }
    $Host.UI.RawUI.ForegroundColor = $currentColor
}

try {
    Write-ToCI "Variables used: " -type "group"
    Get-ChildItem variable:TF_*
    Write-Output ""
    Write-Output ""
} finally {
    Write-ToCI -type "endgroup"
}

Test-NugetPackages

if ($script:ErrorCount -gt 0) {
    Write-ToCI -message "Verification failed, $($script:ErrorCount) errors found!" -type "task.logissue" -vso
}
