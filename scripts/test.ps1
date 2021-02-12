# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT license. See LICENSE file in the project root for full license information.

# Script to run tests for testfx.

[CmdletBinding(PositionalBinding=$false)]
Param(
  [Parameter(Mandatory=$false)]
  [ValidateSet("Debug", "Release")]
  [Alias("c")]
  [string] $Configuration = "Debug",

  [Parameter(Mandatory=$false)]
  [Alias("p")]
  [System.String] $Pattern = "UnitTests",
  
  [Parameter(Mandatory=$false)]
  [Alias("pl")]
  [Switch] $Parallel = $false,

  [Parameter(Mandatory=$false)]
  [Switch] $All = $false,

  [Parameter(Mandatory=$false)]
  [Alias("h")]
  [Switch] $Help = $false
)

. $PSScriptRoot\common.lib.ps1

#
# Script Preferences
#
$ErrorActionPreference = "Stop"

#
# Variables
#
$env:TF_TESTS_OUTDIR_PATTERN = "*.Tests"
$env:TF_UNITTEST_FILES_PATTERN = "*.UnitTests*.dll"
$env:TF_COMPONENTTEST_FILES_PATTERN = "*.ComponentTests*.dll"
$env:TF_E2ETEST_FILES_PATTERN = "*.E2ETests*.dll"
$env:TF_NetCoreContainers =@("MSTestAdapter.PlatformServices.NetCore.UnitTests.dll")
#
# Test configuration
#
Write-Verbose "Setup build configuration."
$TFT_Configuration = $Configuration
$TFT_Pattern = $Pattern
$TFT_Parallel = $Parallel
$TFT_All = $All
$TestFramework = ".NETCoreApp,Version=v2.1"

#
# Prints help text for the switches this script supports.
#
function Print-Help {
  if (-not $Help) {
    return
  }

  Write-Host -object ""
  Write-Host -object "********* MSTest Adapter Test Script *********"
  Write-Host -object ""
  Write-Host -object "  Help (-h)                     - [Switch] - Prints this help message."
  Write-Host -object "  Parallel (-pl)                - [Switch] - Indicates that the tests should be run in parallel."
  Write-Host -object "  All                           - [Switch] - Indicates that all tests should be run. This ignores the pattern provided."
  Write-Host -object ""
  Write-Host -object "  Configuration (-c)            - [String] - Specifies the build configuration. Defaults to 'Debug'."
  Write-Host -object "  Pattern (-p)                  - [String] - Runs tests from the test container that matches this pattern. For instance, specify -p E2E to run all End-to-End tests. Alternatively specifying -p TestFramework.UnitTests runs only tests from Microsoft.VisualStudio.TestPlatform.TestFramework.UnitTests.dll."

  Write-Host -object ""
  Exit 0
}

function Invoke-Test
{
    & dotnet --info

    $timer = Start-Timer
    
    Write-Log "Run-Test: Started."
    
    Write-Log "    Computing Test Containers."
    # Get all the test project folders. They should all be ending with ".Tests"
    $outDir = Join-Path $env:TF_OUT_DIR -ChildPath $TFT_Configuration
    $testFolders = Get-ChildItem $outDir -Directory -Filter $env:TF_TESTS_OUTDIR_PATTERN | %{$_.FullName}

    # Get test assemblies from these folders that match the pattern specified.
    foreach($container in $testFolders)
    {
        $testContainer = Get-ChildItem $container\* -Recurse -Include $env:TF_UNITTEST_FILES_PATTERN, $env:TF_COMPONENTTEST_FILES_PATTERN, $env:TF_E2ETEST_FILES_PATTERN
        
        $testContainerName = $testContainer.Name
        $testContainerPath = $testContainer.FullName
        $allContainers += ,"$testContainerName"
        
        if($TFT_All)
        {
            if($env:TF_NetCoreContainers -Contains $testContainerName)
            {
                $netCoreTestContainers += ,"$testContainerPath" 
            }
            else
            {
                $testContainers += ,"$testContainerPath"
            }
        }
        else 
        {
            if($testContainerPath -match $TFT_Pattern)
            {
                if($env:TF_NetCoreContainers -Contains $testContainerName)
                {
                    $netCoreTestContainers += ,"$testContainerPath" 

                }
                else
                {
                    $testContainers += ,"$testContainerPath"
                }
            }
        }
    }
                        
    if($testContainers.Count -gt 0 -Or $netCoreTestContainers.Count -gt 0)
    {
        $testContainersString = [system.String]::Join(",",$testContainers)
        Write-Log "    Matched Test Containers: $testContainersString."
        Run-Test -testContainers $testContainers -netCoreTestContainers $netCoreTestContainers
    }
    else
    {
        $allContainersString = [system.String]::Join(",",$allContainers)
        Write-Log "    None of the test containers matched the pattern $TFT_Pattern."
        Write-Log "    Test Containers available: $allContainersString."
    }
    
    Write-Log "Run-Test: Complete. {$(Get-ElapsedTime($timer))}"
}

function Run-Test([string[]] $testContainers, [string[]] $netCoreTestContainers)
{	
    $vstestPath = Get-VSTestPath
 
    $additionalArguments = ''
    if($TFT_Parallel)
    {
       $additionalArguments += "/parallel"
    }
    
     if($testContainers.Count -gt 0)
     {
        if(!(Test-Path $vstestPath))
        {
            Write-Error "Unable to find vstest.console.exe at $vstestPath. Test aborted."
        }
    
        Write-Verbose "$vstestPath $testContainers $additionalArguments /logger:trx"
        & $vstestPath $testContainers $additionalArguments /logger:trx

        if ($lastExitCode -ne 0) 
        {
            throw "Tests failed."
        }
     }
    
    if($netCoreTestContainers.Count -gt 0)
    {
        Try
        {
            Write-Verbose "dotnet test $netCoreTestContainers /framework:$TestFramework $additionalArguments /logger:trx"
            & dotnet test $netCoreTestContainers /framework:$TestFramework $additionalArguments /logger:trx
        }

        Catch [System.Management.Automation.CommandNotFoundException]
        {
            Write-Error "Unable to find dotnet.exe. Test aborted."
        }

        if ($lastExitCode -ne 0) 
        {
            throw "Tests failed."
        }
    }
}

function Get-VSTestPath
{
    $versionsFile = "$PSScriptRoot\build\TestFx.Versions.targets"
    $TestPlatformVersion = (([XML](Get-Content $versionsFile)).Project.PropertyGroup.TestPlatformVersion).InnerText

    $vsInstallPath = "$PSScriptRoot\..\packages\Microsoft.TestPlatform.$TestPlatformVersion\"
    $vstestPath = Join-Path -path $vsInstallPath "tools\net451\Common7\IDE\Extensions\TestPlatform\vstest.console.exe"
    return Resolve-Path -path $vstestPath
}

Print-Help
Invoke-Test
