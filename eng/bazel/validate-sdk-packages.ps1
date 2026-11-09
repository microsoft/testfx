# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT license. See LICENSE file in the project root for full license information.

#requires -Version 7.2

[CmdletBinding()]
param(
    [switch] $LayoutOnly,
    [Alias('ArtifactsRoot')]
    [string] $EvidenceRoot,
    [ValidateRange(1, 64)]
    [int] $Jobs = 2
)

$ErrorActionPreference = 'Stop'
$root = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
if ($EvidenceRoot -and -not [IO.Path]::IsPathFullyQualified($EvidenceRoot)) { throw 'EvidenceRoot must be an absolute path.' }
if (-not $EvidenceRoot) { $EvidenceRoot = Join-Path $root 'artifacts\bazel-sdk-validation' }
$run = [DateTime]::UtcNow.ToString('yyyyMMdd-HHmmss') + '-' + [Guid]::NewGuid().ToString('N').Substring(0, 8)
$directory = [IO.Path]::GetFullPath((Join-Path $EvidenceRoot $run))
$feed = Join-Path $directory 'feed'
New-Item -ItemType Directory -Path $feed -Force | Out-Null
$pwsh = Join-Path $PSHOME $(if ($IsWindows) { 'pwsh.exe' } else { 'pwsh' })
$bazel = Join-Path $root 'eng\bazel.ps1'
$frameworks = @('net8.0', 'net9.0', 'net10.0', 'net11.0')
$steps = [Collections.Generic.List[object]]::new()
$packages = @{}
$evaluations = [Collections.Generic.List[object]]::new()
$succeeded = $false
$layoutPassed = $false
$consumerPassed = $false

function Invoke-Step(
    [string] $Name, [string] $Executable, [string[]] $Arguments,
    [string] $WorkingDirectory = $root, [int] $ExpectedExit = 0,
    [hashtable] $Environment = @{}, [int] $TimeoutSeconds = 600
) {
    $start = [Diagnostics.ProcessStartInfo]::new($Executable)
    $start.WorkingDirectory = $WorkingDirectory
    $start.UseShellExecute = $false
    $start.RedirectStandardOutput = $true
    $start.RedirectStandardError = $true
    foreach ($argument in $Arguments) { $start.ArgumentList.Add($argument) }
    foreach ($key in $Environment.Keys) {
        if ($null -eq $Environment[$key]) { $null = $start.Environment.Remove($key) }
        else { $start.Environment[$key] = $Environment[$key] }
    }
    $process = [Diagnostics.Process]::new()
    $process.StartInfo = $start
    $started = [DateTimeOffset]::UtcNow
    $launched = $false
    try {
        if (-not $process.Start()) { throw "Could not start $Name." }
        $launched = $true
        $stdout = $process.StandardOutput.ReadToEndAsync()
        $stderr = $process.StandardError.ReadToEndAsync()
        $timedOut = -not $process.WaitForExit($TimeoutSeconds * 1000)
        if ($timedOut) {
            $process.Kill($true)
            if (-not $process.WaitForExit(10000)) { throw "$Name did not terminate after its timeout." }
        }
        $output = $stdout.GetAwaiter().GetResult()
        $errorOutput = $stderr.GetAwaiter().GetResult()
        [IO.File]::WriteAllText((Join-Path $directory "$Name.log"), $output + $errorOutput)
        $steps.Add([pscustomobject]@{
            Name = $Name; Executable = $Executable; Arguments = $Arguments; WorkingDirectory = $WorkingDirectory
            ExitCode = $process.ExitCode; ExpectedExit = $ExpectedExit; StartedUtc = $started; TimedOut = $timedOut
        })
        if ($timedOut) { throw "$Name exceeded its $TimeoutSeconds second limit. See $Name.log." }
        if ($process.ExitCode -ne $ExpectedExit) {
            Write-Host ($output + $errorOutput)
            throw "$Name returned $($process.ExitCode), expected $ExpectedExit."
        }
        Write-Host "$Name`: exit $($process.ExitCode)"
        return $output
    }
    finally {
        try {
            if ($launched -and -not $process.HasExited) {
                $process.Kill($true)
                if (-not $process.WaitForExit(10000)) { throw "$Name did not terminate after cancellation." }
            }
        }
        finally { $process.Dispose() }
    }
}

function Invoke-Bazel([string] $Name, [string[]] $Arguments) {
    Invoke-Step $Name $pwsh (@('-NoProfile', '-File', $bazel) + $Arguments) -TimeoutSeconds 1800
}

function Assert-Equal($Expected, $Actual, [string] $Description) {
    if ($Expected -cne $Actual) { throw "$Description`: expected '$Expected', got '$Actual'." }
}

function Assert-Set([object[]] $Expected, [object[]] $Actual, [string] $Description) {
    Assert-Equal ([string]::Join('|', @($Expected | Sort-Object -CaseSensitive))) `
        ([string]::Join('|', @($Actual | Sort-Object -CaseSensitive))) $Description
}

function Read-Property([xml] $Document, [string] $Name) {
    $nodes = @($Document.SelectNodes("/Project/PropertyGroup/$Name"))
    if ($nodes.Count -ne 1) { throw "Expected one $Name property." }
    return $nodes[0].InnerText.Trim()
}

function Read-Entry($Archive, [string] $Name) {
    $entry = $Archive.GetEntry($Name)
    if (-not $entry) { throw "Missing package entry: $Name" }
    $reader = [IO.StreamReader]::new($entry.Open())
    try { return $reader.ReadToEnd() } finally { $reader.Dispose() }
}

function Entry-Hash($Entry) {
    $stream = $Entry.Open()
    try { return [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($stream)) }
    finally { $stream.Dispose() }
}

function Assert-Trx([string] $Path, [string] $Name, [string] $Outcome) {
    [xml]$trx = Get-Content -LiteralPath $Path -Raw
    $results = @($trx.SelectNodes('//*[local-name()="UnitTestResult"]'))
    $counters = $trx.SelectSingleNode('//*[local-name()="Counters"]')
    Assert-Equal 1 $results.Count 'Executed result count'
    Assert-Equal $Outcome $results[0].outcome 'Test outcome'
    Assert-Equal $Name $results[0].testName 'Executed test identity'
    Assert-Equal 1 ([int]$counters.total) 'Selected test count'
    Assert-Equal 1 ([int]$counters.executed) 'Executed test count'
    Assert-Equal $(if ($Outcome -eq 'Passed') { 1 } else { 0 }) ([int]$counters.passed) 'Passed test count'
    Assert-Equal $(if ($Outcome -eq 'Failed') { 1 } else { 0 }) ([int]$counters.failed) 'Failed test count'
    Assert-Equal 0 ([int]$counters.notExecuted) 'Skipped test count'
}

function New-Consumer([string] $Name, [string] $Properties, [switch] $Meta, [switch] $Layered) {
    $path = Join-Path $directory $Name
    New-Item -ItemType Directory -Path $path | Out-Null
    foreach ($file in @('Directory.Build.props', 'Directory.Build.targets', 'Directory.Packages.props')) {
        [IO.File]::WriteAllText((Join-Path $path $file), '<Project />')
    }
    [IO.File]::WriteAllText((Join-Path $path 'NuGet.config'), $nugetConfiguration)
    @{
        sdk = @{ version = $sdkVersion; allowPrerelease = $true; rollForward = 'disable' }
        'msbuild-sdks' = @{ 'MSTest.Sdk' = $mstestVersion }
        test = @{ runner = $(if ($Meta) { 'VSTest' } else { 'Microsoft.Testing.Platform' }) }
    } | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $path 'global.json') -Encoding utf8
    $propertiesXml = "<PropertyGroup><TargetFramework>net11.0</TargetFramework><ImplicitUsings>enable</ImplicitUsings><Nullable>enable</Nullable>$Properties</PropertyGroup>"
    $project = if ($Layered) {
        @"
<Project>
  <Import Project="Sdk.props" Sdk="Microsoft.NET.Sdk.Web" />
  <Import Project="Sdk.props" Sdk="MSTest.Sdk" />
  $propertiesXml
  <Import Project="Sdk.targets" Sdk="MSTest.Sdk" />
  <Import Project="Sdk.targets" Sdk="Microsoft.NET.Sdk.Web" />
</Project>
"@
    }
    elseif ($Meta) { "<Project Sdk=`"Microsoft.NET.Sdk`">$propertiesXml<ItemGroup><PackageReference Include=`"MSTest`" Version=`"$mstestVersion`" /></ItemGroup></Project>" }
    else { "<Project Sdk=`"MSTest.Sdk`">$propertiesXml</Project>" }
    [IO.File]::WriteAllText((Join-Path $path 'Consumer.csproj'), $project)
    return $path
}

function Assert-ResolvedPackages([string] $Consumer, [string] $Framework) {
    $assetsPath = Join-Path $Consumer 'obj\project.assets.json'
    $assets = Get-Content -LiteralPath $assetsPath -Raw | ConvertFrom-Json -AsHashtable
    Assert-Set @([IO.Path]::GetFullPath($consumerEnvironment.NUGET_PACKAGES).TrimEnd('\', '/')) `
        @($assets.packageFolders.Keys | ForEach-Object { [IO.Path]::GetFullPath($_).TrimEnd('\', '/') }) 'Isolated restore cache'
    foreach ($identity in $assets.libraries.Keys) {
        $id, $version = $identity.Split('/', 2)
        if ($id -ne 'MSTest' -and $id -notlike 'MSTest.*' -and $id -notlike 'Microsoft.Testing.*') { continue }
        if ($id -eq 'Microsoft.Testing.Extensions.CodeCoverage') {
            Assert-Equal $componentVersions.MicrosoftTestingExtensionsCodeCoverageVersion $version 'External coverage pin'
            continue
        }
        if (-not $packages.ContainsKey($id)) { throw "A non-native TestFx package resolved: $identity" }
        Assert-Equal $packages[$id].Version $version "Resolved version of $id"
        $installed = Join-Path $consumerEnvironment.NUGET_PACKAGES "$($id.ToLowerInvariant())\$version\$($id.ToLowerInvariant()).$version.nupkg"
        Assert-Equal $packages[$id].Sha256 (Get-FileHash -LiteralPath $installed -Algorithm SHA256).Hash "Restored archive provenance for $id"
    }
    Copy-Item -LiteralPath $assetsPath -Destination (Join-Path $directory "$(Split-Path $Consumer -Leaf)-$Framework.assets.json")
    foreach ($file in Get-ChildItem -LiteralPath (Join-Path $Consumer 'obj') -File -Filter '*.nuget.g.*') {
        Copy-Item -LiteralPath $file.FullName -Destination (Join-Path $directory "$(Split-Path $Consumer -Leaf)-$Framework.$($file.Name)")
    }
}

try {
    $revision = (Invoke-Step 'revision' 'git' @('rev-parse', 'HEAD')).Trim()
    $dirty = Invoke-Step 'working-tree' 'git' @('status', '--porcelain')
    [xml]$versions = Get-Content -LiteralPath (Join-Path $root 'eng\Versions.props') -Raw
    [xml]$repositoryProperties = Get-Content -LiteralPath (Join-Path $root 'Directory.Build.props') -Raw
    [xml]$sourceProperties = Get-Content -LiteralPath (Join-Path $root 'src\Directory.Build.props') -Raw
    [xml]$centralPackages = Get-Content -LiteralPath (Join-Path $root 'Directory.Packages.props') -Raw
    $mstestVersion = (Read-Property $versions 'VersionPrefix') + '-preview.bazel'
    $platformVersion = (Read-Property $versions 'TestingPlatformVersionPrefix') + '-preview.bazel'
    $componentVersions = @{
        MSTestVersion = $mstestVersion; MSTestWindowsUIAutomationVersion = $mstestVersion
        MicrosoftTestingPlatformVersion = $platformVersion
        MicrosoftTestingExtensionsCodeCoverageVersion = Read-Property $versions 'MicrosoftTestingExtensionsCodeCoverageVersion'
    }
    foreach ($name in @('MicrosoftNETTestSdkVersion', 'MicrosoftTestingExtensionsFakesVersion', 'MicrosoftPlaywrightVersion', 'AspireHostingTestingVersion')) {
        $componentVersions[$name] = Read-Property $centralPackages $name
    }
    $targets = @('//:mstest_package', '//:mstest_sdk_package')
    if (-not $LayoutOnly) {
        $targets += @('//:packages', '//:framework_package', '//:adapter_package', '//:analyzers_package', '//:platform_msbuild_package', '//:telemetry_package')
    }
    $null = Invoke-Bazel 'pack' (@('build') + $targets + @("--jobs=$Jobs", '--noshow_progress', '--show_result=0'))
    $executionRoot = (Invoke-Bazel 'execution-root' @('info', 'execution_root')).Trim()
    $paths = (Invoke-Bazel 'package-files' @('cquery', "set($($targets -join ' '))", '--output=files', '--noshow_progress')).Trim() -split '\r?\n'
    $graphJson = Invoke-Bazel 'native-actions' @('aquery', "deps(set($($targets -join ' ')))", '--output=jsonproto', '--include_artifacts=false', '--noshow_progress')
    [IO.File]::WriteAllText((Join-Path $directory 'actions.json'), $graphJson)
    $actions = ($graphJson | ConvertFrom-Json).actions
    Assert-Equal 2 @($actions | Where-Object mnemonic -EQ 'TestFxSdkPackageInputs').Count 'Native SDK input-generation actions'
    Assert-Equal 2 @($actions | Where-Object mnemonic -EQ 'TestFxSdkNuGetPack').Count 'Native SDK packing actions'
    foreach ($action in $actions) {
        if (@($action.arguments | Where-Object { $_ -match '(?i)(^|[\\/])MSBuild\.(exe|dll)$' -or $_ -in @('msbuild', 'dotnet build', 'dotnet pack') }).Count) {
            throw 'The native producer action graph invokes MSBuild.'
        }
    }
    foreach ($relative in $paths) {
        $path = Join-Path $executionRoot $relative
        $archive = [IO.Compression.ZipFile]::OpenRead($path)
        try {
            $manifests = @($archive.Entries | Where-Object FullName -Like '*.nuspec')
            Assert-Equal 1 $manifests.Count 'Package manifest count'
            [xml]$nuspec = Read-Entry $archive $manifests[0].FullName
            $metadata = $nuspec.SelectSingleNode('//*[local-name()="metadata"]')
            $id = [string]$metadata.id
            if ($packages.ContainsKey($id)) { throw "Duplicate native package: $id" }
            $version = [string]$metadata.version
            Assert-Equal $(if ($id -like 'MSTest*') { $mstestVersion } else { $platformVersion }) $version "Native $id version"
            $fileHashes = @{}
            foreach ($entry in $archive.Entries) { $fileHashes[$entry.FullName] = Entry-Hash $entry }
            $packages[$id] = [pscustomobject]@{
                Id = $id; Version = $version; Path = $path
                Sha256 = (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash
                Files = $fileHashes
            }
            Copy-Item -LiteralPath $path -Destination (Join-Path $feed "$id.$version.nupkg")
            if ($id -notin @('MSTest', 'MSTest.Sdk')) { continue }
            $projectDirectory = Join-Path $root "src\Package\$id"
            [xml]$project = Get-Content -LiteralPath (Join-Path $projectDirectory "$id.csproj") -Raw
            $expectedDescription = (Read-Property $project 'PackageDescription').Replace('$(CommonProductDescription)', (Read-Property $sourceProperties 'CommonProductDescription'))
            Assert-Equal ($expectedDescription -replace '\r\n', "`n") ($metadata.description -replace '\r\n', "`n") "$id authored description"
            Assert-Equal 'PACKAGE.md' ([string]$metadata.readme) "$id readme metadata"
            Assert-Equal 'MIT' ([string]$metadata.license.InnerText) "$id license"
            Assert-Equal (Get-FileHash -LiteralPath (Join-Path $projectDirectory 'PACKAGE.md')).Hash $fileHashes['PACKAGE.md'] "$id readme bytes"
            $payload = @($archive.Entries.FullName | Where-Object { $_ -notlike '*.nuspec' -and $_ -notlike '_rels/*' -and $_ -notlike 'package/*' -and $_ -cne '[Content_Types].xml' })
            $groups = @($metadata.SelectNodes('./*[local-name()="dependencies"]/*[local-name()="group"]'))
            if ($id -eq 'MSTest.Sdk') {
                $sdkPaths = @('Sdk/Sdk.props', 'Sdk/Sdk.targets', 'Sdk/Features/Aspire.targets', 'Sdk/Features/Playwright.targets',
                    'Sdk/Features/WindowsUIAutomation.targets', 'Sdk/Runner/Runner.targets', 'Sdk/Runner/Common.targets',
                    'Sdk/Runner/ClassicEngine.targets', 'Sdk/Runner/NativeAOT.targets', 'Sdk/VSTest/VSTest.targets')
                Assert-Set ($sdkPaths + 'PACKAGE.md') $payload 'SDK payload without assembly anchors or templates'
                Assert-Equal 0 $groups.Count 'SDK NuGet dependency groups'
                foreach ($asset in $sdkPaths) {
                    if ($asset -in @('Sdk/Sdk.props', 'Sdk/Runner/Runner.targets')) {
                        $expected = [IO.File]::ReadAllText((Join-Path $projectDirectory ($asset.Replace('/', '\') + '.template')))
                        foreach ($name in $componentVersions.Keys) { $expected = $expected.Replace('${' + $name + '}', $componentVersions[$name]) }
                        Assert-Equal $expected (Read-Entry $archive $asset) "Generated template $asset"
                    }
                    else {
                        Assert-Equal (Get-FileHash -LiteralPath (Join-Path $projectDirectory $asset.Replace('/', '\'))).Hash $fileHashes[$asset] "Unchanged SDK asset $asset"
                    }
                }
            }
            else {
                Assert-Set (@($frameworks | ForEach-Object { "lib/$_/_._" }) + 'PACKAGE.md') $payload 'Meta-package markers without assembly anchors'
                Assert-Set $frameworks @($groups.targetFramework) 'Meta-package dependency frameworks'
                $expectedDependencies = @{
                    'MSTest.TestFramework' = $mstestVersion; 'MSTest.TestAdapter' = $mstestVersion
                    'Microsoft.Testing.Extensions.TrxReport' = $platformVersion
                    'Microsoft.NET.Test.Sdk' = $componentVersions.MicrosoftNETTestSdkVersion
                    'Microsoft.Testing.Extensions.CodeCoverage' = $componentVersions.MicrosoftTestingExtensionsCodeCoverageVersion
                }
                foreach ($group in $groups) {
                    Assert-Set @($expectedDependencies.Keys) @($group.dependency.id) 'Meta-package dependency IDs'
                    foreach ($dependency in $group.dependency) {
                        Assert-Equal $expectedDependencies[$dependency.id] ([string]$dependency.version) "Meta-package dependency $($dependency.id)"
                        Assert-Equal $(if ($dependency.id -eq 'Microsoft.NET.Test.Sdk') { '' } else { 'Build,Analyzers' }) ([string]$dependency.exclude) "Dependency asset flow $($dependency.id)"
                    }
                }
            }
        }
        finally { $archive.Dispose() }
    }
    $expectedIds = @('MSTest', 'MSTest.Sdk')
    if (-not $LayoutOnly) {
        $expectedIds += @('MSTest.TestFramework', 'MSTest.TestAdapter', 'MSTest.Analyzers', 'Microsoft.Testing.Platform',
            'Microsoft.Testing.Extensions.TrxReport', 'Microsoft.Testing.Extensions.TrxReport.Abstractions',
            'Microsoft.Testing.Platform.MSBuild', 'Microsoft.Testing.Extensions.Telemetry')
    }
    Assert-Set $expectedIds @($packages.Keys) 'Native package set'
    $layoutPassed = $true

    $sdkVersion = [regex]::Match([IO.File]::ReadAllText((Join-Path $root 'MODULE.bazel')), 'dotnet\.toolchain\(dotnet_version = "([^"]+)"\)').Groups[1].Value
    if (-not $sdkVersion) { throw 'Could not identify the pinned native SDK.' }
    $dotnetName = if ($IsWindows) { 'dotnet.exe' } else { 'dotnet' }
    $sdks = @(Get-ChildItem -LiteralPath (Join-Path $executionRoot 'external') -Directory -Filter "*dotnet_sdk.$sdkVersion`_*" |
        Where-Object { Test-Path -LiteralPath (Join-Path $_.FullName $dotnetName) })
    Assert-Equal 1 $sdks.Count 'Pinned native SDK installations'
    $dotnet = Join-Path $sdks[0].FullName $dotnetName
    $consumerEnvironment = @{
        NUGET_PACKAGES = Join-Path $directory 'restored-packages'
        NUGET_HTTP_CACHE_PATH = Join-Path $directory 'http-cache'
        NUGET_FALLBACK_PACKAGES = $null
        DOTNET_CLI_HOME = Join-Path $directory 'cli-home'
        DOTNET_ROOT = $sdks[0].FullName; DOTNET_ROOT_X64 = $sdks[0].FullName
        DOTNET_ROOT_ARM64 = $sdks[0].FullName
        DOTNET_HOST_PATH = $dotnet; DOTNET_ROLL_FORWARD = $null; DOTNET_ROLL_FORWARD_TO_PRERELEASE = $null
        MSBuildSDKsPath = $null; MSBuildExtensionsPath = $null; DOTNET_MSBUILD_SDK_RESOLVER_CLI_DIR = $null
        MSBUILD_EXE_PATH = $null
        DOTNET_MSBUILD_SDK_RESOLVER_SDKS_DIR = $null; DOTNET_MSBUILD_SDK_RESOLVER_SDKS_VER = $null
        DOTNET_NOLOGO = '1'; DOTNET_SKIP_FIRST_TIME_EXPERIENCE = '1'; DOTNET_CLI_TELEMETRY_OPTOUT = '1'
    }
    [xml]$repositoryFeeds = Get-Content -LiteralPath (Join-Path $root 'NuGet.config') -Raw
    $sourceEntries = '<add key="native" value="' + [Security.SecurityElement]::Escape($feed) + '" />'
    $mappings = '<packageSource key="native"><package pattern="MSTest" /><package pattern="MSTest.*" /><package pattern="Microsoft.Testing.*" /></packageSource>'
    foreach ($source in $repositoryFeeds.configuration.packageSources.add) {
        $sourceEntries += '<add key="' + $source.key + '" value="' + [Security.SecurityElement]::Escape($source.value) + '" />'
        $mappings += '<packageSource key="' + $source.key + '"><package pattern="*" /><package pattern="Microsoft.Testing.Extensions.CodeCoverage" /></packageSource>'
    }
    $nugetConfiguration = "<configuration><packageSources><clear />$sourceEntries</packageSources><fallbackPackageFolders><clear /></fallbackPackageFolders><packageSourceMapping>$mappings</packageSourceMapping></configuration>"
    $evaluationCases = @(
        @{ Name = 'sdk-default'; Properties = ''; Expected = @('MSTest.TestFramework', 'MSTest.TestAdapter', 'Microsoft.Testing.Extensions.TrxReport', 'Microsoft.Testing.Extensions.CodeCoverage') },
        @{ Name = 'sdk-none'; Properties = '<TestingExtensionsProfile>None</TestingExtensionsProfile>'; Expected = @('MSTest.TestFramework', 'MSTest.TestAdapter') },
        @{ Name = 'sdk-disabled'; Properties = '<EnableMicrosoftTestingExtensionsTrxReport>false</EnableMicrosoftTestingExtensionsTrxReport><EnableMicrosoftTestingExtensionsCodeCoverage>false</EnableMicrosoftTestingExtensionsCodeCoverage>'; Expected = @('MSTest.TestFramework', 'MSTest.TestAdapter') },
        @{ Name = 'sdk-library'; Properties = '<IsTestApplication>false</IsTestApplication>'; Expected = @('MSTest.TestFramework') },
        @{ Name = 'sdk-layered'; Properties = ''; Layered = $true; Expected = @('MSTest.TestFramework', 'MSTest.TestAdapter', 'Microsoft.Testing.Extensions.TrxReport', 'Microsoft.Testing.Extensions.CodeCoverage') },
        @{ Name = 'sdk-central'; Properties = '<ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>'; Expected = @('MSTest.TestFramework', 'MSTest.TestAdapter', 'Microsoft.Testing.Extensions.TrxReport', 'Microsoft.Testing.Extensions.CodeCoverage') },
        @{ Name = 'sdk-vstest-precedence'; Properties = '<UseVSTest>true</UseVSTest><PublishAot>true</PublishAot>'; Expected = @('MSTest.TestFramework', 'MSTest.TestAdapter', 'Microsoft.NET.Test.Sdk') },
        @{ Name = 'sdk-source-generation'; Properties = '<EnableMSTestSourceGeneration>true</EnableMSTestSourceGeneration>'; Expected = @('MSTest.TestFramework', 'MSTest.TestAdapter', 'Microsoft.Testing.Extensions.TrxReport', 'Microsoft.Testing.Extensions.CodeCoverage', 'MSTest.SourceGeneration') },
        @{ Name = 'sdk-aot-selection'; Properties = '<PublishAot>true</PublishAot>'; Expected = @('Microsoft.Testing.Platform.MSBuild', 'MSTest.TestFramework', 'MSTest.SourceGeneration', 'MSTest.TestAdapter', 'Microsoft.Testing.Extensions.TrxReport', 'Microsoft.Testing.Extensions.CodeCoverage', 'Microsoft.CodeCoverage.MSBuild') },
        @{ Name = 'sdk-version-overrides'; Properties = '<MSTestVersion>7.8.9</MSTestVersion><MicrosoftTestingPlatformVersion>3.2.1</MicrosoftTestingPlatformVersion>'; MSTestVersion = '7.8.9'; PlatformVersion = '3.2.1'; Expected = @('MSTest.TestFramework', 'MSTest.TestAdapter', 'Microsoft.Testing.Extensions.TrxReport', 'Microsoft.Testing.Extensions.CodeCoverage') }
    )
    foreach ($case in $evaluationCases) {
        $consumer = New-Consumer $case.Name $case.Properties -Layered:([bool]$case.Layered)
        $result = Invoke-Step "evaluate-$($case.Name)" $dotnet @('msbuild', 'Consumer.csproj', '-nologo',
            '-getProperty:UsingMSTestSdk,MSTestVersion,MicrosoftTestingPlatformVersion,_MSTestSdkImportsMicrosoftNETSdk,OutputType,NETCoreSdkVersion,MSBuildToolsPath',
            '-getItem:PackageReference,PackageVersion', "-bl:$(Join-Path $directory "$($case.Name)-evaluation.binlog")") $consumer -Environment $consumerEnvironment
        $evaluation = $result | ConvertFrom-Json -AsHashtable
        $references = @($evaluation.Items.PackageReference | Where-Object Sdk -EQ 'MSTest')
        Assert-Set $case.Expected @($references.Identity) "$($case.Name) selected SDK packages"
        $expectedMSTestVersion = if ($case.MSTestVersion) { $case.MSTestVersion } else { $mstestVersion }
        $expectedPlatformVersion = if ($case.PlatformVersion) { $case.PlatformVersion } else { $platformVersion }
        Assert-Equal $expectedMSTestVersion $evaluation.Properties.MSTestVersion "$($case.Name) MSTest version"
        Assert-Equal $expectedPlatformVersion $evaluation.Properties.MicrosoftTestingPlatformVersion "$($case.Name) platform version"
        Assert-Equal $sdkVersion $evaluation.Properties.NETCoreSdkVersion "$($case.Name) actual SDK"
        Assert-Equal ([IO.Path]::GetFullPath((Join-Path $sdks[0].FullName "sdk\$sdkVersion")).TrimEnd('\', '/')) `
            ([IO.Path]::GetFullPath($evaluation.Properties.MSBuildToolsPath).TrimEnd('\', '/')) "$($case.Name) MSBuild tool provenance"
        Assert-Equal 'true' $evaluation.Properties.UsingMSTestSdk "$($case.Name) SDK import"
        Assert-Equal $(if ($case.Layered) { 'false' } else { 'true' }) $evaluation.Properties._MSTestSdkImportsMicrosoftNETSdk "$($case.Name) layered imports"
        if ($case.Name -eq 'sdk-library') { Assert-Equal 'Library' $evaluation.Properties.OutputType 'Test library output' }
        $versionedReferences = $references
        if ($case.Name -eq 'sdk-central') {
            Assert-Set $case.Expected @($evaluation.Items.PackageVersion.Identity) 'Central package versions'
            foreach ($reference in $references) { Assert-Equal '' ([string]$reference.Version) 'CPM reference without inline version' }
            $versionedReferences = $evaluation.Items.PackageVersion
        }
        foreach ($reference in $versionedReferences) {
            $expectedVersion = if ($reference.Identity -like 'MSTest.*') { $expectedMSTestVersion }
            elseif ($reference.Identity -in @('Microsoft.Testing.Extensions.CodeCoverage', 'Microsoft.CodeCoverage.MSBuild')) { $componentVersions.MicrosoftTestingExtensionsCodeCoverageVersion }
            elseif ($reference.Identity -eq 'Microsoft.NET.Test.Sdk') { $componentVersions.MicrosoftNETTestSdkVersion }
            else { $expectedPlatformVersion }
            Assert-Equal $expectedVersion $reference.Version "$($case.Name) reference $($reference.Identity)"
        }
        if ((Get-Content -LiteralPath (Join-Path $directory "evaluate-$($case.Name).log") -Raw).Contains('MSB4011')) { throw 'Duplicate SDK imports.' }
        $evaluations.Add([pscustomobject]@{ Name = $case.Name; Directory = $consumer; Evaluation = $evaluation })
    }
    $cachedSdk = Join-Path $consumerEnvironment.NUGET_PACKAGES "mstest.sdk\$mstestVersion\mstest.sdk.$mstestVersion.nupkg"
    Assert-Equal $packages['MSTest.Sdk'].Sha256 (Get-FileHash -LiteralPath $cachedSdk -Algorithm SHA256).Hash 'Resolved native SDK archive'
    $invalid = New-Consumer 'sdk-invalid-profile' '<TestingExtensionsProfile>Invalid</TestingExtensionsProfile>'
    $diagnostic = Invoke-Step 'invalid-profile' $dotnet @('msbuild', 'Consumer.csproj', '-nologo', '-t:_MSTestSDKValidateTestingExtensionsProfile',
        "-bl:$(Join-Path $directory 'invalid-profile.binlog')") $invalid -ExpectedExit 1 -Environment $consumerEnvironment
    if (-not $diagnostic.Contains("Valid values are 'Default', 'AllMicrosoft' and 'None'.")) { throw 'The invalid-profile diagnostic was not observed.' }
    if ($LayoutOnly) {
        $succeeded = $true
        Write-Host "Native SDK/meta layouts and SDK evaluation passed; composed consumers were not requested. Evidence: $directory"
        exit 0
    }

    $defaultConsumer = Join-Path $directory 'sdk-default'
    $noneConsumer = Join-Path $directory 'sdk-none'
    $metaConsumer = New-Consumer 'meta-default' '' -Meta
    $expectedHashes = @{
        'MSTest.TestFramework' = $packages['MSTest.TestFramework'].Files['lib/net11.0/MSTest.TestFramework.dll']
        'Microsoft.Testing.Platform' = $packages['Microsoft.Testing.Platform'].Files['lib/net11.0/Microsoft.Testing.Platform.dll']
    }
    foreach ($assemblyName in $expectedHashes.Keys) {
        if (-not $expectedHashes[$assemblyName]) { throw "The native packages have no net11 runtime assembly for $assemblyName." }
    }
    $expectations = Join-Path $directory 'expected-assemblies.json'
    $expectedHashes | ConvertTo-Json | Set-Content -LiteralPath $expectations -Encoding utf8
    $consumerEnvironment.TESTFX_BAZEL_PACKAGE_EXPECTATIONS = $expectations
    foreach ($consumer in @($defaultConsumer, $noneConsumer, $metaConsumer)) {
        Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'SdkConsumerTests.cs'), (Join-Path $PSScriptRoot 'PackageProvenance.cs') -Destination $consumer
        $name = Split-Path $consumer -Leaf
        foreach ($framework in $(if ($consumer -eq $noneConsumer) { @('net11.0') } else { $frameworks })) {
            $projectPath = Join-Path $consumer 'Consumer.csproj'
            [xml]$project = Get-Content -LiteralPath $projectPath -Raw
            $project.SelectSingleNode('/Project/PropertyGroup/TargetFramework').InnerText = $framework
            $project.Save($projectPath)
            $null = Invoke-Step "build-$name-$framework" $dotnet @('build', 'Consumer.csproj', '--disable-build-servers', '-c', 'Release',
                '--configfile', (Join-Path $consumer 'NuGet.config'), '--nologo', '-v:quiet',
                "-bl:$(Join-Path $directory "$name-$framework.binlog")") $consumer -Environment $consumerEnvironment
            Assert-ResolvedPackages $consumer $framework
        }
    }
    foreach ($consumer in @($defaultConsumer, $noneConsumer)) {
        $name = Split-Path $consumer -Leaf
        $assembly = Join-Path $consumer 'bin\Release\net11.0\Consumer.dll'
        $help = Invoke-Step "help-$name" $dotnet @($assembly, '--help') $consumer -Environment $consumerEnvironment
        Assert-Equal ($consumer -eq $defaultConsumer) ([regex]::IsMatch($help, '(?m)^\s+--report-trx(?:\s|$)')) "$name TRX registration"
        Assert-Equal ($consumer -eq $defaultConsumer) ([regex]::IsMatch($help, '(?m)^\s+--coverage(?:\s|$)')) "$name coverage registration"
        $discovery = Invoke-Step "discover-$name" $dotnet @($assembly, '--list-tests') $consumer -Environment $consumerEnvironment
        foreach ($test in @('NativePackagePass', 'NativePackageFailure')) {
            if (-not $discovery.Contains($test)) { throw "$name did not discover $test." }
        }
        $passArgs = @($assembly, '--filter', 'FullyQualifiedName~NativePackagePass', '--minimum-expected-tests', '1')
        $failureArgs = @($assembly, '--filter', 'FullyQualifiedName~NativePackageFailure')
        if ($consumer -eq $defaultConsumer) {
            $reportArgs = @('--report-trx', '--results-directory', (Join-Path $consumer 'results with spaces!'))
            $passArgs += $reportArgs + @('--report-trx-filename', 'pass.trx')
            $failureArgs += $reportArgs + @('--report-trx-filename', 'failure.trx')
        }
        $pass = Invoke-Step "pass-$name" $dotnet $passArgs $consumer -Environment $consumerEnvironment
        foreach ($assemblyName in $expectedHashes.Keys) {
            if (-not $pass.Contains("BAZEL_PACKAGE_ASSEMBLY: $assemblyName;")) { throw "$name did not verify its loaded native $assemblyName." }
        }
        $failure = Invoke-Step "failure-$name" $dotnet $failureArgs $consumer -ExpectedExit 2 -Environment $consumerEnvironment
        if (-not $failure.Contains('Intentional native package consumer failure.')) { throw "$name did not execute the intentional failure." }
        $empty = Invoke-Step "empty-$name" $dotnet @($assembly, '--filter', 'FullyQualifiedName~NoMatchingNativeTest', '--minimum-expected-tests', '1') $consumer -ExpectedExit 9 -Environment $consumerEnvironment
        if (-not $empty.Contains('tests ran 0, minimum expected 1')) { throw "$name did not enforce the zero-result policy." }
        if ($consumer -eq $defaultConsumer) {
            Assert-Trx (Join-Path $consumer 'results with spaces!\pass.trx') 'NativePackagePass' 'Passed'
            Assert-Trx (Join-Path $consumer 'results with spaces!\failure.trx') 'NativePackageFailure' 'Failed'
        }
    }
    $nativeDriverResults = Join-Path $defaultConsumer 'native driver results'
    $null = Invoke-Step 'sdk-dotnet-test' $dotnet @('test', '--project', 'Consumer.csproj', '--no-build', '-c', 'Release',
        '--filter', 'FullyQualifiedName~NativePackagePass', '--minimum-expected-tests', '1', '--report-trx',
        '--report-trx-filename', 'driver.trx', '--results-directory', $nativeDriverResults,
        "-bl:$(Join-Path $directory 'sdk-dotnet-test.binlog')") $defaultConsumer -Environment $consumerEnvironment
    Assert-Trx (Join-Path $nativeDriverResults 'driver.trx') 'NativePackagePass' 'Passed'
    $metaResults = Join-Path $metaConsumer 'results'
    $metaDiscovery = Invoke-Step 'discover-meta' $dotnet @('test', 'Consumer.csproj', '--no-build', '-c', 'Release', '--list-tests',
        "-bl:$(Join-Path $directory 'meta-discovery.binlog')") $metaConsumer -Environment $consumerEnvironment
    foreach ($test in @('NativePackagePass', 'NativePackageFailure')) {
        if (-not $metaDiscovery.Contains($test)) { throw "The meta-package consumer did not discover $test." }
    }
    foreach ($case in @(@{ Name = 'pass'; Test = 'NativePackagePass'; Exit = 0; Outcome = 'Passed' }, @{ Name = 'failure'; Test = 'NativePackageFailure'; Exit = 1; Outcome = 'Failed' })) {
        $null = Invoke-Step "meta-$($case.Name)" $dotnet @('test', 'Consumer.csproj', '--no-build', '-c', 'Release',
            '--filter', "FullyQualifiedName~$($case.Test)", '--logger', "trx;LogFileName=$($case.Name).trx", '--results-directory', $metaResults,
            "-bl:$(Join-Path $directory "meta-$($case.Name).binlog")") $metaConsumer -ExpectedExit $case.Exit -Environment $consumerEnvironment
        Assert-Trx (Join-Path $metaResults "$($case.Name).trx") $case.Test $case.Outcome
    }
    $consumerPassed = $true
    $succeeded = $true
    Write-Host "Native SDK/meta-package consumers passed. Evidence: $directory"
}
finally {
    [pscustomobject]@{
        Passed = $succeeded; LayoutPassed = $layoutPassed; ConsumersPassed = $consumerPassed; LayoutOnly = [bool]$LayoutOnly
        Revision = $revision; WorkingTree = $dirty; Packages = @($packages.Values); Evaluations = $evaluations; Steps = $steps
    } | ConvertTo-Json -Depth 15 | Set-Content -LiteralPath (Join-Path $directory 'validation.json') -Encoding utf8
}

exit 0
