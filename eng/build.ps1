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
    if (Test-Path "C:\PROGRA~2\Windows Kits\10\UnionMetadata\10.0.16299.0") {
        Write-Host "Windows SDK 10.0.16299 is already installed, skipping..."
    } else {
        Write-Host "Downloading Windows SDK 10.0.16299..." -ForegroundColor Green
        Invoke-WebRequest -Method Get -Uri https://go.microsoft.com/fwlink/p/?linkid=864422 -OutFile sdksetup.exe -UseBasicParsing

        Write-Host "Installing Windows SDK, if setup requests elevation please approve." -ForegroundColor Green
        $process = Start-Process -Wait sdksetup.exe -ArgumentList "/quiet", "/norestart", "/ceip off", "/features OptionId.UWPManaged"  -PassThru

        if ($process.ExitCode -eq 0) {
            Remove-Item sdksetup.exe -Force
            Write-Host "Installation succeeded"
        }
        else {
            Write-Error "Failed to install Windows SDK (Exit code: $($process.ExitCode))"
        }
    }
} else {
    Write-Host "Skipping Windows SDK installation"
}

# Remove extra parameters that are not used by the common build script
$null = $PSBoundParameters.Remove("vs")
$null = $PSBoundParameters.Remove("installWindowsSdk")

& $PSScriptRoot\common\Build.ps1 @PSBoundParameters
