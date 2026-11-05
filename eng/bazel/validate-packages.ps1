# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT license. See LICENSE file in the project root for full license information.

#requires -Version 7.2

[CmdletBinding()]
param(
    [ValidateRange(1, 2147483647)]
    [int] $Jobs = 8,

    [ValidateScript({ [System.IO.Path]::IsPathFullyQualified($_) })]
    [string] $EvidenceRoot
)

$ErrorActionPreference = 'Stop'
$PSNativeCommandUseErrorActionPreference = $false
. (Join-Path $PSScriptRoot 'console-output.ps1') -SelfTest
$root = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$bazel = Join-Path $root 'eng\bazel.ps1'
$run = [DateTime]::UtcNow.ToString('yyyyMMdd-HHmmss') + '-' + [Guid]::NewGuid().ToString('N').Substring(0, 8)
$directory = if ($EvidenceRoot) { Join-Path $EvidenceRoot $run } else { Join-Path $root "artifacts\bazel-package-validation\$run" }
$feed = Join-Path $directory 'feed'
$consumer = Join-Path $directory 'consumer'
New-Item -ItemType Directory -Path $feed, $consumer | Out-Null
$frameworks = @('net8.0', 'net9.0', 'net10.0', 'net11.0')
$expectedPackageIds = @('Microsoft.Testing.Platform', 'Microsoft.Testing.Extensions.TrxReport.Abstractions', 'Microsoft.Testing.Extensions.TrxReport')
$seenPackages = [System.Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
$seenRuntimes = [System.Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
$packageRecords = [System.Collections.Generic.List[object]]::new()
$steps = [System.Collections.Generic.List[object]]::new()
$runtimeRecords = [System.Collections.Generic.List[object]]::new()
$expectedHashes = [ordered]@{}
$succeeded = $false
$consumerSdk = $null

function Invoke-Step([string] $Name, [string] $Executable, [string[]] $Arguments, [int] $ExpectedExit = 0) {
    $log = Join-Path $directory "$Name.log"
    & $Executable @Arguments *> $log
    $code = $LASTEXITCODE
    $steps.Add([pscustomobject]@{ Name = $Name; Executable = $Executable; Arguments = $Arguments; ExitCode = $code; ExpectedExit = $ExpectedExit })
    if ($code -ne $ExpectedExit) {
        Get-Content -LiteralPath $log -Tail 50 | Write-Host
        throw "$Name returned $code, expected $ExpectedExit. See $log"
    }
    Write-Host "$Name`: exit $code"
    return Read-BazelPlainLog $log
}

function Assert-SinglePassingTest([string] $Path) {
    [xml]$trx = Get-Content -LiteralPath $Path -Raw
    $counters = $trx.SelectSingleNode('//*[local-name()="Counters"]')
    $results = @($trx.SelectNodes('//*[local-name()="UnitTestResult"]'))
    if (-not $counters -or $results.Count -ne 1 -or $results[0].outcome -ne 'Passed' -or
        [int]$counters.total -ne 1 -or [int]$counters.executed -ne 1 -or
        [int]$counters.passed -ne 1 -or [int]$counters.failed -ne 0) {
        throw "Expected exactly one executed, passing test in $Path."
    }
}

function Get-ConsumerSdk {
    $sdkVersion = [regex]::Match((Get-Content -LiteralPath (Join-Path $root 'MODULE.bazel') -Raw), 'dotnet\.toolchain\(dotnet_version = "([^"]+)"\)').Groups[1].Value
    if (-not $sdkVersion) { throw 'Could not identify the declared consumer SDK.' }
    $toolchainOutput = Invoke-Step 'consumer-toolchain' $bazel @(
        'cquery', '@rules_dotnet//dotnet:resolved_toolchain', '--extra_toolchains=@dotnet_toolchains//:all',
        '--output=starlark', '--noshow_progress',
        '--starlark:expr="TESTFX_CONSUMER_SDK|" + providers(target)["ToolchainInfo"].dotnetinfo.sdk_version + "|" + providers(target)["ToolchainInfo"].dotnetinfo.runtime_path'
    )
    $toolchain = [regex]::Match($toolchainOutput, '(?m)^TESTFX_CONSUMER_SDK\|([^|\r\n]+)\|([^|\r\n]+)')
    if (-not $toolchain.Success -or $toolchain.Groups[1].Value -cne $sdkVersion -or -not $toolchain.Groups[2].Value.StartsWith('external/')) {
        throw 'The resolved consumer toolchain does not match the declared SDK repository.'
    }

    # A distinct consumer SDK is materialized here, but need not be staged as a producer action input.
    $outputBase = & $bazel info output_base
    if ($LASTEXITCODE -ne 0) { throw 'Could not locate the materialized SDK repositories.' }
    $dotnetName = if ($IsWindows) { 'dotnet.exe' } else { 'dotnet' }
    $sdks = @(Get-ChildItem -LiteralPath (Join-Path $outputBase 'external') -Directory -Filter "*dotnet_sdk.$sdkVersion`_*" |
        Where-Object { Test-Path -LiteralPath (Join-Path $_.FullName $dotnetName) })
    if ($sdks.Count -ne 1) { throw 'Expected one materialized consumer SDK for the execution host.' }
    $dotnet = Join-Path $sdks[0].FullName $dotnetName
    $resolvedDotnet = [IO.Path]::GetFullPath((Join-Path $outputBase $toolchain.Groups[2].Value))
    $comparison = if ($IsWindows) { [StringComparison]::OrdinalIgnoreCase } else { [StringComparison]::Ordinal }
    if (-not [string]::Equals([IO.Path]::GetFullPath($dotnet), $resolvedDotnet, $comparison)) {
        throw 'The materialized consumer SDK does not match the selected toolchain executable.'
    }
    $version = @(Get-Content -LiteralPath (Join-Path $sdks[0].FullName "sdk\$sdkVersion\.version"))
    if ($version.Count -lt 2 -or $version[1] -cne $sdkVersion) { throw 'The consumer SDK archive has unexpected version metadata.' }
    return [pscustomobject]@{ Version = $sdkVersion; Commit = $version[0]; Dotnet = $dotnet; Repository = $sdks[0].FullName }
}

$oldPackages = $env:NUGET_PACKAGES
$oldRollForward = $env:DOTNET_ROLL_FORWARD
$oldPrerelease = $env:DOTNET_ROLL_FORWARD_TO_PRERELEASE
$oldExpectations = $env:TESTFX_BAZEL_PACKAGE_EXPECTATIONS
$oldDotnetRoot = $env:DOTNET_ROOT
$oldDotnetRootX64 = $env:DOTNET_ROOT_X64
Push-Location $root
try {
    $revision = git rev-parse HEAD
    if ($LASTEXITCODE -ne 0) { throw 'Could not record the source revision.' }
    $workingTree = @(git status --porcelain)
    [xml]$versionDocument = Get-Content -LiteralPath (Join-Path $root 'eng\Versions.props') -Raw
    $versionProperties = @($versionDocument.SelectNodes('//*[local-name()="TestingPlatformVersionPrefix"]'))
    if ($versionProperties.Count -ne 1) { throw 'Expected one platform version prefix.' }
    $expectedPackageVersion = $versionProperties[0].InnerText + '-preview.bazel'
    $null = Invoke-Step 'pack' $bazel @('build', '//:packages', "--jobs=$Jobs", '--noshow_progress', '--show_result=0')
    $executionRoot = & $bazel info execution_root
    if ($LASTEXITCODE -ne 0) { throw 'Could not locate the native execution root.' }
    $paths = @(& $bazel cquery //:packages --output=files --noshow_progress)
    if ($LASTEXITCODE -ne 0 -or $paths.Count -ne $expectedPackageIds.Count) { throw 'Expected exactly three native MTP packages.' }

    $consumerSdk = Get-ConsumerSdk
    $sdkVersion = $consumerSdk.Version
    $dotnet = $consumerSdk.Dotnet

    foreach ($path in $paths) {
        $packagePath = Join-Path $executionRoot $path
        $archive = [System.IO.Compression.ZipFile]::OpenRead($packagePath)
        try {
            $manifests = @($archive.Entries | Where-Object FullName -Like '*.nuspec')
            if ($manifests.Count -ne 1) { throw "Expected one nuspec in $packagePath." }
            $reader = [System.IO.StreamReader]::new($manifests[0].Open())
            try { [xml]$manifest = $reader.ReadToEnd() } finally { $reader.Dispose() }
            $metadata = $manifest.SelectSingleNode('//*[local-name()="metadata"]')
            $id = $metadata.id
            $version = $metadata.version
            if ($id -notin $expectedPackageIds -or -not $seenPackages.Add($id)) {
                throw "Unexpected or duplicate native package: $id"
            }
            if ($version -cne $expectedPackageVersion -or -not $archive.GetEntry('PACKAGE.md')) {
                throw "Missing experimental version or package readme: $packagePath"
            }
            foreach ($framework in $frameworks) {
                $entry = $archive.GetEntry("lib/$framework/$id.dll")
                if (-not $entry) { throw "$id is missing its $framework implementation." }
                if ($framework -eq 'net11.0') {
                    $stream = $entry.Open()
                    try { $expectedHashes[$id] = [Convert]::ToHexString([System.Security.Cryptography.SHA256]::HashData($stream)) }
                    finally { $stream.Dispose() }
                }
            }
            if ($id -eq 'Microsoft.Testing.Platform' -and @($metadata.SelectNodes('.//*[local-name()="dependency"]')).Count) {
                throw 'The platform package must remain dependency-free.'
            }
            if ($id -ne 'Microsoft.Testing.Extensions.TrxReport.Abstractions') {
                foreach ($framework in $frameworks) {
                    if (-not $archive.GetEntry("buildTransitive/$framework/$id.props")) {
                        throw "$id is missing its $framework consumer build hook."
                    }
                }
            }
            Copy-Item -LiteralPath $packagePath -Destination (Join-Path $feed "$id.$version.nupkg")
            $packageRecords.Add([pscustomobject]@{ Id = $id; Version = $version; Sha256 = (Get-FileHash -LiteralPath $packagePath -Algorithm SHA256).Hash; Files = @($archive.Entries.FullName) })
        }
        finally { $archive.Dispose() }
    }
    $versions = @($packageRecords.Version | Sort-Object -Unique)
    if ($versions.Count -ne 1) { throw 'The native MTP packages are not version-aligned.' }

    $sample = Join-Path $root 'samples\public\TestingPlatformExamples\TestingPlatformExplorer'
    foreach ($file in Get-ChildItem -LiteralPath $sample -Recurse -File) {
        $relative = [System.IO.Path]::GetRelativePath($sample, $file.FullName)
        if ($file.Extension -notin @('.cs', '.csproj', '.json') -or $relative -match '(^|[\\/])(bin|obj)[\\/]') { continue }
        $destination = Join-Path $consumer $relative
        New-Item -ItemType Directory -Path (Split-Path $destination -Parent) -Force | Out-Null
        Copy-Item -LiteralPath $file.FullName -Destination $destination
    }
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'PackageProvenance.cs') -Destination $consumer
    foreach ($file in @('Directory.Build.props', 'Directory.Build.targets', 'Directory.Packages.props')) {
        '<Project />' | Set-Content -LiteralPath (Join-Path $consumer $file) -Encoding utf8
    }
    @{ sdk = @{ version = $sdkVersion; allowPrerelease = $true; rollForward = 'disable' } } |
        ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $consumer 'global.json') -Encoding utf8
    $escapedFeed = [System.Security.SecurityElement]::Escape($feed)
    @"
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources><clear /><add key="native" value="$escapedFeed" /><add key="nuget" value="https://api.nuget.org/v3/index.json" /></packageSources>
  <packageSourceMapping>
    <packageSource key="native"><package pattern="Microsoft.Testing.*" /></packageSource>
    <packageSource key="nuget"><package pattern="*" /></packageSource>
  </packageSourceMapping>
</configuration>
"@ | Set-Content -LiteralPath (Join-Path $consumer 'NuGet.config') -Encoding utf8
    $expectedHashes | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $directory 'expected-assemblies.json') -Encoding utf8
    $env:NUGET_PACKAGES = Join-Path $directory 'restored-packages'
    $env:DOTNET_ROLL_FORWARD = $null
    $env:DOTNET_ROLL_FORWARD_TO_PRERELEASE = $null
    $env:TESTFX_BAZEL_PACKAGE_EXPECTATIONS = Join-Path $directory 'expected-assemblies.json'

    Set-Location $consumer
    $projectPath = Join-Path $consumer 'TestingPlatformExplorer.csproj'
    [xml]$project = Get-Content -LiteralPath $projectPath -Raw
    $targetFramework = $project.SelectSingleNode('//*[local-name()="TargetFramework"]')
    if (-not $targetFramework) { throw 'The copied consumer does not declare a target framework.' }
    foreach ($framework in $frameworks) {
        $targetFramework.InnerText = $framework
        $project.Save($projectPath)
        $null = Invoke-Step "consumer-build-$framework" $dotnet @(
            'build', 'TestingPlatformExplorer.csproj', '--disable-build-servers', '-c', 'Release',
            "-p:MTPVersion=$($versions[0])",
            "-bl:$(Join-Path $directory "consumer-$framework.binlog")", '--nologo', '-v:quiet'
        )
        Copy-Item -LiteralPath (Join-Path $consumer 'obj\project.assets.json') -Destination (Join-Path $directory "assets-$framework.json")
    }

    $assembly = Join-Path $consumer 'bin\Release\net11.0\TestingPlatformExplorer.dll'
    $results = Join-Path $directory 'results with spaces!'
    $output = Invoke-Step 'consumer-pass' $dotnet @(
        $assembly, '--filter-uid', 'TestingPlatformExplorer.UnitTests.SomeTests.TestMethod1',
        '--report-trx', '--report-trx-filename', 'consumer.trx', '--results-directory', $results
    )
    if (-not $output.Contains('BAZEL_PACKAGE_RUNTIME: .NET 11.') -or -not $output.Contains('BAZEL_PACKAGE_ASSEMBLY: Microsoft.Testing.Platform;')) {
        throw 'The consumer did not report the expected runtime and native package provenance.'
    }
    Assert-SinglePassingTest (Join-Path $results 'consumer.trx')
    $failure = Invoke-Step 'consumer-failure' $dotnet @($assembly, '--filter-uid', 'TestingPlatformExplorer.UnitTests.SomeTests.TestMethod2', '--ansi', 'on') 2
    if ($failure -notmatch 'failed TestMethod2') { throw 'The packaged failing test did not execute.' }
    $empty = Invoke-Step 'consumer-empty' $dotnet @($assembly, '--filter-uid', 'NoMatchingTest', '--minimum-expected-tests', '1') 9
    if ($empty -notmatch 'tests ran 0, minimum expected 1') { throw 'The packaged zero-result policy did not execute.' }

    Set-Location $root
    $null = Invoke-Step 'publish' $bazel @('build', '//:published_samples', "--jobs=$Jobs", '--noshow_progress', '--show_result=0')
    $published = @(& $bazel cquery 'filter("explorer_net[0-9]+_0$", deps(//:published_samples))' --output=starlark '--starlark:expr=str(target.label) + "|" + providers(target)["DefaultInfo"].files_to_run.executable.path' --noshow_progress)
    if ($LASTEXITCODE -ne 0 -or $published.Count -ne 4) { throw 'Expected four native self-contained publishers.' }
    $env:DOTNET_ROOT = Join-Path $directory 'missing-runtime'
    $env:DOTNET_ROOT_X64 = $env:DOTNET_ROOT
    foreach ($publishedTarget in $published) {
        $parts = $publishedTarget.Split('|', 2)
        $framework = $parts[0].Split(':')[-1].Substring('explorer_'.Length).Replace('_', '.')
        if ($framework -notin $frameworks -or -not $seenRuntimes.Add($framework)) { throw "Unexpected or duplicate publisher framework: $framework" }
        $original = Join-Path $executionRoot $parts[1]
        $layout = Join-Path $directory "published-$framework"
        Copy-Item -LiteralPath (Split-Path $original -Parent) -Destination $layout -Recurse
        $executable = Join-Path $layout ([System.IO.Path]::GetFileName($original))
        $runtimeResults = Join-Path $layout 'results'
        $runtimeOutput = Invoke-Step "runtime-pass-$framework" $executable @(
            '--filter-uid', 'TestingPlatformExplorer.UnitTests.SomeTests.TestMethod1',
            '--report-trx', '--report-trx-filename', 'tests.trx', '--results-directory', $runtimeResults
        )
        $major = $framework.Substring(3).Split('.')[0]
        if ($runtimeOutput -notmatch "BAZEL_RUNTIME_VERIFIED: \.NET $major\.") {
            throw "The self-contained $framework host did not run on its own runtime."
        }
        $coreLib = [regex]::Match($runtimeOutput, '(?m)^BAZEL_CORELIB: ([^\r\n]+)').Groups[1].Value
        if (-not $coreLib -or [System.IO.Path]::GetRelativePath($layout, $coreLib) -ne 'System.Private.CoreLib.dll') {
            throw "The $framework host did not load CoreLib from its staged shipping layout."
        }
        Assert-SinglePassingTest (Join-Path $runtimeResults 'tests.trx')
        $runtimeFailure = Invoke-Step "runtime-failure-$framework" $executable @('--filter-uid', 'TestingPlatformExplorer.UnitTests.SomeTests.TestMethod2', '--ansi', 'on') 2
        if ($runtimeFailure -notmatch 'failed TestMethod2') { throw "The $framework failing test did not execute." }
        $runtimeEmpty = Invoke-Step "runtime-empty-$framework" $executable @('--filter-uid', 'NoMatchingTest', '--minimum-expected-tests', '1') 9
        if ($runtimeEmpty -notmatch 'tests ran 0, minimum expected 1') { throw "The $framework zero-result policy did not execute." }
        $runtimeRecords.Add([pscustomobject]@{ Framework = $framework; Executable = $executable; CoreLib = $coreLib; CoreLibSha256 = (Get-FileHash -LiteralPath $coreLib -Algorithm SHA256).Hash })
    }
    $succeeded = $true
    Write-Host "Native package consumers passed. Evidence: $directory"
}
finally {
    $env:NUGET_PACKAGES = $oldPackages
    $env:DOTNET_ROLL_FORWARD = $oldRollForward
    $env:DOTNET_ROLL_FORWARD_TO_PRERELEASE = $oldPrerelease
    $env:TESTFX_BAZEL_PACKAGE_EXPECTATIONS = $oldExpectations
    $env:DOTNET_ROOT = $oldDotnetRoot
    $env:DOTNET_ROOT_X64 = $oldDotnetRootX64
    Pop-Location
    [pscustomobject]@{ Passed = $succeeded; BaseRevision = $revision; WorkingTree = $workingTree; Jobs = $Jobs; ConsumerSdk = $consumerSdk; Packages = $packageRecords; RuntimeLayouts = $runtimeRecords; Steps = $steps; ExpectedAssemblyHashes = $expectedHashes } |
        ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $directory 'validation.json') -Encoding utf8
}

exit 0
