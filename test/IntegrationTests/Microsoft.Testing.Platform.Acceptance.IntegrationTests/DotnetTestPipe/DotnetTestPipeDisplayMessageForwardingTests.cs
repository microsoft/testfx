// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.Acceptance.IntegrationTests.DotnetTestPipe;

/// <summary>
/// Acceptance tests for forwarding generic warning/error host messages over the
/// <c>--server dotnettestcli --dotnet-test-pipe</c> protocol.
/// <para>
/// Under the pipe protocol the host installs a forwarding output device that discards informational output,
/// so host-side diagnostics produced outside test results (hang/crash dump diagnostics, retry summaries, generic
/// extension/framework warnings and errors) would otherwise be swallowed in multi-assembly runs. Protocol 1.3.0
/// adds <c>DisplayMessage</c> (serializer id 12): when both sides negotiate 1.3.0, the host forwards
/// <c>WarningMessageOutputDeviceData</c> / <c>ErrorMessageOutputDeviceData</c> to the SDK as a
/// <c>DisplayMessage</c> carrying a severity level. These tests assert that contract on the wire via the
/// black-box <see cref="FakeDotnetTestSdk"/> harness.
/// </para>
/// </summary>
[TestClass]
public sealed class DotnetTestPipeDisplayMessageForwardingTests : AcceptanceTestBase<DotnetTestPipeDisplayMessageForwardingTests.TestAssetFixture>
{
    private const string AssetName = "DotnetTestPipeDisplayMessageForwarding";

    private const string ErrorText = "boom error from the framework";
    private const string WarningText = "careful warning from the framework";

    // Mirrors ProtocolConstants.SupportedVersions on the host side: 1.3.0 is the version that introduced
    // DisplayMessage forwarding.
    private const string HostAdvertisedProtocolVersions = "1.0.0;1.1.0;1.2.0;1.3.0";

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task DotnetTestPipe_WhenSdkSupports130_ForwardsWarningAndErrorAsDisplayMessages()
    {
        var testHost = TestInfrastructure.TestHost.LocateFrom(
            AssetFixture.TargetAssetPath, AssetName, TargetFrameworks.NetCurrent);

        FakeDotnetTestSdkResult result = await FakeDotnetTestSdk.RunAsync(
            testHost,
            supportedProtocolVersions: HostAdvertisedProtocolVersions,
            cancellationToken: TestContext.CancellationToken);

        Assert.AreEqual(
            "1.3.0",
            result.NegotiatedProtocolVersion,
            "When both sides advertise 1.3.0, negotiation should select 1.3.0 to enable DisplayMessage forwarding.");

        (byte? Level, string? Text)[] forwarded = [..
            result.MessagesWithSerializerId(DotnetTestPipeProtocol.SerializerIds.DisplayMessage)
                .Select(m =>
                {
                    (string? _, string? _, byte? level, string? text) = DotnetTestPipeProtocol.DecodeDisplayMessageBody(m.Body);
                    return (level, text);
                })];

        Assert.Contains(
            m => m.Level == DotnetTestPipeProtocol.DisplayMessageLevels.Error && m.Text == ErrorText,
            forwarded,
            $"Expected a forwarded error DisplayMessage. Forwarded: [{string.Join(" | ", forwarded.Select(m => $"{m.Level}:{m.Text}"))}].");
        Assert.Contains(
            m => m.Level == DotnetTestPipeProtocol.DisplayMessageLevels.Warning && m.Text == WarningText,
            forwarded,
            $"Expected a forwarded warning DisplayMessage. Forwarded: [{string.Join(" | ", forwarded.Select(m => $"{m.Level}:{m.Text}"))}].");

        // The forwarded frames carry the same InstanceId as the handshake so the SDK can correlate the
        // messages back to the originating assembly in a multi-assembly run.
        Assert.IsNotNull(result.ReceivedHandshake);
        Assert.IsTrue(result.ReceivedHandshake.TryGetValue(DotnetTestPipeProtocol.HandshakeProperties.InstanceId, out string? handshakeInstanceId));
        string[] forwardedInstanceIds = [..
            result.MessagesWithSerializerId(DotnetTestPipeProtocol.SerializerIds.DisplayMessage)
                .Select(m => DotnetTestPipeProtocol.DecodeDisplayMessageBody(m.Body).InstanceId)
                .Where(id => id is not null)
                .Select(id => id!)
                .Distinct()];
        string onlyInstanceId = Assert.ContainsSingle(forwardedInstanceIds);
        Assert.AreEqual(handshakeInstanceId, onlyInstanceId);
    }

    [TestMethod]
    public async Task DotnetTestPipe_WhenSdkOnlySupports120_DoesNotForwardDisplayMessages()
    {
        var testHost = TestInfrastructure.TestHost.LocateFrom(
            AssetFixture.TargetAssetPath, AssetName, TargetFrameworks.NetCurrent);

        // An older SDK that does not understand DisplayMessage advertises only up to 1.2.0.
        FakeDotnetTestSdkResult result = await FakeDotnetTestSdk.RunAsync(
            testHost,
            supportedProtocolVersions: "1.0.0;1.1.0;1.2.0",
            cancellationToken: TestContext.CancellationToken);

        Assert.AreEqual(
            "1.2.0",
            result.NegotiatedProtocolVersion,
            "An SDK that supports up to 1.2.0 should negotiate down to 1.2.0.");

        Assert.IsEmpty(
            result.MessagesWithSerializerId(DotnetTestPipeProtocol.SerializerIds.DisplayMessage),
            "No DisplayMessage should be forwarded when the negotiated protocol is below 1.3.0.");
    }

    public sealed class TestAssetFixture() : TestAssetFixtureBase()
    {
        private const string Sources = """
#file DotnetTestPipeDisplayMessageForwarding.csproj
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
    <PackageReference Include="Microsoft.Testing.Platform" Version="$MicrosoftTestingPlatformVersion$" />
  </ItemGroup>
</Project>

#file Program.cs
using Microsoft.Testing.Platform.Builder;
using Microsoft.Testing.Platform.Capabilities.TestFramework;
using Microsoft.Testing.Platform.Extensions.OutputDevice;
using Microsoft.Testing.Platform.Extensions.TestFramework;
using Microsoft.Testing.Platform.OutputDevice;
using Microsoft.Testing.Platform.Services;

public class Program
{
    public static async Task<int> Main(string[] args)
    {
        ITestApplicationBuilder builder = await TestApplication.CreateBuilderAsync(args);
        builder.RegisterTestFramework(
            _ => new TestFrameworkCapabilities(),
            (_, serviceProvider) => new DummyTestFramework(serviceProvider.GetOutputDevice()));
        using ITestApplication app = await builder.BuildAsync();
        return await app.RunAsync();
    }
}

public class DummyTestFramework : ITestFramework, IOutputDeviceDataProducer
{
    private readonly IOutputDevice _outputDevice;

    public DummyTestFramework(IOutputDevice outputDevice) => _outputDevice = outputDevice;

    public string Uid => nameof(DummyTestFramework);
    public string Version => "2.0.0";
    public string DisplayName => nameof(DummyTestFramework);
    public string Description => nameof(DummyTestFramework);
    public Task<bool> IsEnabledAsync() => Task.FromResult(true);
    public Task<CreateTestSessionResult> CreateTestSessionAsync(CreateTestSessionContext context)
        => Task.FromResult(new CreateTestSessionResult() { IsSuccess = true });
    public Task<CloseTestSessionResult> CloseTestSessionAsync(CloseTestSessionContext context)
        => Task.FromResult(new CloseTestSessionResult() { IsSuccess = true });
    public async Task ExecuteRequestAsync(ExecuteRequestContext context)
    {
        await _outputDevice.DisplayAsync(this, new WarningMessageOutputDeviceData("careful warning from the framework"), CancellationToken.None);
        await _outputDevice.DisplayAsync(this, new ErrorMessageOutputDeviceData("boom error from the framework"), CancellationToken.None);
        context.Complete();
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
