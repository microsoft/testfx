// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Acceptance.IntegrationTests;
using Microsoft.Testing.Platform.ServerMode.IntegrationTests.Messages.V100;

using MSTest.Acceptance.IntegrationTests.Messages.V100;

namespace MSTest.Acceptance.IntegrationTests;

[TestGroup]
public sealed class ServerModeTests : ServerModeTestsBase
{
    private readonly TestAssetFixture _fixture;

    public ServerModeTests(ITestExecutionContext testExecutionContext, TestAssetFixture fixture)
        : base(testExecutionContext) => _fixture = fixture;

    [ArgumentsProvider(nameof(TargetFrameworks.All), typeof(TargetFrameworks))]
    public async Task DiscoverAndRun(string tfm)
    {
        using TestingPlatformClient jsonClient = await StartAsServerAndConnectToTheClientAsync(TestHost.LocateFrom(_fixture.ProjectPath, "MSTestProject", tfm, buildConfiguration: BuildConfiguration.Release));
        LogsCollector logs = new();
        jsonClient.RegisterLogListener(logs);
        TelemetryCollector telemetry = new();
        jsonClient.RegisterTelemetryListener(telemetry);

        InitializeResponse initializeResponseArgs = await jsonClient.Initialize();

        Assert.IsTrue(initializeResponseArgs.Capabilities.Testing.VSTestProvider);
        Assert.IsFalse(initializeResponseArgs.Capabilities.Testing.MultiRequestSupport);
        Assert.IsTrue(initializeResponseArgs.Capabilities.Testing.SupportsDiscovery);

        TestNodeUpdateCollector discoveryCollector = new();
        ResponseListener discoveryListener = await jsonClient.DiscoverTests(Guid.NewGuid(), discoveryCollector.CollectNodeUpdates);

        TestNodeUpdateCollector runCollector = new();
        ResponseListener runListener = await jsonClient.RunTests(Guid.NewGuid(), runCollector.CollectNodeUpdates);

        await Task.WhenAll(discoveryListener.WaitCompletion(), runListener.WaitCompletion());
        Assert.AreEqual(1, discoveryCollector.TestNodeUpdates.Count(x => x.Node.NodeType == "action"), $"Wrong number of discovery");
        Assert.AreEqual(2, runCollector.TestNodeUpdates.Count, $"Wrong number of updates");
        Assert.IsFalse(logs.Count == 0, $"Logs are empty");
        Assert.IsFalse(telemetry.IsEmpty, $"telemetry is empty");
        await jsonClient.Exit();
        Assert.AreEqual(0, await jsonClient.WaitServerProcessExit());
        Assert.AreEqual(0, jsonClient.ExitCode);
    }

    [ArgumentsProvider(nameof(TargetFrameworks.All), typeof(TargetFrameworks))]
    public async Task WhenClientDies_Server_ShouldClose_Gracefully(string tfm)
    {
        using TestingPlatformClient jsonClient = await StartAsServerAndConnectToTheClientAsync(TestHost.LocateFrom(_fixture.ProjectPath, "MSTestProject", tfm, buildConfiguration: BuildConfiguration.Release));
        LogsCollector logs = new();
        jsonClient.RegisterLogListener(logs);
        TelemetryCollector telemetry = new();
        jsonClient.RegisterTelemetryListener(telemetry);

        InitializeResponse initializeResponseArgs = await jsonClient.Initialize();
        Assert.IsFalse(initializeResponseArgs.Capabilities.Testing.MultiRequestSupport);

        TestNodeUpdateCollector discoveryCollector = new();

        // We're not interested we want to start some activity at adapter level
        // We know that it's possible that we're fast enough to not start the discovery at all in all cases.
        _ = jsonClient.DiscoverTests(Guid.NewGuid(), discoveryCollector.CollectNodeUpdates, @checked: false);

        await jsonClient.Exit(gracefully: false);
        int exitCode = await jsonClient.WaitServerProcessExit();
        Assert.AreEqual(3, exitCode);
    }

    [TestFixture(TestFixtureSharingStrategy.PerTestGroup)]
    public sealed class TestAssetFixture(AcceptanceFixture acceptanceFixture) : TestAssetFixtureBase(acceptanceFixture.NuGetGlobalPackagesFolder)
    {
        public const string ProjectName = "MSTestProject";

        public string ProjectPath => GetAssetPath(ProjectName);

        public override IEnumerable<(string ID, string Name, string Code)> GetAssetsToGenerate()
        {
            yield return (ProjectName, ProjectName,
                CurrentMSTestSourceCode
                .PatchCodeWithReplace("$TargetFramework$", $"<TargetFrameworks>{TargetFrameworks.All.ToMSBuildTargetFrameworks()}</TargetFrameworks>")
                .PatchCodeWithReplace("$MicrosoftNETTestSdkVersion$", MicrosoftNETTestSdkVersion)
                .PatchCodeWithReplace("$MSTestVersion$", MSTestVersion)
                .PatchCodeWithReplace("$EnableMSTestRunner$", "<EnableMSTestRunner>true</EnableMSTestRunner>")
                .PatchCodeWithReplace("$OutputType$", "<OutputType>Exe</OutputType>")
                .PatchCodeWithReplace("$Extra$", string.Empty));
        }
    }
}
