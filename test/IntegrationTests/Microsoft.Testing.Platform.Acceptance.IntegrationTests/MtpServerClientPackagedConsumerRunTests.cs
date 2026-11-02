// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.Acceptance.IntegrationTests;

/// <summary>
/// The durable ship gate for the source-only <c>Microsoft.Testing.Platform.ServerMode.Client.Sources</c> package: it
/// proves that a real adopter can consume the <em>packed</em> package and actually drive an MTP app end to end.
/// <para>
/// The other tests each cover one half of that promise but not the whole: <see cref="MtpServerClientSourcePackageTests"/>
/// inspects the <c>.nupkg</c> textually, <see cref="MtpServerClientSourcePackageConsumerTests"/> proves the packed
/// source <em>compiles</em> in a hostile consumer, and the P4 acceptance test drives the client via a
/// <c>ProjectReference</c> (not the package). This test closes the loop the way vstest actually ships:
/// </para>
/// <list type="number">
///   <item>generate a throwaway consumer app that references the packed package by <c>PackageReference</c>
///   (hostile settings — no implicit usings, nullable on, warnings as errors) so the client source is injected
///   into and compiled <em>into</em> the consumer's own assembly, exactly as it will be in vstest;</item>
///   <item>build it (compile proof) against an isolated NuGet cache so a stale same-version drop cannot mask a
///   fresh pack;</item>
///   <item>run the built apphost against a second generated app — a real Microsoft.Testing.Platform test app —
///   and assert, via the consumer's own exit code and stdout markers, that it discovered then ran the single
///   expected node over the JSON-RPC wire.</item>
/// </list>
/// The end-to-end run covers the net8.0 (System.Text.Json) axis vstest's .NET runner uses. A Unix-only net6.0
/// run additionally proves that the down-level package slice retries through the muxer when the sibling apphost
/// exists but the current process cannot execute it. The package must have been produced first (build with
/// <c>-pack</c>); otherwise the test fails with a hint.
/// </summary>
[TestClass]
public sealed class MtpServerClientPackagedConsumerRunTests : AcceptanceTestBase<NopAssetFixture>
{
    private const string AssetName = "MtpServerClientPackagedRun";
    private const string PackageId = "Microsoft.Testing.Platform.ServerMode.Client.Sources";
    private const string ExpectedTestDisplayName = "TestMethod1";

    // The System.Text.Json axis vstest's .NET runner consumes. P4 already proves the net462-Jsonite-server <->
    // net-STJ-client cross-formatter leg on the real wire; here we focus on compile + run through a real
    // PackageReference, which is the exact shape of the vstest adoption.
    private const string Net6Tfm = "net6.0";
    private const string Net8Tfm = "net8.0";

    private const string Sources = """
#file PackagedConsumer/PackagedConsumer.csproj
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>$ConsumerTfm$</TargetFramework>
    <OutputType>Exe</OutputType>
    <UseAppHost>true</UseAppHost>
    <!-- Hostile consumer, mirroring vstest: the injected source must be self-contained and warning-clean. -->
    <ImplicitUsings>disable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
    <LangVersion>12.0</LangVersion>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.Testing.Platform.ServerMode.Client.Sources" Version="$ServerClientSourceVersion$" />
  </ItemGroup>
</Project>

#file PackagedConsumer/Program.cs
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Testing.Platform.ServerMode.Client;

namespace PackagedConsumer;

// Compiled entirely from the packed source-only client (the PackageReference above), so the internal client
// types are injected into and built into THIS assembly and driven directly — exactly what a real adopter such
// as vstest ships. It launches the sibling DummyApp (a real Microsoft.Testing.Platform app) in server mode,
// discovers then runs its single node over the JSON-RPC wire, verifies the exact node, and reports the outcome
// only through the process exit code plus a few parseable marker lines (keeping the acceptance test host-agnostic).
internal static class Program
{
    private const string ExpectedDisplayName = "TestMethod1";

    public static async Task<int> Main(string[] args)
    {
        if (args.Length < 1)
        {
            Console.Error.WriteLine("PACKAGEDCONSUMER: missing child application path argument.");
            return 2;
        }

        string source = args[0];
        using CancellationTokenSource cts = new(TimeSpan.FromSeconds(90));
        CancellationToken cancellationToken = cts.Token;

        try
        {
            if (!await DiscoverAsync(source, cancellationToken))
            {
                return 1;
            }

            if (!await RunAsync(source, cancellationToken))
            {
                return 1;
            }

            Console.WriteLine("PACKAGEDCONSUMER: OK");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("PACKAGEDCONSUMER: FAILED " + ex);
            return 1;
        }
    }

    private static async Task<bool> DiscoverAsync(string source, CancellationToken cancellationToken)
    {
        List<MtpTestNodeUpdate> discovered = new();
        object gate = new();
        using MtpServerClient client = await MtpServerClient.LaunchAsync(source, CreateOptions(), cancellationToken);
        client.TestNodesUpdated += (_, e) =>
        {
            lock (gate)
            {
                discovered.AddRange(e.Changes);
            }
        };

        MtpServerCapabilities capabilities = await client.InitializeAsync(cancellationToken);
        if (!capabilities.SupportsDiscovery)
        {
            Console.Error.WriteLine("PACKAGEDCONSUMER: server did not advertise discovery support.");
            return false;
        }

        await client.DiscoverTestsAsync(cancellationToken);

        List<MtpTestNodeUpdate> snapshot;
        lock (gate)
        {
            snapshot = discovered.ToList();
        }

        int count = snapshot.Count(n => n.NodeType == "action" && n.DisplayName == ExpectedDisplayName && n.ExecutionState == "discovered");
        if (count != 1)
        {
            Console.Error.WriteLine(FormattableString.Invariant($"PACKAGEDCONSUMER: expected exactly one discovered '{ExpectedDisplayName}', got {count}. {Describe(snapshot)}"));
            return false;
        }

        Console.WriteLine("PACKAGEDCONSUMER: DISCOVERED " + ExpectedDisplayName);
        await client.ExitAsync(cancellationToken);
        return true;
    }

    private static async Task<bool> RunAsync(string source, CancellationToken cancellationToken)
    {
        List<MtpTestNodeUpdate> executed = new();
        object gate = new();
        using MtpServerClient client = await MtpServerClient.LaunchAsync(source, CreateOptions(), cancellationToken);
        client.TestNodesUpdated += (_, e) =>
        {
            lock (gate)
            {
                executed.AddRange(e.Changes);
            }
        };

        _ = await client.InitializeAsync(cancellationToken);
        _ = await client.RunTestsAsync(cancellationToken);

        List<MtpTestNodeUpdate> snapshot;
        lock (gate)
        {
            snapshot = executed.ToList();
        }

        int count = snapshot.Count(n => n.NodeType == "action" && n.DisplayName == ExpectedDisplayName && n.ExecutionState == "passed");
        if (count != 1)
        {
            Console.Error.WriteLine(FormattableString.Invariant($"PACKAGEDCONSUMER: expected exactly one passed '{ExpectedDisplayName}', got {count}. {Describe(snapshot)}"));
            return false;
        }

        Console.WriteLine("PACKAGEDCONSUMER: EXECUTED " + ExpectedDisplayName);
        await client.ExitAsync(cancellationToken);
        return true;
    }

    private static string Describe(IReadOnlyList<MtpTestNodeUpdate> nodes)
        => nodes.Count == 0
            ? "<none>"
            : string.Join(", ", nodes.Select(n => "[uid=" + n.Uid + ", name=" + n.DisplayName + ", type=" + n.NodeType + ", state=" + n.ExecutionState + "]"));

    private static MtpServerClientOptions CreateOptions()
    {
        MtpServerClientOptions options = new()
        {
            Logger = new DelegateMtpClientLogger(
                (level, message) => Console.WriteLine("PACKAGEDCONSUMER: LOG " + level + " " + message)),
        };

        // The acceptance test hands us the local preview SDK location via DOTNET_ROOT; forward it (and the
        // related dotnet knobs + the exit-on-unhandled switch) into the launched child so it resolves the same
        // runtime the test uses. The client only adds to the inherited environment, so this is purely additive.
        ForwardEnvironmentVariable(options, "DOTNET_ROOT");
        ForwardEnvironmentVariable(options, "DOTNET_INSTALL_DIR");
        options.EnvironmentVariables["DOTNET_SKIP_FIRST_TIME_EXPERIENCE"] = "1";
        options.EnvironmentVariables["DOTNET_MULTILEVEL_LOOKUP"] = "0";
        options.EnvironmentVariables["TESTINGPLATFORM_EXIT_PROCESS_ON_UNHANDLED_EXCEPTION"] = "0";

        return options;
    }

    private static void ForwardEnvironmentVariable(MtpServerClientOptions options, string name)
    {
        string? value = Environment.GetEnvironmentVariable(name);
        if (!string.IsNullOrEmpty(value))
        {
            options.EnvironmentVariables[name] = value;
        }
    }
}

#file DummyApp/DummyApp.csproj
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>$DummyTfm$</TargetFramework>
    <OutputType>Exe</OutputType>
    <UseAppHost>true</UseAppHost>
    <Nullable>enable</Nullable>
    <LangVersion>preview</LangVersion>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.Testing.Platform" Version="$MicrosoftTestingPlatformVersion$" />
  </ItemGroup>
</Project>

#file DummyApp/Program.cs
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

// A minimal test framework that publishes a single, deterministic test node so the client-side assertions have
// an exact uid/display-name/state to match. On a discovery request the node reports the 'discovered' state; on
// a run request it reports 'passed'.
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

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task PackagedConsumer_LaunchesRealServer_DiscoversAndRunsExpectedNode()
        => await RunPackagedConsumerAsync(Net8Tfm, useManagedChildPath: false, TestContext.CancellationToken);

    [TestMethod]
    [OSCondition(ConditionMode.Exclude, OperatingSystems.Windows, IgnoreMessage = "Unix execute permission classes do not apply on Windows.")]
    [UnsupportedOSPlatform("windows")]
    public async Task PackagedNet6Consumer_WhenSiblingApphostCannotExecute_RetriesThroughDotnet()
        => await RunPackagedConsumerAsync(Net6Tfm, useManagedChildPath: true, TestContext.CancellationToken);

    private async Task RunPackagedConsumerAsync(
        string consumerTfm,
        bool useManagedChildPath,
        CancellationToken cancellationToken)
    {
        string patchedSources = Sources
            .PatchCodeWithReplace("$ConsumerTfm$", consumerTfm)
            .PatchCodeWithReplace("$DummyTfm$", Net8Tfm)
            .PatchCodeWithReplace("$ServerClientSourceVersion$", ResolveServerClientSourceVersion())
            .PatchCodeWithReplace("$MicrosoftTestingPlatformVersion$", MicrosoftTestingPlatformVersion);

        using TestAsset testAsset = await TestAsset.GenerateAssetAsync($"{AssetName}-{consumerTfm}", patchedSources);

        // Build the child MTP app against the shared NuGet cache — its Microsoft.Testing.Platform dependency
        // resolves exactly like the P4 acceptance app does.
        DotnetMuxerResult dummyBuild = await DotnetCli.RunAsync(
            $"build {testAsset.TargetAssetPath}/DummyApp -c {Constants.BuildConfiguration}",
            failIfReturnValueIsNotZero: false,
            cancellationToken: cancellationToken);
        Assert.AreEqual(0, dummyBuild.ExitCode, $"The child MTP app failed to build. Build output:\n{dummyBuild.StandardOutput}\n{dummyBuild.StandardError}");

        // Build the consumer against a throwaway packages folder so a stale same-version (2.4.0-dev) cache of the
        // source-only package can never mask the freshly packed one. The source-only package has no transitive
        // dependencies, so the isolated restore only fetches it (the net8.0 framework reference comes from the SDK).
        string isolatedPackages = Path.Combine(testAsset.TargetAssetPath, ".nuget-packages");
        var consumerBuildEnvironment = new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["NUGET_PACKAGES"] = isolatedPackages,
        };
        DotnetMuxerResult consumerBuild = await DotnetCli.RunAsync(
            $"build {testAsset.TargetAssetPath}/PackagedConsumer -c {Constants.BuildConfiguration}",
            environmentVariables: consumerBuildEnvironment,
            failIfReturnValueIsNotZero: false,
            cancellationToken: cancellationToken);
        Assert.AreEqual(0, consumerBuild.ExitCode, $"The packaged consumer failed to compile the injected source. Build output:\n{consumerBuild.StandardOutput}\n{consumerBuild.StandardError}");

        BuildConfiguration configuration = Enum.Parse<BuildConfiguration>(Constants.BuildConfiguration);
        string consumerExe = TestInfrastructure.TestHost.LocateFrom(
            Path.Combine(testAsset.TargetAssetPath, "PackagedConsumer"), "PackagedConsumer", consumerTfm, buildConfiguration: configuration).FullName;
        string dummyAppExe = TestInfrastructure.TestHost.LocateFrom(
            Path.Combine(testAsset.TargetAssetPath, "DummyApp"), "DummyApp", Net8Tfm, buildConfiguration: configuration).FullName;

        string childSource = dummyAppExe;
        if (useManagedChildPath && !OperatingSystem.IsWindows())
        {
            // The owner class applies to this process, so GroupExecute alone does not grant execution. A
            // net6.0 consumer has no File.GetUnixFileMode preflight and must recover from Process.Start EACCES.
            File.SetUnixFileMode(
                dummyAppExe,
                UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.GroupExecute);
            childSource = Path.Combine(Path.GetDirectoryName(dummyAppExe)!, "DummyApp.dll");
        }

        // Run the consumer apphost directly (not via TestHost.ExecuteAsync, which injects MTP flags a plain exe
        // does not understand), passing the child MTP app path as its single argument. The consumer performs the
        // full launch -> initialize -> discover, then a second launch -> initialize -> run through the packed
        // client and asserts the node internally; it exits 0 only when both halves matched exactly.
        using var command = new TestInfrastructure.CommandLine();
        string consumerCommand = consumerTfm == Net8Tfm
            ? $"\"{consumerExe}\""
            : $"dotnet \"{Path.Combine(Path.GetDirectoryName(consumerExe)!, "PackagedConsumer.dll")}\"";
        int exitCode = await command.RunAsyncAndReturnExitCodeAsync(
            $"{consumerCommand} \"{childSource}\"",
            environmentVariables: CreateChildEnvironment(),
            cancellationToken: cancellationToken);

        Assert.AreEqual(
            0,
            exitCode,
            $"The packaged consumer did not complete the end-to-end run.\nSTD:\n{command.StandardOutput}\nERR:\n{command.ErrorOutput}");
        Assert.Contains($"PACKAGEDCONSUMER: DISCOVERED {ExpectedTestDisplayName}", command.StandardOutput);
        Assert.Contains($"PACKAGEDCONSUMER: EXECUTED {ExpectedTestDisplayName}", command.StandardOutput);
        Assert.Contains("PACKAGEDCONSUMER: OK", command.StandardOutput);
        if (useManagedChildPath)
        {
            Assert.Contains(
                "PACKAGEDCONSUMER: LOG Warning The sibling apphost could not be executed; retrying through 'dotnet",
                command.StandardOutput,
                "The packaged net6.0 slice should recover from EACCES by launching the managed assembly through dotnet.");
        }
    }

    private static Dictionary<string, string?> CreateChildEnvironment()
    {
        var environment = new Dictionary<string, string?>(StringComparer.Ordinal);

        // Neutralize inherited environment that would otherwise change the launched processes' behavior (dump
        // generation, telemetry opt-out, banners, ambient agent detection, ...). These are added on top of the
        // inherited environment, so overwriting each well-known variable with an empty value clears it.
        foreach (string variable in WellKnownEnvironmentVariables.ToSkipEnvironmentVariables)
        {
            environment[variable] = string.Empty;
        }

        // Point the consumer (and, through it, the child host it forwards these to) at the repo-local preview SDK.
        string dotnetRoot = $"{RootFinder.Find()}/.dotnet";
        environment["DOTNET_ROOT"] = dotnetRoot;
        environment["DOTNET_INSTALL_DIR"] = dotnetRoot;
        environment["DOTNET_SKIP_FIRST_TIME_EXPERIENCE"] = "1";
        environment["DOTNET_MULTILEVEL_LOOKUP"] = "0";
        environment["DOTNET_ROLL_FORWARD"] = "Major";
        environment["TESTINGPLATFORM_EXIT_PROCESS_ON_UNHANDLED_EXCEPTION"] = "0";

        return environment;
    }

    private static string ResolveServerClientSourceVersion()
    {
        const string prefix = PackageId + ".";
        const string extension = ".nupkg";

        // Require exactly one match: FirstOrDefault() over several lingering builds could run against an
        // arbitrary stale version and defeat this gate even with an isolated NuGet cache.
        string[] matches = Directory
            .GetFiles(Constants.ArtifactsPackagesShipping, prefix + "*" + extension, SearchOption.TopDirectoryOnly)
            .Where(path => !path.EndsWith(".symbols.nupkg", StringComparison.OrdinalIgnoreCase))
            .Select(path => (path, name: Path.GetFileName(path)))
            .Where(tuple => tuple.name.Length > prefix.Length && char.IsDigit(tuple.name[prefix.Length]))
            .Select(tuple => tuple.path)
            .ToArray();

        if (matches.Length != 1)
        {
            throw new InvalidOperationException(
                $"Expected exactly one packed '{PackageId}' package in '{Constants.ArtifactsPackagesShipping}', " +
                $"but found {matches.Length}. Build with -pack first and clear any stale builds from the shipping folder.");
        }

        string fileName = Path.GetFileName(matches[0]);
        return fileName.Substring(prefix.Length, fileName.Length - prefix.Length - extension.Length);
    }
}
