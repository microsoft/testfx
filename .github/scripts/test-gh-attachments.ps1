[CmdletBinding()]
param(
    [ValidateSet('Run', 'Verify')]
    [string] $Mode = 'Run',
    [Parameter(Mandatory)]
    [string] $WorkDirectory,
    [string] $DownloadDirectory
)

$ErrorActionPreference = 'Stop'
$repo = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$work = [IO.Path]::GetFullPath($WorkDirectory)
$staging = Join-Path $work 'attachments'
$expectedPath = Join-Path $work 'expected.json'

if ($Mode -eq 'Verify') {
    if (-not $DownloadDirectory) {
        throw 'DownloadDirectory is required for Verify.'
    }

    $expected = Get-Content -LiteralPath $expectedPath -Raw | ConvertFrom-Json
    foreach ($entry in $expected.files) {
        $downloaded = Join-Path $DownloadDirectory $entry.path
        if (-not (Test-Path -LiteralPath $downloaded -PathType Leaf)) {
            throw "Uploaded archive omitted $($entry.path)."
        }

        if ((Get-FileHash -LiteralPath $downloaded -Algorithm SHA256).Hash -cne $entry.sha256) {
            throw "Uploaded archive changed $($entry.path)."
        }
    }

    $actual = @(Get-ChildItem -LiteralPath $DownloadDirectory -File -Recurse)
    if ($actual.Count -ne $expected.files.Count) {
        throw "Expected $($expected.files.Count) archive files, found $($actual.Count)."
    }

    $summary = [IO.File]::ReadAllText((Join-Path $work 'linked.summary.md'))
    $blocks = [regex]::Matches($summary, '(?s)<details>\s*<summary><code>(?<name>.*?)</code></summary>(?<body>.*?)</details>')
    $manifests = @(Get-ChildItem -LiteralPath $DownloadDirectory -Filter attachments.json -File -Recurse)
    if ($blocks.Count -ne 4 -or -not $env:ATTACHMENT_URL) {
        throw 'Expected four post-upload test-details blocks and a real artifact URL.'
    }

    foreach ($manifestFile in $manifests) {
        $module = Get-Content -LiteralPath $manifestFile.FullName -Raw | ConvertFrom-Json
        $test = $module.tests[0]
        $name = "$($module.assemblyName) ($($module.targetFramework)/$($module.architecture), attempt $($module.attemptNumber)): $($test.fullyQualifiedName) - $($test.displayName)"
        $path = $test.attachments[0].relativePath
        $matchedBlocks = @($blocks | Where-Object { $_.Groups['body'].Value.Contains($path) })
        if ($matchedBlocks.Count -ne 1 -or [Net.WebUtility]::HtmlDecode($matchedBlocks[0].Groups['name'].Value) -cne $name -or -not $matchedBlocks[0].Groups['body'].Value.Contains("[Download attachments]($env:ATTACHMENT_URL)")) {
            throw 'The post-upload details lost the test, file, or actual download-link association.'
        }
    }

    Write-Output "Verified downloaded artifact: $($actual.Count) files, including both module manifests."
    exit 0
}

if (Test-Path -LiteralPath $work) {
    throw "Use a fresh task-owned WorkDirectory: $work"
}

$null = New-Item -ItemType Directory -Path $work
$shipping = [IO.Path]::Combine($repo, 'artifacts', 'packages', 'Release', 'Shipping')
$dotnet = Join-Path (Join-Path $repo '.dotnet') $(if ($IsWindows) { 'dotnet.exe' } else { 'dotnet' })
$packages = @{}
$provenance = @()
foreach ($id in @('MSTest.TestFramework', 'MSTest.TestAdapter', 'Microsoft.Testing.Platform', 'Microsoft.Testing.Platform.MSBuild', 'Microsoft.Testing.Extensions.TrxReport', 'Microsoft.Testing.Extensions.GitHubActionsReport')) {
    $candidates = @(Get-ChildItem -LiteralPath $shipping -Filter "$id.*.nupkg" -File |
        Where-Object { $_.Name -match ('^' + [regex]::Escape($id) + '\.\d') -and $_.Name -notlike '*.symbols.nupkg' })
    if ($candidates.Count -ne 1) {
        throw "Expected one freshly packed $id package, found $($candidates.Count)."
    }

    $archive = [IO.Compression.ZipFile]::OpenRead($candidates[0].FullName)
    try {
        $entry = @($archive.Entries | Where-Object FullName -like '*.nuspec')[0]
        $reader = [IO.StreamReader]::new($entry.Open())
        try {
            [xml] $nuspec = $reader.ReadToEnd()
        }
        finally {
            $reader.Dispose()
        }

        $packages[$id] = $nuspec.package.metadata.version
    }
    finally {
        $archive.Dispose()
    }

    $provenance += [ordered]@{
        id = $id
        version = $packages[$id]
        sha256 = (Get-FileHash -LiteralPath $candidates[0].FullName -Algorithm SHA256).Hash
    }
}

$provenance | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $work 'packages.json') -Encoding utf8
$source = @'
using System.Reflection;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Smoke;

[TestClass]
public sealed class AttachmentTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public void FailsWithAttachment()
    {
        AddAttachment("failed.txt", "failed attachment");
        if (Environment.GetEnvironmentVariable("TESTFX_ATTACHMENT_SMOKE_PASS") != "true")
        {
            Assert.Fail("Intentional smoke-test failure.");
        }
    }

    [TestMethod]
    public void PassingTest() => AddAttachment("passing-only.txt", "must not be staged");

    private void AddAttachment(string name, string content)
    {
        string directory = Path.Combine(
            Environment.GetEnvironmentVariable("TESTFX_ATTACHMENT_SMOKE_ORIGINALS")!,
            Assembly.GetExecutingAssembly().GetName().Name!);
        Directory.CreateDirectory(directory);
        string path = Path.Combine(directory, name);
        File.WriteAllText(path, content + " from " + Assembly.GetExecutingAssembly().GetName().Name, new UTF8Encoding(false));
        TestContext.AddResultFile(path);
    }
}
'@

$sdkVersion = (Get-Content -LiteralPath (Join-Path $repo 'global.json') -Raw | ConvertFrom-Json).sdk.version
@{ sdk = @{ version = $sdkVersion; paths = @((Join-Path $repo '.dotnet')) }; test = @{ runner = 'Microsoft.Testing.Platform' } } |
    ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $work 'global.json') -Encoding utf8
$escapedShipping = [Security.SecurityElement]::Escape($shipping)
@"
<configuration><packageSources><clear />
<add key="shipping" value="$escapedShipping" />
<add key="nuget" value="https://api.nuget.org/v3/index.json" />
</packageSources></configuration>
"@ | Set-Content -LiteralPath (Join-Path $work 'NuGet.config') -Encoding utf8

function Invoke-Dotnet([string[]] $Arguments, [string] $Log, [int] $ExpectedExit = 0) {
    & $dotnet @Arguments *> $Log
    $actualExit = $LASTEXITCODE
    if ($actualExit -ne $ExpectedExit) {
        Get-Content -LiteralPath $Log -Tail 60 | Write-Output
        throw "Expected dotnet exit $ExpectedExit, observed $actualExit. See $Log."
    }
}

function Assert-Trx([string] $Path, [int] $Failed, [int] $Modules = 1) {
    [xml] $trx = Get-Content -LiteralPath $Path -Raw
    $counters = $trx.TestRun.ResultSummary.Counters
    $total = 2 * $Modules
    if ([int] $counters.total -ne $total -or [int] $counters.executed -ne $total -or [int] $counters.failed -ne $Failed -or [int] $counters.passed -ne ($total - $Failed) -or [int] $counters.notExecuted -ne 0) {
        throw "Unexpected executed test counts in $Path."
    }

    $results = @($trx.TestRun.Results.UnitTestResult)
    if ($results.Count -ne $total -or @($results.executionId | Sort-Object -Unique).Count -ne $total) {
        throw "Missing or duplicate test executions in $Path."
    }

    foreach ($name in @('FailsWithAttachment', 'PassingTest')) {
        $result = @($results | Where-Object testName -CEQ $name)
        $outcome = if ($name -eq 'FailsWithAttachment' -and $Failed -gt 0) { 'Failed' } else { 'Passed' }
        if ($result.Count -ne $Modules -or @($result | Where-Object outcome -CNE $outcome).Count -ne 0) {
            throw "Wrong identity/outcome for $name in $Path."
        }
    }
}

$environmentNames = @('NUGET_PACKAGES', 'GITHUB_ACTIONS', 'GITHUB_STEP_SUMMARY', 'TESTFX_ATTACHMENT_SMOKE_PASS', 'TESTFX_ATTACHMENT_SMOKE_ORIGINALS')
$previous = @{}
foreach ($name in $environmentNames) {
    $previous[$name] = [Environment]::GetEnvironmentVariable($name)
}

$summaries = @()
try {
    $env:NUGET_PACKAGES = Join-Path $work '.packages'
    $env:GITHUB_ACTIONS = 'true'
    foreach ($module in @('AttachmentsA', 'AttachmentsB')) {
        $directory = Join-Path $work $module
        $null = New-Item -ItemType Directory -Path $directory
        [IO.File]::WriteAllText((Join-Path $directory 'Tests.cs'), $source, [Text.UTF8Encoding]::new($true))
        $references = ($packages.Keys | Sort-Object | ForEach-Object {
            "    <PackageReference Include=`"$_`" Version=`"$($packages[$_])`" />"
        }) -join "`n"
        $project = Join-Path $directory "$module.csproj"
        @"
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <OutputType>Exe</OutputType>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <EnableMSTestRunner>true</EnableMSTestRunner>
    <GenerateTestingPlatformEntryPoint>true</GenerateTestingPlatformEntryPoint>
    <TestingPlatformDotnetTestSupport>true</TestingPlatformDotnetTestSupport>
  </PropertyGroup>
  <ItemGroup>
$references
  </ItemGroup>
</Project>
"@ | Set-Content -LiteralPath $project -Encoding utf8
        Invoke-Dotnet @('build', $project, '-c', 'Release', "-p:RestoreConfigFile=$(Join-Path $work 'NuGet.config')", "-bl:$(Join-Path $directory 'build.binlog')") (Join-Path $directory 'build.log')
        $assets = Get-Content -LiteralPath (Join-Path $directory 'obj/project.assets.json') -Raw | ConvertFrom-Json
        if (@($assets.packageFolders.PSObject.Properties.Name).Count -ne 1 -or [IO.Path]::GetFullPath(@($assets.packageFolders.PSObject.Properties.Name)[0]).TrimEnd('\', '/') -cne [IO.Path]::GetFullPath($env:NUGET_PACKAGES).TrimEnd('\', '/')) {
            throw 'The smoke consumer did not use its isolated package cache.'
        }

        foreach ($id in $packages.Keys) {
            $payload = Join-Path $env:NUGET_PACKAGES "$($id.ToLowerInvariant())/$($packages[$id])/$($id.ToLowerInvariant()).$($packages[$id]).nupkg"
            $expectedHash = @($provenance | Where-Object id -CEQ $id)[0].sha256
            if ((Get-FileHash -LiteralPath $payload -Algorithm SHA256).Hash -cne $expectedHash) {
                throw "The smoke consumer did not resolve the freshly packed $id bits."
            }
        }

        foreach ($id in @('Microsoft.Testing.Platform', 'Microsoft.Testing.Extensions.GitHubActionsReport')) {
            $resolved = $assets.targets.'net8.0'.PSObject.Properties["$id/$($packages[$id])"].Value
            $runtimeAsset = @($resolved.runtime.PSObject.Properties.Name | Where-Object { $_.EndsWith("/$id.dll", [StringComparison]::Ordinal) })
            if ($runtimeAsset.Count -ne 1) {
                throw "Expected one resolved runtime assembly for $id."
            }

            $cached = Join-Path $env:NUGET_PACKAGES "$($id.ToLowerInvariant())/$($packages[$id])/$($runtimeAsset[0])"
            $executableLayout = Join-Path $directory "bin/Release/net8.0/$id.dll"
            if ((Get-FileHash -LiteralPath $cached -Algorithm SHA256).Hash -cne (Get-FileHash -LiteralPath $executableLayout -Algorithm SHA256).Hash) {
                throw "The executable layout contains stale $id bits."
            }
        }

        $hostDll = Join-Path $directory "bin/Release/net8.0/$module.dll"
        foreach ($case in $(if ($module -eq 'AttachmentsA') { @('disabled', 'passing', 'failed') } else { @('failed') })) {
            $env:TESTFX_ATTACHMENT_SMOKE_PASS = if ($case -eq 'passing') { 'true' } else { 'false' }
            $env:TESTFX_ATTACHMENT_SMOKE_ORIGINALS = Join-Path $directory "originals-$case"
            $env:GITHUB_STEP_SUMMARY = Join-Path $directory "$case.summary.md"
            $trx = Join-Path $directory "$case.trx"
            $arguments = @($hostDll, '--report-gh', '--report-trx', '--report-trx-filename', $trx)
            if ($case -ne 'disabled') {
                $arguments += @('--report-gh-attachments', $(if ($case -eq 'passing') { Join-Path $work 'passing-attachments' } else { $staging }))
            }

            Invoke-Dotnet $arguments (Join-Path $directory "$case.log") $(if ($case -eq 'passing') { 0 } else { 2 })
            Assert-Trx $trx $(if ($case -eq 'passing') { 0 } else { 1 })
            if ($case -eq 'disabled' -and (Test-Path -LiteralPath $staging)) {
                throw 'Staging occurred without the attachment option.'
            }

            if ($case -eq 'disabled' -and ([IO.File]::ReadAllText($env:GITHUB_STEP_SUMMARY)).Contains('Attachments (paths inside the workflow artifact):')) {
                throw 'The attachment section was rendered without the attachment option.'
            }

            if ($case -eq 'passing' -and (Test-Path -LiteralPath (Join-Path $work 'passing-attachments'))) {
                throw 'A passing run staged attachments.'
            }

            if ($case -eq 'failed') {
                $summaries += $env:GITHUB_STEP_SUMMARY
            }

            Write-Output "$module/${case}: host exit $(if ($case -eq 'passing') { 0 } else { 2 }); executed 2; failed $(if ($case -eq 'passing') { 0 } else { 1 })."
        }
    }

    $solution = Join-Path $work 'Attachments.slnx'
    '<Solution><Project Path="AttachmentsA/AttachmentsA.csproj" /><Project Path="AttachmentsB/AttachmentsB.csproj" /></Solution>' |
        Set-Content -LiteralPath $solution -Encoding utf8
    $env:TESTFX_ATTACHMENT_SMOKE_PASS = 'false'
    $env:TESTFX_ATTACHMENT_SMOKE_ORIGINALS = Join-Path $work 'native-originals'
    $env:GITHUB_STEP_SUMMARY = Join-Path $work 'native.summary.md'
    $nativeResults = Join-Path $work 'native-results'
    Invoke-Dotnet @('test', '--solution', $solution, '-c', 'Release', '--no-build', '--report-gh', '--report-gh-attachments', $staging, '--report-trx', '--results-directory', $nativeResults) (Join-Path $work 'native.log') 2
    $nativeReports = @(Get-ChildItem -LiteralPath $nativeResults -Filter '*.trx' -File)
    if ($nativeReports.Count -ne 2) {
        throw "The real multi-module dotnet test run produced $($nativeReports.Count) reports, expected 2."
    }

    foreach ($report in $nativeReports) {
        Assert-Trx $report.FullName 1
    }

    foreach ($module in @('AttachmentsA', 'AttachmentsB')) {
        if (@($nativeReports | Where-Object Name -like "$module`_*").Count -ne 1) {
            throw "The native run omitted the $module report."
        }
    }

    $mergedDirectory = Join-Path $nativeResults 'merged'
    if (Test-Path -LiteralPath $mergedDirectory) {
        $merged = @(Get-ChildItem -LiteralPath $mergedDirectory -Filter '*.trx' -File)
        if ($merged.Count -ne 1) {
            throw 'Expected one authoritative merged TRX report.'
        }

        Assert-Trx $merged[0].FullName 2 2
    }

    $summaries += $env:GITHUB_STEP_SUMMARY
    Write-Output 'Native multi-module dotnet test: parent exit 2; executed 4; failed 2.'
}
finally {
    foreach ($name in $environmentNames) {
        [Environment]::SetEnvironmentVariable($name, $previous[$name])
    }
}

$manifests = @(Get-ChildItem -LiteralPath $staging -Filter attachments.json -Recurse -File)
if ($manifests.Count -ne 4) {
    throw "Expected direct and native manifests for both modules, found $($manifests.Count)."
}

$observedModules = @()
foreach ($manifestFile in $manifests) {
    $manifest = Get-Content -LiteralPath $manifestFile.FullName -Raw | ConvertFrom-Json
    $observedModules += $manifest.assemblyName
    if ($manifest.schemaVersion -ne 1 -or $manifest.tests.Count -ne 1 -or $manifest.tests[0].fullyQualifiedName -notlike '*FailsWithAttachment') {
        throw 'The manifest lost the failed-test association.'
    }

    $attachment = @($manifest.tests[0].attachments)
    if ($attachment.Count -ne 1 -or $attachment[0].status -cne 'staged' -or -not $attachment[0].relativePath) {
        throw 'The manifest does not describe exactly one staged failed-test file.'
    }

    $filePath = Join-Path $staging $attachment[0].relativePath
    $content = [IO.File]::ReadAllText($filePath)
    if ($content -cne "failed attachment from $($manifest.assemblyName)" -or (Get-Item -LiteralPath $filePath).Length -ne $attachment[0].sizeBytes) {
        throw 'Staged attachment contents/size do not match the producing test.'
    }

    $matchedBlocks = 0
    foreach ($summaryFile in $summaries) {
        $summary = [IO.File]::ReadAllText($summaryFile)
        foreach ($block in [regex]::Matches($summary, '(?s)<details>\s*<summary><code>(?<name>.*?)</code>.*?</summary>(?<body>.*?)</details>')) {
            if (-not $block.Groups['body'].Value.Contains($attachment[0].relativePath)) {
                continue
            }

            $matchedBlocks++
            if ([Net.WebUtility]::HtmlDecode($block.Groups['name'].Value) -cne $manifest.tests[0].fullyQualifiedName) {
                throw 'The archive path was associated with the wrong rendered test.'
            }

            $headings = [regex]::Matches($summary.Substring(0, $block.Index), '(?m)^#{2,6} .*Attachments[AB].*$')
            if ($headings.Count -eq 0 -or -not $headings[$headings.Count - 1].Value.Contains($manifest.assemblyName)) {
                throw 'The archive path was associated with the wrong rendered module.'
            }
        }
    }

    if ($matchedBlocks -ne 1) {
        throw "Expected one rendered failed-test attachment association, found $matchedBlocks."
    }
}

if ((@($observedModules | Sort-Object) -join ',') -cne 'AttachmentsA,AttachmentsA,AttachmentsB,AttachmentsB') {
    throw 'Module identities were not preserved.'
}

$files = @(Get-ChildItem -LiteralPath $staging -File -Recurse | ForEach-Object {
    [ordered]@{ path = [IO.Path]::GetRelativePath($staging, $_.FullName).Replace('\', '/'); sha256 = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash }
})
@{ files = $files } | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath $expectedPath -Encoding utf8
if ($previous['GITHUB_STEP_SUMMARY']) {
    foreach ($summary in $summaries) {
        [IO.File]::AppendAllText($previous['GITHUB_STEP_SUMMARY'], [IO.File]::ReadAllText($summary))
    }
}

Write-Output 'Verified packed-package composition: four direct host runs plus native multi-module dotnet test, twelve executed tests, five intentional failed results, and four correctly associated staged files.'
exit 0
