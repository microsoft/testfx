[CmdletBinding(PositionalBinding=$false)]
Param(
  [switch] $help,
  
  [string] $target = "Build",
  
  [ValidateSet("Debug", "Release")]
  [Alias("c")]
  [string] $configuration = "Debug",
  
  [switch] $clearPackageCache,
  [switch] $templates,
  [switch] $wizards,
  [switch] $vsmanprojects,
  [switch] $full
)

$ErrorActionPreference = "Stop"

. $PSScriptRoot\common.lib.ps1

$solutions = @("TestFx.sln","Templates\MSTestTemplates.sln","WizardExtensions\WizardExtensions.sln")
$vsmanprojs =@(
"setup\Templates\Desktop\Microsoft.VisualStudio.Templates.CS.MSTestv2.Desktop.UnitTest.vsmanproj",
"setup\Templates\UWP\Microsoft.VisualStudio.Templates.CS.MSTestv2.UWP.UnitTest.vsmanproj",
"setup\WizardExtensions\MSTestv2IntelliTestExtension\Microsoft.VisualStudio.TestTools.MSTestV2.WizardExtension.IntelliTest.vsmanproj",
"setup\WizardExtensions\MSTestv2UnitTestExtension\Microsoft.VisualStudio.TestTools.MSTestV2.WizardExtension.UnitTest.vsmanproj"
)

function Invoke-Build([string] $solution)
{
    $msbuild = Locate-MSBuild
	$solutionPath = Locate-Solution -relativePath $solution

	Write-Host -object "Starting $solution build..."
	& $msbuild /t:$target /p:Configuration=$configuration /tv:$msbuildVersion /m $solutionPath
  
	if ($lastExitCode -ne 0) {
		throw "Build failed with an exit code of '$lastExitCode'."
	}
}

function Build-vsmanprojs
{
  $msbuild = Locate-MSBuild
  $packagesPath = Locate-PackagesPath
  
  foreach($vsmanproj in $vsmanprojs)
  {
	$vsmanprojPath = Locate-Solution -relativePath $vsmanproj
	
	Write-Host -object "Starting $vsmanproj build..."
	& $msbuild /t:$target /p:Configuration=$configuration /tv:$msbuildVersion /m /p:TargetExt=.vsman /p:MicroBuildOverridePluginDirectory="$packagesPath" $vsmanprojPath
	
	if ($lastExitCode -ne 0) {
		throw "VSManProj build failed with an exit code of '$lastExitCode'."
	}
  }
}

function Perform-Build {
  Write-Host -object ""

  Invoke-Build -solution "TestFx.sln"
  
  if($templates -or $full)
  {
	Invoke-Build -solution "Templates\MSTestTemplates.sln"
  }
  
  if($wizards -or $full)
  {
	Invoke-Build -solution "WizardExtensions\WizardExtensions.sln"	
  }
  
  if($vsmanprojects -or $full)
  {
	Build-vsmanprojs
  }
  
  Write-Host -object "The build completed successfully." -foregroundColor Green
}

function Perform-Restore {
  Write-Host -object ""

  $nuget = Locate-NuGet
  $nugetConfig = Locate-NuGetConfig
  $toolset = Locate-Toolset
  
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
  foreach($solution in $solutions)
  {
	$solutionPath = Locate-Solution -relativePath $solution

	& $nuget restore -msbuildPath $msbuildPath -verbosity quiet -nonInteractive -configFile $nugetConfig $solutionPath
  }

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
  Write-Host -object "  Templates                     - [Switch] - Indicates Templates should also be build."
  Write-Host -object "  Wizards                       - [Switch] - Indicates WizardExtensions should also be build."
  Write-Host -object "  VSManProjects                 - [Switch] - Indiactes VSManProjes should also be build."
  Write-Host -object "  Full                          - [Switch] - Indicates to perform a full build which includes Adapter,Framework,Templates,Wizards, and vsmanprojs."
  Write-Host -object ""
  Write-Host -object "  Configuration                 - [String] - Specifies the build configuration. Defaults to 'Debug'."
  Write-Host -object "  Target                        - [String] - Specifies the build target. Defaults to 'Build'."

  Write-Host -object ""
  Exit 0
}

Print-Help
Perform-Restore
Perform-Build
