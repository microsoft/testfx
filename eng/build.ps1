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
  [switch] $vscode,
  [switch] $installWindowsSdk,
  [Parameter(ValueFromRemainingArguments=$true)][String[]]$properties
)

if ($vs -or $vscode) {
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

    # Enables the logging of Json RPC messages if diagnostic logging for Test Explorer is enabled in Visual Studio.
    $env:_TestingPlatformDiagnostics_=1;

    if ($vs) {
        # Launch Visual Studio with the locally defined environment variables
        & "$PSScriptRoot\..\TestFx.slnx"
    } else {
        if (Get-Command code -ErrorAction Ignore) {
            & code "$PSScriptRoot\.."
        } elseif (Get-Command code-insiders -ErrorAction Ignore) {
            & code-insiders "$PSScriptRoot\.."
        } else {
            Write-Error "VS Code not found. Please install it from https://code.visualstudio.com/"
            return
        }
    }

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

$runUnitTests = $test.IsPresent
$runIntegrationTests = $integrationTest.IsPresent

if (($runUnitTests -or $runIntegrationTests) -and -not $clean -and -not $help) {
    $childBuildArguments = [System.Collections.Generic.List[string]]::new()
    foreach ($parameter in $PSBoundParameters.GetEnumerator()) {
        if ($parameter.Key -in "test", "integrationTest") {
            continue
        }

        if ($parameter.Key -eq "properties") {
            foreach ($property in $parameter.Value) {
                $childBuildArguments.Add($property)
            }

            continue
        }

        if ($parameter.Value -is [System.Management.Automation.SwitchParameter]) {
            if ($parameter.Value.IsPresent) {
                $childBuildArguments.Add("-$($parameter.Key)")
            }

            continue
        }

        if ($parameter.Value -is [bool]) {
            $childBuildArguments.Add("-$($parameter.Key):`$$($parameter.Value.ToString().ToLowerInvariant())")
            continue
        }

        if ($null -ne $parameter.Value) {
            $childBuildArguments.Add("-$($parameter.Key)")
            $childBuildArguments.Add([string]$parameter.Value)
        }
    }

    # Arcade still bootstraps the pinned SDK and performs build/pack/sign work, but it no longer
    # receives either test switch. Test execution is delegated to the native dotnet test command.
    $powerShellPath = (Get-Process -Id $PID).Path
    & $powerShellPath -ExecutionPolicy ByPass -NoProfile -File $PSCommandPath @childBuildArguments
    if ($LASTEXITCODE -ne 0) {
        exit $LASTEXITCODE
    }

    & $PSScriptRoot\run-tests.ps1 `
        -configuration $configuration `
        -unit:$runUnitTests `
        -integration:$runIntegrationTests `
        -ci:$ci.IsPresent `
        -projects $projects `
        -properties $properties
    exit $LASTEXITCODE
}

& $PSScriptRoot\common\Build.ps1 @PSBoundParameters
