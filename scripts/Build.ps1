# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT license. See LICENSE file in the project root for full license information.

# Build script for MSTest Test Framework.

[CmdletBinding(PositionalBinding = $false)]
Param(
  [ValidateSet("Debug", "Release")]
  [Alias("c")]
  [string] $Configuration = "Debug",

  [Alias("fv")]
  [string] $FrameworkVersion = "99.99.99",

  [Alias("av")]
  [string] $AdapterVersion = "99.99.99",

  [Alias("vs")]
  [string] $VersionSuffix = "dev",

  [string] $BuildVersionPrefix = "14.0",

  [string] $BuildVersionSuffix = "9999.99",

  [string] $Target = "Build",

  [Alias("h")]
  [Switch] $Help,

  [Alias("cl")]
  [Switch] $Clean,

  [Alias("sr")]
  [Switch] $SkipRestore,

  [Alias("cache")]
  [Switch] $ClearPackageCache,

  [Switch] $Official,

  [Switch] $Full,

  [Alias("uxlf")]
  [Switch] $UpdateXlf,

  [Alias("loc")]
  [Switch] $IsLocalizedBuild,

  [Alias("tpv")]
  [string] $TestPlatformVersion = $null,

  [Alias("np")]
  [Switch] $DisallowPrereleaseMSBuild,

  [Alias("f")]
  [Switch] $Force,

  [Alias("s")]
  [String[]] $Steps = @("UpdateTPVersion", "Restore", "Build", "Publish")
)

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
$TFB_SkipRestore = $SkipRestore
$TFB_Clean = $Clean
$TFB_ClearPackageCache = $ClearPackageCache
$TFB_Full = $Full
$TFB_Official = $Official
$TFB_UpdateXlf = $UpdateXlf
$TFB_IsLocalizedBuild = $IsLocalizedBuild -or $TFB_Official
$TPB_BRANCH = "LOCALBRANCH"
$TPB_COMMIT = "LOCALBUILD"
try {
    $TPB_BRANCH = $env:BUILD_SOURCEBRANCH -replace "^refs/heads/"
    if ([string]::IsNullOrWhiteSpace($TPB_BRANCH)) {
        $TPB_BRANCH = git -C "." rev-parse --abbrev-ref HEAD
    }
}
catch { }

try {
    $TPB_COMMIT = $env:BUILD_SOURCEVERSION
    if ([string]::IsNullOrWhiteSpace($TPB_COMMIT)) {
        $TPB_COMMIT = git -C "." rev-parse HEAD
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
function Print-Help {
  if (-not $Help) {
    return
  }

  Write-Host -object ""
  Write-Host -object "********* MSTest Adapter Build Script *********"
  Write-Host -object ""
  Write-Host -object "  Help (-h)                        - [switch]   - Prints this help message."
  Write-Host -object "  Clean (-cl)                      - [switch]   - Indicates that this should be a clean build."
  Write-Host -object "  SkipRestore (-sr)                - [switch]   - Indicates nuget package restoration should be skipped."
  Write-Host -object "  ClearPackageCache (-cache)       - [switch]   - Indicates local package cache should be cleared before restore."
  Write-Host -object "  Updatexlf (-uxlf)                - [switch]   - Indicates that there are resource changes and that these need to be copied to other languages as well."
  Write-Host -object "  IsLocalizedBuild (-loc)          - [switch]   - Indicates that the build needs to generate resource assemblies as well."
  Write-Host -object "  Official                         - [switch]   - Indicates that this is an official build. Only used in CI builds."
  Write-Host -object "  Full                             - [switch]   - Indicates to perform a full build which includes Adapter, Framework"
  Write-Host -object "  DisallowPrereleaseMSBuild (-np)  - [switch]   - Uses an RTM version of MSBuild to build the projects"
  Write-Host -object ""
  Write-Host -object "  Configuration (-c)               - [string]   - Specifies the build configuration. Defaults to 'Debug'."
  Write-Host -object "  FrameworkVersion (-fv)           - [string]   - Specifies the version of the Test Framework nuget package."
  Write-Host -object "  AdapterVersion (-av)             - [string]   - Specifies the version of the Test Adapter nuget package."
  Write-Host -object "  VersionSuffix (-vs)              - [string]   - Specifies the version suffix for the nuget packages."
  Write-Host -object "  Target                           - [string]   - Specifies the build target. Defaults to 'Build'."
  Write-Host -object ""
  Write-Host -object "  Steps (-s)                       - [string[]] - List of build steps to run, valid steps: `"UpdateTPVersion`", `"Restore`", `"Build`", `"Publish`""

  Write-Host -object ""
  Exit 0
}

function Install-WindowsSDK {
  Push-Location
  $temp = [System.IO.Path]::GetTempFileName();
  Remove-Item $temp
  New-Item $temp -Type Directory | Out-Null
  Set-Location $temp

  try {
    Invoke-WebRequest -Method Get -Uri https://go.microsoft.com/fwlink/p/?LinkId=838916 -OutFile sdksetup.exe -UseBasicParsing
    Start-Process -Wait sdksetup.exe -ArgumentList "/q", "/norestart", "/ceip off", "/features OptionId.WindowsSoftwareDevelopmentKit" -PassThru
  }
  finally {
    Pop-Location

    Remove-Item $temp -Force -Recurse | Out-Null
  }

}

#
# Restores packages for the solutions.
#
function Perform-Restore {
  $timer = Start-Timer

  Write-Log "Perform-Restore: Started."

  if ($TFB_SkipRestore) {
    Write-Log "Perform-Restore: Skipped."
    return;
  }

  $msbuild = Locate-MSBuildPath
  $nuget = Locate-NuGet
  $nugetConfig = Locate-NuGetConfig
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

  Write-Log "Perform-Restore: Completed. {$(Get-ElapsedTime($timer))}"
}

#
# Builds the solutions specified.
#
function Perform-Build {
  $timer = Start-Timer

  Write-Log "Perform-Build: Started."

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

  Write-Log "Perform-Build: Completed. {$(Get-ElapsedTime($timer))}"
}

function Invoke-MSBuild([string]$solution, $buildTarget = $Target, $hasVsixExtension = "false", [switch]$NoRestore) {
  $msbuild = Locate-MSBuild -hasVsixExtension $hasVsixExtension
  $solutionPath = Locate-Item -relativePath $solution
  $logsDir = Get-LogsPath

  $fileName = [System.IO.Path]::GetFileNameWithoutExtension($solution)
  $binLog = Join-Path -path $logsDir -childPath "$fileName.$buildTarget.binlog"

  $restore = "True"
  if($NoRestore) {
    $restore = "False"
  }

  $argument = @("-t:$buildTarget",
                "-p:Configuration=$configuration",
                "-v:m",
                "-p:IsLocalizedBuild=$TFB_IsLocalizedBuild",
                "-p:UpdateXlf=$TFB_UpdateXlf",
                "-p:BuildVersion=$TFB_BuildVersion",
                "-restore:$restore",
                "`"$solutionPath`"",
                "-bl:`"$binLog`"",
                "-m")

  Write-Log "    $buildTarget`: $solution..."
  & {
    $PSNativeCommandArgumentPassing = 'Legacy'
    & "$msbuild" $argument;
  }

  if ($lastExitCode -ne 0) {
    throw "Build failed with an exit code of '$lastExitCode'."
  }
}

#
# Creates Fx & Adapter nuget packages
#
function Create-NugetPackages {
  $timer = Start-Timer

  Write-Log "Create-NugetPackages: Started."

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
  $nugetExe = Locate-Nuget

  foreach ($file in $nuspecFiles) {
    $version = $TFB_FrameworkVersion

    if ($file.Contains("TestAdapter")) {
      $version = $TFB_AdapterVersion
    }

    if (![string]::IsNullOrEmpty($TFB_VersionSuffix)) {
      $versionSuffix = $TFB_VersionSuffix -replace "\.", "-"
      $version = $version + "-" + $versionSuffix
    }

    Write-Verbose "$nugetExe pack $stagingDir\$file -OutputDirectory $packageOutDir -Version $version -Properties Version=$version``;Srcroot=$env:TF_SRC_DIR``;Packagesroot=$env:TF_PACKAGES_DIR``;TestPlatformVersion=$TestPlatformVersion``;NOWARN=`"NU5127,NU5128,NU5129`"``;BranchName=$TPB_BRANCH``;CommitId=$TPB_COMMIT"
    & $nugetExe pack $stagingDir\$file -OutputDirectory $packageOutDir -Version $version -Properties Version=$version`;Srcroot=$env:TF_SRC_DIR`;Packagesroot=$env:TF_PACKAGES_DIR`;TestPlatformVersion=$TestPlatformVersion`;NOWARN="NU5127,NU5128,NU5129"`;BranchName=$TPB_BRANCH`;CommitId=$TPB_COMMIT
    if ($lastExitCode -ne 0) {
      throw "Nuget pack failed with an exit code of '$lastExitCode'."
    }
  }

  Write-Log "Create-NugetPackages: Complete. {$(Get-ElapsedTime($timer))}"
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

Print-Help

if (ShouldRunStep @("UpdateTPVersion")) {
  Sync-PackageVersions
}

if (ShouldRunStep @("Install-WindowsSDK")) {
  Install-WindowsSDK
}

if (ShouldRunStep @("UpdateTPVersion", "Restore")) {
  Perform-Restore
}

if (ShouldRunStep @("Build")) {
  Perform-Build
}

if (ShouldRunStep @("Publish")) {
  Create-NugetPackages
}
