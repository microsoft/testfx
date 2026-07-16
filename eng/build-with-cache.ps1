#!/usr/bin/env pwsh
# Runs a build using the BuildXL-backed MSBuildCache project-cache plugin.
#
# Why this script exists (see docs/build-caching.md for the full story):
#   * The plugin needs the x64 flavor of *desktop* MSBuild.exe with -reportFileAccesses. The .NET (Core)
#     MSBuild used by `dotnet build` does not support that switch, and the 32-bit desktop MSBuild rejects it.
#   * The plugin only engages when the real project/solution graph is the MSBuild entry point. It does NOT
#     engage when the build is driven through Arcade's Build.proj (which builds projects via a nested MSBuild
#     task), so this script invokes MSBuild.exe directly on the given project/solution graph.
#
# It reuses the Arcade helpers in eng/common/tools.ps1 to bootstrap the repo-pinned .NET SDK and to locate
# the Visual Studio / MSBuild that satisfies the requirements declared in global.json (rather than picking an
# arbitrary VS install), then asserts the resolved MSBuild is the required x64 flavor.
#
# MSBuildCache only produces correct results on a CLEAN tree (it does not support incremental dev builds),
# so start from a fresh checkout or run `git clean -xdf` first. In CI the tree is already clean.
[CmdletBinding(PositionalBinding = $false)]
param(
    # Project, solution, or solution filter to build. Defaults to the full solution.
    [string] $Project = "$PSScriptRoot\..\TestFx.slnx",
    [string][Alias('c')] $configuration = 'Release',
    # Semicolon-separated MSBuild targets to run.
    [string] $Targets = 'Build',
    # BuildXL cache universe; overriding (or bumping the value in eng/projectcaching.props) invalidates the cache.
    [string] $CacheUniverse = '',
    # Skip NuGet restore (use when a previous Arcade restore already populated project.assets.json AND the
    # MSBuildCache package -- restore MUST have run with /p:MSBuildCacheEnabled=true, otherwise the plugin
    # will not be imported and the build runs uncached).
    [switch] $NoRestore,
    # Set to true on CI. Mirrors the CI build semantics Arcade's CIBuild.cmd applies
    # (ContinuousIntegrationBuild=true, warnings-as-errors, node reuse disabled).
    [switch] $ci,
    # Path to write an MSBuild binary log to (optional). Named BinLogPath (not BinaryLog) to avoid a
    # case-insensitive collision with the internal $binaryLog variable in eng/common/tools.ps1.
    [string] $BinLogPath = '',
    # Extra MSBuild properties as "Name=Value" strings (each becomes -p:Name=Value).
    [string[]] $ExtraProperties = @(),
    # Extra args passed straight through to MSBuild.exe.
    [Parameter(ValueFromRemainingArguments = $true)][string[]] $MSBuildArgs
)

$ErrorActionPreference = 'Stop'
$repoRoot = (Resolve-Path "$PSScriptRoot\..").Path

# Reuse the Arcade helpers: they bootstrap the repo-pinned SDK and enforce the global.json VS requirements.
. "$PSScriptRoot\common\tools.ps1"

# Bootstrap / locate the repo-pinned .NET SDK (also restores it after a `git clean -xdf`).
$dotnetRoot = InitializeDotNetCli -install:$true -createSdkLocationFile:$true
$env:DOTNET_ROOT = $dotnetRoot
$env:DOTNET_MULTILEVEL_LOOKUP = '0'
# Point desktop MSBuild's .NET SDK resolver at the bootstrapped SDK so MSBuild-SDK-style projects (Arcade)
# resolve, and put it first on PATH.
$env:DOTNET_MSBUILD_SDK_RESOLVER_CLI_DIR = $dotnetRoot
$env:PATH = "$dotnetRoot;$env:PATH"

# Locate the VS/MSBuild that satisfies global.json requirements (throws if none is found), then require the
# x64 flavor because -reportFileAccesses is x64-only.
$msbuild = InitializeVisualStudioMSBuild
if ($msbuild -notmatch '\\amd64\\MSBuild\.exe$') {
    throw "MSBuildCache requires the x64 desktop MSBuild (Bin\amd64\MSBuild.exe), but Arcade resolved '$msbuild'. " +
          "Run this on an x64 host with a matching Visual Studio installed."
}
Write-Host "Using MSBuild: $msbuild"

$logDir = Join-Path $repoRoot "artifacts\log\$configuration\MSBuildCache"

$argsList = @(
    (Resolve-Path $Project).Path,
    '-graph', '-m', '-reportFileAccesses',
    "-t:$Targets",
    "-p:Configuration=$configuration",
    '-p:MSBuildCacheEnabled=true',
    "-p:MSBuildCacheLogDirectory=$logDir"
)
if (-not $NoRestore) { $argsList += '-restore' }
if ($ci) {
    # Mirror the CI semantics that Arcade's CIBuild.cmd / tools.ps1 apply so the cached compile is a real CI
    # build: mark it a CI build, treat warnings as errors, and disable node reuse.
    $argsList += '-p:ContinuousIntegrationBuild=true'
    $argsList += '-warnaserror'
    $argsList += '-nr:false'
}
if ($CacheUniverse) { $argsList += "-p:MSBuildCacheCacheUniverse=$CacheUniverse" }
if ($BinLogPath) { $argsList += "-bl:$BinLogPath" }
foreach ($p in $ExtraProperties) { if ($p) { $argsList += "-p:$p" } }
if ($MSBuildArgs) { $argsList += $MSBuildArgs }

Write-Host "& `"$msbuild`" $($argsList -join ' ')"
& $msbuild @argsList
exit $LASTEXITCODE
