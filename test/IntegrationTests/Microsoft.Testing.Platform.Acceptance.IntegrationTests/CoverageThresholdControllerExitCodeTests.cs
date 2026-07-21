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
using Microsoft.Testing.Platform.Extensions.TestHostControllers;
using Microsoft.Testing.Platform.Messages;
using Microsoft.Testing.Platform.Services;

public class Startup
{
    public static async Task<int> Main(string[] args)
    {
        var testApplicationBuilder = await TestApplication.CreateBuilderAsync(args);
        testApplicationBuilder.RegisterTestFramework(_ => new TestFrameworkCapabilities(), (_, __) => new DummyTestFramework());

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

    public Type[] DataTypesProduced => new[] { typeof(TestCoverageThresholdMessage) };

    public Task<bool> IsEnabledAsync() => Task.FromResult(true);

    public Task BeforeTestHostProcessStartAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task OnTestHostProcessStartedAsync(ITestHostProcessInformation testHostProcessInformation, CancellationToken cancellationToken) => Task.CompletedTask;

    public async Task OnTestHostProcessExitedAsync(ITestHostProcessInformation testHostProcessInformation, CancellationToken cancellationToken)
    {
        string? thresholdStatus = Environment.GetEnvironmentVariable("COVERAGE_THRESHOLD_STATUS");
        if (thresholdStatus == "Failed")
        {
            await _messageBus.PublishAsync(this, new TestCoverageThresholdMessage(70.0, 80.0, CoverageMetric.Line, CoverageThresholdStatistic.Minimum));
        }
        else if (thresholdStatus == "Passed")
        {
            await _messageBus.PublishAsync(this, new TestCoverageThresholdMessage(90.0, 80.0, CoverageMetric.Line, CoverageThresholdStatistic.Minimum));
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

    public Type[] DataTypesProduced => new[] { typeof(TestNodeUpdateMessage) };

    public Task<CreateTestSessionResult> CreateTestSessionAsync(CreateTestSessionContext context)
        => Task.FromResult(new CreateTestSessionResult() { IsSuccess = true });

    public Task<CloseTestSessionResult> CloseTestSessionAsync(CloseTestSessionContext context)
        => Task.FromResult(new CloseTestSessionResult() { IsSuccess = true });

    public async Task ExecuteRequestAsync(ExecuteRequestContext context)
    {
        await context.MessageBus.PublishAsync(this, new TestNodeUpdateMessage(context.Request.Session.SessionUid, new TestNode()
        {
            Uid = "Test1",
            DisplayName = "Test1",
            Properties = new PropertyBag(new PassedTestNodeStateProperty()),
        }));

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
