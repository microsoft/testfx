// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.IO.Compression;
using System.Text.Json;

namespace Microsoft.Testing.Platform.Acceptance.IntegrationTests;

/// <summary>
/// End-to-end coverage for the optional Microsoft.Testing.Platform.Browser package.
/// </summary>
[TestClass]
[DoNotParallelize]
public sealed class BrowserPackageExecutionTests : AcceptanceTestBase<NopAssetFixture>
{
    private static readonly string TargetFramework = TargetFrameworks.NetCurrent;

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
    <PackageReference Include="MSTest" Version="$MSTestVersion$" />
    <PackageReference Include="Microsoft.Testing.Platform.Browser" Version="$BrowserPackageVersion$" />
  </ItemGroup>

</Project>

#file BrowserPackageTests.cs
using Microsoft.VisualStudio.TestTools.UnitTesting;

[TestClass]
public sealed class BrowserPackageTests
{
    [TestMethod]
    public void RunsInsideBrowser()
    {
        Assert.IsTrue(OperatingSystem.IsBrowser());
    }

    [TestMethod]
    [Ignore]
    public void SkippedInsideBrowser()
    {
    }

    [TestMethod]
    public void FailsInsideBrowser()
        => Assert.Fail("Intentional browser experiment failure.");
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
    <PackageReference Include="MSTest" Version="$MSTestVersion$" />
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
    public async Task BrowserPackage_DotnetTestRunsAndListsTestsThroughSdkHttpGateway()
    {
        string? browser = LocateBrowser();
        if (browser is null)
        {
            Assert.Inconclusive("Skipping Microsoft.Testing.Platform.Browser execution: no Chromium-family browser was found.");
            return;
        }

        string browserPackageVersion = GetBrowserPackageVersion();
        using TestAsset generator = await TestAsset.GenerateAssetAsync(
            "BrowserPackageTestProject",
            SourceCode
                .PatchCodeWithReplace("$TargetFramework$", TargetFramework)
                .PatchCodeWithReplace("$MSTestVersion$", MSTestVersion)
                .PatchCodeWithReplace("$BrowserPackageVersion$", browserPackageVersion)
                .PatchCodeWithReplace("$Browser$", EscapeMsBuildValue(browser)));
        Assert.IsFalse(Directory.Exists(Path.Combine(generator.TargetAssetPath, "wwwroot")));
        Assert.IsEmpty(Directory.EnumerateFiles(generator.TargetAssetPath, "*.html", SearchOption.TopDirectoryOnly));
        Assert.IsEmpty(Directory.EnumerateFiles(generator.TargetAssetPath, "*.js", SearchOption.TopDirectoryOnly));

        DotnetMuxerResult run = await RunBrowserTestAsync(
            generator,
            "--filter FullyQualifiedName~RunsInsideBrowser",
            environmentVariables: new Dictionary<string, string?>
            {
                ["DEBUG"] = "*",
            });
        string runOutput = run.StandardOutput + run.StandardError;
        Assert.AreEqual(0, run.ExitCode, run.ToString());
        Assert.Contains($"({TargetFramework}|wasm) passed [+1/x0/?0]", runOutput);
        Assert.Contains("succeeded: 1", runOutput);
        Assert.DoesNotContain("--dotnet-test-http-token", runOutput);
        Assert.DoesNotContain("pw:channel", runOutput);
        await AssertIsTestingPlatformApplicationAsync(
            generator,
            $"-property:TargetFramework={TargetFramework} -property:RuntimeIdentifier={WasmRuntime.BrowserRid}");

        DotnetMuxerResult list = await RunBrowserTestAsync(generator, "--list-tests");
        string listOutput = list.StandardOutput + list.StandardError;
        Assert.AreEqual(0, list.ExitCode, list.ToString());
        Assert.Contains("RunsInsideBrowser", listOutput);
        Assert.Contains("SkippedInsideBrowser", listOutput);
        Assert.Contains("FailsInsideBrowser", listOutput);
        Assert.Contains("Discovered 3 tests", listOutput);

        DotnetMuxerResult skipped = await RunBrowserTestAsync(
            generator,
            "--filter FullyQualifiedName~SkippedInsideBrowser");
        string skippedOutput = skipped.StandardOutput + skipped.StandardError;
        Assert.AreEqual((int)ExitCode.ZeroTests, skipped.ExitCode, skipped.ToString());
        Assert.Contains("skipped: 1", skippedOutput);
        Assert.DoesNotContain("did not complete within", skippedOutput);

        DotnetMuxerResult failed = await RunBrowserTestAsync(
            generator,
            "--filter FullyQualifiedName~FailsInsideBrowser");
        string failedOutput = failed.StandardOutput + failed.StandardError;
        Assert.AreNotEqual(0, failed.ExitCode, failed.ToString());
        Assert.Contains("failed: 1", failedOutput);
        Assert.Contains("Intentional browser experiment failure.", failedOutput);
        Assert.DoesNotContain("did not complete within", failedOutput);
    }

    [TestMethod]
    public async Task BrowserPackage_WrapsOnlyMarkedBrowserComputeRunArguments()
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

        string commonArguments =
            $"msbuild {generator.TargetAssetPath} -target:ComputeRunArguments"
            + $" -property:TargetFramework={TargetFramework}"
            + $" -property:RuntimeIdentifier={WasmRuntime.BrowserRid}"
            + " -getProperty:RunCommand -getProperty:RunArguments";

        DotnetMuxerResult ordinary = await DotnetCli.RunAsync(
            commonArguments,
            warnAsError: false,
            failIfReturnValueIsNotZero: false,
            useMultithreadedMSBuild: false,
            cancellationToken: TestContext.CancellationToken);
        Assert.AreEqual(0, ordinary.ExitCode, ordinary.ToString());
        Assert.DoesNotContain("Microsoft.Testing.Platform.Browser.dll", ordinary.StandardOutput);

        DotnetMuxerResult marked = await DotnetCli.RunAsync(
            commonArguments + " -property:DotnetTestInvocation=true",
            warnAsError: false,
            failIfReturnValueIsNotZero: false,
            useMultithreadedMSBuild: false,
            cancellationToken: TestContext.CancellationToken);
        Assert.AreEqual(0, marked.ExitCode, marked.ToString());
        Assert.Contains("Microsoft.Testing.Platform.Browser.dll", marked.StandardOutput);
        Assert.Contains("--host-command-uri", marked.StandardOutput);
        Assert.Contains("--host-arguments-uri", marked.StandardOutput);
        Assert.DoesNotContain(".launch", marked.StandardOutput);
    }

    [TestMethod]
    public async Task BrowserPackage_HostAssetsAreBuildOnlyConsumerAssets()
    {
        string? browser = LocateBrowser();
        if (browser is null)
        {
            Assert.Inconclusive("Skipping Microsoft.Testing.Platform.Browser execution: no Chromium-family browser was found.");
            return;
        }

        string browserPackageVersion = GetBrowserPackageVersion();
        using TestAsset generator = await TestAsset.GenerateAssetAsync(
            "BrowserPackageStaticWebAssetsProject",
            (SourceCode + ConsumerPageSourceCode)
                .PatchCodeWithReplace("$TargetFramework$", TargetFramework)
                .PatchCodeWithReplace("$MSTestVersion$", MSTestVersion)
                .PatchCodeWithReplace("$BrowserPackageVersion$", browserPackageVersion)
                .PatchCodeWithReplace("$Browser$", EscapeMsBuildValue(browser)));

        DotnetMuxerResult build = await DotnetCli.RunAsync(
            $"build {generator.TargetAssetPath} --configuration Release --framework {TargetFramework} --runtime {WasmRuntime.BrowserRid}",
            warnAsError: false,
            failIfReturnValueIsNotZero: false,
            useMultithreadedMSBuild: false,
            cancellationToken: TestContext.CancellationToken);
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
        DotnetMuxerResult publish = await DotnetCli.RunAsync(
            $"publish {generator.TargetAssetPath} --configuration Release --framework {TargetFramework} --runtime {WasmRuntime.BrowserRid} --output {publishDirectory}",
            warnAsError: false,
            failIfReturnValueIsNotZero: false,
            useMultithreadedMSBuild: false,
            cancellationToken: TestContext.CancellationToken);
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
    public async Task BrowserPackage_DesktopTestApplicationIsUnaffected()
    {
        string browserPackageVersion = GetBrowserPackageVersion();
        using TestAsset generator = await TestAsset.GenerateAssetAsync(
            "BrowserPackageDesktopTestProject",
            DesktopSourceCode
                .PatchCodeWithReplace("$TargetFramework$", TargetFramework)
                .PatchCodeWithReplace("$MSTestVersion$", MSTestVersion)
                .PatchCodeWithReplace("$BrowserPackageVersion$", browserPackageVersion));

        DotnetMuxerResult run = await DotnetCli.RunAsync(
            $"test --project {generator.TargetAssetPath} --configuration Release --framework {TargetFramework}",
            warnAsError: false,
            failIfReturnValueIsNotZero: false,
            cancellationToken: TestContext.CancellationToken);

        string output = run.StandardOutput + run.StandardError;
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
        Assert.Contains("tools/net8.0/any/.playwright/package/cli.js", entries);
        Assert.DoesNotContain("buildMultiTargeting/Microsoft.Testing.Platform.Browser.After.targets", entries);

        ZipArchiveEntry browserTargetsEntry = archive.GetEntry(
            "buildMultiTargeting/Microsoft.Testing.Platform.Browser.targets")
            ?? throw new AssertFailedException("The browser package targets were not packaged.");
        using Stream browserTargetsStream = browserTargetsEntry.Open();
        using var browserTargetsReader = new StreamReader(browserTargetsStream);
        string browserTargets = browserTargetsReader.ReadToEnd();
        Assert.Contains("SourceId=\"$(PackageId)\"", browserTargets);
        Assert.Contains("AssetKind=\"Build\"", browserTargets);
        Assert.Contains("AfterTargets=\"ComputeRunArguments\"", browserTargets);
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

    private async Task<DotnetMuxerResult> RunBrowserTestAsync(
        TestAsset generator,
        string testArguments,
        Dictionary<string, string?>? environmentVariables = null)
        => await DotnetCli.RunAsync(
            $"test --project {generator.TargetAssetPath} --configuration Release --framework {TargetFramework}"
            + $" -property:DotnetTestInvocation=true {testArguments}",
            environmentVariables: environmentVariables,
            warnAsError: false,
            failIfReturnValueIsNotZero: false,
            useMultithreadedMSBuild: false,
            cancellationToken: TestContext.CancellationToken);

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
        Assert.AreEqual("true", evaluation.StandardOutput.Trim());
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
        return Directory
            .EnumerateFiles(Constants.ArtifactsPackagesShipping, $"{packagePrefix}*.nupkg")
            .OrderByDescending(File.GetLastWriteTimeUtc)
            .FirstOrDefault()
            ?? throw new AssertFailedException(
                $"Microsoft.Testing.Platform.Browser was not packed under '{Constants.ArtifactsPackagesShipping}'.");
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

        string path = evaluation.StandardOutput.Trim();
        return Path.IsPathRooted(path)
            ? path
            : Path.GetFullPath(path, generator.TargetAssetPath);
    }
}
