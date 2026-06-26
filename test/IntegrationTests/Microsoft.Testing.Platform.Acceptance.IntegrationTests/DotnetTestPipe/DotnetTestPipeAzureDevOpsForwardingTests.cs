// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.Acceptance.IntegrationTests.DotnetTestPipe;

/// <summary>
/// Acceptance tests for forwarding Azure DevOps logging commands over the
/// <c>--server dotnettestcli --dotnet-test-pipe</c> protocol.
/// <para>
/// Under the pipe protocol the host installs a no-op output device, so the AzureDevOpsReport
/// extension's logging commands (<c>##[group]</c>, <c>##vso[...]</c>) would otherwise be swallowed
/// in multi-assembly runs. Protocol 1.2.0 adds <c>AzureDevOpsLogMessage</c> (serializer id 11): when
/// both sides negotiate 1.2.0 and the host is running on an Azure DevOps agent, those marked lines are
/// forwarded to the SDK instead of being dropped. These tests assert that contract on the wire via the
/// black-box <see cref="FakeDotnetTestSdk"/> harness.
/// </para>
/// </summary>
[TestClass]
public sealed class DotnetTestPipeAzureDevOpsForwardingTests : AcceptanceTestBase<DotnetTestPipeAzureDevOpsForwardingTests.TestAssetFixture>
{
    private const string AssetName = "DotnetTestPipeAzureDevOpsForwarding";

    // Mirrors ProtocolConstants.SupportedVersions on the host side: the host advertises 1.2.0, the
    // version that introduced AzureDevOpsLogMessage forwarding.
    private const string HostAdvertisedProtocolVersions = "1.0.0;1.1.0;1.2.0";

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task DotnetTestPipe_WhenSdkSupports120AndRunningInAzureDevOps_ForwardsLogGroupCommands()
    {
        var testHost = TestInfrastructure.TestHost.LocateFrom(
            AssetFixture.TargetAssetPath, AssetName, TargetFrameworks.NetCurrent);

        FakeDotnetTestSdkResult result = await FakeDotnetTestSdk.RunAsync(
            testHost,
            extraArguments: "--report-azdo",
            environmentVariables: new Dictionary<string, string?> { ["TF_BUILD"] = "true" },
            supportedProtocolVersions: HostAdvertisedProtocolVersions,
            cancellationToken: TestContext.CancellationToken);

        Assert.AreEqual(
            "1.2.0",
            result.NegotiatedProtocolVersion,
            "When both sides advertise 1.2.0, negotiation should select 1.2.0 to enable Azure DevOps log forwarding.");

        string[] forwardedLines = GetForwardedAzureDevOpsLines(result);

        Assert.Contains(
            line => line.StartsWith("##[group]", StringComparison.Ordinal),
            forwardedLines,
            $"Expected a forwarded '##[group]' command. Forwarded lines: [{string.Join(" | ", forwardedLines)}].");
        Assert.Contains(
            "##[endgroup]",
            forwardedLines,
            $"Expected a forwarded '##[endgroup]' command. Forwarded lines: [{string.Join(" | ", forwardedLines)}].");

        // The forwarded frames carry the same InstanceId as the handshake so the SDK can correlate the
        // logging commands back to the originating assembly in a multi-assembly run.
        Assert.IsNotNull(result.ReceivedHandshake);
        Assert.IsTrue(result.ReceivedHandshake.TryGetValue(DotnetTestPipeProtocol.HandshakeProperties.InstanceId, out string? handshakeInstanceId));
        string[] forwardedInstanceIds = [..
            result.MessagesWithSerializerId(DotnetTestPipeProtocol.SerializerIds.AzureDevOpsLogMessage)
                .Select(m => DotnetTestPipeProtocol.DecodeAzureDevOpsLogMessageBody(m.Body).InstanceId)
                .Where(id => id is not null)
                .Select(id => id!)
                .Distinct()];
        string onlyInstanceId = Assert.ContainsSingle(forwardedInstanceIds);
        Assert.AreEqual(handshakeInstanceId, onlyInstanceId);
    }

    [TestMethod]
    public async Task DotnetTestPipe_WhenSdkOnlySupports110_DoesNotForwardLogMessages()
    {
        var testHost = TestInfrastructure.TestHost.LocateFrom(
            AssetFixture.TargetAssetPath, AssetName, TargetFrameworks.NetCurrent);

        // An older SDK that does not understand AzureDevOpsLogMessage advertises only up to 1.1.0.
        FakeDotnetTestSdkResult result = await FakeDotnetTestSdk.RunAsync(
            testHost,
            extraArguments: "--report-azdo",
            environmentVariables: new Dictionary<string, string?> { ["TF_BUILD"] = "true" },
            supportedProtocolVersions: "1.0.0;1.1.0",
            cancellationToken: TestContext.CancellationToken);

        Assert.AreEqual(
            "1.1.0",
            result.NegotiatedProtocolVersion,
            "An SDK that supports up to 1.1.0 should negotiate down to 1.1.0.");

        Assert.IsEmpty(
            result.MessagesWithSerializerId(DotnetTestPipeProtocol.SerializerIds.AzureDevOpsLogMessage),
            "No AzureDevOpsLogMessage should be forwarded when the negotiated protocol is below 1.2.0.");
    }

    [TestMethod]
    public async Task DotnetTestPipe_WhenNotRunningInAzureDevOps_DoesNotForwardLogMessages()
    {
        var testHost = TestInfrastructure.TestHost.LocateFrom(
            AssetFixture.TargetAssetPath, AssetName, TargetFrameworks.NetCurrent);

        // 1.2.0 is negotiated, but without TF_BUILD the host is not on an Azure DevOps agent, so the
        // AzureDevOpsReport extension produces nothing and the forwarder stays a no-op.
        FakeDotnetTestSdkResult result = await FakeDotnetTestSdk.RunAsync(
            testHost,
            extraArguments: "--report-azdo",
            supportedProtocolVersions: HostAdvertisedProtocolVersions,
            cancellationToken: TestContext.CancellationToken);

        Assert.AreEqual("1.2.0", result.NegotiatedProtocolVersion);

        Assert.IsEmpty(
            result.MessagesWithSerializerId(DotnetTestPipeProtocol.SerializerIds.AzureDevOpsLogMessage),
            "No AzureDevOpsLogMessage should be forwarded when not running on an Azure DevOps agent.");
    }

    private static string[] GetForwardedAzureDevOpsLines(FakeDotnetTestSdkResult result)
        => [..
            result.MessagesWithSerializerId(DotnetTestPipeProtocol.SerializerIds.AzureDevOpsLogMessage)
                .Select(m => DotnetTestPipeProtocol.DecodeAzureDevOpsLogMessageBody(m.Body).LogText)
                .Where(text => text is not null)
                .Select(text => text!)];

    public sealed class TestAssetFixture() : TestAssetFixtureBase()
    {
        private const string Sources = """
#file DotnetTestPipeAzureDevOpsForwarding.csproj
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFrameworks>$TargetFrameworks$</TargetFrameworks>
    <OutputType>Exe</OutputType>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <UseAppHost>true</UseAppHost>
    <LangVersion>preview</LangVersion>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.Testing.Extensions.AzureDevOpsReport" Version="$MicrosoftTestingPlatformVersion$" />
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
        builder.AddAzureDevOpsProvider();
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
            Sources
                .PatchTargetFrameworks(TargetFrameworks.NetCurrent)
                .PatchCodeWithReplace("$MicrosoftTestingPlatformVersion$", MicrosoftTestingPlatformVersion));
    }
}
