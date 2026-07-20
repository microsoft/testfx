#!/usr/bin/env pwsh

[CmdletBinding()]
param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Debug",

    [ValidateSet("all", "mstest", "testing-platform")]
    [string]$ProductsToBuild = "all",

    [string]$CacheUniverse,

    [switch]$Clean
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot

$artifactsDirectory = Join-Path $repoRoot "artifacts"
$generatedSourceFiles = @(
    (Join-Path $repoRoot "src\Package\MSTest.Sdk\Sdk\Sdk.props")
    (Join-Path $repoRoot "src\Package\MSTest.Sdk\Sdk\Runner\Runner.targets")
)

function Clear-BuildOutputs {
    if (Test-Path $artifactsDirectory) {
        Get-ChildItem $artifactsDirectory -Force |
            Where-Object Name -NotIn @("log", "msbuild-cache") |
            Remove-Item -Recurse -Force
    }

    $generatedSourceFiles |
        Where-Object { Test-Path $_ } |
        Remove-Item -Force
}

if ($Clean) {
    Clear-BuildOutputs
}

$staleBuildOutputs = @(
    $generatedSourceFiles
    if (Test-Path $artifactsDirectory) {
        Get-ChildItem $artifactsDirectory -Force |
            Where-Object Name -NotIn @("log", "msbuild-cache") |
            Select-Object -ExpandProperty FullName
    }
) | Where-Object { Test-Path $_ }

if ($staleBuildOutputs) {
    throw "MSBuildCache requires a clean build. Remove existing build outputs or rerun with -Clean."
}

$gitStatus = & git -C $repoRoot status --porcelain --untracked-files=all
if ($LASTEXITCODE -ne 0) {
    throw "Unable to verify that the repository is clean."
}

if ($gitStatus) {
    throw "MSBuildCache requires a clean repository. Commit or stash changes and remove untracked files before retrying."
}

. "$PSScriptRoot/common/tools.ps1"

if (-not (IsWindowsPlatform)) {
    throw "MSBuildCache requires Windows because MSBuild file access reporting is Windows-only."
}

Create-Directory $ToolsetDir
$dotnetRoot = InitializeDotNetCli -install:$true -createSdkLocationFile:$true
$env:DOTNET_ROOT = $dotnetRoot
$env:DOTNET_MSBUILD_SDK_RESOLVER_CLI_DIR = $dotnetRoot
$env:DOTNET_MULTILEVEL_LOOKUP = 0
$env:PATH = "$dotnetRoot;$env:PATH"

$msbuildPath = InitializeVisualStudioMSBuild
$solution = switch ($ProductsToBuild) {
    "all" { Join-Path $repoRoot "TestFx.slnx" }
    "mstest" { Join-Path $repoRoot "MSTest.slnf" }
    "testing-platform" { Join-Path $repoRoot "Microsoft.Testing.Platform.slnf" }
}

$logDirectory = Join-Path $artifactsDirectory "log\$Configuration\MSBuildCache"
$publishedPluginLogDirectory = Join-Path $logDirectory "Plugin"
$pluginLogDirectory = Join-Path ([System.IO.Path]::GetTempPath()) "testfx-msbuildcache-$PID"

if (Test-Path $publishedPluginLogDirectory) {
    Remove-Item $publishedPluginLogDirectory -Recurse -Force
}

Create-Directory $publishedPluginLogDirectory
Create-Directory $pluginLogDirectory

$arguments = @(
    $solution
    "/restore"
    "/graph"
    "/m"
    "/reportfileaccesses"
    "/nr:false"
    "/t:Build"
    "/warnaserror"
    "/v:minimal"
    "/p:Configuration=$Configuration"
    "/p:RepoRoot=$repoRoot\"
    "/p:MSBuildCachePackageEnabled=true"
    "/p:MSBuildCacheEnabled=true"
    "/p:MSBuildCacheLogDirectory=$pluginLogDirectory"
    "/bl:$(Join-Path $logDirectory 'Build.binlog')"
)

if ($CacheUniverse) {
    $arguments += "/p:MSBuildCacheCacheUniverse=$CacheUniverse"
}

Write-Host "Building $solution with MSBuildCache using $msbuildPath"
$exitCode = 1
try {
    & $msbuildPath @arguments
    $exitCode = $LASTEXITCODE
}
finally {
    if (Test-Path $pluginLogDirectory) {
        try {
            Get-ChildItem $pluginLogDirectory -Force |
                Where-Object Name -NE "CacheClient.log" |
                Copy-Item -Destination $publishedPluginLogDirectory -Recurse -Force
        }
        catch {
            Write-Warning "Failed to publish MSBuildCache plugin diagnostics: $($_.Exception.Message)"
        }
        finally {
            Remove-Item $pluginLogDirectory -Recurse -Force -ErrorAction SilentlyContinue
        }
    }

    if ($exitCode -ne 0) {
        Clear-BuildOutputs
    }
}

if ($exitCode -ne 0) {
    throw "MSBuildCache build failed with exit code $exitCode."
}
