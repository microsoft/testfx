[CmdletBinding(PositionalBinding=$false)]
Param(
  [string] $msbuildVersion = "15.0",
  [string] $nugetVersion = "3.6.0-beta1",
  [string] $target = "Build",
  [string] $configuration = "Debug",
  [string] $locateVsApiVersion = "0.2.4-beta",  
  [switch] $skipRestore,
  [switch] $clearPackageCache
)

# set-strictmode -version 2.0
$ErrorActionPreference = "Stop"


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

  Download-File -address "https://dist.nuget.org/win-x86-commandline/v$nugetVersion/NuGet.exe" -fileName $nuget

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
  $locateVsApi = Join-Path -path $packagesPath -ChildPath "RoslynTools.Microsoft.LocateVS\$locateVsApiVersion\tools\LocateVS.dll"

  if (!(Test-Path -path $locateVsApi)) {
    throw "The specified LocateVS API version ($locateVsApiVersion) could not be located."
  }

  return Resolve-Path -path $locateVsApi
}

function Locate-PackagesPath {
  if ($env:NUGET_PACKAGES -eq $null) {
    $env:NUGET_PACKAGES =  Join-Path -path $env:UserProfile -childPath ".nuget\packages\"
  }
  
  $packagesPath = $env:NUGET_PACKAGES

  Create-Directory -path $packagesPath
  return Resolve-Path -path $packagesPath
}

function Locate-RootPath {
  $scriptPath = Locate-ScriptPath
  $rootPath = Join-Path -path $scriptPath -childPath "..\..\"
  return Resolve-Path -path $rootPath
}

function Locate-ScriptPath {
  $myInvocation = Get-Variable -name "MyInvocation" -scope "Script"
  $scriptPath = Split-Path -path $myInvocation.Value.MyCommand.Definition -parent
  return Resolve-Path -path $scriptPath
}

function Locate-Solution {
  $rootPath = Locate-RootPath
  $solution = Join-Path -path $rootPath -childPath "TestFx.sln"
  return Resolve-Path -path $solution
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

  Download-File -address "https://dist.nuget.org/win-x86-commandline/v$nugetVersion/NuGet.exe" -fileName $nuget


  if (!(Test-Path -path $nuget)) {
    throw "The specified NuGet version ($nugetVersion) could not be downloaded."
  }

  return Resolve-Path -path $nuget

}

function Perform-Build {
  Write-Host -object ""

  $msbuild = Locate-MSBuild

  $solution = Locate-Solution

  Write-Host -object "Starting solution build..."
  & $msbuild /t:$target /p:Configuration=$configuration /tv:$msbuildVersion /m $solution

  if ($lastExitCode -ne 0) {
    throw "The build failed with an exit code of '$lastExitCode'."
  }

  Write-Host -object "The build completed successfully." -foregroundColor Green
}

function Perform-Restore {
  Write-Host -object ""

  if ($skipRestore) {
    Write-Host -object "Skipping restore..."
    return
  }

  $nuget = Locate-NuGet
  $nugetConfig = Locate-NuGetConfig
  $packagesPath = Locate-PackagesPath
  $toolset = Locate-Toolset
  $solution = Locate-Solution
  
  if ($clearPackageCache) {
    Write-Host -object "Clearing local package cache..."
    & $nuget locals all -clear
  }

  Write-Host -object "Starting toolset restore..."
  & $nuget restore -packagesDirectory $packagesPath -msbuildVersion $msbuildVersion -verbosity quiet -nonInteractive -configFile $nugetConfig $toolset

  if ($lastExitCode -ne 0) {
    throw "The restore failed with an exit code of '$lastExitCode'."
  }

  Write-Host -object "Locating MSBuild install path..."
  $msbuildPath = Locate-MSBuildPath

  Write-Host -object "Starting solution restore..."
  & $nuget restore -packagesDirectory $packagesPath -msbuildPath $msbuildPath -verbosity quiet -nonInteractive -configFile $nugetConfig $solution

  if ($lastExitCode -ne 0) {
    throw "The restore failed with an exit code of '$lastExitCode'."
  }

  Write-Host -object "The restore completed successfully." -foregroundColor Green
}

function Print-Help {
  if (-not $help) {
    return
  }

  Write-Host -object ""
  Write-Host -object "LiveUnitTesting Build Script"
  Write-Host -object ""
  Write-Host -object "  Help                          - [Switch] - Prints this help message."
  Write-Host -object ""
  Write-Host -object "  Configuration                 - [String] - Specifies the build configuration. Defaults to 'Debug'."
  Write-Host -object "  DeployHive                    - [String] - Specifies the VSIX deployment hive. Defaults to 'LiveUnitTesting'."
  Write-Host -object "  IntegrationTestFilter         - [String] - Specifies the integration test filter. Defaults to '*.IntegrationTests.dll'."
  Write-Host -object "  LocateVsApiVersion            - [String] - Specifies the LocateVs API version. Defaults to '0.2.4-beta'."
  Write-Host -object "  ModifyVsixManifestToolVersion - [String] - Specifies the ModifyVsixManifest tool version. Defaults to '0.2.4-beta'."
  Write-Host -object "  MSBuildVersion                - [String] - Specifies the MSBuild version. Defaults to '15.0'."
  Write-Host -object "  NuGetVersion                  - [String] - Specifies the NuGet version. Defaults to '3.6.0-beta1'."
  Write-Host -object "  RoslynDeploymentVsixVersion   - [String] - Specifies the roslyn vsix version. Defaults to '2.0.0.6120504'."
  Write-Host -object "  SignToolVersion               - [String] - Specifies the Sign tool version. Defaults to '0.2.4-beta'."
  Write-Host -object "  Target                        - [String] - Specifies the build target. Defaults to 'Build'."
  Write-Host -object "  TestFilter                    - [String] - Specifies the test filter. Defaults to '*.UnitTests.dll'."
  Write-Host -object "  TestSetupVsixVersion          - [String] - Specifies the test setup vsix version. Defaults to '2.0.0.6120504'."
  Write-Host -object "  VsixExpInstallerToolVersion   - [String] - Specifies the VsixExpInstaller tool version. Defaults to '0.2.4-beta'."
  Write-Host -object "  xUnitVersion                  - [String] - Specifies the xUnit version. Defaults to '2.2.0-beta4-build3444'."
  Write-Host -object ""
  Write-Host -object "  ClearPackageCache             - [Switch] - Indicates local package cache should be cleared before restore."
  Write-Host -object "  Instrument                    - [Switch] - Indicates the build should produce instrumented binaries."
  Write-Host -object "  Integration                   - [Switch] - Indicates the Integration Tests should be run."
  Write-Host -object "  Official                      - [Switch] - Indicates this is an official build which changes the semantic version."
  Write-Host -object "  RealSign                      - [Switch] - Indicates the real signing step should be performed."
  Write-Host -object "  SkipBuild                     - [Switch] - Indicates the build step should be skipped."
  Write-Host -object "  SkipDeploy                    - [Switch] - Indicates the VSIX deployment step should be skipped."
  Write-Host -object "  SkipRestore                   - [Switch] - Indicates the restore step should be skipped."
  Write-Host -object "  SkipTest                      - [Switch] - Indicates the test step should be skipped."
  Write-Host -object "  SkipTest32                    - [Switch] - Indicates the 32-bit Unit Tests should be skipped."
  Write-Host -object "  SkipTest64                    - [Switch] - Indicates the 64-bit Unit Tests should be skipped."

  Exit 0
}

Perform-Restore
Perform-Build

