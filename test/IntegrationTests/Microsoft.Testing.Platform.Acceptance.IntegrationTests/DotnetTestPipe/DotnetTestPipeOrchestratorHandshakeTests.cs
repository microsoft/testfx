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
            environmentVariables: new() { { EnvironmentVariableConstants.TESTINGPLATFORM_TELEMETRY_OPTOUT, "1" } },
            cancellationToken: TestContext.CancellationToken);

        // The orchestrator process and the single (passing) test host it spawned should both have
        // connected and handshaked.
        Dictionary<byte, string> orchestratorHandshake = Assert.ContainsSingle(result.HandshakesWithHostType(TestHostOrchestratorHostType));

        Assert.IsTrue(
            orchestratorHandshake.TryGetValue(DotnetTestPipeProtocol.HandshakeProperties.OrchestratorFeature, out string? feature),
            "Orchestrator handshake is missing the OrchestratorFeature property.");
        Assert.AreEqual(RetryOrchestratorFeature, feature);

        // The actual test host (spawned by the orchestrator) handshakes as a plain TestHost and must
        // NOT carry an orchestrator feature.
        Dictionary<byte, string> testHostHandshake = Assert.ContainsSingle(result.HandshakesWithHostType(TestHostHostType));
        Assert.IsFalse(
            testHostHandshake.ContainsKey(DotnetTestPipeProtocol.HandshakeProperties.OrchestratorFeature),
            "Test host handshake should not carry the OrchestratorFeature property.");
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

public class DummyTestFramework : ITestFramework
{
    public string Uid => nameof(DummyTestFramework);
    public string Version => "2.0.0";
    public string DisplayName => nameof(DummyTestFramework);
    public string Description => nameof(DummyTestFramework);
    public Task<bool> IsEnabledAsync() => Task.FromResult(true);
    public Task<CreateTestSessionResult> CreateTestSessionAsync(CreateTestSessionContext context)
        => Task.FromResult(new CreateTestSessionResult() { IsSuccess = true });
    public Task<CloseTestSessionResult> CloseTestSessionAsync(CloseTestSessionContext context)
        => Task.FromResult(new CloseTestSessionResult() { IsSuccess = true });
    public Task ExecuteRequestAsync(ExecuteRequestContext context)
    {
        context.Complete();
        return Task.CompletedTask;
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
