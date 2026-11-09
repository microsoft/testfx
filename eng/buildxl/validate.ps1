# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT license. See LICENSE file in the project root for full license information.

#Requires -Version 7.2

[CmdletBinding()]
param(
    [switch] $NoJournal,
    [ValidateSet('InvalidProjectRejected', 'BuildFailurePropagates', 'ChangedInputInvalidatesCache',
        'NativeBuildProducesClosure', 'CallerStateRestored', 'UnchangedBuildUsesCache', 'MissingOutputRestoredFromCache')]
    [string[]] $Case = @('InvalidProjectRejected', 'BuildFailurePropagates', 'ChangedInputInvalidatesCache',
        'NativeBuildProducesClosure', 'CallerStateRestored', 'UnchangedBuildUsesCache', 'MissingOutputRestoredFromCache'),
    [string] $BuildScript = (Join-Path (Split-Path $PSScriptRoot -Parent) 'buildxl.ps1')
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version 2.0
if (-not $IsWindows) {
    throw 'The BuildXL smoke checks require Windows.'
}

$repo = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$evidence = Join-Path $repo "artifacts\buildxl-validation\$([Guid]::NewGuid().ToString('N'))"
New-Item -ItemType Directory -Path $evidence | Out-Null
$pwsh = Join-Path $PSHOME 'pwsh.exe'
$results = [Collections.Generic.List[object]]::new()
$commands = [Collections.Generic.List[object]]::new()
[string[]] $commonArguments = if ($NoJournal) { @('-NoJournal') } else { @() }
$statsPath = Join-Path $repo 'artifacts\buildxl\logs\BuildXL.stats'

function Assert-Equal($Expected, $Actual) {
    if ($Expected -cne $Actual) {
        throw "Expected <$Expected>, got <$Actual>."
    }
}

function Invoke-Build([string] $Name, [string[]] $BuildArguments = @()) {
    $log = Join-Path $evidence "$Name.log"
    $started = [DateTime]::UtcNow
    & $pwsh -NoLogo -NoProfile -File $BuildScript @commonArguments @BuildArguments *> $log
    $code = $LASTEXITCODE
    $commands.Add([pscustomobject]@{ Name = $Name; Arguments = @($commonArguments) + $BuildArguments; ExitCode = $code; Log = $log })
    return [pscustomobject]@{ Name = $Name; ExitCode = $code; Output = (Get-Content -LiteralPath $log -Raw); Started = $started }
}

function Read-FreshStats($Run) {
    Assert-Equal 0 $Run.ExitCode
    if ((Get-Item -LiteralPath $statsPath).LastWriteTimeUtc -lt $Run.Started) {
        throw "BuildXL did not produce fresh counters for $($Run.Name)."
    }

    Copy-Item -LiteralPath $statsPath -Destination (Join-Path $evidence "$($Run.Name).stats")
    $stats = @{}
    foreach ($line in Get-Content -LiteralPath $statsPath) {
        if ($line -cmatch '^(TotalProcessPips|ProcessPipCacheHits|ProcessPipCacheMisses)=(\d+)$') {
            $stats[$Matches[1]] = [long] $Matches[2]
        }
    }

    Assert-Equal 3 $stats.Count
    return $stats
}

function Test-Case([string] $Name, [scriptblock] $Body) {
    if ($Name -notin $Case) {
        return
    }

    try {
        & $Body
        $results.Add([pscustomobject]@{ Name = $Name; Passed = $true; Error = $null })
        Write-Host "PASS $Name"
    }
    catch {
        $results.Add([pscustomobject]@{ Name = $Name; Passed = $false; Error = $_.Exception.Message })
        Write-Host "FAIL $Name`: $($_.Exception.Message)"
    }
}

$failureProject = Join-Path $evidence 'failure.csproj'
@'
<Project DefaultTargets="Build">
  <Target Name="Restore" />
  <Target Name="Build">
    <Error Code="BUILDXLTEST001" Text="Deliberate BuildXL failure propagation check." />
  </Target>
</Project>
'@ | Set-Content -LiteralPath $failureProject -Encoding utf8
$probeProject = Join-Path $evidence 'cache-probe.csproj'
@'
<Project DefaultTargets="Build">
  <Target Name="Restore" />
  <Target Name="Build">
    <ReadLinesFromFile File="$(MSBuildProjectDirectory)\input.txt">
      <Output TaskParameter="Lines" ItemName="ProbeInput" />
    </ReadLinesFromFile>
    <WriteLinesToFile File="$(MSBuildProjectDirectory)\output.txt" Lines="@(ProbeInput)" Overwrite="true" />
  </Target>
</Project>
'@ | Set-Content -LiteralPath $probeProject -Encoding utf8

try {
    Test-Case 'InvalidProjectRejected' {
        $missing = [IO.Path]::GetRelativePath($repo, (Join-Path $evidence 'missing.csproj'))
        $run = Invoke-Build 'invalid-project' @('-Project', $missing)
        Assert-Equal 1 $run.ExitCode
        Assert-Equal $true $run.Output.Contains('Project must name an existing .csproj inside this checkout.')
    }
    Test-Case 'BuildFailurePropagates' {
        $project = [IO.Path]::GetRelativePath($repo, $failureProject)
        $run = Invoke-Build 'expected-build-failure' @('-Project', $project)
        Assert-Equal $true $run.Output.Contains('BUILDXLTEST001')
        Assert-Equal 1 $run.ExitCode
    }
    Test-Case 'ChangedInputInvalidatesCache' {
        $project = [IO.Path]::GetRelativePath($repo, $probeProject)
        $inputPath = Join-Path $evidence 'input.txt'
        $outputPath = Join-Path $evidence 'output.txt'
        Set-Content -LiteralPath $inputPath -Value 'alpha' -NoNewline -Encoding utf8
        $stamp = (Get-Item -LiteralPath $inputPath).LastWriteTimeUtc
        $before = Read-FreshStats (Invoke-Build 'input-before' @('-Project', $project))
        Assert-Equal 1 $before.TotalProcessPips
        Assert-Equal "alpha$([Environment]::NewLine)" (Get-Content -LiteralPath $outputPath -Raw)

        Set-Content -LiteralPath $inputPath -Value 'bravo' -NoNewline -Encoding utf8
        (Get-Item -LiteralPath $inputPath).LastWriteTimeUtc = $stamp
        Assert-Equal $stamp (Get-Item -LiteralPath $inputPath).LastWriteTimeUtc
        $after = Read-FreshStats (Invoke-Build 'input-after' @('-Project', $project))
        Assert-Equal 1 $after.TotalProcessPips
        Assert-Equal 1 $after.ProcessPipCacheMisses
        Assert-Equal 0 $after.ProcessPipCacheHits
        Assert-Equal "bravo$([Environment]::NewLine)" (Get-Content -LiteralPath $outputPath -Raw)
    }
    Test-Case 'NativeBuildProducesClosure' {
        $stats = Read-FreshStats (Invoke-Build 'native-build')
        Assert-Equal 6 $stats.TotalProcessPips
        foreach ($name in @('Microsoft.Testing.Platform', 'Microsoft.Testing.Extensions.TrxReport.Abstractions', 'Microsoft.Testing.Extensions.TrxReport')) {
            $assembly = Join-Path $repo "artifacts\buildxl\bin\$name\Debug\net10.0\$name.dll"
            Assert-Equal $name ([Reflection.AssemblyName]::GetAssemblyName($assembly).Name)
        }
    }
    Test-Case 'CallerStateRestored' {
        $names = @(
            'PATH', 'DOTNET_INSTALL_DIR', 'DOTNET_NOLOGO', 'NUGET_PACKAGES',
            'DOTNET_ROOT', 'DOTNET_MSBUILD_SDK_RESOLVER_CLI_DIR', 'DOTNET_MULTILEVEL_LOOKUP', 'DOTNET_CLI_TELEMETRY_OPTOUT',
            'TESTFX_BUILDXL_ROOT', 'TESTFX_BUILDXL_PROJECT', 'TESTFX_BUILDXL_CONFIGURATION', 'TESTFX_BUILDXL_TFM',
            'TESTFX_BUILDXL_MSBUILD', 'TESTFX_BUILDXL_BIN', 'TESTFX_BUILDXL_OBJ', 'TESTFX_BUILDXL_ADDITIONAL_TARGETS'
        )
        $saved = @{}
        foreach ($name in $names) {
            $saved[$name] = [Environment]::GetEnvironmentVariable($name)
        }

        $cachedSdk = Get-Variable -Name _DotNetInstallDir -Scope Global -ErrorAction SilentlyContinue
        $cachedSdkValue = if ($null -ne $cachedSdk) { $cachedSdk.Value } else { $null }
        Push-Location (Join-Path $repo 'docs')
        try {
            if (Test-Path -LiteralPath Env:DOTNET_INSTALL_DIR) {
                Remove-Item -LiteralPath Env:DOTNET_INSTALL_DIR
            }

            $env:TESTFX_BUILDXL_PROJECT = 'caller-state-sentinel'
            Set-Variable -Name _DotNetInstallDir -Scope Global -Value 'must-not-be-used-as-an-sdk'
            $expected = @{}
            foreach ($name in $names) {
                $expected[$name] = [Environment]::GetEnvironmentVariable($name)
            }

            $log = Join-Path $evidence 'caller-state.log'
            & $BuildScript -NoJournal:$NoJournal *> $log
            $code = $LASTEXITCODE
            $commands.Add([pscustomobject]@{ Name = 'caller-state'; Arguments = $commonArguments; ExitCode = $code; Log = $log })
            Assert-Equal 0 $code
            Assert-Equal (Join-Path $repo 'docs') $PWD.Path
            foreach ($name in $names) {
                $actual = [Environment]::GetEnvironmentVariable($name)
                if ($null -eq $expected[$name] -and $null -ne $actual) {
                    throw "Caller environment variable $name changed from absent to present."
                }

                Assert-Equal $expected[$name] $actual
            }

            Assert-Equal 'must-not-be-used-as-an-sdk' (Get-Variable -Name _DotNetInstallDir -Scope Global).Value
        }
        finally {
            foreach ($name in $names) {
                $value = if ($null -eq $saved[$name]) { [NullString]::Value } else { $saved[$name] }
                [Environment]::SetEnvironmentVariable($name, $value)
            }

            if ($cachedSdk) {
                Set-Variable -Name _DotNetInstallDir -Scope Global -Value $cachedSdkValue
            }
            else {
                Remove-Variable -Name _DotNetInstallDir -Scope Global -ErrorAction SilentlyContinue
            }

            Pop-Location
        }
    }
    Test-Case 'UnchangedBuildUsesCache' {
        $stats = Read-FreshStats (Invoke-Build 'warm-build')
        Assert-Equal 6 $stats.TotalProcessPips
        Assert-Equal 6 $stats.ProcessPipCacheHits
        Assert-Equal 0 $stats.ProcessPipCacheMisses
    }
    Test-Case 'MissingOutputRestoredFromCache' {
        $assembly = Join-Path $repo 'artifacts\buildxl\bin\Microsoft.Testing.Extensions.TrxReport\Debug\net10.0\Microsoft.Testing.Extensions.TrxReport.dll'
        $expectedHash = (Get-FileHash -LiteralPath $assembly -Algorithm SHA256).Hash
        Remove-Item -LiteralPath $assembly
        $stats = Read-FreshStats (Invoke-Build 'rehydrated-build')
        Assert-Equal 6 $stats.TotalProcessPips
        Assert-Equal 6 $stats.ProcessPipCacheHits
        Assert-Equal 0 $stats.ProcessPipCacheMisses
        Assert-Equal $expectedHash (Get-FileHash -LiteralPath $assembly -Algorithm SHA256).Hash
    }
}
finally {
    $report = [ordered]@{
        Total = $results.Count
        Passed = @($results | Where-Object Passed).Count
        WrapperSha256 = (Get-FileHash -LiteralPath $BuildScript -Algorithm SHA256).Hash
        ConfigurationSha256 = (Get-FileHash -LiteralPath (Join-Path $PSScriptRoot 'config.dsc') -Algorithm SHA256).Hash
        ValidatorSha256 = (Get-FileHash -LiteralPath $PSCommandPath -Algorithm SHA256).Hash
        Tests = $results
        Commands = $commands
    }
    $report | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $evidence 'results.json') -Encoding utf8
    Write-Host "BuildXL evidence: $evidence"
}

if ($results.Count -ne $Case.Count -or @($results | Where-Object { -not $_.Passed }).Count -ne 0) {
    exit 1
}

exit 0
