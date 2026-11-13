# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT license. See LICENSE file in the project root for full license information.

#requires -Version 7.2

[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string] $EngineeringSdkDirectory,

    [Parameter(Mandatory)]
    [string] $PublishedSdkDirectory,

    [string] $EvidenceRoot
)

$ErrorActionPreference = 'Stop'
$PSNativeCommandUseErrorActionPreference = $false
$root = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$verifier = Join-Path $PSScriptRoot 'verify-engineering-sdk.ps1'
$engineeringSdk = (Resolve-Path -LiteralPath $EngineeringSdkDirectory).Path
$publishedSdk = (Resolve-Path -LiteralPath $PublishedSdkDirectory).Path
$base = if ($EvidenceRoot) { [IO.Path]::GetFullPath($EvidenceRoot) } else { Join-Path $root 'artifacts\bazel-sdk-verifier-tests' }
$directory = Join-Path $base ([Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $directory | Out-Null
$expectedPacks = [ordered]@{ 'net10.0' = '10.0.14'; 'net11.0' = '11.0.0-rc.3.26522.148' }
$results = [Collections.Generic.List[object]]::new()
$pwsh = (Get-Process -Id $PID).Path
$dotnetName = if ($IsWindows) { 'dotnet.exe' } else { 'dotnet' }

function New-Graph([string] $SdkRoot = '.', [string] $Version = '12.0.100-alpha.1.26562.108') {
    $actions = foreach ($pair in $expectedPacks.GetEnumerator()) {
        @{
            mnemonic = 'CSharpCompile'
            targetId = 1
            arguments = @('compiler_wrapper', "$SdkRoot/$dotnetName", "$SdkRoot/sdk/$Version/Roslyn/bincore/csc.dll", '@compiler.params')
            paramFiles = @(@{ arguments = @("-r:external/rules_dotnet++dotnet+nuget.microsoft.netcore.app.ref.v$($pair.Value)/ref/$($pair.Key)/System.Runtime.dll") })
        }
    }
    return @{ targets = @(@{ id = 1; label = '//:platform' }); actions = @($actions) }
}

function Assert-Fixture([string] $Name, [hashtable] $Graph, [int] $ExpectedExit, [string] $Message, [string] $ExecutionRoot = $engineeringSdk, [string] $TargetFramework) {
    $path = Join-Path $directory "$Name.json"
    $output = Join-Path $directory "$Name.result.json"
    $log = Join-Path $directory "$Name.log"
    $Graph | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath $path -Encoding utf8
    $frameworkArguments = if ($TargetFramework) { @('-TargetFrameworks', $TargetFramework) } else { @() }
    & $pwsh -NoProfile -NonInteractive -File $verifier -ActionGraph $path -ExecutionRoot $ExecutionRoot -OutputPath $output @frameworkArguments *> $log
    $code = $LASTEXITCODE
    $text = Get-Content -LiteralPath $log -Raw
    if ($code -ne $ExpectedExit -or -not $text.Contains($Message)) { throw "Fixture $Name failed: exit=$code`n$text" }
    $record = Get-Content -LiteralPath $output -Raw | ConvertFrom-Json
    if ($record.Passed -ne ($ExpectedExit -eq 0)) { throw "Fixture $Name wrote an incorrect Passed state." }
    $results.Add([pscustomobject]@{ Name = $Name; ExitCode = $code; Result = 'Passed'; Input = 'Synthetic action graph; not native build evidence' })
    Write-Host "$Name passed"
}

function Assert-EnvironmentRestored([string] $Name, [string] $Fixture, [bool] $ExpectFailure) {
    $expected = @{
        DOTNET_CLI_HOME = 'caller home with spaces'
        # PowerShell 7.5 added empty-but-present environment values.
        DOTNET_CLI_TELEMETRY_OPTOUT = if ($PSVersionTable.PSVersion -ge [version]'7.5') { '' } else { $null }
        DOTNET_ROLL_FORWARD = 'Major'
        DOTNET_ROLL_FORWARD_TO_PRERELEASE = $null
    }
    $previous = @{}
    foreach ($key in $expected.Keys) {
        $path = "Env:\$key"
        $previous[$key] = if (Test-Path -LiteralPath $path) { (Get-Item -LiteralPath $path).Value } else { $null }
    }
    try {
        foreach ($entry in $expected.GetEnumerator()) {
            $path = "Env:\$($entry.Key)"
            if ($null -ne $entry.Value) { Set-Item -LiteralPath $path -Value $entry.Value }
            elseif (Test-Path -LiteralPath $path) { Remove-Item -LiteralPath $path }
        }
        $failed = $false
        try {
            & $verifier -ActionGraph (Join-Path $directory "$Fixture.json") -ExecutionRoot $engineeringSdk -OutputPath (Join-Path $directory "$Name.result.json") *> (Join-Path $directory "$Name.log")
        }
        catch [System.Management.Automation.RuntimeException] {
            if (-not $ExpectFailure -or -not $_.Exception.Message.Contains('action graph must include the requested target frameworks')) { throw }
            $failed = $true
        }
        if ($failed -ne $ExpectFailure) { throw "$Name observed an unexpected verifier outcome." }
        foreach ($entry in $expected.GetEnumerator()) {
            $path = "Env:\$($entry.Key)"
            $exists = Test-Path -LiteralPath $path
            if ($exists -ne ($null -ne $entry.Value) -or ($exists -and (Get-Item -LiteralPath $path).Value -cne $entry.Value)) {
                throw "$Name did not restore $($entry.Key), including presence versus an empty value."
            }
        }
        $results.Add([pscustomobject]@{ Name = $Name; Result = 'Passed'; Input = 'Synthetic action graph; in-process environment restoration' })
        Write-Host "$Name passed"
    }
    finally {
        foreach ($entry in $previous.GetEnumerator()) {
            $path = "Env:\$($entry.Key)"
            if ($null -ne $entry.Value) { Set-Item -LiteralPath $path -Value $entry.Value }
            elseif (Test-Path -LiteralPath $path) { Remove-Item -LiteralPath $path }
        }
    }
}

Assert-Fixture 'NoSourceActions' @{ targets = @(); actions = @() } 1 'No source-owned compiler actions'
$graph = New-Graph
$graph.actions[0].arguments[1] = "different-sdk/$dotnetName"
Assert-Fixture 'MismatchedCompilerHost' $graph 1 'compiler and execution host come from different SDK roots'
$graph = New-Graph
$graph.actions[1].paramFiles[0].arguments[0] = '-r:external/rules_dotnet++dotnet+nuget.microsoft.netcore.app.ref.v12.0.0-alpha.1.26562.108/ref/net11.0/System.Runtime.dll'
Assert-Fixture 'WrongNet11ReferencePack' $graph 1 'Unexpected net11.0 reference pack'
$graph = New-Graph
$graph.actions = @($graph.actions[0])
Assert-Fixture 'MissingFrameworks' $graph 1 'action graph must include the requested target frameworks'
Assert-Fixture 'RequestedFrameworkSubset' $graph 0 'Engineering compiler verified for 1 declared source actions' -TargetFramework 'net10.0'
$graph = New-Graph
foreach ($action in $graph.actions) {
    $action.arguments += $action.paramFiles[0].arguments
    $action.Remove('paramFiles')
}
Assert-Fixture 'ExpandedCompilerArguments' $graph 0 'Engineering compiler verified for 2 declared source actions'
$graph = New-Graph
$graph.targets += @{ id = 2; label = '@@rules_dotnet+//tools/csharp_compiler_worker:csharp_compiler_worker' }
$graph.actions += @{
    mnemonic = 'CSharpCompile'
    targetId = 2
    arguments = @('compiler_wrapper', "./$dotnetName", './sdk/12.0.100-alpha.1.26562.108/Roslyn/bincore/csc.dll')
}
Assert-Fixture 'ChangedBootstrapCompiler' $graph 1 'bootstrap compiler changed'
Assert-Fixture 'OutsideExecutionRoot' (New-Graph '../outside-root') 1 'Action input is outside the supplied execution root'
Assert-Fixture 'PublishedCompilerRejected' (New-Graph '.' '11.0.100-rc.3.26522.148') 1 'Expected global.json compiler SDK' $publishedSdk
Assert-Fixture 'VerifiedEngineeringCompiler' (New-Graph) 0 'Engineering compiler verified for 2 declared source actions'
Assert-EnvironmentRestored 'EnvironmentRestoredAfterSuccess' 'VerifiedEngineeringCompiler' $false
Assert-EnvironmentRestored 'EnvironmentRestoredAfterFailure' 'MissingFrameworks' $true
$results | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $directory 'fixture-results.json') -Encoding utf8
Write-Host "Verifier fixtures passed: $($results.Count). These do not establish native-build or composed-product parity. Evidence: $directory"

exit 0
