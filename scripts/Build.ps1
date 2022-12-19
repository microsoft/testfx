# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT license. See LICENSE file in the project root for full license information.

# Build script for MSTest Test Framework.

[CmdletBinding(PositionalBinding = $false, DefaultParameterSetName = "OneVersion")]
Param(
    [ValidateSet("Debug", "Release")]
    [Alias("c")]
    [string] $Configuration = "Debug",

    [Alias("fv")]
    [Parameter(ParameterSetName = 'MultipleVersions')]
    [string] $FrameworkVersion = "99.99.99",

    [Alias("av")]
    [Parameter(ParameterSetName = 'MultipleVersions')]
    [string] $AdapterVersion = "99.99.99",

    [Alias("v")]
    [Parameter(ParameterSetName = 'OneVersion')]
    [string] $Version,

    [Alias("vs")]
    [string] $VersionSuffix = "dev",

    [string] $BuildVersionPrefix = "14.0",

    [string] $BuildVersionSuffix = "9999.99",

    [string] $Target = "Build",

    [Alias("h")]
    [Switch] $Help,

    [Alias("cl")]
    [Switch] $Clean,

    [Alias("cache")]
    [Switch] $ClearPackageCache,

    [Alias("tpv")]
    [string] $TestPlatformVersion = $null,

    [Alias("np")]
    [Switch] $DisallowPrereleaseMSBuild,

    [Alias("f")]
    [Switch] $Force,

    [switch] $CI,

    [Alias("s")]
    [ValidateSet("InstallDotnet", "UpdateTPVersion", "Restore", "Build", "Pack")]
    [String[]] $Steps = @("InstallDotnet", "UpdateTPVersion", "Restore", "Build", "Pack")
)

if ($Version) {
    $FrameworkVersion = $AdapterVersion = $Version
}

. $PSScriptRoot\common.lib.ps1

#
# Build configuration
#
Write-Verbose "Setup build configuration."
$TFB_Configuration = $Configuration
$TFB_FrameworkVersion = $FrameworkVersion
$TFB_AdapterVersion = $AdapterVersion
$TFB_VersionSuffix = $VersionSuffix
$TFB_BuildVersion = $BuildVersionPrefix + "." + $BuildVersionSuffix
$TFB_Clean = $Clean
$TFB_ClearPackageCache = $ClearPackageCache
$TFB_CI = $CI;
$TFB_BRANCH = "LOCALBRANCH"
$TFB_COMMIT = "LOCALBUILD"
try {
    $TFB_BRANCH = $env:BUILD_SOURCEBRANCH -replace "^refs/heads/"
    if ([string]::IsNullOrWhiteSpace($TFB_BRANCH)) {
        $TFB_BRANCH = git -C "." rev-parse --abbrev-ref HEAD
    }
}
catch { }

try {
    $TFB_COMMIT = $env:BUILD_SOURCEVERSION
    if ([string]::IsNullOrWhiteSpace($TFB_COMMIT)) {
        $TFB_COMMIT = git -C "." rev-parse HEAD
    }
}
catch { }

$TFB_Solutions = @(
    "TestFx.sln"
)

#
# Script Preferences
#
$ErrorActionPreference = "Stop"

#
# Prints help text for the switches this script supports.
#
function Write-Help {
    if (-not $Help) {
        return
    }

    Write-Host -object ""
    Write-Host -object "********* MSTest Build Script *********"
    Write-Host -object ""
    Write-Host -object "  Help (-h)                        - [switch]   - Prints this help message."
    Write-Host -object "  Clean (-cl)                      - [switch]   - Indicates that this should be a clean build."
    Write-Host -object "  SkipRestore (-sr)                - [switch]   - Indicates nuget package restoration should be skipped."
    Write-Host -object "  ClearPackageCache (-cache)       - [switch]   - Indicates local package cache should be cleared before restore."
    Write-Host -object "  DisallowPrereleaseMSBuild (-np)  - [switch]   - Uses an RTM version of MSBuild to build the projects"
    Write-Host -object ""
    Write-Host -object "  Configuration (-c)               - [string]   - Specifies the build configuration. Defaults to 'Debug'."
    Write-Host -object "  FrameworkVersion (-fv)           - [string]   - Specifies the version of the Test Framework nuget package."
    Write-Host -object "  AdapterVersion (-av)             - [string]   - Specifies the version of the Test Adapter nuget package."
    Write-Host -object "  VersionSuffix (-vs)              - [string]   - Specifies the version suffix for the nuget packages."
    Write-Host -object "  Target                           - [string]   - Specifies the build target. Defaults to 'Build'."
    Write-Host -object ""
    Write-Host -object "  Steps (-s)                       - [string[]] - List of build steps to run, valid steps: `"InstallDotnet`", `"UpdateTPVersion`", `"Restore`", `"Build`", `"Pack`""

    Write-Host -object ""
    Exit 0
}

#
# Restores packages for the solutions.
#
function Restore-Package {
    $timer = Start-Timer
    Write-Log "Restore-Package: Started."

    $msbuild = Find-MSBuildPath
    $nuget = Find-NuGet
    $nugetConfig = Find-NuGetConfig
    $toolset = ".\scripts\Toolset\tools.proj"
    if ($TFB_ClearPackageCache) {
        Write-Log "Clearing local package cache..."
        & $nuget locals all -clear
    }

    Write-Log "Starting toolset restore..."
    Write-Verbose "$nuget restore -verbosity normal -nonInteractive -configFile $nugetConfig -msbuildpath $msbuild $toolset"
    & $nuget restore -verbosity normal -nonInteractive -configFile $nugetConfig -msbuildpath $msbuild $toolset

    if ($lastExitCode -ne 0) {
        throw "The restore failed with an exit code of '$lastExitCode'."
    }

    Write-Log "Restore-Package: Completed. {$(Get-ElapsedTime($timer))}"
}

#
# Builds the solutions specified.
#
function Invoke-Build {
    $timer = Start-Timer

    Write-Log "Invoke-Build: Started."

    if ($TFB_Clean) {
        $foldersToDel = @( $TFB_Configuration, "TestAssets" )
        Write-Log "    Clean build requested."
        foreach ($folder in $foldersToDel) {
            $outDir = Join-Path $env:TF_OUT_DIR -ChildPath $folder

            if (Test-Path $outDir) {
                Write-Output "    Deleting $outDir"
                Remove-Item -Recurse -Force $outDir
            }
        }
    }

    Invoke-MSBuild -solution "TestFx.sln"

    Write-Log "Invoke-Build: Completed. {$(Get-ElapsedTime($timer))}"
}


function Get-LogPath {
    $artifacts = Join-Path -Path $TF_OUT_DIR -ChildPath "log" | Join-Path -ChildPath $TFB_Configuration

    if (-not (Test-Path $artifacts)) {
        New-Item -Type Directory -Path $artifacts | Out-Null
    }

    return $artifacts
}

function Invoke-MSBuild([string]$solution, $buildTarget = $Target, $hasVsixExtension = "false", [switch]$NoRestore) {
    $msbuild = Find-MSBuild -hasVsixExtension $hasVsixExtension
    $solutionPath = Find-Item -relativePath $solution
    $logsDir = Get-LogPath

    $fileName = [System.IO.Path]::GetFileNameWithoutExtension($solution)
    $binLog = Join-Path -path $logsDir -childPath "$fileName.$buildTarget.binlog"

    $restore = "True"
    if ($NoRestore) {
        $restore = "False"
    }

    $argument = @("-t:$buildTarget",
        "-p:Configuration=$TFB_Configuration",
        "-v:m",
        "-p:BuildVersion=$TFB_BuildVersion",
        "-p:BranchName=`"$TFB_BRANCH`"",
        "-p:CommitHash=$TFB_COMMIT",
        "-p:MajorMinorPatch=$TFB_FrameworkVersion",
        "-p:VersionSuffix=$TFB_VersionSuffix",
        "-restore:$restore",
        "`"$solutionPath`"",
        "-bl:`"$binLog`"",
        "-m")

    if (-not $TFB_CI) {
        $argument += "-p:UpdateXlfOnBuild=true"
    }

    Write-Log "    $buildTarget`: $solution..."
    & {
        & "$msbuild" $argument;
    }

    if ($lastExitCode -ne 0) {
        throw "Build failed with an exit code of '$lastExitCode'."
    }
}

#
# Creates Fx & Adapter nuget packages
#
function New-NugetPackages {
    $timer = Start-Timer

    Write-Log "New-NugetPackages: Started."

    $stagingDir = Join-Path $env:TF_OUT_DIR $TFB_Configuration
    $packageOutDir = Join-Path $stagingDir "MSTestPackages"
    $tfSrcPackageDir = Join-Path $env:TF_SRC_DIR "Package"

    "" > "$stagingDir\_._"

    # Copy over the nuspecs to the staging directory
    $nuspecFiles = @(
        "MSTest.nuspec",
        "MSTest.TestAdapter.nuspec",
        "MSTest.TestAdapter.symbols.nuspec",
        "MSTest.TestFramework.nuspec",
        "MSTest.TestFramework.symbols.nuspec",
        "MSTest.Internal.TestFx.Documentation.nuspec"
    )

    foreach ($file in $nuspecFiles) {
        Copy-Item $tfSrcPackageDir\$file $stagingDir -Force
    }

    Copy-Item (Join-Path $tfSrcPackageDir "Icon.png") $stagingDir -Force

    Copy-Item -Path "$($env:TF_PACKAGES_DIR)\microsoft.testplatform.adapterutilities\$TestPlatformVersion\lib" -Destination "$($stagingDir)\Microsoft.TestPlatform.AdapterUtilities" -Recurse -Force

    # Call nuget pack on these components.
    $nugetExe = Find-Nuget

    foreach ($file in $nuspecFiles) {
        $version = $TFB_FrameworkVersion

        if ($file.Contains("TestAdapter")) {
            $version = $TFB_AdapterVersion
        }

        if (![string]::IsNullOrEmpty($TFB_VersionSuffix)) {
            $versionSuffix = $TFB_VersionSuffix -replace "\.", "-"
            $version = $version + "-" + $versionSuffix
        }

        $MicrosoftNETCoreUniversalWindowsPlatformVersion = Get-PackageVersion -PackageName "MicrosoftNETCoreUniversalWindowsPlatformVersion"
        $SystemNetWebSocketsClientVersion = Get-PackageVersion -PackageName "SystemNetWebSocketsClientVersion"
        $SystemNetNameResolutionVersion = Get-PackageVersion -PackageName "SystemNetNameResolutionVersion"
        $SystemTextRegularExpressionsVersion = Get-PackageVersion -PackageName "SystemTextRegularExpressionsVersion"
        $SystemPrivateUriVersion = Get-PackageVersion -PackageName "SystemPrivateUriVersion"
        $SystemXmlReaderWriterVersion = Get-PackageVersion -PackageName "SystemXmlReaderWriterVersion"

        Write-Verbose "$nugetExe pack $stagingDir\$file -OutputDirectory $packageOutDir -Version $version -Properties Version=$version``;Srcroot=$env:TF_SRC_DIR``;Packagesroot=$env:TF_PACKAGES_DIR``;TestPlatformVersion=$TestPlatformVersion``;MicrosoftNETCoreUniversalWindowsPlatformVersion=$MicrosoftNETCoreUniversalWindowsPlatformVersion``;SystemNetWebSocketsClientVersion=$SystemNetWebSocketsClientVersion``;SystemTextRegularExpressionsVersion=$SystemTextRegularExpressionsVersion``;SystemPrivateUriVersion=$SystemPrivateUriVersion``;SystemXmlReaderWriterVersion=$SystemXmlReaderWriterVersion``;SystemNetNameResolutionVersion=$SystemNetNameResolutionVersion``;NOWARN=`"NU5127,NU5128,NU5129`"``;BranchName=$TFB_BRANCH``;CommitHash=$TFB_COMMIT"
        & $nugetExe pack $stagingDir\$file -OutputDirectory $packageOutDir -Version $version -Properties Version=$version`;Srcroot=$env:TF_SRC_DIR`;Packagesroot=$env:TF_PACKAGES_DIR`;TestPlatformVersion=$TestPlatformVersion`;MicrosoftNETCoreUniversalWindowsPlatformVersion=$MicrosoftNETCoreUniversalWindowsPlatformVersion`;SystemNetWebSocketsClientVersion=$SystemNetWebSocketsClientVersion`;SystemTextRegularExpressionsVersion=$SystemTextRegularExpressionsVersion`;SystemPrivateUriVersion=$SystemPrivateUriVersion`;SystemXmlReaderWriterVersion=$SystemXmlReaderWriterVersion`;SystemNetNameResolutionVersion=$SystemNetNameResolutionVersion`;NOWARN="NU5127,NU5128,NU5129"`;BranchName=$TFB_BRANCH`;CommitHash=$TFB_COMMIT
        if ($lastExitCode -ne 0) {
            throw "Nuget pack failed with an exit code of '$lastExitCode'."
        }
    }

    Write-Log "New-NugetPackages: Complete. {$(Get-ElapsedTime($timer))}"
}

function ShouldRunStep([string[]]$CurrentSteps) {
    if ($Force) {
        return $true
    }

    foreach ($step in $CurrentSteps) {
        if ($Steps -contains $step) {
            return $true
        }
    }

    return $false
}

Write-Help

Write-Log "Build started: args = '$args'"
Write-Log "MSTest environment variables: "
Get-ChildItem env: | Where-Object -FilterScript { $_.Name.StartsWith("TF_") } | Format-Table
Write-Log "MSTest build variables: "
Get-Variable | Where-Object -FilterScript { $_.Name.StartsWith("TFB_") } | Format-Table

if (ShouldRunStep @("InstallDotnet")) {
    # We want to install required .NET CLI before restoring/building to ensure we always use latest patched version.
    Install-DotNetCli
}

if (ShouldRunStep @("UpdateTPVersion")) {
    Sync-PackageVersions
}

if (ShouldRunStep @("UpdateTPVersion", "Restore")) {
    Restore-Package
}

if (ShouldRunStep @("Build")) {
    Invoke-Build
}

if (ShouldRunStep @("Pack")) {
    New-NugetPackages
}
