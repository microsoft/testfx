// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Acceptance.IntegrationTests;
using Microsoft.Testing.Platform.Acceptance.IntegrationTests.Helpers;

namespace MSTest.Acceptance.IntegrationTests;

/// <summary>
/// End-to-end acceptance test for <c>MtpServerClient.LaunchInProcessAsync</c>, the embedded-host launch path
/// of the source-only <c>Microsoft.Testing.Platform.ServerMode.Client.Sources</c> package.
/// </summary>
/// <remarks>
/// <para>
/// This is the scenario the API exists for and it cannot be faked from the test host: one single process is
/// at the same time the embedded host (it compiles the packed source-only client and drives it) and the
/// Microsoft.Testing.Platform application (a real <c>TestApplication</c> with real MSTest registered over a
/// real <c>[TestClass]</c>). No <c>Process.Start</c> is involved anywhere — exactly the constraint a MAUI /
/// Android / iOS runner works under.
/// </para>
/// <para>
/// The generated app reports its verdict only through its exit code plus a few parseable marker lines, so
/// this test stays independent of the app's console formatting. Because it consumes the packed client
/// package, the repository must have been built with <c>-pack</c> first; otherwise the test fails with a
/// hint. The sibling <see cref="MtpServerClientAcceptanceTests"/> covers the external-process launch path
/// against the same kind of real application.
/// </para>
/// </remarks>
[TestClass]
public sealed class MtpServerClientInProcessAcceptanceTests : AcceptanceTestBase<NopAssetFixture>
{
    private const string AssetName = "MtpServerClientInProcessHost";
    private const string PackageId = "Microsoft.Testing.Platform.ServerMode.Client.Sources";

    private const string Sources = """
#file InProcessHost/InProcessHost.csproj
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>$TargetFramework$</TargetFramework>
    <OutputType>Exe</OutputType>
    <UseAppHost>true</UseAppHost>
    <Nullable>enable</Nullable>
    <LangVersion>preview</LangVersion>
    <!-- The app owns its own Main: it is the embedded host AND the test application. -->
    <EnableMSTestRunner>true</EnableMSTestRunner>
    <GenerateProgramFile>false</GenerateProgramFile>
    <GenerateTestingPlatformEntryPoint>false</GenerateTestingPlatformEntryPoint>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.Testing.Platform.ServerMode.Client.Sources" Version="$ServerClientSourceVersion$" />
    <PackageReference Include="Microsoft.Testing.Platform" Version="$MicrosoftTestingPlatformVersion$" />
    <PackageReference Include="MSTest" Version="$MSTestVersion$" />
  </ItemGroup>
</Project>

#file InProcessHost/UnitTests.cs
using Microsoft.VisualStudio.TestTools.UnitTesting;

[TestClass]
public sealed class UnitTests
{
    [TestMethod]
    public void TestMethod1() => Assert.IsTrue(true);
}

#file InProcessHost/Program.cs
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Testing.Platform.Builder;
using Microsoft.Testing.Platform.ServerMode.Client;
using Microsoft.VisualStudio.TestTools.UnitTesting;

internal static class Program
{
    private const string ExpectedDisplayName = "TestMethod1";

    public static async Task<int> Main(string[] args)
    {
        using CancellationTokenSource cts = new(TimeSpan.FromSeconds(120));
        CancellationToken cancellationToken = cts.Token;

        try
        {
            // One session, driven statefully: discover then run over the same in-process connection.
            List<MtpTestNodeUpdate> updates = new();
            object gate = new();

            using IMtpServerClient client = await MtpServerClient.LaunchInProcessAsync(
                RunTestApplicationAsync,
                CreateOptions(),
                cancellationToken);

            client.TestNodesUpdated += (_, e) =>
            {
                lock (gate)
                {
                    updates.AddRange(e.Changes);
                }
            };

            MtpServerCapabilities capabilities = await client.InitializeAsync(cancellationToken);
            if (!capabilities.SupportsDiscovery)
            {
                Console.Error.WriteLine("INPROCESSHOST: the server did not advertise discovery support.");
                return 1;
            }

            int currentProcessId;
            using (System.Diagnostics.Process current = System.Diagnostics.Process.GetCurrentProcess())
            {
                currentProcessId = current.Id;
            }

            if (capabilities.ServerProcessId != currentProcessId)
            {
                Console.Error.WriteLine("INPROCESSHOST: the server reported a different process id, so it was not hosted in process.");
                return 1;
            }

            Console.WriteLine("INPROCESSHOST: SAMEPROCESS " + client.ProcessId);

            await client.DiscoverTestsAsync(cancellationToken);
            if (!Expect(updates, gate, "discovered"))
            {
                return 1;
            }

            Console.WriteLine("INPROCESSHOST: DISCOVERED " + ExpectedDisplayName);

            await client.RunTestsAsync(cancellationToken);
            if (!Expect(updates, gate, "passed"))
            {
                return 1;
            }

            Console.WriteLine("INPROCESSHOST: EXECUTED " + ExpectedDisplayName);

            await client.ExitAsync(cancellationToken);

            // The non-blocking teardown: closes the transport and awaits the hosted application. The trailing
            // Dispose from the using block is then a no-op.
            await client.ShutdownAsync();

            if (client.ServerExitCode != 0)
            {
                Console.Error.WriteLine("INPROCESSHOST: the hosted application exited with " + client.ServerExitCode);
                return 1;
            }

            Console.WriteLine("INPROCESSHOST: EXITCODE " + client.ServerExitCode);
            Console.WriteLine("INPROCESSHOST: OK");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("INPROCESSHOST: FAILED " + ex);
            return 1;
        }
    }

    // The whole point of the API: the caller only says "how to run the application". The client owns the
    // listener, the argument array, the connect race, the transport and the shutdown.
    private static async Task<int> RunTestApplicationAsync(string[] serverArguments, CancellationToken cancellationToken)
    {
        ITestApplicationBuilder builder = await TestApplication.CreateBuilderAsync(serverArguments);
        builder.AddMSTest(() => new[] { Assembly.GetEntryAssembly()! });
        using ITestApplication app = await builder.BuildAsync();
        return await app.RunAsync();
    }

    private static bool Expect(List<MtpTestNodeUpdate> updates, object gate, string executionState)
    {
        List<MtpTestNodeUpdate> snapshot;
        lock (gate)
        {
            snapshot = updates.ToList();
        }

        int count = snapshot.Count(n => n.NodeType == "action" && n.DisplayName == ExpectedDisplayName && n.ExecutionState == executionState);
        if (count == 1)
        {
            return true;
        }

        Console.Error.WriteLine(FormattableString.Invariant(
            $"INPROCESSHOST: expected exactly one '{executionState}' {ExpectedDisplayName}, got {count}. {Describe(snapshot)}"));
        return false;
    }

    private static string Describe(IReadOnlyList<MtpTestNodeUpdate> nodes)
        => nodes.Count == 0
            ? "<none>"
            : string.Join(", ", nodes.Select(n => "[uid=" + n.Uid + ", name=" + n.DisplayName + ", type=" + n.NodeType + ", state=" + n.ExecutionState + "]"));

    private static MtpServerClientOptions CreateOptions()
        => new()
        {
            ClientName = "InProcessHost",
            // The session serves discover and run over one connection.
            IsStateful = true,
            ConnectionTimeout = TimeSpan.FromSeconds(60),
            ServerShutdownTimeout = TimeSpan.FromSeconds(30),
            Logger = new DelegateMtpClientLogger(
                (level, message) => Console.WriteLine("INPROCESSHOST: LOG " + level + " " + message)),
        };
}
""";

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task InProcessHost_DiscoversAndRunsItsOwnMSTestNodes_WithoutStartingAProcess()
    {
        string patchedSources = Sources
            .PatchCodeWithReplace("$TargetFramework$", TargetFrameworks.NetCurrent)
            .PatchCodeWithReplace("$ServerClientSourceVersion$", ResolveServerClientSourceVersion())
            .PatchCodeWithReplace("$MicrosoftTestingPlatformVersion$", MicrosoftTestingPlatformVersion)
            .PatchCodeWithReplace("$MSTestVersion$", MSTestVersion);

        using TestAsset testAsset = await TestAsset.GenerateAssetAsync(AssetName, patchedSources);

        // Restore into a throwaway packages folder so the freshly packed client is always used; a stale
        // cached copy of the same dev version would otherwise make this gate pass vacuously.
        string isolatedPackages = Path.Combine(testAsset.TargetAssetPath, ".nuget-packages");
        var environmentVariables = new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["NUGET_PACKAGES"] = isolatedPackages,
        };

        DotnetMuxerResult build = await DotnetCli.RunAsync(
            $"build {testAsset.TargetAssetPath}/InProcessHost -c {Constants.BuildConfiguration}",
            environmentVariables: environmentVariables,
            failIfReturnValueIsNotZero: false,
            cancellationToken: TestContext.CancellationToken);

        Assert.AreEqual(
            0,
            build.ExitCode,
            $"The embedded host failed to compile the injected source. Build output:{Environment.NewLine}{build.StandardOutput}{Environment.NewLine}{build.StandardError}");

        var testHost = TestHost.LocateFrom(
            Path.Combine(testAsset.TargetAssetPath, "InProcessHost"),
            "InProcessHost",
            TargetFrameworks.NetCurrent,
            buildConfiguration: Enum.Parse<BuildConfiguration>(Constants.BuildConfiguration));

        // Run the apphost directly rather than through TestHost.ExecuteAsync: this app owns its Main and is
        // the embedded HOST, so it does not accept the platform flags that helper injects.
        using var command = new CommandLine();

        // The trailing space matters: CommandLine's quoted-path parser reads the arguments as everything after
        // the closing quote plus one separator, so a bare quoted path with nothing after it is out of range.
        int exitCode = await command.RunAsyncAndReturnExitCodeAsync(
            $"\"{testHost.FullName}\" ",
            environmentVariables: CreateChildEnvironment(),
            cancellationToken: TestContext.CancellationToken);

        Assert.AreEqual(
            0,
            exitCode,
            $"The embedded host did not complete successfully.{Environment.NewLine}STD:{Environment.NewLine}{command.StandardOutput}{Environment.NewLine}ERR:{Environment.NewLine}{command.ErrorOutput}");
        Assert.Contains("INPROCESSHOST: SAMEPROCESS", command.StandardOutput);
        Assert.Contains("INPROCESSHOST: DISCOVERED TestMethod1", command.StandardOutput);
        Assert.Contains("INPROCESSHOST: EXECUTED TestMethod1", command.StandardOutput);
        Assert.Contains("INPROCESSHOST: EXITCODE 0", command.StandardOutput);
        Assert.Contains("INPROCESSHOST: OK", command.StandardOutput);
    }

    private static Dictionary<string, string?> CreateChildEnvironment()
    {
        var environment = new Dictionary<string, string?>(StringComparer.Ordinal);

        // Neutralize inherited environment that would otherwise change the hosted application's behavior
        // (dump generation, telemetry opt-out, banners, ambient agent detection, ...). These are added on top
        // of the inherited environment, so overwriting each well-known variable with an empty value clears it.
        foreach (string variable in WellKnownEnvironmentVariables.ToSkipEnvironmentVariables)
        {
            environment[variable] = string.Empty;
        }

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

        // Require exactly one match: picking an arbitrary lingering build could compile against a stale
        // package and defeat this gate even with an isolated NuGet cache.
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
