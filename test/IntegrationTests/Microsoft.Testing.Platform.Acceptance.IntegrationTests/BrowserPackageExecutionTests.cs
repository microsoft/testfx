// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.IO.Compression;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Microsoft.Testing.Platform.Acceptance.IntegrationTests;

/// <summary>
/// End-to-end coverage for the optional Microsoft.Testing.Platform.Browser package.
/// </summary>
[TestClass]
[DoNotParallelize]
public sealed class BrowserPackageExecutionTests : AcceptanceTestBase<NopAssetFixture>
{
    private const string StrictAcceptanceEnvironmentVariable = "TESTINGPLATFORM_BROWSER_REQUIRED";
    private static readonly string[] AlignedPackageIds =
    [
        "Microsoft.Testing.Platform.Browser",
        "Microsoft.Testing.Platform",
        "Microsoft.Testing.Platform.MSBuild",
        "Microsoft.Testing.Extensions.Telemetry",
        "Microsoft.Testing.Extensions.TrxReport.Abstractions",
        "MSTest.TestFramework",
        "MSTest.TestAdapter",
        "MSTest.Analyzers",
    ];

    private static readonly string TargetFramework = TargetFrameworks.NetCurrent;
    private static readonly Regex AnsiEscapeRegex = new(
        @"\x1B\[[0-?]*[ -/]*[@-~]",
        RegexOptions.CultureInvariant);

    private BrowserPackageCache? _browserPackageCache;

    private const string SourceCode = """
#file BrowserPackageTestProject.csproj
<Project Sdk="Microsoft.NET.Sdk.WebAssembly">

  <PropertyGroup>
    <TargetFramework>$TargetFramework$</TargetFramework>
    <OutputType>Exe</OutputType>
    <EnableMSTestRunner>true</EnableMSTestRunner>
    <ImplicitUsings>enable</ImplicitUsings>
    <WasmBuildNative>false</WasmBuildNative>
    <PublishTrimmed>false</PublishTrimmed>
    <NoWarn>$(NoWarn);NETSDK1201</NoWarn>

    <TestingPlatformBrowserExecutable>$Browser$</TestingPlatformBrowserExecutable>
    <TestingPlatformBrowserStartupTimeoutSeconds>60</TestingPlatformBrowserStartupTimeoutSeconds>
    <TestingPlatformBrowserCompletionTimeoutSeconds>120</TestingPlatformBrowserCompletionTimeoutSeconds>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="MSTest.TestFramework" Version="$MSTestVersion$" />
    <PackageReference Include="MSTest.TestAdapter" Version="$MSTestVersion$" />
    <PackageReference Include="Microsoft.Testing.Platform.Browser" Version="$BrowserPackageVersion$" />
  </ItemGroup>

</Project>

#file BrowserPackageTests.cs
using Microsoft.VisualStudio.TestTools.UnitTesting;

[TestClass]
public sealed class BrowserPackageTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public void RunsInsideBrowser()
    {
        Assert.IsTrue(OperatingSystem.IsBrowser());
        string artifactPath = Path.GetFullPath("browser-export.txt");
        File.WriteAllText(artifactPath, "browser-artifact-marker");
        TestContext.AddResultFile(artifactPath);
    }

    [TestMethod]
    [Ignore]
    public void SkippedInsideBrowser()
    {
    }

    [TestMethod]
    public void FailsInsideBrowser()
        => Assert.Fail("Intentional browser experiment failure.");

    [TestMethod]
    public async Task WaitsForCancellation()
        => await Task.Delay(Timeout.InfiniteTimeSpan, TestContext.CancellationToken);

    [TestMethod]
    public void ProducesLargeArtifact()
    {
        const int ArtifactLength = 1_048_713;
        byte[] content = new byte[ArtifactLength];
        for (int i = 0; i < content.Length; i++)
        {
            content[i] = (byte)(i % 251);
        }

        string artifactPath = Path.GetFullPath("browser-large-export.bin");
        File.WriteAllBytes(artifactPath, content);
        TestContext.AddResultFile(artifactPath);
    }
}
""";

    private const string ConsumerPageSourceCode = """

#file wwwroot/index.html
<!DOCTYPE html>
<html lang="en">
<head><meta charset="utf-8"><title>Consumer page</title></head>
<body>consumer-page-marker</body>
</html>
""";

    private const string DesktopSourceCode = """
#file BrowserPackageDesktopTestProject.csproj
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>$TargetFramework$</TargetFramework>
    <OutputType>Exe</OutputType>
    <EnableMSTestRunner>true</EnableMSTestRunner>
    <ImplicitUsings>enable</ImplicitUsings>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="MSTest.TestFramework" Version="$MSTestVersion$" />
    <PackageReference Include="MSTest.TestAdapter" Version="$MSTestVersion$" />
    <PackageReference Include="Microsoft.Testing.Platform.Browser" Version="$BrowserPackageVersion$" />
  </ItemGroup>

</Project>

#file BrowserPackageDesktopTests.cs
using Microsoft.VisualStudio.TestTools.UnitTesting;

[TestClass]
public sealed class BrowserPackageDesktopTests
{
    [TestMethod]
    public void RunsOutsideBrowser()
    {
        Assert.IsFalse(OperatingSystem.IsBrowser());
    }
}
""";

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    [TestCategory("BrowserComposedValidation")]
    public async Task BrowserPackage_DotnetTestRunsAndListsTestsThroughSdkHttpGateway()
    {
        string browser = RequireBrowser();

        string browserPackageVersion = GetBrowserPackageVersion();
        using TestAsset generator = await TestAsset.GenerateAssetAsync(
            "BrowserPackageTestProject",
            SourceCode
                .PatchCodeWithReplace("$TargetFramework$", TargetFramework)
                .PatchCodeWithReplace("$MSTestVersion$", MSTestVersion)
                .PatchCodeWithReplace("$BrowserPackageVersion$", browserPackageVersion)
                .PatchCodeWithReplace("$Browser$", EscapeMsBuildValue(browser)));
        using BrowserPackageCache packageCache = RequireAlignedBrowserInputs(generator);
        Assert.IsFalse(Directory.Exists(Path.Combine(generator.TargetAssetPath, "wwwroot")));
        Assert.IsEmpty(Directory.EnumerateFiles(generator.TargetAssetPath, "*.html", SearchOption.TopDirectoryOnly));
        Assert.IsEmpty(Directory.EnumerateFiles(generator.TargetAssetPath, "*.js", SearchOption.TopDirectoryOnly));

        string resultsDirectory = Path.Combine(
            TestContext.TestRunResultsDirectory ?? Path.GetTempPath(),
            $"browser-export-{Guid.NewGuid():N}");
        DotnetMuxerResult run = await RunBrowserTestAsync(
            generator,
            $"--results-directory \"{resultsDirectory}\" --filter FullyQualifiedName~RunsInsideBrowser",
            environmentVariables: new Dictionary<string, string?>
            {
                ["DEBUG"] = "*",
            });
        string runOutput = NormalizeTerminalOutput(run.StandardOutput + run.StandardError);
        Assert.AreEqual(0, run.ExitCode, run.ToString());
        Assert.Contains($"({TargetFramework}|wasm) passed [+1/x0/?0]", runOutput);
        Assert.Contains("succeeded: 1", runOutput);
        Assert.DoesNotContain("--dotnet-test-http-token", runOutput);
        Assert.DoesNotContain("pw:channel", runOutput);
        string exportedArtifact = Assert.ContainsSingle(
            Directory.EnumerateFiles(
                resultsDirectory,
                "browser-export.txt",
                SearchOption.AllDirectories));
        Assert.AreEqual("browser-artifact-marker", File.ReadAllText(exportedArtifact));
        AssertUsesMinimalMSTestDependencies(generator);
        BrowserPackageRestoreIdentity.AssertMatches(
            Path.Combine(generator.TargetAssetPath, "obj", "project.assets.json"),
            GetBrowserPackageFeed(requireExplicit: true),
            AlignedPackageIds);
        await AssertIsTestingPlatformApplicationAsync(
            generator,
            $"-property:TargetFramework={TargetFramework} -property:RuntimeIdentifier={WasmRuntime.BrowserRid}");

        string largeResultsDirectory = Path.Combine(
            TestContext.TestRunResultsDirectory ?? Path.GetTempPath(),
            $"browser-large-export-{Guid.NewGuid():N}");
        DotnetMuxerResult largeArtifact = await RunBrowserTestAsync(
            generator,
            $"--results-directory \"{largeResultsDirectory}\" --filter FullyQualifiedName~ProducesLargeArtifact");
        Assert.AreEqual(0, largeArtifact.ExitCode, largeArtifact.ToString());
        string exportedLargeArtifact = Assert.ContainsSingle(
            Directory.EnumerateFiles(
                largeResultsDirectory,
                "browser-large-export.bin",
                SearchOption.AllDirectories));
        byte[] exportedContent = File.ReadAllBytes(exportedLargeArtifact);
        Assert.HasCount(1_048_713, exportedContent);
        byte[] expectedContent = new byte[exportedContent.Length];
        for (int i = 0; i < expectedContent.Length; i++)
        {
            expectedContent[i] = (byte)(i % 251);
        }

        Assert.AreSequenceEqual(expectedContent, exportedContent);

        DotnetMuxerResult projectConfiguredFilter = await RunBrowserTestAsync(
            generator,
            testArguments: string.Empty,
            projectProperties:
                "-property:TestingPlatformCommandLineArguments=\"--filter FullyQualifiedName~NoSuchBrowserTest\"");
        string projectConfiguredFilterOutput =
            NormalizeTerminalOutput(
                projectConfiguredFilter.StandardOutput + projectConfiguredFilter.StandardError);
        Assert.AreEqual(
            (int)ExitCode.ZeroTests,
            projectConfiguredFilter.ExitCode,
            projectConfiguredFilter.ToString());
        Assert.Contains("Zero tests ran", projectConfiguredFilterOutput);
        Assert.DoesNotContain("Intentional browser experiment failure.", projectConfiguredFilterOutput);

        DotnetMuxerResult list = await RunBrowserTestAsync(generator, "--list-tests");
        string listOutput = NormalizeTerminalOutput(list.StandardOutput + list.StandardError);
        Assert.AreEqual(0, list.ExitCode, list.ToString());
        Assert.Contains("RunsInsideBrowser", listOutput);
        Assert.Contains("SkippedInsideBrowser", listOutput);
        Assert.Contains("FailsInsideBrowser", listOutput);
        Assert.Contains("WaitsForCancellation", listOutput);
        Assert.Contains("ProducesLargeArtifact", listOutput);
        Assert.Contains("Discovered 5 tests", listOutput);

        DotnetMuxerResult skipped = await RunBrowserTestAsync(
            generator,
            "--filter FullyQualifiedName~SkippedInsideBrowser");
        string skippedOutput = NormalizeTerminalOutput(skipped.StandardOutput + skipped.StandardError);
        Assert.AreEqual(0, skipped.ExitCode, skipped.ToString());
        Assert.Contains("skipped: 1", skippedOutput);
        Assert.DoesNotContain("did not complete within", skippedOutput);

        DotnetMuxerResult failed = await RunBrowserTestAsync(
            generator,
            "--filter FullyQualifiedName~FailsInsideBrowser");
        string failedOutput = NormalizeTerminalOutput(failed.StandardOutput + failed.StandardError);
        Assert.AreNotEqual(0, failed.ExitCode, failed.ToString());
        Assert.Contains("failed: 1", failedOutput);
        Assert.Contains("Intentional browser experiment failure.", failedOutput);
        Assert.DoesNotContain("did not complete within", failedOutput);
    }

    [TestMethod]
    public async Task BrowserPackage_DefinesDedicatedBrowserRunArguments()
    {
        string browserPackageVersion = GetBrowserPackageVersion();
        using TestAsset generator = await TestAsset.GenerateAssetAsync(
            "BrowserPackageComputeRunArgumentsProject",
            SourceCode
                .PatchCodeWithReplace("$TargetFramework$", TargetFramework)
                .PatchCodeWithReplace("$MSTestVersion$", MSTestVersion)
                .PatchCodeWithReplace("$BrowserPackageVersion$", browserPackageVersion)
                .PatchCodeWithReplace("$Browser$", "browser-placeholder"));

        DotnetMuxerResult restore = await DotnetCli.RunAsync(
            $"restore {generator.TargetAssetPath}",
            warnAsError: false,
            failIfReturnValueIsNotZero: false,
            cancellationToken: TestContext.CancellationToken);
        Assert.AreEqual(0, restore.ExitCode, restore.ToString());

        string commonProperties =
            $" -property:TargetFramework={TargetFramework}"
            + $" -property:RuntimeIdentifier={WasmRuntime.BrowserRid}"
            + " -getProperty:RunCommand -getProperty:RunArguments";

        DotnetMuxerResult ordinary = await DotnetCli.RunAsync(
            $"msbuild {generator.TargetAssetPath} -target:ComputeRunArguments"
            + commonProperties,
            warnAsError: false,
            failIfReturnValueIsNotZero: false,
            useMultithreadedMSBuild: false,
            cancellationToken: TestContext.CancellationToken);
        Assert.AreEqual(0, ordinary.ExitCode, ordinary.ToString());
        Assert.DoesNotContain("Microsoft.Testing.Platform.Browser.dll", ordinary.StandardOutput);

        DotnetMuxerResult unsupported = await DotnetCli.RunAsync(
            $"msbuild {generator.TargetAssetPath} -target:ComputeRunArguments;ComputeTestingPlatformRunArguments"
            + commonProperties
            + " -getProperty:TestingPlatformRunCommand"
            + " -getProperty:TestingPlatformRunArguments",
            warnAsError: false,
            failIfReturnValueIsNotZero: false,
            useMultithreadedMSBuild: false,
            cancellationToken: TestContext.CancellationToken);
        Assert.AreNotEqual(0, unsupported.ExitCode, unsupported.ToString());
        Assert.Contains("TestingPlatformRunArgumentsVersion=1", unsupported.StandardOutput + unsupported.StandardError);

        DotnetMuxerResult dedicated = await DotnetCli.RunAsync(
            $"msbuild {generator.TargetAssetPath} -target:ComputeRunArguments;ComputeTestingPlatformRunArguments"
            + commonProperties
            + " -property:TestingPlatformRunArgumentsVersion=1"
            + " -property:TestingPlatformRunCommand=host-command"
            + " -property:TestingPlatformRunArguments=host-arguments"
            + " -property:TestingPlatformRunWorkingDirectory=host-working-directory"
            + " -getProperty:TestingPlatformRunCommand"
            + " -getProperty:TestingPlatformRunArguments"
            + " -getProperty:TestingPlatformRunWorkingDirectory",
            warnAsError: false,
            failIfReturnValueIsNotZero: false,
            useMultithreadedMSBuild: false,
            cancellationToken: TestContext.CancellationToken);
        Assert.AreEqual(0, dedicated.ExitCode, dedicated.ToString());
        Assert.Contains("Microsoft.Testing.Platform.Browser.dll", dedicated.StandardOutput);
        Assert.Contains("--host-command-uri", dedicated.StandardOutput);
        Assert.Contains("--host-arguments-uri", dedicated.StandardOutput);
        Assert.Contains("host-command", dedicated.StandardOutput);
        Assert.Contains("host-arguments", dedicated.StandardOutput);
        Assert.DoesNotContain(".launch", dedicated.StandardOutput);
    }

    [TestMethod]
    [TestCategory("BrowserComposedValidation")]
    public async Task BrowserPackage_SdkCancellationStopsManagedBrowserRun()
    {
        string browser = RequireBrowser();

        string browserPackageVersion = GetBrowserPackageVersion();
        using TestAsset generator = await TestAsset.GenerateAssetAsync(
            "BrowserPackageCancellationProject",
            SourceCode
                .PatchCodeWithReplace("$TargetFramework$", TargetFramework)
                .PatchCodeWithReplace("$MSTestVersion$", MSTestVersion)
                .PatchCodeWithReplace("$BrowserPackageVersion$", browserPackageVersion)
                .PatchCodeWithReplace("$Browser$", EscapeMsBuildValue(browser)));
        using BrowserPackageCache packageCache = RequireAlignedBrowserInputs(generator);

        DotnetMuxerResult build = await RunBrowserSdkAsync(
            $"build {generator.TargetAssetPath} --configuration Release --framework {TargetFramework}",
            workingDirectory: generator.TargetAssetPath,
            failIfReturnValueIsNotZero: false);
        Assert.AreEqual(0, build.ExitCode, build.ToString());

        var stopwatch = Stopwatch.StartNew();
        DotnetMuxerResult result = await RunBrowserTestAsync(
            generator,
            "--no-build --timeout 5s --filter FullyQualifiedName~WaitsForCancellation");
        stopwatch.Stop();

        string output = NormalizeTerminalOutput(result.StandardOutput + result.StandardError);
        Assert.AreEqual((int)ExitCode.TestSessionAborted, result.ExitCode, result.ToString());
        // Allow the configured 60-second browser startup timeout, the 5-second SDK policy timeout,
        // and loaded-machine cleanup while still proving managed cancellation beats the 120-second
        // launcher completion timeout.
        var cancellationCompletionLimit = TimeSpan.FromSeconds(90);
        Assert.IsLessThan(
            cancellationCompletionLimit,
            stopwatch.Elapsed,
            "Managed HTTP cancellation should finish before the launcher's 120-second completion timeout.");
        Assert.DoesNotContain("did not complete within 120", output);
        Assert.DoesNotContain("Missing test session end", output);
    }

    [TestMethod]
    [TestCategory("BrowserComposedValidation")]
    public async Task BrowserPackage_ArtifactQuotaFailureFailsRun()
    {
        string browser = RequireBrowser();

        string browserPackageVersion = GetBrowserPackageVersion();
        using TestAsset generator = await TestAsset.GenerateAssetAsync(
            "BrowserPackageArtifactQuotaProject",
            SourceCode
                .PatchCodeWithReplace("$TargetFramework$", TargetFramework)
                .PatchCodeWithReplace("$MSTestVersion$", MSTestVersion)
                .PatchCodeWithReplace("$BrowserPackageVersion$", browserPackageVersion)
                .PatchCodeWithReplace("$Browser$", EscapeMsBuildValue(browser)));
        using BrowserPackageCache packageCache = RequireAlignedBrowserInputs(generator);

        DotnetMuxerResult build = await RunBrowserSdkAsync(
            $"build {generator.TargetAssetPath} --configuration Release --framework {TargetFramework}",
            workingDirectory: generator.TargetAssetPath,
            failIfReturnValueIsNotZero: false);
        Assert.AreEqual(0, build.ExitCode, build.ToString());

        string resultsDirectory = Path.Combine(
            TestContext.TestRunResultsDirectory ?? Path.GetTempPath(),
            $"browser-quota-{Guid.NewGuid():N}");
        DotnetMuxerResult result = await RunBrowserTestAsync(
            generator,
            $"--no-build --results-directory \"{resultsDirectory}\" --filter FullyQualifiedName~ProducesLargeArtifact",
            environmentVariables: new Dictionary<string, string?>
            {
                ["DOTNET_CLI_TEST_ARTIFACT_MAX_FILE_BYTES"] = "1048576",
            });

        string output = NormalizeTerminalOutput(result.StandardOutput + result.StandardError);
        Assert.AreNotEqual(0, result.ExitCode, result.ToString());
        Assert.Contains("exceeding the peer's 1048576-byte per-file export limit", output);
        Assert.IsEmpty(
            Directory.Exists(resultsDirectory)
                ? Directory.EnumerateFiles(
                    resultsDirectory,
                    "browser-large-export.bin",
                    SearchOption.AllDirectories)
                : []);
    }

    [TestMethod]
    [TestCategory("BrowserComposedValidation")]
    public async Task BrowserPackage_HostAssetsAreBuildOnlyConsumerAssets()
    {
        string browser = RequireBrowser();

        string browserPackageVersion = GetBrowserPackageVersion();
        using TestAsset generator = await TestAsset.GenerateAssetAsync(
            "BrowserPackageStaticWebAssetsProject",
            (SourceCode + ConsumerPageSourceCode)
                .PatchCodeWithReplace("$TargetFramework$", TargetFramework)
                .PatchCodeWithReplace("$MSTestVersion$", MSTestVersion)
                .PatchCodeWithReplace("$BrowserPackageVersion$", browserPackageVersion)
                .PatchCodeWithReplace("$Browser$", EscapeMsBuildValue(browser)));
        using BrowserPackageCache packageCache = RequireAlignedBrowserInputs(generator);

        DotnetMuxerResult build = await RunBrowserSdkAsync(
            $"build {generator.TargetAssetPath} --configuration Release --framework {TargetFramework} --runtime {WasmRuntime.BrowserRid}",
            workingDirectory: generator.TargetAssetPath,
            failIfReturnValueIsNotZero: false);
        Assert.AreEqual(0, build.ExitCode, build.ToString());

        using var buildManifest = JsonDocument.Parse(File.ReadAllText(
            await GetEvaluatedPathAsync(
                generator,
                "StaticWebAssetBuildManifestPath")));
        Assert.IsTrue(ContainsBrowserHostAsset(
            buildManifest,
            "_mtp/browser-host.html",
            "Build"));
        Assert.IsTrue(ContainsBrowserHostAsset(
            buildManifest,
            "_mtp/Microsoft.Testing.Platform.Browser.main.js",
            "Build"));

        DotnetMuxerResult run = await RunBrowserTestAsync(
            generator,
            "--filter FullyQualifiedName~RunsInsideBrowser");
        Assert.AreEqual(0, run.ExitCode, run.ToString());

        string publishDirectory = Path.Combine(generator.TargetAssetPath, "publish");
        DotnetMuxerResult publish = await RunBrowserSdkAsync(
            $"publish {generator.TargetAssetPath} --configuration Release --framework {TargetFramework} --runtime {WasmRuntime.BrowserRid} --output {publishDirectory}",
            workingDirectory: generator.TargetAssetPath,
            failIfReturnValueIsNotZero: false);
        Assert.AreEqual(0, publish.ExitCode, publish.ToString());

        using var publishManifest = JsonDocument.Parse(File.ReadAllText(
            await GetEvaluatedPathAsync(
                generator,
                "StaticWebAssetPublishManifestPath")));
        Assert.IsFalse(ContainsBrowserHostAsset(
            publishManifest,
            "_mtp/browser-host.html"));
        Assert.IsFalse(ContainsBrowserHostAsset(
            publishManifest,
            "_mtp/Microsoft.Testing.Platform.Browser.main.js"));
        Assert.IsEmpty(Directory.EnumerateFiles(
            publishDirectory,
            "Microsoft.Testing.Platform.Browser.main.js",
            SearchOption.AllDirectories));
        string consumerIndex = Directory.EnumerateFiles(
            publishDirectory,
            "index.html",
            SearchOption.AllDirectories).Single();
        Assert.Contains("consumer-page-marker", File.ReadAllText(consumerIndex));
    }

    [TestMethod]
    [TestCategory("BrowserComposedValidation")]
    public async Task BrowserPackage_DesktopTestApplicationIsUnaffected()
    {
        string browserPackageVersion = GetBrowserPackageVersion();
        using TestAsset generator = await TestAsset.GenerateAssetAsync(
            "BrowserPackageDesktopTestProject",
            DesktopSourceCode
                .PatchCodeWithReplace("$TargetFramework$", TargetFramework)
                .PatchCodeWithReplace("$MSTestVersion$", MSTestVersion)
                .PatchCodeWithReplace("$BrowserPackageVersion$", browserPackageVersion));

        bool useAlignedInputs = HasExplicitAlignedInputs() || IsStrictComposedValidation();
        using BrowserPackageCache? packageCache =
            useAlignedInputs
                ? RequireAlignedBrowserInputs(generator)
                : null;
        DotnetMuxerResult run = useAlignedInputs
            ? await RunBrowserSdkAsync(
                $"test --project {generator.TargetAssetPath} --configuration Release --framework {TargetFramework}",
                workingDirectory: generator.TargetAssetPath,
                failIfReturnValueIsNotZero: false)
            : await DotnetCli.RunAsync(
                $"test --project {generator.TargetAssetPath} --configuration Release --framework {TargetFramework}",
                warnAsError: false,
                failIfReturnValueIsNotZero: false,
                cancellationToken: TestContext.CancellationToken);

        string output = NormalizeTerminalOutput(run.StandardOutput + run.StandardError);
        Assert.AreEqual(0, run.ExitCode, run.ToString());
        string architecture = RuntimeInformation.ProcessArchitecture.ToString().ToLowerInvariant();
        Assert.Contains($"({TargetFramework}|{architecture}) passed [+1/x0/?0]", output);
        await AssertIsTestingPlatformApplicationAsync(
            generator,
            $"-property:TargetFramework={TargetFramework}");
    }

    [TestMethod]
    public void BrowserPackage_ContainsPrivateCrossPlatformPlaywrightRuntime()
    {
        string package = GetBrowserPackagePath();
        using ZipArchive archive = ZipFile.OpenRead(package);
        string[] entries = [.. archive.Entries.Select(static entry => entry.FullName)];

        Assert.Contains("tools/net8.0/any/Microsoft.Playwright.dll", entries);
        Assert.Contains("tools/net8.0/any/Microsoft.Bcl.AsyncInterfaces.dll", entries);
        Assert.Contains("tools/net8.0/any/.playwright/node/win32_x64/node.exe", entries);
        Assert.Contains("tools/net8.0/any/.playwright/node/linux-x64/node", entries);
        Assert.Contains("tools/net8.0/any/.playwright/node/linux-arm64/node", entries);
        Assert.Contains("tools/net8.0/any/.playwright/node/darwin-x64/node", entries);
        Assert.Contains("tools/net8.0/any/.playwright/node/darwin-arm64/node", entries);
        Assert.Contains("tools/net8.0/any/.playwright/node/LICENSE", entries);
        Assert.Contains("tools/net8.0/any/.playwright/package/cli.js", entries);
        Assert.Contains("tools/net8.0/any/.playwright/package/LICENSE", entries);
        Assert.Contains("tools/net8.0/any/.playwright/package/NOTICE", entries);
        Assert.Contains("tools/net8.0/any/.playwright/package/ThirdPartyNotices.txt", entries);
        Assert.DoesNotContain(
            static entry => entry.Contains("//", StringComparison.Ordinal),
            entries);
        Assert.DoesNotContain(
            static entry => entry.EndsWith(".ttf", StringComparison.OrdinalIgnoreCase),
            entries);
        Assert.DoesNotContain(
            static entry => entry.Contains(
                "/.playwright/package/lib/vite/",
                StringComparison.Ordinal),
            entries);
        Assert.DoesNotContain(
            static entry => entry.Contains(
                "/.playwright/package/types/",
                StringComparison.Ordinal),
            entries);
        Assert.DoesNotContain("buildMultiTargeting/Microsoft.Testing.Platform.Browser.After.targets", entries);

        ZipArchiveEntry browserTargetsEntry = archive.GetEntry(
            "buildMultiTargeting/Microsoft.Testing.Platform.Browser.targets")
            ?? throw new AssertFailedException("The browser package targets were not packaged.");
        using Stream browserTargetsStream = browserTargetsEntry.Open();
        using var browserTargetsReader = new StreamReader(browserTargetsStream);
        string browserTargets = browserTargetsReader.ReadToEnd();
        Assert.Contains("SourceId=\"$(PackageId)\"", browserTargets);
        Assert.Contains("AssetKind=\"Build\"", browserTargets);
        Assert.Contains("Name=\"ComputeTestingPlatformRunArguments\"", browserTargets);
        Assert.Contains("ComputeTestingPlatformRunArgumentsDependsOn", browserTargets);
        Assert.Contains("TestingPlatformRunArgumentsVersion", browserTargets);
        Assert.DoesNotContain("AfterTargets=\"ComputeRunArguments\"", browserTargets);
        Assert.DoesNotContain("DotnetTestInvocation", browserTargets);
        Assert.DoesNotContain("CustomAfterDirectoryBuildTargets", browserTargets);

        ZipArchiveEntry browserMainEntry = archive.GetEntry(
            "buildMultiTargeting/assets/Microsoft.Testing.Platform.Browser.main.js")
            ?? throw new AssertFailedException("The package-owned browser supervisor was not packaged.");
        using Stream browserMainStream = browserMainEntry.Open();
        using var browserMainReader = new StreamReader(browserMainStream);
        string browserMain = browserMainReader.ReadToEnd();
        Assert.Contains("await import('../_framework/dotnet.js')", browserMain);
        Assert.Contains("__mtpBrowserGetArguments", browserMain);
        Assert.Contains("__mtpBrowserComplete", browserMain);
        Assert.Contains("launcherAvailable", browserMain);
        Assert.DoesNotContain("testingPlatformBrowser", browserMain);

        ZipArchiveEntry runtimeConfigEntry = archive.GetEntry(
            "tools/net8.0/any/Microsoft.Testing.Platform.Browser.runtimeconfig.json")
            ?? throw new AssertFailedException("The browser launcher runtimeconfig was not packaged.");
        using Stream runtimeConfigStream = runtimeConfigEntry.Open();
        using var runtimeConfig = System.Text.Json.JsonDocument.Parse(runtimeConfigStream);
        Assert.AreEqual(
            "Major",
            runtimeConfig.RootElement.GetProperty("runtimeOptions").GetProperty("rollForward").GetString());

        ZipArchiveEntry nuspecEntry = archive.Entries.Single(
            static entry => entry.FullName.EndsWith(".nuspec", StringComparison.Ordinal));
        using Stream nuspecStream = nuspecEntry.Open();
        var nuspec = XDocument.Load(nuspecStream);
        string[] packageDependencies =
        [
            .. nuspec.Descendants()
                .Where(static element => element.Name.LocalName == "dependency")
                .Select(static element => element.Attribute("id")?.Value)
                .OfType<string>(),
        ];
        Assert.DoesNotContain(
            "Microsoft.Playwright",
            packageDependencies,
            "Microsoft.Playwright must remain a private build-time dependency; its runtime is bundled under tools.");
    }

    [TestMethod]
    public async Task BrowserPackage_PackNoBuildPreservesRuntimePayload()
    {
        string packageOutput = Path.Combine(
            TestContext.TestRunResultsDirectory ?? Path.GetTempPath(),
            $"browser-pack-no-build-{Guid.NewGuid():N}");
        Directory.CreateDirectory(packageOutput);

        DotnetMuxerResult pack = await DotnetCli.RunAsync(
            $"pack {Path.Combine(RootFinder.Find(), "src", "Platform", "Microsoft.Testing.Platform.Browser", "Microsoft.Testing.Platform.Browser.csproj")} --configuration {Constants.BuildConfiguration} --no-build --no-restore -property:PackageOutputPath={packageOutput}",
            warnAsError: false,
            failIfReturnValueIsNotZero: false,
            useMultithreadedMSBuild: false,
            cancellationToken: TestContext.CancellationToken);

        Assert.AreEqual(0, pack.ExitCode, pack.ToString());
        string package = Directory.EnumerateFiles(
            packageOutput,
            "Microsoft.Testing.Platform.Browser.*.nupkg").Single();
        using ZipArchive archive = ZipFile.OpenRead(package);
        Assert.IsNotNull(archive.GetEntry("tools/net8.0/any/Microsoft.Playwright.dll"));
        Assert.IsNotNull(archive.GetEntry("tools/net8.0/any/.playwright/package/cli.js"));
    }

    [TestMethod]
    public async Task BrowserPackage_PackWithoutBundledNodePreservesPrivateDriverAndLicenses()
    {
        string packageOutput = Path.Combine(
            TestContext.TestRunResultsDirectory ?? Path.GetTempPath(),
            $"browser-pack-system-node-{Guid.NewGuid():N}");
        Directory.CreateDirectory(packageOutput);

        DotnetMuxerResult pack = await DotnetCli.RunAsync(
            $"pack {Path.Combine(RootFinder.Find(), "src", "Platform", "Microsoft.Testing.Platform.Browser", "Microsoft.Testing.Platform.Browser.csproj")} --configuration {Constants.BuildConfiguration} --no-build --no-restore -property:TestingPlatformBrowserBundlePlaywrightNode=false -property:PackageOutputPath={packageOutput}",
            warnAsError: false,
            failIfReturnValueIsNotZero: false,
            useMultithreadedMSBuild: false,
            cancellationToken: TestContext.CancellationToken);

        Assert.AreEqual(0, pack.ExitCode, pack.ToString());
        string package = Directory.EnumerateFiles(
            packageOutput,
            "Microsoft.Testing.Platform.Browser.*.nupkg").Single();
        using ZipArchive archive = ZipFile.OpenRead(package);
        Assert.IsNotNull(archive.GetEntry("tools/net8.0/any/Microsoft.Playwright.dll"));
        Assert.IsNotNull(archive.GetEntry("tools/net8.0/any/.playwright/package/cli.js"));
        Assert.IsNotNull(archive.GetEntry("tools/net8.0/any/.playwright/package/LICENSE"));
        Assert.IsNotNull(archive.GetEntry("tools/net8.0/any/.playwright/package/NOTICE"));
        Assert.IsNotNull(archive.GetEntry("tools/net8.0/any/.playwright/package/ThirdPartyNotices.txt"));
        Assert.IsNotNull(archive.GetEntry("tools/net8.0/any/.playwright/node/LICENSE"));
        Assert.DoesNotContain(
            static entry =>
                entry.FullName.StartsWith("tools/net8.0/any/.playwright/node/", StringComparison.Ordinal)
                && !entry.FullName.EndsWith("/LICENSE", StringComparison.Ordinal),
            archive.Entries);
    }

    [TestMethod]
    public void BrowserPackage_InvalidExplicitBrowserPathFails()
    {
        string? previousValue = Environment.GetEnvironmentVariable(
            "TESTINGPLATFORM_BROWSER_EXECUTABLE");
        string invalidPath = Path.Combine(
            Path.GetTempPath(),
            $"missing-browser-{Guid.NewGuid():N}");

        try
        {
            Environment.SetEnvironmentVariable(
                "TESTINGPLATFORM_BROWSER_EXECUTABLE",
                invalidPath);

            Assert.ThrowsExactly<AssertFailedException>(() => LocateBrowser());
        }
        finally
        {
            Environment.SetEnvironmentVariable(
                "TESTINGPLATFORM_BROWSER_EXECUTABLE",
                previousValue);
        }
    }

    [TestMethod]
    public void BrowserPackage_EvaluatedPropertyIgnoresTelemetryBanner()
        => Assert.AreEqual(
            "true",
            GetLastNonEmptyOutputLine($"Telemetry is: Disabled{Environment.NewLine}true"));

    private async Task<DotnetMuxerResult> RunBrowserTestAsync(
        TestAsset generator,
        string testArguments,
        Dictionary<string, string?>? environmentVariables = null,
        string? projectProperties = null)
        => await RunBrowserSdkAsync(
            $"test --project {generator.TargetAssetPath} --configuration Release --framework {TargetFramework}"
            + $" {projectProperties} {testArguments}",
            environmentVariables: environmentVariables,
            workingDirectory: generator.TargetAssetPath,
            failIfReturnValueIsNotZero: false);

    private BrowserPackageCache RequireAlignedBrowserInputs(TestAsset generator)
    {
        if (_browserPackageCache is not null)
        {
            throw new InvalidOperationException(
                "The composed browser package cache was already initialized for this test.");
        }

        _browserPackageCache = BrowserPackageCache.Create(generator.TargetAssetPath);
        string packageFeed = GetBrowserPackageFeed(requireExplicit: true);
        string dotnetPath = GetBrowserSdkDotnetPath();
        WriteSdkSelectionGlobalJson(generator.TargetAssetPath, dotnetPath);
        string nugetConfigPath = Path.Combine(generator.TargetAssetPath, "NuGet.config");
        var nugetConfig = XDocument.Load(nugetConfigPath);
        XElement localShipping = nugetConfig.Descendants()
            .Single(element =>
                element.Name.LocalName == "add"
                && element.Attribute("key")?.Value == "local-shipping");
        localShipping.SetAttributeValue("value", packageFeed);
        nugetConfig.Save(nugetConfigPath);

        string dotnetRoot = Path.GetDirectoryName(dotnetPath)
            ?? throw new AssertFailedException($"Unable to determine the SDK root for '{dotnetPath}'.");
        string sdkVersion = GetBrowserSdkVersion(dotnetRoot);
        string sdkTargetsPath = Path.Combine(
            dotnetRoot,
            "sdk",
            sdkVersion,
            "Sdks",
            "Microsoft.NET.Sdk",
            "Sdk",
            "Sdk.targets");
        var sdkTargets = XDocument.Load(sdkTargetsPath);
        string? runArgumentsVersion = sdkTargets.Descendants()
            .SingleOrDefault(element =>
                element.Name.LocalName == "TestingPlatformRunArgumentsVersion")
            ?.Value;
        if (runArgumentsVersion != "1")
        {
            ReportUnavailableComposedInput(
                $"The SDK-under-test at '{dotnetPath}' does not expose TestingPlatformRunArgumentsVersion=1.");
        }

        return _browserPackageCache;
    }

    private async Task<DotnetMuxerResult> RunBrowserSdkAsync(
        string arguments,
        Dictionary<string, string?>? environmentVariables = null,
        string? workingDirectory = null,
        bool failIfReturnValueIsNotZero = true)
    {
        string dotnetPath = GetBrowserSdkDotnetPath();
        environmentVariables ??= [];
        foreach ((string key, string? value) in GetBrowserSdkEnvironment(dotnetPath))
        {
            environmentVariables[key] = value;
        }

        if (_browserPackageCache is not null)
        {
            environmentVariables["NUGET_PACKAGES"] = _browserPackageCache.Path;
        }

        return await DotnetCli.RunAsync(
            arguments,
            workingDirectory: workingDirectory,
            environmentVariables: environmentVariables,
            warnAsError: false,
            failIfReturnValueIsNotZero: failIfReturnValueIsNotZero,
            useMultithreadedMSBuild: false,
            dotnetPath: dotnetPath,
            cancellationToken: TestContext.CancellationToken);
    }

    private static void WriteSdkSelectionGlobalJson(string assetPath, string dotnetPath)
    {
        string dotnetRoot = Path.GetDirectoryName(dotnetPath)
            ?? throw new AssertFailedException($"Unable to determine the SDK root for '{dotnetPath}'.");
        string sdkVersion = GetBrowserSdkVersion(dotnetRoot);
        File.WriteAllText(
            Path.Combine(assetPath, "global.json"),
            $$"""
            {
              "sdk": {
                "version": "{{sdkVersion}}",
                "rollForward": "disable",
                "allowPrerelease": true,
                "paths": [ "$host$" ]
              },
              "test": {
                "runner": "Microsoft.Testing.Platform"
              }
            }
            """);
    }

    private static string GetBrowserSdkVersion(string dotnetRoot)
        => BrowserSdkSelection.ResolveSdkVersion(
            dotnetRoot,
            Environment.GetEnvironmentVariable("TESTINGPLATFORM_BROWSER_SDK_VERSION"),
            Environment.GetEnvironmentVariable("TESTINGPLATFORM_BROWSER_SDK_COMMIT"));

    private static string GetBrowserSdkDotnetPath()
    {
        string? dotnetPath = Environment.GetEnvironmentVariable("TESTINGPLATFORM_BROWSER_SDK_DOTNET");
        if (dotnetPath is null)
        {
            ReportUnavailableComposedInput(
                "Set TESTINGPLATFORM_BROWSER_SDK_DOTNET to the dotnet executable from the aligned source-built SDK.");
        }

        return File.Exists(dotnetPath)
            ? Path.GetFullPath(dotnetPath)
            : throw new AssertFailedException(
                $"TESTINGPLATFORM_BROWSER_SDK_DOTNET does not exist: '{dotnetPath}'.");
    }

    private static Dictionary<string, string?> GetBrowserSdkEnvironment(string dotnetPath)
    {
        string dotnetRoot = Path.GetDirectoryName(dotnetPath)
            ?? throw new AssertFailedException($"Unable to determine the SDK root for '{dotnetPath}'.");
        string sdkPath = Path.Combine(dotnetRoot, "sdk", GetBrowserSdkVersion(dotnetRoot));
        return new()
        {
            ["DOTNET_ROOT"] = dotnetRoot,
            ["DOTNET_ROOT_X64"] = dotnetRoot,
            ["DOTNET_INSTALL_DIR"] = dotnetRoot,
            ["DOTNET_MULTILEVEL_LOOKUP"] = "0",
            ["DOTNET_MSBUILD_SDK_RESOLVER_CLI_DIR"] = dotnetRoot,
            ["DOTNET_HOST_PATH"] = dotnetPath,
            ["MSBuildSDKsPath"] = Path.Combine(sdkPath, "Sdks"),
            ["MSBUILD_EXE_PATH"] = Path.Combine(sdkPath, "MSBuild.dll"),
            ["MSBuildExtensionsPath"] = sdkPath,
            ["MSBuildToolsPath"] = sdkPath,
            ["RoslynTargetsPath"] = Path.Combine(sdkPath, "Roslyn"),
            ["PATH"] = dotnetRoot + Path.PathSeparator + Environment.GetEnvironmentVariable("PATH"),
        };
    }

    private static string NormalizeTerminalOutput(string output)
        => AnsiEscapeRegex.Replace(output, string.Empty);

    private static void AssertUsesMinimalMSTestDependencies(TestAsset generator)
    {
        using var assets = JsonDocument.Parse(File.ReadAllText(Path.Combine(
            generator.TargetAssetPath,
            "obj",
            "project.assets.json")));
        string[] libraries =
        [
            .. assets.RootElement.GetProperty("libraries").EnumerateObject()
                .Select(static library => library.Name.ToLowerInvariant()),
        ];

        Assert.DoesNotContain(
            static library => library.StartsWith("mstest/", StringComparison.Ordinal),
            libraries);
        Assert.DoesNotContain(
            static library => library.StartsWith("microsoft.net.test.sdk/", StringComparison.Ordinal),
            libraries);
        Assert.DoesNotContain(
            static library => library.StartsWith(
                "microsoft.testing.extensions.codecoverage/",
                StringComparison.Ordinal),
            libraries);
        Assert.DoesNotContain(
            static library => library.StartsWith(
                "microsoft.testing.extensions.trxreport/",
                StringComparison.Ordinal),
            libraries);
        Assert.Contains(
            static library => library.StartsWith("mstest.testadapter/", StringComparison.Ordinal),
            libraries);
        Assert.Contains(
            static library => library.StartsWith("mstest.testframework/", StringComparison.Ordinal),
            libraries);
        Assert.Contains(
            static library => library.StartsWith(
                "microsoft.testing.extensions.trxreport.abstractions/",
                StringComparison.Ordinal),
            libraries);
    }

    private async Task AssertIsTestingPlatformApplicationAsync(
        TestAsset generator,
        string properties)
    {
        DotnetMuxerResult evaluation = await DotnetCli.RunAsync(
            $"msbuild {generator.TargetAssetPath} -getProperty:IsTestingPlatformApplication {properties}",
            warnAsError: false,
            failIfReturnValueIsNotZero: false,
            useMultithreadedMSBuild: false,
            cancellationToken: TestContext.CancellationToken);

        Assert.AreEqual(0, evaluation.ExitCode, evaluation.ToString());
        Assert.AreEqual("true", GetLastNonEmptyOutputLine(evaluation.StandardOutput));
    }

    private static string GetBrowserPackageVersion()
    {
        string fileName = Path.GetFileName(GetBrowserPackagePath());
        const string packagePrefix = "Microsoft.Testing.Platform.Browser.";
        return fileName[packagePrefix.Length..^".nupkg".Length];
    }

    private static string GetBrowserPackagePath()
    {
        const string packagePrefix = "Microsoft.Testing.Platform.Browser.";
        string packageFeed = GetBrowserPackageFeed(requireExplicit: false);
        return Directory
            .EnumerateFiles(packageFeed, $"{packagePrefix}*.nupkg")
            .OrderByDescending(File.GetLastWriteTimeUtc)
            .FirstOrDefault()
            ?? throw new AssertFailedException(
                $"Microsoft.Testing.Platform.Browser was not packed under '{packageFeed}'.");
    }

    private static string GetBrowserPackageFeed(bool requireExplicit)
    {
        string? packageFeed = Environment.GetEnvironmentVariable(
            "TESTINGPLATFORM_BROWSER_PACKAGE_FEED");
        if (packageFeed is null)
        {
            if (requireExplicit)
            {
                ReportUnavailableComposedInput(
                    "Set TESTINGPLATFORM_BROWSER_PACKAGE_FEED to the immutable aligned TestFX package directory.");
            }

            return Constants.ArtifactsPackagesShipping;
        }

        return Directory.Exists(packageFeed)
            ? Path.GetFullPath(packageFeed)
            : throw new AssertFailedException(
                $"TESTINGPLATFORM_BROWSER_PACKAGE_FEED does not exist: '{packageFeed}'.");
    }

    private static bool HasExplicitAlignedInputs()
        => Environment.GetEnvironmentVariable("TESTINGPLATFORM_BROWSER_SDK_DOTNET") is not null
            && Environment.GetEnvironmentVariable("TESTINGPLATFORM_BROWSER_PACKAGE_FEED") is not null;

    private static bool IsStrictComposedValidation()
        => string.Equals(
            Environment.GetEnvironmentVariable(StrictAcceptanceEnvironmentVariable),
            "1",
            StringComparison.Ordinal);

    private static string RequireBrowser()
    {
        string? browser = LocateBrowser();
        if (browser is not null)
        {
            return browser;
        }

        ReportUnavailableComposedInput(
            "No Chromium-family browser was found. Set TESTINGPLATFORM_BROWSER_EXECUTABLE to an existing executable.");
        return null!;
    }

    private static void ReportUnavailableComposedInput(string message)
    {
        if (IsStrictComposedValidation())
        {
            throw new AssertFailedException(
                $"{StrictAcceptanceEnvironmentVariable}=1 requires composed browser validation to run: {message}");
        }

        Assert.Inconclusive($"Skipping Microsoft.Testing.Platform.Browser composed validation: {message}");
    }

    private static string? LocateBrowser()
    {
        string? requestedBrowser = Environment.GetEnvironmentVariable(
            "TESTINGPLATFORM_BROWSER_EXECUTABLE");
        if (requestedBrowser is not null)
        {
            return File.Exists(requestedBrowser)
                ? Path.GetFullPath(requestedBrowser)
                : throw new AssertFailedException(
                    $"TESTINGPLATFORM_BROWSER_EXECUTABLE does not exist: '{requestedBrowser}'.");
        }

        IEnumerable<string> candidates = OperatingSystem.IsWindows()
            ? new[]
            {
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Microsoft", "Edge", "Application", "msedge.exe"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Microsoft", "Edge", "Application", "msedge.exe"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Google", "Chrome", "Application", "chrome.exe"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Google", "Chrome", "Application", "chrome.exe"),
            }
            : OperatingSystem.IsMacOS()
                ? new[]
                {
                    "/Applications/Microsoft Edge.app/Contents/MacOS/Microsoft Edge",
                    "/Applications/Google Chrome.app/Contents/MacOS/Google Chrome",
                    "/Applications/Chromium.app/Contents/MacOS/Chromium",
                }
                : FindBrowsersOnPath();

        return candidates.FirstOrDefault(File.Exists);
    }

    private static IEnumerable<string> FindBrowsersOnPath()
    {
        string? path = Environment.GetEnvironmentVariable("PATH");
        if (path is null)
        {
            yield break;
        }

        foreach (string directory in path.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            foreach (string executable in new[] { "microsoft-edge", "google-chrome", "chromium", "chromium-browser" })
            {
                yield return Path.Combine(directory, executable);
            }
        }
    }

    private static string EscapeMsBuildValue(string value)
        => value.Replace("&", "&amp;", StringComparison.Ordinal)
            .Replace("<", "&lt;", StringComparison.Ordinal)
            .Replace(">", "&gt;", StringComparison.Ordinal);

    private static bool ContainsBrowserHostAsset(
        JsonDocument manifest,
        string relativePath,
        string? assetKind = null)
        => manifest.RootElement.GetProperty("Assets").EnumerateArray().Any(
            asset => asset.GetProperty("SourceId").GetString() == "BrowserPackageTestProject"
                && asset.GetProperty("RelativePath").GetString() == relativePath
                && (assetKind is null
                    || asset.GetProperty("AssetKind").GetString() == assetKind));

    private async Task<string> GetEvaluatedPathAsync(
        TestAsset generator,
        string propertyName)
    {
        DotnetMuxerResult evaluation = await DotnetCli.RunAsync(
            $"msbuild {generator.TargetAssetPath} -target:ResolveStaticWebAssetsConfiguration"
            + $" -getProperty:{propertyName}"
            + $" -property:Configuration=Release"
            + $" -property:TargetFramework={TargetFramework}"
            + $" -property:RuntimeIdentifier={WasmRuntime.BrowserRid}",
            warnAsError: false,
            failIfReturnValueIsNotZero: false,
            useMultithreadedMSBuild: false,
            cancellationToken: TestContext.CancellationToken);
        Assert.AreEqual(0, evaluation.ExitCode, evaluation.ToString());

        string path = GetLastNonEmptyOutputLine(evaluation.StandardOutput);
        return Path.IsPathRooted(path)
            ? path
            : Path.GetFullPath(path, generator.TargetAssetPath);
    }

    private static string GetLastNonEmptyOutputLine(string output)
        => output
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
            .Last();
}
