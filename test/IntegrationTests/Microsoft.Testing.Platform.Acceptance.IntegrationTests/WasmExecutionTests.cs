// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.Acceptance.IntegrationTests;

/// <summary>
/// End-to-end coverage for hosting the raw Microsoft.Testing.Platform (no MSTest adapter) in
/// WebAssembly (issue <see href="https://github.com/microsoft/testfx/issues/2196"/>) using
/// <c>wasi-wasm</c> as a headless proxy for <c>browser-wasm</c>: both share the same single-threaded
/// execution model, so getting <c>wasi-wasm</c> green covers the core platform work
/// <c>browser-wasm</c> also needs. The MSTest-adapter counterpart lives in
/// <c>MSTest.Acceptance.IntegrationTests.WasmExecutionTests</c>.
///
/// <para>
/// Single-threaded wasm runtimes have no thread pool: <c>Task.Run</c> continuations never execute
/// and blocking waits throw <see cref="PlatformNotSupportedException"/>. The platform detects this
/// (see <c>RuntimeFeatureHelper.IsMultiThreaded</c>) and falls back to inline/synchronous execution
/// for the few thread-dependent spots on the run path (message-bus consumers, the shutdown watchdog,
/// telemetry ingest, and the countdown-event wait). A user-authored <c>async Task Main</c> is fine as
/// long as the pipeline completes without hopping to a background thread, which is exactly what these
/// fallbacks guarantee.
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
/// <para>Two complementary assertions:</para>
/// <list type="number">
///   <item>
///     <see cref="RawPlatform_BuildsForWasi"/> is an always-on build assertion. It only relies on
///     the <c>wasi-experimental</c> workload that the repo bootstrap installs, so it runs on every CI
///     leg and guards the build plumbing against silent regressions.
///   </item>
///   <item>
///     <see cref="RawPlatform_RunsUnderWasmtime"/> publishes the app for <c>wasi-wasm</c> and runs it
///     end-to-end under <c>wasmtime</c>, asserting the single passing test reports success. It is
///     skipped (inconclusive) when the wasm publish toolchain (<c>wasm-tools</c> workload) or
///     <c>wasmtime</c> is not available, so it only runs where the wasm runtime exists.
///   </item>
/// </list>
/// </summary>
[TestClass]
public sealed class WasmExecutionTests : AcceptanceTestBase<NopAssetFixture>
{
    // wasi-wasm is only supported on the current .NET TFM in this repo.
    private static readonly string TargetFramework = TargetFrameworks.NetCurrent;

    // Minimal project hosting Microsoft.Testing.Platform directly (no MSTest adapter): a user-authored
    // async Main registers a DummyFramework that publishes a single passing test. This mirrors the
    // former samples/WasiPlayground.
    private const string SourceCode = """
#file WasmPlatformProject.csproj
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>$TargetFramework$</TargetFramework>
    <RuntimeIdentifier>$WasiRid$</RuntimeIdentifier>
    <OutputType>Exe</OutputType>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>

    <!--
        wasi-wasm build knobs, shared with the MSTest asset in the MSTest acceptance tests:
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

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task RawPlatform_BuildsForWasi()
    {
        using TestAsset generator = await GenerateAssetAsync();

        DotnetMuxerResult buildResult = await DotnetCli.RunAsync(
            $"build {generator.TargetAssetPath} -f {TargetFramework} -r {WasmRuntime.WasiRid} -c Release",
            // Trimming/wasm builds can emit non-actionable warnings; we only assert on the build
            // succeeding, not on a warning-clean build.
            warnAsError: false,
            cancellationToken: TestContext.CancellationToken);

        // A clean build proves the Microsoft.Testing.Platform package restores and compiles against
        // wasi-wasm — the plumbing that browser-wasm/wasi-wasm execution ultimately relies on.
        buildResult.AssertExitCodeIs(0);

        // Also assert the compiled output assembly exists to guard against a silent no-op build (the
        // wasm bundle / dotnet.wasm itself is only produced on publish, exercised by
        // RawPlatform_RunsUnderWasmtime). This mirrors the artifact check the MSTest counterpart
        // performs on its generated entry point.
        string[] outputAssemblies = Directory.GetFiles(
            Path.Combine(generator.TargetAssetPath, "bin"),
            "WasmPlatformProject.dll",
            SearchOption.AllDirectories);
        Assert.IsNotEmpty(
            outputAssemblies,
            $"Expected a compiled 'WasmPlatformProject.dll' under '{Path.Combine(generator.TargetAssetPath, "bin")}'.");
    }

    [TestMethod]
    public async Task RawPlatform_RunsUnderWasmtime()
    {
        using TestAsset generator = await GenerateAssetAsync();

        // Publish first so the publish path is exercised in CI (where the 'wasm-tools' workload is
        // installed but 'wasmtime' is not). Only a missing 'wasm-tools' workload is an acceptable
        // skip; any other publish failure is a real regression and must fail the test.
        DotnetMuxerResult publishResult = await WasmRuntime.PublishForWasiAsync(
            generator.TargetAssetPath, TargetFramework, TestContext.CancellationToken);
        if (publishResult.ExitCode != 0)
        {
            Assert.IsTrue(
                WasmRuntime.IsMissingWasmToolsWorkload(publishResult),
                $"'dotnet publish -r wasi-wasm' failed for an unexpected reason (not a missing 'wasm-tools' workload).{Environment.NewLine}{publishResult}");
            Assert.Inconclusive(
                $"Skipping wasm execution: the 'wasm-tools' workload is not installed.{Environment.NewLine}{publishResult}");
            return;
        }

        string appBundle = WasmRuntime.GetAppBundlePath(generator.TargetAssetPath, TargetFramework);
        Assert.IsTrue(
            Directory.Exists(appBundle),
            $"Expected the wasi AppBundle directory at '{appBundle}'.");
        WasmRuntime.StageIcuData(appBundle);

        // Publishing is covered above; only the runtime invocation is gated on 'wasmtime'.
        string? wasmtime = WasmRuntime.LocateWasmtime();
        if (wasmtime is null)
        {
            Assert.Inconclusive(WasmRuntime.WasmtimeUnavailableMessage);
            return;
        }

        (int exitCode, _, _, string combined) =
            await WasmRuntime.RunUnderWasmtimeAsync(wasmtime, appBundle, "WasmPlatformProject", TestContext.CancellationToken);

        // No test fails, so the platform must exit cleanly. A PlatformNotSupportedException (the
        // single-threaded blocking-wait failure mode) would indicate a regression in the wasm
        // fallbacks. Check the combined STDOUT+STDERR so we catch it regardless of the stream it
        // surfaces on.
        Assert.IsFalse(
            combined.Contains("PlatformNotSupportedException", StringComparison.Ordinal),
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

    private Task<TestAsset> GenerateAssetAsync()
        => TestAsset.GenerateAssetAsync(
            "WasmPlatformProject",
            SourceCode
                .PatchCodeWithReplace("$TargetFramework$", TargetFramework)
                .PatchCodeWithReplace("$WasiRid$", WasmRuntime.WasiRid)
                .PatchCodeWithReplace("$MicrosoftTestingPlatformVersion$", MicrosoftTestingPlatformVersion));
}
