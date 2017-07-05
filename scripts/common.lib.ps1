# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT license. See LICENSE file in the project root for full license information.

# Common utilities for building solution and running tests

#
# Global Variables
#
$global:msbuildVersion = "15.0"
$global:nugetVersion = "3.6.0-beta1"
$global:locateVsApiVersion = "0.2.4-beta"
$global:nugetUrl = "https://dist.nuget.org/win-x86-commandline/v$nugetVersion/NuGet.exe"

#
# Global Environment Variables
#
$env:TF_ROOT_DIR = (Get-Item (Split-Path $MyInvocation.MyCommand.Path)).Parent.FullName
$env:TF_OUT_DIR = Join-Path $env:TF_ROOT_DIR "artifacts"
$env:TF_SRC_DIR = Join-Path $env:TF_ROOT_DIR "src"
$env:TF_TEST_DIR = Join-Path $env:TF_ROOT_DIR "test"
$env:TF_PACKAGES_DIR = Join-Path $env:TF_ROOT_DIR "packages"


function Create-Directory([string[]] $path) {
  if (!(Test-Path -path $path)) {
    New-Item -path $path -force -itemType "Directory" | Out-Null
  }
}

function Download-File([string] $address, [string] $fileName) {
  $webClient = New-Object -typeName "System.Net.WebClient"
  $webClient.DownloadFile($address, $fileName)
}

function Get-ProductVersion([string[]] $path) {
  if (!(Test-Path -path $path)) {
    return ""
  }

  $item = Get-Item -path $path
  return $item.VersionInfo.ProductVersion
}

function Locate-MSBuild($hasVsixExtension = "false") {
  $msbuildPath = Locate-MSBuildPath -hasVsixExtension $hasVsixExtension
  $msbuild = Join-Path -path $msbuildPath -childPath "MSBuild.exe"

  if (!(Test-Path -path $msbuild)) {
    throw "The specified MSBuild version ($msbuildVersion) could not be located."
  }

  return Resolve-Path -path $msbuild
}

function Locate-MSBuildPath($hasVsixExtension = "false") {
  $vsInstallPath = Locate-VsInstallPath -hasVsixExtension $hasVsixExtension
  $msbuildPath = Join-Path -path $vsInstallPath -childPath "MSBuild\$msbuildVersion\Bin"
  return Resolve-Path -path $msbuildPath
}

function Locate-NuGet {
  $rootPath = $env:TF_ROOT_DIR
  $nuget = Join-Path -path $rootPath -childPath "nuget.exe"

  if (Test-Path -path $nuget) {
    $currentVersion = Get-ProductVersion -path $nuget

    if ($currentVersion.StartsWith($nugetVersion)) {
      return Resolve-Path -path $nuget
    }

    Write-Host -object "The located version of NuGet ($currentVersion) is out of date. The specified version ($nugetVersion) will be downloaded instead."
    Remove-Item -path $nuget | Out-Null
  }

  Download-File -address $nugetUrl -fileName $nuget

  if (!(Test-Path -path $nuget)) {
    throw "The specified NuGet version ($nugetVersion) could not be downloaded."
  }

  return Resolve-Path -path $nuget
}

function Locate-NuGetConfig {
  $rootPath = $env:TF_ROOT_DIR
  $nugetConfig = Join-Path -path $rootPath -childPath "Nuget.config"
  return Resolve-Path -path $nugetConfig
}

function Locate-Toolset {
  $rootPath = $env:TF_ROOT_DIR
  $toolset = Join-Path -path $rootPath -childPath "scripts\Toolset\packages.config"
  return Resolve-Path -path $toolset
}

function Locate-PackagesPath {
  $rootPath = $env:TF_ROOT_DIR
  $packagesPath = Join-Path -path $rootPath -childPath "packages"
  
  Create-Directory -path $packagesPath
  return Resolve-Path -path $packagesPath
}

function Locate-VsInstallPath($hasVsixExtension = "false") {
   $locateVsApi = Locate-LocateVsApi
   $requiredPackageIds = @()

   $requiredPackageIds += "Microsoft.Component.MSBuild"
   $requiredPackageIds += "Microsoft.Net.Component.4.6.TargetingPack"
   $requiredPackageIds += "Microsoft.VisualStudio.Component.Roslyn.Compiler"
   
   if($hasVsixExtension -eq 'true')
   {
     $requiredPackageIds += "Microsoft.VisualStudio.Component.VSSDK"
   }
   
   Write-Verbose "VSInstallation requirements : $requiredPackageIds"

   Add-Type -path $locateVsApi

   try
   {
    $vsInstallPath = [LocateVS.Instance]::GetInstallPath("15.3", $requiredPackageIds)
   } catch [Exception] {
    $vsInstallPath = [LocateVS.Instance]::GetInstallPath($msbuildVersion, $requiredPackageIds)
  }

   Write-Verbose "VSInstallPath is : $vsInstallPath"
   return Resolve-Path -path $vsInstallPath
}

function Locate-LocateVsApi {
  $packagesPath = Locate-PackagesPath
  $locateVsApi = Join-Path -path $packagesPath -ChildPath "RoslynTools.Microsoft.LocateVS.$locateVsApiVersion\tools\LocateVS.dll"

  if (!(Test-Path -path $locateVsApi)) {
    throw "The specified LocateVS API version ($locateVsApiVersion) could not be located."
  }

  return Resolve-Path -path $locateVsApi
}

function Locate-Item([string] $relativePath) {
  $rootPath = $env:TF_ROOT_DIR
  $itemPath = Join-Path -path $rootPath -childPath $relativePath
  return Resolve-Path -path $itemPath
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

function Write-Log ([string] $message, $messageColor = "Green")
{
    $currentColor = $Host.UI.RawUI.ForegroundColor
    $Host.UI.RawUI.ForegroundColor = $messageColor
    if ($message)
    {
        Write-Output "... $message"
    }
    $Host.UI.RawUI.ForegroundColor = $currentColor
}
