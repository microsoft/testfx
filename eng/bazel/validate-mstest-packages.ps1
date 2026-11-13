# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT license. See LICENSE file in the project root for full license information.

#requires -Version 7.2

[CmdletBinding()]
param(
    [string] $OutputRoot,
    [switch] $VerifySatelliteSensitivity,
    [switch] $IncludeWindowsPlatformPackages
)

$ErrorActionPreference = 'Stop'
$PSNativeCommandUseErrorActionPreference = $false
if ($IncludeWindowsPlatformPackages -and -not $IsWindows) { throw 'Native UI package validation requires Windows.' }
$root = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$bazel = Join-Path $root 'eng\bazel.ps1'
if (-not $OutputRoot) { $OutputRoot = Join-Path $root 'artifacts\bazel-mstest-validation' }
$directory = Join-Path ([System.IO.Path]::GetFullPath($OutputRoot)) ([DateTime]::UtcNow.ToString('yyyyMMdd-HHmmss') + '-' + [Guid]::NewGuid().ToString('N').Substring(0, 8))
$feed = Join-Path $directory 'feed'
New-Item -ItemType Directory -Path $feed -Force | Out-Null
$frameworks = @('net10.0', 'net11.0')
$coreFrameworks = @('netstandard2.0', 'net462') + $frameworks
$adapterFrameworks = if ($IsWindows) { @('net462') + $frameworks } else { $frameworks }
$platformFrameworks = @{
    'MSTest.Uwp' = 'net10.0-windows10.0.17763'
    'MSTest.WinUI' = 'net10.0-windows10.0.18362'
}
$portableIds = @(
    'MSTest.TestFramework', 'MSTest.TestAdapter', 'MSTest.Analyzers',
    'Microsoft.Testing.Platform', 'Microsoft.Testing.Platform.MSBuild',
    'Microsoft.Testing.Extensions.Telemetry', 'Microsoft.Testing.Extensions.TrxReport',
    'Microsoft.Testing.Extensions.TrxReport.Abstractions'
)
$expectedIds = $portableIds
if ($IncludeWindowsPlatformPackages) { $expectedIds += @('MSTest.Uwp', 'MSTest.WinUI') }
$passingTests = @('NativeAssembliesAreLoadedFromConsumer', 'KoreanSatellitesAreCopiedAndLoadable', 'PackagedTargetsProvideCoreAndParallelization')
$packages = @{}
$steps = [System.Collections.Generic.List[object]]::new()
$runs = [System.Collections.Generic.List[object]]::new()
$succeeded = $false

function Invoke-Step([string] $Name, [string] $Executable, [string[]] $Arguments, [int] $ExpectedExit = 0) {
    $log = Join-Path $directory "$Name.log"
    & $Executable @Arguments *> $log
    $code = $LASTEXITCODE
    $steps.Add([pscustomobject]@{ Name = $Name; Executable = $Executable; Arguments = $Arguments; ExitCode = $code; ExpectedExit = $ExpectedExit; Log = $log })
    if ($code -ne $ExpectedExit) {
        Get-Content -LiteralPath $log -Tail 60 | Write-Host
        throw "$Name returned $code, expected $ExpectedExit. See $log"
    }
    Write-Host "$Name`: exit $code"
    return Get-Content -LiteralPath $log -Raw
}

function Remove-ProcessEnvironmentVariable([string] $Name) {
    if (Test-Path -LiteralPath "Env:$Name") { Remove-Item -LiteralPath "Env:$Name" }
}

function Read-ZipText($Archive, [string] $Path) {
    $entry = $Archive.GetEntry($Path)
    if (-not $entry) { throw "Missing package entry: $Path" }
    $reader = [System.IO.StreamReader]::new($entry.Open())
    try { return $reader.ReadToEnd() } finally { $reader.Dispose() }
}

function Get-ZipHash($Archive, [string] $Path) {
    $entry = $Archive.GetEntry($Path)
    if (-not $entry) { throw "Missing package entry: $Path" }
    $stream = $entry.Open()
    try { return [Convert]::ToHexString([System.Security.Cryptography.SHA256]::HashData($stream)) }
    finally { $stream.Dispose() }
}

function Assert-Names([string[]] $Expected, [string[]] $Actual, [string] $Context) {
    if ($Expected.Count -ne $Actual.Count -or (Compare-Object ($Expected | Sort-Object) ($Actual | Sort-Object))) {
        throw "$Context expected [$($Expected -join ', ')], got [$($Actual -join ', ')]."
    }
}

function Assert-Trx([string] $Path, [string[]] $Expected, [int] $Failed = 0, [string] $FailureMessage = '') {
    [xml]$trx = Get-Content -LiteralPath $Path -Raw
    $counters = $trx.SelectSingleNode('//*[local-name()="Counters"]')
    $results = @($trx.SelectNodes('//*[local-name()="UnitTestResult"]'))
    Assert-Names $Expected @($results | ForEach-Object testName) $Path
    if (-not $counters -or [int]$counters.total -ne $Expected.Count -or [int]$counters.executed -ne $Expected.Count -or
        [int]$counters.passed -ne ($Expected.Count - $Failed) -or [int]$counters.failed -ne $Failed -or
        @($results | Where-Object outcome -EQ 'Passed').Count -ne ($Expected.Count - $Failed) -or
        @($results | Where-Object outcome -EQ 'Failed').Count -ne $Failed) {
        throw "Unexpected executed/pass/fail/skip counters: $Path"
    }
    if ($FailureMessage) {
        $messages = @($trx.SelectNodes('//*[local-name()="UnitTestResult" and @outcome="Failed"]//*[local-name()="ErrorInfo"]/*[local-name()="Message"]') | ForEach-Object InnerText)
        if (@($messages | Where-Object { $_.Contains($FailureMessage) }).Count -ne 1) {
            throw "The intended behavioral assertion did not fail: $Path"
        }
    }
}

function Assert-PackageLayout($Archive, $Metadata, [string] $Id) {
    $groups = @($Metadata.SelectNodes('./*[local-name()="dependencies"]/*[local-name()="group"]'))
    $packageFrameworks = switch ($Id) {
        'MSTest.TestFramework' { $coreFrameworks }
        'MSTest.TestAdapter' { $adapterFrameworks }
        { $platformFrameworks.ContainsKey($_) } { @($platformFrameworks[$Id]) }
        default { $frameworks }
    }
    if ($Id -in @('MSTest.TestFramework', 'MSTest.TestAdapter', 'Microsoft.Testing.Platform.MSBuild', 'Microsoft.Testing.Extensions.Telemetry', 'MSTest.Uwp', 'MSTest.WinUI')) {
        Assert-Names $packageFrameworks @($groups | ForEach-Object targetFramework) "$Id framework groups"
    }
    $allDependencies = @($Metadata.SelectNodes('.//*[local-name()="dependency"]'))
    foreach ($dependency in $allDependencies) {
        if ($dependency.id -in @('NotPublishable', 'MSTestAdapter.PlatformServices', 'MSTest.TestFramework.Extensions', 'Microsoft.Testing.Extensions.MSBuild')) {
            throw "$Id declares a private bundled assembly as package dependency: $($dependency.id)"
        }
    }
    if (@($Archive.Entries | Where-Object Name -Like 'MSTest.TestFramework.Extensions.*').Count) {
        throw "$Id contains a retired Extensions payload."
    }
    foreach ($framework in $packageFrameworks) {
        switch ($Id) {
            'MSTest.TestFramework' {
                foreach ($extension in @('dll', 'xml', 'pdb')) {
                    $null = Get-ZipHash $Archive "lib/$framework/MSTest.TestFramework.$extension"
                }
                $null = Get-ZipHash $Archive 'buildTransitive/MSTest.TestFramework.targets'
            }
            'MSTest.TestAdapter' {
                foreach ($name in @('MSTest.TestAdapter', 'MSTestAdapter.PlatformServices')) {
                    $null = Get-ZipHash $Archive "buildTransitive/$framework/$name.dll"
                }
                foreach ($extension in @('props', 'targets')) {
                    $null = Get-ZipHash $Archive "build/$framework/MSTest.TestAdapter.$extension"
                    $null = Get-ZipHash $Archive "buildTransitive/$framework/MSTest.TestAdapter.$extension"
                }
                $null = Get-ZipHash $Archive "buildTransitive/$framework/Parallelize.targets"
                if (@($Archive.Entries | Where-Object FullName -Like 'lib/*').Count) { throw 'The adapter must not expose a lib folder.' }
                if ($framework -eq 'net462') {
                    $null = Get-ZipHash $Archive 'buildTransitive/net462/MSTest.TestFramework.dll'
                }
            }
            { $platformFrameworks.ContainsKey($_) } {
                foreach ($name in @('MSTest.TestAdapter', 'MSTestAdapter.PlatformServices')) {
                    $null = Get-ZipHash $Archive "buildTransitive/$framework/$name.dll"
                }
                $null = Get-ZipHash $Archive "lib/$framework/$Id.dll"
                foreach ($extension in @('props', 'targets')) {
                    $null = Get-ZipHash $Archive "buildTransitive/$framework/$Id.$extension"
                }
                $null = Get-ZipHash $Archive "buildTransitive/$framework/Parallelize.targets"
                if (@($Archive.Entries | Where-Object FullName -Like 'buildTransitive/common/*').Count) {
                    throw "$Id must use package-local targets, not a shared wrapper directory."
                }
                if (@($Archive.Entries | Where-Object { $_.FullName -like '*/MSTest.TestFramework.dll' -or
                    $_.FullName -like 'lib/*/MSTest.Uwp.dll' -and $Id -ne 'MSTest.Uwp' -or
                    $_.FullName -like 'lib/*/MSTest.WinUI.dll' -and $Id -ne 'MSTest.WinUI' }).Count) {
                    throw "$Id must depend on shared core and isolate its own UI assembly."
                }
            }
            'Microsoft.Testing.Platform.MSBuild' {
                $null = Get-ZipHash $Archive "lib/$framework/Microsoft.Testing.Extensions.MSBuild.dll"
                $null = Get-ZipHash $Archive '_MSBuildTasks/netstandard2.0/Microsoft.Testing.Platform.MSBuild.dll'
                $null = Get-ZipHash $Archive '_MSBuildTasks/netstandard2.0/Microsoft.Testing.Platform.dll'
                if ($IsWindows) {
                    foreach ($companion in @('Microsoft.Win32.Registry', 'System.Security.AccessControl', 'System.Security.Principal.Windows')) {
                        $null = Get-ZipHash $Archive "_MSBuildTasks/netstandard2.0/$companion.dll"
                    }
                }
                foreach ($hostAssembly in @('Microsoft.Build.Framework', 'Microsoft.Build.Utilities.Core')) {
                    if ($Archive.GetEntry("_MSBuildTasks/netstandard2.0/$hostAssembly.dll")) { throw "The MSBuild host must supply $hostAssembly, not a packed reference assembly." }
                }
                foreach ($area in @('build', 'buildTransitive', 'buildMultiTargeting')) {
                    $null = Get-ZipHash $Archive "$area/Microsoft.Testing.Platform.MSBuild.props"
                    $null = Get-ZipHash $Archive "$area/Microsoft.Testing.Platform.MSBuild.targets"
                }
            }
            'Microsoft.Testing.Extensions.Telemetry' {
                $null = Get-ZipHash $Archive "lib/$framework/Microsoft.Testing.Extensions.Telemetry.dll"
                $null = Get-ZipHash $Archive "build/$framework/Microsoft.Testing.Extensions.Telemetry.props"
                $null = Get-ZipHash $Archive "buildTransitive/$framework/Microsoft.Testing.Extensions.Telemetry.props"
            }
        }
    }
    $catalogs = switch ($Id) {
        'MSTest.TestFramework' {
            @{ Directory = 'src\TestFramework\TestFramework\Resources\xlf'; Stem = 'FrameworkMessages'; Assembly = 'MSTest.TestFramework'; Folders = @($coreFrameworks | ForEach-Object { "lib/$_" }) }
        }
        'MSTest.TestAdapter' {
            @{ Directory = 'src\Adapter\MSTest.TestAdapter\Resources\xlf'; Stem = 'PlatformAdapterResources'; Assembly = 'MSTest.TestAdapter'; Folders = @('buildTransitive/_localization') }
            @{ Directory = 'src\Adapter\MSTestAdapter.PlatformServices\Resources\xlf'; Stem = 'Resource'; Assembly = 'MSTestAdapter.PlatformServices'; Folders = @('buildTransitive/_localization') }
        }
        { $platformFrameworks.ContainsKey($_) } {
            $folder = "buildTransitive/$($platformFrameworks[$Id])/_localization"
            @{ Directory = 'src\Adapter\MSTest.TestAdapter\Resources\xlf'; Stem = 'PlatformAdapterResources'; Assembly = 'MSTest.TestAdapter'; Folders = @($folder) }
            @{ Directory = 'src\Adapter\MSTestAdapter.PlatformServices\Resources\xlf'; Stem = 'Resource'; Assembly = 'MSTestAdapter.PlatformServices'; Folders = @($folder) }
        }
        'MSTest.WinUI' {
            @{ Directory = 'src\TestFramework\MSTest.WinUI\Resources\xlf'; Stem = 'FrameworkExtensionsMessages'; Assembly = 'MSTest.WinUI'; Folders = @("lib/$($platformFrameworks[$Id])") }
        }
    }
    foreach ($catalog in $catalogs) {
        foreach ($source in Get-ChildItem -LiteralPath (Join-Path $root $catalog.Directory) -Filter "$($catalog.Stem).*.xlf" -File) {
            $culture = $source.BaseName.Substring($catalog.Stem.Length + 1)
            foreach ($folder in $catalog.Folders) { $null = Get-ZipHash $Archive "$folder/$culture/$($catalog.Assembly).resources.dll" }
        }
    }
    if ($Id -eq 'MSTest.TestAdapter' -and @($Archive.Entries | Where-Object FullName -Match '(^|/)(winui|uwp|uap[^/]*|net\d+\.\d+-windows[^/]*)(/|$)').Count) {
        throw 'The ordinary adapter must not ship UI flavor payloads or build assets.'
    }
    foreach ($group in $groups) {
        $dependencies = @($group.SelectNodes('./*[local-name()="dependency"]'))
        switch ($Id) {
            'MSTest.TestFramework' {
                Assert-Names @('MSTest.Analyzers') @($dependencies | ForEach-Object id) "$Id dependencies"
                if ($dependencies[0].exclude -cne 'Compile,Runtime' -or $dependencies[0].version -cne $mstestVersion) {
                    throw 'The framework must retain native analyzer/build assets and exclude only compile/runtime.'
                }
            }
            { $_ -in @('MSTest.TestAdapter', 'MSTest.Uwp', 'MSTest.WinUI') } {
                $ids = @('Microsoft.Testing.Extensions.TrxReport.Abstractions', 'Microsoft.Testing.Extensions.Telemetry', 'Microsoft.Testing.Platform.MSBuild', 'Microsoft.TestPlatform.ObjectModel')
                if ($IsWindows -or $Id -ne 'MSTest.TestAdapter') { $ids += 'MSTest.TestFramework' }
                if ($group.targetFramework -eq 'net462') { $ids += @('System.Memory', 'System.Threading.Tasks.Extensions', 'System.Reflection.Metadata') }
                Assert-Names $ids @($dependencies | ForEach-Object id) "$Id dependencies"
                if ($platformFrameworks.ContainsKey($Id)) {
                    $core = @($dependencies | Where-Object id -CEQ 'MSTest.TestFramework')
                    if ($core.Count -ne 1 -or $core[0].version -cne "[$mstestVersion]") {
                        throw "$Id must pin the matching core package version exactly."
                    }
                }
                foreach ($dependency in $dependencies | Where-Object { $_.id -ne 'Microsoft.TestPlatform.ObjectModel' -and $_.id -notlike 'System.*' }) {
                    if ($dependency.HasAttribute('exclude')) { throw "The adapter must retain build/analyzer assets from $($dependency.id)." }
                }
            }
            { $_ -in @('Microsoft.Testing.Platform.MSBuild', 'Microsoft.Testing.Extensions.Telemetry') } {
                $platform = @($dependencies | Where-Object id -EQ 'Microsoft.Testing.Platform')
                $nextMajor = ([version]($mtpVersion.Split('-')[0])).Major + 1
                if ($platform.Count -ne 1 -or $platform[0].version -cne "[$mtpVersion, $nextMajor.0.0)") {
                    throw "$Id lost its platform compatibility upper bound."
                }
            }
        }
    }
}

function New-Expectations([string] $Framework, [string] $Path) {
    $assemblies = [ordered]@{}
    foreach ($id in $portableIds | Where-Object { $_ -ne 'MSTest.Analyzers' }) {
        $archive = [System.IO.Compression.ZipFile]::OpenRead($packages[$id].Path)
        try {
            $names = switch ($id) {
                'MSTest.TestFramework' { @("lib/$Framework/MSTest.TestFramework.dll") }
                'MSTest.TestAdapter' { @("buildTransitive/$Framework/MSTest.TestAdapter.dll", "buildTransitive/$Framework/MSTestAdapter.PlatformServices.dll") }
                'Microsoft.Testing.Platform.MSBuild' { @("lib/$Framework/Microsoft.Testing.Extensions.MSBuild.dll") }
                default { @("lib/$Framework/$id.dll") }
            }
            foreach ($name in $names) { $assemblies[[System.IO.Path]::GetFileNameWithoutExtension($name)] = Get-ZipHash $archive $name }
        }
        finally { $archive.Dispose() }
    }
    $resources = @(
        @{ Assembly = 'MSTest.TestFramework'; Package = 'MSTest.TestFramework'; Folder = "lib/$Framework"; Source = 'src\TestFramework\TestFramework\Resources'; Stem = 'FrameworkMessages'; BaseName = 'Microsoft.VisualStudio.TestTools.UnitTesting.Resources.FrameworkMessages'; Key = 'AccessStringInvalidSyntax' },
        @{ Assembly = 'MSTestAdapter.PlatformServices'; Package = 'MSTest.TestAdapter'; Folder = 'buildTransitive/_localization'; Source = 'src\Adapter\MSTestAdapter.PlatformServices\Resources'; Stem = 'Resource'; BaseName = 'Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Resources.Resource'; Key = 'CannotFindFile' },
        @{ Assembly = 'MSTest.TestAdapter'; Package = 'MSTest.TestAdapter'; Folder = 'buildTransitive/_localization'; Source = 'src\Adapter\MSTest.TestAdapter\Resources'; Stem = 'PlatformAdapterResources'; BaseName = 'Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Resources.PlatformAdapterResources'; Key = 'MSTestExtensionDescription' }
    )
    foreach ($resource in $resources) {
        [xml]$catalog = Get-Content -LiteralPath (Join-Path $root "$($resource.Source)\xlf\$($resource.Stem).ko.xlf") -Raw
        $target = $catalog.SelectSingleNode("//*[local-name()='trans-unit' and @id='$($resource.Key)']/*[local-name()='target']")
        if (-not $target -or -not $target.InnerText) { throw "The source catalog lacks $($resource.Key)." }
        $resource.Value = $target.InnerText
        $archive = [System.IO.Compression.ZipFile]::OpenRead($packages[$resource.Package].Path)
        try { $resource.Sha256 = Get-ZipHash $archive "$($resource.Folder)/ko/$($resource.Assembly).resources.dll" }
        finally { $archive.Dispose() }
    }
    @{ Framework = $Framework; Assemblies = $assemblies; Resources = $resources } |
        ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $Path -Encoding utf8
}

function New-Consumer([string] $Name, [string] $Framework, [string] $PackageFeed) {
    $consumer = Join-Path $directory $Name
    New-Item -ItemType Directory -Path $consumer | Out-Null
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'mstest-consumer\MSTestPackageConsumer.csproj'), (Join-Path $PSScriptRoot 'mstest-consumer\NativePackageTests.cs') -Destination $consumer
    foreach ($file in @('Directory.Build.props', 'Directory.Build.targets', 'Directory.Packages.props')) {
        '<Project />' | Set-Content -LiteralPath (Join-Path $consumer $file) -Encoding utf8
    }
    @{ sdk = @{ version = $sdkVersion; allowPrerelease = $true; rollForward = 'disable' } } |
        ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $consumer 'global.json') -Encoding utf8
    $escapedFeed = [System.Security.SecurityElement]::Escape($PackageFeed)
    @"
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources><clear /><add key="native" value="$escapedFeed" /><add key="nuget" value="https://api.nuget.org/v3/index.json" /></packageSources>
  <fallbackPackageFolders><clear /></fallbackPackageFolders>
  <packageSourceMapping>
    <packageSource key="native"><package pattern="MSTest.*" /><package pattern="Microsoft.Testing.*" /></packageSource>
    <packageSource key="nuget"><package pattern="*" /></packageSource>
  </packageSourceMapping>
</configuration>
"@ | Set-Content -LiteralPath (Join-Path $consumer 'NuGet.config') -Encoding utf8
    $projectPath = Join-Path $consumer 'MSTestPackageConsumer.csproj'
    [xml]$project = Get-Content -LiteralPath $projectPath -Raw
    $project.SelectSingleNode('//*[local-name()="TargetFramework"]').InnerText = $Framework
    $project.Save($projectPath)
    return $consumer
}

function Build-Consumer([string] $Name, [string] $Framework, [string] $PackageFeed, [string] $Cache) {
    $consumer = New-Consumer $Name $Framework $PackageFeed
    $env:NUGET_PACKAGES = $Cache
    $publish = Join-Path $consumer 'published'
    Push-Location $consumer
    try {
        $null = Invoke-Step "$Name-publish" $dotnet @(
            'publish', 'MSTestPackageConsumer.csproj', '--disable-build-servers', '-c', 'Release',
            '--self-contained', 'true', '-r', $rid, '-o', $publish,
            "-p:MSTestVersion=$mstestVersion", "-p:MTPVersion=$mtpVersion",
            "-p:RestoreConfigFile=$(Join-Path $consumer 'NuGet.config')",
            "-bl:$(Join-Path $directory "$Name.binlog")", '--nologo', '-v:quiet'
        )
    }
    finally { Pop-Location }
    $assetsPath = Join-Path $consumer 'obj\project.assets.json'
    $assets = Get-Content -LiteralPath $assetsPath -Raw | ConvertFrom-Json -AsHashtable
    Assert-Names @([System.IO.Path]::TrimEndingDirectorySeparator($Cache)) @($assets.packageFolders.Keys | ForEach-Object { [System.IO.Path]::TrimEndingDirectorySeparator($_) }) "$Name restore folders"
    $resolved = @($assets.libraries.Keys | Where-Object { $_ -match '^(MSTest\.|Microsoft\.Testing\.)' })
    Assert-Names @($portableIds | ForEach-Object { "$_/$($packages[$_].Version)" }) $resolved "$Name native package closure"
    foreach ($id in $portableIds) {
        $package = $packages[$id]
        $cached = Join-Path $Cache ($id.ToLowerInvariant() + '\' + $package.Version + '\' + $id.ToLowerInvariant() + '.' + $package.Version + '.nupkg')
        $staged = Join-Path $PackageFeed "$id.$($package.Version).nupkg"
        if ((Get-FileHash -LiteralPath $cached -Algorithm SHA256).Hash -cne (Get-FileHash -LiteralPath $staged -Algorithm SHA256).Hash) {
            throw "$Name restored stale or non-native package contents: $id"
        }
    }
    $generated = @(Get-ChildItem -LiteralPath (Join-Path $consumer 'obj') -Recurse -File -Filter '*.cs')
    $registration = @($generated | Select-String -SimpleMatch 'global::Microsoft.VisualStudio.TestTools.UnitTesting.TestingPlatformBuilderHook.AddExtensions')
    if (-not $registration.Count) { throw "$Name did not generate the packaged MSTest builder registration." }
    return [pscustomobject]@{ Name = $Name; Framework = $Framework; Directory = $consumer; Published = $publish; Executable = (Join-Path $publish "MSTestPackageConsumer$appExtension"); Assets = $assetsPath }
}

function Run-Consumer($Consumer) {
    $name = $Consumer.Name
    $expectations = Join-Path $Consumer.Directory 'expectations.json'
    New-Expectations $Consumer.Framework $expectations
    $env:TESTFX_BAZEL_MSTEST_EXPECTATIONS = $expectations
    $env:TESTFX_BAZEL_CONSUMER_EVIDENCE = Join-Path $Consumer.Directory 'loaded-assemblies.paths'
    $previousEvidence = if (Test-Path -LiteralPath $env:TESTFX_BAZEL_CONSUMER_EVIDENCE) { @(Get-Content -LiteralPath $env:TESTFX_BAZEL_CONSUMER_EVIDENCE).Count } else { 0 }
    $env:DOTNET_ROOT = Join-Path $directory 'missing-runtime'
    $env:DOTNET_ROOT_X64 = $env:DOTNET_ROOT
    $discovery = Invoke-Step "$name-discovery" $Consumer.Executable @('--list-tests')
    $plainDiscovery = [regex]::Replace($discovery, '\x1B\[[0-?]*[ -/]*[@-~]', '')
    $allTests = $passingTests + 'IntentionalFailure'
    $discoveredNames = @($plainDiscovery -split '\r?\n' | ForEach-Object { $_.Trim() } | Where-Object { $_ -in $allTests })
    Assert-Names $allTests $discoveredNames "$name discovery"
    if ($plainDiscovery -notmatch 'Test discovery summary: found 4 test\(s\)') { throw "$name did not discover exactly four tests." }
    $results = Join-Path $Consumer.Directory 'results with spaces!'
    $null = Invoke-Step "$name-pass" $Consumer.Executable @(
        '--filter', 'FullyQualifiedName!~IntentionalFailure', '--minimum-expected-tests', '3',
        '--report-trx', '--report-trx-filename', 'pass.trx', '--results-directory', $results
    )
    Assert-Trx (Join-Path $results 'pass.trx') $passingTests
    $evidencePaths = @(Get-Content -LiteralPath $env:TESTFX_BAZEL_CONSUMER_EVIDENCE)
    if ($evidencePaths.Count -ne $previousEvidence + 1) { throw "$name did not append exactly one consumer provenance path." }
    $provenancePath = $evidencePaths[-1]
    $provenance = Get-Content -LiteralPath $provenancePath -Raw | ConvertFrom-Json
    $major = $Consumer.Framework.Substring(3).Split('.')[0]
    if ($provenance.Runtime -notlike ".NET $major.*" -or [System.IO.Path]::GetRelativePath($Consumer.Published, $provenance.CoreLib) -cne 'System.Private.CoreLib.dll') {
        throw "$name did not execute on its self-contained $($Consumer.Framework) runtime."
    }
    $null = Invoke-Step "$name-failure" $Consumer.Executable @(
        '--filter', 'FullyQualifiedName~IntentionalFailure', '--report-trx',
        '--report-trx-filename', 'failure.trx', '--results-directory', $results
    ) 2
    Assert-Trx (Join-Path $results 'failure.trx') @('IntentionalFailure') 1 'BAZEL_INTENTIONAL_FAILURE'
    $empty = Invoke-Step "$name-empty" $Consumer.Executable @('--filter', 'FullyQualifiedName~NoSuchPackageCase', '--minimum-expected-tests', '1') 9
    if ($empty -notmatch 'tests ran 0, minimum expected 1') { throw "$name did not execute the zero-result policy." }
    $runs.Add([pscustomobject]@{ Consumer = $Consumer; Runtime = $provenance.Runtime; Provenance = $provenancePath; ProvenanceIndex = $env:TESTFX_BAZEL_CONSUMER_EVIDENCE; Results = $results; Passed = $true })
}

function Run-MSBuildConsumer($Consumer) {
    $results = Join-Path $Consumer.Directory 'msbuild results with spaces!'
    $env:DOTNET_ROOT = $sdks[0].FullName
    $env:DOTNET_ROOT_X64 = $env:DOTNET_ROOT
    foreach ($scenario in @(
        @{ Name = 'pass'; Filter = 'FullyQualifiedName!~IntentionalFailure'; Exit = 0; Tests = $passingTests; Failed = 0; Message = '' },
        @{ Name = 'failure'; Filter = 'FullyQualifiedName~IntentionalFailure'; Exit = 1; Tests = @('IntentionalFailure'); Failed = 1; Message = 'BAZEL_INTENTIONAL_FAILURE' }
    )) {
        $name = "msbuild-$($scenario.Name)"
        $previousEvidence = @(Get-Content -LiteralPath $env:TESTFX_BAZEL_CONSUMER_EVIDENCE).Count
        $arguments = "--filter $($scenario.Filter) --report-trx --report-trx-filename $name.trx --results-directory `"$results`""
        Push-Location $Consumer.Directory
        try {
            $null = Invoke-Step $name $dotnet @(
                'msbuild', 'MSTestPackageConsumer.csproj', '-t:InvokeTestingPlatform', '-p:Configuration=Release',
                "-p:RuntimeIdentifier=$rid", "-p:MSTestVersion=$mstestVersion", "-p:MTPVersion=$mtpVersion",
                "-p:TestingPlatformExecutablePath=$($Consumer.Executable)", "-p:TestingPlatformCommandLineArguments=$($arguments.Replace('"', '%22'))",
                "-bl:$(Join-Path $directory "$name.binlog")", '-nologo', '-v:minimal'
            ) $scenario.Exit
        }
        finally { Pop-Location }
        Assert-Trx (Join-Path $results "$name.trx") $scenario.Tests $scenario.Failed $scenario.Message
        $evidencePaths = @(Get-Content -LiteralPath $env:TESTFX_BAZEL_CONSUMER_EVIDENCE)
        $expectedEvidence = $previousEvidence + $(if ($scenario.Name -eq 'pass') { 1 } else { 0 })
        if ($evidencePaths.Count -ne $expectedEvidence) { throw "$name did not preserve append-only consumer provenance." }
    }
    $runs.Add([pscustomobject]@{ Consumer = $Consumer; Driver = 'Native Platform.MSBuild InvokeTestingPlatform'; ParentExits = @(0, 1); ProvenanceIndex = $env:TESTFX_BAZEL_CONSUMER_EVIDENCE; Provenance = $evidencePaths; Results = $results; Passed = $true })
}

$environmentNames = @(
    'NUGET_PACKAGES', 'DOTNET_ROOT', 'DOTNET_ROOT_X64', 'DOTNET_ROLL_FORWARD', 'DOTNET_ROLL_FORWARD_TO_PRERELEASE',
    'MSBuildSDKsPath', 'MSBuildExtensionsPath', 'MSBUILD_EXE_PATH', 'MSBUILDDISABLENODEREUSE', 'TESTFX_BAZEL_MSTEST_EXPECTATIONS',
    'TESTFX_BAZEL_CONSUMER_EVIDENCE', 'TESTINGPLATFORM_UI_LANGUAGE', 'TESTINGPLATFORM_TELEMETRY_OPTOUT', 'DOTNET_CLI_TELEMETRY_OPTOUT'
)
$previousEnvironment = @{}
foreach ($name in $environmentNames) {
    $present = Test-Path -LiteralPath "Env:$name"
    $previousEnvironment[$name] = @{
        Present = $present
        Value = if ($present) { (Get-Item -LiteralPath "Env:$name").Value } else { $null }
    }
}
Push-Location $root
try {
    $revision = git rev-parse HEAD
    if ($LASTEXITCODE -ne 0) { throw 'Could not record the source revision.' }
    $workingTree = @(git status --porcelain)
    [xml]$versions = Get-Content -LiteralPath (Join-Path $root 'eng\Versions.props') -Raw
    $mstestVersion = $versions.SelectSingleNode('//*[local-name()="VersionPrefix"]').InnerText + '-preview.bazel'
    $mtpVersion = $versions.SelectSingleNode('//*[local-name()="TestingPlatformVersionPrefix"]').InnerText + '-preview.bazel'
    $packageClosure = if ($IncludeWindowsPlatformPackages) { '//:mstest_platform_package_closure' } else { '//:mstest_package_closure' }
    $null = Invoke-Step 'native-pack' $bazel @('build', $packageClosure, '--jobs=2', '--noshow_progress', '--show_result=0')
    $executionRoot = & $bazel info execution_root
    if ($LASTEXITCODE -ne 0) { throw 'Could not locate the native execution root.' }
    $paths = @(& $bazel cquery $packageClosure --output=files --noshow_progress)
    if ($LASTEXITCODE -ne 0 -or $paths.Count -ne $expectedIds.Count) { throw "Expected $($expectedIds.Count) native packages in the MSTest validation closure." }
    foreach ($path in $paths) {
        $packagePath = Join-Path $executionRoot $path
        $archive = [System.IO.Compression.ZipFile]::OpenRead($packagePath)
        try {
            $manifests = @($archive.Entries | Where-Object FullName -Like '*.nuspec')
            if ($manifests.Count -ne 1) { throw "Expected one manifest: $packagePath" }
            [xml]$manifest = Read-ZipText $archive $manifests[0].FullName
            $metadata = $manifest.SelectSingleNode('//*[local-name()="metadata"]')
            $id = [string]$metadata.id
            $version = [string]$metadata.version
            if ($id -notin $expectedIds -or $packages.ContainsKey($id)) { throw "Unexpected or duplicate package: $id" }
            $expectedVersion = if ($id.StartsWith('MSTest.', [StringComparison]::Ordinal)) { $mstestVersion } else { $mtpVersion }
            if ($version -cne $expectedVersion) { throw "Unexpected package version: $id $version" }
            $null = Read-ZipText $archive 'PACKAGE.md'
            Assert-PackageLayout $archive $metadata $id
            $requestPath = Join-Path (Split-Path $packagePath -Parent) 'nuget_pack_request.json'
            if (Test-Path -LiteralPath $requestPath) {
                $request = Get-Content -LiteralPath $requestPath -Raw | ConvertFrom-Json
                foreach ($file in $request.files) {
                    $source = Join-Path $executionRoot $file.source
                    if ((Get-ZipHash $archive $file.target) -cne (Get-FileHash -LiteralPath $source -Algorithm SHA256).Hash) {
                        throw "$id differs from its declared native input: $($file.target)"
                    }
                }
            }
            elseif ($id -ne 'MSTest.Analyzers') { throw "Missing native payload request for $id." }
            Copy-Item -LiteralPath $packagePath -Destination (Join-Path $feed "$id.$version.nupkg")
            $packages[$id] = [pscustomobject]@{ Id = $id; Version = $version; Path = $packagePath; Request = $requestPath; Sha256 = (Get-FileHash -LiteralPath $packagePath -Algorithm SHA256).Hash; Files = @($archive.Entries.FullName) }
        }
        finally { $archive.Dispose() }
    }
    Assert-Names $expectedIds @($packages.Keys) 'Native feed'
    if ($IsWindows) {
        $coreArchive = [System.IO.Compression.ZipFile]::OpenRead($packages['MSTest.TestFramework'].Path)
        $adapterArchive = $null
        try {
            $adapterArchive = [System.IO.Compression.ZipFile]::OpenRead($packages['MSTest.TestAdapter'].Path)
            if ((Get-ZipHash $adapterArchive 'buildTransitive/net462/MSTest.TestFramework.dll') -cne
                (Get-ZipHash $coreArchive 'lib/net462/MSTest.TestFramework.dll')) {
                throw 'The net462 VSTest resolver copy must match ordinary net462 core.'
            }
        }
        finally {
            $coreArchive.Dispose()
            if ($adapterArchive) { $adapterArchive.Dispose() }
        }
    }

    $sdkVersion = [regex]::Match((Get-Content -LiteralPath (Join-Path $root 'MODULE.bazel') -Raw), 'dotnet\.toolchain\(dotnet_version = "([^"]+)"\)').Groups[1].Value
    if (-not $sdkVersion) { throw 'Could not identify the native toolchain SDK.' }
    $dotnetName = if ($IsWindows) { 'dotnet.exe' } else { 'dotnet' }
    $sdks = @(Get-ChildItem -LiteralPath (Join-Path $executionRoot 'external') -Directory -Filter "*dotnet_sdk.$sdkVersion`_*" |
        Where-Object { Test-Path -LiteralPath (Join-Path $_.FullName $dotnetName) })
    if ($sdks.Count -ne 1) { throw 'Expected one native SDK for this execution host.' }
    $dotnet = Join-Path $sdks[0].FullName $dotnetName
    $rid = if ($IsWindows) { 'win-x64' } elseif ($IsMacOS) { 'osx-arm64' } else { 'linux-x64' }
    $appExtension = if ($IsWindows) { '.exe' } else { '' }
    foreach ($name in @('DOTNET_ROLL_FORWARD', 'DOTNET_ROLL_FORWARD_TO_PRERELEASE', 'MSBuildSDKsPath', 'MSBuildExtensionsPath', 'MSBUILD_EXE_PATH')) {
        Remove-ProcessEnvironmentVariable $name
    }
    $env:MSBUILDDISABLENODEREUSE = '1'
    $env:TESTINGPLATFORM_UI_LANGUAGE = 'en-US'
    $env:TESTINGPLATFORM_TELEMETRY_OPTOUT = '1'
    $env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
    $cache = Join-Path $directory 'restored-packages'
    foreach ($framework in $frameworks) {
        $env:DOTNET_ROOT = $sdks[0].FullName
        $env:DOTNET_ROOT_X64 = $env:DOTNET_ROOT
        $consumer = Build-Consumer "consumer-$framework" $framework $feed $cache
        Run-Consumer $consumer
        if ($framework -eq 'net11.0') { Run-MSBuildConsumer $consumer }
    }

    if ($VerifySatelliteSensitivity) {
        $mutationDirectory = Join-Path $directory 'satellite-counterfactual'
        $mutationFeed = Join-Path $mutationDirectory 'feed'
        New-Item -ItemType Directory -Path $mutationFeed -Force | Out-Null
        Get-ChildItem -LiteralPath $feed -File |
            Where-Object Name -NE "MSTest.TestAdapter.$mstestVersion.nupkg" |
            Copy-Item -Destination $mutationFeed
        $request = Get-Content -LiteralPath $packages['MSTest.TestAdapter'].Request -Raw | ConvertFrom-Json
        $removed = 'buildTransitive/_localization/ko/MSTest.TestAdapter.resources.dll'
        if (@($request.files | Where-Object target -EQ $removed).Count -ne 1) { throw 'The counterfactual must remove exactly one adapter satellite.' }
        $request.files = @($request.files | Where-Object target -NE $removed)
        foreach ($file in $request.files) { $file.source = Join-Path $executionRoot $file.source }
        $request.output = Join-Path $mutationFeed "MSTest.TestAdapter.$mstestVersion.nupkg"
        $request.nuspecOutput = Join-Path $mutationDirectory 'MSTest.TestAdapter.nuspec'
        $request.versionFile = Join-Path $executionRoot $request.versionFile
        foreach ($group in $request.dependencyGroups) {
            foreach ($dependency in $group.dependencies) {
                if ($dependency.versionFile) { $dependency.versionFile = Join-Path $executionRoot $dependency.versionFile }
            }
        }
        $mutationRequest = Join-Path $mutationDirectory 'nuget_pack_request.json'
        $request | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath $mutationRequest -Encoding utf8
        $null = Invoke-Step 'counterfactual-pack' $bazel @('run', '@rules_dotnet//dotnet/private/tools/nuget_pack', '--jobs=2', '--noshow_progress', '--', $mutationRequest)
        $env:DOTNET_ROOT = $sdks[0].FullName
        $env:DOTNET_ROOT_X64 = $env:DOTNET_ROOT
        $counterfactual = Build-Consumer 'counterfactual-net11.0' 'net11.0' $mutationFeed (Join-Path $mutationDirectory 'restored-packages')
        $expectations = Join-Path $counterfactual.Directory 'expectations.json'
        New-Expectations 'net11.0' $expectations
        $env:TESTFX_BAZEL_MSTEST_EXPECTATIONS = $expectations
        $env:TESTFX_BAZEL_CONSUMER_EVIDENCE = Join-Path $counterfactual.Directory 'loaded-assemblies.paths'
        $results = Join-Path $counterfactual.Directory 'results'
        $null = Invoke-Step 'counterfactual-execution' $counterfactual.Executable @(
            '--filter', 'FullyQualifiedName!~IntentionalFailure', '--minimum-expected-tests', '3',
            '--report-trx', '--report-trx-filename', 'counterfactual.trx', '--results-directory', $results
        ) 2
        Assert-Trx (Join-Path $results 'counterfactual.trx') $passingTests 1 'Missing native Korean satellite: MSTest.TestAdapter.'
        $runs.Add([pscustomobject]@{ Consumer = $counterfactual; Mutation = $removed; RequestSha256 = (Get-FileHash -LiteralPath $mutationRequest -Algorithm SHA256).Hash; Detected = $true; Results = $results })
        $env:DOTNET_ROOT = $sdks[0].FullName
        $env:DOTNET_ROOT_X64 = $env:DOTNET_ROOT
        $restored = Build-Consumer 'restored-net11.0' 'net11.0' $feed (Join-Path $directory 'restored-green-packages')
        Run-Consumer $restored
    }
    $succeeded = $true
    Write-Host "Native MSTest ordinary package consumers passed. Optional UI packages cover modern native layouts, not classic UAP production or UI activation. Evidence: $directory"
}
finally {
    foreach ($name in $environmentNames) {
        $previous = $previousEnvironment[$name]
        if ($previous.Present) { Set-Item -LiteralPath "Env:$name" -Value $previous.Value }
        else { Remove-ProcessEnvironmentVariable $name }
    }
    Pop-Location
    [pscustomobject]@{ Passed = $succeeded; IncludeWindowsPlatformPackages = [bool]$IncludeWindowsPlatformPackages; ClassicUapProduced = $false; UIActivationValidated = $false; BaseRevision = $revision; WorkingTree = $workingTree; Packages = @($packages.Values); Runs = $runs; Steps = $steps } |
        ConvertTo-Json -Depth 10 | Set-Content -LiteralPath (Join-Path $directory 'validation.json') -Encoding utf8
}

exit 0
