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
    private const string SelectionOrchestratorFeature = "SelectionOrchestrator";

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
        Assert.ContainsOnly(
            handshake => !handshake.ContainsKey(DotnetTestPipeProtocol.HandshakeProperties.OrchestratorFeature),
            testHostHandshakes,
            "Test host handshakes should not carry the OrchestratorFeature property.");
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task DotnetTestPipe_ComposedOrchestrators_EachProcessHandshakesWithItsUid(bool useController)
    {
        var testHost = TestInfrastructure.TestHost.LocateFrom(
            AssetFixture.TargetAssetPath, AssetName, TargetFrameworks.NetCurrent);
        string resultDirectory = Directory.CreateDirectory(Path.Combine(testHost.DirectoryName, Guid.NewGuid().ToString("N"))).FullName;
        string arguments = $"--retry-failed-tests 1 --results-directory \"{resultDirectory}\"";
        if (useController)
        {
            string config = Path.Combine(resultDirectory, "testconfig.json");
            await File.WriteAllTextAsync(config, """{"environmentVariables":{"COMPOSED_CONTROLLER_CHILD":"1"}}""", TestContext.CancellationToken);
            arguments += $" --config-file \"{config}\"";
        }

        FakeDotnetTestSdkMultiConnectionResult result = await FakeDotnetTestSdkMultiConnection.RunAsync(
            testHost,
            extraArguments: arguments,
            environmentVariables: new()
            {
                { EnvironmentVariableConstants.TESTINGPLATFORM_TELEMETRY_OPTOUT, "1" },
                { "RETRY_MARKER_DIRECTORY", resultDirectory },
                { "COMPOSED_SELECTION_ORCHESTRATOR", "1" },
            },
            cancellationToken: TestContext.CancellationToken);

        result.TestHostResult.AssertExitCodeIs(ExitCode.Success);

        Assert.AreSequenceEqual(
            new[] { RetryOrchestratorFeature, SelectionOrchestratorFeature },
            result.HandshakesWithHostType(TestHostOrchestratorHostType)
                .Select(handshake => handshake[DotnetTestPipeProtocol.HandshakeProperties.OrchestratorFeature]).Order(StringComparer.Ordinal));
        Assert.HasCount(useController ? 2 : 0, result.HandshakesWithHostType("TestHostController"));
        Assert.ContainsSingle(result.ReceivedHandshakes.Select(h => h[DotnetTestPipeProtocol.HandshakeProperties.ExecutionId]).Distinct());
        Assert.HasCount(result.ReceivedHandshakes.Count, result.ReceivedHandshakes.Select(h => h[DotnetTestPipeProtocol.HandshakeProperties.InstanceId]).Distinct());

        Dictionary<byte, string>[] testHostHandshakes =
        [
            .. result.HandshakesWithHostType(TestHostHostType)
                .OrderBy(handshake => int.Parse(handshake[DotnetTestPipeProtocol.HandshakeProperties.AttemptNumber], CultureInfo.InvariantCulture)),
        ];
        Assert.HasCount(2, testHostHandshakes);
        Assert.AreEqual("1", testHostHandshakes[0][DotnetTestPipeProtocol.HandshakeProperties.AttemptNumber]);
        Assert.AreEqual("2", testHostHandshakes[1][DotnetTestPipeProtocol.HandshakeProperties.AttemptNumber]);
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
using Microsoft.Testing.Platform.Extensions.TestHostOrchestrator;
using Microsoft.Testing.Platform.Extensions.TestFramework;

#pragma warning disable TPEXP // The asset deliberately exercises the experimental orchestrator-composition API.

public class Program
{
    public static async Task<int> Main(string[] args)
    {
        ITestApplicationBuilder builder = await TestApplication.CreateBuilderAsync(args);
        builder.RegisterTestFramework(_ => new TestFrameworkCapabilities(), (_, __) => new DummyTestFramework());
        builder.TestHostOrchestrator.AddTestHostOrchestrator(_ => new SelectionOrchestrator());
        builder.AddRetryProvider();
        using ITestApplication app = await builder.BuildAsync();
        return await app.RunAsync();
    }
}

public class SelectionOrchestrator : IComposableTestHostExecutionOrchestrator
{
    public const string UidValue = "SelectionOrchestrator";

    public string Uid => UidValue;
    public string Version => "1.0.0";
    public string DisplayName => UidValue;
    public string Description => UidValue;
    public TestHostExecutionOrchestratorStage Stage => TestHostExecutionOrchestratorStage.Selection;

    public Task<bool> IsEnabledAsync()
        => Task.FromResult(Environment.GetEnvironmentVariable("COMPOSED_SELECTION_ORCHESTRATOR") == "1");

    public async Task<int> OrchestrateTestHostExecutionAsync(CancellationToken cancellationToken)
    {
        if (Environment.GetEnvironmentVariable("COMPOSED_SELECTION_STARTED") == "1")
        {
            throw new InvalidOperationException("The child reactivated the consumed Selection orchestrator.");
        }

        string executablePath = Environment.ProcessPath
            ?? throw new InvalidOperationException("The current test application executable path is unavailable.");
        System.Diagnostics.ProcessStartInfo startInfo = new(executablePath)
        {
            UseShellExecute = false,
        };
        startInfo.Environment["COMPOSED_SELECTION_STARTED"] = "1";
        if (Path.GetFileNameWithoutExtension(executablePath).Equals("dotnet", StringComparison.OrdinalIgnoreCase))
        {
            startInfo.ArgumentList.Add(typeof(Program).Assembly.Location);
        }
        foreach (string argument in Environment.GetCommandLineArgs().Skip(1))
        {
            startInfo.ArgumentList.Add(argument);
        }

        using System.Diagnostics.Process child = System.Diagnostics.Process.Start(startInfo)
            ?? throw new InvalidOperationException("The child test application process could not be started.");
        try
        {
            await child.WaitForExitAsync(cancellationToken);
            return child.ExitCode;
        }
        catch (OperationCanceledException)
        {
            if (!child.HasExited)
            {
                child.Kill(entireProcessTree: true);
                await child.WaitForExitAsync();
            }

            throw;
        }
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
        if (Environment.GetEnvironmentVariable("TESTINGPLATFORM_TESTHOSTORCHESTRATORCHAIN") is not null)
        {
            throw new InvalidOperationException("The test host leaked the consumed orchestrator chain to test code.");
        }

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
