# Common utilities for building solution and running tests

#
# Global variables
#
$global:msbuildVersion = "15.0"
$global:nugetVersion = "3.6.0-beta1"
$global:locateVsApiVersion = "0.2.4-beta"
$global:nugetUrl = "https://dist.nuget.org/win-x86-commandline/v$nugetVersion/NuGet.exe"

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

function Locate-MSBuild {
  $msbuildPath = Locate-MSBuildPath
  $msbuild = Join-Path -path $msbuildPath -childPath "MSBuild.exe"

  if (!(Test-Path -path $msbuild)) {
    throw "The specified MSBuild version ($msbuildVersion) could not be located."
  }

  return Resolve-Path -path $msbuild
}

function Locate-MSBuildPath {
  $vsInstallPath = Locate-VsInstallPath
  $msbuildPath = Join-Path -path $vsInstallPath -childPath "MSBuild\$msbuildVersion\Bin"
  return Resolve-Path -path $msbuildPath
}

function Locate-NuGet {
  $rootPath = Locate-RootPath
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
  $rootPath = Locate-RootPath
  $nugetConfig = Join-Path -path $rootPath -childPath "Nuget.config"
  return Resolve-Path -path $nugetConfig
}

function Locate-Toolset {
  $rootPath = Locate-RootPath
  $toolset = Join-Path -path $rootPath -childPath "scripts\Toolset\packages.config"
  return Resolve-Path -path $toolset
}

function Locate-RootPath {
  $scriptPath = Locate-ScriptPath
  $rootPath = Join-Path -path $scriptPath -childPath "..\"
  return Resolve-Path -path $rootPath
}

function Locate-ScriptPath {
  $myInvocation = Get-Variable -name "MyInvocation" -scope "Script"
  $scriptPath = Split-Path -path $myInvocation.Value.MyCommand.Definition -parent
  return Resolve-Path -path $scriptPath
}

function Locate-PackagesPath {
  $rootPath = Locate-RootPath
  $packagesPath = Join-Path -path $rootPath -childPath "packages"
  
  Create-Directory -path $packagesPath
  return Resolve-Path -path $packagesPath
}

function Locate-VsInstallPath {
   $locateVsApi = Locate-LocateVsApi
   $requiredPackageIds = @()

   $requiredPackageIds += "Microsoft.Component.MSBuild"
   $requiredPackageIds += "Microsoft.Net.Component.4.6.TargetingPack"
   $requiredPackageIds += "Microsoft.VisualStudio.Component.Roslyn.Compiler"
   $requiredPackageIds += "Microsoft.VisualStudio.Component.VSSDK"

   Add-Type -path $locateVsApi
   $vsInstallPath = [LocateVS.Instance]::GetInstallPath($msbuildVersion, $requiredPackageIds)

   Write-Host -object "VSInstallPath is : $vsInstallPath" 
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

function Locate-Solution([string] $relativePath) {
  $rootPath = Locate-RootPath
  $solution = Join-Path -path $rootPath -childPath $relativePath
  return Resolve-Path -path $solution
}
