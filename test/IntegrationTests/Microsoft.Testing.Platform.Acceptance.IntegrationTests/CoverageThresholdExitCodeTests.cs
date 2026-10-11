// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Helpers;

namespace Microsoft.Testing.Platform.Acceptance.IntegrationTests;

/// <summary>
/// Exercises the in-process (<c>ConsoleTestHost</c>) coverage-threshold exit-code policy end to end.
/// </summary>
[TestClass]
public sealed class CoverageThresholdExitCodeTests : AcceptanceTestBase<CoverageThresholdExitCodeTests.TestAssetFixture>
{
    private const string AssetName = "CoverageThresholdExitCode";

    [TestMethod]
    [DataRow("--coverage-threshold-line 80 --coverage-threshold-branch 70", "normal", (int)ExitCode.Success, "Total - Branch: 70.0% >= 70.0% threshold")]
    [DataRow("--coverage-threshold-line 80", "below", (int)ExitCode.CoverageThresholdFailed, "Total - Line: 79.0% < 80.0% threshold")]
    [DataRow("--coverage-threshold-line 80", "boundary", (int)ExitCode.CoverageThresholdFailed, "Coverage threshold for '--coverage-threshold-line' failed:")]
    [DataRow("--coverage-threshold-line 79.995", "boundary", (int)ExitCode.Success, "Coverage Threshold Results:")]
    [DataRow("--coverage-threshold-branch 71", "normal", (int)ExitCode.CoverageThresholdFailed, "Total - Branch: 70.0% < 71.0% threshold")]
    [DataRow("--coverage-threshold-branch 0", "line-only", (int)ExitCode.CoverageThresholdFailed, "requires an overall coverage measurement")]
    [DataRow("--coverage-threshold-line 0", "empty", (int)ExitCode.CoverageThresholdFailed, "requires non-empty overall coverage data")]
    [DataRow("--coverage-threshold-line 80", "missing", (int)ExitCode.CoverageThresholdFailed, "Enable a compatible coverage collector")]
    [DataRow("--coverage-threshold-line 80", "module", (int)ExitCode.CoverageThresholdFailed, "requires an overall coverage measurement")]
    [DataRow("--coverage-threshold-line 80", "ambiguous", (int)ExitCode.CoverageThresholdFailed, "multiple coverage producers")]
    [DataRow("--coverage-threshold-line 80 --ignore-exit-code 14", "below", (int)ExitCode.Success, "Coverage Threshold Results:")]
    [DataRow("--coverage-threshold-line 80 --minimum-expected-tests 2", "below", (int)ExitCode.MinimumExpectedTestsPolicyViolation, "Coverage Threshold Results:")]
    [DataRow("--coverage-threshold-line 101", "normal", (int)ExitCode.InvalidCommandLine, "expects a percentage from 0 to 100")]
    [DataRow("--coverage-threshold-branch NaN", "normal", (int)ExitCode.InvalidCommandLine, "expects a percentage from 0 to 100")]
    [DataRow("--coverage-threshold-line 80 --list-tests", "normal", (int)ExitCode.InvalidCommandLine, "Coverage thresholds cannot be combined")]
    [DataRow("--coverage-threshold-line 49.92", "exact-boundary", (int)ExitCode.Success, "Total - Line: 49.9% >= 49.9% threshold")]
    public async Task ConfiguredThreshold_UsesOverallMeasurements(string command, string mode, int expectedExitCode, string expectedOutput)
    {
        var testHost = TestInfrastructure.TestHost.LocateFrom(AssetFixture.TargetAssetPath, AssetName, TargetFrameworks.NetCurrent);
        TestHostResult result = await testHost.ExecuteAsync(
            command,
            environmentVariables: new Dictionary<string, string?> { ["COVERAGE_MEASUREMENTS"] = mode },
            cancellationToken: TestContext.CancellationToken);

        result.AssertExitCodeIs((ExitCode)expectedExitCode);
        result.AssertOutputContains(expectedOutput);
    }

    [DynamicData(nameof(TargetFrameworks.AllForDynamicData), typeof(TargetFrameworks))]
    [TestMethod]
    public async Task ConfiguredThreshold_WithFailingTest_PreservesTestFailure(string currentTfm)
    {
        var testHost = TestInfrastructure.TestHost.LocateFrom(AssetFixture.TargetAssetPath, AssetName, currentTfm);
        TestHostResult result = await testHost.ExecuteAsync(
            "--coverage-threshold-line 80",
            environmentVariables: new Dictionary<string, string?> { ["COVERAGE_MEASUREMENTS"] = "below", ["FAIL_TEST"] = "1" },
            cancellationToken: TestContext.CancellationToken);

        result.AssertExitCodeIs(ExitCode.AtLeastOneTestFailed);
        result.AssertOutputContains("Total - Line: 79.0% < 80.0% threshold");
    }

    [TestMethod]
    public async Task ConfiguredThreshold_WithCancellation_PreservesAbort()
    {
        var testHost = TestInfrastructure.TestHost.LocateFrom(AssetFixture.TargetAssetPath, AssetName, TargetFrameworks.NetCurrent);
        TestHostResult result = await testHost.ExecuteAsync(
            "--coverage-threshold-line 80 --timeout 500ms",
            environmentVariables: new Dictionary<string, string?> { ["DELAY_TEST"] = "1" },
            cancellationToken: TestContext.CancellationToken);

        result.AssertExitCodeIs(ExitCode.TestSessionAborted);
    }

    [TestMethod]
    public async Task ConfiguredThreshold_WithNoTests_PreservesZeroTests()
    {
        var testHost = TestInfrastructure.TestHost.LocateFrom(AssetFixture.TargetAssetPath, AssetName, TargetFrameworks.NetCurrent);
        TestHostResult result = await testHost.ExecuteAsync(
            "--coverage-threshold-line 80",
            environmentVariables: new Dictionary<string, string?> { ["SKIP_TEST"] = "1" },
            cancellationToken: TestContext.CancellationToken);

        result.AssertExitCodeIs(ExitCode.ZeroTests);
    }

    [DynamicData(nameof(TargetFrameworks.AllForDynamicData), typeof(TargetFrameworks))]
    [TestMethod]
    public async Task FailedThreshold_WithPassingTests_ReturnsCoverageThresholdFailedExitCode(string currentTfm)
    {
        TestHostResult testHostResult = await ExecuteAsync(currentTfm, thresholdStatus: "Failed", failTest: false);

        testHostResult.AssertExitCodeIs(ExitCode.CoverageThresholdFailed);
        testHostResult.AssertOutputContains("Coverage Threshold Results:");
        testHostResult.AssertOutputContains("Total - Line (Minimum over Module): 70.0% < 80.0% threshold");
    }

    [DynamicData(nameof(TargetFrameworks.AllForDynamicData), typeof(TargetFrameworks))]
    [TestMethod]
    public async Task PassedThreshold_WithPassingTests_ReturnsSuccess(string currentTfm)
    {
        TestHostResult testHostResult = await ExecuteAsync(currentTfm, thresholdStatus: "Passed", failTest: false);

        testHostResult.AssertExitCodeIs(ExitCode.Success);
        testHostResult.AssertOutputContains("Total - Line (Minimum over Module): 90.0% >= 80.0% threshold");
    }

    [DynamicData(nameof(TargetFrameworks.AllForDynamicData), typeof(TargetFrameworks))]
    [TestMethod]
    public async Task FailedThreshold_WithFailingTest_RetainsOriginalNonSuccessExitCode(string currentTfm)
    {
        TestHostResult testHostResult = await ExecuteAsync(currentTfm, thresholdStatus: "Failed", failTest: true);

        testHostResult.AssertExitCodeIs(ExitCode.AtLeastOneTestFailed);
    }

    [DynamicData(nameof(TargetFrameworks.AllForDynamicData), typeof(TargetFrameworks))]
    [TestMethod]
    public async Task FailedThreshold_WithIgnoredFailingTest_ReturnsSuccess(string currentTfm)
    {
        var testHost = TestInfrastructure.TestHost.LocateFrom(AssetFixture.TargetAssetPath, AssetName, currentTfm);
        TestHostResult testHostResult = await testHost.ExecuteAsync(
            command: $"--ignore-exit-code {(int)ExitCode.AtLeastOneTestFailed}",
            environmentVariables: new Dictionary<string, string?>
            {
                ["COVERAGE_THRESHOLD_STATUS"] = "Failed",
                ["FAIL_TEST"] = "1",
            },
            cancellationToken: TestContext.CancellationToken);

        testHostResult.AssertExitCodeIs(ExitCode.Success);
    }

    private async Task<TestHostResult> ExecuteAsync(string currentTfm, string thresholdStatus, bool failTest)
    {
        var testHost = TestInfrastructure.TestHost.LocateFrom(AssetFixture.TargetAssetPath, AssetName, currentTfm);
        return await testHost.ExecuteAsync(
            environmentVariables: new Dictionary<string, string?>
            {
                ["COVERAGE_THRESHOLD_STATUS"] = thresholdStatus,
                ["FAIL_TEST"] = failTest ? "1" : "0",
            },
            cancellationToken: TestContext.CancellationToken);
    }

    public sealed class TestAssetFixture() : TestAssetFixtureBase()
    {
        private const string Sources = """
#file CoverageThresholdExitCode.csproj

<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFrameworks>$TargetFrameworks$</TargetFrameworks>
    <OutputType>Exe</OutputType>
    <UseAppHost>true</UseAppHost>
    <Nullable>enable</Nullable>
    <LangVersion>preview</LangVersion>
    <NoWarn>$(NoWarn);NETSDK1201</NoWarn>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.Testing.Platform" Version="$MicrosoftTestingPlatformVersion$" />
  </ItemGroup>
</Project>

#file Program.cs
using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Testing.Platform.Builder;
using Microsoft.Testing.Platform.Capabilities.TestFramework;
using Microsoft.Testing.Platform.Extensions;
using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Extensions.TestFramework;

public class Startup
{
    public static async Task<int> Main(string[] args)
    {
        var testApplicationBuilder = await TestApplication.CreateBuilderAsync(args);
        testApplicationBuilder.RegisterTestFramework(_ => new TestFrameworkCapabilities(), (_, __) => new DummyTestFramework());
        using ITestApplication app = await testApplicationBuilder.BuildAsync();
        return await app.RunAsync();
    }
}

public class DummyTestFramework : ITestFramework, IDataProducer
{
    public string Uid => nameof(DummyTestFramework);

    public string Version => "2.0.0";

    public string DisplayName => nameof(DummyTestFramework);

    public string Description => nameof(DummyTestFramework);

    public Task<bool> IsEnabledAsync() => Task.FromResult(true);

    public Type[] DataTypesProduced => new[] { typeof(TestNodeUpdateMessage), typeof(TestCoverageThresholdMessage), typeof(TestCoverageMessage) };

    public Task<CreateTestSessionResult> CreateTestSessionAsync(CreateTestSessionContext context)
        => Task.FromResult(new CreateTestSessionResult() { IsSuccess = true });

    public Task<CloseTestSessionResult> CloseTestSessionAsync(CloseTestSessionContext context)
        => Task.FromResult(new CloseTestSessionResult() { IsSuccess = true });

    public async Task ExecuteRequestAsync(ExecuteRequestContext context)
    {
        if (Environment.GetEnvironmentVariable("DELAY_TEST") == "1")
        {
            await Task.Delay(Timeout.Infinite, context.CancellationToken);
        }

        IProperty state = Environment.GetEnvironmentVariable("FAIL_TEST") == "1"
            ? new FailedTestNodeStateProperty()
            : new PassedTestNodeStateProperty();

        if (Environment.GetEnvironmentVariable("SKIP_TEST") != "1")
        {
            await context.MessageBus.PublishAsync(this, new TestNodeUpdateMessage(context.Request.Session.SessionUid, new TestNode()
            {
                Uid = "Test1",
                DisplayName = "Test1",
                Properties = new PropertyBag(state),
            }));
        }

        string? measurements = Environment.GetEnvironmentVariable("COVERAGE_MEASUREMENTS");
        if (measurements is not null and not "missing")
        {
            CoverageScope scope = measurements == "module" ? new CoverageScope(CoverageScopeLevel.Module, "app.dll") : CoverageScope.Overall;
            long covered = measurements switch { "empty" => 0, "below" => 79, "boundary" => 15999, "exact-boundary" => 312, _ => 80 };
            long coverable = measurements switch { "empty" => 0, "boundary" => 20000, "exact-boundary" => 625, _ => 100 };
            await context.MessageBus.PublishAsync(this, new TestCoverageMessage(context.Request.Session.SessionUid, scope, CoverageMetric.Line, covered, coverable, Uid));
            if (measurements != "line-only")
            {
                await context.MessageBus.PublishAsync(this, new TestCoverageMessage(context.Request.Session.SessionUid, scope, CoverageMetric.Branch, 70, 100, Uid));
            }

            if (measurements == "ambiguous")
            {
                await context.MessageBus.PublishAsync(this, new TestCoverageMessage(context.Request.Session.SessionUid, scope, CoverageMetric.Line, 100, 100, "other"));
            }
        }

        string? thresholdStatus = Environment.GetEnvironmentVariable("COVERAGE_THRESHOLD_STATUS");
        if (thresholdStatus is "Failed" or "Passed")
        {
            double actualPercentage = thresholdStatus == "Failed" ? 70.0 : 90.0;
            await context.MessageBus.PublishAsync(this, new TestCoverageThresholdMessage(
                context.Request.Session.SessionUid,
                CoverageScope.Overall,
                CoverageMetric.Line,
                CoverageAggregation.Minimum,
                actualPercentage,
                requiredPercentage: 80.0,
                hasCoverableData: true,
                producerId: nameof(DummyTestFramework),
                aggregatedOver: CoverageScopeLevel.Module));
        }

        context.Complete();
    }
}
""";

        public string TargetAssetPath => GetAssetPath(AssetName);

        public override (string ID, string Name, string Code) GetAssetsToGenerate() => (AssetName, AssetName,
                Sources
                .PatchTargetFrameworks(TargetFrameworks.All)
                .PatchCodeWithReplace("$MicrosoftTestingPlatformVersion$", MicrosoftTestingPlatformVersion));
    }

    public TestContext TestContext { get; set; }
}
