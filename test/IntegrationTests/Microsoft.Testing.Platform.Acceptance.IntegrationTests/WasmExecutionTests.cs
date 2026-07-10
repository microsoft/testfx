// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.Acceptance.IntegrationTests;

/// <summary>
/// End-to-end coverage for running MSTest tests in WebAssembly (issue
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
/// <para>Two complementary assertions:</para>
/// <list type="number">
///   <item>
///     <see cref="WasmBuild_GeneratesTestingPlatformEntryPoint"/> is an always-on build assertion.
///     It only relies on the <c>wasi-experimental</c> workload that the repo bootstrap installs, so
///     it runs on every CI leg and guards the build/entry-point plumbing against silent regressions.
///   </item>
///   <item>
///     <see cref="WasmExecution_RunsTestsUnderWasmtime"/> publishes a real MSTest project for
///     <c>wasi-wasm</c> and runs it end-to-end under <c>wasmtime</c>, asserting the passing tests
///     report success and the failing test is reported as failed. It is skipped (inconclusive) when
///     the wasm publish toolchain (<c>wasm-tools</c> workload) or <c>wasmtime</c> is not available —
///     for example on the default Windows CI matrix — so it only runs where the wasm runtime exists.
///   </item>
/// </list>
/// </summary>
[TestClass]
public sealed class WasmExecutionTests : AcceptanceTestBase<NopAssetFixture>
{
    // The wasm runtime that the mono wasi runtime pack targets. See samples/WasiPlayground.
    private const string WasiRid = "wasi-wasm";

    // wasi-wasm is only supported on the current .NET TFM in this repo (see samples/WasiPlayground).
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
        wasi-wasm build knobs, kept in sync with samples/WasiPlayground/WasiPlayground.csproj:
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
        using TestAsset generator = await GenerateWasmAssetAsync();

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
            Assert.Inconclusive(
                "Skipping wasm execution: 'wasmtime' was not found on PATH (nor via WASMTIME_EXE). " +
                "Install wasmtime and the 'wasm-tools' workload to exercise this test.");
            return;
        }

        using TestAsset generator = await GenerateWasmAssetAsync();

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
            return;
        }

        string appBundle = Path.Combine(
            generator.TargetAssetPath, "bin", "Release", TargetFramework, WasiRid, "AppBundle");
        Assert.IsTrue(
            Directory.Exists(appBundle),
            $"Expected the wasi AppBundle directory at '{appBundle}'.");

        StageIcuData(appBundle);

        // Invoke wasmtime the same way samples/WasiPlayground/README.md documents:
        //   wasmtime run -S http --dir . -- dotnet.wasm <AppName> <mtp-args...>
        // '-S http' is required because the runtime imports wasi:http; '--dir .' grants filesystem
        // access to the bundle so the platform can read/write its files.
        var commandLine = new TestInfrastructure.CommandLine();
        int exitCode = await commandLine.RunAsyncAndReturnExitCodeAsync(
            $"\"{wasmtime}\" run -S http --dir . -- dotnet.wasm WasmTestProject",
            workingDirectory: appBundle,
            cancellationToken: TestContext.CancellationToken);

        string output = commandLine.StandardOutput;
        string error = commandLine.ErrorOutput;
        string combined = $"STDOUT:{Environment.NewLine}{output}{Environment.NewLine}STDERR:{Environment.NewLine}{error}";

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

    private Task<TestAsset> GenerateWasmAssetAsync()
        => TestAsset.GenerateAssetAsync(
            "WasmTestProject",
            SourceCode
                .PatchCodeWithReplace("$TargetFramework$", TargetFramework)
                .PatchCodeWithReplace("$WasiRid$", WasiRid)
                .PatchCodeWithReplace("$MSTestVersion$", MSTestVersion));

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
    // disk. Stage it next to the bundle when it is not already present (see WasiPlayground README).
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
