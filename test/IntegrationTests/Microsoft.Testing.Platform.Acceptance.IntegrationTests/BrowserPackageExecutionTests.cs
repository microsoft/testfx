// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.IO.Compression;

namespace Microsoft.Testing.Platform.Acceptance.IntegrationTests;

/// <summary>
/// End-to-end coverage for the optional Microsoft.Testing.Platform.Browser package.
/// </summary>
[TestClass]
[DoNotParallelize]
public sealed class BrowserPackageExecutionTests : AcceptanceTestBase<NopAssetFixture>
{
    private static readonly string TargetFramework = TargetFrameworks.NetCurrent;
    private const string InvocationId1 = "11111111111111111111111111111111";
    private const string InvocationId2 = "22222222222222222222222222222222";
    private const string InvocationId3 = "33333333333333333333333333333333";
    private const string InvocationId4 = "44444444444444444444444444444444";
    private const string InvocationId5 = "55555555555555555555555555555555";
    private const string InvocationId6 = "66666666666666666666666666666666";
    private const string InvocationId7 = "77777777777777777777777777777777";
    private const string InvocationId8 = "88888888888888888888888888888888";
    private const string InvocationId9 = "99999999999999999999999999999999";
    private const string InvocationIdA = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";

    private const string SourceCode = """
#file BrowserPackageTestProject.csproj
<Project Sdk="Microsoft.NET.Sdk.WebAssembly">

  <PropertyGroup>
    <TargetFramework>$TargetFramework$</TargetFramework>
    <OutputType>Exe</OutputType>
    <EnableMSTestRunner>true</EnableMSTestRunner>
    <EnableMicrosoftTestingPlatform>true</EnableMicrosoftTestingPlatform>
    <ImplicitUsings>enable</ImplicitUsings>
    <WasmBuildNative>false</WasmBuildNative>
    <PublishTrimmed>false</PublishTrimmed>
    <NoWarn>$(NoWarn);NETSDK1201</NoWarn>

    <TestingPlatformBrowserExecutable>$Browser$</TestingPlatformBrowserExecutable>
    <TestingPlatformBrowserAdditionalArguments>--mtp-test-value=&quot;line1&#xD;&#xA;line2;%#'&quot;</TestingPlatformBrowserAdditionalArguments>
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
        => Assert.Fail("Intentional browser preview failure.");
}
""";

    private const string MultiTargetingSourceCode = """
#file BrowserMultiTargetingProject.csproj
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFrameworks>net9.0;$TargetFramework$</TargetFrameworks>
    <OutputType>Exe</OutputType>
    <RuntimeIdentifier Condition=" '$(TargetFramework)' == '$TargetFramework$' ">browser-wasm</RuntimeIdentifier>
    <TestingPlatformBrowserGenerateHostAssets>false</TestingPlatformBrowserGenerateHostAssets>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.Testing.Platform.Browser" Version="$BrowserPackageVersion$" />
  </ItemGroup>

</Project>

#file Program.cs
return 0;

#file Directory.Build.targets
<Project>
  <PropertyGroup>
    <_ParentDirectoryBuildTargets>$([MSBuild]::GetPathOfFileAbove('Directory.Build.targets', '$(MSBuildThisFileDirectory)..'))</_ParentDirectoryBuildTargets>
  </PropertyGroup>
  <Import Project="$(_ParentDirectoryBuildTargets)" Condition=" '$(_ParentDirectoryBuildTargets)' != '' " />

  <Target Name="_ProvideMultiTargetingFrameworkHost" AfterTargets="ComputeRunArguments">
    <PropertyGroup>
      <RunCommand>framework-host-$(TargetFramework)</RunCommand>
      <RunArguments>--framework $(TargetFramework) --invocation $(DotnetTestInvocationId)</RunArguments>
      <RunWorkingDirectory>$(MSBuildProjectDirectory)</RunWorkingDirectory>
    </PropertyGroup>
    <WriteLinesToFile File="$(MSBuildProjectDirectory)\framework-host-$(TargetFramework).txt"
                      Lines="$(RunCommand)"
                      Overwrite="true" />
  </Target>

  <Target Name="_RecordMultiTargetingBrowserLauncher"
          AfterTargets="_ConfigureTestingPlatformBrowserRun"
          Condition=" '$(RuntimeIdentifier)' == 'browser-wasm'
                      AND '$(DotnetTestInvocation)' == 'true'
                      AND '$(DotnetTestHttpBootstrapVersion)' == '1' ">
    <WriteLinesToFile File="$(MSBuildProjectDirectory)\browser-launcher-$(TargetFramework).txt"
                      Lines="$(RunCommand)"
                      Overwrite="true" />
  </Target>
</Project>
""";

    private const string FrameworkOwnedPageSourceCode = """
#file BrowserFrameworkPageTestProject.csproj
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>$TargetFramework$</TargetFramework>
    <RuntimeIdentifier>browser-wasm</RuntimeIdentifier>
    <OutputType>Exe</OutputType>
    <SelfContained>true</SelfContained>
    <EnableMSTestRunner>true</EnableMSTestRunner>
    <EnableMicrosoftTestingPlatform>true</EnableMicrosoftTestingPlatform>
    <ImplicitUsings>enable</ImplicitUsings>
    <WasmMainJSPath>main.js</WasmMainJSPath>
    <WasmBuildNative>false</WasmBuildNative>
    <PublishTrimmed>false</PublishTrimmed>
    <NoWarn>$(NoWarn);NETSDK1201</NoWarn>

    <TestingPlatformBrowserGenerateHostAssets>false</TestingPlatformBrowserGenerateHostAssets>
    <TestingPlatformBrowserHostCommand>$Node$</TestingPlatformBrowserHostCommand>
    <TestingPlatformBrowserHostArguments>&quot;$(MSBuildProjectDirectory)\server.mjs&quot; &quot;$(MSBuildProjectDirectory)\bin\$(Configuration)\$(TargetFramework)\$(RuntimeIdentifier)\AppBundle&quot;</TestingPlatformBrowserHostArguments>
    <TestingPlatformBrowserHostWorkingDirectory>$(MSBuildProjectDirectory)</TestingPlatformBrowserHostWorkingDirectory>
    <TestingPlatformBrowserExecutable>$Browser$</TestingPlatformBrowserExecutable>
    <TestingPlatformBrowserStartupTimeoutSeconds>60</TestingPlatformBrowserStartupTimeoutSeconds>
    <TestingPlatformBrowserCompletionTimeoutSeconds>120</TestingPlatformBrowserCompletionTimeoutSeconds>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="MSTest" Version="$MSTestVersion$" />
    <PackageReference Include="Microsoft.Testing.Platform.Browser" Version="$BrowserPackageVersion$" />
    <WasmExtraFilesToDeploy Include="index.html" />
  </ItemGroup>

</Project>

#file BrowserFrameworkPageTests.cs
using Microsoft.VisualStudio.TestTools.UnitTesting;

[TestClass]
public sealed class BrowserFrameworkPageTests
{
    [TestMethod]
    public void UsesVersionedBrowserApi()
    {
        Assert.IsTrue(OperatingSystem.IsBrowser());
    }
}

#file index.html
<!DOCTYPE html>
<html lang="en">
<head><meta charset="utf-8"><title>Framework-owned MTP page</title></head>
<body><script type="module" src="./main.js"></script></body>
</html>

#file main.js
$FrameworkPageMain$

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

    private const string FrameworkPageMainSource = """
import { dotnet } from './_framework/dotnet.js';

const api = globalThis.testingPlatformBrowser;
if (api?.contractVersion !== 1) {
    throw new Error('Expected testingPlatformBrowser contract version 1.');
}

let exitCode;
let failure;
try {
    const { runMain } = await dotnet.withApplicationArguments(...api.getArguments()).create();
    exitCode = await runMain();
}
catch (error) {
    failure = error;
    exitCode = 1;
    console.error(error instanceof Error ? error.stack ?? error.message : String(error));
}

api.complete(exitCode);
if (failure !== undefined) {
    throw failure;
}
""";

    private const string FatalFrameworkPageMainSource = """
const api = globalThis.testingPlatformBrowser;
if (api?.contractVersion !== 1 || typeof api.reportFatalError !== 'function') {
    throw new Error('Expected testingPlatformBrowser fatal-error API version 1.');
}

api.reportFatalError('framework-owned page fatal marker');
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

        DotnetMuxerResult run = await DotnetCli.RunAsync(
            $"test --project {generator.TargetAssetPath} --configuration Release --framework {TargetFramework} -property:DotnetTestInvocation=true -property:DotnetTestHttpBootstrapVersion=1 -property:DotnetTestInvocationId={InvocationId1} --filter FullyQualifiedName~RunsInsideBrowser",
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
            $"test --project {generator.TargetAssetPath} --configuration Release --framework {TargetFramework} -property:DotnetTestInvocation=true -property:DotnetTestHttpBootstrapVersion=1 -property:DotnetTestInvocationId={InvocationId2} --list-tests",
            warnAsError: false,
            failIfReturnValueIsNotZero: false,
            useMultithreadedMSBuild: false,
            cancellationToken: TestContext.CancellationToken);

        string listOutput = list.StandardOutput + list.StandardError;
        Assert.AreEqual(0, list.ExitCode, list.ToString());
        Assert.Contains("RunsInsideBrowser", listOutput);
        Assert.Contains("SkippedInsideBrowser", listOutput);
        Assert.Contains("FailsInsideBrowser", listOutput);
        Assert.Contains("Discovered 3 tests", listOutput);

        DotnetMuxerResult skipped = await DotnetCli.RunAsync(
            $"test --project {generator.TargetAssetPath} --configuration Release --framework {TargetFramework} -property:DotnetTestInvocation=true -property:DotnetTestHttpBootstrapVersion=1 -property:DotnetTestInvocationId={InvocationId3} --filter FullyQualifiedName~SkippedInsideBrowser",
            warnAsError: false,
            failIfReturnValueIsNotZero: false,
            useMultithreadedMSBuild: false,
            cancellationToken: TestContext.CancellationToken);
        string skippedOutput = skipped.StandardOutput + skipped.StandardError;
        Assert.AreEqual((int)ExitCode.ZeroTests, skipped.ExitCode, skipped.ToString());
        Assert.Contains("skipped: 1", skippedOutput);
        Assert.DoesNotContain("did not complete within", skippedOutput);

        DotnetMuxerResult failed = await DotnetCli.RunAsync(
            $"test --project {generator.TargetAssetPath} --configuration Release --framework {TargetFramework} -property:DotnetTestInvocation=true -property:DotnetTestHttpBootstrapVersion=1 -property:DotnetTestInvocationId={InvocationId4} --filter FullyQualifiedName~FailsInsideBrowser",
            warnAsError: false,
            failIfReturnValueIsNotZero: false,
            useMultithreadedMSBuild: false,
            cancellationToken: TestContext.CancellationToken);
        string failedOutput = failed.StandardOutput + failed.StandardError;
        Assert.AreNotEqual(0, failed.ExitCode, failed.ToString());
        Assert.Contains("failed: 1", failedOutput);
        Assert.Contains("Intentional browser preview failure.", failedOutput);
        Assert.DoesNotContain("did not complete within", failedOutput);
    }

    [TestMethod]
    public async Task BrowserPackage_FrameworkOwnedPageUsesVersionedApi()
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
            "BrowserFrameworkPageTestProject",
            FrameworkOwnedPageSourceCode
                .PatchCodeWithReplace("$FrameworkPageMain$", FrameworkPageMainSource)
                .PatchCodeWithReplace("$TargetFramework$", TargetFramework)
                .PatchCodeWithReplace("$MSTestVersion$", MSTestVersion)
                .PatchCodeWithReplace("$BrowserPackageVersion$", browserPackageVersion)
                .PatchCodeWithReplace("$Node$", EscapeMsBuildValue(node))
                .PatchCodeWithReplace("$Browser$", EscapeMsBuildValue(browser)));

        DotnetMuxerResult run = await DotnetCli.RunAsync(
            $"test --project {generator.TargetAssetPath} --configuration Release --framework {TargetFramework} -property:DotnetTestInvocation=true -property:DotnetTestHttpBootstrapVersion=1 -property:DotnetTestInvocationId={InvocationId5}",
            warnAsError: false,
            failIfReturnValueIsNotZero: false,
            useMultithreadedMSBuild: false,
            cancellationToken: TestContext.CancellationToken);

        string output = run.StandardOutput + run.StandardError;
        Assert.AreEqual(0, run.ExitCode, run.ToString());
        Assert.Contains($"({TargetFramework}|wasm) passed [+1/x0/?0]", output);

        string appBundle = Path.Combine(
            generator.TargetAssetPath,
            "bin",
            "Release",
            TargetFramework,
            WasmRuntime.BrowserRid,
            "AppBundle");
        Assert.IsTrue(File.Exists(Path.Combine(appBundle, "index.html")));
        Assert.IsTrue(File.Exists(Path.Combine(appBundle, "main.js")));
        Assert.IsFalse(
            File.Exists(Path.Combine(appBundle, "Microsoft.Testing.Platform.Browser.main.js")),
            "TestingPlatformBrowserGenerateHostAssets=false must not deploy the package-owned supervisor.");
    }

    [TestMethod]
    public async Task BrowserPackage_CustomWasmMainJsPathDoesNotDeployPackagePage()
    {
        string browserPackageVersion = GetBrowserPackageVersion();
        string source = FrameworkOwnedPageSourceCode
            .Replace(
                "    <TestingPlatformBrowserGenerateHostAssets>false</TestingPlatformBrowserGenerateHostAssets>",
                string.Empty,
                StringComparison.Ordinal)
            .PatchCodeWithReplace("$FrameworkPageMain$", FrameworkPageMainSource)
            .PatchCodeWithReplace("$TargetFramework$", TargetFramework)
            .PatchCodeWithReplace("$MSTestVersion$", MSTestVersion)
            .PatchCodeWithReplace("$BrowserPackageVersion$", browserPackageVersion)
            .PatchCodeWithReplace("$Node$", "node")
            .PatchCodeWithReplace("$Browser$", string.Empty);
        using TestAsset generator = await TestAsset.GenerateAssetAsync(
            "BrowserCustomMainJsProject",
            source);
        Assert.DoesNotContain(
            "TestingPlatformBrowserGenerateHostAssets",
            File.ReadAllText(Path.Combine(generator.TargetAssetPath, "BrowserFrameworkPageTestProject.csproj")));

        DotnetMuxerResult build = await DotnetCli.RunAsync(
            $"build {generator.TargetAssetPath} --configuration Release --framework {TargetFramework} --runtime {WasmRuntime.BrowserRid}",
            warnAsError: false,
            failIfReturnValueIsNotZero: false,
            useMultithreadedMSBuild: false,
            cancellationToken: TestContext.CancellationToken);

        Assert.AreEqual(0, build.ExitCode, build.ToString());
        string appBundle = Path.Combine(
            generator.TargetAssetPath,
            "bin",
            "Release",
            TargetFramework,
            WasmRuntime.BrowserRid,
            "AppBundle");
        Assert.IsTrue(File.Exists(Path.Combine(appBundle, "index.html")));
        Assert.IsTrue(File.Exists(Path.Combine(appBundle, "main.js")));
        Assert.IsFalse(File.Exists(Path.Combine(appBundle, "Microsoft.Testing.Platform.Browser.main.js")));

        string launchConfiguration = Path.Combine(
            generator.TargetAssetPath,
            "obj",
            "Release",
            TargetFramework,
            WasmRuntime.BrowserRid,
            $"Microsoft.Testing.Platform.Browser.{InvocationId6}.launch");

        DotnetMuxerResult plainComputeRunArguments = await DotnetCli.RunAsync(
            $"msbuild {generator.TargetAssetPath} -target:ComputeRunArguments -property:Configuration=Release -property:TargetFramework={TargetFramework} -property:RuntimeIdentifier={WasmRuntime.BrowserRid}",
            warnAsError: false,
            failIfReturnValueIsNotZero: false,
            useMultithreadedMSBuild: false,
            cancellationToken: TestContext.CancellationToken);
        Assert.AreEqual(0, plainComputeRunArguments.ExitCode, plainComputeRunArguments.ToString());
        Assert.IsFalse(File.Exists(launchConfiguration));

        DotnetMuxerResult missingBootstrapVersion = await DotnetCli.RunAsync(
            $"msbuild {generator.TargetAssetPath} -target:ComputeRunArguments -property:Configuration=Release -property:TargetFramework={TargetFramework} -property:RuntimeIdentifier={WasmRuntime.BrowserRid} -property:DotnetTestInvocation=true",
            warnAsError: false,
            failIfReturnValueIsNotZero: false,
            useMultithreadedMSBuild: false,
            cancellationToken: TestContext.CancellationToken);
        Assert.AreNotEqual(0, missingBootstrapVersion.ExitCode);
        Assert.Contains("requires DotnetTestHttpBootstrapVersion=1", missingBootstrapVersion.StandardOutput + missingBootstrapVersion.StandardError);

        DotnetMuxerResult unsupportedBootstrapVersion = await DotnetCli.RunAsync(
            $"msbuild {generator.TargetAssetPath} -target:ComputeRunArguments -property:Configuration=Release -property:TargetFramework={TargetFramework} -property:RuntimeIdentifier={WasmRuntime.BrowserRid} -property:DotnetTestInvocation=true -property:DotnetTestHttpBootstrapVersion=2",
            warnAsError: false,
            failIfReturnValueIsNotZero: false,
            useMultithreadedMSBuild: false,
            cancellationToken: TestContext.CancellationToken);
        Assert.AreNotEqual(0, unsupportedBootstrapVersion.ExitCode);
        Assert.Contains("requires DotnetTestHttpBootstrapVersion=1", unsupportedBootstrapVersion.StandardOutput + unsupportedBootstrapVersion.StandardError);

        DotnetMuxerResult missingInvocationId = await DotnetCli.RunAsync(
            $"msbuild {generator.TargetAssetPath} -target:ComputeRunArguments -property:Configuration=Release -property:TargetFramework={TargetFramework} -property:RuntimeIdentifier={WasmRuntime.BrowserRid} -property:DotnetTestInvocation=true -property:DotnetTestHttpBootstrapVersion=1",
            warnAsError: false,
            failIfReturnValueIsNotZero: false,
            useMultithreadedMSBuild: false,
            cancellationToken: TestContext.CancellationToken);
        Assert.AreNotEqual(0, missingInvocationId.ExitCode);
        Assert.Contains("requires DotnetTestInvocationId", missingInvocationId.StandardOutput + missingInvocationId.StandardError);

        DotnetMuxerResult invalidInvocationId = await DotnetCli.RunAsync(
            $"msbuild {generator.TargetAssetPath} -target:ComputeRunArguments -property:Configuration=Release -property:TargetFramework={TargetFramework} -property:RuntimeIdentifier={WasmRuntime.BrowserRid} -property:DotnetTestInvocation=true -property:DotnetTestHttpBootstrapVersion=1 -property:DotnetTestInvocationId=not-a-guid",
            warnAsError: false,
            failIfReturnValueIsNotZero: false,
            useMultithreadedMSBuild: false,
            cancellationToken: TestContext.CancellationToken);
        Assert.AreNotEqual(0, invalidInvocationId.ExitCode);
        Assert.Contains("32-character hexadecimal GUID", invalidInvocationId.StandardOutput + invalidInvocationId.StandardError);

        DotnetMuxerResult supportedComputeRunArguments = await DotnetCli.RunAsync(
            $"msbuild {generator.TargetAssetPath} -target:ComputeRunArguments -property:Configuration=Release -property:TargetFramework={TargetFramework} -property:RuntimeIdentifier={WasmRuntime.BrowserRid} -property:DotnetTestInvocation=true -property:DotnetTestHttpBootstrapVersion=1 -property:DotnetTestInvocationId={InvocationId6}",
            warnAsError: false,
            failIfReturnValueIsNotZero: false,
            useMultithreadedMSBuild: false,
            cancellationToken: TestContext.CancellationToken);
        Assert.AreEqual(0, supportedComputeRunArguments.ExitCode, supportedComputeRunArguments.ToString());
        Assert.IsTrue(File.Exists(launchConfiguration));
        string legacyLaunchConfiguration = Path.Combine(
            Path.GetDirectoryName(launchConfiguration)!,
            "Microsoft.Testing.Platform.Browser.launch");
        File.WriteAllText(legacyLaunchConfiguration, "legacy");

        DotnetMuxerResult clean = await DotnetCli.RunAsync(
            $"clean {generator.TargetAssetPath} --configuration Release --framework {TargetFramework} --runtime {WasmRuntime.BrowserRid}",
            warnAsError: false,
            failIfReturnValueIsNotZero: false,
            useMultithreadedMSBuild: false,
            cancellationToken: TestContext.CancellationToken);
        Assert.AreEqual(0, clean.ExitCode, clean.ToString());
        Assert.IsFalse(File.Exists(launchConfiguration));
        Assert.IsFalse(File.Exists(legacyLaunchConfiguration));
    }

    [TestMethod]
    public async Task BrowserPackage_FrameworkFatalErrorTerminatesWithoutCompletionTimeout()
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
        string source = FrameworkOwnedPageSourceCode
            .Replace(
                "    <TestingPlatformBrowserCompletionTimeoutSeconds>120</TestingPlatformBrowserCompletionTimeoutSeconds>",
                "    <TestingPlatformBrowserCompletionTimeoutSeconds>5</TestingPlatformBrowserCompletionTimeoutSeconds>",
                StringComparison.Ordinal)
            .PatchCodeWithReplace("$FrameworkPageMain$", FatalFrameworkPageMainSource)
            .PatchCodeWithReplace("$TargetFramework$", TargetFramework)
            .PatchCodeWithReplace("$MSTestVersion$", MSTestVersion)
            .PatchCodeWithReplace("$BrowserPackageVersion$", browserPackageVersion)
            .PatchCodeWithReplace("$Node$", EscapeMsBuildValue(node))
            .PatchCodeWithReplace("$Browser$", EscapeMsBuildValue(browser));
        using TestAsset generator = await TestAsset.GenerateAssetAsync(
            "BrowserFatalFrameworkPageProject",
            source);

        DotnetMuxerResult run = await DotnetCli.RunAsync(
            $"test --project {generator.TargetAssetPath} --configuration Release --framework {TargetFramework} -property:DotnetTestInvocation=true -property:DotnetTestHttpBootstrapVersion=1 -property:DotnetTestInvocationId={InvocationId7}",
            warnAsError: false,
            failIfReturnValueIsNotZero: false,
            useMultithreadedMSBuild: false,
            cancellationToken: TestContext.CancellationToken);

        string output = run.StandardOutput + run.StandardError;
        Assert.AreNotEqual(0, run.ExitCode, run.ToString());
        Assert.Contains("framework-owned page fatal marker", output);
        Assert.Contains("fatal integration error", output);
        Assert.DoesNotContain("did not complete within 5 seconds", output);
    }

    [TestMethod]
    public async Task BrowserPackage_MultiTargetingActivatesOnlyForBrowserDotnetTestInvocation()
    {
        string browserPackageVersion = GetBrowserPackageVersion();
        using TestAsset generator = await TestAsset.GenerateAssetAsync(
            "BrowserMultiTargetingProject",
            MultiTargetingSourceCode
                .PatchCodeWithReplace("$TargetFramework$", TargetFramework)
                .PatchCodeWithReplace("$BrowserPackageVersion$", browserPackageVersion));

        DotnetMuxerResult restore = await DotnetCli.RunAsync(
            $"restore {generator.TargetAssetPath}",
            warnAsError: false,
            failIfReturnValueIsNotZero: false,
            cancellationToken: TestContext.CancellationToken);
        Assert.AreEqual(0, restore.ExitCode, restore.ToString());

        DotnetMuxerResult desktopQuery = await DotnetCli.RunAsync(
            $"msbuild {generator.TargetAssetPath} -target:ComputeRunArguments -property:TargetFramework=net9.0 -property:DotnetTestInvocation=true -property:DotnetTestHttpBootstrapVersion=1",
            warnAsError: false,
            failIfReturnValueIsNotZero: false,
            useMultithreadedMSBuild: false,
            cancellationToken: TestContext.CancellationToken);
        Assert.AreEqual(0, desktopQuery.ExitCode, desktopQuery.ToString());
        Assert.AreEqual(
            "framework-host-net9.0",
            File.ReadAllText(Path.Combine(generator.TargetAssetPath, "framework-host-net9.0.txt")).Trim());
        Assert.IsFalse(File.Exists(Path.Combine(generator.TargetAssetPath, "browser-launcher-net9.0.txt")));

        DotnetMuxerResult plainBrowserQuery = await DotnetCli.RunAsync(
            $"msbuild {generator.TargetAssetPath} -target:ComputeRunArguments -property:TargetFramework={TargetFramework} -property:RuntimeIdentifier={WasmRuntime.BrowserRid}",
            warnAsError: false,
            failIfReturnValueIsNotZero: false,
            useMultithreadedMSBuild: false,
            cancellationToken: TestContext.CancellationToken);
        Assert.AreEqual(0, plainBrowserQuery.ExitCode, plainBrowserQuery.ToString());
        Assert.AreEqual(
            $"framework-host-{TargetFramework}",
            File.ReadAllText(Path.Combine(generator.TargetAssetPath, $"framework-host-{TargetFramework}.txt")).Trim());
        Assert.IsFalse(File.Exists(Path.Combine(generator.TargetAssetPath, $"browser-launcher-{TargetFramework}.txt")));

        DotnetMuxerResult dotnetTestBrowserQuery = await DotnetCli.RunAsync(
            $"msbuild {generator.TargetAssetPath} -target:ComputeRunArguments -property:TargetFramework={TargetFramework} -property:RuntimeIdentifier={WasmRuntime.BrowserRid} -property:DotnetTestInvocation=true -property:DotnetTestHttpBootstrapVersion=1 -property:DotnetTestInvocationId={InvocationId8}",
            warnAsError: false,
            failIfReturnValueIsNotZero: false,
            useMultithreadedMSBuild: false,
            cancellationToken: TestContext.CancellationToken);
        Assert.AreEqual(0, dotnetTestBrowserQuery.ExitCode, dotnetTestBrowserQuery.ToString());
        string browserLauncherCommand = File.ReadAllText(
            Path.Combine(generator.TargetAssetPath, $"browser-launcher-{TargetFramework}.txt"));
        Assert.IsNotEmpty(browserLauncherCommand);
        Assert.DoesNotContain($"framework-host-{TargetFramework}", browserLauncherCommand);

        Task<DotnetMuxerResult> firstQueryTask = DotnetCli.RunAsync(
            $"msbuild {generator.TargetAssetPath} -target:ComputeRunArguments -property:TargetFramework={TargetFramework} -property:RuntimeIdentifier={WasmRuntime.BrowserRid} -property:DotnetTestInvocation=true -property:DotnetTestHttpBootstrapVersion=1 -property:DotnetTestInvocationId={InvocationId9}",
            warnAsError: false,
            failIfReturnValueIsNotZero: false,
            useMultithreadedMSBuild: false,
            cancellationToken: TestContext.CancellationToken);
        Task<DotnetMuxerResult> secondQueryTask = DotnetCli.RunAsync(
            $"msbuild {generator.TargetAssetPath} -target:ComputeRunArguments -property:TargetFramework={TargetFramework} -property:RuntimeIdentifier={WasmRuntime.BrowserRid} -property:DotnetTestInvocation=true -property:DotnetTestHttpBootstrapVersion=1 -property:DotnetTestInvocationId={InvocationIdA}",
            warnAsError: false,
            failIfReturnValueIsNotZero: false,
            useMultithreadedMSBuild: false,
            cancellationToken: TestContext.CancellationToken);

        DotnetMuxerResult firstQuery = await firstQueryTask;
        DotnetMuxerResult secondQuery = await secondQueryTask;
        Assert.AreEqual(0, firstQuery.ExitCode, firstQuery.ToString());
        Assert.AreEqual(0, secondQuery.ExitCode, secondQuery.ToString());

        string intermediateDirectory = Path.Combine(
            generator.TargetAssetPath,
            "obj",
            "Debug",
            TargetFramework,
            WasmRuntime.BrowserRid);
        string firstConfiguration = Path.Combine(
            intermediateDirectory,
            $"Microsoft.Testing.Platform.Browser.{InvocationId9}.launch");
        string secondConfiguration = Path.Combine(
            intermediateDirectory,
            $"Microsoft.Testing.Platform.Browser.{InvocationIdA}.launch");
        Assert.IsTrue(File.Exists(firstConfiguration));
        Assert.IsTrue(File.Exists(secondConfiguration));

        string firstArguments = ReadEncodedLaunchConfigurationValue(firstConfiguration, "host-arguments-uri");
        string secondArguments = ReadEncodedLaunchConfigurationValue(secondConfiguration, "host-arguments-uri");
        Assert.Contains(InvocationId9, firstArguments);
        Assert.DoesNotContain(InvocationIdA, firstArguments);
        Assert.Contains(InvocationIdA, secondArguments);
        Assert.DoesNotContain(InvocationId9, secondArguments);

        DotnetMuxerResult clean = await DotnetCli.RunAsync(
            $"clean {generator.TargetAssetPath} --framework {TargetFramework} --runtime {WasmRuntime.BrowserRid}",
            warnAsError: false,
            failIfReturnValueIsNotZero: false,
            useMultithreadedMSBuild: false,
            cancellationToken: TestContext.CancellationToken);
        Assert.AreEqual(0, clean.ExitCode, clean.ToString());
        Assert.IsEmpty(
            Directory.EnumerateFiles(
                intermediateDirectory,
                "Microsoft.Testing.Platform.Browser.*.launch",
                SearchOption.TopDirectoryOnly));
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
        Assert.Contains("buildMultiTargeting/Microsoft.Testing.Platform.Browser.After.targets", entries);

        ZipArchiveEntry browserMainEntry = archive.GetEntry(
            "buildMultiTargeting/assets/Microsoft.Testing.Platform.Browser.main.js")
            ?? throw new AssertFailedException("The package-owned browser supervisor was not packaged.");
        using Stream browserMainStream = browserMainEntry.Open();
        using var browserMainReader = new StreamReader(browserMainStream);
        string browserMain = browserMainReader.ReadToEnd();
        Assert.Contains("testingPlatformBrowser", browserMain);
        Assert.DoesNotContain("__mtpBrowserArguments", browserMain);
        Assert.DoesNotContain("__mtpBrowserResult", browserMain);

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
            $"pack {Path.Combine(RootFinder.Find(), "src", "Platform", "Microsoft.Testing.Platform.Browser", "Microsoft.Testing.Platform.Browser.csproj")} --configuration Debug --no-build --no-restore -property:PackageOutputPath={packageOutput}",
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

    private static string ReadEncodedLaunchConfigurationValue(string path, string name)
    {
        string prefix = name + "=";
        string line = File.ReadLines(path).Single(line => line.StartsWith(prefix, StringComparison.Ordinal));
        return Uri.UnescapeDataString(line[prefix.Length..]);
    }
}
