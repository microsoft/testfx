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

function Start-Timer
{
    return [System.Diagnostics.Stopwatch]::StartNew()
}

function Get-ElapsedTime([System.Diagnostics.Stopwatch] $timer)
{
    $timer.Stop()
    return $timer.Elapsed
}

function Write-Log ([string] $message, $messageColor = "Green")
{
    $currentColor = $Host.UI.RawUI.ForegroundColor
    $Host.UI.RawUI.ForegroundColor = $messageColor
    if ($message)
    {
        Write-Output "... $message"
    }
    $Host.UI.RawUI.ForegroundColor = $currentColor
}

function Invoke-Test
{
    $timer = Start-Timer
    $vstestPath = Get-VSTestPath
	
	if(!(Test-Path $vstestPath))
	{
		Write-Host -object "Unable to find vstest.console.exe at $vstestPath"
		Write-Error "Test aborted."
	}
	
	$artifactsPath = Locate-Artifacts
	$testFolders = Get-ChildItem $artifactsPath -Filter *Tests* | %{$_.FullName}

    Write-Host -object "Invoke-Test: Start test."
	Write-Log "TestContainers : "
	foreach($source in $testFolders)
	{
		$testOutputPath = Join-Path $source "bin/$configuration"
		$testContainerPath = Get-ChildItem $testOutputPath/* -Recurse -Include "*.UnitTests*.dll", "*.ComponentTests*.dll", "*.E2ETests*.dll" | %{$_.FullName}
		
		Write-Log "$testContainerPath "
		$testContainers += ,"$testContainerPath"
		
	}
	
	Write-Verbose "$vstestPath $testContainers /logger:trx"
	$output = & $vstestPath $testContainers /logger:trx
		
	if($output[-2].Contains("Test Run Successful."))
	{
		Write-Log ".. .$($output[-3])"
	}
	else
	{
		Write-Log ".. .$($output[-2])"
		Write-Log ".. . Failed tests:" "Red"
	}
	
	Write-Log "Invoke-Test: Complete. {$(Get-ElapsedTime($timer))}"
}

function Get-VSTestPath
{
	$vsInstallPath = Locate-VsInstallPath
	$vstestPath = Join-Path -path $vsInstallPath "Common7\IDE\CommonExtensions\Microsoft\TestWindow\vstest.console.exe"
	return Resolve-Path -path $vstestPath
}
	
Invoke-Test
