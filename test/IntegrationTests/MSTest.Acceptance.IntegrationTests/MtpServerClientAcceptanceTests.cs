// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

extern alias serverclient;

using Microsoft.Testing.Platform.Acceptance.IntegrationTests;

using serverclient::Microsoft.Testing.Platform.ServerMode.Client;

namespace MSTest.Acceptance.IntegrationTests;

/// <summary>
/// End-to-end acceptance test for the source-only MTP server-mode client
/// (<c>Microsoft.Testing.Platform.ServerClient</c>). It launches a real, generated
/// Microsoft.Testing.Platform application in server mode through the package's own
/// <see cref="MtpServerClient"/> and asserts the discovered and executed test nodes that come back
/// over the wire. This proves the client against the actual server (not the in-memory fake used by the
/// package unit tests) and exercises the .NET System.Text.Json formatter path end to end.
/// </summary>
[TestClass]
public sealed class MtpServerClientAcceptanceTests : AcceptanceTestBase<MtpServerClientAcceptanceTests.TestAssetFixture>
{
    private const string AssetName = "MtpServerClientAsset";
    private const string ExpectedTestDisplayName = "TestMethod1";

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    [DynamicData(nameof(TargetFrameworks.AllForDynamicData), typeof(TargetFrameworks))]
    public async Task DiscoverAndRun_ViaSourcePackageClient_ReportsExpectedTestNode(string tfm)
    {
        CancellationToken cancellationToken = TestContext.CancellationToken;
        string source = TestHost.LocateFrom(AssetFixture.TargetAssetPath, AssetName, tfm).FullName;

        // Session 1: initialize + discover. The client owns the launched process; disposing it tears the
        // process down. Two independent launches (discover, then run) keep the collected node sets isolated
        // and avoid depending on a single non-stateful server serving two operations.
        List<MtpTestNodeUpdate> discovered = [];
        object discoverGate = new();
        using (var client = MtpServerClient.Launch(source, CreateOptions()))
        {
            client.TestNodesUpdated += (_, e) =>
            {
                lock (discoverGate)
                {
                    discovered.AddRange(e.Changes);
                }
            };

            MtpServerCapabilities capabilities = await client.InitializeAsync(cancellationToken);
            Assert.IsTrue(capabilities.SupportsDiscovery, "The server should advertise discovery support in its initialize response.");

            await client.DiscoverTestsAsync(cancellationToken);

            List<MtpTestNodeUpdate> snapshot;
            lock (discoverGate)
            {
                snapshot = [.. discovered];
            }

            Assert.ContainsSingle(
                n => n.NodeType == "action" && n.DisplayName == ExpectedTestDisplayName && n.ExecutionState == "discovered",
                snapshot,
                $"Expected exactly one discovered action node named '{ExpectedTestDisplayName}'. Collected: {Describe(snapshot)}");

            await client.ExitAsync(cancellationToken);
        }

        // Session 2: initialize + run.
        List<MtpTestNodeUpdate> executed = [];
        object runGate = new();
        using (var client = MtpServerClient.Launch(source, CreateOptions()))
        {
            client.TestNodesUpdated += (_, e) =>
            {
                lock (runGate)
                {
                    executed.AddRange(e.Changes);
                }
            };

            _ = await client.InitializeAsync(cancellationToken);
            _ = await client.RunTestsAsync(cancellationToken);

            List<MtpTestNodeUpdate> snapshot;
            lock (runGate)
            {
                snapshot = [.. executed];
            }

            Assert.ContainsSingle(
                n => n.NodeType == "action" && n.DisplayName == ExpectedTestDisplayName && n.ExecutionState == "passed",
                snapshot,
                $"Expected exactly one passed action node named '{ExpectedTestDisplayName}'. Collected: {Describe(snapshot)}");

            await client.ExitAsync(cancellationToken);
        }
    }

    private static string Describe(IReadOnlyList<MtpTestNodeUpdate> nodes)
        => nodes.Count == 0
            ? "<none>"
            : string.Join(", ", nodes.Select(n => $"[uid={n.Uid}, name={n.DisplayName}, type={n.NodeType}, state={n.ExecutionState}]"));

    private static MtpServerClientOptions CreateOptions()
    {
        MtpServerClientOptions options = new();

        // Neutralize inherited environment that would otherwise change the child host's behavior (dump
        // generation, telemetry opt-out, banners, ambient LLM/agent detection, ...). The client only adds to
        // the inherited environment, so we overwrite each well-known variable with an empty value; MTP treats
        // an empty value as "not set" for the switches that matter here.
        foreach (string variable in WellKnownEnvironmentVariables.ToSkipEnvironmentVariables)
        {
            options.EnvironmentVariables[variable] = string.Empty;
        }

        string dotnetRoot = $"{RootFinder.Find()}/.dotnet";
        options.EnvironmentVariables["DOTNET_ROOT"] = dotnetRoot;
        options.EnvironmentVariables["DOTNET_INSTALL_DIR"] = dotnetRoot;
        options.EnvironmentVariables["DOTNET_SKIP_FIRST_TIME_EXPERIENCE"] = "1";
        options.EnvironmentVariables["DOTNET_MULTILEVEL_LOOKUP"] = "0";

        // Let unhandled exceptions surface as a non-zero exit code instead of killing the process abruptly,
        // matching the reference server-mode harness.
        options.EnvironmentVariables["TESTINGPLATFORM_EXIT_PROCESS_ON_UNHANDLED_EXCEPTION"] = "0";

        return options;
    }

    public sealed class TestAssetFixture() : TestAssetFixtureBase()
    {
        private const string Sources = """
#file MtpServerClientAsset.csproj
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFrameworks>$TargetFrameworks$</TargetFrameworks>
    <OutputType>Exe</OutputType>
    <UseAppHost>true</UseAppHost>
    <Nullable>enable</Nullable>
    <LangVersion>preview</LangVersion>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.Testing.Platform" Version="$MicrosoftTestingPlatformVersion$" />
  </ItemGroup>
</Project>

#file Program.cs
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using Microsoft.Testing.Platform.Builder;
using Microsoft.Testing.Platform.Capabilities;
using Microsoft.Testing.Platform.Capabilities.TestFramework;
using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Extensions.TestFramework;
using Microsoft.Testing.Platform.Requests;

internal sealed class Program
{
    public static async Task<int> Main(string[] args)
    {
        ITestApplicationBuilder builder = await TestApplication.CreateBuilderAsync(args);
        builder.RegisterTestFramework(_ => new Capabilities(), (_, __) => new DummyTestFramework());
        using ITestApplication app = await builder.BuildAsync();
        return await app.RunAsync();
    }
}

// A minimal test framework that publishes a single, deterministic test node so the client-side
// assertions have an exact uid/display-name/state to match. On a discovery request the node reports the
// 'discovered' state; on a run request it reports 'passed'.
internal sealed class DummyTestFramework : ITestFramework, IDataProducer
{
    public string Uid => nameof(DummyTestFramework);

    public string Version => string.Empty;

    public string DisplayName => string.Empty;

    public string Description => string.Empty;

    public Type[] DataTypesProduced => new[] { typeof(TestNodeUpdateMessage) };

    public Task<CloseTestSessionResult> CloseTestSessionAsync(CloseTestSessionContext context)
        => Task.FromResult(new CloseTestSessionResult() { IsSuccess = true });

    public Task<CreateTestSessionResult> CreateTestSessionAsync(CreateTestSessionContext context)
        => Task.FromResult(new CreateTestSessionResult() { IsSuccess = true });

    public async Task ExecuteRequestAsync(ExecuteRequestContext context)
    {
        IProperty state = context.Request is DiscoverTestExecutionRequest
            ? DiscoveredTestNodeStateProperty.CachedInstance
            : (IProperty)PassedTestNodeStateProperty.CachedInstance;

        await context.MessageBus.PublishAsync(this, new TestNodeUpdateMessage(
            context.Request.Session.SessionUid,
            new TestNode() { Uid = "TestMethod1", DisplayName = "TestMethod1", Properties = new(state) }));
        context.Complete();
    }

    public Task<bool> IsEnabledAsync() => Task.FromResult(true);
}

internal sealed class Capabilities : ITestFrameworkCapabilities
{
    IReadOnlyCollection<ITestFrameworkCapability> ICapabilities<ITestFrameworkCapability>.Capabilities => Array.Empty<ITestFrameworkCapability>();
}
""";

        public string TargetAssetPath => GetAssetPath(AssetName);

        public override (string ID, string Name, string Code) GetAssetsToGenerate()
            => (AssetName, AssetName,
                Sources
                .PatchTargetFrameworks(TargetFrameworks.All)
                .PatchCodeWithReplace("$MicrosoftTestingPlatformVersion$", MicrosoftTestingPlatformVersion));
    }
}
