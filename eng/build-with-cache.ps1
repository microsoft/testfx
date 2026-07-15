#!/usr/bin/env pwsh
# Runs a cached build using the BuildXL-backed MSBuildCache project-cache plugin.
#
# The plugin requires the x64 flavor of *desktop* MSBuild.exe with -reportFileAccesses; the .NET (Core)
# MSBuild used by `dotnet build` does not support that switch. This script locates the x64 desktop
# MSBuild.exe (VS 17.9+), points it at the repo-pinned .NET SDK, and runs a static-graph build with caching
# enabled. See docs/build-caching.md for details.
#
# MSBuildCache only produces correct results on a CLEAN tree (it does not support incremental dev builds),
# so start from a fresh checkout or run `git clean -xdf` first.
[CmdletBinding(PositionalBinding = $false)]
param(
    # Project, solution, or solution filter to build. Defaults to the full solution.
    [string] $Project = "$PSScriptRoot\..\TestFx.slnx",
    [string][Alias('c')] $Configuration = 'Release',
    # BuildXL cache universe; bump (or override) to invalidate all cache entries.
    [string] $CacheUniverse = '',
    [switch] $NoRestore,
    # Extra args passed straight through to MSBuild.exe.
    [Parameter(ValueFromRemainingArguments = $true)][string[]] $MSBuildArgs
)

$ErrorActionPreference = 'Stop'
$repoRoot = Resolve-Path "$PSScriptRoot\.."

# Ensure the repo-pinned .NET SDK (.dotnet) is installed and first on PATH so desktop MSBuild resolves it.
. "$PSScriptRoot\common\tools.ps1"
$dotnetRoot = InitializeDotNetCli -install:$true
$env:DOTNET_ROOT = $dotnetRoot
$env:DOTNET_MULTILEVEL_LOOKUP = '0'
$env:PATH = "$dotnetRoot;$env:PATH"

# Locate the x64 desktop MSBuild.exe (Bin\amd64\MSBuild.exe). -reportFileAccesses is x64-only.
$vswhere = "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe"
if (-not (Test-Path $vswhere)) {
    throw "vswhere.exe not found. Visual Studio 17.9+ (with the MSBuild component) is required for cached builds."
}
$msbuild = & $vswhere -latest -prerelease -products * -requires Microsoft.Component.MSBuild -find MSBuild\**\Bin\amd64\MSBuild.exe |
    Select-Object -First 1
if (-not $msbuild) {
    throw "Could not find x64 desktop MSBuild.exe (Bin\amd64\MSBuild.exe). Install/repair Visual Studio 17.9+."
}
Write-Host "Using MSBuild: $msbuild"

$logDir = Join-Path $repoRoot "artifacts\log\$Configuration\MSBuildCache"

$argsList = @(
    (Resolve-Path $Project).Path,
    '-graph', '-m', '-reportFileAccesses',
    "-p:Configuration=$Configuration",
    '-p:MSBuildCacheEnabled=true',
    "-p:MSBuildCacheLogDirectory=$logDir"
)
if (-not $NoRestore) { $argsList += '-restore' }
if ($CacheUniverse) { $argsList += "-p:MSBuildCacheCacheUniverse=$CacheUniverse" }
if ($MSBuildArgs) { $argsList += $MSBuildArgs }

Write-Host "& `"$msbuild`" $($argsList -join ' ')"
& $msbuild @argsList
exit $LASTEXITCODE
