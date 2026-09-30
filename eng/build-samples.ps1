#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Builds all sample projects in the samples/public folder.

.DESCRIPTION
    This script iterates through all solution files in samples/public and builds them.
    It can be used both locally by developers and in CI pipelines.

.PARAMETER Configuration
    The build configuration to use (default: Release).

.PARAMETER TreatWarningsAsErrors
    Whether to treat warnings as errors (default: false).

.PARAMETER BinaryLogDirectory
    Optional directory path where binary log files will be created for each solution.

.PARAMETER LocalPackageDirectory
    Optional directory containing locally packed TestFX packages. When provided, the sample
    MSTest.Sdk global.json pins are temporarily replaced with LocalMSTestVersion and a temporary
    NuGet.config exposes this directory.

.PARAMETER LocalMSTestVersion
    MSTest package version available in LocalPackageDirectory.

.PARAMETER LocalMTPVersion
    Microsoft.Testing.Platform package version available in LocalPackageDirectory.

.PARAMETER PublishedPackagesOnly
    Build only samples whose checked-in MSTest.Sdk versions are expected to resolve from the
    configured public package feeds.

.EXAMPLE
    .\eng\build-samples.ps1
    Builds all samples in Release configuration.

.EXAMPLE
    .\eng\build-samples.ps1 -Configuration Debug
    Builds all samples in Debug configuration.

.EXAMPLE
    .\eng\build-samples.ps1 -Configuration Release -BinaryLogDirectory "artifacts\log\Release"
    Builds all samples with binary logs for each solution.
#>

[CmdletBinding()]
param(
    [string]$Configuration = "Release",
    [switch]$TreatWarningsAsErrors,
    [string]$BinaryLogDirectory,
    [string]$LocalPackageDirectory,
    [string]$LocalMSTestVersion,
    [string]$LocalMTPVersion,
    [switch]$PublishedPackagesOnly
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$repoRootWithTrailingSeparator = $repoRoot + [System.IO.Path]::DirectorySeparatorChar
$samplesFolder = "$repoRoot/samples/public"
$globalJsonBackups = @{}
$nuGetConfigBackups = @{}
$nuGetConfigPaths = @(
    (Join-Path $repoRoot "NuGet.config"),
    (Join-Path $samplesFolder "NuGet.config")
)
$publishedPackageSampleNames = @(
    "ClassicUwpMtpApp",
    "MTPHostIntegration",
    "UwpMtpApp",
    "WinUIMtpAppContainerApp",
    "WinUIMtpPackagedApp",
    "WinUIMtpUnpackagedApp"
)
$localPackageSampleNames = $publishedPackageSampleNames
$localPackageProperties = @()
$localHostingVersion = $null
$localRestorePackagesPath = $null
$dotnetPath = $null

function Remove-LocalRestorePackages {
    if ($null -ne $dotnetPath) {
        & $dotnetPath build-server shutdown
    }

    if ($null -ne $localRestorePackagesPath -and (Test-Path $localRestorePackagesPath)) {
        Remove-Item -LiteralPath $localRestorePackagesPath -Recurse -Force
    }
}

if ($BinaryLogDirectory) {
    New-Item -ItemType Directory -Path $BinaryLogDirectory -Force | Out-Null
}

$failed = $false
$successCount = 0
$failureCount = 0
$solutions = @()

try {
    $localPackageArguments = @($LocalPackageDirectory, $LocalMSTestVersion, $LocalMTPVersion)
    $configuredLocalPackageArguments = @($localPackageArguments | Where-Object { -not [string]::IsNullOrWhiteSpace($_) })
    if ($configuredLocalPackageArguments.Count -ne 0 -and $configuredLocalPackageArguments.Count -ne $localPackageArguments.Count) {
        throw "LocalPackageDirectory, LocalMSTestVersion, and LocalMTPVersion must be provided together."
    }

    if ($PublishedPackagesOnly -and $configuredLocalPackageArguments.Count -ne 0) {
        throw "PublishedPackagesOnly cannot be combined with local package arguments."
    }

    # Source the arcade tools to get access to InitializeDotNetCli
    . "$PSScriptRoot/common/tools.ps1"

    # Initialize .NET CLI before configuring local packages so any server that cached the
    # published-feed NuGet configuration can be shut down before resolving the local SDK.
    $dotnetRoot = InitializeDotNetCli -install:$true
    $dotnetPath = "$dotnetRoot/dotnet.exe"

    if ($configuredLocalPackageArguments.Count -ne 0) {
        $LocalPackageDirectory = (Resolve-Path $LocalPackageDirectory).Path
        $localRestorePackagesPath = Join-Path $repoRoot "artifacts/tmp/sample-packages-local"
        Remove-LocalRestorePackages

        $hostingPackage = Get-ChildItem -Path $LocalPackageDirectory -Filter "Microsoft.Testing.Extensions.Hosting.*.nupkg" | Select-Object -First 1
        if ($null -eq $hostingPackage) {
            throw "Microsoft.Testing.Extensions.Hosting package was not found in LocalPackageDirectory."
        }

        $localHostingVersion = $hostingPackage.BaseName.Substring("Microsoft.Testing.Extensions.Hosting.".Length)

        foreach ($sampleName in $localPackageSampleNames) {
            $globalJsonPath = Get-Item (Join-Path $samplesFolder "$sampleName/global.json")
            $globalJson = Get-Content -Raw -Path $globalJsonPath.FullName | ConvertFrom-Json
            $msbuildSdksProperty = $globalJson.PSObject.Properties["msbuild-sdks"]
            if ($null -eq $msbuildSdksProperty) {
                continue
            }

            $mstestSdkProperty = $msbuildSdksProperty.Value.PSObject.Properties["MSTest.Sdk"]
            if ($null -eq $mstestSdkProperty) {
                continue
            }

            $globalJsonBackups[$globalJsonPath.FullName] = [System.IO.File]::ReadAllBytes($globalJsonPath.FullName)
            $mstestSdkProperty.Value = $LocalMSTestVersion
            $globalJson | ConvertTo-Json -Depth 20 | Set-Content -Path $globalJsonPath.FullName
        }

        foreach ($nuGetConfigPath in $nuGetConfigPaths) {
            $nuGetConfigBackups[$nuGetConfigPath] = [System.IO.File]::ReadAllBytes($nuGetConfigPath)
            [xml]$nuGetConfig = Get-Content -Raw -Path $nuGetConfigPath
            $localSource = $nuGetConfig.CreateElement("add")
            $localSource.SetAttribute("key", "local-testfx")
            $localSource.SetAttribute("value", $LocalPackageDirectory)
            [void]$nuGetConfig.configuration.packageSources.AppendChild($localSource)

            $packageSourceMapping = $nuGetConfig.SelectSingleNode("/configuration/packageSourceMapping")
            if ($null -ne $packageSourceMapping) {
                $localSourceMapping = $nuGetConfig.CreateElement("packageSource")
                $localSourceMapping.SetAttribute("key", "local-testfx")
                $localPackagePatterns = @("MSTest", "MSTest.*", "Microsoft.Testing.*")
                foreach ($patternValue in $localPackagePatterns) {
                    $pattern = $nuGetConfig.CreateElement("package")
                    $pattern.SetAttribute("pattern", $patternValue)
                    [void]$localSourceMapping.AppendChild($pattern)
                }

                [void]$packageSourceMapping.AppendChild($localSourceMapping)

                # Package source mapping selects only the sources with the most specific matching
                # pattern. Add the same specific patterns to public feeds so released and preview
                # package versions remain available alongside the local CI build.
                foreach ($publicSource in @("dotnet-public", "test-tools")) {
                    $publicSourceMapping = $packageSourceMapping.SelectSingleNode("packageSource[@key='$publicSource']")
                    if ($null -eq $publicSourceMapping) {
                        continue
                    }

                    foreach ($patternValue in $localPackagePatterns) {
                        $pattern = $nuGetConfig.CreateElement("package")
                        $pattern.SetAttribute("pattern", $patternValue)
                        [void]$publicSourceMapping.AppendChild($pattern)
                    }
                }
            }

            $nuGetConfig.Save($nuGetConfigPath)
        }

        $localPackageProperties = @(
            "/p:MSTestVersion=$LocalMSTestVersion",
            "/p:MTPVersion=$LocalMTPVersion",
            "/p:MicrosoftTestingPlatformVersion=$LocalMTPVersion",
            "/p:MicrosoftTestingExtensionsHostingVersion=$localHostingVersion",
            "/p:RestorePackagesPath=$localRestorePackagesPath",
            "/p:EnableMicrosoftTestingPlatform=true",
            "/p:EnableMicrosoftTestingExtensionsCodeCoverage=false"
        )
    }

    Write-Host "Building samples in: $samplesFolder"
    Write-Host "Configuration: $Configuration"
    Write-Host ""

    # Find all solution files in samples/public
    $solutions = Get-ChildItem -Path $samplesFolder -Include @("*.sln", "*.slnx") -Recurse
    if ($PublishedPackagesOnly) {
        $solutions = @($solutions | Where-Object { $publishedPackageSampleNames -contains $_.Directory.Name })
    }

    foreach ($solution in $solutions) {
        Write-Host "Building solution: $($solution.FullName)"

        $usesLocalPackages = $localPackageSampleNames -contains $solution.Directory.Name
        $solutionPackageProperties = if ($usesLocalPackages) {
            $localPackageProperties
        }
        else {
            @()
        }

        # UWP projects require MSBuild instead of dotnet build
        $isUwpSolution = $solution.Name -in @("UwpVSTestApp.sln", "UwpMtpApp.sln", "ClassicUwpMtpApp.sln")

        if ($isUwpSolution) {
            # Restore NuGet packages first for UWP projects
            $restoreArgs = @(
                "restore",
                $solution.FullName,
                "/p:Configuration=$Configuration",
                "/p:RepoRoot=$repoRootWithTrailingSeparator",
                "/p:Platform=x64"
            ) + $solutionPackageProperties

            if ($BinaryLogDirectory) {
                $solutionName = [System.IO.Path]::GetFileNameWithoutExtension($solution.Name)
                $restoreBinlogPath = Join-Path $BinaryLogDirectory "$solutionName.restore.binlog"
                $restoreArgs += "/bl:$restoreBinlogPath"
            }

            & $dotnetPath $restoreArgs

            if ($LASTEXITCODE -ne 0) {
                Write-Host "ERROR: Failed to restore packages for $($solution.Name)"
                $failed = $true
                $failureCount++
                continue
            }

            $msbuildPath = InitializeVisualStudioMSBuild -install:$true

            $buildArgs = @(
                $solution.FullName,
                "/p:Configuration=$Configuration",
                "/p:RepoRoot=$repoRootWithTrailingSeparator",
                "/p:TreatWarningsAsErrors=$TreatWarningsAsErrors",
                "/p:Platform=x64",
                "/v:minimal"
            ) + $solutionPackageProperties

            if ($BinaryLogDirectory) {
                $solutionName = [System.IO.Path]::GetFileNameWithoutExtension($solution.Name)
                $binlogPath = Join-Path $BinaryLogDirectory "$solutionName.binlog"
                $buildArgs += "/bl:$binlogPath"
            }

            & $msbuildPath $buildArgs
        }
        else {
            $buildArgs = @(
                "build",
                $solution.FullName,
                "--configuration", $Configuration,
                "/p:TreatWarningsAsErrors=$TreatWarningsAsErrors"
            ) + $solutionPackageProperties

            if ($usesLocalPackages) {
                $buildArgs += "/p:Platform=x64"
            }

            if ($BinaryLogDirectory) {
                $solutionName = [System.IO.Path]::GetFileNameWithoutExtension($solution.Name)
                $binlogPath = Join-Path $BinaryLogDirectory "$solutionName.binlog"
                $buildArgs += "-bl:$binlogPath"
            }

            & $dotnetPath $buildArgs
        }

        if ($LASTEXITCODE -ne 0) {
            Write-Host "ERROR: Failed to build $($solution.Name)"
            $failed = $true
            $failureCount++
        }
        else {
            Write-Host "SUCCESS: Built $($solution.Name)"
            $successCount++
        }

        Write-Host ""
    }
}
finally {
    foreach ($globalJsonPath in $globalJsonBackups.Keys) {
        [System.IO.File]::WriteAllBytes($globalJsonPath, $globalJsonBackups[$globalJsonPath])
    }

    foreach ($nuGetConfigPath in $nuGetConfigBackups.Keys) {
        [System.IO.File]::WriteAllBytes($nuGetConfigPath, $nuGetConfigBackups[$nuGetConfigPath])
    }

    Remove-LocalRestorePackages
}

Write-Host "========================================"
Write-Host "Build Summary:"
Write-Host "  Total solutions: $($solutions.Count)"
Write-Host "  Succeeded: $successCount"
Write-Host "  Failed: $failureCount"
Write-Host "========================================"

if ($failed) {
    Write-Host "One or more samples failed to build"
    exit 1
}

Write-Host "All samples built successfully!"
exit 0
