#!/usr/bin/env pwsh
# Runs a build using the BuildXL-backed MSBuildCache project-cache plugin.
#
# The plugin requires the x64 flavor of *desktop* MSBuild.exe with -reportFileAccesses; the .NET (Core)
# MSBuild used by `dotnet build` does not support that switch. It also only engages when the actual
# project/solution graph is the build entry point -- it does NOT engage when the build is driven through
# Arcade's Build.proj (which builds projects via a nested MSBuild task). This script therefore invokes the
# x64 desktop MSBuild.exe directly on the given project/solution graph. See docs/build-caching.md.
#
# MSBuildCache only produces correct results on a CLEAN tree (it does not support incremental dev builds),
# so start from a fresh checkout or run `git clean -xdf` first. In CI the tree is already clean.
#
# The repo-pinned .NET SDK must already be installed (run an Arcade restore first, e.g. build.cmd -restore
# or eng\common\CIBuild.cmd ... -restore /p:Build=false), so that the Arcade MSBuild SDK resolves.
[CmdletBinding(PositionalBinding = $false)]
param(
    # Project, solution, or solution filter to build. Defaults to the full solution.
    [string] $Project = "$PSScriptRoot\..\TestFx.slnx",
    [string][Alias('c')] $Configuration = 'Release',
    # Semicolon-separated MSBuild targets to run.
    [string] $Targets = 'Build',
    # BuildXL cache universe; overriding (or bumping the value in eng/projectcaching.props) invalidates the cache.
    [string] $CacheUniverse = '',
    # Skip NuGet restore (use when a previous Arcade restore already populated project.assets.json).
    [switch] $NoRestore,
    # Path to write an MSBuild binary log to (optional).
    [string] $BinaryLog = '',
    # Extra MSBuild properties as "Name=Value" strings (each becomes -p:Name=Value).
    [string[]] $ExtraProperties = @(),
    # Extra args passed straight through to MSBuild.exe (e.g. -bl:..., -p:Foo=Bar).
    [Parameter(ValueFromRemainingArguments = $true)][string[]] $MSBuildArgs
)

$ErrorActionPreference = 'Stop'
$repoRoot = (Resolve-Path "$PSScriptRoot\..").Path

# Ensure the repo-pinned .NET SDK is first on PATH so desktop MSBuild resolves it (and the Arcade SDK).
$dotnetRoot = if ($env:DOTNET_ROOT) { $env:DOTNET_ROOT }
    elseif (Test-Path (Join-Path $repoRoot '.dotnet')) { Join-Path $repoRoot '.dotnet' }
    else { $null }
if ($dotnetRoot) {
    $env:DOTNET_ROOT = $dotnetRoot
    $env:DOTNET_MULTILEVEL_LOOKUP = '0'
    $env:PATH = "$dotnetRoot;$env:PATH"
}

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
    "-t:$Targets",
    "-p:Configuration=$Configuration",
    '-p:MSBuildCacheEnabled=true',
    "-p:MSBuildCacheLogDirectory=$logDir"
)
if (-not $NoRestore) { $argsList += '-restore' }
if ($CacheUniverse) { $argsList += "-p:MSBuildCacheCacheUniverse=$CacheUniverse" }
if ($BinaryLog) { $argsList += "-bl:$BinaryLog" }
foreach ($p in $ExtraProperties) { if ($p) { $argsList += "-p:$p" } }
if ($MSBuildArgs) { $argsList += $MSBuildArgs }

Write-Host "& `"$msbuild`" $($argsList -join ' ')"
& $msbuild @argsList
exit $LASTEXITCODE
