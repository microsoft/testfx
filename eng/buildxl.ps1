# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT license. See LICENSE file in the project root for full license information.

#Requires -Version 7.0

[CmdletBinding(PositionalBinding = $false)]
param(
    [string] $Project = 'TestFx.slnx',
    [ValidateSet('Debug', 'Release')]
    [Alias('c')]
    [string] $Configuration = 'Debug',
    [ValidateSet('', 'net10.0', 'net11.0', 'netstandard2.0')]
    [string] $TargetFramework = '',
    [ValidateSet('BuildXL', 'MSBuild')]
    [string] $Engine = 'BuildXL',
    [ValidatePattern('^$|^[a-z0-9][a-z0-9-]{0,80}$')]
    [string] $OutputId = '',
    [ValidateRange(1, 64)]
    [int] $Jobs = [Math]::Min(64, [Environment]::ProcessorCount),
    [string] $AdditionalTargets = '',
    [switch] $PrepareOnly,
    [switch] $SkipRestore,
    [switch] $NoJournal
)

$ErrorActionPreference = 'Stop'
$PSNativeCommandUseErrorActionPreference = $false
Set-StrictMode -Version 2.0

function Get-BuildXLFileSystemMode([string[]] $Paths, [switch] $NoJournal) {
    if ($NoJournal) {
        Write-Host 'BuildXL filesystem mode: full input hashing without hardlinks (-NoJournal).'
        return 'FullHashing'
    }

    $volumes = @($Paths | Where-Object { $_ } | ForEach-Object { [IO.Path]::GetPathRoot($_) } | Sort-Object -Unique)
    foreach ($volume in $volumes) {
        $diagnostics = & fsutil.exe usn queryjournal $volume 2>&1
        $code = $LASTEXITCODE
        if ($code -ne 0) {
            Write-Host "BuildXL filesystem mode: full input hashing without hardlinks; journal unavailable on $volume (exit $code): $($diagnostics -join ' ')"
            return 'FullHashing'
        }
    }

    Write-Host 'BuildXL filesystem mode: journal-backed input tracking and hardlinks.'
    return 'Journal'
}

if ($env:OS -ne 'Windows_NT' -or $env:PROCESSOR_ARCHITECTURE -ne 'AMD64') {
    throw 'The experimental BuildXL build requires Windows x64 and PowerShell 7.'
}

$repo = Split-Path $PSScriptRoot -Parent
$projectPath = [IO.Path]::GetFullPath((Join-Path $repo $Project))
$relativeProject = [IO.Path]::GetRelativePath($repo, $projectPath)
if ($relativeProject.StartsWith('..', [StringComparison]::Ordinal) -or
    [IO.Path]::GetExtension($projectPath) -notin @('.csproj', '.fsproj', '.sln', '.slnx', '.slnf') -or
    -not (Test-Path -LiteralPath $projectPath -PathType Leaf)) {
    throw 'Project must name an existing MSBuild project or solution inside this checkout.'
}

if ($AdditionalTargets) {
    $AdditionalTargets = [IO.Path]::GetFullPath($AdditionalTargets, $repo)
    if (-not $AdditionalTargets.StartsWith($repo + '\', [StringComparison]::OrdinalIgnoreCase) -or
        [IO.Path]::GetExtension($AdditionalTargets) -ne '.targets' -or
        -not (Test-Path -LiteralPath $AdditionalTargets -PathType Leaf)) {
        throw 'AdditionalTargets must name an existing .targets file inside this checkout.'
    }
}

# This is the public LKG pinned by BuildXL's BuildXLLkgVersionPublic.cmd.
$version = '0.1.0-20261030.6'
$packageHash = '460832AD3CB34CB9D6B7DF3B5CF6059E172045206C48E6D48B9A3A401B0C5802'
$tools = Join-Path $repo "artifacts\buildxl-tools\$version"
$package = Join-Path $tools "microsoft.buildxl.win-x64.$version.nupkg"
$deployment = Join-Path $tools 'deployment'
$bxl = Join-Path $deployment 'bxl.exe'
New-Item -ItemType Directory -Path $tools -Force | Out-Null

if (-not (Test-Path -LiteralPath $package -PathType Leaf)) {
    $download = "$package.$([Guid]::NewGuid().ToString('N')).download"
    try {
        $feed = Invoke-RestMethod 'https://pkgs.dev.azure.com/mseng/PipelineTools/_packaging/BuildXL.External/nuget/v3/index.json'
        $base = @($feed.resources | Where-Object { $_.'@type' -eq 'PackageBaseAddress/3.0.0' })
        if ($base.Count -ne 1) {
            throw 'The BuildXL feed must expose exactly one NuGet package base address.'
        }

        $uri = "$($base[0].'@id')microsoft.buildxl.win-x64/$version/microsoft.buildxl.win-x64.$version.nupkg"
        Write-Host "Downloading BuildXL $version."
        Invoke-WebRequest $uri -OutFile $download
        if ((Get-FileHash -LiteralPath $download -Algorithm SHA256).Hash -ne $packageHash) {
            throw 'Downloaded BuildXL package failed SHA-256 verification.'
        }

        Move-Item -LiteralPath $download -Destination $package
    }
    finally {
        if (Test-Path -LiteralPath $download) {
            Remove-Item -LiteralPath $download
        }
    }
}

if ((Get-FileHash -LiteralPath $package -Algorithm SHA256).Hash -ne $packageHash) {
    throw "BuildXL package failed SHA-256 verification: $package"
}

if (-not (Test-Path -LiteralPath $bxl -PathType Leaf)) {
    $staging = Join-Path $tools "deployment-$([Guid]::NewGuid().ToString('N'))"
    try {
        [IO.Compression.ZipFile]::ExtractToDirectory($package, $staging)
        if (-not (Test-Path -LiteralPath (Join-Path $staging 'bxl.exe') -PathType Leaf)) {
            throw 'The BuildXL package does not contain bxl.exe.'
        }

        [IO.Directory]::Move($staging, $deployment)
    }
    finally {
        if (Test-Path -LiteralPath $staging) {
            Remove-Item -LiteralPath $staging -Recurse -Force
        }
    }
}

$environmentNames = @(
    'PATH', 'DOTNET_INSTALL_DIR', 'DOTNET_NOLOGO', 'NUGET_PACKAGES', 'MSBUILDDEBUGPATH', 'MSBUILDENABLEMULTITHREADED',
    'DOTNET_ROOT', 'DOTNET_MSBUILD_SDK_RESOLVER_CLI_DIR', 'DOTNET_MULTILEVEL_LOOKUP', 'DOTNET_CLI_TELEMETRY_OPTOUT',
    'TESTFX_BUILDXL_ROOT', 'TESTFX_BUILDXL_PROJECT', 'TESTFX_BUILDXL_CONFIGURATION', 'TESTFX_BUILDXL_TFM',
    'TESTFX_BUILDXL_MSBUILD', 'TESTFX_BUILDXL_BIN', 'TESTFX_BUILDXL_OBJ', 'TESTFX_BUILDXL_ADDITIONAL_TARGETS'
)
$originalEnvironment = @{}
foreach ($name in $environmentNames) {
    $originalEnvironment[$name] = [Environment]::GetEnvironmentVariable($name)
}

$cachedSdk = Get-Variable -Name _DotNetInstallDir -Scope Global -ErrorAction SilentlyContinue
$cachedSdkValue = if ($null -ne $cachedSdk) { $cachedSdk.Value } else { $null }
try {
    Remove-Variable -Name _DotNetInstallDir -Scope Global -ErrorAction SilentlyContinue
    . (Join-Path $PSScriptRoot 'common\tools.ps1')
    $sdk = InitializeDotNetCli -install:$true -createSdkLocationFile:$false
    $dotnet = Join-Path $sdk 'dotnet.exe'
    $sdkVersion = & $dotnet --version
    if ($LASTEXITCODE -ne 0 -or $sdkVersion -ne $GlobalJson.tools.dotnet) {
        throw 'BuildXL must use the exact SDK pinned by global.json.'
    }

    $output = if ($OutputId) { Join-Path $repo "artifacts\buildxl-runs\$OutputId" } else { Join-Path $repo 'artifacts\buildxl' }
    $bin = Join-Path $output 'bin\'
    $obj = if ($OutputId) { Join-Path $repo "artifacts\obj\buildxl-runs\$OutputId\" } else { Join-Path $repo 'artifacts\obj\buildxl\' }
    $logs = Join-Path $output 'logs'
    New-Item -ItemType Directory -Path $logs -Force | Out-Null
    $properties = @(
        "/p:Configuration=$Configuration",
        "/p:ArtifactsBinDir=$bin",
        "/p:ArtifactsObjDir=$obj",
        "/p:CustomBeforeDirectoryBuildTargets=$(Join-Path $PSScriptRoot 'buildxl\msbuild.targets')",
        '/p:SkipUapTargetFramework=true',
        '/p:ShouldUnsetParentConfigurationAndPlatform=false',
        '/p:EnableProjectDataOnBuild=false',
        '/p:UseSharedCompilation=true'
    )
    if ($TargetFramework) {
        $properties += "/p:TargetFrameworks=$TargetFramework"
    }

    if ($AdditionalTargets) {
        $properties += "/p:CustomAfterMicrosoftCommonTargets=$AdditionalTargets"
    }

    $env:DOTNET_ROOT = $sdk
    $env:DOTNET_MSBUILD_SDK_RESOLVER_CLI_DIR = $sdk
    $env:DOTNET_MULTILEVEL_LOOKUP = '0'
    $env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
    $env:TESTFX_BUILDXL_ROOT = $repo
    $env:TESTFX_BUILDXL_PROJECT = $relativeProject
    $env:TESTFX_BUILDXL_CONFIGURATION = $Configuration
    $env:TESTFX_BUILDXL_TFM = if ($TargetFramework) { $TargetFramework } else { '*' }
    $env:TESTFX_BUILDXL_MSBUILD = Join-Path $sdk "sdk\$sdkVersion"
    $env:TESTFX_BUILDXL_BIN = $bin
    $env:TESTFX_BUILDXL_OBJ = $obj
    $env:TESTFX_BUILDXL_ADDITIONAL_TARGETS = if ($AdditionalTargets) { $AdditionalTargets } else { '*' }

    $revision = & git -C $repo rev-parse HEAD
    if ($LASTEXITCODE -ne 0) {
        throw 'Cannot identify the BuildXL source revision.'
    }

    $fileSystemMode = Get-BuildXLFileSystemMode -Paths @($repo, $sdk, $env:NUGET_PACKAGES, $output) -NoJournal:$NoJournal
    [ordered]@{
        SourceRevision = $revision.Trim()
        Sdk = $sdkVersion
        BuildXL = $version
        BuildXLPackageSha256 = $packageHash
        Engine = $Engine
        Project = $relativeProject
        Configuration = $Configuration
        TargetFramework = if ($TargetFramework) { $TargetFramework } else { 'Project defaults' }
        SkipUapTargetFramework = $true
        Jobs = $Jobs
        ProcessorCount = [Environment]::ProcessorCount
        UseSharedCompilation = $true
        MSBuildRuntime = 'DotNetCore'
        MSBuildPath = Join-Path $env:TESTFX_BUILDXL_MSBUILD 'MSBuild.dll'
        CompilerDirectory = Join-Path $env:TESTFX_BUILDXL_MSBUILD 'Roslyn\bincore'
        FileSystemMode = $fileSystemMode
        NoJournal = $fileSystemMode -eq 'FullHashing'
        NoJournalRequested = $NoJournal.IsPresent
    } | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $output 'toolchain.json') -Encoding utf8

    Push-Location $repo
    try {
        if (-not $SkipRestore) {
            & $dotnet msbuild $projectPath /t:Restore @properties /nr:false /nologo /v:minimal "/bl:$logs\restore.binlog"
            if ($LASTEXITCODE -ne 0) {
                exit $LASTEXITCODE
            }
        }

        if ($PrepareOnly) {
            exit 0
        }

        if ($Engine -eq 'MSBuild') {
            & $dotnet msbuild $projectPath /t:Build @properties "/m:$Jobs" /nr:false /nologo /v:minimal "/bl:$logs\build.binlog"
            exit $LASTEXITCODE
        }

        $config = Join-Path $output 'config.dsc'
        Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'buildxl\config.dsc') -Destination $config -Force
        $journalArguments = @()
        if ($fileSystemMode -eq 'FullHashing') {
            # Rehash inputs instead of relying on journal-backed file identities.
            $journalArguments = @('/verifyJournalForEngineVolumes-', '/scanChangeJournal-', '/useFileContentTable-', '/useHardlinks-')
        }

        # BuildXL creates the Azure summary with CreateNew, including when a task retries.
        $summary = Join-Path $logs 'BuildXL.Summary.md'
        if (Test-Path -LiteralPath $summary -PathType Leaf) {
            $history = Join-Path $logs 'summary-history'
            New-Item -ItemType Directory -Path $history -Force | Out-Null
            Move-Item -LiteralPath $summary -Destination (Join-Path $history "BuildXL.Summary.$([Guid]::NewGuid().ToString('N')).md")
        }

        $frameworkDescription = if ($TargetFramework) { $TargetFramework } else { 'project target frameworks; classic UAP excluded' }
        Write-Host "BuildXL: $relativeProject ($Configuration, $frameworkDescription). Outputs: $output"
        & $bxl "/config:$config" "/objectDirectory:$output\engine" `
            "/cacheDirectory:$output\cache" "/logsDirectory:$logs" "/maxProc:$Jobs" /server- /remoteTelemetry- /trackBuildsInUserFolder- @journalArguments
        exit $LASTEXITCODE
    }
    finally {
        Pop-Location
    }
}
finally {
    foreach ($name in $environmentNames) {
        # An untyped null is converted to an empty string by PowerShell's method binder.
        $value = if ($null -eq $originalEnvironment[$name]) { [NullString]::Value } else { $originalEnvironment[$name] }
        [Environment]::SetEnvironmentVariable($name, $value)
    }

    if ($cachedSdk) {
        Set-Variable -Name _DotNetInstallDir -Scope Global -Value $cachedSdkValue
    }
    else {
        Remove-Variable -Name _DotNetInstallDir -Scope Global -ErrorAction SilentlyContinue
    }
}
