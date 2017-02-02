﻿# Copyright (c) Microsoft. All rights reserved.
# Build script for MSTest Test Framework.

[CmdletBinding(PositionalBinding=$false)]
Param(  
  [Parameter(Mandatory=$false)]
  [ValidateSet("Debug", "Release")]
  [Alias("c")]
  [System.String] $Configuration = "Debug",

  [Parameter(Mandatory=$false)]
  [Alias("fv")]
  [System.String] $FrameworkVersion = "99.99.99",
  
  [Parameter(Mandatory=$false)]
  [Alias("av")]
  [System.String] $AdapterVersion = "99.99.99",

  [Parameter(Mandatory=$false)]
  [Alias("vs")]
  [System.String] $VersionSuffix = "dev",
  
  [Parameter(Mandatory=$false)]
  [System.String] $Target = "Build",
  
  [Parameter(Mandatory=$false)]
  [Alias("h")]
  [Switch] $Help = $false,

  [Parameter(Mandatory=$false)]
  [Alias("cl")]
  [Switch] $Clean = $false,

  [Parameter(Mandatory=$false)]
  [Alias("sr")]
  [Switch] $SkipRestore = $false,

  [Parameter(Mandatory=$false)]
  [Alias("cache")]
  [Switch] $ClearPackageCache = $false,

  [Parameter(Mandatory=$false)]
  [Alias("tmpl")]
  [Switch] $Templates = $false,

  [Parameter(Mandatory=$false)]
  [Alias("wiz")]
  [Switch] $Wizards = $false,

  [Parameter(Mandatory=$false)]
  [Switch] $Official = $false,

  [Parameter(Mandatory=$false)]
  [Switch] $Full = $false
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
$TFB_SkipRestore = $SkipRestore
$TFB_Clean = $Clean
$TFB_Solutions = @("TestFx.sln","Templates\MSTestTemplates.sln","WizardExtensions\WizardExtensions.sln")
$TFB_VSmanprojs =@("src\setup\Microsoft.VisualStudio.Templates.CS.MSTestv2.Desktop.UnitTest.vsmanproj",
                   "src\setup\Microsoft.VisualStudio.Templates.CS.MSTestv2.UWP.UnitTest.vsmanproj", 
                   "src\setup\Microsoft.VisualStudio.TestTools.MSTestV2.WizardExtension.IntelliTest.vsmanproj", 
                   "src\setup\Microsoft.VisualStudio.TestTools.MSTestV2.WizardExtension.UnitTest.vsmanproj")

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
  Write-Host -object "  Help (-h)                     - [Switch] - Prints this help message."
  Write-Host -object "  Clean (-cl)                   - [Switch] - Indicates that this should be a clean build."
  Write-Host -object "  SkipRestore (-sr)             - [Switch] - Indicates nuget package restoration should be skipped."
  Write-Host -object "  ClearPackageCache (-cache)    - [Switch] - Indicates local package cache should be cleared before restore."
  Write-Host -object "  Templates (-tmpl)             - [Switch] - Indicates Templates should also be built."
  Write-Host -object "  Wizards (-wiz)                - [Switch] - Indicates WizardExtensions should also be built."
  Write-Host -object "  Official                      - [Switch] - Indicates that this is an official build. Only used in CI builds."
  Write-Host -object "  Full                          - [Switch] - Indicates to perform a full build which includes Adapter,Framework,Templates,Wizards, and vsmanprojs."
  Write-Host -object ""
  Write-Host -object "  Configuration (-c)            - [String] - Specifies the build configuration. Defaults to 'Debug'."
  Write-Host -object "  FrameworkVersion (-fv)        - [String] - Specifies the version of the Test Framework nuget package."
  Write-Host -object "  AdapterVersion (-av)          - [String] - Specifies the version of the Test Adapter nuget package."
  Write-Host -object "  VersionSuffix (-vs)           - [String] - Specifies the version suffix for the nuget packages."
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
  
  if($TFB_SkipRestore)
  {
    Write-Log "Perform-Restore: Skipped."
    return;
  }

  $nuget = Locate-NuGet
  $nugetConfig = Locate-NuGetConfig
  $toolset = Locate-Toolset
  
  if ($ClearPackageCache) {
    Write-Log "    Clearing local package cache..."
    & $nuget locals all -clear
  }

  Write-Log "    Starting toolset restore..."
  Write-Verbose "$nuget restore -msbuildVersion $msbuildVersion -verbosity quiet -nonInteractive -configFile $nugetConfig $toolset"
  & $nuget restore -msbuildVersion $msbuildVersion -verbosity quiet -nonInteractive -configFile $nugetConfig $toolset
  
  if ($lastExitCode -ne 0) {
    throw "The restore failed with an exit code of '$lastExitCode'."
  }

  Write-Verbose "Locating MSBuild install path..."
  $msbuildPath = Locate-MSBuildPath 

  Write-Verbose "Starting solution restore..."
  foreach($solution in $TFB_Solutions)
  {
	$solutionPath = Locate-Solution -relativePath $solution

	Write-Verbose "$nuget restore -msbuildPath $msbuildPath -verbosity quiet -nonInteractive -configFile $nugetConfig $solutionPath"
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

  if($TFB_Clean)
  {
    $foldersToDel = @( $TFB_Configuration, "TestAssets" )
    Write-Log "    Clean build requested."
    foreach($folder in $foldersToDel)
    {
      $outDir = Join-Path $env:TF_OUT_DIR -ChildPath $folder
      Write-Output "    Deleting $outDir"
      Remove-Item -Recurse -Force $outDir
    }
  }

  Invoke-Build -solution "TestFx.sln"
  
  if($Templates -or $Full)
  {
	Invoke-Build -solution "Templates\MSTestTemplates.sln"
  }
  
  if($Wizards -or $Full)
  {
	Invoke-Build -solution "WizardExtensions\WizardExtensions.sln"	
  }
  
  if($Official)
  {
	Build-vsmanprojs
  }
  
  Write-Log "Perform-Build: Completed. {$(Get-ElapsedTime($timer))}"
}

function Invoke-Build([string] $solution)
{
    $msbuild = Locate-MSBuild
	$solutionPath = Locate-Solution -relativePath $solution
    $solutionDir = [System.IO.Path]::GetDirectoryName($solutionPath)
    $solutionSummaryLog = Join-Path -path $solutionDir -childPath "msbuild.log"
    $solutionWarningLog = Join-Path -path $solutionDir -childPath "msbuild.wrn"
    $solutionFailureLog = Join-Path -path $solutionDir -childPath "msbuild.err"

	Write-Log "    Building $solution..."
	Write-Verbose "$msbuild /t:$Target /p:Configuration=$configuration /tv:$msbuildVersion /v:m /flp1:Summary`;Verbosity=diagnostic`;Encoding=UTF-8`;LogFile=$solutionSummaryLog /flp2:WarningsOnly`;Verbosity=diagnostic`;Encoding=UTF-8`;LogFile=$solutionWarningLog /flp3:ErrorsOnly`;Verbosity=diagnostic`;Encoding=UTF-8`;LogFile=$solutionFailureLog $solutionPath"
	& $msbuild /t:$Target /p:Configuration=$configuration /tv:$msbuildVersion /v:m /flp1:Summary`;Verbosity=diagnostic`;Encoding=UTF-8`;LogFile=$solutionSummaryLog /flp2:WarningsOnly`;Verbosity=diagnostic`;Encoding=UTF-8`;LogFile=$solutionWarningLog /flp3:ErrorsOnly`;Verbosity=diagnostic`;Encoding=UTF-8`;LogFile=$solutionFailureLog $solutionPath
  
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
	
	Write-Log "    Building $vsmanproj..."
	Write-Verbose "$msbuild /t:$Target /p:Configuration=$configuration /tv:$msbuildVersion /m /p:TargetExt=.vsman $vsmanprojPath"
	& $msbuild /t:$Target /p:Configuration=$configuration /tv:$msbuildVersion /m /p:TargetExt=.vsman $vsmanprojPath
	
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
    if($Official)
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

Print-Help
Perform-Restore
Perform-Build
Create-NugetPackages
