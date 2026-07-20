// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.Acceptance.IntegrationTests.DotnetTestPipe;

/// <summary>
/// Verifies that, under the <c>--server dotnettestcli</c> pipe protocol, the test host orchestrator
/// (here the retry orchestrator) performs the handshake as well — not only the test hosts it spawns.
/// This locks down <a href="https://github.com/microsoft/testfx/issues/6179">microsoft/testfx#6179</a>:
/// the orchestrator handshakes with <c>HostType=TestHostOrchestrator</c> and advertises the
/// orchestration feature responsible for the run via the <c>OrchestratorFeature</c> property.
/// </summary>
[TestClass]
public class DotnetTestPipeOrchestratorHandshakeTests : AcceptanceTestBase<DotnetTestPipeOrchestratorHandshakeTests.TestAssetFixture>
{
    private const string AssetName = "DotnetTestPipeOrchestratorHandshake";

    // Mirrors HandshakeMessageHostTypes.TestHostOrchestrator on the testfx side. Kept as a literal
    // here so the black-box harness stays decoupled from internal testfx types.
    private const string TestHostOrchestratorHostType = "TestHostOrchestrator";
    private const string TestHostHostType = "TestHost";

    // The retry orchestrator extension Uid, sent as the OrchestratorFeature value.
    private const string RetryOrchestratorFeature = "RetryOrchestrator";

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task DotnetTestPipe_Orchestrator_HandshakesWithFeature()
    {
        var testHost = TestInfrastructure.TestHost.LocateFrom(
            AssetFixture.TargetAssetPath, AssetName, TargetFrameworks.NetCurrent);

        string resultDirectory = Path.Combine(testHost.DirectoryName, Guid.NewGuid().ToString("N"));

        FakeDotnetTestSdkMultiConnectionResult result = await FakeDotnetTestSdkMultiConnection.RunAsync(
            testHost,
            extraArguments: $"--retry-failed-tests 3 --results-directory {resultDirectory}",
            environmentVariables: new()
            {
                { EnvironmentVariableConstants.TESTINGPLATFORM_TELEMETRY_OPTOUT, "1" },
                { "RETRY_MARKER_DIRECTORY", resultDirectory },
            },
            cancellationToken: TestContext.CancellationToken);

        result.TestHostResult.AssertExitCodeIs(ExitCode.Success);

        Dictionary<byte, string> orchestratorHandshake = Assert.ContainsSingle(result.HandshakesWithHostType(TestHostOrchestratorHostType));

        Assert.IsTrue(
            orchestratorHandshake.TryGetValue(DotnetTestPipeProtocol.HandshakeProperties.OrchestratorFeature, out string? feature),
            "Orchestrator handshake is missing the OrchestratorFeature property.");
        Assert.AreEqual(RetryOrchestratorFeature, feature);
        Assert.IsFalse(
            orchestratorHandshake.ContainsKey(DotnetTestPipeProtocol.HandshakeProperties.AttemptNumber),
            "The orchestrator itself should not report a test-host attempt number.");

        Dictionary<byte, string>[] testHostHandshakes =
        [
            .. result.HandshakesWithHostType(TestHostHostType)
                .OrderBy(handshake => int.Parse(handshake[DotnetTestPipeProtocol.HandshakeProperties.AttemptNumber], CultureInfo.InvariantCulture)),
        ];

        Assert.HasCount(2, testHostHandshakes);
        Assert.AreEqual("1", testHostHandshakes[0][DotnetTestPipeProtocol.HandshakeProperties.AttemptNumber]);
        Assert.AreEqual("2", testHostHandshakes[1][DotnetTestPipeProtocol.HandshakeProperties.AttemptNumber]);
        Assert.AreEqual(
            testHostHandshakes[0][DotnetTestPipeProtocol.HandshakeProperties.ExecutionId],
            testHostHandshakes[1][DotnetTestPipeProtocol.HandshakeProperties.ExecutionId],
            "All retry attempts should remain part of the same logical execution.");
        Assert.AreNotEqual(
            testHostHandshakes[0][DotnetTestPipeProtocol.HandshakeProperties.InstanceId],
            testHostHandshakes[1][DotnetTestPipeProtocol.HandshakeProperties.InstanceId],
            "Each retry attempt runs in a distinct test-host process.");
        Assert.IsTrue(
            testHostHandshakes.All(handshake => !handshake.ContainsKey(DotnetTestPipeProtocol.HandshakeProperties.OrchestratorFeature)),
            "Test host handshakes should not carry the OrchestratorFeature property.");
    }

    public sealed class TestAssetFixture() : TestAssetFixtureBase()
    {
        private const string AssetCode = """
#file DotnetTestPipeOrchestratorHandshake.csproj
<Project Sdk="Microsoft.NET.Sdk">
    <PropertyGroup>
        <TargetFrameworks>$TargetFrameworks$</TargetFrameworks>
        <ImplicitUsings>enable</ImplicitUsings>
        <Nullable>enable</Nullable>
        <OutputType>Exe</OutputType>
        <UseAppHost>true</UseAppHost>
        <LangVersion>preview</LangVersion>
    </PropertyGroup>

    <ItemGroup>
        <PackageReference Include="Microsoft.Testing.Platform" Version="$MicrosoftTestingPlatformVersion$" />
        <PackageReference Include="Microsoft.Testing.Extensions.Retry" Version="$MicrosoftTestingPlatformVersion$" />
    </ItemGroup>
</Project>

#file Program.cs
using Microsoft.Testing.Extensions;
using Microsoft.Testing.Platform.Builder;
using Microsoft.Testing.Platform.Capabilities.TestFramework;
using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Extensions.TestFramework;

public class Program
{
    public static async Task<int> Main(string[] args)
    {
        ITestApplicationBuilder builder = await TestApplication.CreateBuilderAsync(args);
        builder.RegisterTestFramework(_ => new TestFrameworkCapabilities(), (_, __) => new DummyTestFramework());
        builder.AddRetryProvider();
        using ITestApplication app = await builder.BuildAsync();
        return await app.RunAsync();
    }
}

public class DummyTestFramework : ITestFramework, IDataProducer
{
    public string Uid => nameof(DummyTestFramework);
    public string Version => "2.0.0";
    public string DisplayName => nameof(DummyTestFramework);
    public string Description => nameof(DummyTestFramework);
    public Type[] DataTypesProduced => new[] { typeof(TestNodeUpdateMessage) };
    public Task<bool> IsEnabledAsync() => Task.FromResult(true);
    public Task<CreateTestSessionResult> CreateTestSessionAsync(CreateTestSessionContext context)
        => Task.FromResult(new CreateTestSessionResult() { IsSuccess = true });
    public Task<CloseTestSessionResult> CloseTestSessionAsync(CloseTestSessionContext context)
        => Task.FromResult(new CloseTestSessionResult() { IsSuccess = true });
    public async Task ExecuteRequestAsync(ExecuteRequestContext context)
    {
        string markerDirectory = Environment.GetEnvironmentVariable("RETRY_MARKER_DIRECTORY")!;
        Directory.CreateDirectory(markerDirectory);
        string markerPath = Path.Combine(markerDirectory, "first-attempt.failed");
        bool isFirstAttempt = !File.Exists(markerPath);
        if (isFirstAttempt)
        {
            File.WriteAllText(markerPath, string.Empty);
        }

        TestNodeStateProperty state = isFirstAttempt
            ? new FailedTestNodeStateProperty()
            : PassedTestNodeStateProperty.CachedInstance;
        await context.MessageBus.PublishAsync(
            this,
            new TestNodeUpdateMessage(
                context.Request.Session.SessionUid,
                new TestNode
                {
                    Uid = "flaky",
                    DisplayName = "FlakyTest",
                    Properties = new(state),
                }));
        context.Complete();
    }
}
""";

        public string TargetAssetPath => GetAssetPath(AssetName);

        public override (string ID, string Name, string Code) GetAssetsToGenerate() => (AssetName, AssetName,
            AssetCode
                .PatchTargetFrameworks(TargetFrameworks.NetCurrent)
                .PatchCodeWithReplace("$MicrosoftTestingPlatformVersion$", MicrosoftTestingPlatformVersion));
    }
}
