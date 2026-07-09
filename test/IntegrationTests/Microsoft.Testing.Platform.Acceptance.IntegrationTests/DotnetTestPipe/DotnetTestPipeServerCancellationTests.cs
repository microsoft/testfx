// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.Acceptance.IntegrationTests.DotnetTestPipe;

/// <summary>
/// End-to-end tests for server-initiated session cancellation over the <c>--server dotnettestcli</c> pipe
/// protocol (issue https://github.com/microsoft/testfx/issues/8691).
/// <para>
/// The fake SDK advertises a reverse "server control" pipe in its handshake reply. The test app connects back to
/// it and parks a long-poll; the SDK completes that long-poll with a <c>CancelSession</c> message while the app is
/// blocked inside a running test. The app is expected to react by stopping <b>gracefully</b> - it still emits its
/// test results and <c>TestSessionEnd</c> and exits cleanly, rather than being killed.
/// </para>
/// </summary>
[TestClass]
public sealed class DotnetTestPipeServerCancellationTests : AcceptanceTestBase<DotnetTestPipeServerCancellationTests.TestAssetFixture>
{
    private const string AssetName = "DotnetTestPipeServerCancellationTest";

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task DotnetTestPipe_ServerCancelSession_StopsGracefullyAndStillReports()
    {
        var testHost = TestInfrastructure.TestHost.LocateFrom(
            AssetFixture.TargetAssetPath, AssetName, TargetFrameworks.NetCurrent);

        // The fake SDK advertises the reverse control pipe and, once the app connects and parks, pushes a
        // CancelSession. The advertised protocol version must include 1.4.0 so the negotiation succeeds; the
        // feature itself is gated on the ServerControlPipeName property, not the version string.
        FakeDotnetTestSdkResult result = await FakeDotnetTestSdk.RunAsync(
            testHost,
            environmentVariables: new Dictionary<string, string?> { ["SERVERCANCEL_WAIT"] = "1" },
            supportedProtocolVersions: "1.0.0;1.1.0;1.2.0;1.3.0;1.4.0",
            advertiseServerControlPipe: true,
            cancellationToken: TestContext.CancellationToken);

        Assert.IsTrue(result.ServerControlPipeConnected, "The test app should have connected back to the reverse server-control pipe.");
        Assert.IsTrue(result.ServerCancelSent, "The fake SDK should have pushed a CancelSession over the control pipe.");

        // Reporting must survive the cancel: the app publishes a passed result only AFTER it observes the cancel,
        // so seeing a TestResult frame proves the graceful path kept the reporting pipeline alive.
        Assert.IsNotEmpty(
            result.MessagesWithSerializerId(DotnetTestPipeProtocol.SerializerIds.TestResultMessages).ToList(),
            "Expected a TestResult message after the server-initiated cancel (reporting should survive a graceful stop).");

        // TestSessionEnd must still be emitted - the app wound down cleanly instead of being killed.
        byte[] sessionEventTypes =
        [
            .. result.MessagesWithSerializerId(DotnetTestPipeProtocol.SerializerIds.TestSessionEvent)
                .Select(m => DotnetTestPipeProtocol.DecodeTestSessionEventBody(m.Body).SessionType)
                .Where(t => t.HasValue)
                .Select(t => t!.Value)
        ];

        Assert.Contains(
            DotnetTestPipeProtocol.SessionEventTypes.TestSessionEnd,
            sessionEventTypes,
            $"Expected a TestSessionEnd event after cancel; got [{string.Join(", ", sessionEventTypes)}].");

        Assert.AreEqual(
            0,
            result.TestHostResult.ExitCode,
            $"A graceful server-initiated cancel that still reports a passing test should exit cleanly.{Environment.NewLine}" +
            $"stdout:{Environment.NewLine}{result.TestHostResult.StandardOutput}{Environment.NewLine}" +
            $"stderr:{Environment.NewLine}{result.TestHostResult.StandardError}");
    }

    [TestMethod]
    public async Task DotnetTestPipe_NoServerControlPipeAdvertised_AppNeverConnectsBack()
    {
        var testHost = TestInfrastructure.TestHost.LocateFrom(
            AssetFixture.TargetAssetPath, AssetName, TargetFrameworks.NetCurrent);

        // An SDK that does not advertise the control pipe (the old-SDK / opted-out case) must not cause the app
        // to open any control channel; the feature stays off and the app finishes on its own fallback timeout.
        FakeDotnetTestSdkResult result = await FakeDotnetTestSdk.RunAsync(
            testHost,
            supportedProtocolVersions: "1.0.0;1.1.0;1.2.0;1.3.0;1.4.0",
            advertiseServerControlPipe: false,
            cancellationToken: TestContext.CancellationToken);

        Assert.IsFalse(result.ServerControlPipeConnected, "No control pipe was advertised, so the app must not connect back.");
        Assert.IsFalse(result.ServerCancelSent, "No control pipe was advertised, so no cancel could be sent.");
    }

    public sealed class TestAssetFixture() : TestAssetFixtureBase()
    {
        private const string AssetCode = """
#file DotnetTestPipeServerCancellationTest.csproj
<Project Sdk="Microsoft.NET.Sdk">
    <PropertyGroup>
        <TargetFrameworks>$TargetFrameworks$</TargetFrameworks>
        <ImplicitUsings>enable</ImplicitUsings>
        <Nullable>enable</Nullable>
        <OutputType>Exe</OutputType>
        <UseAppHost>true</UseAppHost>
        <LangVersion>preview</LangVersion>
        <!-- IGracefulStopTestExecutionCapability is [Experimental("TPEXP")]; this asset intentionally uses it. -->
        <NoWarn>$(NoWarn);TPEXP</NoWarn>
    </PropertyGroup>

    <ItemGroup>
        <PackageReference Include="Microsoft.Testing.Platform" Version="$MicrosoftTestingPlatformVersion$" />
    </ItemGroup>
</Project>

#file Program.cs
using Microsoft.Testing.Platform.Builder;
using Microsoft.Testing.Platform.Capabilities.TestFramework;
using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Extensions.TestFramework;

public class Program
{
    public static async Task<int> Main(string[] args)
    {
        ITestApplicationBuilder builder = await TestApplication.CreateBuilderAsync(args);
        var framework = new DummyTestFramework();
        builder.RegisterTestFramework(
            _ => new TestFrameworkCapabilities(framework.GracefulStopCapability),
            (_, __) => framework);
        using ITestApplication app = await builder.BuildAsync();
        return await app.RunAsync();
    }
}

// A graceful-stop capability whose StopTestExecutionAsync unblocks the running test. The platform resolves this
// capability when it receives a server-initiated CancelSession over the reverse control pipe.
public sealed class DummyGracefulStopCapability : IGracefulStopTestExecutionCapability
{
    private readonly TaskCompletionSource _requested = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public Task Requested => _requested.Task;

    public Task StopTestExecutionAsync(CancellationToken cancellationToken)
    {
        _requested.TrySetResult();
        return Task.CompletedTask;
    }
}

public sealed class DummyTestFramework : ITestFramework, IDataProducer
{
    public DummyGracefulStopCapability GracefulStopCapability { get; } = new();

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
        // Only the cancellation scenario asks us to park (via an env var) so the negative test finishes fast.
        if (Environment.GetEnvironmentVariable("SERVERCANCEL_WAIT") == "1")
        {
            // Park until the SDK asks us to stop (via the reverse control pipe -> graceful stop) or the whole
            // application is cancelled. A generous fallback timeout keeps the process from hanging forever if no
            // signal ever arrives (the test assertions then fail loudly rather than the run deadlocking).
            Task requested = GracefulStopCapability.Requested;
            Task timeout = Task.Delay(TimeSpan.FromSeconds(60));
            await Task.WhenAny(requested, timeout);
        }

        // Reporting must survive the cancel: publish a passed result AFTER we observed the stop request so the
        // harness can prove the graceful path kept the reporting pipeline alive.
        await context.MessageBus.PublishAsync(this, new TestNodeUpdateMessage(context.Request.Session.SessionUid,
            new TestNode() { Uid = "1", DisplayName = "CancelledButReported", Properties = new(PassedTestNodeStateProperty.CachedInstance) }));
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
