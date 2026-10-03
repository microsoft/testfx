#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Executes every runnable public sample and verifies every project is classified.

.DESCRIPTION
    Samples with deterministic, self-contained tests are run normally. Samples that require an
    external service, browser, interactive desktop, or are intended as scale benchmarks are started
    with --help to verify their executable and extension composition without exercising that
    external dependency. Library and application-under-test projects are explicitly classified as
    dependencies so newly added projects cannot silently escape CI validation.

.PARAMETER Configuration
    The build configuration to use (default: Release).

.PARAMETER BinaryLogDirectory
    Optional directory path where binary log files will be created for each invocation.

.PARAMETER Include
    Optional wildcard patterns matched against project paths. Useful for focused local validation.
#>

[CmdletBinding()]
param(
    [string]$Configuration = "Release",
    [string]$BinaryLogDirectory,
    [string[]]$Include = @("*")
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$samplesFolder = Join-Path $repoRoot "samples/public"

. "$PSScriptRoot/samples-tools.ps1"

. "$PSScriptRoot/common/tools.ps1"

$dotnetRoot = InitializeDotNetCli -install:$true
$dotnetPath = Join-Path $dotnetRoot "dotnet.exe"
$env:DOTNET_ROLL_FORWARD = "Major"

if ($BinaryLogDirectory) {
    New-Item -ItemType Directory -Path $BinaryLogDirectory -Force | Out-Null
}

$dependencyProjects = @(
    "MTPHostIntegration/AspireApi/AspireApi.csproj",
    "MTPHostIntegration/AspireAppHost/AspireAppHost.csproj",
    "MTPHostIntegration/ServiceDefaults/ServiceDefaults.csproj",
    "mstest-runner/EnsureTestFramework/EnsureTestFramework/EnsureTestFramework.csproj",
    "mstest-runner/NativeAotRunner/ClassLibrary1/ClassLibrary1.csproj",
    "mstest-runner/RunInDocker/MyServer/MyServer.csproj"
)

$validations = @(
    @{ Project = "MTPOTel/MTPOTel.csproj"; Mode = "Run" },
    @{ Project = "ReliableTestSuite/ReliableTestSuite.csproj"; Mode = "Run" },
    @{ Project = "TestingPlatformExamples/TestingPlatformExplorer/TestingPlatformExplorer.csproj"; Mode = "Smoke" },

    @{ Project = "DemoMSTestSdk/ProjectUsingAspire/ProjectUsingAspire.csproj"; Mode = "Smoke" },
    @{ Project = "DemoMSTestSdk/ProjectUsingMSTestRunner/ProjectUsingMSTestRunner.csproj"; Mode = "Run" },
    @{ Project = "DemoMSTestSdk/ProjectUsingPlaywright/ProjectUsingPlaywright.csproj"; Mode = "Smoke" },
    @{ Project = "DemoMSTestSdk/ProjectUsingVSTest/ProjectUsingVSTest.csproj"; Mode = "VSTest" },
    @{ Project = "DemoMSTestSdk/ProjectUsingWindowsUIAutomation/ProjectUsingWindowsUIAutomation.csproj"; Mode = "Smoke" },
    @{ Project = "DemoMSTestSdk/ProjectWithNativeAOT/ProjectWithNativeAOT.csproj"; Mode = "Run" },

    @{ Project = "MTPHostIntegration/AspNetCoreTests/AspNetCoreTests.csproj"; Mode = "Run" },
    @{ Project = "MTPHostIntegration/AspireTests/AspireTests.csproj"; Mode = "Run" },

    @{ Project = "mstest-runner/CustomReportExtension/CustomReportExtension/CustomReportExtension.csproj"; Mode = "Smoke" },
    @{ Project = "mstest-runner/EnsureTestFramework/MyTestProject/MyTestProject.csproj"; Mode = "Smoke" },
    @{ Project = "mstest-runner/MSTestProjectWithExplicitMain/MSTestProjectWithExplicitMain/MSTestProjectWithExplicitMain.csproj"; Mode = "Run" },
    @{ Project = "mstest-runner/NativeAotRunner/TestProject1/TestProject1.csproj"; Mode = "Run" },
    @{ Project = "mstest-runner/RunInDocker/MyServer.Tests/MyServer.Tests.csproj"; Mode = "ExecutableSmoke" },
    @{ Project = "mstest-runner/Simple1/Simple1.csproj"; Mode = "Run" },
    @{ Project = "mstest-runner/runner_vs_vstest/10C_100M/10C100M.csproj"; Mode = "Smoke" },
    @{ Project = "mstest-runner/runner_vs_vstest/100C_100M/100C100M.csproj"; Mode = "Smoke" },
    @{ Project = "mstest-runner/runner_vs_vstest/1000C_100M/1000C100M.csproj"; Mode = "Smoke" },

    @{ Project = "UwpVSTestApp/UwpVSTestApp.csproj"; Mode = "VSTest"; Platform = "x64" },
    @{ Project = "UwpMtpApp/UwpMtpApp.csproj"; Mode = "MtpTest"; Platform = "x64" },
    @{ Project = "UwpMtpApp/UwpMtpApp.csproj"; Mode = "VisualStudioInvoke"; Platform = "x64"; PackageName = "MSTestUwpMtpSample" },
    @{ Project = "ClassicUwpMtpApp/ClassicUwpMtpApp.csproj"; Mode = "VisualStudioInvoke"; Platform = "x64"; PackageName = "MSTestClassicUwpMtpSample" },
    @{ Project = "WinUIVSTestApp/WinUIVSTestApp.csproj"; Mode = "VSTest"; Platform = "x64" },
    @{ Project = "WinUIMtpPackagedApp/WinUIMtpPackagedApp.csproj"; Mode = "MtpTest"; Platform = "x64"; PackageName = "27a818e1-af01-4177-9e34-ad49120c15ed" },
    @{ Project = "WinUIMtpUnpackagedApp/WinUIMtpUnpackagedApp.csproj"; Mode = "MtpTest"; Platform = "x64"; Timeout = "5m"; RetryHandshakeFailure = $true },
    @{ Project = "WinUIMtpAppContainerApp/WinUIMtpAppContainerApp.csproj"; Mode = "Invoke"; Platform = "x64"; PackageName = "MSTestWinUIAppContainerSample" }
)

$classifiedProjects = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::OrdinalIgnoreCase)
foreach ($project in $dependencyProjects) {
    $null = $classifiedProjects.Add($project.Replace("/", [System.IO.Path]::DirectorySeparatorChar))
}

foreach ($validation in $validations) {
    $null = $classifiedProjects.Add($validation.Project.Replace("/", [System.IO.Path]::DirectorySeparatorChar))
}

$unclassifiedProjects = @(Get-ChildItem -Path $samplesFolder -Include @("*.csproj", "*.fsproj", "*.vbproj") -Recurse |
    ForEach-Object { Get-SampleRelativePath -FullPath $_.FullName -SamplesFolder $samplesFolder } |
    Where-Object { !$classifiedProjects.Contains($_) })

if ($unclassifiedProjects.Count -ne 0) {
    throw "The following public sample projects are not classified for execution or dependency-only coverage:`n$($unclassifiedProjects -join [Environment]::NewLine)"
}

function Get-BinlogArgument {
    param(
        [string]$Project,
        [string]$Mode
    )

    if (!$BinaryLogDirectory) {
        return "-bl:{}"
    }

    $logName = "$($Project.Replace('/', '.').Replace('\', '.')).$Mode.binlog"
    return "-bl:$(Join-Path $BinaryLogDirectory $logName)"
}

function Invoke-SampleCommand {
    param(
        [string]$Project,
        [string]$Mode,
        [string]$Platform,
        [string]$PackageName,
        [string]$Timeout,
        [bool]$RetryHandshakeFailure
    )

    $projectPath = Join-Path $samplesFolder $Project
    $binlogArgument = Get-BinlogArgument -Project $Project -Mode $Mode
    $platformArgument = if ($Platform) { "/p:Platform=$Platform" } else { $null }

    Write-Host "Validating sample: $Project ($Mode)"
    Push-Location (Split-Path -Parent $projectPath)

    try {
        if ($PackageName) {
            Get-AppxPackage -Name $PackageName | Remove-AppxPackage
        }

        switch ($Mode) {
            "Run" {
                $executableArguments = @()
            }
            "Smoke" {
                $executableArguments = @("--help")
            }
            "ExecutableSmoke" {
                $executableArguments = @("--help")
            }
            { $_ -in @("Run", "Smoke", "ExecutableSmoke") } {
                $executableName = "$([System.IO.Path]::GetFileNameWithoutExtension($projectPath)).exe"
                $executable = Get-ChildItem -Path (Join-Path (Split-Path -Parent $projectPath) "bin\$Configuration") -Filter $executableName -Recurse |
                    Select-Object -First 1

                if (!$executable) {
                    throw "Could not find the built executable '$executableName' for '$Project'."
                }

                & $executable.FullName $executableArguments
                if ($LASTEXITCODE -ne 0) {
                    throw "Sample validation failed for '$Project' with exit code $LASTEXITCODE."
                }

                Write-Host "SUCCESS: Validated $Project"
                Write-Host ""
                return
            }
            "VSTest" {
                $arguments = @("test", $projectPath, "--configuration", $Configuration, $binlogArgument)
            }
            "MtpTest" {
                $arguments = @("test", "--project", $projectPath, "--configuration", $Configuration, $binlogArgument)
            }
            "Invoke" {
                $arguments = @("msbuild", $projectPath, "-restore", "-t:Build;InvokeTestingPlatform", "-p:Configuration=$Configuration", $binlogArgument)
            }
            "VisualStudioInvoke" {
                $msbuildPath = InitializeVisualStudioMSBuild -install:$true
                $arguments = @(
                    $projectPath,
                    "/restore",
                    "/t:Build;InvokeTestingPlatform",
                    "/p:Configuration=$Configuration",
                    "/p:Platform=$Platform",
                    "/p:EnableMicrosoftTestingExtensionsPackagedApp=true",
                    "/bl:$($binlogArgument.Substring(4))",
                    "/v:minimal"
                )

                & $msbuildPath $arguments
                if ($LASTEXITCODE -ne 0) {
                    throw "Sample validation failed for '$Project' with exit code $LASTEXITCODE."
                }

                Write-Host "SUCCESS: Validated $Project"
                Write-Host ""
                return
            }
            default {
                throw "Unknown sample validation mode '$Mode'."
            }
        }

        if ($platformArgument) {
            $arguments += $platformArgument
        }

        if ($Timeout) {
            $arguments += @("--timeout", $Timeout)
        }

        $maxAttempts = if ($RetryHandshakeFailure) { 2 } else { 1 }
        for ($attempt = 1; $attempt -le $maxAttempts; $attempt++) {
            if (!$RetryHandshakeFailure) {
                & $dotnetPath $arguments
                $exitCode = $LASTEXITCODE
            }
            else {
                $commandOutput = @()
                & $dotnetPath $arguments | Tee-Object -Variable commandOutput
                $exitCode = $LASTEXITCODE
            }

            if ($exitCode -eq 0) {
                break
            }

            $hasHandshakeFailure = $RetryHandshakeFailure -and
                @($commandOutput | Where-Object { $_ -match "Handshake failures:" }).Count -ne 0
            if (!$hasHandshakeFailure -or $attempt -eq $maxAttempts) {
                throw "Sample validation failed for '$Project' with exit code $exitCode."
            }

            Write-Warning "Test host handshake failed for '$Project' on attempt $attempt; retrying once."
            Start-Sleep -Seconds 5
        }

        Write-Host "SUCCESS: Validated $Project"
        Write-Host ""
    }
    finally {
        Pop-Location
    }
}

$selectedValidations = @($validations | Where-Object {
    $project = $_.Project
    $Include | Where-Object { $project -like $_ }
})

foreach ($validation in $selectedValidations) {
    $platform = if ($validation.ContainsKey("Platform")) { $validation.Platform } else { $null }
    $packageName = if ($validation.ContainsKey("PackageName")) { $validation.PackageName } else { $null }
    $timeout = if ($validation.ContainsKey("Timeout")) { $validation.Timeout } else { $null }
    $retryHandshakeFailure = $validation.ContainsKey("RetryHandshakeFailure") -and $validation.RetryHandshakeFailure
    Invoke-SampleCommand -Project $validation.Project -Mode $validation.Mode -Platform $platform -PackageName $packageName -Timeout $timeout -RetryHandshakeFailure $retryHandshakeFailure
}

Write-Host "Validated $($selectedValidations.Count) public sample invocation(s)."
