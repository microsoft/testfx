// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.Acceptance.IntegrationTests;

/// <summary>
/// End-to-end coverage for the optional Microsoft.Testing.Platform.Browser package.
/// </summary>
[TestClass]
public sealed class BrowserPackageExecutionTests : AcceptanceTestBase<NopAssetFixture>
{
    private static readonly string TargetFramework = TargetFrameworks.NetCurrent;

    private const string SourceCode = """
#file BrowserPackageTestProject.csproj
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>$TargetFramework$</TargetFramework>
    <RuntimeIdentifier>browser-wasm</RuntimeIdentifier>
    <OutputType>Exe</OutputType>
    <SelfContained>true</SelfContained>
    <EnableMSTestRunner>true</EnableMSTestRunner>
    <EnableMicrosoftTestingPlatform>true</EnableMicrosoftTestingPlatform>
    <ImplicitUsings>enable</ImplicitUsings>
    <WasmBuildNative>false</WasmBuildNative>
    <PublishTrimmed>false</PublishTrimmed>
    <NoWarn>$(NoWarn);NETSDK1201</NoWarn>

    <TestingPlatformBrowserHostCommand>$Node$</TestingPlatformBrowserHostCommand>
    <TestingPlatformBrowserHostArguments>&quot;$(MSBuildProjectDirectory)\server.mjs&quot; &quot;$(MSBuildProjectDirectory)\bin\$(Configuration)\$(TargetFramework)\$(RuntimeIdentifier)\AppBundle&quot;</TestingPlatformBrowserHostArguments>
    <TestingPlatformBrowserExecutable>$Browser$</TestingPlatformBrowserExecutable>
    <TestingPlatformBrowserAdditionalArguments>--mtp-test-value=a;b</TestingPlatformBrowserAdditionalArguments>
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
}

#file server.mjs
import { createReadStream, existsSync, renameSync, statSync, writeFileSync } from 'node:fs';
import { createServer } from 'node:http';
import { extname, resolve, sep } from 'node:path';

const root = resolve(process.argv[2]);
const launchInfoPath = process.env.TESTINGPLATFORM_BROWSER_LAUNCH_INFO_FILE;
if (!launchInfoPath) {
    throw new Error('TESTINGPLATFORM_BROWSER_LAUNCH_INFO_FILE is required.');
}

const contentTypes = new Map([
    ['.css', 'text/css'],
    ['.dat', 'application/octet-stream'],
    ['.dll', 'application/octet-stream'],
    ['.html', 'text/html; charset=utf-8'],
    ['.js', 'text/javascript; charset=utf-8'],
    ['.json', 'application/json; charset=utf-8'],
    ['.wasm', 'application/wasm'],
]);

const server = createServer((request, response) => {
    const pathname = decodeURIComponent(new URL(request.url, 'http://127.0.0.1').pathname);
    const relative = pathname === '/' ? 'index.html' : pathname.slice(1);
    const file = resolve(root, relative);
    if (file !== root && !file.startsWith(root + sep)) {
        response.writeHead(403).end();
        return;
    }

    if (!existsSync(file) || !statSync(file).isFile()) {
        response.writeHead(404).end();
        return;
    }

    response.setHeader('Content-Type', contentTypes.get(extname(file)) ?? 'application/octet-stream');
    createReadStream(file).pipe(response);
});

server.listen(0, '127.0.0.1', () => {
    const address = server.address();
    const temporaryPath = `${launchInfoPath}.${process.pid}.tmp`;
    writeFileSync(temporaryPath, JSON.stringify({ version: 1, url: `http://127.0.0.1:${address.port}/` }), { mode: 0o600 });
    renameSync(temporaryPath, launchInfoPath);
});
""";

    private const string DesktopSourceCode = """
#file BrowserPackageDesktopTestProject.csproj
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>$TargetFramework$</TargetFramework>
    <OutputType>Exe</OutputType>
    <EnableMSTestRunner>true</EnableMSTestRunner>
    <EnableMicrosoftTestingPlatform>true</EnableMicrosoftTestingPlatform>
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
        string? node = WasmRuntime.LocateNode();
        if (node is null)
        {
            Assert.Inconclusive(WasmRuntime.NodeUnavailableMessage);
            return;
        }

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
                .PatchCodeWithReplace("$Node$", EscapeMsBuildValue(node))
                .PatchCodeWithReplace("$Browser$", EscapeMsBuildValue(browser)));

        DotnetMuxerResult run = await DotnetCli.RunAsync(
            $"test --project {generator.TargetAssetPath} --configuration Release --framework {TargetFramework}",
            environmentVariables: new Dictionary<string, string?>
            {
                ["DEBUG"] = "*",
            },
            warnAsError: false,
            failIfReturnValueIsNotZero: false,
            useMultithreadedMSBuild: false,
            cancellationToken: TestContext.CancellationToken);

        string runOutput = run.StandardOutput + run.StandardError;
        Assert.AreEqual(0, run.ExitCode, run.ToString());
        Assert.Contains($"({TargetFramework}|wasm) passed [+1/x0/?0]", runOutput);
        Assert.Contains("succeeded: 1", runOutput);
        Assert.DoesNotContain("--dotnet-test-http-token", runOutput);
        Assert.DoesNotContain("pw:channel", runOutput);

        DotnetMuxerResult list = await DotnetCli.RunAsync(
            $"test --project {generator.TargetAssetPath} --configuration Release --framework {TargetFramework} --list-tests",
            warnAsError: false,
            failIfReturnValueIsNotZero: false,
            useMultithreadedMSBuild: false,
            cancellationToken: TestContext.CancellationToken);

        string listOutput = list.StandardOutput + list.StandardError;
        Assert.AreEqual(0, list.ExitCode, list.ToString());
        Assert.Contains("RunsInsideBrowser", listOutput);
        Assert.Contains("Discovered 1 tests", listOutput);
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
        Assert.Contains($"({TargetFramework}|x64) passed [+1/x0/?0]", output);
    }

    private static string GetBrowserPackageVersion()
    {
        const string packagePrefix = "Microsoft.Testing.Platform.Browser.";
        string package = Directory
            .EnumerateFiles(Constants.ArtifactsPackagesShipping, $"{packagePrefix}*.nupkg")
            .OrderByDescending(File.GetLastWriteTimeUtc)
            .FirstOrDefault()
            ?? throw new AssertFailedException(
                $"Microsoft.Testing.Platform.Browser was not packed under '{Constants.ArtifactsPackagesShipping}'.");

        string fileName = Path.GetFileName(package);
        return fileName[packagePrefix.Length..^".nupkg".Length];
    }

    private static string? LocateBrowser()
    {
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
}
