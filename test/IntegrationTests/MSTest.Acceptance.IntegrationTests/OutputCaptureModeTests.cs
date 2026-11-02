// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Acceptance.IntegrationTests;
using Microsoft.Testing.Platform.Acceptance.IntegrationTests.Helpers;
using Microsoft.Testing.Platform.Helpers;

namespace MSTest.Acceptance.IntegrationTests;

[TestClass]
public sealed class OutputCaptureModeTests : AcceptanceTestBase<OutputCaptureModeTests.TestAssetFixture>
{
    private const string AssetName = "OutputCaptureMode";

    public TestContext TestContext { get; set; } = null!;

    private const string ConsoleOutMarker = "CAPTUREMODE_CONSOLE_OUT";
    private const string TraceMarker = "CAPTUREMODE_TRACE";
    private const string TestContextWriteMarker = "CAPTUREMODE_TESTCONTEXT_WRITE_ONLY";
    private const string TestContextWriteLineMarker = "CAPTUREMODE_TESTCONTEXT_LINE_ONLY";
    private const string StaleTestContextMarker = "CAPTUREMODE_STALE_TESTCONTEXT";

    [TestMethod]
    [DynamicData(nameof(TargetFrameworks.AllForDynamicData), typeof(TargetFrameworks))]
    public async Task LiveMode_EchoesOutputLive_EvenForPassingTests(string currentTfm)
    {
        var testHost = TestHost.LocateFrom(AssetFixture.TargetAssetPath, AssetName, currentTfm);

        TestHostResult testHostResult = await testHost.ExecuteAsync(
            "--filter FullyQualifiedName~PassingTestWithOutput --settings capturelive.runsettings",
            cancellationToken: TestContext.CancellationToken);

        testHostResult.AssertExitCodeIs(ExitCode.Success);
        testHostResult.AssertOutputContainsSummary(failed: 0, passed: 1, skipped: 0);

        // In Live mode, Console and Trace output is echoed live to the console while the test runs, so it
        // shows up even though the test passed (a passing test's captured output is not otherwise printed).
        testHostResult.AssertOutputContains(ConsoleOutMarker);
        testHostResult.AssertOutputContains(TraceMarker);
        // Exact counts also guard against routing the live echo back through captured standard output.
        AssertMarkerOccursExactlyOnce(testHostResult, TestContextWriteMarker);
        AssertMarkerOccursExactlyOnce(testHostResult, TestContextWriteLineMarker);
    }

    [TestMethod]
    [DynamicData(nameof(TargetFrameworks.AllForDynamicData), typeof(TargetFrameworks))]
    public async Task LiveMode_DoesNotEchoOutputFromCompletedTestContext(string currentTfm)
    {
        var testHost = TestHost.LocateFrom(AssetFixture.TargetAssetPath, AssetName, currentTfm);

        TestHostResult testHostResult = await testHost.ExecuteAsync(
            "--filter FullyQualifiedName~PassingTestWithDelayedOutput --settings capturelive.runsettings",
            cancellationToken: TestContext.CancellationToken);

        testHostResult.AssertExitCodeIs(ExitCode.Success);
        testHostResult.AssertOutputContainsSummary(failed: 0, passed: 1, skipped: 0);
        testHostResult.AssertOutputDoesNotContain(StaleTestContextMarker);
    }

    [TestMethod]
    [DynamicData(nameof(TargetFrameworks.AllForDynamicData), typeof(TargetFrameworks))]
    public async Task ResultMode_DoesNotEchoLive_ForPassingTests(string currentTfm)
    {
        var testHost = TestHost.LocateFrom(AssetFixture.TargetAssetPath, AssetName, currentTfm);

        TestHostResult testHostResult = await testHost.ExecuteAsync(
            "--filter FullyQualifiedName~PassingTestWithOutput --settings captureresult.runsettings",
            cancellationToken: TestContext.CancellationToken);

        testHostResult.AssertExitCodeIs(ExitCode.Success);
        testHostResult.AssertOutputContainsSummary(failed: 0, passed: 1, skipped: 0);

        // In Result mode (the default), output is captured into the result but not streamed live, so a
        // passing test's output is not surfaced.
        testHostResult.AssertOutputDoesNotContain(ConsoleOutMarker);
        testHostResult.AssertOutputDoesNotContain(TestContextWriteMarker);
        testHostResult.AssertOutputDoesNotContain(TestContextWriteLineMarker);
    }

    [TestMethod]
    [DynamicData(nameof(TargetFrameworks.AllForDynamicData), typeof(TargetFrameworks))]
    public async Task ResultMode_SurfacesCapturedOutput_OnFailingTests(string currentTfm)
    {
        var testHost = TestHost.LocateFrom(AssetFixture.TargetAssetPath, AssetName, currentTfm);

        TestHostResult testHostResult = await testHost.ExecuteAsync(
            "--filter FullyQualifiedName~FailingTestWithOutput --settings captureresult.runsettings",
            cancellationToken: TestContext.CancellationToken);

        testHostResult.AssertExitCodeIs(ExitCode.AtLeastOneTestFailed);
        testHostResult.AssertOutputContainsSummary(failed: 1, passed: 0, skipped: 0);

        // A failed test surfaces captured output in the failure block, proving that TestContext live echo remains
        // additive and does not remove the messages from the completed result.
        testHostResult.AssertOutputContains(ConsoleOutMarker);
        AssertMarkerOccursExactlyOnce(testHostResult, TestContextWriteMarker);
        AssertMarkerOccursExactlyOnce(testHostResult, TestContextWriteLineMarker);
    }

    [TestMethod]
    [DynamicData(nameof(TargetFrameworks.AllForDynamicData), typeof(TargetFrameworks))]
    public async Task NoneMode_DoesNotCapture_OutputFlowsThroughLive(string currentTfm)
    {
        var testHost = TestHost.LocateFrom(AssetFixture.TargetAssetPath, AssetName, currentTfm);

        TestHostResult testHostResult = await testHost.ExecuteAsync(
            "--filter FullyQualifiedName~PassingTestWithOutput --settings capturenone.runsettings",
            cancellationToken: TestContext.CancellationToken);

        testHostResult.AssertExitCodeIs(ExitCode.Success);
        testHostResult.AssertOutputContainsSummary(failed: 0, passed: 1, skipped: 0);

        // In None mode nothing is captured, so Console output is not intercepted and flows straight to the
        // console as the test runs (even for a passing test). TestContext output keeps its legacy result-only
        // behavior and is not echoed live.
        testHostResult.AssertOutputContains(ConsoleOutMarker);
        testHostResult.AssertOutputDoesNotContain(TestContextWriteMarker);
        testHostResult.AssertOutputDoesNotContain(TestContextWriteLineMarker);
    }

    [TestMethod]
    [DynamicData(nameof(TargetFrameworks.AllForDynamicData), typeof(TargetFrameworks))]
    public async Task LegacyBooleanTrue_BehavesLikeResult(string currentTfm)
    {
        var testHost = TestHost.LocateFrom(AssetFixture.TargetAssetPath, AssetName, currentTfm);

        TestHostResult testHostResult = await testHost.ExecuteAsync(
            "--filter FullyQualifiedName~PassingTestWithOutput --settings capturelegacytrue.runsettings",
            cancellationToken: TestContext.CancellationToken);

        testHostResult.AssertExitCodeIs(ExitCode.Success);
        testHostResult.AssertOutputContainsSummary(failed: 0, passed: 1, skipped: 0);

        // The legacy boolean true maps to Result: captured but not streamed for a passing test.
        testHostResult.AssertOutputDoesNotContain(ConsoleOutMarker);
    }

    [TestMethod]
    [DynamicData(nameof(TargetFrameworks.AllForDynamicData), typeof(TargetFrameworks))]
    public async Task LegacyBooleanFalse_BehavesLikeNone(string currentTfm)
    {
        var testHost = TestHost.LocateFrom(AssetFixture.TargetAssetPath, AssetName, currentTfm);

        TestHostResult testHostResult = await testHost.ExecuteAsync(
            "--filter FullyQualifiedName~PassingTestWithOutput --settings capturelegacyfalse.runsettings",
            cancellationToken: TestContext.CancellationToken);

        testHostResult.AssertExitCodeIs(ExitCode.Success);
        testHostResult.AssertOutputContainsSummary(failed: 0, passed: 1, skipped: 0);

        // The legacy boolean false maps to None: nothing captured, so output flows straight to the console.
        testHostResult.AssertOutputContains(ConsoleOutMarker);
        testHostResult.AssertOutputDoesNotContain(TestContextWriteMarker);
        testHostResult.AssertOutputDoesNotContain(TestContextWriteLineMarker);
    }

    public sealed class TestAssetFixture() : TestAssetFixtureBase()
    {
        public string TargetAssetPath => GetAssetPath(AssetName);

        public override (string ID, string Name, string Code) GetAssetsToGenerate() => (AssetName, AssetName,
                SourceCode
                .PatchTargetFrameworks(TargetFrameworks.All)
                .PatchCodeWithReplace("$MSTestVersion$", MSTestVersion));

        private const string SourceCode = """
#file OutputCaptureMode.csproj
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <EnableMSTestRunner>true</EnableMSTestRunner>
    <TargetFrameworks>$TargetFrameworks$</TargetFrameworks>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="MSTest.TestAdapter" Version="$MSTestVersion$" />
    <PackageReference Include="MSTest.TestFramework" Version="$MSTestVersion$" />
  </ItemGroup>

  <ItemGroup>
    <None Update="*.runsettings">
      <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
    </None>
  </ItemGroup>

</Project>

#file capturenone.runsettings
<?xml version="1.0" encoding="utf-8"?>
<RunSettings>
  <MSTest>
    <CaptureTraceOutput>None</CaptureTraceOutput>
  </MSTest>
</RunSettings>

#file captureresult.runsettings
<?xml version="1.0" encoding="utf-8"?>
<RunSettings>
  <MSTest>
    <CaptureTraceOutput>Result</CaptureTraceOutput>
  </MSTest>
</RunSettings>

#file capturelive.runsettings
<?xml version="1.0" encoding="utf-8"?>
<RunSettings>
  <MSTest>
    <CaptureTraceOutput>Live</CaptureTraceOutput>
  </MSTest>
</RunSettings>

#file capturelegacyfalse.runsettings
<?xml version="1.0" encoding="utf-8"?>
<RunSettings>
  <MSTest>
    <CaptureTraceOutput>false</CaptureTraceOutput>
  </MSTest>
</RunSettings>

#file capturelegacytrue.runsettings
<?xml version="1.0" encoding="utf-8"?>
<RunSettings>
  <MSTest>
    <CaptureTraceOutput>true</CaptureTraceOutput>
  </MSTest>
</RunSettings>

#file UnitTest1.cs
using System;
using System.Diagnostics;
using System.Threading.Tasks;

using Microsoft.VisualStudio.TestTools.UnitTesting;

[TestClass]
public class UnitTest1
{
    private static readonly TaskCompletionSource<object> StaleWriteRelease = new TaskCompletionSource<object>();
    private static Task StaleWriteTask;

    public TestContext TestContext { get; set; }

    [TestMethod]
    public void PassingTestWithOutput()
    {
        Console.WriteLine("CAPTUREMODE_CONSOLE_OUT");
        Console.Error.WriteLine("CAPTUREMODE_CONSOLE_ERR");
        Trace.WriteLine("CAPTUREMODE_TRACE");
        TestContext.Write("CAPTUREMODE_TESTCONTEXT_WRITE_ONLY");
        TestContext.WriteLine("CAPTUREMODE_TESTCONTEXT_LINE_ONLY");
    }

    [TestMethod]
    public void FailingTestWithOutput()
    {
        Console.WriteLine("CAPTUREMODE_CONSOLE_OUT");
        Console.Error.WriteLine("CAPTUREMODE_CONSOLE_ERR");
        Trace.WriteLine("CAPTUREMODE_TRACE");
        TestContext.Write("CAPTUREMODE_TESTCONTEXT_WRITE_ONLY");
        TestContext.WriteLine("CAPTUREMODE_TESTCONTEXT_LINE_ONLY");
        Assert.Fail("BOOM");
    }

    [TestMethod]
    public void PassingTestWithDelayedOutput()
    {
        TestContext capturedContext = TestContext;
        StaleWriteTask = Task.Run(async () =>
        {
            await StaleWriteRelease.Task;
            capturedContext.WriteLine("CAPTUREMODE_STALE_TESTCONTEXT");
        });
    }

    [ClassCleanup]
    public static async Task ReleaseDelayedOutput()
    {
        if (StaleWriteTask != null)
        {
            StaleWriteRelease.SetResult(null);
            await StaleWriteTask;
        }
    }
}
""";
    }

    private static void AssertMarkerOccursExactlyOnce(TestHostResult testHostResult, string marker)
        => Assert.HasCount(1, Regex.Matches(testHostResult.StandardOutput, Regex.Escape(marker)), testHostResult.ToString());
}
