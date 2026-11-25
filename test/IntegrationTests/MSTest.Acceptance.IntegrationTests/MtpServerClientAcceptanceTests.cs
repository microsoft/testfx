// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

extern alias serverclient;

using System.Diagnostics;
using System.Xml.Linq;

using Microsoft.Testing.Platform.Acceptance.IntegrationTests;
using Microsoft.Testing.Platform.Acceptance.IntegrationTests.Helpers;
using Microsoft.Testing.Platform.Helpers;

using serverclient::Microsoft.Testing.Platform.ServerMode.Client;

namespace MSTest.Acceptance.IntegrationTests;

/// <summary>
/// End-to-end acceptance test for the source-only MTP server-mode client
/// (<c>Microsoft.Testing.Platform.ServerMode.Client.Sources</c>). It launches a real, generated
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
    private const string CleanupAssetName = "MtpServerClientCleanupAsset";

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
            await WaitForServerExitAsync(client, cancellationToken);

            // The non-blocking teardown on the external-process path: it shares one teardown with Dispose, so
            // the trailing Dispose from the using block joins the same (already finished) work.
            await client.ShutdownAsync();
            Assert.AreEqual(0, client.ServerExitCode, "The launched process must have exited cleanly once shutdown completed.");
        }
    }

    [TestMethod]
    [DataRow(null)]
    [DataRow(false)]
    [DataRow(true)]
    public async Task ClassCleanupFailure_PreservesTestOutcomeAndProjectsForClientCapability(bool? supportsTestNodeHierarchy)
    {
        CancellationToken cancellationToken = TestContext.CancellationToken;
        string source = CleanupAssetSources
            .PatchTargetFrameworks([TargetFrameworks.NetCurrent])
            .PatchCodeWithReplace("$MSTestVersion$", MSTestVersion)
            .PatchCodeWithReplace("$MicrosoftTestingPlatformVersion$", MicrosoftTestingPlatformVersion);
        using TestAsset testAsset = await TestAsset.GenerateAssetAsync(CleanupAssetName, source);

        DotnetMuxerResult build = await DotnetCli.RunAsync(
            $"build \"{testAsset.TargetAssetPath}\" -c Release",
            failIfReturnValueIsNotZero: false,
            cancellationToken: cancellationToken);
        Assert.AreEqual(
            0,
            build.ExitCode,
            $"The cleanup test app failed to build. Build output:\n{build.StandardOutput}\n{build.StandardError}");

        string app = TestHost.LocateFrom(testAsset.TargetAssetPath, CleanupAssetName, TargetFrameworks.NetCurrent).FullName;
        MtpServerClientOptions options = CreateOptions(supportsTestNodeHierarchy);
        options.EnvironmentVariables["MTP_FAIL_CLASS_CLEANUP"] = "1";
        List<MtpTestNodeUpdate> updates = [];
        object gate = new();

        using var client = MtpServerClient.Launch(app, options);
        client.TestNodesUpdated += (_, e) =>
        {
            lock (gate)
            {
                updates.AddRange(e.Changes);
            }
        };

        _ = await client.InitializeAsync(cancellationToken);
        _ = await client.RunTestsAsync(cancellationToken);

        List<MtpTestNodeUpdate> snapshot;
        lock (gate)
        {
            snapshot = [.. updates];
        }

        MtpTestNodeUpdate testResult = Assert.ContainsSingle(
            node => node.NodeType == "action"
                && node.DisplayName == ExpectedTestDisplayName
                && node.ExecutionState == "passed",
            snapshot);
        MtpTestNodeUpdate cleanupFailure = Assert.ContainsSingle(
            node => node.ExecutionState == "failed"
                && node.ErrorMessage?.Contains("class cleanup failed sentinel", StringComparison.Ordinal) == true,
            snapshot);
        Assert.AreNotEqual(testResult.Uid, cleanupFailure.Uid, "The fixture failure must have an identity distinct from the test result.");

        if (supportsTestNodeHierarchy == true)
        {
            Assert.AreEqual("group", cleanupFailure.NodeType);
            MtpTestNodeUpdate assemblyNode = Assert.ContainsSingle(
                node => node.NodeType == "group"
                    && node.ParentUid is null
                    && node.Uid!.StartsWith("mstest-assembly:", StringComparison.Ordinal),
                snapshot);
            Assert.AreEqual(assemblyNode.Uid, cleanupFailure.ParentUid);
        }
        else
        {
            Assert.AreEqual("action", cleanupFailure.NodeType);
            Assert.IsNull(cleanupFailure.ParentUid);
            Assert.DoesNotContain(node => node.NodeType == "group", snapshot);
        }

        await client.ExitAsync(cancellationToken);
        await WaitForServerExitAsync(client, cancellationToken);
        await client.ShutdownAsync();
        Assert.AreEqual(
            (int)ExitCode.Success,
            client.ServerExitCode,
            "The server process exits successfully after orderly shutdown; the cleanup failure is reported through the test-node update.");
    }

    [TestMethod]
    [DataRow(null)]
    [DataRow(false)]
    [DataRow(true)]
    public async Task AssemblyCleanupFailure_PreservesTestOutcomeAndProjectsForClientCapability(bool? supportsTestNodeHierarchy)
    {
        CancellationToken cancellationToken = TestContext.CancellationToken;
        string source = CleanupAssetSources
            .PatchTargetFrameworks([TargetFrameworks.NetCurrent])
            .PatchCodeWithReplace("$MSTestVersion$", MSTestVersion)
            .PatchCodeWithReplace("$MicrosoftTestingPlatformVersion$", MicrosoftTestingPlatformVersion);
        using TestAsset testAsset = await TestAsset.GenerateAssetAsync(CleanupAssetName, source);

        DotnetMuxerResult build = await DotnetCli.RunAsync(
            $"build \"{testAsset.TargetAssetPath}\" -c Release",
            failIfReturnValueIsNotZero: false,
            cancellationToken: cancellationToken);
        Assert.AreEqual(
            0,
            build.ExitCode,
            $"The cleanup test app failed to build. Build output:\n{build.StandardOutput}\n{build.StandardError}");

        string app = TestHost.LocateFrom(testAsset.TargetAssetPath, CleanupAssetName, TargetFrameworks.NetCurrent).FullName;
        MtpServerClientOptions options = CreateOptions(supportsTestNodeHierarchy);
        options.EnvironmentVariables["MTP_FAIL_ASSEMBLY_CLEANUP"] = "1";
        List<MtpTestNodeUpdate> updates = [];
        object gate = new();

        using var client = MtpServerClient.Launch(app, options);
        client.TestNodesUpdated += (_, e) =>
        {
            lock (gate)
            {
                updates.AddRange(e.Changes);
            }
        };

        _ = await client.InitializeAsync(cancellationToken);
        _ = await client.RunTestsAsync(cancellationToken);

        List<MtpTestNodeUpdate> snapshot;
        lock (gate)
        {
            snapshot = [.. updates];
        }

        MtpTestNodeUpdate testResult = Assert.ContainsSingle(
            node => node.NodeType == "action"
                && node.DisplayName == ExpectedTestDisplayName
                && node.ExecutionState == "passed",
            snapshot);
        MtpTestNodeUpdate cleanupFailure = Assert.ContainsSingle(
            node => node.ExecutionState == "failed"
                && node.ErrorMessage?.Contains("assembly cleanup failed sentinel", StringComparison.Ordinal) == true,
            snapshot);
        Assert.AreNotEqual(testResult.Uid, cleanupFailure.Uid, "The fixture failure must have an identity distinct from the test result.");
        Assert.AreEqual($"{CleanupAssetName} AssemblyCleanup", cleanupFailure.DisplayName);

        if (supportsTestNodeHierarchy == true)
        {
            Assert.AreEqual("group", cleanupFailure.NodeType);
            Assert.IsTrue(cleanupFailure.Uid!.StartsWith("mstest-assembly:", StringComparison.Ordinal));
            Assert.IsNull(cleanupFailure.ParentUid);
            MtpTestNodeUpdate classNode = Assert.ContainsSingle(
                node => node.NodeType == "group"
                    && node.DisplayName == "CleanupTests"
                    && node.ParentUid == cleanupFailure.Uid,
                snapshot);
            Assert.AreEqual(classNode.Uid, testResult.ParentUid);
        }
        else
        {
            Assert.AreEqual("action", cleanupFailure.NodeType);
            Assert.IsNull(cleanupFailure.ParentUid);
            Assert.IsNull(testResult.ParentUid);
            Assert.DoesNotContain(node => node.NodeType == "group", snapshot);
        }

        await client.ExitAsync(cancellationToken);
        await WaitForServerExitAsync(client, cancellationToken);
        await client.ShutdownAsync();
        Assert.AreEqual(
            (int)ExitCode.Success,
            client.ServerExitCode,
            "The server process exits successfully after orderly shutdown; the cleanup failure is reported through the test-node update.");
    }

    [TestMethod]
    public async Task ClassCleanupFailure_ProducesSeparateFailedTrxResult()
    {
        CancellationToken cancellationToken = TestContext.CancellationToken;
        string source = CleanupAssetSources
            .PatchTargetFrameworks([TargetFrameworks.NetCurrent])
            .PatchCodeWithReplace("$MSTestVersion$", MSTestVersion)
            .PatchCodeWithReplace("$MicrosoftTestingPlatformVersion$", MicrosoftTestingPlatformVersion);
        using TestAsset testAsset = await TestAsset.GenerateAssetAsync(CleanupAssetName, source);

        DotnetMuxerResult build = await DotnetCli.RunAsync(
            $"build \"{testAsset.TargetAssetPath}\" -c Release",
            failIfReturnValueIsNotZero: false,
            cancellationToken: cancellationToken);
        Assert.AreEqual(
            0,
            build.ExitCode,
            $"The cleanup test app failed to build. Build output:\n{build.StandardOutput}\n{build.StandardError}");

        string resultsDirectory = Path.Combine(testAsset.TargetAssetPath, "results");
        Directory.CreateDirectory(resultsDirectory);
        var testHost = TestHost.LocateFrom(testAsset.TargetAssetPath, CleanupAssetName, TargetFrameworks.NetCurrent);
        TestHostResult execution = await testHost.ExecuteAsync(
            $"--report-trx --report-trx-filename cleanup.trx --results-directory \"{resultsDirectory}\"",
            new() { ["MTP_FAIL_CLASS_CLEANUP"] = "1" },
            cancellationToken: cancellationToken);
        execution.AssertExitCodeIs(ExitCode.AtLeastOneTestFailed);

        string trxFile = Path.Combine(resultsDirectory, "cleanup.trx");
        Assert.IsTrue(File.Exists(trxFile), $"The child run did not produce its TRX report.\n{execution}");
        XNamespace ns = "http://microsoft.com/schemas/VisualStudio/TeamTest/2010";
        var trx = XDocument.Load(trxFile);
        XElement[] results = [.. trx.Descendants(ns + "UnitTestResult")];
        Assert.HasCount(2, results);

        XElement testResult = Assert.ContainsSingle(
            result => result.Attribute("testName")?.Value == ExpectedTestDisplayName,
            results);
        Assert.AreEqual("Passed", testResult.Attribute("outcome")?.Value);

        XElement cleanupResult = Assert.ContainsSingle(
            result => result.Attribute("testName")?.Value == "[CleanupTests ClassCleanup]",
            results);
        Assert.AreEqual("Failed", cleanupResult.Attribute("outcome")?.Value);
        Assert.Contains(
            "class cleanup failed sentinel",
            string.Join(" ", cleanupResult.Descendants(ns + "Message").Select(message => message.Value)));
    }

    [TestMethod]
    public async Task AssemblyCleanupFailure_ProducesSeparateFailedTrxResult()
    {
        CancellationToken cancellationToken = TestContext.CancellationToken;
        string source = CleanupAssetSources
            .PatchTargetFrameworks([TargetFrameworks.NetCurrent])
            .PatchCodeWithReplace("$MSTestVersion$", MSTestVersion)
            .PatchCodeWithReplace("$MicrosoftTestingPlatformVersion$", MicrosoftTestingPlatformVersion);
        using TestAsset testAsset = await TestAsset.GenerateAssetAsync(CleanupAssetName, source);

        DotnetMuxerResult build = await DotnetCli.RunAsync(
            $"build \"{testAsset.TargetAssetPath}\" -c Release",
            failIfReturnValueIsNotZero: false,
            cancellationToken: cancellationToken);
        Assert.AreEqual(
            0,
            build.ExitCode,
            $"The cleanup test app failed to build. Build output:\n{build.StandardOutput}\n{build.StandardError}");

        string resultsDirectory = Path.Combine(testAsset.TargetAssetPath, "results");
        Directory.CreateDirectory(resultsDirectory);
        var testHost = TestHost.LocateFrom(testAsset.TargetAssetPath, CleanupAssetName, TargetFrameworks.NetCurrent);
        TestHostResult execution = await testHost.ExecuteAsync(
            $"--report-trx --report-trx-filename cleanup.trx --results-directory \"{resultsDirectory}\"",
            new() { ["MTP_FAIL_ASSEMBLY_CLEANUP"] = "1" },
            cancellationToken: cancellationToken);
        execution.AssertExitCodeIs(ExitCode.AtLeastOneTestFailed);

        string trxFile = Path.Combine(resultsDirectory, "cleanup.trx");
        Assert.IsTrue(File.Exists(trxFile), $"The child run did not produce its TRX report.\n{execution}");
        XNamespace ns = "http://microsoft.com/schemas/VisualStudio/TeamTest/2010";
        var trx = XDocument.Load(trxFile);
        XElement[] results = [.. trx.Descendants(ns + "UnitTestResult")];
        Assert.HasCount(2, results);

        XElement testResult = Assert.ContainsSingle(
            result => result.Attribute("testName")?.Value == ExpectedTestDisplayName,
            results);
        Assert.AreEqual("Passed", testResult.Attribute("outcome")?.Value);

        XElement cleanupResult = Assert.ContainsSingle(
            result => result.Attribute("testName")?.Value == $"{CleanupAssetName} AssemblyCleanup",
            results);
        Assert.AreEqual("Failed", cleanupResult.Attribute("outcome")?.Value);
        Assert.Contains(
            "assembly cleanup failed sentinel",
            string.Join(" ", cleanupResult.Descendants(ns + "Message").Select(message => message.Value)));

        XElement counters = Assert.ContainsSingle(trx.Descendants(ns + "Counters"));
        Assert.AreEqual("2", counters.Attribute("total")?.Value);
        Assert.AreEqual("1", counters.Attribute("passed")?.Value);
        Assert.AreEqual("1", counters.Attribute("failed")?.Value);
    }

    [TestMethod]
    [DynamicData(nameof(TargetFrameworks.AllForDynamicData), typeof(TargetFrameworks))]
    public async Task ShutdownAsync_WhileExternalTeardownBlocks_ReturnsImmediatelyAndHidesForcedExitCode(string tfm)
    {
        CancellationToken cancellationToken = TestContext.CancellationToken;
        string source = TestHost.LocateFrom(AssetFixture.TargetAssetPath, AssetName, tfm).FullName;
        MtpServerClientOptions options = CreateOptions();
        options.EnvironmentVariables["MTP_SERVER_BLOCK_AFTER_RUN"] = "1";

        using var client = MtpServerClient.Launch(source, options);
        _ = await client.InitializeAsync(cancellationToken);

        var handlerEntered = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseHandler = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        client.TestNodesUpdated += (_, _) =>
        {
            handlerEntered.TrySetResult(true);
            releaseHandler.Task.GetAwaiter().GetResult();
        };

        Task discover = client.DiscoverTestsAsync(cancellationToken);
        await handlerEntered.Task.WaitAsync(cancellationToken);

        try
        {
            var stopwatch = Stopwatch.StartNew();
            Task shutdown = client.ShutdownAsync();
            stopwatch.Stop();

            Assert.IsLessThan(
                TimeSpan.FromSeconds(2),
                stopwatch.Elapsed,
                $"Calling ShutdownAsync took {stopwatch.Elapsed.TotalMilliseconds:F0} ms; external-process teardown must run asynchronously.");

            _ = await Assert.ThrowsExactlyAsync<ObjectDisposedException>(() => discover);

            // Keep the handler blocked until teardown finishes so this exercises the connection's bounded
            // read-loop wait rather than only proving that Task.Run returns quickly for an immediate dispose.
            await shutdown;
            Assert.IsNull(client.ServerExitCode, "Forced process termination must not surface the operating system's kill status as an application exit code.");
        }
        finally
        {
            _ = releaseHandler.TrySetResult(true);
        }
    }

    private static async Task WaitForServerExitAsync(MtpServerClient client, CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        while (client.ServerExitCode is null && stopwatch.Elapsed < TimeSpan.FromSeconds(30))
        {
            await Task.Delay(100, cancellationToken);
        }

        Assert.IsNotNull(client.ServerExitCode, "The launched process did not exit after receiving the exit notification.");
    }

    private static string Describe(IReadOnlyList<MtpTestNodeUpdate> nodes)
        => nodes.Count == 0
            ? "<none>"
            : string.Join(", ", nodes.Select(n => $"[uid={n.Uid}, name={n.DisplayName}, type={n.NodeType}, state={n.ExecutionState}]"));

    private static MtpServerClientOptions CreateOptions(bool? supportsTestNodeHierarchy = null)
    {
        MtpServerClientOptions options = new()
        {
            SupportsTestNodeHierarchy = supportsTestNodeHierarchy,
        };

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

    private const string CleanupAssetSources = """
#file MtpServerClientCleanupAsset.csproj
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
    <PackageReference Include="Microsoft.Testing.Extensions.TrxReport" Version="$MicrosoftTestingPlatformVersion$" />
    <PackageReference Include="MSTest.TestAdapter" Version="$MSTestVersion$" />
    <PackageReference Include="MSTest.TestFramework" Version="$MSTestVersion$" />
  </ItemGroup>
</Project>

#file Program.cs
using System.Reflection;
using System.Threading.Tasks;

using Microsoft.Testing.Extensions;
using Microsoft.Testing.Platform.Builder;
using Microsoft.VisualStudio.TestTools.UnitTesting;

internal sealed class Program
{
    public static async Task<int> Main(string[] args)
    {
        ITestApplicationBuilder builder = await TestApplication.CreateBuilderAsync(args);
        builder.AddMSTest(() => [Assembly.GetEntryAssembly()!]);
        builder.AddTrxReportProvider();
        using ITestApplication app = await builder.BuildAsync();
        return await app.RunAsync();
    }
}

#file CleanupTests.cs
using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;

[TestClass]
public sealed class CleanupTests
{
    [TestMethod]
    public void TestMethod1()
    {
    }

    [ClassCleanup]
    public static void ClassCleanup()
    {
        if (Environment.GetEnvironmentVariable("MTP_FAIL_CLASS_CLEANUP") == "1")
        {
            throw new InvalidOperationException("class cleanup failed sentinel");
        }
    }

    [AssemblyCleanup]
    public static void AssemblyCleanup()
    {
        if (Environment.GetEnvironmentVariable("MTP_FAIL_ASSEMBLY_CLEANUP") == "1")
        {
            throw new InvalidOperationException("assembly cleanup failed sentinel");
        }
    }
}
""";

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
        int exitCode = await app.RunAsync();
        if (Environment.GetEnvironmentVariable("MTP_SERVER_BLOCK_AFTER_RUN") == "1")
        {
            await Task.Delay(System.Threading.Timeout.Infinite);
        }

        return exitCode;
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
