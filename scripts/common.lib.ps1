# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT license. See LICENSE file in the project root for full license information.

Add-Type -AssemblyName System.IO.Compression.FileSystem

# Common utilities for building solution and running tests

$TF_ROOT_DIR = (Get-Item (Split-Path $MyInvocation.MyCommand.Path)).Parent.FullName
$TF_VERSIONS_FILE = "$TF_ROOT_DIR\eng\Versions.props"
$TF_OUT_DIR = Join-Path $TF_ROOT_DIR "artifacts"
$TF_SRC_DIR = Join-Path $TF_ROOT_DIR "src"
$TF_TEST_DIR = Join-Path $TF_ROOT_DIR "test"
$TF_PACKAGES_DIR = Join-Path $TF_ROOT_DIR "packages"
$TF_TOOLS_DIR = Join-Path $TF_ROOT_DIR "tools"

function Get-PackageVersion ([string]$PackageName) {
  $packages = ([XML](Get-Content $TF_VERSIONS_FILE)).Project.PropertyGroup

  return $packages[$PackageName].InnerText;
}

#
# Global Variables
#
$global:nugetVersion = Get-PackageVersion -PackageName "NuGetFrameworksVersion"
$global:vswhereVersion = Get-PackageVersion -PackageName "VsWhereVersion"

#
# Global Environment Variables
#
$env:TF_ROOT_DIR = $TF_ROOT_DIR
$env:TF_OUT_DIR = $TF_OUT_DIR
$env:TF_SRC_DIR = $TF_SRC_DIR
$env:TF_TEST_DIR = $TF_TEST_DIR
$env:TF_PACKAGES_DIR = $TF_PACKAGES_DIR
$env:TF_TOOLS_DIR = $TF_TOOLS_DIR
$env:DOTNET_CLI_VERSION = "6.0.100-alpha.1.21067.8"

if ([String]::IsNullOrWhiteSpace($TestPlatformVersion)) {
  $TestPlatformVersion = Get-PackageVersion -PackageName "MicrosoftNETTestSdkVersion"
}

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
    throw "The specified MSBuild could not be located."
  }

  return Resolve-Path -path $msbuild
}

function Locate-MSBuildPath($hasVsixExtension = "false") {
  $vsInstallPath = Locate-VsInstallPath -hasVsixExtension $hasVsixExtension

  # first try to find the VS2019+ path
  try {
    $msbuildPath = Join-Path -path $vsInstallPath -childPath "MSBuild\Current\Bin"
    $msbuildPath = Resolve-Path $msbuildPath
  }
  catch {
    # Resolve-Path throws if the path does not exist, so use the VS2017 path as a fallback
    $msbuildPath = Join-Path -path $vsInstallPath -childPath "MSBuild\15.0\Bin"
    $msbuildPath = Resolve-Path $msbuildPath
  }

  return $msbuildPath
}

function Locate-NuGet {
  $rootPath = Join-Path -path $env:TF_PACKAGES_DIR -childPath "toolset"
  $nuget = Join-Path -path $rootPath -childPath "nuget.exe"

  if (Test-Path -path $nuget) {
    $currentVersion = Get-ProductVersion -path $nuget

    if ($currentVersion.StartsWith($nugetVersion)) {
      return Resolve-Path -path $nuget
    }

    Write-Host -object "The located version of NuGet ($currentVersion) is out of date. The specified version ($nugetVersion) will be downloaded instead."
    Remove-Item -path $nuget | Out-Null
  }

  New-Item $rootPath -ItemType Directory | Out-Null
  Download-File -address "https://dist.nuget.org/win-x86-commandline/v$nugetVersion/NuGet.exe" -fileName $nuget

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

function Locate-PackagesPath {
  $rootPath = $env:TF_ROOT_DIR
  $packagesPath = Join-Path -path $rootPath -childPath "packages"

  Create-Directory -path $packagesPath
  return Resolve-Path -path $packagesPath
}

function Locate-VsWhere {
  $packagesPath = Locate-PackagesPath

  $vswhere = Join-Path -path $packagesPath -childPath "vswhere\$vswhereVersion\tools\vswhere.exe"
  if(-not (Test-Path $vswhere)) {
    $nuget = Locate-NuGet
    $nugetConfig = Locate-NuGetConfig

    Write-Verbose "$nuget install vswhere -version $vswhereVersion -OutputDirectory $packagesPath -ConfigFile $nugetConfig -ExcludeVersion"
    & $nuget install vswhere -version $vswhereVersion -OutputDirectory $packagesPath -ConfigFile $nugetConfig -ExcludeVersion | Out-Null
  }

  Write-Verbose "vswhere location is : $vswhere"
  return $vswhere
}

function Locate-VsInstallPath($hasVsixExtension = "false") {
  $vswhere = Locate-VsWhere
  $requiredPackageIds = @()

  $requiredPackageIds += "Microsoft.Component.MSBuild"
  $requiredPackageIds += "Microsoft.Net.Component.4.5.2.TargetingPack"
  $requiredPackageIds += "Microsoft.VisualStudio.Windows.Build"

  if ($hasVsixExtension -eq 'true') {
    $requiredPackageIds += "Microsoft.VisualStudio.Component.VSSDK"
  }
  $version = "[16.0.0, 18.0.0)"
  Write-Verbose "$vswhere -version $version -products * -requires $requiredPackageIds -property installationPath"

  if ($Official -or $DisallowPrereleaseMSBuild) {
    $vsInstallPath = & $vswhere -version $version -products * -requires $requiredPackageIds -property installationPath | Select-Object -First 1
  }
  else {
    # Allow using pre release versions of VS for dev builds
    $vsInstallPath = & $vswhere -version $version -prerelease -products * -requires $requiredPackageIds -property installationPath | Select-Object -First 1
  }

  if (-not $vsInstallPath)
  {
    throw "Could not find any VisualStudio with version $version and capabilities: $($requiredPackageIds -join ", ")"
  }
  Write-Verbose "VSInstallPath is : $vsInstallPath"
  return Resolve-Path -path $vsInstallPath
}

function Locate-Item([string] $relativePath) {
  $rootPath = $env:TF_ROOT_DIR
  $itemPath = Join-Path -path $rootPath -childPath $relativePath
  return Resolve-Path -path $itemPath
}

function Get-LogsPath {
  $artifacts = Join-Path -path $TF_OUT_DIR -childPath "logs"

  if (-not (Test-Path $artifacts)) {
    New-Item -Type Directory -Path $artifacts | Out-Null
  }

  return $artifacts
}

function Get-VSTestPath
{
    $TestPlatformVersion = Get-PackageVersion -PackageName "MicrosoftNETTestSdkVersion"
    $vstestPath = Join-Path -path (Locate-PackagesPath) "Microsoft.TestPlatform\$TestPlatformVersion\tools\net451\Common7\IDE\Extensions\TestPlatform\vstest.console.exe"

    return Resolve-Path -path $vstestPath
}

function Start-Timer {
  return [System.Diagnostics.Stopwatch]::StartNew()
}

function Get-ElapsedTime([System.Diagnostics.Stopwatch] $timer) {
  $timer.Stop()
  return $timer.Elapsed
}

function Write-Log ([string] $message, $messageColor = "Green") {
  $currentColor = $Host.UI.RawUI.ForegroundColor
  $Host.UI.RawUI.ForegroundColor = $messageColor
  if ($message) {
    Write-Output "... $message"
  }
  $Host.UI.RawUI.ForegroundColor = $currentColor
}

function Replace-InFile($File, $RegEx, $ReplaceWith) {
  $content = Get-Content -Raw -Encoding utf8 $File
  $newContent = ($content -replace $RegEx, $ReplaceWith)
  if (-not $content.Equals($newContent)) {
    Write-Log "Updating TestPlatform version in $File"
    $newContent | Set-Content -Encoding utf8 $File -NoNewline
  }
}

function Sync-PackageVersions {
  $versionsRegex = '(?mi)<(MicrosoftNETTestSdkVersion.*?)>(.*?)<\/MicrosoftNETTestSdkVersion>'
  $packageRegex = '(?mi)<package id="Microsoft\.TestPlatform([0-9a-z.]+)?" version="([0-9a-z.-]*)"'
  $sourceRegex = '(?mi)(.+[a-z =]+\@?\")Microsoft\.TestPlatform\\([0-9.-a-z]+)\";'

  if ([String]::IsNullOrWhiteSpace($TestPlatformVersion)) {
    $TestPlatformVersion = Get-PackageVersion -PackageName "MicrosoftNETTestSdkVersion"
  }
  else {
    Replace-InFile -File $TF_VERSIONS_FILE -RegEx $versionsRegex -ReplaceWith "<`$1>$TestPlatformVersion</MicrosoftNETTestSdkVersion>"
  }

  (Get-ChildItem "$PSScriptRoot\..\src\*packages.config", "$PSScriptRoot\..\test\*packages.config" -Recurse) | ForEach-Object {
    Replace-InFile -File $_ -RegEx $packageRegex -ReplaceWith ('<package id="Microsoft.TestPlatform$1" version="{0}"' -f $TestPlatformVersion)
  }

  Replace-InFile -File "$PSScriptRoot\..\test\E2ETests\Automation.CLI\CLITestBase.common.cs" -RegEx $sourceRegex -ReplaceWith ('$1Microsoft.TestPlatform\{0}";' -f $TestPlatformVersion)
}

function Install-DotNetCli {
  Write-Log "Install-DotNetCli: Get dotnet-install.ps1 script..."
  $dotnetInstallRemoteScript = "https://raw.githubusercontent.com/dotnet/cli/master/scripts/obtain/dotnet-install.ps1"
  $dotnetInstallScript = Join-Path $env:TF_TOOLS_DIR "dotnet-install.ps1"
  if (-not (Test-Path $env:TF_TOOLS_DIR)) {
    New-Item $env:TF_TOOLS_DIR -Type Directory | Out-Null
  }

  $dotnet_dir = Join-Path $env:TF_TOOLS_DIR "dotnet"

  if (-not (Test-Path $dotnet_dir)) {
    New-Item $dotnet_dir -Type Directory | Out-Null
  }

  (New-Object System.Net.WebClient).DownloadFile($dotnetInstallRemoteScript, $dotnetInstallScript)

  if (-not (Test-Path $dotnetInstallScript)) {
    Write-Error "Failed to download dotnet install script."
  }

  Unblock-File $dotnetInstallScript

  Write-Log "Install-DotNetCli: Get the latest dotnet cli toolset..."
  $dotnetInstallPath = Join-Path $env:TF_TOOLS_DIR "dotnet"
  New-Item -ItemType directory -Path $dotnetInstallPath -Force | Out-Null
  & $dotnetInstallScript -Channel "master" -InstallDir $dotnetInstallPath -Version $env:DOTNET_CLI_VERSION

  & $dotnetInstallScript -InstallDir "$dotnetInstallPath" -Runtime 'dotnet' -Version '2.1.30' -Channel '2.1.30' -Architecture x64 -NoPath
  $env:DOTNET_ROOT = $dotnetInstallPath

  & $dotnetInstallScript -InstallDir "${dotnetInstallPath}_x86" -Runtime 'dotnet' -Version '2.1.30' -Channel '2.1.30' -Architecture x86 -NoPath
  ${env:DOTNET_ROOT(x86)} = "${dotnetInstallPath}_x86"

  & $dotnetInstallScript -InstallDir "$dotnetInstallPath" -Runtime 'dotnet' -Version '3.1.24' -Channel '3.1.24' -Architecture x64 -NoPath
  $env:DOTNET_ROOT = $dotnetInstallPath

  & $dotnetInstallScript -InstallDir "${dotnetInstallPath}_x86" -Runtime 'dotnet' -Version '3.1.24' -Channel '3.1.24' -Architecture x86 -NoPath
  ${env:DOTNET_ROOT(x86)} = "${dotnetInstallPath}_x86"

  & $dotnetInstallScript -InstallDir "$dotnetInstallPath" -Runtime 'dotnet' -Version '5.0.16' -Channel '5.0.16' -Architecture x64 -NoPath
  $env:DOTNET_ROOT = $dotnetInstallPath

  & $dotnetInstallScript -InstallDir "${dotnetInstallPath}_x86" -Runtime 'dotnet' -Version '5.0.16' -Channel '5.0.16' -Architecture x86 -NoPath
  ${env:DOTNET_ROOT(x86)} = "${dotnetInstallPath}_x86"

  $env:DOTNET_MULTILEVEL_LOOKUP = 0

  "---- dotnet environment variables"
  Get-ChildItem "Env:\dotnet_*"

  "`n`n---- x64 dotnet"
  & "$env:DOTNET_ROOT\dotnet.exe" --info

  "`n`n---- x86 dotnet"
  # avoid erroring out because we don't have the sdk for x86 that global.json requires
  try {
    & "${env:DOTNET_ROOT(x86)}\dotnet.exe" --info 2> $null
  }
  catch {}
  Write-Log "Install-DotNetCli: Complete."
}

function Unzip
{
    param([string]$zipfile, [string]$outpath)

    [System.IO.Compression.ZipFile]::ExtractToDirectory($zipfile, $outpath)
}
