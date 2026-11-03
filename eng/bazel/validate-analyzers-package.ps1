# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT license. See LICENSE file in the project root for full license information.

#requires -Version 7.2

[CmdletBinding()]
param(
    [string] $PackagePath,
    [ValidateRange(1, 256)]
    [int] $Jobs = 2
)

$ErrorActionPreference = 'Stop'
$PSNativeCommandUseErrorActionPreference = $false
$root = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$bazel = Join-Path $root 'eng\bazel.ps1'
$run = [DateTime]::UtcNow.ToString('yyyyMMdd-HHmmss') + '-' + [Guid]::NewGuid().ToString('N').Substring(0, 8)
$directory = Join-Path $root "artifacts\bazel-analyzer-validation\$run"
$feed = Join-Path $directory 'feed'
$consumer = Join-Path $directory 'consumer'
$cache = if ($env:TESTFX_BAZEL_OUTPUT_ROOT) {
    [IO.Path]::GetFullPath((Join-Path $env:TESTFX_BAZEL_OUTPUT_ROOT "analyzer-validation\$run\restored-packages"))
} else {
    Join-Path $directory 'restored-packages'
}
New-Item -ItemType Directory -Path $feed, $consumer | Out-Null
$steps = [System.Collections.Generic.List[object]]::new()
$cases = [System.Collections.Generic.List[object]]::new()
$packageRecord = $null
$legacyRecord = $null
$succeeded = $false

function Invoke-Step([string] $Name, [string] $Executable, [string[]] $Arguments, [int] $ExpectedExit = 0) {
    $log = Join-Path $directory "$Name.log"
    $errorLog = Join-Path $directory "$Name.stderr.log"
    & $Executable @Arguments > $log 2> $errorLog
    $code = $LASTEXITCODE
    $steps.Add([pscustomobject]@{ Name = $Name; Executable = $Executable; Arguments = $Arguments; ExitCode = $code; ExpectedExit = $ExpectedExit })
    if ($code -ne $ExpectedExit) {
        Get-Content -LiteralPath $log -Tail 40 | Write-Host
        Get-Content -LiteralPath $errorLog -Tail 40 | Write-Host
        throw "$Name returned $code, expected $ExpectedExit. See $log"
    }
    return Get-Content -LiteralPath $log -Raw
}

function Invoke-Case([string] $Name, [scriptblock] $Body) {
    try {
        & $Body
        $cases.Add([pscustomobject]@{ Name = $Name; Outcome = 'Passed' })
        Write-Host "$Name`: passed"
    }
    catch {
        $cases.Add([pscustomobject]@{ Name = $Name; Outcome = 'Failed'; Error = $_.Exception.Message })
        throw
    }
}

function Read-ZipText($Archive, [string] $Path) {
    $entry = $Archive.GetEntry($Path)
    if (-not $entry) { throw "Missing package entry: $Path" }
    $reader = [System.IO.StreamReader]::new($entry.Open())
    try { return $reader.ReadToEnd() } finally { $reader.Dispose() }
}

function Get-EntryHash($Entry) {
    $stream = $Entry.Open()
    try { return [Convert]::ToHexString([System.Security.Cryptography.SHA256]::HashData($stream)) }
    finally { $stream.Dispose() }
}

function Assert-Restore([string] $ProjectDirectory, [bool] $AllowsRoslyn = $false) {
    $assets = Get-Content -LiteralPath (Join-Path $ProjectDirectory 'obj\project.assets.json') -Raw | ConvertFrom-Json
    $folders = @($assets.packageFolders.PSObject.Properties.Name)
    if ($folders.Count -ne 1 -or [IO.Path]::GetFullPath($folders[0]).TrimEnd('\', '/') -ne $cache) {
        throw "Consumer restored outside its isolated cache: $ProjectDirectory"
    }
    if ($assets.libraries.PSObject.Properties.Name -cnotcontains "MSTest.Analyzers/$expectedVersion") {
        throw 'The consumer did not resolve the experimental analyzer package.'
    }
    if (-not $AllowsRoslyn -and @($assets.libraries.PSObject.Properties.Name | Where-Object { $_ -like 'Microsoft.CodeAnalysis*' }).Count) {
        throw 'Compiler-host dependencies leaked into the package consumer.'
    }
    $restored = Join-Path $cache "mstest.analyzers\$expectedVersion\mstest.analyzers.$expectedVersion.nupkg"
    if ((Get-FileHash -LiteralPath $restored -Algorithm SHA256).Hash -cne $packageRecord.Sha256) {
        throw 'The consumer restored different analyzer package bytes.'
    }
}

function Invoke-CompilerCase([string] $Language, [string] $Mode, [bool] $Fixed = $false, [bool] $Disabled = $false) {
    $name = "$Language-$($Mode.ToLowerInvariant())" + $(if ($Fixed) { '-fixed' } elseif ($Disabled) { '-disabled' } else { '-missing-class' })
    Invoke-Case $name {
        $projectDirectory = Join-Path $consumer $Language
        $source = if ($Language -eq 'cs') { $csharpSource } else { $visualBasicSource }
        if ($Fixed) {
            $source = if ($Language -eq 'cs') {
                $source.Replace('public class MissingTestClass', "[TestClass]`npublic class MissingTestClass")
            } else {
                $source.Replace('Public Class MissingTestClass', "<TestClass>`nPublic Class MissingTestClass")
            }
        }
        [IO.File]::WriteAllText((Join-Path $projectDirectory "Source.$Language"), $source, [Text.UTF8Encoding]::new($true))
        $expectedExit = if ($Fixed -or $Disabled -or $Mode -eq 'None') { 0 } else { 1 }
        $report = Join-Path $directory "$name.sarif"
        $null = Invoke-Step $name $dotnet @(
            'build', (Join-Path $projectDirectory "Consumer.$($Language)proj"), '--no-restore', '--disable-build-servers',
            '-t:Rebuild', '-c', 'Release', '--nologo', '-v:quiet',
            "-p:MSTestAnalysisMode=$Mode", '-p:WarningsAsErrors=MSTEST0030',
            "-p:RunAnalyzersDuringBuild=$(-not $Disabled)", "-p:ErrorLog=$report",
            "-p:ContractOutput=$(Join-Path $directory $name)", "-bl:$(Join-Path $directory "$name.binlog")"
        ) $expectedExit
        if (-not (Test-Path -LiteralPath $report)) { throw "$name did not emit compiler diagnostics." }
        $diagnostics = @((Get-Content -LiteralPath $report -Raw | ConvertFrom-Json).runs.results)
        $failures = @($diagnostics | Where-Object { $_.level -eq 'error' -and $_.ruleId -ne 'MSTEST0030' })
        $loadFailures = @($diagnostics | Where-Object { $_.ruleId -in @('AD0001', 'CS8032', 'CS9057', 'BC42376', 'BC42378') })
        if ($failures.Count -or $loadFailures.Count) { throw "$name had unrelated compiler/analyzer failures." }
        $matching = @($diagnostics | Where-Object { $_.ruleId -eq 'MSTEST0030' })
        if ($matching.Count -ne $expectedExit -or ($matching.Count -and $matching[0].level -ne 'error')) {
            throw "$name expected $expectedExit MSTEST0030 error, observed $($matching.Count)."
        }
        $selected = @(Get-Content -LiteralPath (Join-Path $directory "$name.analyzers.txt") | Where-Object { [IO.Path]::GetFileName($_) -in @('MSTest.Analyzers.dll', 'MSTest.Analyzers.CodeFixes.dll') })
        $expectedAssemblies = if ($Language -eq 'cs') { @('MSTest.Analyzers.dll', 'MSTest.Analyzers.CodeFixes.dll') } else { @('MSTest.Analyzers.dll') }
        if ($selected.Count -ne $expectedAssemblies.Count) { throw "$name selected the wrong analyzer assembly count." }
        foreach ($assembly in $expectedAssemblies) {
            $expectedPath = Join-Path $cache "mstest.analyzers\$expectedVersion\analyzers\dotnet\$Language\$assembly"
            if ($selected -notcontains $expectedPath) { throw "$name did not select $expectedPath." }
        }
        $configs = @(Get-Content -LiteralPath (Join-Path $directory "$name.configs.txt") | ForEach-Object { [IO.Path]::GetFullPath($_) })
        $expectedConfig = Join-Path $cache "mstest.analyzers\$expectedVersion\globalconfigs\mstest-$($Mode.ToLowerInvariant()).globalconfig"
        if ($configs -notcontains $expectedConfig) { throw "$name did not import its packaged analysis mode." }
        $sdk = @(Get-Content -LiteralPath (Join-Path $directory "$name.sdk.txt"))
        if ($sdk.Count -ne 3 -or $sdk[0] -cne $sdkVersion) { throw "$name used a different SDK." }
        $expectedBuild = Join-Path $sdks[0].FullName "sdk\$sdkVersion\MSBuild.dll"
        $actualBuild = Join-Path $sdk[1] 'MSBuild.dll'
        $expectedProps = Join-Path $sdks[0].FullName "sdk\$sdkVersion\Sdks\Microsoft.NET.Sdk\Sdk\Sdk.props"
        $actualProps = Join-Path $sdk[2] 'Microsoft.NET.Sdk\Sdk\Sdk.props'
        if ((Get-FileHash -LiteralPath $actualBuild).Hash -cne (Get-FileHash -LiteralPath $expectedBuild).Hash -or
            (Get-FileHash -LiteralPath $actualProps).Hash -cne (Get-FileHash -LiteralPath $expectedProps).Hash) {
            throw "$name mixed SDK build components."
        }
    }
}

$environmentNames = @('NUGET_PACKAGES', 'DOTNET_ROOT', 'DOTNET_ROOT_X64', 'DOTNET_HOST_PATH', 'DOTNET_ROLL_FORWARD', 'DOTNET_ROLL_FORWARD_TO_PRERELEASE', 'MSBuildSDKsPath', 'MSBuildExtensionsPath', 'MSBUILD_EXE_PATH')
$previousEnvironment = @{}
foreach ($name in $environmentNames) { $previousEnvironment[$name] = [Environment]::GetEnvironmentVariable($name) }
Push-Location $root
try {
    $revision = git rev-parse HEAD
    if ($LASTEXITCODE -ne 0) { throw 'Could not record source revision.' }
    $workingTree = @(git status --porcelain)
    [xml]$versions = Get-Content -LiteralPath 'eng\Versions.props' -Raw
    $prefixes = @($versions.SelectNodes('//*[local-name()="VersionPrefix"]'))
    if ($prefixes.Count -ne 1) { throw 'Expected one MSTest version prefix.' }
    $expectedVersion = $prefixes[0].InnerText + '-bazel'
    $null = Invoke-Step 'native-pack' $bazel @('build', '//:analyzers_package', "--jobs=$Jobs", '--noshow_progress', '--show_result=0')
    $executionRoot = (& $bazel info execution_root).Trim()
    if ($LASTEXITCODE -ne 0) { throw 'Could not locate the native execution root.' }
    $outputs = @(& $bazel cquery //:analyzers_package --output=files --noshow_progress)
    if ($LASTEXITCODE -ne 0 -or $outputs.Count -ne 1) { throw 'Expected exactly one analyzer NuGet.' }
    $nativePackage = Join-Path $executionRoot $outputs[0]
    $requestPath = Join-Path (Split-Path $nativePackage) 'nuget_pack_request.json'
    $request = Get-Content -LiteralPath $requestPath -Raw | ConvertFrom-Json
    $selectedPackage = if ($PackagePath) { (Resolve-Path -LiteralPath $PackagePath).Path } else { $nativePackage }
    $archive = [IO.Compression.ZipFile]::OpenRead($selectedPackage)
    try {
        Invoke-Case 'package-layout-and-private-dependencies' {
            [xml]$manifest = Read-ZipText $archive 'MSTest.Analyzers.nuspec'
            $metadata = $manifest.SelectSingleNode('//*[local-name()="metadata"]')
            if ($metadata.id -cne 'MSTest.Analyzers' -or $metadata.version -cne $expectedVersion -or $metadata.developmentDependency -cne 'true' -or
                @($metadata.SelectNodes('.//*[local-name()="dependencies" or local-name()="dependency"]')).Count) {
                throw 'Analyzer package identity, developmentDependency, or dependency suppression is incorrect.'
            }
            $expected = [System.Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
            foreach ($path in @('MSTest.Analyzers.nuspec', 'PACKAGE.md', 'tools/install.ps1', 'tools/uninstall.ps1', 'buildTransitive/MSTest.Analyzers.props', 'buildTransitive/MSTest.Analyzers.targets')) {
                $null = $expected.Add($path)
            }
            foreach ($mode in @('all', 'default', 'none', 'recommended')) {
                $path = "globalconfigs/mstest-$mode.globalconfig"
                $null = $expected.Add($path)
                if ((Read-ZipText $archive $path) -notmatch '(?m)^is_global = true\r?$') { throw "Invalid globalconfig: $path" }
            }
            foreach ($language in @('cs', 'vb')) {
                $assemblies = if ($language -eq 'cs') { @('MSTest.Analyzers', 'MSTest.Analyzers.CodeFixes') } else { @('MSTest.Analyzers') }
                foreach ($assembly in $assemblies) {
                    $null = $expected.Add("analyzers/dotnet/$language/$assembly.dll")
                    $stem = if ($assembly -eq 'MSTest.Analyzers') { 'Resources' } else { 'CodeFixResources' }
                    foreach ($catalog in Get-ChildItem -LiteralPath (Join-Path $root "src\Analyzers\$assembly\xlf") -Filter "$stem.*.xlf") {
                        $culture = $catalog.BaseName.Substring($stem.Length + 1)
                        $null = $expected.Add("analyzers/dotnet/$language/$culture/$assembly.resources.dll")
                    }
                }
            }
            $actual = @($archive.Entries.FullName | Where-Object { $_ -ne '_rels/.rels' -and $_ -ne '[Content_Types].xml' -and $_ -notmatch '^package/services/metadata/core-properties/[^/]+\.psmdcp$' })
            if (-not $expected.SetEquals([string[]]$actual) -or $actual.Count -ne $expected.Count) {
                throw "Unexpected analyzer package layout: $((Compare-Object @($expected) $actual | Out-String).Trim())"
            }
            foreach ($entry in $request.files | Where-Object { $_.target -notlike 'globalconfigs/*' }) {
                $nativeHash = (Get-FileHash -LiteralPath (Join-Path $executionRoot $entry.source) -Algorithm SHA256).Hash
                if ((Get-EntryHash $archive.GetEntry($entry.target)) -cne $nativeHash) { throw "Package payload differs from its native input: $($entry.target)" }
            }
        }
        $packageRecord = [pscustomobject]@{
            Id = 'MSTest.Analyzers'; Version = $expectedVersion; Path = $selectedPackage
            Sha256 = (Get-FileHash -LiteralPath $selectedPackage -Algorithm SHA256).Hash
            Files = @($archive.Entries.FullName)
        }
        Copy-Item -LiteralPath $selectedPackage -Destination (Join-Path $feed "MSTest.Analyzers.$expectedVersion.nupkg")
    }
    finally { $archive.Dispose() }

    $sdkVersion = [regex]::Match((Get-Content -LiteralPath 'MODULE.bazel' -Raw), 'dotnet\.toolchain\(dotnet_version = "([^"]+)"\)').Groups[1].Value
    if (-not $sdkVersion) { throw 'Could not identify the native SDK.' }
    $dotnetName = if ($IsWindows) { 'dotnet.exe' } else { 'dotnet' }
    $sdks = @(Get-ChildItem -LiteralPath (Join-Path $executionRoot 'external') -Directory -Filter "*dotnet_sdk.$sdkVersion`_*" |
        Where-Object { Test-Path -LiteralPath (Join-Path $_.FullName $dotnetName) })
    if ($sdks.Count -ne 1) { throw 'Expected one native SDK for the execution host.' }
    $dotnet = Join-Path $sdks[0].FullName $dotnetName
    $framework = 'net' + $sdkVersion.Split('.')[0] + '.0'
    foreach ($name in $environmentNames) {
        if (Test-Path -LiteralPath "Env:$name") { Remove-Item -LiteralPath "Env:$name" }
    }
    $env:NUGET_PACKAGES = $cache
    $env:DOTNET_ROOT = $sdks[0].FullName
    $env:DOTNET_ROOT_X64 = $sdks[0].FullName
    $env:DOTNET_HOST_PATH = $dotnet
    foreach ($file in @('Directory.Build.props', 'Directory.Packages.props')) {
        '<Project />' | Set-Content -LiteralPath (Join-Path $consumer $file) -Encoding utf8
    }
    'root = true' | Set-Content -LiteralPath (Join-Path $consumer '.editorconfig') -Encoding utf8
    @{ sdk = @{ version = $sdkVersion; allowPrerelease = $true; rollForward = 'disable' } } |
        ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $consumer 'global.json') -Encoding utf8
    $escapedFeed = [Security.SecurityElement]::Escape($feed)
    @"
<configuration>
  <packageSources><clear /><add key="native" value="$escapedFeed" /><add key="nuget" value="https://api.nuget.org/v3/index.json" /></packageSources>
  <fallbackPackageFolders><clear /></fallbackPackageFolders>
  <packageSourceMapping>
    <packageSource key="native"><package pattern="MSTest.Analyzers" /></packageSource>
    <packageSource key="nuget"><package pattern="*" /></packageSource>
  </packageSourceMapping>
</configuration>
"@ | Set-Content -LiteralPath (Join-Path $consumer 'NuGet.config') -Encoding utf8
    @'
<Project>
  <Target Name="CaptureAnalyzerPackageContract" BeforeTargets="CoreCompile" Condition="'$(ContractOutput)' != ''">
    <WriteLinesToFile File="$(ContractOutput).analyzers.txt" Lines="@(Analyzer->'%(FullPath)')" Overwrite="true" />
    <WriteLinesToFile File="$(ContractOutput).configs.txt" Lines="@(EditorConfigFiles->'%(FullPath)')" Overwrite="true" />
    <WriteLinesToFile File="$(ContractOutput).sdk.txt" Lines="$(NETCoreSdkVersion);$(MSBuildToolsPath);$(MSBuildSDKsPath)" Overwrite="true" />
  </Target>
</Project>
'@ | Set-Content -LiteralPath (Join-Path $consumer 'Directory.Build.targets') -Encoding utf8
    $csharpSource = @'
using Microsoft.VisualStudio.TestTools.UnitTesting;

[assembly: DoNotParallelize]

public class MissingTestClass
{
    [TestMethod]
    public void Test()
    {
    }
}
'@
    $visualBasicSource = @'
Imports Microsoft.VisualStudio.TestTools.UnitTesting

<Assembly: DoNotParallelize>

Public Class MissingTestClass
    <TestMethod>
    Public Sub Test()
    End Sub
End Class
'@
    $csharpInput = Join-Path $directory 'MissingTestClass.cs'
    [IO.File]::WriteAllText($csharpInput, $csharpSource, [Text.UTF8Encoding]::new($true))
    Set-Location $consumer
    foreach ($language in @('cs', 'vb')) {
        $projectDirectory = Join-Path $consumer $language
        New-Item -ItemType Directory -Path $projectDirectory | Out-Null
        $project = Join-Path $projectDirectory "Consumer.$($language)proj"
        @"
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>$framework</TargetFramework>
    <EnableNETAnalyzers>false</EnableNETAnalyzers>
    <NuGetAudit>false</NuGetAudit>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="MSTest.Analyzers" Version="$expectedVersion" />
    <PackageReference Include="MSTest.TestFramework" Version="4.5.2" />
  </ItemGroup>
</Project>
"@ | Set-Content -LiteralPath $project -Encoding utf8
        $null = Invoke-Step "$language-restore" $dotnet @('restore', $project, '--configfile', (Join-Path $consumer 'NuGet.config'), '--nologo', '-v:quiet', "-bl:$(Join-Path $directory "$language-restore.binlog")")
        Assert-Restore $projectDirectory
        foreach ($mode in @('Default', 'Recommended', 'All', 'None')) { Invoke-CompilerCase $language $mode }
        Invoke-CompilerCase $language 'Default' -Fixed $true
        Invoke-CompilerCase $language 'Default' -Disabled $true
    }

    Invoke-Case 'roslyn-3.11-codefix-and-satellites' {
        $projectDirectory = Join-Path $consumer 'legacy'
        New-Item -ItemType Directory -Path $projectDirectory | Out-Null
        Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'AnalyzerPackageConsumer.cs') -Destination $projectDirectory
        $project = Join-Path $projectDirectory 'LegacyHost.csproj'
        @"
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>$framework</TargetFramework>
    <LangVersion>preview</LangVersion>
    <Nullable>enable</Nullable>
    <MSTestAnalysisMode>None</MSTestAnalysisMode>
    <EnableNETAnalyzers>false</EnableNETAnalyzers>
    <NuGetAudit>false</NuGetAudit>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="MSTest.Analyzers" Version="$expectedVersion" />
    <PackageReference Include="MSTest.TestFramework" Version="4.5.2" />
    <PackageReference Include="Microsoft.CodeAnalysis.CSharp.Workspaces" Version="3.11.0" />
  </ItemGroup>
</Project>
"@ | Set-Content -LiteralPath $project -Encoding utf8
        $null = Invoke-Step 'legacy-host-build' $dotnet @(
            'build', $project, '--disable-build-servers', '-c', 'Release', '--nologo', '-v:quiet',
            "-p:RestoreConfigFile=$(Join-Path $consumer 'NuGet.config')", "-bl:$(Join-Path $directory 'legacy-host.binlog')"
        )
        Assert-Restore $projectDirectory -AllowsRoslyn $true
        $packageDirectory = Join-Path $cache "mstest.analyzers\$expectedVersion"
        $output = Invoke-Step 'legacy-host-run' $dotnet @((Join-Path $projectDirectory "bin\Release\$framework\LegacyHost.dll"), $packageDirectory, $csharpInput)
        $script:legacyRecord = $output | ConvertFrom-Json
        [xml]$catalog = Get-Content -LiteralPath (Join-Path $root 'src\Analyzers\MSTest.Analyzers\xlf\Resources.fr.xlf') -Raw
        $expectedTitle = $catalog.SelectSingleNode('//*[local-name()="trans-unit" and @id="TypeContainingTestMethodShouldBeATestClassTitle"]/*[local-name()="target"]').InnerText
        [xml]$fixCatalog = Get-Content -LiteralPath (Join-Path $root 'src\Analyzers\MSTest.Analyzers.CodeFixes\xlf\CodeFixResources.fr.xlf') -Raw
        $expectedFix = $fixCatalog.SelectSingleNode('//*[local-name()="trans-unit" and @id="AddTestClassFix"]/*[local-name()="target"]').InnerText
        if ($legacyRecord.RoslynVersion -cne '3.11.0.0' -or $legacyRecord.DiagnosticId -cne 'MSTEST0030' -or
            $legacyRecord.FrenchTitle -cne $expectedTitle -or $legacyRecord.FrenchCodeFix -cne $expectedFix) {
            throw 'The packaged assemblies failed the Roslyn API or localized resource contract.'
        }
    }
    Invoke-Case 'native-producer-action-graph' {
        $graphText = Invoke-Step 'native-actions' $bazel @('aquery', 'deps(//:analyzers_package)', '--output=jsonproto', '--noshow_progress')
        $graph = $graphText | ConvertFrom-Json -Depth 100
        $forbidden = @($graph.actions | Where-Object {
            ($_.arguments -join ' ') -match '(?i)(?:^|[\\/\s])(?:MSBuild(?:\.dll|\.exe)?|build\.(?:cmd|sh))(?:\s|$)|dotnet(?:\.exe)?\s+(?:build|test|pack)\b'
        })
        if ($forbidden.Count -or $graph.actions.mnemonic -notcontains 'NuGetPack' -or $graph.actions.mnemonic -notcontains 'TestFxAnalyzerGlobalConfigs') {
            throw 'The analyzer package graph did not retain native compilation, generation, and packing.'
        }
    }
    $succeeded = $true
    Write-Host "Analyzer package validation passed: $($cases.Count) cases. Evidence: $directory"
}
finally {
    foreach ($name in $environmentNames) {
        if ($null -eq $previousEnvironment[$name]) {
            if (Test-Path -LiteralPath "Env:$name") { Remove-Item -LiteralPath "Env:$name" }
        } else {
            Set-Item -LiteralPath "Env:$name" -Value $previousEnvironment[$name]
        }
    }
    Pop-Location
    [pscustomobject]@{
        Passed = $succeeded; BaseRevision = $revision; WorkingTree = $workingTree
        Package = $packageRecord; LegacyHost = $legacyRecord; Steps = $steps; Cases = $cases
    } | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath (Join-Path $directory 'validation.json') -Encoding utf8
}

exit 0
