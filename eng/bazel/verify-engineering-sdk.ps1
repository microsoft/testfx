# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT license. See LICENSE file in the project root for full license information.

#requires -Version 7.2

[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string] $ActionGraph,

    [Parameter(Mandatory)]
    [string] $ExecutionRoot,

    [Parameter(Mandatory)]
    [string] $OutputPath,

    [ValidateSet('net10.0', 'net11.0')]
    [ValidateCount(1, 2)]
    [string[]] $TargetFrameworks = @('net10.0', 'net11.0')
)

$ErrorActionPreference = 'Stop'
$PSNativeCommandUseErrorActionPreference = $false
$root = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$graphPath = (Resolve-Path -LiteralPath $ActionGraph).Path
$execution = (Resolve-Path -LiteralPath $ExecutionRoot).Path
$output = [IO.Path]::GetFullPath($OutputPath)
$directory = Split-Path $output -Parent
New-Item -ItemType Directory -Path $directory -Force | Out-Null

# These expectations come from the checksum-verified SDK, not the selected toolchain.
$sdkCommit = '8b22ce04ad5fa175ef5cd8f96c38fcba40e173dc'
$compilerVersion = "5.14.0-1.26562.108 ($sdkCommit)"
$runtimeVersion = '12.0.0-alpha.1.26562.108'
$bootstrapVersion = '10.0.401'
$packVersions = [ordered]@{
    'net10.0' = '10.0.14'
    'net11.0' = '11.0.0-rc.3.26522.148'
}
$records = [Collections.Generic.List[object]]::new()
$packs = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
$userActions = 0
$bootstrapActions = 0
$passed = $false

function Resolve-ActionPath([string] $Path) {
    $full = [IO.Path]::GetFullPath($(if ([IO.Path]::IsPathFullyQualified($Path)) { $Path } else { Join-Path $execution $Path }))
    $relative = [IO.Path]::GetRelativePath($execution, $full)
    if ([IO.Path]::IsPathRooted($relative) -or $relative -eq '..' -or $relative.StartsWith('..' + [IO.Path]::DirectorySeparatorChar)) {
        throw "Action input is outside the supplied execution root: $Path"
    }
    return $full
}

$environment = @{}
foreach ($name in @('DOTNET_CLI_HOME', 'DOTNET_CLI_TELEMETRY_OPTOUT', 'DOTNET_ROLL_FORWARD', 'DOTNET_ROLL_FORWARD_TO_PRERELEASE')) {
    $path = "Env:\$name"
    $environment[$name] = if (Test-Path -LiteralPath $path) { (Get-Item -LiteralPath $path).Value } else { $null }
}
try {
    $pin = Get-Content -LiteralPath (Join-Path $root 'global.json') -Raw | ConvertFrom-Json
    $expectedSdk = $pin.sdk.version
    if ($expectedSdk -cne $pin.tools.dotnet) { throw 'The two global.json SDK pins disagree.' }
    $graph = Get-Content -LiteralPath $graphPath -Raw | ConvertFrom-Json -AsHashtable
    $targets = @{}
    foreach ($target in $graph.targets) { $targets[[string]$target.id] = $target.label }
    $compilerRoots = @{}

    foreach ($action in $graph.actions) {
        if ($action.mnemonic -ne 'CSharpCompile') { continue }
        $label = $targets[[string]$action.targetId]
        if (-not $label) { throw 'A compiler action has no target identity.' }
        $compilerArguments = @($action.arguments | Where-Object { $_.Replace('\', '/').EndsWith('/Roslyn/bincore/csc.dll') })
        if ($compilerArguments.Count -ne 1) { throw "Expected one compiler input for $label." }
        $compiler = $compilerArguments[0].Replace('\', '/')
        $match = [regex]::Match($compiler, '^(?<root>.+)/sdk/(?<version>[^/]+)/Roslyn/bincore/csc\.dll$')
        if (-not $match.Success) { throw "Unrecognized compiler layout for $label`: $compiler" }

        if ($label.StartsWith('//') -or $label.StartsWith('@@//')) {
            $userActions++
            $compilerIndex = [Array]::IndexOf([string[]]$action.arguments, $compilerArguments[0])
            $hostName = if ($IsWindows) { 'dotnet.exe' } else { 'dotnet' }
            if ($compilerIndex -lt 1 -or $action.arguments[$compilerIndex - 1].Replace('\', '/') -cne ($match.Groups['root'].Value + '/' + $hostName)) {
                throw "The compiler and execution host come from different SDK roots for $label."
            }
            $compilerRoots[$match.Groups['root'].Value] = $match.Groups['version'].Value
            $arguments = @($action.arguments)
            foreach ($file in $action.paramFiles) { $arguments += $file.arguments }
            foreach ($argument in $arguments) {
                $pack = [regex]::Match($argument.Replace('\', '/'), 'nuget\.microsoft\.netcore\.app\.ref\.v(?<version>[^/]+)/ref/(?<tfm>net[\d.]+)/')
                if (-not $pack.Success) { continue }
                $tfm = $pack.Groups['tfm'].Value
                if ($packVersions.Contains($tfm)) {
                    if ($pack.Groups['version'].Value -cne $packVersions[$tfm]) { throw "Unexpected $tfm reference pack for $label`: $argument" }
                    $null = $packs.Add($tfm)
                }
            }
        }
        elseif ($label -match '^@@?rules_dotnet[^/]*//') {
            $bootstrapActions++
            if ($match.Groups['version'].Value -cne $bootstrapVersion) {
                throw "The rules_dotnet bootstrap compiler changed for $label`: $compiler"
            }
        }
    }
    if ($userActions -eq 0) { throw 'No source-owned compiler actions were audited.' }

    $env:DOTNET_CLI_HOME = Join-Path $directory 'cli-home'
    $env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
    foreach ($name in @('DOTNET_ROLL_FORWARD', 'DOTNET_ROLL_FORWARD_TO_PRERELEASE')) {
        if (Test-Path -LiteralPath "Env:\$name") { Remove-Item -LiteralPath "Env:\$name" }
    }
    foreach ($entry in $compilerRoots.GetEnumerator()) {
        $sdk = Resolve-ActionPath $entry.Key
        $dotnet = Join-Path $sdk $(if ($IsWindows) { 'dotnet.exe' } else { 'dotnet' })
        $csc = Join-Path $sdk "sdk\$($entry.Value)\Roslyn\bincore\csc.dll"
        $actual = (& $dotnet $csc -version 2>&1 | Out-String).Trim()
        $exitCode = $LASTEXITCODE
        $records.Add([pscustomobject]@{
            Sdk = $entry.Value
            Host = $dotnet
            Compiler = $csc
            CompilerSha256 = (Get-FileHash -LiteralPath $csc -Algorithm SHA256).Hash
            VersionOutput = $actual
            ExitCode = $exitCode
        })
        if ($exitCode -ne 0) { throw "The selected compiler failed to execute: $actual" }
        if ($entry.Value -cne $expectedSdk) { throw "Expected global.json compiler SDK $expectedSdk, selected $($entry.Value) ($actual)." }
        if ($actual -cne $compilerVersion) { throw "Unexpected compiler identity: $actual" }
        $version = @(Get-Content -LiteralPath (Join-Path $sdk "sdk\$expectedSdk\.version"))
        if ($version.Count -lt 2 -or $version[0] -cne $sdkCommit -or $version[1] -cne $expectedSdk) {
            throw 'The selected SDK has unexpected source provenance.'
        }
        $runtime = Get-Content -LiteralPath (Join-Path $sdk "sdk\$expectedSdk\Roslyn\bincore\csc.runtimeconfig.json") -Raw | ConvertFrom-Json
        if ($runtime.runtimeOptions.framework.version -cne $runtimeVersion) { throw 'The compiler requests an unexpected runtime.' }
        $coreLib = Join-Path $sdk "shared\Microsoft.NETCore.App\$runtimeVersion\System.Private.CoreLib.dll"
        $records[$records.Count - 1] | Add-Member -NotePropertyName RuntimeSha256 -NotePropertyValue (Get-FileHash -LiteralPath $coreLib -Algorithm SHA256).Hash
    }
    if (@($TargetFrameworks | Where-Object { -not $packs.Contains($_) }).Count) {
        throw "The action graph must include the requested target frameworks ($($TargetFrameworks -join ', ')) and parameter files. Use aquery with --include_param_files --output=jsonproto."
    }
    $passed = $true
    Write-Host "Engineering compiler verified for $userActions declared source actions; requested reference packs preserved: $($TargetFrameworks -join ', ')."
}
finally {
    foreach ($entry in $environment.GetEnumerator()) {
        $path = "Env:\$($entry.Key)"
        if ($null -ne $entry.Value) { Set-Item -LiteralPath $path -Value $entry.Value }
        elseif (Test-Path -LiteralPath $path) { Remove-Item -LiteralPath $path }
    }
    [pscustomobject]@{
        Passed = $passed
        ActionGraphSha256 = (Get-FileHash -LiteralPath $graphPath -Algorithm SHA256).Hash
        ExecutionRoot = $execution
        ExpectedSdk = $expectedSdk
        UserCompilerActions = $userActions
        BootstrapCompilerActions = $bootstrapActions
        ReferencePacks = $packVersions
        ExpectedReferenceFrameworks = $TargetFrameworks
        ObservedReferenceFrameworks = @($packs)
        Compilers = $records
        Scope = 'Action-graph and executed compiler identity only; pair with successful native build, tests, and composed-consumer results.'
    } | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $output -Encoding utf8
}
