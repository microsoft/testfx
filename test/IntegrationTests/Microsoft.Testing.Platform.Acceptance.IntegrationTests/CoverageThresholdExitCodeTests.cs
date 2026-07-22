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

    public Type[] DataTypesProduced => new[] { typeof(TestNodeUpdateMessage), typeof(TestCoverageThresholdMessage) };

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
