// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Acceptance.IntegrationTests;
using Microsoft.Testing.Platform.Acceptance.IntegrationTests.Helpers;

namespace MSTest.Acceptance.IntegrationTests;

/// <summary>
/// End-to-end coverage for running MSTest tests in WebAssembly (issue
/// <see href="https://github.com/microsoft/testfx/issues/2196"/>) using <c>wasi-wasm</c> as a
/// headless proxy for <c>browser-wasm</c>: both share the same single-threaded execution model, so
/// getting <c>wasi-wasm</c> green covers the core work <c>browser-wasm</c> also needs. This is the
/// MSTest-adapter counterpart to the raw-platform
/// <c>Microsoft.Testing.Platform.Acceptance.IntegrationTests.WasmExecutionTests</c>.
///
/// <para>
/// Single-threaded wasm runtimes have no thread pool: <c>Task.Run</c> continuations never execute and
/// blocking waits throw <see cref="PlatformNotSupportedException"/>. The platform and the MSTest
/// adapter detect this (see <c>RuntimeFeatureHelper.IsMultiThreaded</c> /
/// <c>RuntimeContext.IsMultiThreaded</c>) and fall back to inline/synchronous execution for the few
/// thread-dependent spots on the run path (message-bus consumers, the shutdown watchdog, telemetry
/// ingest, the countdown-event wait, and the adapter's per-test task factory). The MSTest project
/// here runs on Microsoft.Testing.Platform via the generated entry point (no user-authored Main).
/// </para>
///
/// <para>Two complementary assertions:</para>
/// <list type="number">
///   <item>
///     <see cref="WasmBuild_GeneratesTestingPlatformEntryPoint"/> is an always-on build assertion. It
///     only relies on the <c>wasi-experimental</c> workload that the repo bootstrap installs, so it
///     runs on every CI leg and guards the build/entry-point plumbing against silent regressions.
///   </item>
///   <item>
///     <see cref="WasmExecution_RunsTestsUnderWasmtime"/> publishes a real MSTest project for
///     <c>wasi-wasm</c> and runs it end-to-end under <c>wasmtime</c>, asserting the passing tests
///     report success and the failing test is reported as failed. It is skipped (inconclusive) when
///     the wasm publish toolchain (<c>wasm-tools</c> workload) or <c>wasmtime</c> is not available, so
///     it only runs where the wasm runtime exists.
///   </item>
/// </list>
/// </summary>
[TestClass]
public sealed class WasmExecutionTests : AcceptanceTestBase<NopAssetFixture>
{
    // wasi-wasm is only supported on the current .NET TFM in this repo.
    private static readonly string TargetFramework = TargetFrameworks.NetCurrent;

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
        wasi-wasm build knobs, shared with the raw-platform asset in the platform acceptance tests:
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

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task WasmBuild_GeneratesTestingPlatformEntryPoint()
    {
        using TestAsset generator = await GenerateAssetAsync();

        DotnetMuxerResult buildResult = await DotnetCli.RunAsync(
            $"build {generator.TargetAssetPath} -f {TargetFramework} -r {WasmRuntime.WasiRid} -c Release",
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
        string? wasmtime = WasmRuntime.LocateWasmtime();
        if (wasmtime is null)
        {
            Assert.Inconclusive(WasmRuntime.WasmtimeUnavailableMessage);
            return;
        }

        using TestAsset generator = await GenerateAssetAsync();

        DotnetMuxerResult publishResult = await WasmRuntime.PublishForWasiAsync(
            generator.TargetAssetPath, TargetFramework, TestContext.CancellationToken);
        if (publishResult.ExitCode != 0)
        {
            Assert.Inconclusive(
                "Skipping wasm execution: 'dotnet publish -r wasi-wasm' failed (the 'wasm-tools' " +
                $"workload is likely not installed).{Environment.NewLine}{publishResult}");
            return;
        }

        string appBundle = WasmRuntime.GetAppBundlePath(generator.TargetAssetPath, TargetFramework);
        Assert.IsTrue(
            Directory.Exists(appBundle),
            $"Expected the wasi AppBundle directory at '{appBundle}'.");
        WasmRuntime.StageIcuData(appBundle);

        (int exitCode, string output, string error, string combined) =
            await WasmRuntime.RunUnderWasmtimeAsync(wasmtime, appBundle, "WasmTestProject", TestContext.CancellationToken);

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

    private Task<TestAsset> GenerateAssetAsync()
        => TestAsset.GenerateAssetAsync(
            "WasmTestProject",
            SourceCode
                .PatchCodeWithReplace("$TargetFramework$", TargetFramework)
                .PatchCodeWithReplace("$WasiRid$", WasmRuntime.WasiRid)
                .PatchCodeWithReplace("$MSTestVersion$", MSTestVersion));
}
