// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.Acceptance.IntegrationTests;

/// <summary>
/// End-to-end coverage for running MSTest tests on <c>browser-wasm</c> (issue
/// <see href="https://github.com/microsoft/testfx/issues/2196"/>).
///
/// <para>
/// <c>browser-wasm</c> shares the single-threaded execution model of <c>wasi-wasm</c> (no thread
/// pool: <c>Task.Run</c> continuations never execute and blocking waits throw
/// <see cref="PlatformNotSupportedException"/>), and the same platform/adapter fallbacks
/// (<c>RuntimeFeatureHelper.IsMultiThreaded</c> / <c>RuntimeContext.IsMultiThreaded</c>) already cover
/// both. What differs is the <b>host</b>: <c>wasi-wasm</c> runs headless under <c>wasmtime</c>, while
/// <c>browser-wasm</c> boots through a JS module that loads <c>dotnet.js</c>. Because
/// Microsoft.Testing.Platform never touches the DOM, the published bundle boots headlessly under
/// <c>node</c> via the same loader, which is what this test uses instead of a real browser.
/// </para>
///
/// <para>Two complementary assertions, mirroring <see cref="WasmExecutionTests"/>:</para>
/// <list type="number">
///   <item>
///     <see cref="BrowserWasmBuild_GeneratesTestingPlatformEntryPoint"/> builds for
///     <c>browser-wasm</c> and asserts the MTP entry point is generated. The repo bootstrap
///     installs the required <c>wasm-tools</c> workload (see <c>eng/restore-toolset</c>), so this
///     runs in CI; it is skipped (inconclusive) only when that workload is genuinely absent (e.g.
///     a local machine that has not run the bootstrap), and otherwise a build failure fails the test.
///   </item>
///   <item>
///     <see cref="BrowserWasmExecution_RunsTestsUnderNode"/> publishes a real MSTest project for
///     <c>browser-wasm</c> and boots it end-to-end under <c>node</c> via a small
///     <c>dotnet.js</c> runner, asserting the passing tests report success and the failing test is
///     reported as failed. Skipped (inconclusive) only when the <c>wasm-tools</c> workload or
///     <c>node</c> is unavailable; otherwise publish must succeed and the run must exit with
///     <c>AtLeastOneTestFailed</c>.
///   </item>
/// </list>
/// </summary>
[TestClass]
public sealed class BrowserWasmExecutionTests : AcceptanceTestBase<NopAssetFixture>
{
    private const string BrowserRid = "browser-wasm";

    // browser-wasm is only supported on the current .NET TFM in this repo (see samples/BrowserPlayground).
    private static readonly string TargetFramework = TargetFrameworks.NetCurrent;

    // Minimal MSTest project targeting browser-wasm, running on Microsoft.Testing.Platform via the
    // generated entry point. The bundled main.js is the WasmMainJSPath boot module (needed for a
    // browser-wasm build to produce a bundle); the node run below uses a runner staged at test time.
    // One passing and one failing test so the run exercises both the success summary and the
    // non-zero exit code on failure.
    private const string SourceCode = """
#file BrowserTestProject.csproj
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>$TargetFramework$</TargetFramework>
    <RuntimeIdentifier>$BrowserRid$</RuntimeIdentifier>
    <OutputType>Exe</OutputType>
    <!-- Publishing browser-wasm must be self-contained so the mono browser-wasm runtime pack resolves. -->
    <SelfContained>true</SelfContained>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <EnableMSTestRunner>true</EnableMSTestRunner>

    <!--
        Force the locally built Microsoft.Testing.Platform dependency to win over the transitive
        -preview one, mirroring the other acceptance-test assets.
    -->
    <EnableMicrosoftTestingPlatform>true</EnableMicrosoftTestingPlatform>

    <!-- JS boot module the wasm runtime loads first (see wwwroot/main.js in samples/BrowserPlayground). -->
    <WasmMainJSPath>main.js</WasmMainJSPath>

    <!--
        Use the pre-built dotnet.native.wasm (no emscripten relink) and keep the managed assemblies
        untrimmed: MSTest discovers tests reflectively and an untuned trim strips the test methods
        (resulting in 0 discovered). InvariantGlobalization is intentionally NOT set: on browser-wasm
        it would force WasmBuildNative=true (an emscripten relink), which we avoid here; the browser
        bundle ships ICU by default.
    -->
    <WasmBuildNative>false</WasmBuildNative>
    <PublishTrimmed>false</PublishTrimmed>

    <NoWarn>$(NoWarn);NETSDK1201</NoWarn>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="MSTest" Version="$MSTestVersion$" />
  </ItemGroup>

</Project>

#file main.js
import { dotnet } from './_framework/dotnet.js';
const { runMain } = await dotnet.withApplicationArgumentsFromQuery().create();
const exitCode = await runMain();
globalThis.mtpExitCode = exitCode;

#file UnitTest1.cs
using Microsoft.VisualStudio.TestTools.UnitTesting;

[TestClass]
public sealed class UnitTest1
{
    [TestMethod]
    public void PassingTest()
        => Assert.AreEqual(4, 2 + 2);

    [TestMethod]
    public void AnotherPassingTest()
        => Assert.IsTrue(true);

    [TestMethod]
    public void FailingTest()
        => Assert.Fail("Intentional failure to verify failures are reported under browser-wasm.");
}
""";

    // node runner staged next to the published bundle. Boots the browser-wasm app under node (no DOM
    // required — Microsoft.Testing.Platform does not touch the DOM) and maps the .NET exit code onto
    // the node process exit code. Mirrors samples/BrowserPlayground/wwwroot/runtests.mjs.
    private const string NodeRunnerSource = """
import { dotnet } from './_framework/dotnet.js';
const { runMain } = await dotnet.withApplicationArguments(...process.argv.slice(2)).create();
const exitCode = await runMain();
// Set exitCode rather than calling process.exit(): process.exit() can terminate Node before
// redirected stdout/stderr has flushed, which would truncate the MTP summary this test asserts on.
process.exitCode = exitCode;
""";

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task BrowserWasmBuild_GeneratesTestingPlatformEntryPoint()
    {
        // Detect the toolchain up front and skip only when it is genuinely absent. Once the
        // 'wasm-tools' workload is installed, a non-zero build must fail this test so real
        // regressions (compiler errors, a broken generated MTP entry point) are not masked.
        if (!await IsBrowserWasmWorkloadInstalledAsync())
        {
            Assert.Inconclusive(
                "Skipping browser-wasm build: the 'wasm-tools' workload is not installed. " +
                "Install it with 'dotnet workload install wasm-tools' to exercise this test.");
            return;
        }

        using TestAsset generator = await GenerateBrowserWasmAssetAsync();

        DotnetMuxerResult buildResult = await DotnetCli.RunAsync(
            $"build {generator.TargetAssetPath} -f {TargetFramework} -r {BrowserRid} -c Release",
            // Trimming/wasm builds can emit non-actionable warnings; we only assert on the build
            // succeeding and the entry point being generated, not on a warning-clean build.
            warnAsError: false,
            cancellationToken: TestContext.CancellationToken);

        buildResult.AssertExitCodeIs(0);

        // The MTP MSBuild task writes the generated entry point into the intermediate output folder.
        // Its presence proves the browser-wasm build produced the Microsoft.Testing.Platform host
        // entry point (the plumbing that browser-wasm execution ultimately relies on).
        string[] generatedEntryPoints = Directory.GetFiles(
            Path.Combine(generator.TargetAssetPath, "obj"),
            "MicrosoftTestingPlatformEntryPoint.cs",
            SearchOption.AllDirectories);

        Assert.IsNotEmpty(
            generatedEntryPoints,
            $"Expected a generated 'MicrosoftTestingPlatformEntryPoint.cs' under '{Path.Combine(generator.TargetAssetPath, "obj")}'.");
    }

    [TestMethod]
    public async Task BrowserWasmExecution_RunsTestsUnderNode()
    {
        // Detect the toolchain up front and skip only when it is genuinely absent. With the
        // workload and node present, publish and execution must succeed/behave as asserted, so
        // browser-specific publish or run regressions are not silently swallowed.
        if (!await IsBrowserWasmWorkloadInstalledAsync())
        {
            Assert.Inconclusive(
                "Skipping browser-wasm execution: the 'wasm-tools' workload is not installed. " +
                "Install it with 'dotnet workload install wasm-tools' to exercise this test.");
            return;
        }

        string? node = LocateNode();
        if (node is null)
        {
            Assert.Inconclusive(
                "Skipping browser-wasm execution: 'node' was not found on PATH (nor via NODE_EXE). " +
                "Install Node.js to exercise this test.");
            return;
        }

        using TestAsset generator = await GenerateBrowserWasmAssetAsync();

        // The workload is present, so a publish failure is a real browser-wasm regression: require success.
        DotnetMuxerResult publishResult = await DotnetCli.RunAsync(
            $"publish {generator.TargetAssetPath} -f {TargetFramework} -r {BrowserRid} -c Release",
            warnAsError: false,
            cancellationToken: TestContext.CancellationToken);

        publishResult.AssertExitCodeIs(0);

        string appBundle = Path.Combine(
            generator.TargetAssetPath, "bin", "Release", TargetFramework, BrowserRid, "AppBundle");
        Assert.IsTrue(
            Directory.Exists(appBundle),
            $"Expected the browser-wasm AppBundle directory at '{appBundle}'.");

        // Stage the node runner next to the bundle so it can import ./_framework/dotnet.js.
        string runnerPath = Path.Combine(appBundle, "runtests.mjs");
        File.WriteAllText(runnerPath, NodeRunnerSource);

        var commandLine = new TestInfrastructure.CommandLine();
        int exitCode = await commandLine.RunAsyncAndReturnExitCodeAsync(
            $"\"{node}\" runtests.mjs",
            workingDirectory: appBundle,
            cancellationToken: TestContext.CancellationToken);

        string output = commandLine.StandardOutput;
        string error = commandLine.ErrorOutput;
        string combined = $"STDOUT:{Environment.NewLine}{output}{Environment.NewLine}STDERR:{Environment.NewLine}{error}";

        // A PlatformNotSupportedException (the single-threaded blocking-wait failure mode) would
        // indicate a regression in the wasm fallbacks.
        Assert.IsFalse(
            error.Contains("PlatformNotSupportedException", StringComparison.Ordinal)
                || output.Contains("PlatformNotSupportedException", StringComparison.Ordinal),
            $"Microsoft.Testing.Platform hit an unexpected PlatformNotSupportedException under browser-wasm.{Environment.NewLine}{combined}");

        // The run summary proves tests actually executed: the two passing tests succeeded and the
        // intentional failure was reported.
        Assert.IsTrue(
            output.Contains("succeeded: 2", StringComparison.Ordinal),
            $"Expected 2 succeeded tests in the browser-wasm run summary.{Environment.NewLine}{combined}");
        Assert.IsTrue(
            output.Contains("failed: 1", StringComparison.Ordinal),
            $"Expected 1 failed test in the browser-wasm run summary.{Environment.NewLine}{combined}");

        // Assert the exact "at least one test failed" exit code (2). A generic non-zero check would
        // also pass for a post-run crash or other MTP failure; requiring AtLeastOneTestFailed keeps
        // those from masquerading as the expected failing-test outcome.
        Assert.AreEqual(
            (int)ExitCode.AtLeastOneTestFailed,
            exitCode,
            $"Expected exit code {(int)ExitCode.AtLeastOneTestFailed} (AtLeastOneTestFailed) because one test fails.{Environment.NewLine}{combined}");
    }

    private Task<TestAsset> GenerateBrowserWasmAssetAsync()
        => TestAsset.GenerateAssetAsync(
            "BrowserTestProject",
            SourceCode
                .PatchCodeWithReplace("$TargetFramework$", TargetFramework)
                .PatchCodeWithReplace("$BrowserRid$", BrowserRid)
                .PatchCodeWithReplace("$MSTestVersion$", MSTestVersion));

    // Detects whether the 'wasm-tools' workload (which provides the browser-wasm build/publish
    // toolchain) is installed for the SDK under test, by parsing 'dotnet workload list'. This lets
    // the tests skip only when the toolchain is genuinely absent instead of swallowing every build
    // failure as "workload missing".
    private async Task<bool> IsBrowserWasmWorkloadInstalledAsync()
    {
        DotnetMuxerResult result = await DotnetCli.RunAsync(
            "workload list",
            warnAsError: false,
            // 'workload list' returns non-zero in some environments; we only parse its output.
            failIfReturnValueIsNotZero: false,
            cancellationToken: TestContext.CancellationToken);

        // The installed workload id is 'wasm-tools' (the SDK may print a versioned manifest such as
        // 'wasm-tools-net10', so match the stem case-insensitively).
        return result.StandardOutput.Contains("wasm-tools", StringComparison.OrdinalIgnoreCase);
    }

    private static string? LocateNode()
    {
        if (Environment.GetEnvironmentVariable("NODE_EXE") is { Length: > 0 } fromEnv && File.Exists(fromEnv))
        {
            return fromEnv;
        }

        string exeName = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "node.exe" : "node";
        string? path = Environment.GetEnvironmentVariable("PATH");
        if (path is null)
        {
            return null;
        }

        foreach (string directory in path.Split(Path.PathSeparator))
        {
            if (directory.Length == 0)
            {
                continue;
            }

            string candidate = Path.Combine(directory, exeName);
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        return null;
    }
}
