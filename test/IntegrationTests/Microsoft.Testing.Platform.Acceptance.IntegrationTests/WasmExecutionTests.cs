// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.Acceptance.IntegrationTests;

/// <summary>
/// End-to-end coverage for running Microsoft.Testing.Platform (MTP) — both the raw platform and the
/// MSTest adapter on top of it — in WebAssembly (issue
/// <see href="https://github.com/microsoft/testfx/issues/2196"/>) using <c>wasi-wasm</c> as a
/// headless proxy for <c>browser-wasm</c>: both share the same single-threaded execution model, so
/// getting <c>wasi-wasm</c> green covers the core platform work <c>browser-wasm</c> also needs.
///
/// <para>
/// Single-threaded wasm runtimes have no thread pool: <c>Task.Run</c> continuations never execute
/// and blocking waits throw <see cref="PlatformNotSupportedException"/>. The platform and the MSTest
/// adapter detect this (see <c>RuntimeFeatureHelper.IsMultiThreaded</c> /
/// <c>RuntimeContext.IsMultiThreaded</c>) and fall back to inline/synchronous execution for the few
/// thread-dependent spots on the run path (message-bus consumers, the shutdown watchdog, telemetry
/// ingest, the countdown-event wait, and the adapter's per-test task factory). A user-authored
/// <c>async Task Main</c> is fine as long as the pipeline completes without hopping to a background
/// thread, which is exactly what these fallbacks guarantee.
/// </para>
///
/// <para>
/// Some extensions have their own wasi limitations unrelated to threading and are therefore not
/// exercised here: the hang-dump provider relies on <c>System.Diagnostics.Process</c>
/// (<see href="https://github.com/microsoft/testfx/issues/8557"/>) and the Azure DevOps provider's
/// <c>HttpClient</c> sets <c>AutomaticDecompression</c>, which the wasi <c>HttpClientHandler</c> does
/// not support.
/// </para>
///
/// <para>Two layers, each with an always-on build assertion and a gated execution assertion:</para>
/// <list type="number">
///   <item>
///     Raw platform: <see cref="RawPlatform_BuildsForWasi"/> and
///     <see cref="RawPlatform_RunsUnderWasmtime"/> host MTP directly with a <c>DummyFramework</c>
///     (one passing test, no MSTest adapter). This is the scenario the former
///     <c>samples/WasiPlayground</c> demonstrated, now promoted to an acceptance test.
///   </item>
///   <item>
///     MSTest adapter: <see cref="WasmBuild_GeneratesTestingPlatformEntryPoint"/> and
///     <see cref="WasmExecution_RunsTestsUnderWasmtime"/> build/publish a real MSTest project (the
///     adapter running on MTP via the generated entry point, no user-authored Main).
///   </item>
/// </list>
///
/// <para>
/// The build assertions only rely on the <c>wasi-experimental</c> workload that the repo bootstrap
/// installs, so they run on every CI leg and guard the build/entry-point plumbing against silent
/// regressions. The execution assertions publish for <c>wasi-wasm</c> and run under <c>wasmtime</c>;
/// they are skipped (inconclusive) when the wasm publish toolchain (<c>wasm-tools</c> workload) or
/// <c>wasmtime</c> is not available — for example on the default Windows CI matrix — so they only run
/// where the wasm runtime exists.
/// </para>
/// </summary>
[TestClass]
public sealed class WasmExecutionTests : AcceptanceTestBase<NopAssetFixture>
{
    // The wasm runtime that the mono wasi runtime pack targets.
    private const string WasiRid = "wasi-wasm";

    // wasi-wasm is only supported on the current .NET TFM in this repo.
    private static readonly string TargetFramework = TargetFrameworks.NetCurrent;

    // Minimal project hosting Microsoft.Testing.Platform directly (no MSTest adapter): a user-authored
    // async Main registers a DummyFramework that publishes a single passing test. This is the raw
    // platform equivalent of the MSTest asset below and mirrors the former samples/WasiPlayground.
    private const string RawPlatformSourceCode = """
#file WasmPlatformProject.csproj
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>$TargetFramework$</TargetFramework>
    <RuntimeIdentifier>$WasiRid$</RuntimeIdentifier>
    <OutputType>Exe</OutputType>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>

    <!--
        wasi-wasm build knobs, shared with the MSTest asset below:
        - UsingWasiRuntimeWorkload works around an SDK manifest bug where the WASI Sdk targets are
          never imported for net10 projects, so no dotnet.wasm is produced.
        - WasmSingleFileBundle=false keeps the managed assemblies on disk to avoid requiring the
          wasi-sdk (clang) toolchain.
        - WasmBuildNative=false / PublishTrimmed=false run in interpreter mode against the pre-built
          dotnet.wasm, avoiding the native relink step.
        - InvariantGlobalization avoids a trimmer crash during publish.
    -->
    <UsingWasiRuntimeWorkload>true</UsingWasiRuntimeWorkload>
    <WasmSingleFileBundle>false</WasmSingleFileBundle>
    <WasmBuildNative>false</WasmBuildNative>
    <PublishTrimmed>false</PublishTrimmed>
    <InvariantGlobalization>true</InvariantGlobalization>

    <NoWarn>$(NoWarn);NETSDK1201</NoWarn>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.Testing.Platform" Version="$MicrosoftTestingPlatformVersion$" />
  </ItemGroup>

</Project>

#file Program.cs
using Microsoft.Testing.Platform.Builder;
using Microsoft.Testing.Platform.Capabilities.TestFramework;
using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Extensions.TestFramework;

ITestApplicationBuilder testApplicationBuilder = await TestApplication.CreateBuilderAsync(args);
testApplicationBuilder.RegisterTestFramework(_ => new TestFrameworkCapabilities(), (_, _) => new DummyFramework());
using ITestApplication testApplication = await testApplicationBuilder.BuildAsync();
return await testApplication.RunAsync();

internal sealed class DummyFramework : ITestFramework, IDataProducer
{
    public string Uid => nameof(DummyFramework);

    public string Version => "1.0.0";

    public string DisplayName => "DummyFramework";

    public string Description => DisplayName;

    public Type[] DataTypesProduced => [typeof(TestNodeUpdateMessage)];

    public Task<CloseTestSessionResult> CloseTestSessionAsync(CloseTestSessionContext context)
        => Task.FromResult(new CloseTestSessionResult() { IsSuccess = true });

    public Task<CreateTestSessionResult> CreateTestSessionAsync(CreateTestSessionContext context)
        => Task.FromResult(new CreateTestSessionResult() { IsSuccess = true });

    public async Task ExecuteRequestAsync(ExecuteRequestContext context)
    {
        await context.MessageBus.PublishAsync(this, new TestNodeUpdateMessage(context.Request.Session.SessionUid, new TestNode()
        {
            DisplayName = "Test display name",
            Uid = "Uid1",
            Properties = new PropertyBag(PassedTestNodeStateProperty.CachedInstance),
        }));
        context.Complete();
    }

    public Task<bool> IsEnabledAsync() => Task.FromResult(true);
}
""";

    // Minimal MSTest project targeting wasi-wasm, running on Microsoft.Testing.Platform via the
    // generated entry point (no user-authored Main). One passing and one failing test so the run
    // exercises both the success summary and the non-zero exit code on failure.
    private const string SourceCode = """
#file WasmTestProject.csproj
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>$TargetFramework$</TargetFramework>
    <RuntimeIdentifier>$WasiRid$</RuntimeIdentifier>
    <OutputType>Exe</OutputType>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <EnableMSTestRunner>true</EnableMSTestRunner>

    <!--
        Force the locally built Microsoft.Testing.Platform dependency to win over the transitive
        -preview one, mirroring the other acceptance-test assets.
    -->
    <EnableMicrosoftTestingPlatform>true</EnableMicrosoftTestingPlatform>

    <!--
        wasi-wasm build knobs, shared with the raw-platform asset above:
        - UsingWasiRuntimeWorkload works around an SDK manifest bug where the WASI Sdk targets are
          never imported for net10 projects, so no dotnet.wasm is produced.
        - WasmSingleFileBundle=false keeps the managed assemblies on disk to avoid requiring the
          wasi-sdk (clang) toolchain.
        - InvariantGlobalization avoids a trimmer crash during publish.
    -->
    <UsingWasiRuntimeWorkload>true</UsingWasiRuntimeWorkload>
    <WasmSingleFileBundle>false</WasmSingleFileBundle>
    <InvariantGlobalization>true</InvariantGlobalization>

    <!--
        Run in interpreter mode: use the pre-built dotnet.wasm and load managed assemblies from
        disk. This avoids the native relink step (which pulls in the wasi-sdk / clang toolchain and
        is turned on by default for a Release publish). Trimming is disabled because MSTest discovers
        tests reflectively and an untuned trim strips the test methods (resulting in 0 discovered).
    -->
    <WasmBuildNative>false</WasmBuildNative>
    <PublishTrimmed>false</PublishTrimmed>

    <NoWarn>$(NoWarn);NETSDK1201</NoWarn>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="MSTest" Version="$MSTestVersion$" />
  </ItemGroup>

</Project>

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
        => Assert.Fail("Intentional failure to verify failures are reported under wasi-wasm.");
}
""";

    private const string WasmtimeUnavailableMessage =
        "Skipping wasm execution: 'wasmtime' was not found on PATH (nor via WASMTIME_EXE). " +
        "Install wasmtime and the 'wasm-tools' workload to exercise this test.";

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task RawPlatform_BuildsForWasi()
    {
        using TestAsset generator = await GenerateRawPlatformAssetAsync();

        DotnetMuxerResult buildResult = await DotnetCli.RunAsync(
            $"build {generator.TargetAssetPath} -f {TargetFramework} -r {WasiRid} -c Release",
            // Trimming/wasm builds can emit non-actionable warnings; we only assert on the build
            // succeeding, not on a warning-clean build.
            warnAsError: false,
            cancellationToken: TestContext.CancellationToken);

        // A clean build proves the Microsoft.Testing.Platform package restores and compiles against
        // wasi-wasm — the plumbing that browser-wasm/wasi-wasm execution ultimately relies on.
        buildResult.AssertExitCodeIs(0);
    }

    [TestMethod]
    public async Task RawPlatform_RunsUnderWasmtime()
    {
        string? wasmtime = LocateWasmtime();
        if (wasmtime is null)
        {
            Assert.Inconclusive(WasmtimeUnavailableMessage);
            return;
        }

        using TestAsset generator = await GenerateRawPlatformAssetAsync();
        string? appBundle = await TryPublishForWasiAsync(generator);
        if (appBundle is null)
        {
            return;
        }

        (int exitCode, _, string error, string combined) =
            await RunUnderWasmtimeAsync(wasmtime, appBundle, "WasmPlatformProject");

        // No test fails, so the platform must exit cleanly. A PlatformNotSupportedException (the
        // single-threaded blocking-wait failure mode) would indicate a regression in the wasm
        // fallbacks.
        Assert.IsFalse(
            error.Contains("PlatformNotSupportedException", StringComparison.Ordinal),
            $"Microsoft.Testing.Platform hit an unexpected PlatformNotSupportedException under wasi-wasm.{Environment.NewLine}{combined}");

        // The DummyFramework publishes a single passing test.
        Assert.IsTrue(
            combined.Contains("succeeded: 1", StringComparison.Ordinal),
            $"Expected 1 succeeded test in the wasm run summary.{Environment.NewLine}{combined}");
        Assert.IsTrue(
            combined.Contains("failed: 0", StringComparison.Ordinal),
            $"Expected 0 failed tests in the wasm run summary.{Environment.NewLine}{combined}");

        // MTP returns a zero exit code when every test passes.
        Assert.AreEqual(
            0,
            exitCode,
            $"Expected a zero exit code because all tests pass.{Environment.NewLine}{combined}");
    }

    [TestMethod]
    public async Task WasmBuild_GeneratesTestingPlatformEntryPoint()
    {
        using TestAsset generator = await GenerateMSTestAssetAsync();

        DotnetMuxerResult buildResult = await DotnetCli.RunAsync(
            $"build {generator.TargetAssetPath} -f {TargetFramework} -r {WasiRid} -c Release",
            // Trimming/wasm builds can emit non-actionable warnings; we only assert on the build
            // succeeding and the entry point being generated, not on a warning-clean build.
            warnAsError: false,
            cancellationToken: TestContext.CancellationToken);

        buildResult.AssertExitCodeIs(0);

        // The MTP MSBuild task writes the generated entry point into the intermediate output folder.
        // Its presence proves the wasi-wasm build produced the Microsoft.Testing.Platform host entry
        // point (the plumbing that browser-wasm/wasi-wasm execution ultimately relies on).
        string[] generatedEntryPoints = Directory.GetFiles(
            Path.Combine(generator.TargetAssetPath, "obj"),
            "MicrosoftTestingPlatformEntryPoint.cs",
            SearchOption.AllDirectories);

        Assert.IsNotEmpty(
            generatedEntryPoints,
            $"Expected a generated 'MicrosoftTestingPlatformEntryPoint.cs' under '{Path.Combine(generator.TargetAssetPath, "obj")}'.");
    }

    [TestMethod]
    public async Task WasmExecution_RunsTestsUnderWasmtime()
    {
        string? wasmtime = LocateWasmtime();
        if (wasmtime is null)
        {
            Assert.Inconclusive(WasmtimeUnavailableMessage);
            return;
        }

        using TestAsset generator = await GenerateMSTestAssetAsync();
        string? appBundle = await TryPublishForWasiAsync(generator);
        if (appBundle is null)
        {
            return;
        }

        (int exitCode, string output, string error, string combined) =
            await RunUnderWasmtimeAsync(wasmtime, appBundle, "WasmTestProject");

        // The asset has one failing test, so a non-zero exit code is expected — but it must be the
        // clean "tests failed" exit, not a crash. A PlatformNotSupportedException (the single-threaded
        // blocking-wait failure mode) would indicate a regression in the wasm fallbacks.
        Assert.IsFalse(
            error.Contains("PlatformNotSupportedException", StringComparison.Ordinal),
            $"Microsoft.Testing.Platform hit an unexpected PlatformNotSupportedException under wasi-wasm.{Environment.NewLine}{combined}");

        // The run summary proves tests actually executed: the two passing tests succeeded and the
        // intentional failure was reported.
        Assert.IsTrue(
            output.Contains("succeeded: 2", StringComparison.Ordinal),
            $"Expected 2 succeeded tests in the wasm run summary.{Environment.NewLine}{combined}");
        Assert.IsTrue(
            output.Contains("failed: 1", StringComparison.Ordinal),
            $"Expected 1 failed test in the wasm run summary.{Environment.NewLine}{combined}");

        // MTP returns a non-zero exit code when any test fails.
        Assert.AreNotEqual(
            0,
            exitCode,
            $"Expected a non-zero exit code because one test fails.{Environment.NewLine}{combined}");
    }

    private Task<TestAsset> GenerateRawPlatformAssetAsync()
        => TestAsset.GenerateAssetAsync(
            "WasmPlatformProject",
            RawPlatformSourceCode
                .PatchCodeWithReplace("$TargetFramework$", TargetFramework)
                .PatchCodeWithReplace("$WasiRid$", WasiRid)
                .PatchCodeWithReplace("$MicrosoftTestingPlatformVersion$", MicrosoftTestingPlatformVersion));

    private Task<TestAsset> GenerateMSTestAssetAsync()
        => TestAsset.GenerateAssetAsync(
            "WasmTestProject",
            SourceCode
                .PatchCodeWithReplace("$TargetFramework$", TargetFramework)
                .PatchCodeWithReplace("$WasiRid$", WasiRid)
                .PatchCodeWithReplace("$MSTestVersion$", MSTestVersion));

    // Publishes the asset for wasi-wasm and returns the AppBundle directory, staging ICU next to it.
    // Returns null (after marking the test inconclusive) when the 'wasm-tools' publish workload is
    // not installed — e.g. on the default Windows CI matrix.
    private async Task<string?> TryPublishForWasiAsync(TestAsset generator)
    {
        DotnetMuxerResult publishResult = await DotnetCli.RunAsync(
            $"publish {generator.TargetAssetPath} -f {TargetFramework} -r {WasiRid} -c Release",
            warnAsError: false,
            // Publishing wasi-wasm requires the 'wasm-tools' workload; when it is missing dotnet
            // fails rather than skipping, so don't blow up the run — treat it as "not available".
            failIfReturnValueIsNotZero: false,
            cancellationToken: TestContext.CancellationToken);

        if (publishResult.ExitCode != 0)
        {
            Assert.Inconclusive(
                "Skipping wasm execution: 'dotnet publish -r wasi-wasm' failed (the 'wasm-tools' " +
                $"workload is likely not installed).{Environment.NewLine}{publishResult}");
            return null;
        }

        string appBundle = Path.Combine(
            generator.TargetAssetPath, "bin", "Release", TargetFramework, WasiRid, "AppBundle");
        Assert.IsTrue(
            Directory.Exists(appBundle),
            $"Expected the wasi AppBundle directory at '{appBundle}'.");

        StageIcuData(appBundle);
        return appBundle;
    }

    // Invoke wasmtime the way MTP-on-wasi is documented to run:
    //   wasmtime run -S http --dir . -- dotnet.wasm <AppName> <mtp-args...>
    // '-S http' is required because the runtime imports wasi:http; '--dir .' grants filesystem access
    // to the bundle so the platform can read/write its files.
    private async Task<(int ExitCode, string Output, string Error, string Combined)> RunUnderWasmtimeAsync(
        string wasmtime, string appBundle, string appName)
    {
        var commandLine = new TestInfrastructure.CommandLine();
        int exitCode = await commandLine.RunAsyncAndReturnExitCodeAsync(
            $"\"{wasmtime}\" run -S http --dir . -- dotnet.wasm {appName}",
            workingDirectory: appBundle,
            cancellationToken: TestContext.CancellationToken);

        string output = commandLine.StandardOutput;
        string error = commandLine.ErrorOutput;
        string combined = $"STDOUT:{Environment.NewLine}{output}{Environment.NewLine}STDERR:{Environment.NewLine}{error}";
        return (exitCode, output, error, combined);
    }

    private static string? LocateWasmtime()
    {
        if (Environment.GetEnvironmentVariable("WASMTIME_EXE") is { Length: > 0 } fromEnv && File.Exists(fromEnv))
        {
            return fromEnv;
        }

        string exeName = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "wasmtime.exe" : "wasmtime";
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

    // The pre-built dotnet.wasm does not embed the ICU data file, so the runtime loads icudt.dat from
    // disk. Stage it next to the bundle when it is not already present.
    private static void StageIcuData(string appBundle)
    {
        if (File.Exists(Path.Combine(appBundle, "icudt.dat")))
        {
            return;
        }

        string runtimePacksRoot = Path.Combine(RootFinder.Find(), ".dotnet", "packs", "Microsoft.NETCore.App.Runtime.Mono.wasi-wasm");
        if (!Directory.Exists(runtimePacksRoot))
        {
            return;
        }

        string? icu = Directory
            .GetFiles(runtimePacksRoot, "icudt.dat", SearchOption.AllDirectories)
            .FirstOrDefault();
        if (icu is not null)
        {
            File.Copy(icu, Path.Combine(appBundle, "icudt.dat"), overwrite: true);
        }
    }
}
