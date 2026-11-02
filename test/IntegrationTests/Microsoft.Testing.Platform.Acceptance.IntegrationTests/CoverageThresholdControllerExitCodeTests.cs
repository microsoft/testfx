// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Helpers;

namespace Microsoft.Testing.Platform.Acceptance.IntegrationTests;

/// <summary>
/// Exercises the out-of-process (<c>TestHostControllersTestHost</c>) coverage-threshold exit-code override
/// end to end. A coverage threshold message is published by an <c>ITestHostProcessLifetimeHandler</c>
/// running in the controller process (a separate code path from the in-process <c>ConsoleTestHost</c>):
/// the controller registers the coverage-result consumer, drains the message queue after the test host
/// exits, and turns an otherwise-successful run into <see cref="ExitCode.CoverageThresholdFailed"/> (14)
/// when a threshold failed, while a passed threshold leaves the run successful.
/// </summary>
[TestClass]
public sealed class CoverageThresholdControllerExitCodeTests : AcceptanceTestBase<CoverageThresholdControllerExitCodeTests.TestAssetFixture>
{
    private const string AssetName = "CoverageThresholdControllerExitCode";

    [TestMethod]
    public async Task ConfiguredThreshold_AzureSummaryOutsidePipelines_DoesNotInvokeDisabledProcessor()
    {
        using TempDirectory results = new();
        string summaryPath = Path.Combine(results.Path, "summary.md");
        var testHost = TestInfrastructure.TestHost.LocateFrom(AssetFixture.TargetAssetPath, AssetName, TargetFrameworks.NetCurrent);

        TestHostResult result = await testHost.ExecuteAsync(
            $"--coverage-threshold-line 81 --report-azdo --report-azdo-summary \"{summaryPath}\" --results-directory \"{results.Path}\"",
            environmentVariables: new Dictionary<string, string?>
            {
                ["COVERAGE_MEASUREMENTS"] = "normal",
                ["CI_SUMMARY_PROVIDER"] = "azure",
                ["TF_BUILD"] = null,
            },
            cancellationToken: TestContext.CancellationToken);

        result.AssertExitCodeIs(ExitCode.CoverageThresholdFailed);
        result.AssertOutputContains("TF_BUILD is not set to 'true'; skipping summary emission.");
        Assert.IsFalse(File.Exists(summaryPath));
        Assert.IsFalse(Directory.Exists(Path.Combine(results.Path, ".ci-summary-fragments")));
    }

    [TestMethod]
    [DataRow("github", "80", (int)ExitCode.Success)]
    [DataRow("github", "81", (int)ExitCode.CoverageThresholdFailed)]
    [DataRow("azure", "80", (int)ExitCode.Success)]
    [DataRow("azure", "81", (int)ExitCode.CoverageThresholdFailed)]
    public async Task ConfiguredThreshold_InController_ReachesCiSummary(string provider, string threshold, int expectedExitCode)
    {
        using TempDirectory results = new();
        string summaryPath = Path.Combine(results.Path, "summary.md");
        var testHost = TestInfrastructure.TestHost.LocateFrom(AssetFixture.TargetAssetPath, AssetName, TargetFrameworks.NetCurrent);
        string reportingOptions = provider == "github" ? "--report-gh" : $"--report-azdo --report-azdo-summary \"{summaryPath}\"";

        TestHostResult result = await testHost.ExecuteAsync(
            $"--coverage-threshold-line {threshold} {reportingOptions} --results-directory \"{results.Path}\"",
            environmentVariables: new Dictionary<string, string?>
            {
                ["COVERAGE_MEASUREMENTS"] = "normal",
                ["CI_SUMMARY_PROVIDER"] = provider,
                ["GITHUB_ACTIONS"] = provider == "github" ? "true" : null,
                ["GITHUB_STEP_SUMMARY"] = summaryPath,
                ["TF_BUILD"] = provider == "azure" ? "true" : null,
            },
            cancellationToken: TestContext.CancellationToken);

        result.AssertExitCodeIs((ExitCode)expectedExitCode);
        Assert.IsTrue(File.Exists(summaryPath), result.ToString());
        string summary = File.ReadAllText(summaryPath).ReplaceLineEndings("\n");
        string verdict = expectedExitCode == 0 ? "✅ Passed" : "❌ Failed";
        Assert.Contains(
            $"""
            | Scope | Metric | Actual | Required | Result |
            | --- | --- | ---: | ---: | --- |
            | {AssetName} ({TargetFrameworks.NetCurrent}) — Overall | Line | 80.0% | {threshold}.0% | {verdict} |
            """.ReplaceLineEndings("\n"),
            summary);
    }

    [TestMethod]
    [DataRow("--coverage-threshold-line 80 --coverage-threshold-branch 70", "normal", false, (int)ExitCode.Success)]
    [DataRow("--coverage-threshold-line 81", "normal", false, (int)ExitCode.CoverageThresholdFailed)]
    [DataRow("--coverage-threshold-branch 71", "normal", false, (int)ExitCode.CoverageThresholdFailed)]
    [DataRow("--coverage-threshold-line 80", "missing", false, (int)ExitCode.CoverageThresholdFailed)]
    [DataRow("--coverage-threshold-line 0", "empty", false, (int)ExitCode.CoverageThresholdFailed)]
    [DataRow("--coverage-threshold-line 81", "normal", true, (int)ExitCode.AtLeastOneTestFailed)]
    [DataRow("--coverage-threshold-line 81 --ignore-exit-code 14", "normal", false, (int)ExitCode.Success)]
    [DataRow("--coverage-threshold-line 81 --ignore-exit-code 2", "normal", true, (int)ExitCode.Success)]
    [DataRow("--coverage-threshold-line 80", "testhost", false, (int)ExitCode.Success)]
    public async Task ConfiguredThreshold_WaitsForControllerCollection(string command, string mode, bool failTest, int expectedExitCode)
    {
        var testHost = TestInfrastructure.TestHost.LocateFrom(AssetFixture.TargetAssetPath, AssetName, TargetFrameworks.NetCurrent);
        TestHostResult result = await testHost.ExecuteAsync(
            command,
            environmentVariables: new Dictionary<string, string?>
            {
                ["COVERAGE_MEASUREMENTS"] = mode,
                ["FAIL_TEST"] = failTest ? "1" : "0",
            },
            cancellationToken: TestContext.CancellationToken);

        result.AssertExitCodeIs((ExitCode)expectedExitCode);
        result.AssertOutputContains("Coverage Threshold Results:");
        if (mode != "missing")
        {
            Assert.DoesNotContain("Enable a compatible coverage collector", result.StandardOutput);
        }
    }

    [DynamicData(nameof(TargetFrameworks.AllForDynamicData), typeof(TargetFrameworks))]
    [TestMethod]
    public async Task FailedThreshold_InController_ReturnsCoverageThresholdFailedExitCode(string currentTfm)
    {
        var testHost = TestInfrastructure.TestHost.LocateFrom(AssetFixture.TargetAssetPath, AssetName, currentTfm);
        TestHostResult testHostResult = await testHost.ExecuteAsync(
            environmentVariables: new Dictionary<string, string?>
            {
                ["COVERAGE_THRESHOLD_STATUS"] = "Failed",
            },
            cancellationToken: TestContext.CancellationToken);

        testHostResult.AssertExitCodeIs(ExitCode.CoverageThresholdFailed);
        testHostResult.AssertOutputContains("Coverage Threshold Results:");
        testHostResult.AssertOutputContains("Total - Line (Minimum over Module): 70.0% < 80.0% threshold");
    }

    [DynamicData(nameof(TargetFrameworks.AllForDynamicData), typeof(TargetFrameworks))]
    [TestMethod]
    public async Task PassedThreshold_InController_ReturnsSuccess(string currentTfm)
    {
        var testHost = TestInfrastructure.TestHost.LocateFrom(AssetFixture.TargetAssetPath, AssetName, currentTfm);
        TestHostResult testHostResult = await testHost.ExecuteAsync(
            environmentVariables: new Dictionary<string, string?>
            {
                ["COVERAGE_THRESHOLD_STATUS"] = "Passed",
            },
            cancellationToken: TestContext.CancellationToken);

        testHostResult.AssertExitCodeIs(ExitCode.Success);
        testHostResult.AssertOutputContains("Coverage Threshold Results:");
        testHostResult.AssertOutputContains("Total - Line (Minimum over Module): 90.0% >= 80.0% threshold");
    }

    [DynamicData(nameof(TargetFrameworks.AllForDynamicData), typeof(TargetFrameworks))]
    [TestMethod]
    public async Task FailedThreshold_InController_WithIgnoreExitCode_ReturnsSuccess(string currentTfm)
    {
        var testHost = TestInfrastructure.TestHost.LocateFrom(AssetFixture.TargetAssetPath, AssetName, currentTfm);
        TestHostResult testHostResult = await testHost.ExecuteAsync(
            command: $"--ignore-exit-code {(int)ExitCode.CoverageThresholdFailed}",
            environmentVariables: new Dictionary<string, string?>
            {
                ["COVERAGE_THRESHOLD_STATUS"] = "Failed",
            },
            cancellationToken: TestContext.CancellationToken);

        // The controller-side coverage-threshold verdict must also honor '--ignore-exit-code 14'.
        testHostResult.AssertExitCodeIs(ExitCode.Success);
        testHostResult.AssertOutputContains("Coverage Threshold Results:");
        testHostResult.AssertOutputContains("Total - Line (Minimum over Module): 70.0% < 80.0% threshold");
    }

    [DynamicData(nameof(TargetFrameworks.AllForDynamicData), typeof(TargetFrameworks))]
    [TestMethod]
    public async Task FailedThreshold_InController_WithIgnoredFailingTest_ReturnsSuccess(string currentTfm)
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

    public sealed class TestAssetFixture() : TestAssetFixtureBase()
    {
        private const string Sources = """
#file CoverageThresholdControllerExitCode.csproj

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
    <PackageReference Include="Microsoft.Testing.Extensions.GitHubActionsReport" Version="$MicrosoftTestingExtensionsGitHubActionsReportVersion$" />
    <PackageReference Include="Microsoft.Testing.Extensions.AzureDevOpsReport" Version="$MicrosoftTestingPlatformVersion$" />
  </ItemGroup>
</Project>

#file Program.cs
using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Testing.Extensions;
using Microsoft.Testing.Platform.Builder;
using Microsoft.Testing.Platform.Capabilities.TestFramework;
using Microsoft.Testing.Platform.Extensions;
using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Extensions.TestFramework;
using Microsoft.Testing.Platform.Extensions.TestHostControllers;
using Microsoft.Testing.Platform.Messages;
using Microsoft.Testing.Platform.Services;
using Microsoft.Testing.Platform.TestHost;

public class Startup
{
    public static async Task<int> Main(string[] args)
    {
        var testApplicationBuilder = await TestApplication.CreateBuilderAsync(args);
        testApplicationBuilder.RegisterTestFramework(_ => new TestFrameworkCapabilities(), (_, __) => new DummyTestFramework());
        string? summaryProvider = Environment.GetEnvironmentVariable("CI_SUMMARY_PROVIDER");
        if (summaryProvider == "github")
        {
            testApplicationBuilder.AddGitHubActionsProvider();
        }
        else if (summaryProvider == "azure")
        {
            testApplicationBuilder.AddAzureDevOpsProvider();
        }

        // Registering a test host controller extension makes the platform run in controller mode:
        // a separate test host process runs the tests and this (controller) process publishes the
        // coverage threshold result and evaluates the exit code override.
        testApplicationBuilder.TestHostControllers.AddProcessLifetimeHandler(serviceProvider =>
            new CoverageThresholdLifetimeHandler(serviceProvider.GetMessageBus()));

        using ITestApplication app = await testApplicationBuilder.BuildAsync();
        return await app.RunAsync();
    }
}

public class CoverageThresholdLifetimeHandler : ITestHostProcessLifetimeHandler, IDataProducer
{
    private readonly IMessageBus _messageBus;

    public CoverageThresholdLifetimeHandler(IMessageBus messageBus)
        => _messageBus = messageBus;

    public string Uid => nameof(CoverageThresholdLifetimeHandler);

    public string Version => "1.0.0";

    public string DisplayName => nameof(CoverageThresholdLifetimeHandler);

    public string Description => nameof(CoverageThresholdLifetimeHandler);

    public Type[] DataTypesProduced => Environment.GetEnvironmentVariable("COVERAGE_MEASUREMENTS") == "testhost"
        ? Array.Empty<Type>()
        : new[] { typeof(TestCoverageThresholdMessage), typeof(TestCoverageMessage) };

    public Task<bool> IsEnabledAsync() => Task.FromResult(true);

    public Task BeforeTestHostProcessStartAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task OnTestHostProcessStartedAsync(ITestHostProcessInformation testHostProcessInformation, CancellationToken cancellationToken) => Task.CompletedTask;

    public async Task OnTestHostProcessExitedAsync(ITestHostProcessInformation testHostProcessInformation, CancellationToken cancellationToken)
    {
        string? thresholdStatus = Environment.GetEnvironmentVariable("COVERAGE_THRESHOLD_STATUS");
        var sessionUid = new SessionUid("controller");
        string? measurements = Environment.GetEnvironmentVariable("COVERAGE_MEASUREMENTS");
        if (measurements is "normal" or "empty")
        {
            await _messageBus.PublishAsync(this, new TestCoverageMessage(
                sessionUid, CoverageScope.Overall, CoverageMetric.Line,
                measurements == "empty" ? 0 : 80, measurements == "empty" ? 0 : 100, Uid));
            await _messageBus.PublishAsync(this, new TestCoverageMessage(
                sessionUid, CoverageScope.Overall, CoverageMetric.Branch, 70, 100, Uid));
        }

        if (thresholdStatus == "Failed")
        {
            await _messageBus.PublishAsync(this, new TestCoverageThresholdMessage(
                sessionUid, CoverageScope.Overall, CoverageMetric.Line, CoverageAggregation.Minimum,
                actualPercentage: 70.0, requiredPercentage: 80.0, hasCoverableData: true, producerId: nameof(CoverageThresholdLifetimeHandler),
                aggregatedOver: CoverageScopeLevel.Module));
        }
        else if (thresholdStatus == "Passed")
        {
            await _messageBus.PublishAsync(this, new TestCoverageThresholdMessage(
                sessionUid, CoverageScope.Overall, CoverageMetric.Line, CoverageAggregation.Minimum,
                actualPercentage: 90.0, requiredPercentage: 80.0, hasCoverableData: true, producerId: nameof(CoverageThresholdLifetimeHandler),
                aggregatedOver: CoverageScopeLevel.Module));
        }
    }
}

public class DummyTestFramework : ITestFramework, IDataProducer
{
    public string Uid => nameof(DummyTestFramework);

    public string Version => "2.0.0";

    public string DisplayName => nameof(DummyTestFramework);

    public string Description => nameof(DummyTestFramework);

    public Task<bool> IsEnabledAsync() => Task.FromResult(true);

    public Type[] DataTypesProduced => new[] { typeof(TestNodeUpdateMessage), typeof(TestCoverageMessage) };

    public Task<CreateTestSessionResult> CreateTestSessionAsync(CreateTestSessionContext context)
        => Task.FromResult(new CreateTestSessionResult() { IsSuccess = true });

    public Task<CloseTestSessionResult> CloseTestSessionAsync(CloseTestSessionContext context)
        => Task.FromResult(new CloseTestSessionResult() { IsSuccess = true });

    public async Task ExecuteRequestAsync(ExecuteRequestContext context)
    {
        IProperty state = Environment.GetEnvironmentVariable("FAIL_TEST") == "1"
            ? new FailedTestNodeStateProperty()
            : new PassedTestNodeStateProperty();
        await context.MessageBus.PublishAsync(this, new TestNodeUpdateMessage(context.Request.Session.SessionUid, new TestNode()
        {
            Uid = "Test1",
            DisplayName = "Test1",
            Properties = new PropertyBag(state),
        }));

        if (Environment.GetEnvironmentVariable("COVERAGE_MEASUREMENTS") == "testhost")
        {
            await context.MessageBus.PublishAsync(this, new TestCoverageMessage(
                context.Request.Session.SessionUid, CoverageScope.Overall, CoverageMetric.Line, 80, 100, Uid));
        }

        context.Complete();
    }
}
""";

        public string TargetAssetPath => GetAssetPath(AssetName);

        public override (string ID, string Name, string Code) GetAssetsToGenerate() => (AssetName, AssetName,
                Sources
                .PatchTargetFrameworks(TargetFrameworks.All)
                .PatchCodeWithReplace("$MicrosoftTestingExtensionsGitHubActionsReportVersion$", MicrosoftTestingExtensionsGitHubActionsReportVersion)
                .PatchCodeWithReplace("$MicrosoftTestingPlatformVersion$", MicrosoftTestingPlatformVersion));
    }

    public TestContext TestContext { get; set; }
}
