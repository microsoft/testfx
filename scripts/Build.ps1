[CmdletBinding(PositionalBinding=$false)]
Param(
  [switch] $help,
  
  [string] $target = "Build",
  
  [ValidateSet("Debug", "Release")]
  [Alias("c")]
  [string] $configuration = "Debug",
  
  [switch] $clearPackageCache
)

$ErrorActionPreference = "Stop"

. $PSScriptRoot\common.lib.ps1

function Perform-Build {
  Write-Host -object ""

  $msbuild = Locate-MSBuild 

  $solution = Locate-Solution -relativePath "TestFx.sln"
  $templates = Locate-Solution -relativePath "Templates\MSTestTemplates.sln"
  $wizards = Locate-Solution -relativePath "WizardExtensions\WizardExtensions.sln"

  Write-Host -object "Starting solution build..."
  & $msbuild /t:$target /p:Configuration=$configuration /tv:$msbuildVersion /m $solution
  
  Write-Host -object "Starting Templates build..."
  & $msbuild /t:$target /p:Configuration=$configuration /tv:$msbuildVersion /m $templates
  
  Write-Host -object "Starting Wizard Extensions build..."
  & $msbuild /t:$target /p:Configuration=$configuration /tv:$msbuildVersion /m $wizards

  if ($lastExitCode -ne 0) {
    throw "The build failed with an exit code of '$lastExitCode'."
  }

  Write-Host -object "The build completed successfully." -foregroundColor Green
}

function Perform-Restore {
  Write-Host -object ""

  $nuget = Locate-NuGet
  $nugetConfig = Locate-NuGetConfig
  $toolset = Locate-Toolset
  $solution = Locate-Solution -relativePath "TestFx.sln"
  $templates = Locate-Solution -relativePath "Templates\MSTestTemplates.sln"
  $wizards = Locate-Solution -relativePath "WizardExtensions\WizardExtensions.sln"
  
  if ($clearPackageCache) {
    Write-Host -object "Clearing local package cache..."
    & $nuget locals all -clear
  }

  Write-Host -object "Starting toolset restore..."
  & $nuget restore -msbuildVersion $msbuildVersion -verbosity quiet -nonInteractive -configFile $nugetConfig $toolset
  
  if ($lastExitCode -ne 0) {
    throw "The restore failed with an exit code of '$lastExitCode'."
  }

  Write-Host -object "Locating MSBuild install path..."
  $msbuildPath = Locate-MSBuildPath 

  Write-Host -object "Starting solution restore..."
  & $nuget restore -msbuildPath $msbuildPath -verbosity quiet -nonInteractive -configFile $nugetConfig $solution
  & $nuget restore -msbuildPath $msbuildPath -verbosity quiet -nonInteractive -configFile $nugetConfig $templates
  & $nuget restore -msbuildPath $msbuildPath -verbosity quiet -nonInteractive -configFile $nugetConfig $wizards

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
  Write-Host -object "MSTest Adapter Build Script"
  Write-Host -object ""
  Write-Host -object "  Help                          - [Switch] - Prints this help message."
  Write-Host -object "  ClearPackageCache             - [Switch] - Indicates local package cache should be cleared before restore."
  Write-Host -object ""
  Write-Host -object "  Configuration                 - [String] - Specifies the build configuration. Defaults to 'Debug'."
  Write-Host -object "  Target                        - [String] - Specifies the build target. Defaults to 'Build'."
  Write-Host -object ""
  Exit 0
}

Print-Help
Perform-Restore
Perform-Build
