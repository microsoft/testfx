# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT license. See LICENSE file in the project root for full license information.

#Requires -Version 7.0
<#
.SYNOPSIS
Builds real shipping packages and tests an isolated, non-shipping MSTest package split.
.DESCRIPTION
Run from this worktree only. Outputs are retained in a fresh artifacts/package-split directory.
Never run concurrently with another producer build in the same worktree.
No global cache eviction or production-file edits are performed. Native builds are opt-in.
Native prerequisite checks are read-only; machine settings such as Developer Mode are never changed.
ModernUwpLaunch additionally attempts activation and removes only its unique AppX registration.
Use short RunRoot/ProbeName values for native runs: Windows native loaders have path limits.
#>
[CmdletBinding()]
param(
    [ValidateSet('Pack', 'Probe')]
    [string] $Stage = 'Pack',
    [string] $RunRoot,
    [ValidatePattern('^(?!\.{1,2}$)[a-zA-Z0-9._-]+$')]
    [string] $ProbeName = ('probes-' + [Guid]::NewGuid().ToString('N')),
    [switch] $NativeBuild,
    [switch] $ModernUwpLaunch,
    [string[]] $Only = @()
)

Set-StrictMode -Version 3.0
$ErrorActionPreference = 'Stop'
if ($ModernUwpLaunch) { $NativeBuild = $true }
$repository = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
if (-not $RunRoot) {
    $RunRoot = Join-Path $repository ('artifacts/package-split/' + [Guid]::NewGuid().ToString('N'))
}
$RunRoot = [IO.Path]::GetFullPath($RunRoot)
if (-not $RunRoot.StartsWith((Join-Path $repository 'artifacts/package-split/'), [StringComparison]::OrdinalIgnoreCase)) {
    throw 'RunRoot must be under this worktree artifacts/package-split/.'
}

function Write-Utf8([string] $Path, [string] $Text) {
    [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($Path)) | Out-Null
    [IO.File]::WriteAllText($Path, $Text, [Text.UTF8Encoding]::new($Path.EndsWith('.cs')))
}

function Invoke-Recorded([string] $Name, [string] $Exe, [string[]] $Arguments, [string] $WorkingDirectory, [hashtable] $Environment = @{}) {
    $start = [Diagnostics.ProcessStartInfo]::new()
    $start.FileName = $Exe
    $start.WorkingDirectory = $WorkingDirectory
    $start.UseShellExecute = $false
    $start.RedirectStandardOutput = $true
    $start.RedirectStandardError = $true
    foreach ($arg in $Arguments) { $start.ArgumentList.Add($arg) }
    foreach ($key in $Environment.Keys) {
        if ($null -eq $Environment[$key]) { $start.Environment.Remove($key) | Out-Null }
        else { $start.Environment[$key] = $Environment[$key] }
    }
    $process = [Diagnostics.Process]::new()
    $process.StartInfo = $start
    $started = [DateTimeOffset]::UtcNow
    $process.Start() | Out-Null
    $stdout = $process.StandardOutput.ReadToEndAsync()
    $stderr = $process.StandardError.ReadToEndAsync()
    try {
        if (-not $process.WaitForExit(1800000)) {
            $process.Kill($true)
            $process.WaitForExit()
            throw "Timed out: $Name (owned PID $($process.Id))"
        }
        $text = $stdout.GetAwaiter().GetResult() + $stderr.GetAwaiter().GetResult()
        $exit = $process.ExitCode
        Write-Utf8 "$RunRoot/logs/$Name.log" $text
        Write-Utf8 "$RunRoot/logs/$Name.command.json" (@{
            executable = $Exe; arguments = $Arguments; cwd = $WorkingDirectory
            environment = $Environment; exit = $exit; started = $started.ToString('o')
        } | ConvertTo-Json -Depth 6)
        Write-Host "$Name exit=$exit"
        if ($exit -ne 0) { Write-Host (($text -split '\r?\n' | Select-Object -Last 18) -join [Environment]::NewLine) }
        return @{ Exit = $exit; Output = $text }
    }
    finally { $process.Dispose() }
}

if ($Stage -eq 'Pack') {
    if (Test-Path $RunRoot) { throw "Pack requires a fresh RunRoot: $RunRoot" }
    [IO.Directory]::CreateDirectory($RunRoot) | Out-Null
    $seeds = @(
        'src/TestFramework/MSTest.WinUI/MSTest.WinUI.csproj',
        'src/TestFramework/MSTest.Uwp/MSTest.Uwp.csproj',
        'src/Adapter/MSTest.TestAdapter/MSTest.TestAdapter.csproj',
        'src/Package/MSTest.Sdk/MSTest.Sdk.csproj',
        'src/Package/MSTest/MSTest.csproj',
        'src/Analyzers/MSTest.SourceGeneration/MSTest.SourceGeneration.csproj',
        # These are solution-level/build-order dependencies rather than ProjectReferences.
        'src/Analyzers/MSTest.GlobalConfigsGenerator/MSTest.GlobalConfigsGenerator.csproj',
        'src/TestFramework/TestFramework.Compatibility/TestFramework.Compatibility.csproj',
        # The repository package-shape gate requires every MSTest package family.
        'src/Adapter/MSTest.Extensions.Hosting/MSTest.Extensions.Hosting.csproj',
        'src/TestFramework/TestFramework.Windows.UIAutomation/TestFramework.Windows.UIAutomation.csproj'
    )
    $projects = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
    function Add-Project([string] $Path) {
        $Path = [IO.Path]::GetFullPath($Path)
        if (-not $projects.Add($Path)) { return }
        [xml] $xml = Get-Content -Raw -LiteralPath $Path
        # Also follow shared props containing producer dependencies.
        $texts = @($xml.OuterXml)
        foreach ($import in $xml.SelectNodes('//Import[@Project]')) {
            $candidate = $import.Project.Replace('$(RepoRoot)', "$repository/")
            if ($candidate.Contains('$(')) { continue }
            if (-not [IO.Path]::IsPathRooted($candidate)) { $candidate = Join-Path (Split-Path $Path) $candidate }
            if (Test-Path $candidate) { $texts += Get-Content -Raw -LiteralPath $candidate }
        }
        foreach ($text in $texts) {
            foreach ($match in [regex]::Matches($text, '<ProjectReference\s+Include="([^"]+)"')) {
                $reference = $match.Groups[1].Value.Replace('$(RepoRoot)', "$repository/")
                if ($reference.Contains('$(')) { throw "Unresolved producer reference: $reference" }
                if (-not [IO.Path]::IsPathRooted($reference)) { $reference = Join-Path (Split-Path $Path) $reference }
                Add-Project $reference
            }
        }
    }
    foreach ($seed in $seeds) { Add-Project (Join-Path $repository $seed) }
    # Dependencies imported by PlatformPackage.props and adapter PackageDependencies.props.
    foreach ($file in Get-ChildItem "$repository/src/Platform" -Directory) {
        if ($file.Name -in @('Microsoft.Testing.Extensions.TrxReport.Abstractions', 'Microsoft.Testing.Extensions.Telemetry',
                'Microsoft.Testing.Platform.MSBuild', 'Microsoft.Testing.Extensions.MSBuild', 'Microsoft.Testing.Extensions.TrxReport',
                'Microsoft.Testing.Extensions.Retry')) {
            Add-Project (Join-Path $file.FullName ($file.Name + '.csproj'))
        }
    }
    $entries = @($projects | Sort-Object | ForEach-Object {
        $path = [Security.SecurityElement]::Escape($_)
        if ($_ -like '*MSTest.Analyzers.Package.csproj') {
            '  <Project Path="' + $path + '"><BuildDependency Project="' +
                [Security.SecurityElement]::Escape((Join-Path $repository 'src/Analyzers/MSTest.GlobalConfigsGenerator/MSTest.GlobalConfigsGenerator.csproj')) + '" /></Project>'
        }
        else { '  <Project Path="' + $path + '" />' }
    })
    Write-Utf8 "$RunRoot/producers.slnx" ("<Solution>`n" + ($entries -join "`n") + "`n</Solution>")
    # Stop parent Directory.Build discovery for the generated solution itself.
    Write-Utf8 "$RunRoot/Directory.Build.props" '<Project />'
    Write-Utf8 "$RunRoot/Directory.Build.targets" '<Project />'
    $head = (& git -C $repository rev-parse HEAD).Trim()
    Write-Utf8 "$RunRoot/source.json" (@{ head = $head; projects = @($projects | Sort-Object); sdk = (Get-Content "$repository/global.json" -Raw | ConvertFrom-Json).sdk.version } | ConvertTo-Json -Depth 5)
    $result = Invoke-Recorded 'producer-pack' (Join-Path $PSHOME 'pwsh.exe') @('-NoProfile', '-Command',
        "& './Build.cmd' -pack -c Release -projects '$RunRoot/producers.slnx' -bl; exit `$LASTEXITCODE") $repository
    if ($result.Exit -ne 0) { throw "Producer pack failed; see $RunRoot/logs/producer-pack.log" }
    Copy-Item "$repository/artifacts/log/Release/Build.binlog" "$RunRoot/producer.binlog"
    [IO.Directory]::CreateDirectory("$RunRoot/producer-feed") | Out-Null
    $snapshot = @(Get-ChildItem "$repository/artifacts/packages/Release/Shipping" -Filter '*.nupkg' | ForEach-Object {
        Copy-Item $_.FullName "$RunRoot/producer-feed/"
        @{ file = $_.Name; sha256 = (Get-FileHash $_.FullName).Hash; produced = $_.LastWriteTimeUtc.ToString('o') }
    })
    if ($snapshot.Count -eq 0) { throw 'Producer pack returned no shipping packages.' }
    Write-Utf8 "$RunRoot/producer-packages.json" (ConvertTo-Json -InputObject $snapshot -Depth 4)
    Write-Host "Pack complete: $RunRoot"
    return
}

$packResult = Get-Content "$RunRoot/logs/producer-pack.command.json" -Raw | ConvertFrom-Json
if ($packResult.exit -ne 0) { throw 'A successful producer pack is required before probing.' }
$producerRoot = $RunRoot
$RunRoot = Join-Path $RunRoot $ProbeName
if (Test-Path $RunRoot) { throw "Probe output must be fresh: $RunRoot" }
[IO.Directory]::CreateDirectory($RunRoot) | Out-Null
Write-Utf8 "$RunRoot/harness-provenance.json" (@{
    harness = (Get-FileHash $PSCommandPath).Hash
    head = (& git -C $repository rev-parse HEAD).Trim()
    producer = $producerRoot
    selected = $Only
    nativeBuild = $NativeBuild.IsPresent
    modernUwpLaunch = $ModernUwpLaunch.IsPresent
} | ConvertTo-Json -Depth 4)
$dotnet = Join-Path $repository '.dotnet/dotnet.exe'
$sdkVersion = (Get-Content "$repository/global.json" -Raw | ConvertFrom-Json).sdk.version
$packages = @{}
$provenance = [Collections.Generic.List[object]]::new()
$results = [Collections.Generic.List[object]]::new()
$dllNames = @('MSTest.TestAdapter.dll', 'MSTestAdapter.PlatformServices.dll')
$tfm = 'net10.0-windows10.0.26100.0'

function Assert-Contract([bool] $Condition, [string] $Message) {
    if (-not $Condition) { throw "ASSERTION: $Message" }
}

function Read-Package([string] $Path) {
    $files = @{}
    $archive = [IO.Compression.ZipFile]::OpenRead($Path)
    try {
        foreach ($entry in $archive.Entries) {
            if ($entry.Name -eq '') { continue }
            $stream = $entry.Open()
            $buffer = [IO.MemoryStream]::new()
            try { $stream.CopyTo($buffer); $files[$entry.FullName] = $buffer.ToArray() }
            finally { $stream.Dispose(); $buffer.Dispose() }
        }
    }
    finally { $archive.Dispose() }
    $specName = @($files.Keys | Where-Object { $_ -like '*.nuspec' })
    if ($specName.Count -ne 1) { throw "Expected one nuspec in $Path" }
    [xml] $spec = [Text.Encoding]::UTF8.GetString($files[$specName[0]]).TrimStart([char]0xFEFF)
    return @{ Files = $files; Spec = $spec; Id = [string]$spec.package.metadata.id; Version = [string]$spec.package.metadata.version }
}

function Save-Package([string] $Feed, [string] $Id, [xml] $Spec, [hashtable] $Files) {
    $Spec.package.metadata.id = $Id
    $Spec.package.metadata.description = "LOCAL EXPERIMENT ONLY. $($Spec.package.metadata.description)"
    $version = [string]$Spec.package.metadata.version
    $path = "$Feed/$Id.$version.nupkg"
    [IO.Directory]::CreateDirectory($Feed) | Out-Null
    $archive = [IO.Compression.ZipFile]::Open($path, [IO.Compression.ZipArchiveMode]::Create)
    try {
        $payload = @{}
        foreach ($key in $Files.Keys) {
            if ($key -match '\.nuspec$|^_rels/|^package/|^\.signature') { continue }
            $payload[$key] = $Files[$key]
        }
        $payload["$Id.nuspec"] = [Text.Encoding]::UTF8.GetBytes($Spec.OuterXml)
        $payload['_rels/.rels'] = [Text.Encoding]::UTF8.GetBytes(
            '<Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships"><Relationship Type="http://schemas.microsoft.com/packaging/2010/07/manifest" Target="/' + $Id + '.nuspec" Id="R1" /></Relationships>')
        foreach ($key in @($payload.Keys | Sort-Object)) {
            $entry = $archive.CreateEntry($key)
            $stream = $entry.Open()
            try {
                [byte[]] $bytes = $payload[$key]
                $stream.Write($bytes, 0, $bytes.Length)
            }
            finally { $stream.Dispose() }
        }
    }
    finally { $archive.Dispose() }
    return $path
}

function Get-DependencyGroups([xml] $Spec) {
    return @($Spec.SelectNodes('//*[local-name()="dependencies"]/*[local-name()="group"]'))
}

function Set-Dependencies([xml] $Spec, [string[]] $Ids) {
    foreach ($group in (Get-DependencyGroups $Spec)) {
        while ($group.HasChildNodes) { $group.RemoveChild($group.FirstChild) | Out-Null }
        foreach ($id in $Ids) {
            $node = $Spec.CreateElement('dependency', $group.NamespaceURI)
            $node.SetAttribute('id', $id)
            $node.SetAttribute('version', "[$version]")
            $group.AppendChild($node) | Out-Null
        }
    }
}

function Convert-AdapterTargets([byte[]] $Bytes) {
    [xml] $xml = [Text.Encoding]::UTF8.GetString($Bytes).TrimStart([char]0xFEFF)
    foreach ($node in @($xml.SelectNodes('//Reference[@Include="MSTest.TestAdapter" or @Include="MSTestAdapter.PlatformServices"]'))) {
        $node.ParentNode.RemoveChild($node) | Out-Null
    }
    foreach ($name in @('GetMSTestV2CultureHierarchy', 'CopyMSTestV2Resources', '_RegisterMSTestV2ResourcesForClean')) {
        foreach ($node in @($xml.SelectNodes("//Target[@Name='$name']"))) { $node.ParentNode.RemoveChild($node) | Out-Null }
    }
    # Rename composition identities, not NuGet-selected compile/runtime items.
    $text = $xml.OuterXml.Replace("'MSTest.Uwp'", "'MSTest.TestAdapter.Uwp'").Replace("'MSTest.WinUI'", "'MSTest.TestAdapter.WinUI'")
    return [Text.Encoding]::UTF8.GetBytes($text)
}

# Use the immutable pack-stage snapshot, never whatever a later build left in artifacts/packages.
$snapshot = Get-Content "$producerRoot/producer-packages.json" -Raw | ConvertFrom-Json
foreach ($record in $snapshot) {
    $file = Get-Item "$producerRoot/producer-feed/$($record.file)"
    Assert-Contract ((Get-FileHash $file.FullName).Hash -eq $record.sha256) "Producer snapshot changed: $($record.file)"
    $package = Read-Package $file.FullName
    if ($packages.ContainsKey($package.Id)) { throw "Ambiguous producer version for $($package.Id)" }
    $packages[$package.Id] = $package
    [IO.Directory]::CreateDirectory("$RunRoot/baseline/feed") | Out-Null
    [IO.Directory]::CreateDirectory("$RunRoot/candidate/feed") | Out-Null
    Copy-Item $file.FullName "$RunRoot/baseline/feed/$($file.Name)"
    $provenance.Add(@{ kind = 'producer'; id = $package.Id; version = $package.Version; path = $file.FullName; sha256 = (Get-FileHash $file.FullName).Hash })
}
foreach ($required in @('MSTest.TestFramework', 'MSTest.TestAdapter', 'MSTest.Uwp', 'MSTest.WinUI', 'MSTest.Sdk', 'MSTest.SourceGeneration', 'Microsoft.Testing.Platform', 'Microsoft.Testing.Extensions.TrxReport')) {
    if (-not $packages.ContainsKey($required)) { throw "Required current pack missing: $required" }
}
$version = $packages['MSTest.TestFramework'].Version
$mtpVersion = $packages['Microsoft.Testing.Platform'].Version
foreach ($id in $packages.Keys) {
    if ($id -notin @('MSTest.TestAdapter', 'MSTest.Uwp', 'MSTest.WinUI')) {
        Copy-Item "$RunRoot/baseline/feed/$id.$($packages[$id].Version).nupkg" "$RunRoot/candidate/feed/"
    }
}

foreach ($flavor in @('Ordinary', 'WinUI', 'Uwp')) {
    $sourceId = if ($flavor -eq 'Ordinary') { 'MSTest.TestAdapter' } else { "MSTest.$flavor" }
    $adapterId = if ($flavor -eq 'Ordinary') { $sourceId } else { "MSTest.TestAdapter.$flavor" }
    $frameworkId = if ($flavor -eq 'Ordinary') { 'MSTest.TestFramework' } else { "MSTest.TestFramework.$flavor" }
    $source = $packages[$sourceId]
    [xml] $adapterSpec = $source.Spec.OuterXml
    $adapterFiles = @{}
    foreach ($key in $source.Files.Keys) {
        if ($key -notmatch '^(lib|build|buildTransitive|tools)/') { $adapterFiles[$key] = $source.Files[$key]; continue }
        if ($key -match '^buildTransitive/([^/]+)/(MSTest\.TestAdapter|MSTestAdapter\.PlatformServices)\.dll$') {
            $adapterFiles["lib/$($Matches[1])/$($Matches[2]).dll"] = $source.Files[$key]
        }
        elseif ($key -match '^buildTransitive/([^/]+)/_localization/(.+\.dll)$') {
            $adapterFiles["lib/$($Matches[1])/$($Matches[2])"] = $source.Files[$key]
        }
        elseif ($key -match '^buildTransitive/([^/]+)/([^/]+)\.(props|targets)$') {
            $folder = $Matches[1]; $name = $Matches[2]; $extension = $Matches[3]
            $newName = if ($name -eq $sourceId) { $adapterId } else { $name }
            $bytes = $source.Files[$key]
            if ($extension -eq 'targets' -and $name -eq $sourceId) { $bytes = Convert-AdapterTargets $bytes }
            if ($extension -eq 'props') {
                $text = [Text.Encoding]::UTF8.GetString($bytes).Replace("'MSTest.Uwp'", "'MSTest.TestAdapter.Uwp'").Replace("'MSTest.WinUI'", "'MSTest.TestAdapter.WinUI'")
                $bytes = [Text.Encoding]::UTF8.GetBytes($text)
            }
            $adapterFiles["buildTransitive/$folder/$newName.$extension"] = $bytes
        }
    }
    if ($flavor -eq 'Ordinary') {
        $frameworks = @($adapterFiles.Keys | Where-Object { $_ -match '^lib/[^/]+/MSTest.TestAdapter.dll$' } | ForEach-Object { $_.Split('/')[1] })
        foreach ($key in $source.Files.Keys) {
            if ($key -match '^buildTransitive/_localization/(.+\.dll)$') {
                foreach ($framework in $frameworks) { $adapterFiles["lib/$framework/$($Matches[1])"] = $source.Files[$key] }
            }
        }
    }
    foreach ($dep in $adapterSpec.SelectNodes('//*[local-name()="dependency"]')) {
        $id = $dep.GetAttribute('id')
        if ($id -eq 'MSTest.TestFramework') { $dep.SetAttribute('id', $frameworkId); $dep.SetAttribute('version', "[$version]") }
        elseif ($packages.ContainsKey($id)) { $dep.SetAttribute('version', "[$($packages[$id].Version)]") }
    }
    $null = Save-Package "$RunRoot/candidate/feed" $adapterId $adapterSpec $adapterFiles
    Assert-Contract (@($adapterFiles.Keys | Where-Object { $_ -match '^(build|buildTransitive|tools)/.*\.dll$' }).Count -eq 0) "$adapterId contains non-lib DLLs"
    if ($flavor -ne 'Ordinary') {
        [xml] $frameworkSpec = $source.Spec.OuterXml
        Set-Dependencies $frameworkSpec @('MSTest.TestFramework')
        $frameworkFiles = @{}
        foreach ($key in $source.Files.Keys) {
            if ($key -notmatch '^(build|buildTransitive|tools)/') { $frameworkFiles[$key] = $source.Files[$key] }
        }
        $null = Save-Package "$RunRoot/candidate/feed" $frameworkId $frameworkSpec $frameworkFiles
        [xml] $metaSpec = $source.Spec.OuterXml
        Set-Dependencies $metaSpec @($frameworkId, $adapterId)
        $metaFiles = @{}
        foreach ($key in $source.Files.Keys) {
            if ($key -notmatch '^(lib|build|buildTransitive|tools)/') { $metaFiles[$key] = $source.Files[$key] }
        }
        $null = Save-Package "$RunRoot/candidate/feed" $sourceId $metaSpec $metaFiles
    }
}

foreach ($variant in @('baseline', 'candidate')) {
    $feed = "$RunRoot/$variant/feed"
    $mapping = [Collections.Generic.List[string]]::new()
    foreach ($file in Get-ChildItem $feed -Filter '*.nupkg') {
        $package = Read-Package $file.FullName
        $mapping.Add("<package pattern=`"$($package.Id)`" />")
        $provenance.Add(@{ kind = $variant; id = $package.Id; version = $package.Version; path = $file.FullName; sha256 = (Get-FileHash $file.FullName).Hash })
    }
    [xml] $config = Get-Content "$repository/NuGet.config" -Raw
    $source = $config.CreateElement('add'); $source.SetAttribute('key', 'experiment'); $source.SetAttribute('value', $feed)
    $config.configuration.packageSources.AppendChild($source) | Out-Null
    $sourceMap = $config.CreateElement('packageSource'); $sourceMap.SetAttribute('key', 'experiment')
    $sourceMap.InnerXml = $mapping -join ''
    $config.configuration.packageSourceMapping.AppendChild($sourceMap) | Out-Null
    Write-Utf8 "$RunRoot/$variant/NuGet.config" $config.OuterXml
    Write-Utf8 "$RunRoot/$variant/Directory.Build.props" '<Project />'
    Write-Utf8 "$RunRoot/$variant/Directory.Build.targets" '<Project />'
    Write-Utf8 "$RunRoot/$variant/Directory.Packages.props" '<Project />'
    Write-Utf8 "$RunRoot/$variant/global.json" (@{ sdk = @{ version = $sdkVersion; rollForward = 'disable'; allowPrerelease = $true }; test = @{ runner = 'VSTest' } } | ConvertTo-Json -Depth 4)
}
Write-Utf8 "$RunRoot/package-provenance.json" ($provenance | ConvertTo-Json -Depth 5)
$sdk = Invoke-Recorded 'sdk-version' $dotnet @('--version') "$RunRoot/candidate"
Assert-Contract ($sdk.Exit -eq 0 -and $sdk.Output.Trim() -eq $sdkVersion) 'Pinned SDK mismatch'

$diagnosticTarget = @'
<Target Name="CaptureSplitEvidence" AfterTargets="Build">
  <PropertyGroup><SplitHooks>@(TestingPlatformBuilderHook)</SplitHooks></PropertyGroup>
  <WriteLinesToFile File="$(MSBuildProjectDirectory)/references.txt" Lines="@(ReferencePath->'%(FullPath)')" Overwrite="true" />
  <WriteLinesToFile File="$(MSBuildProjectDirectory)/copy-local.txt" Lines="@(ReferenceCopyLocalPaths->'%(FullPath)')" Overwrite="true" />
  <WriteLinesToFile File="$(MSBuildProjectDirectory)/properties.txt" Lines="OutputType=$(OutputType);Hooks=$(SplitHooks);Sdk=$(NETCoreSdkVersion);SDKs=$(MSBuildSDKsPath);Tools=$(MSBuildToolsPath);TestApplication=$(IsTestApplication)" Overwrite="true" />
</Target>
'@
$testSource = @'
using Microsoft.VisualStudio.TestTools.UnitTesting;
[TestClass]
public sealed class PackageTests
{
    [TestMethod]
    public void FrameworkAssemblyIdentity() => Assert.AreEqual("MSTest.TestFramework", typeof(Assert).Assembly.GetName().Name);
}
'@

function New-Consumer([string] $Variant, [string] $Name, [string] $References, [string] $Properties = '', [string] $Source = '', [bool] $Sdk = $false) {
    $directory = "$RunRoot/$Variant/$Name"
    $sdkName = if ($Sdk) { "MSTest.Sdk/$version" } else { 'Microsoft.NET.Sdk' }
    Write-Utf8 "$directory/Probe.csproj" @"
<Project Sdk="$sdkName">
  <PropertyGroup>
    <TargetFramework>$tfm</TargetFramework>
    <OutputType>Library</OutputType>
    <EnableWindowsTargeting>true</EnableWindowsTargeting>
    <CopyLocalLockFileAssemblies>true</CopyLocalLockFileAssemblies>
    <GenerateDependencyFile>true</GenerateDependencyFile>
    $Properties
  </PropertyGroup>
  <ItemGroup>$References</ItemGroup>
  $diagnosticTarget
</Project>
"@
    if (-not $Source) { $Source = $testSource }
    Write-Utf8 "$directory/Tests.cs" $Source
    return $directory
}

function Invoke-Consumer([string] $Variant, [string] $Name, [string] $Directory, [string] $Verb = 'build') {
    $environment = @{
        NUGET_PACKAGES = "$RunRoot/$Variant/packages"
        DOTNET_ROOT = (Split-Path $dotnet); DOTNET_ROOT_X64 = (Split-Path $dotnet)
        MSBuildSDKsPath = $null; MSBuildExtensionsPath = $null
        DOTNET_CLI_UI_LANGUAGE = 'fr'; TESTINGPLATFORM_TELEMETRY_OPTOUT = '1'
    }
    $arguments = @($Verb, 'Probe.csproj', '-c', 'Release', "--no-incremental", "-bl:$RunRoot/logs/$Name-$Verb.binlog")
    if ($Verb -eq 'publish') { $arguments = @('publish', 'Probe.csproj', '-c', 'Release', '-o', "$Directory/published", "-bl:$RunRoot/logs/$Name-publish.binlog") }
    $result = Invoke-Recorded "$Name-$Verb" $dotnet $arguments $Directory $environment
    if ($result.Exit -ne 0) { throw "SETUP: $Verb exit $($result.Exit); see $Name-$Verb.log" }
    return (Assert-ConsumerProvenance $Variant $Directory)
}

function Assert-ConsumerProvenance([string] $Variant, [string] $Directory) {
    # Verify the effective cache, exact versions and copied nupkg hashes, not just feed configuration.
    $assets = Get-Content "$Directory/obj/project.assets.json" -Raw | ConvertFrom-Json -AsHashtable
    Assert-Contract ($assets.packageFolders.ContainsKey("$RunRoot/$Variant/packages/".Replace('\', '/')) -or
        @($assets.packageFolders.Keys | Where-Object { [IO.Path]::GetFullPath($_).TrimEnd('\', '/') -eq [IO.Path]::GetFullPath("$RunRoot/$Variant/packages").TrimEnd('\', '/') }).Count -eq 1) 'Unexpected effective package cache'
    foreach ($key in $assets.libraries.Keys) {
        $id, $resolvedVersion = $key.Split('/')
        if (-not $packages.ContainsKey($id) -and $id -notlike 'MSTest.TestAdapter.*' -and $id -notlike 'MSTest.TestFramework.*') { continue }
        $feedPath = "$RunRoot/$Variant/feed/$id.$resolvedVersion.nupkg"
        Assert-Contract (Test-Path $feedPath) "Resolved unexpected local version $key"
        $cached = "$RunRoot/$Variant/packages/$($id.ToLowerInvariant())/$resolvedVersion/$($id.ToLowerInvariant()).$resolvedVersion.nupkg"
        Assert-Contract ((Get-FileHash $cached).Hash -eq (Get-FileHash $feedPath).Hash) "Stale package bytes: $key"
    }
    return $assets
}

function Invoke-Probe([string] $Name, [scriptblock] $Body, [string] $ExpectedFailure = '') {
    if ($Only.Count -ne 0 -and $Name -notin $Only) { return }
    try {
        & $Body
        if ($ExpectedFailure) { throw "COUNTERFACTUAL: expected defect was not detected ($ExpectedFailure)" }
        $results.Add(@{ name = $Name; outcome = 'Passed'; diagnostic = '' })
        Write-Host "PASS $Name"
    }
    catch {
        $diagnostic = $_.Exception.Message
        $expected = $ExpectedFailure -and $diagnostic.StartsWith('ASSERTION:') -and $diagnostic.Contains($ExpectedFailure)
        $outcome = if ($expected) { 'ExpectedBehavioralFailure' } elseif ($diagnostic.StartsWith('SETUP:')) { 'SetupFailure' } else { 'Failed' }
        $results.Add(@{ name = $Name; outcome = $outcome; diagnostic = $diagnostic; location = $_.ScriptStackTrace })
        Write-Host "$outcome $Name : $diagnostic"
    }
    finally { Write-Utf8 "$RunRoot/results.json" (ConvertTo-Json -InputObject @($results.ToArray()) -Depth 6) }
}

function Assert-CleanFramework([string] $Directory, [hashtable] $Assets) {
    $bad = @($Assets.libraries.Keys | Where-Object { $_ -match '^(MSTest.TestAdapter|Microsoft.Testing\.)' })
    $files = @(Get-ChildItem "$Directory/bin/Release/$tfm" -Recurse -Filter '*.dll' | Where-Object { $_.Name -match '^(MSTest.TestAdapter|MSTestAdapter.PlatformServices|Microsoft.Testing\.)' })
    Assert-Contract ($bad.Count -eq 0 -and $files.Count -eq 0) "framework-only runtime is contaminated: packages=$($bad -join ','); files=$(($files | ForEach-Object Name) -join ',')"
    $properties = Get-Content "$Directory/properties.txt" -Raw
    Assert-Contract ($properties -match 'OutputType=Library' -and $properties -match 'Hooks=\s*[\r\n]') "Framework changed library/hook behavior: $properties"
}

foreach ($flavor in @('Ordinary', 'WinUI', 'Uwp')) {
    $name = "FrameworkOnly$flavor"
    Invoke-Probe $name {
        $id = if ($flavor -eq 'Ordinary') { 'MSTest.TestFramework' } else { "MSTest.TestFramework.$flavor" }
        $source = if ($flavor -eq 'Ordinary') { $testSource } else { $testSource.Replace('[TestMethod]', '[Microsoft.VisualStudio.TestTools.UnitTesting.AppContainer.UITestMethod]') }
        $dir = New-Consumer candidate $name "<PackageReference Include=`"$id`" Version=`"$version`" />" '' $source
        $assets = Invoke-Consumer candidate $name $dir
        Assert-CleanFramework $dir $assets
    }
}
foreach ($flavor in @('WinUI', 'Uwp')) {
    $name = "BaselineFrameworkOnly$flavor"
    Invoke-Probe $name {
        $source = $testSource.Replace('[TestMethod]', '[Microsoft.VisualStudio.TestTools.UnitTesting.AppContainer.UITestMethod]')
        $dir = New-Consumer baseline $name "<PackageReference Include=`"MSTest.$flavor`" Version=`"$version`" />" '' $source
        $assets = Invoke-Consumer baseline $name $dir
        Assert-CleanFramework $dir $assets
    } 'framework-only runtime is contaminated'
}

function Assert-AdapterAssets([string] $Directory, [hashtable] $Assets, [string] $Id, [bool] $Compile = $true, [bool] $Runtime = $true, [bool] $French = $false) {
    $target = $Assets.targets.Values | Select-Object -First 1
    $entry = $target["$Id/$version"]
    Assert-Contract ($null -ne $entry) "Missing adapter package $Id"
    $compilePaths = @(if ($entry.ContainsKey('compile')) { $entry.compile.Keys | Where-Object { [IO.Path]::GetFileName($_) -in $dllNames } })
    $runtimePaths = @(if ($entry.ContainsKey('runtime')) { $entry.runtime.Keys | Where-Object { [IO.Path]::GetFileName($_) -in $dllNames } })
    Assert-Contract ($compilePaths.Count -eq $(if ($Compile) { 2 } else { 0 })) "Compile count $($compilePaths.Count), expected $Compile"
    Assert-Contract ($runtimePaths.Count -eq $(if ($Runtime) { 2 } else { 0 })) "Runtime count $($runtimePaths.Count), expected $Runtime"
    $cachedRoot = "$RunRoot/candidate/packages/$($Id.ToLowerInvariant())/$version"
    $sourceId = switch ($Id) { 'MSTest.TestAdapter.WinUI' { 'MSTest.WinUI' }; 'MSTest.TestAdapter.Uwp' { 'MSTest.Uwp' }; default { 'MSTest.TestAdapter' } }
    $sourceTfm = switch ($Id) { 'MSTest.TestAdapter.WinUI' { 'net10.0-windows10.0.18362' }; 'MSTest.TestAdapter.Uwp' { 'net10.0-windows10.0.17763' }; default { 'net10.0' } }
    foreach ($dll in $dllNames) {
        $original = $packages[$sourceId].Files["buildTransitive/$sourceTfm/$dll"]
        Assert-Contract ($null -ne $original) "Producer oracle missing $sourceId/$sourceTfm/$dll"
        $originalHash = [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData([byte[]]$original))
        $selected = @($compilePaths + $runtimePaths | Where-Object { $_.EndsWith("/$dll") } | Select-Object -Unique)
        foreach ($path in $selected) {
            Assert-Contract ((Get-FileHash "$cachedRoot/$path").Hash -eq $originalHash) "Wrong flavor selected: $Id/$path versus producer $sourceId"
        }
    }
    $referencePaths = @(Get-Content "$Directory/references.txt" | Where-Object { [IO.Path]::GetFileName($_) -in $dllNames })
    Assert-Contract ($referencePaths.Count -eq $compilePaths.Count) 'Resolved compile count differs from NuGet compile count'
    foreach ($path in $compilePaths) {
        Assert-Contract ($path.StartsWith('lib/')) "Non-lib compile $path"
        $expected = (Get-FileHash "$cachedRoot/$path").Hash
        $actual = @($referencePaths | Where-Object { [IO.Path]::GetFileName($_) -eq [IO.Path]::GetFileName($path) })
        Assert-Contract ((Get-FileHash $actual[0]).Hash -eq $expected) "Wrong resolved compile bytes for $path"
    }
    foreach ($output in @("$Directory/bin/Release/$tfm", "$Directory/published")) {
        foreach ($dll in $dllNames) {
            Assert-Contract ((Test-Path "$output/$dll") -eq $Runtime) "Runtime presence $output/$dll expected=$Runtime"
        }
        foreach ($path in $runtimePaths) {
            Assert-Contract ($path.StartsWith('lib/')) "Non-lib runtime $path"
            Assert-Contract ((Get-FileHash "$output/$([IO.Path]::GetFileName($path))").Hash -eq (Get-FileHash "$cachedRoot/$path").Hash) "Wrong output bytes: $output/$path"
        }
        $deps = Get-Content "$output/Probe.deps.json" -Raw | ConvertFrom-Json -AsHashtable
        $depsTarget = $deps.targets.Values | Select-Object -First 1
        $depsEntry = $depsTarget["$Id/$version"]
        $depsRuntime = @(if ($depsEntry -and $depsEntry.ContainsKey('runtime')) { $depsEntry.runtime.Keys | Where-Object { [IO.Path]::GetFileName($_) -in $dllNames } })
        Assert-Contract ((($depsRuntime | Sort-Object) -join '|') -eq (($runtimePaths | Sort-Object) -join '|')) "Deps runtime paths differ at $output"
        $satellites = @(Get-ChildItem $output -Recurse -Filter '*.resources.dll' | Where-Object { $_.Name -match '^MSTest(TestAdapter|Adapter.PlatformServices)|^MSTest\.TestAdapter' })
        if (-not $Runtime) { Assert-Contract ($satellites.Count -eq 0) 'Excluded adapter resources leaked' }
        if ($Runtime) {
            $sourcePrefix = if ($sourceId -eq 'MSTest.TestAdapter') { 'buildTransitive/_localization/' } else { "buildTransitive/$sourceTfm/_localization/" }
            $sourceResources = @($packages[$sourceId].Files.Keys | Where-Object { $_.StartsWith($sourcePrefix) -and $_.EndsWith('.dll') -and (-not $French -or $_.StartsWith($sourcePrefix + 'fr/')) })
            Assert-Contract ($satellites.Count -eq $sourceResources.Count) "Adapter satellite count differs from producer at $output"
            foreach ($originalPath in $sourceResources) {
                $relative = $originalPath.Substring($sourcePrefix.Length)
                $originalHash = [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData([byte[]]$packages[$sourceId].Files[$originalPath]))
                Assert-Contract ((Get-FileHash "$output/$relative").Hash -eq $originalHash) "Satellite differs from producer $sourceId/$relative"
            }
            $depsResources = @(if ($depsEntry -and $depsEntry.ContainsKey('resources')) { $depsEntry.resources.Keys })
            Assert-Contract ($depsResources.Count -eq $sourceResources.Count) "Deps satellite count differs from deployed resources at $output"
        }
        if ($French) {
            Assert-Contract ($satellites.Count -eq 2) "French-only expected two satellites, got $($satellites.Count)"
            foreach ($satellite in $satellites) {
                Assert-Contract ($satellite.Directory.Name -eq 'fr') "Unrequested culture $($satellite.Directory.Name)"
                $libTfm = $runtimePaths[0].Split('/')[1]
                Assert-Contract ((Get-FileHash $satellite.FullName).Hash -eq (Get-FileHash "$cachedRoot/lib/$libTfm/fr/$($satellite.Name)").Hash) 'Satellite bytes mismatch'
            }
        }
    }
}

$cases = @(
    @{ Name = 'AdapterOrdinary'; Id = 'MSTest.TestAdapter' },
    @{ Name = 'AdapterWinUI'; Id = 'MSTest.TestAdapter.WinUI' },
    @{ Name = 'AdapterUwp'; Id = 'MSTest.TestAdapter.Uwp' },
    @{ Name = 'AdapterUwpExcludeCompile'; Id = 'MSTest.TestAdapter.Uwp'; Exclude = 'compile' },
    @{ Name = 'AdapterUwpExcludeRuntime'; Id = 'MSTest.TestAdapter.Uwp'; Exclude = 'runtime' },
    @{ Name = 'AdapterUwpFrenchOnly'; Id = 'MSTest.TestAdapter.Uwp'; French = $true }
)
foreach ($case in $cases) {
    Invoke-Probe $case.Name {
        $exclude = if ($case.ContainsKey('Exclude')) { $case.Exclude } else { '' }
        $french = $case.ContainsKey('French')
        $props = if ($french) { '<SatelliteResourceLanguages>fr</SatelliteResourceLanguages>' } else { '' }
        $dir = New-Consumer candidate $case.Name "<PackageReference Include=`"$($case.Id)`" Version=`"$version`" ExcludeAssets=`"$exclude`" /><PackageReference Include=`"MSTest.TestFramework`" Version=`"$version`" />" $props
        $assets = Invoke-Consumer candidate $case.Name $dir
        $null = Invoke-Consumer candidate $case.Name $dir publish
        Assert-AdapterAssets $dir $assets $case.Id ($exclude -ne 'compile') ($exclude -ne 'runtime') $french
    }
}

foreach ($flavor in @('WinUI', 'Uwp')) {
    $name = "Legacy${flavor}BaselineSdk"
    Invoke-Probe $name {
        $dir = New-Consumer candidate $name '' "<MSTestApplicationModel>$flavor</MSTestApplicationModel><EnableMicrosoftTestingPlatform>true</EnableMicrosoftTestingPlatform><EnableMicrosoftTestingExtensionsPackagedApp>false</EnableMicrosoftTestingExtensionsPackagedApp><TestingExtensionsProfile>None</TestingExtensionsProfile><OutputType>Exe</OutputType>" '' $true
        $assets = Invoke-Consumer candidate $name $dir
        $null = Invoke-Consumer candidate $name $dir publish
        Assert-Contract ($assets.libraries.ContainsKey("MSTest.$flavor/$version")) "Baseline SDK did not reference legacy $flavor ID"
        Assert-Contract (-not $assets.libraries.ContainsKey("MSTest.TestAdapter/$version") -and -not $assets.libraries.ContainsKey("MSTest/$version")) 'Legacy UI pulled ordinary composition'
        Assert-AdapterAssets $dir $assets "MSTest.TestAdapter.$flavor"
        Assert-Contract ((Get-Content "$dir/properties.txt" -Raw) -match 'OutputType=Exe') 'SDK composition is not executable'
    }
}

foreach ($variant in @('baseline', 'candidate')) {
    $name = "${variant}SdkHelperNoRuntime"
    Invoke-Probe $name {
        $dir = New-Consumer $variant $name '' '<IsTestApplication>false</IsTestApplication><EnableMSTestSourceGeneration>true</EnableMSTestSourceGeneration><EmitCompilerGeneratedFiles>true</EmitCompilerGeneratedFiles>' '' $true
        $null = Invoke-Consumer $variant $name $dir
        $generated = @(Get-ChildItem "$dir/obj" -Recurse -Filter '*.g.cs' | Where-Object { $_.FullName -match 'MSTest.SourceGeneration' })
        Assert-Contract ($generated.Count -gt 0) 'Source-generated helper produced no MSTest generated source'
        $refs = @(Get-Content "$dir/references.txt" | Where-Object { [IO.Path]::GetFileName($_) -in $dllNames })
        Assert-Contract ($refs.Count -eq 2) 'Generated helper lacks required compile-time adapter APIs'
        $payload = @(Get-ChildItem "$dir/bin" -Recurse -Filter '*.dll' | Where-Object { $_.Name -match '^(MSTest.TestAdapter|MSTestAdapter.PlatformServices|Microsoft.Testing\.)' })
        Assert-Contract ($payload.Count -eq 0) "helper runtime leaked: $(($payload | ForEach-Object Name) -join ',')"
        # SDK helper dependencies still declare telemetry/MSBuild hooks; only the MSTest
        # application registration is suppressed by the existing SDK contract.
        Assert-Contract (-not (Get-Content "$dir/properties.txt" -Raw).Contains('031F8871-2660-4208-8F6B-FC142B40ABFF')) 'Helper registers MSTest application hook'
    }
}

foreach ($flavor in @('WinUI', 'Uwp')) {
    $name = "Sdk${flavor}WithOrdinaryGeneratedHelper"
    Invoke-Probe $name {
        $helperSource = $testSource.Replace('sealed class PackageTests', 'class HelperTests')
        $helper = New-Consumer candidate "$name/Helper" '' '<AssemblyName>Helper</AssemblyName><IsTestApplication>false</IsTestApplication><EnableMSTestSourceGeneration>true</EnableMSTestSourceGeneration><EmitCompilerGeneratedFiles>true</EmitCompilerGeneratedFiles>' $helperSource $true
        $source = 'using Microsoft.VisualStudio.TestTools.UnitTesting; [TestClass] public sealed class ApplicationTests : HelperTests { }'
        $dir = New-Consumer candidate $name '<ProjectReference Include="Helper/Probe.csproj" /><Compile Remove="Helper/**" />' "<MSTestApplicationModel>$flavor</MSTestApplicationModel><EnableMicrosoftTestingPlatform>true</EnableMicrosoftTestingPlatform><EnableMicrosoftTestingExtensionsPackagedApp>false</EnableMicrosoftTestingExtensionsPackagedApp><TestingExtensionsProfile>None</TestingExtensionsProfile><OutputType>Exe</OutputType>" $source $true
        $assets = Invoke-Consumer candidate $name $dir
        $null = Invoke-Consumer candidate $name $dir publish
        Assert-Contract (-not $assets.libraries.ContainsKey("MSTest.TestAdapter/$version")) 'Ordinary helper adapter propagated into UI application'
        Assert-AdapterAssets $dir $assets "MSTest.TestAdapter.$flavor"
        $helperPayload = @(Get-ChildItem "$helper/bin" -Recurse -Filter '*.dll' | Where-Object { $_.Name -match '^(MSTest.TestAdapter|MSTestAdapter.PlatformServices|Microsoft.Testing\.)' })
        Assert-Contract ($helperPayload.Count -eq 0) 'Helper runtime leaked into graph'
        $generated = @(Get-ChildItem "$helper/obj" -Recurse -Filter '*.g.cs' | Where-Object { $_.FullName -match 'MSTest.SourceGeneration' })
        Assert-Contract ($generated.Count -gt 0) 'Helper graph did not execute source generator'
    }
}

$executionSource = @'
using System;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Resources;
using Microsoft.VisualStudio.TestTools.UnitTesting;
[TestClass]
public sealed class ShippingTests
{
    [TestMethod]
    public void AdapterLoadsFromConsumerOutput()
    {
        foreach (string name in new[] { "MSTest.TestAdapter", "MSTestAdapter.PlatformServices" })
        {
            Assembly assembly = Assembly.Load(name);
            Assert.AreEqual(Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, name + ".dll")), assembly.Location);
            Assert.AreEqual(typeof(Assert).Assembly.GetName().Version, assembly.GetName().Version);
        }
    }
    [TestMethod]
    public void FrenchAdapterResourceIsAvailable()
    {
        var resources = new ResourceManager("Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Resources.PlatformAdapterResources", Assembly.Load("MSTest.TestAdapter"));
        Assert.AreEqual("Framework MSTest pour Microsoft Testing Platform.", resources.GetString("MSTestExtensionDescription", CultureInfo.GetCultureInfo("fr")));
    }
}
'@
function Assert-Trx([string] $Path) {
    [xml] $trx = Get-Content $Path -Raw
    $tests = @($trx.SelectNodes('//*[local-name()="UnitTestResult"]'))
    $expected = @('AdapterLoadsFromConsumerOutput', 'FrenchAdapterResourceIsAvailable')
    Assert-Contract ($tests.Count -eq 2) "Expected exactly two executed tests, got $($tests.Count)"
    Assert-Contract ((@($tests | ForEach-Object testName | Sort-Object) -join '|') -eq (($expected | Sort-Object) -join '|')) 'Unexpected executed test identities'
    Assert-Contract (@($tests | Where-Object { $_.outcome -ne 'Passed' }).Count -eq 0) 'Managed execution contains failed/skipped cases'
    $counters = $trx.SelectSingleNode('//*[local-name()="Counters"]')
    Assert-Contract ($counters.total -eq '2' -and $counters.executed -eq '2' -and $counters.passed -eq '2') 'TRX counters mismatch'
}
foreach ($variant in @('baseline', 'candidate')) {
    foreach ($runner in @('Mtp', 'Vstest')) {
        $name = "${variant}Managed$runner"
        Invoke-Probe $name {
            $props = '<SatelliteResourceLanguages>fr</SatelliteResourceLanguages>'
            if ($runner -eq 'Vstest') { $props += '<UseVSTest>true</UseVSTest>' }
            else { $props += '<TestingExtensionsProfile>None</TestingExtensionsProfile><EnableMicrosoftTestingExtensionsTrxReport>true</EnableMicrosoftTestingExtensionsTrxReport>' }
            $dir = New-Consumer $variant $name '' $props $executionSource $true
            $null = Invoke-Consumer $variant $name $dir
            $environment = @{ DOTNET_ROOT = (Split-Path $dotnet); DOTNET_ROOT_X64 = (Split-Path $dotnet); DOTNET_CLI_UI_LANGUAGE = 'fr'; TESTINGPLATFORM_TELEMETRY_OPTOUT = '1' }
            if ($runner -eq 'Mtp') {
                $null = Invoke-Consumer $variant $name $dir publish
                $assembly = "$dir/published/Probe.dll"
                $discovery = Invoke-Recorded "$name-discovery" $dotnet @($assembly, '--list-tests') $dir $environment
                Assert-Contract ($discovery.Exit -eq 0 -and $discovery.Output.Contains('AdapterLoadsFromConsumerOutput') -and $discovery.Output.Contains('FrenchAdapterResourceIsAvailable')) 'MTP discovery missing planned cases'
                $run = Invoke-Recorded "$name-execution" $dotnet @($assembly, '--report-trx', '--report-trx-filename', 'execution.trx', '--results-directory', "$dir/results") $dir $environment
            }
            else {
                $assembly = "$dir/bin/Release/$tfm/Probe.dll"
                $discovery = Invoke-Recorded "$name-discovery" $dotnet @('vstest', $assembly, '/ListTests') $dir $environment
                Assert-Contract ($discovery.Exit -eq 0 -and $discovery.Output.Contains('AdapterLoadsFromConsumerOutput') -and $discovery.Output.Contains('FrenchAdapterResourceIsAvailable')) 'VSTest discovery missing planned cases'
                $run = Invoke-Recorded "$name-execution" $dotnet @('vstest', $assembly, '/Logger:trx;LogFileName=execution.trx', "/ResultsDirectory:$dir/results") $dir $environment
            }
            Assert-Contract ($run.Exit -eq 0) "Managed execution failed exit=$($run.Exit)"
            Assert-Trx "$dir/results/execution.trx"
        }
    }
}

if ($NativeBuild) {
    # Read-only checks: unlike the CI agent preflight, never provision Developer Mode or remove
    # registrations. Build-only runs need MSBuild; desktop/activation checks apply only to launches.
    $prerequisiteFailures = [Collections.Generic.List[string]]::new()
    $vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio/Installer/vswhere.exe'
    $vs = if (Test-Path -LiteralPath $vswhere -PathType Leaf) {
        & $vswhere -latest -prerelease -products '*' -requires Microsoft.Component.MSBuild Microsoft.VisualStudio.Workload.Universal Microsoft.VisualStudio.ComponentGroup.UWP.Support -format json |
            ConvertFrom-Json | Select-Object -First 1
    }
    $msbuild = if ($vs) { Join-Path $vs.installationPath 'MSBuild/Current/Bin/MSBuild.exe' }
    if (-not $msbuild -or -not (Test-Path -LiteralPath $msbuild -PathType Leaf)) {
        $prerequisiteFailures.Add('No Visual Studio instance with desktop MSBuild and the complete UWP support component group was found.')
    }
    if ($ModernUwpLaunch) {
        # Group policy, when configured, overrides the local Developer Mode setting.
        $developerMode = foreach ($key in @('HKLM:\SOFTWARE\Policies\Microsoft\Windows\Appx', 'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\AppModelUnlock')) {
            $property = Get-ItemProperty -LiteralPath $key -Name 'AllowDevelopmentWithoutDevLicense' -ErrorAction SilentlyContinue
            if ($null -ne $property) { $property.AllowDevelopmentWithoutDevLicense; break }
        }
        if ($developerMode -ne 1) {
            $prerequisiteFailures.Add('ModernUwpLaunch requires Developer Mode to be enabled already; this harness never changes it.')
        }
        $session = (Get-Process -Id $PID).SessionId
        $explorer = @(Get-Process -Name explorer -ErrorAction SilentlyContinue | Where-Object SessionId -eq $session)
        if (-not [Environment]::UserInteractive -or $session -le 0 -or $explorer.Count -eq 0) {
            $prerequisiteFailures.Add("ModernUwpLaunch requires an interactive desktop session with explorer.exe; current session is $session.")
        }
    }
    Write-Utf8 "$RunRoot/logs/native-prerequisites.json" (@{
        msbuild = $msbuild; modernUwpLaunch = $ModernUwpLaunch.IsPresent; failures = @($prerequisiteFailures)
    } | ConvertTo-Json -Depth 4)
    if ($prerequisiteFailures.Count -ne 0) {
        throw "Native prerequisites failed; no native probes attempted:$([Environment]::NewLine)- $($prerequisiteFailures -join "$([Environment]::NewLine)- ")"
    }
    foreach ($variant in @('baseline', 'candidate')) {
        foreach ($model in @('ClassicUwp', 'ModernUwp', 'WinUI')) {
            $name = "${variant}Native${model}Build"
            Invoke-Probe $name {
                $directory = "$RunRoot/$variant/$name"
                $templateFile = if ($model -eq 'WinUI') { 'test/IntegrationTests/Microsoft.Testing.Platform.Acceptance.IntegrationTests/PackagedWinUITests.cs' } else { "test/IntegrationTests/MSTest.Acceptance.IntegrationTests/${model}Tests.cs" }
                $constant = if ($model -eq 'WinUI') { 'SourceCode' } else { "${model}SourceCode" }
                $text = Get-Content "$repository/$templateFile" -Raw
                $match = [regex]::Match($text, '(?s)private const string ' + $constant + ' = """\r?\n(.*?)\r?\n""";')
                if (-not $match.Success) { throw "Required native template not found: $constant" }
                $source = $match.Groups[1].Value
                $tokens = @{
                    AssetName = 'NativeProbe'; MSTestVersion = $version; MicrosoftTestingPlatformVersion = $mtpVersion
                    TargetFramework = $tfm; MSBuildSdkExtrasVersion = '3.0.44'; MicrosoftNETCoreUniversalWindowsPlatformVersion = '6.2.14'
                    PackageIdentityName = ('MSTest.Native.' + [Guid]::NewGuid().ToString('N'))
                    RuntimeIdentifier = 'win-x64'; WindowsAppSdkVersion = '1.8.251106002'; WindowsSdkBuildToolsVersion = '10.0.26100.7175'
                    AppContainerConstant = ''; RetryTestConstant = ''; RetryProperty = ''; Publisher = 'CN=MSTest'
                    PhoneProductId = [Guid]::NewGuid().ToString(); ApplicationEntryPoint = 'Windows.FullTrustApplication'
                    ApplicationAttributes = ''; RunFullTrustCapability = '<rescap:Capability Name="runFullTrust" />'
                    ExpectedAppContainer = 'false'; ExpectedPackageFamilyName = 'NotLaunchedByBuildProbe'
                    ExecutionIdExpectationPath = "$directory/expected.txt"; ExecutionIdMarkerPath = "$directory/execution-id.txt"
                    ExecutionMarkerPath = "$directory/execution.txt"; IdentityMarkerPath = "$directory/identity.txt"
                }
                foreach ($token in $tokens.Keys) { $source = $source.Replace('$' + $token + '$', [string]$tokens[$token]) }
                foreach ($file in [regex]::Matches($source, '(?ms)^#file ([^\r\n]+)\r?\n(.*?)(?=^#file |\z)')) {
                    $relative = $file.Groups[1].Value.Trim()
                    $path = [IO.Path]::GetFullPath((Join-Path $directory $relative))
                    if (-not $path.StartsWith([IO.Path]::GetFullPath($directory) + '\', [StringComparison]::OrdinalIgnoreCase)) { throw "Unsafe native template path $relative" }
                    Write-Utf8 $path $file.Groups[2].Value
                }
                [IO.Directory]::CreateDirectory("$directory/Assets") | Out-Null
                Copy-Item "$repository/samples/public/BlankUwpNet9App/Assets/*" "$directory/Assets/"
                $environment = @{
                    NUGET_PACKAGES = "$RunRoot/$variant/packages"; DOTNET_ROOT = (Split-Path $dotnet)
                    DOTNET_ROOT_X64 = (Split-Path $dotnet); DOTNET_MSBUILD_SDK_RESOLVER_CLI_DIR = (Split-Path $dotnet)
                    MSBuildSDKsPath = $null; MSBuildExtensionsPath = $null; DOTNET_CLI_UI_LANGUAGE = 'en'
                }
                $build = Invoke-Recorded $name $msbuild @('NativeProbe.csproj', '/restore', '/t:Build', '/p:Configuration=Release', '/p:Platform=x64',
                    '/p:EnableMicrosoftTestingExtensionsCodeCoverage=false', "/bl:$RunRoot/logs/$name.binlog") $directory $environment
                $null = Assert-ConsumerProvenance $variant $directory
                if ($variant -eq 'candidate' -and $model -eq 'ClassicUwp' -and $build.Exit -ne 0 -and
                    $build.Output.Contains('MSB3030') -and $build.Output.Contains("\mstest.uwp\$version\lib\uap10.0.16299\MSTest.Uwp.dll")) {
                    throw 'INCOMPATIBILITY: existing classic UWP consumer hard-codes MSTest.Uwp/lib UI DLL; metapackage has no DLL at that path (MSB3030).'
                }
                if ($build.Exit -ne 0) { throw "SETUP: native build exit $($build.Exit); see $name.log. No native launch occurred." }
                $recipes = @(Get-ChildItem $directory -Recurse -Filter '*.build.appxrecipe')
                Assert-Contract ($recipes.Count -eq 1) "Native build did not produce exactly one deployment recipe: $($recipes.Count)"
                $manifests = @(Get-ChildItem "$directory/bin" -Recurse -Filter 'AppxManifest.xml')
                Assert-Contract ($manifests.Count -gt 0) 'Native build produced no deployment layout'
                Write-Utf8 "$directory/native-scope.json" (@{ build = 'Passed'; launch = 'NotExecuted'; template = $templateFile; packageIdentity = $tokens.PackageIdentityName; recipes = @($recipes | ForEach-Object FullName) } | ConvertTo-Json -Depth 4)
                if ($ModernUwpLaunch -and $model -eq 'ModernUwp') {
                    # The existing controller owns activation. Our unique manifest identity is
                    # recorded before launch; cleanup addresses that exact identity only.
                    $identity = $tokens.PackageIdentityName
                    Write-Utf8 "$directory/cleanup.ps1" @"
`$ErrorActionPreference = 'Stop'
`$packages = @(Get-AppxPackage -Name '$identity')
foreach (`$package in `$packages) {
    if (`$package.Name -cne '$identity') { throw 'Unexpected AppX identity' }
    Write-Output ("Removing owned registration " + `$package.PackageFullName + " at " + `$package.InstallLocation)
    Remove-AppxPackage -Package `$package.PackageFullName
}
if (@(Get-AppxPackage -Name '$identity').Count -ne 0) { throw 'Owned registration remains' }
"@
                    try {
                        [IO.Directory]::CreateDirectory("$directory/results") | Out-Null
                        $arguments = "--report-trx --report-trx-filename native.trx --results-directory %22$directory/results%22 --diagnostic --diagnostic-output-directory %22$directory/results%22"
                        $run = Invoke-Recorded "$name-execution" $msbuild @('NativeProbe.csproj', '/t:InvokeTestingPlatform', '/p:Configuration=Release',
                            '/p:Platform=x64', "/p:TestingPlatformCommandLineArguments=$arguments", "/bl:$RunRoot/logs/$name-execution.binlog") $directory $environment
                        Write-Utf8 "$directory/native-scope.json" (@{ build = 'Passed'; launch = 'Attempted'; launchExit = $run.Exit; packageIdentity = $identity } | ConvertTo-Json -Depth 4)
                        $report = "$directory/results/native.trx"
                        if (-not (Test-Path $report)) { throw "SETUP: Native launch exit $($run.Exit), no TRX. See $name-execution.log." }
                        [xml] $trx = Get-Content $report -Raw
                        $rows = @($trx.SelectNodes('//*[local-name()="UnitTestResult"]'))
                        $expected = @('AdapterFailureMessages_AreLocalized_English', 'AdapterFailureMessages_UseNeutralFallbackForFrench',
                            'ApplicationResources_AreLocalized_English', 'ApplicationResources_AreLocalized_French',
                            'PlainTestMethod_RunsInPackagedUwp', 'UITestMethod_RunsOnCoreWindowDispatcher')
                        Assert-Contract ((@($rows | ForEach-Object testName | Sort-Object) -join '|') -eq (($expected | Sort-Object) -join '|')) 'Native Modern UWP test identities differ'
                        Write-Utf8 "$directory/native-scope.json" (@{ build = 'Passed'; launchExit = $run.Exit; results = @($rows | ForEach-Object { @{ name = $_.testName; outcome = $_.outcome } }); packageIdentity = $identity } | ConvertTo-Json -Depth 5)
                        Assert-Contract ($run.Exit -eq 0 -and @($rows | Where-Object { $_.outcome -ne 'Passed' }).Count -eq 0) 'Native Modern UWP executed tests failed; inspect native.trx'
                    }
                    finally {
                        $cleanup = Invoke-Recorded "$name-cleanup" 'powershell.exe' @('-NoProfile', '-File', "$directory/cleanup.ps1") $directory
                        if ($cleanup.Exit -ne 0) { throw "Owned native cleanup failed: $name-cleanup.log" }
                    }
                }
            }
        }
    }
}

Write-Host "Results: $RunRoot/results.json"
Assert-Contract ($results.Count -gt 0) 'Zero selected probes'
$missing = @($Only | Where-Object { $_ -notin $results.name })
Assert-Contract ($missing.Count -eq 0) "Requested probes were not run: $($missing -join ',')"
$failures = @($results | Where-Object { $_.outcome -in @('Failed', 'SetupFailure') })
if ($failures.Count -ne 0) { throw "$($failures.Count) probe(s) failed; see results.json (expected behavioral controls are separate)." }
