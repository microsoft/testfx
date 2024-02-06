[CmdletBinding(PositionalBinding=$false)]
Param(
  [string][Alias('c')]$configuration = "Debug",
  [string]$platform = $null,
  [string] $projects,
  [string][Alias('v')]$verbosity = "minimal",
  [string] $msbuildEngine = $null,
  [bool] $warnAsError = $true,
  [bool] $nodeReuse = $true,
  [switch][Alias('r')]$restore,
  [switch] $deployDeps,
  [switch][Alias('b')]$build,
  [switch] $rebuild,
  [switch] $deploy,
  [switch][Alias('t')]$test,
  [switch] $integrationTest,
  [switch] $performanceTest,
  [switch] $sign,
  [switch] $pack,
  [switch] $publish,
  [switch] $clean,
  [switch][Alias('bl')]$binaryLog,
  [switch][Alias('nobl')]$excludeCIBinarylog,
  [switch] $ci,
  [switch] $prepareMachine,
  [string] $runtimeSourceFeed = '',
  [string] $runtimeSourceFeedKey = '',
  [switch] $excludePrereleaseVS,
  [switch] $nativeToolsOnMachine,
  [switch] $help,
  [switch] $vs,
  [switch] $installWindowsSdk,
  [Parameter(ValueFromRemainingArguments=$true)][String[]]$properties
)

if ($vs) {
    . $PSScriptRoot\common\tools.ps1

    # This tells .NET Core to use the bootstrapped runtime
    $env:DOTNET_ROOT=InitializeDotNetCli -install:$true -createSdkLocationFile:$true

    # This tells MSBuild to load the SDK from the directory of the bootstrapped SDK
    $env:DOTNET_MSBUILD_SDK_RESOLVER_CLI_DIR=$env:DOTNET_ROOT

    # This tells .NET Core not to go looking for .NET Core in other places
    $env:DOTNET_MULTILEVEL_LOOKUP=0;

    # Put our local dotnet.exe on PATH first so Visual Studio knows which one to use
    $env:PATH=($env:DOTNET_ROOT + ";" + $env:PATH);

    # Disable .NET runtime signature validation errors which errors for local builds
    $env:VSDebugger_ValidateDotnetDebugLibSignatures=0;

    # Launch Visual Studio with the locally defined environment variables
    & "$PSScriptRoot\..\TestFx.sln"

    return
}

if ($installWindowsSdk) {
    & $PSScriptRoot\install-windows-sdk.ps1
} else {
    Write-Host "Skipping Windows SDK installation"
}

# Remove extra parameters that are not used by the common build script
$null = $PSBoundParameters.Remove("vs")
$null = $PSBoundParameters.Remove("installWindowsSdk")

& $PSScriptRoot\common\Build.ps1 @PSBoundParameters
