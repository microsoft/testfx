# Copyright (c) Microsoft. All rights reserved.
# Build script for MSTest Test Framework.

[CmdletBinding(PositionalBinding=$false)]
Param(
  [switch] $help,
  
  [string] $target = "Build",

  [Parameter(Mandatory=$false)]
  [ValidateSet("Debug", "Release")]
  [Alias("c")]
  [string] $Configuration = "Debug",

  [Parameter(Mandatory=$false)]
  [Alias("fv")]
  [System.String] $FrameworkVersion = "99.99.99",
  
  [Parameter(Mandatory=$false)]
  [Alias("av")]
  [System.String] $AdapterVersion = "99.99.99",

  [Parameter(Mandatory=$false)]
  [Alias("vs")]
  [System.String] $VersionSuffix = "dev",
  
  [switch] $clearPackageCache,
  [switch] $templates,
  [switch] $wizards,
  [switch] $official,
  [switch] $full
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
$TFB_Solutions = @("TestFx.sln","Templates\MSTestTemplates.sln","WizardExtensions\WizardExtensions.sln")
$TFB_VSmanprojs =@(
"setup\Templates\Desktop\Microsoft.VisualStudio.Templates.CS.MSTestv2.Desktop.UnitTest.vsmanproj",
"setup\Templates\UWP\Microsoft.VisualStudio.Templates.CS.MSTestv2.UWP.UnitTest.vsmanproj",
"setup\WizardExtensions\MSTestv2IntelliTestExtension\Microsoft.VisualStudio.TestTools.MSTestV2.WizardExtension.IntelliTest.vsmanproj",
"setup\WizardExtensions\MSTestv2UnitTestExtension\Microsoft.VisualStudio.TestTools.MSTestV2.WizardExtension.UnitTest.vsmanproj"
)

#
# Script Preferences
#
$ErrorActionPreference = "Stop"

#
# Prints help text for the switches this script supports.
#
function Print-Help {
  if (-not $help) {
    return
  }

  Write-Host -object ""
  Write-Host -object "MSTest Adapter Build Script"
  Write-Host -object ""
  Write-Host -object "  Help                          - [Switch] - Prints this help message."
  Write-Host -object "  ClearPackageCache             - [Switch] - Indicates local package cache should be cleared before restore."
  Write-Host -object "  Templates                     - [Switch] - Indicates Templates should also be build."
  Write-Host -object "  Wizards                       - [Switch] - Indicates WizardExtensions should also be build."
  Write-Host -object "  Official                      - [Switch] - Indicates that this is an official build."
  Write-Host -object "  Full                          - [Switch] - Indicates to perform a full build which includes Adapter,Framework,Templates,Wizards, and vsmanprojs."
  Write-Host -object ""
  Write-Host -object "  Configuration                 - [String] - Specifies the build configuration. Defaults to 'Debug'."
  Write-Host -object "  FrameworkVersion              - [String] - Specifies the version of the Test Framework nuget package."
  Write-Host -object "  AdapterVersion                - [String] - Specifies the version of the Test Adapter nuget package."
  Write-Host -object "  VersionSuffix                 - [String] - Specifies the version suffix for the nuget packages."
  Write-Host -object "  Target                        - [String] - Specifies the build target. Defaults to 'Build'."

  Write-Host -object ""
  Exit 0
}

#
# Restores packages for the solutions.
#
function Perform-Restore {
  $timer = Start-Timer

  Write-Log "Perform-Restore: Started."
  
  $nuget = Locate-NuGet
  $nugetConfig = Locate-NuGetConfig
  $toolset = Locate-Toolset
  
  if ($clearPackageCache) {
    Write-Host -object "Clearing local package cache..."
    & $nuget locals all -clear
  }

  Write-Host -object "Starting toolset restore..."
  Write-Host -object "$nuget restore -msbuildVersion $msbuildVersion -verbosity quiet -nonInteractive -configFile $nugetConfig $toolset"
  & $nuget restore -msbuildVersion $msbuildVersion -verbosity quiet -nonInteractive -configFile $nugetConfig $toolset
  
  if ($lastExitCode -ne 0) {
    throw "The restore failed with an exit code of '$lastExitCode'."
  }

  Write-Host -object "Locating MSBuild install path..."
  $msbuildPath = Locate-MSBuildPath 

  Write-Host -object "Starting solution restore..."
  foreach($solution in $TFB_Solutions)
  {
	$solutionPath = Locate-Solution -relativePath $solution

	Write-Host -object "$nuget restore -msbuildPath $msbuildPath -verbosity quiet -nonInteractive -configFile $nugetConfig $solutionPath"
	& $nuget restore -msbuildPath $msbuildPath -verbosity quiet -nonInteractive -configFile $nugetConfig $solutionPath
  }

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

  Invoke-Build -solution "TestFx.sln"
  
  if($templates -or $full)
  {
	Invoke-Build -solution "Templates\MSTestTemplates.sln"
  }
  
  if($wizards -or $full)
  {
	Invoke-Build -solution "WizardExtensions\WizardExtensions.sln"	
  }
  
  if($official)
  {
	Build-vsmanprojs
  }
  
  Write-Log "Perform-Build: Completed. {$(Get-ElapsedTime($timer))}"
}

function Invoke-Build([string] $solution)
{
    $msbuild = Locate-MSBuild
	$solutionPath = Locate-Solution -relativePath $solution

	Write-Host -object "Starting $solution build..."
	Write-Host -object "$msbuild /t:$target /p:Configuration=$configuration /tv:$msbuildVersion /v:q /m $solutionPath"
	& $msbuild /t:$target /p:Configuration=$configuration /tv:$msbuildVersion /v:q /m $solutionPath
  
	if ($lastExitCode -ne 0) {
		throw "Build failed with an exit code of '$lastExitCode'."
	}
}

function Build-vsmanprojs
{
  $msbuild = Locate-MSBuild
  
  foreach($vsmanproj in $TFB_VSmanprojs)
  {
	$vsmanprojPath = Locate-Solution -relativePath $vsmanproj
	
	Write-Host -object "Starting $vsmanproj build..."
	Write-Host -object "$msbuild /t:$target /p:Configuration=$configuration /tv:$msbuildVersion /m /p:TargetExt=.vsman $vsmanprojPath"
	& $msbuild /t:$target /p:Configuration=$configuration /tv:$msbuildVersion /m /p:TargetExt=.vsman $vsmanprojPath
	
	if ($lastExitCode -ne 0) {
		throw "VSManProj build failed with an exit code of '$lastExitCode'."
	}
  }
}

#
# Creates Fx & Adapter nuget packages
#
function Create-NugetPackages
{
    $timer = Start-Timer

    Write-Log "Create-NugetPackages: Started."

    $stagingDir = Join-Path $env:TF_OUT_DIR $TFB_Configuration
    $packageOutDir = Join-Path $stagingDir "MSTestPackages"
    $tfSrcPackageDir = Join-Path $env:TF_SRC_DIR "Package"

    # Copy over the nuspecs to the staging directory
    if($official)
    {
        $nuspecFiles = @("MSTest.TestAdapter.Dotnet.nuspec", "MSTest.TestAdapter.nuspec", "MSTest.TestAdapter.symbols.nuspec", "MSTest.TestFramework.nuspec", "MSTest.TestFramework.symbols.nuspec")
    }
    else
    {
        $nuspecFiles = @("MSTest.TestAdapter.Enu.nuspec","MSTest.TestFramework.enu.nuspec", "MSTest.TestAdapter.Dotnet.nuspec")
    }

    foreach ($file in $nuspecFiles) {
        Copy-Item $tfSrcPackageDir\$file $stagingDir -Force
    }

    # Call nuget pack on these components.
    $nugetExe = Locate-Nuget

    foreach ($file in $nuspecFiles) {
        $version = $TFB_FrameworkVersion
        
        if($file.Contains("TestAdapter"))
        {
            $version = $TFB_AdapterVersion
        }

        Write-Verbose "$nugetExe pack $stagingDir\$file -OutputDirectory $packageOutDir -Version=$version-$TFB_VersionSuffix -Properties Version=$version-$TFB_VersionSuffix"
        & $nugetExe pack $stagingDir\$file -OutputDirectory $packageOutDir -Version $version-$TFB_VersionSuffix -Properties Version=$version-$TFB_VersionSuffix`;Srcroot=$env:TF_SRC_DIR`;Packagesroot=$env:TF_PACKAGES_DIR
    }

    Write-Log "Create-NugetPackages: Complete. {$(Get-ElapsedTime($timer))}"
}

function Start-Timer
{
    return [System.Diagnostics.Stopwatch]::StartNew()
}

function Get-ElapsedTime([System.Diagnostics.Stopwatch] $timer)
{
    $timer.Stop()
    return $timer.Elapsed
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

Print-Help
Perform-Restore
Perform-Build
Create-NugetPackages
