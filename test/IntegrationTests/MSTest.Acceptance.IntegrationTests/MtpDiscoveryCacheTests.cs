// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Acceptance.IntegrationTests;
using Microsoft.Testing.Platform.Acceptance.IntegrationTests.Helpers;

namespace MSTest.Acceptance.IntegrationTests;

[TestClass]
public sealed class MtpDiscoveryCacheTests : AcceptanceTestBase<NopAssetFixture>
{
    private const string AssetName = "DiscoveryCacheHost";

    private const string Sources = """
#file DiscoveryCacheHost/DiscoveryCacheHost.csproj
<Project Sdk="MSTest.Sdk/$MSTestVersion$">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <Nullable>enable</Nullable>
    <LangVersion>preview</LangVersion>
    <GenerateProgramFile>false</GenerateProgramFile>
    <GenerateTestingPlatformEntryPoint>false</GenerateTestingPlatformEntryPoint>
    <EnableMSTestSourceGeneration>false</EnableMSTestSourceGeneration>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.Testing.Platform.ServerMode.Client.Sources" Version="$ServerClientSourceVersion$" />
    <PackageReference Include="Microsoft.Testing.Platform" Version="$MicrosoftTestingPlatformVersion$" />
    <PackageReference Include="Microsoft.Testing.Extensions.TrxReport" Version="$MicrosoftTestingPlatformVersion$" Condition="'$(UseVSTest)' == 'true'" />
  </ItemGroup>
</Project>

#file DiscoveryCacheHost/Tests.cs
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

[assembly: Parallelize(Workers = 2, Scope = ExecutionScope.MethodLevel)]

internal static class Probe
{
    private static readonly object Gate = new();
    internal static string DirectoryPath => Environment.GetEnvironmentVariable("MSTEST_CACHE_PROBE_DIRECTORY")!;
    internal static int RowCount => int.Parse(File.ReadAllText(Path.Combine(DirectoryPath, "rows.txt")));

    internal static void Record(string value)
    {
        lock (Gate)
        {
            File.AppendAllLines(Path.Combine(DirectoryPath, "events.txt"), new[] { value });
        }
    }
}

[TestClass]
public sealed class StaticTests
{
    static StaticTests() => Probe.Record("static-constructor");
    public TestContext TestContext { get; set; } = null!;
    public int UnusedProperty { get { Probe.Record("property-getter"); return 1; } }

    [AssemblyInitialize]
    public static void AssemblyStart(TestContext context) => Probe.Record("assembly-initialize");
    [AssemblyCleanup]
    public static void AssemblyEnd() => Probe.Record("assembly-cleanup");
    [ClassInitialize]
    public static void ClassStart(TestContext context) => Probe.Record("class-initialize");
    [ClassCleanup]
    public static void ClassEnd() => Probe.Record("class-cleanup");
    [TestInitialize]
    public void Start() => Probe.Record("test-initialize");
    [TestCleanup]
    public void End() => Probe.Record("test-cleanup");

    [TestMethod]
    public async Task A() { await Task.CompletedTask; Probe.Record("A"); }
    [TestMethod]
    public void B() => Probe.Record("B");
    [TestMethod]
    public void Recoverable()
    {
        Probe.Record("Recoverable");
        Assert.IsFalse(File.Exists(Path.Combine(Probe.DirectoryPath, "fail.txt")), "controlled failure");
    }

    [TestMethod]
    public async Task Cancellable()
    {
        File.WriteAllText(Path.Combine(Probe.DirectoryPath, "cancel-start.txt"), "started");
        try
        {
            await Task.Delay(Timeout.Infinite, TestContext.CancellationToken);
        }
        finally
        {
            File.WriteAllText(Path.Combine(Probe.DirectoryPath, "cancel-stop.txt"), "stopped");
        }
    }
}

[TestClass]
public abstract class BaseTests
{
    [TestMethod]
    public void Inherited() => Probe.Record("Inherited");
}

[TestClass]
public sealed class DerivedTests : BaseTests
{
}

[TestClass]
public sealed class LiveTests
{
    public static IEnumerable<object[]> Rows()
    {
        Probe.Record("data-provider");
        return Enumerable.Range(1, Probe.RowCount).Select(value => new object[] { value }).ToArray();
    }

    [TestMethod]
    [DynamicData(nameof(Rows), DynamicDataSourceType.Method)]
    public void Dynamic(int value)
    {
        Assert.IsTrue(value >= 1 && value <= Probe.RowCount);
        Probe.Record("Dynamic:" + value);
    }

    [TestMethod]
    [DataRow(1)]
    [DataRow(2)]
    public void ConstantRow(int value) => Probe.Record("ConstantRow:" + value);
}

[TestClass]
public sealed class DependencyTests
{
    [TestMethod]
    public void First() => Probe.Record("dependency-first");
    [TestMethod]
    [DependsOn(nameof(First))]
    public void Second() => Probe.Record("dependency-second");
}

#file DiscoveryCacheHost/Program.cs
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Testing.Extensions;
using Microsoft.Testing.Platform.Builder;
using Microsoft.Testing.Platform.ServerMode.Client;
using Microsoft.Testing.Platform.ServerMode.Client.Protocol.ServerMode;
using Microsoft.VisualStudio.TestTools.UnitTesting;

internal static class Program
{
    public static async Task<int> Main(string[] args)
    {
        if (Environment.GetEnvironmentVariable("MSTEST_CACHE_PROBE_DIRECTORY") is not null)
        {
            string directory = Probe.DirectoryPath;
            if (!args.Contains("--settings"))
            {
                args = args.Concat(new[]
                {
                    "--settings", Path.Combine(directory, "settings.runsettings"),
                    "--diagnostic", "--diagnostic-verbosity", "Trace",
                    "--diagnostic-output-directory", directory,
                    "--results-directory", directory,
                }).ToArray();
            }

            ITestApplicationBuilder builder = await TestApplication.CreateBuilderAsync(args);
            builder.AddMSTest(() => new[] { Assembly.GetExecutingAssembly() });
            builder.AddTrxReportProvider();
            using ITestApplication app = await builder.BuildAsync();
            return await app.RunAsync();
        }

        using var cancellation = new CancellationTokenSource(TimeSpan.FromMinutes(3));
        string root = args[0];
        bool expectHits = bool.Parse(args[1]);
        string[] baseline = await RunSequence(Path.Combine(root, "default"), enableValue: null, disableValue: null, expectHits: false, cancellation.Token);
        string[] cached = await RunSequence(Path.Combine(root, "enabled"), enableValue: "1", disableValue: null, expectHits, cancellation.Token);
        Assert.AreEqual(JsonSerializer.Serialize(baseline), JsonSerializer.Serialize(cached), "Every request and fixture/provider invocation must match.");
        string[] disabled = await RunSequence(Path.Combine(root, "disabled"), enableValue: "true", disableValue: "true", expectHits: false, cancellation.Token);
        Assert.AreEqual(JsonSerializer.Serialize(baseline), JsonSerializer.Serialize(disabled), "The kill switch must override the opt-in.");
        string[] invalid = await RunSequence(Path.Combine(root, "invalid"), enableValue: "yes", disableValue: null, expectHits: false, cancellation.Token);
        Assert.AreEqual(JsonSerializer.Serialize(baseline), JsonSerializer.Serialize(invalid), "Other values must retain baseline discovery.");
        Console.WriteLine("CACHE: PARITY " + baseline.Length);
        return 0;
    }

    private static async Task<string[]> RunSequence(string directory, string? enableValue, string? disableValue, bool expectHits, CancellationToken token)
    {
        Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory, "rows.txt"), "2");
        string settingsPath = Path.Combine(directory, "settings.runsettings");
        const string NormalSettings = "<RunSettings><MSTest><OrderTestsByNameInClass>true</OrderTestsByNameInClass></MSTest></RunSettings>";
        File.WriteAllText(settingsPath, NormalSettings);
        var options = new MtpServerClientOptions { ClientName = "DiscoveryCacheParity", IsStateful = true };
        options.EnvironmentVariables["MSTEST_EXPERIMENTAL_DISCOVERY_CACHE"] = enableValue;
        options.EnvironmentVariables["MSTEST_DISABLE_DISCOVERY_CACHE"] = disableValue;
        options.EnvironmentVariables["MSTEST_CACHE_PROBE_DIRECTORY"] = directory;
        options.EnvironmentVariables["TESTINGPLATFORM_EXPERIMENTAL_VSTEST_RUNSETTINGS"] = null;
        options.EnvironmentVariables["TESTINGPLATFORM_VSTESTBRIDGE_RUNSETTINGS_FILE"] = null;
        options.EnvironmentVariables["DOTNET_ROLL_FORWARD"] = "Major";
        options.EnvironmentVariables["TESTINGPLATFORM_TELEMETRY_OPTOUT"] = "1";
        options.EnvironmentVariables["DOTNET_CLI_TELEMETRY_OPTOUT"] = "1";
        using MtpServerProcess server = await MtpServerProcess.StartAsync(Assembly.GetExecutingAssembly().Location, options, token);
        using IMtpServerClient client = new MtpServerClient(server.Connection, options);
        using var serverProcess = Process.GetProcessById(server.ProcessId);
        var updates = new List<MtpTestNodeUpdate>();
        client.TestNodesUpdated += (_, update) => updates.AddRange(update.Changes);
        await client.InitializeAsync(token);
        await client.DiscoverTestsAsync(token);
        // This single-flight fixture counts the official client's monotonically numbered requests.
        int requestId = 2;
        Dictionary<string, string> uids = updates.Where(node => node.NodeType == "action" && node.ExecutionState == "discovered")
            .ToDictionary(node => node.DisplayName!, node => node.Uid!);
        Console.WriteLine("CACHE: UID A " + uids["A"]);
        var snapshots = new List<string>();

        await Run(new[] { uids["A"] }, "passed");
        await Run(new[] { uids["B"] }, "passed");
        await Run(new[] { uids["B"], uids["A"], uids["A"] }, "passed", "passed");
        await Run(Array.Empty<string>());
        await Run(new[] { "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee" });
        await Run(new[] { "StaticTests.A" }, "passed");
        await Run(new[] { "Unknown.Test" });
        await Run(new[] { uids["Inherited"] }, "passed");
        for (int iteration = 0; iteration < 3; iteration++)
        {
            await Run(new[] { uids["First"], uids["Second"] }, "passed", "passed");
        }

        File.WriteAllText(settingsPath, "<RunSettings><RunConfiguration><TestCaseFilter>Name=A</TestCaseFilter></RunConfiguration></RunSettings>");
        await Run(new[] { uids["A"], uids["B"] }, "passed");
        File.WriteAllText(settingsPath, NormalSettings);
        File.WriteAllText(Path.Combine(directory, "rows.txt"), "3");
        await Run(new[] { uids["A"] }, "passed");
        updates.Clear();
        await client.DiscoverTestsAsync(token);
        requestId++;
        string[] dataUids = updates.Where(node => node.NodeType == "action" && node.ExecutionState == "discovered"
            && node.DisplayName!.StartsWith("Dynamic", StringComparison.Ordinal)).Select(node => node.Uid!).ToArray();
        Assert.HasCount(3, dataUids);
        await Run(dataUids, "passed", "passed", "passed");
        string[] constantUids = uids.Where(pair => pair.Key.StartsWith("ConstantRow", StringComparison.Ordinal)).Select(pair => pair.Value).ToArray();
        Assert.HasCount(2, constantUids);
        await Run(constantUids, "passed", "passed");
        File.WriteAllText(Path.Combine(directory, "fail.txt"), "fail");
        await Run(new[] { uids["Recoverable"] }, "failed");
        File.Delete(Path.Combine(directory, "fail.txt"));
        await Run(new[] { uids["Recoverable"] }, "passed");
        using (var cancelled = new CancellationTokenSource())
        {
            cancelled.Cancel();
            try
            {
                await client.RunTestsAsync(new[] { uids["A"] }, cancelled.Token);
                throw new InvalidOperationException("A pre-cancelled client request unexpectedly completed.");
            }
            catch (OperationCanceledException)
            {
            }
        }

        await Run(new[] { uids["A"] }, "passed");
        int cancelledRequestId = ++requestId;
        Task<MtpRunResult> pending = client.RunTestsAsync(new[] { uids["Cancellable"] }, token);
        try
        {
            await WaitForMarker("cancel-start.txt");
        }
        finally
        {
            // Keep the caller's response pending while cancelling the server request. Client-side
            // token cancellation alone completes before the terminal response/update drain.
            await server.Connection.SendNotificationAsync(JsonRpcMethods.CancelRequest, new CancelRequestArgs(cancelledRequestId), token);
            try
            {
                await pending;
                throw new InvalidOperationException("A cancelled server request unexpectedly completed.");
            }
            catch (MtpServerErrorException error)
            {
                Assert.AreEqual(ErrorCodes.RequestCanceled, error.ErrorCode);
            }
        }

        Assert.IsTrue(File.Exists(Path.Combine(directory, "cancel-stop.txt")), "The server response must follow test termination.");
        await Run(new[] { uids["A"] }, "passed");
        await client.ExitAsync(token);
        await serverProcess.WaitForExitAsync(token);
        Assert.AreEqual(0, serverProcess.ExitCode);
        await client.ShutdownAsync();
        string diagnostics = string.Join(Environment.NewLine, Directory.GetFiles(directory)
            .Where(path => path.EndsWith(".log", StringComparison.Ordinal) || path.EndsWith(".diag", StringComparison.Ordinal))
            .Select(File.ReadAllText));
        int hits = diagnostics.Split("MSTest discovery cache hit:", StringSplitOptions.None).Length - 1;
        if (expectHits)
        {
            Assert.IsTrue(hits >= 2, diagnostics);
            Match catalog = Regex.Match(diagnostics, @"MSTest discovery cache catalog: (\d+)/(\d+) reusable types");
            Assert.IsTrue(catalog.Success, diagnostics);
            Assert.IsTrue(int.Parse(catalog.Groups[1].Value) > 0, diagnostics);
            Console.WriteLine("CACHE: ELIGIBLE " + catalog.Groups[1].Value + "/" + catalog.Groups[2].Value);
        }
        else
        {
            Assert.AreEqual(0, hits, diagnostics);
            if (enableValue != "1")
            {
                Assert.DoesNotContain("MSTest discovery cache", diagnostics);
            }
        }

        Console.WriteLine("CACHE: HITS " + Path.GetFileName(directory) + " " + hits);
        Assert.AreEqual(1, File.ReadAllLines(Path.Combine(directory, "events.txt")).Count(value => value == "static-constructor"));
        Assert.AreEqual(0, File.ReadAllLines(Path.Combine(directory, "events.txt")).Count(value => value == "property-getter"));
        return snapshots.ToArray();

        async Task WaitForMarker(string name)
        {
            using var markerTimeout = CancellationTokenSource.CreateLinkedTokenSource(token);
            markerTimeout.CancelAfter(TimeSpan.FromSeconds(30));
            while (!File.Exists(Path.Combine(directory, name)))
            {
                await Task.Delay(10, markerTimeout.Token);
            }
        }

        async Task Run(string[] selected, params string[] expectedStates)
        {
            int providersBefore = File.ReadAllLines(Path.Combine(directory, "events.txt")).Count(value => value == "data-provider");
            updates.Clear();
            await client.RunTestsAsync(selected, token);
            requestId++;
            MtpTestNodeUpdate[] results = updates.Where(node => node.NodeType == "action"
                && node.ExecutionState is "passed" or "failed" or "skipped" or "error").ToArray();
            Assert.AreEqual(JsonSerializer.Serialize(expectedStates.OrderBy(value => value).ToArray()),
                JsonSerializer.Serialize(results.Select(node => node.ExecutionState).OrderBy(value => value).ToArray()));
            string[] events = File.ReadAllLines(Path.Combine(directory, "events.txt"));
            Assert.AreEqual(providersBefore + 1, events.Count(value => value == "data-provider"), "Unselected data providers must remain live.");
            snapshots.Add(JsonSerializer.Serialize(new
            {
                Results = results.Select(node => node.Uid + "|" + node.DisplayName + "|" + node.ExecutionState).OrderBy(value => value).ToArray(),
                Events = events.GroupBy(value => value).Select(group => group.Key + "=" + group.Count()).OrderBy(value => value).ToArray(),
            }));
        }
    }
}
""";

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    [DataRow(MetadataMode.Reflection)]
    [DataRow(MetadataMode.SourceGeneration)]
    [DataRow(MetadataMode.AotSourceGeneration)]
    public async Task RepeatedSelectedRequestsMatchUncachedExecution(MetadataMode metadataMode)
    {
        string clientVersion = Directory.GetFiles(Constants.ArtifactsPackagesShipping, "Microsoft.Testing.Platform.ServerMode.Client.Sources.*.nupkg")
            .Select(Path.GetFileName)
            .Single(name => !name!.EndsWith(".symbols.nupkg", StringComparison.Ordinal))!
            .Replace("Microsoft.Testing.Platform.ServerMode.Client.Sources.", string.Empty, StringComparison.Ordinal)
            .Replace(".nupkg", string.Empty, StringComparison.Ordinal);
        string source = Sources.PatchCodeWithReplace("$MSTestVersion$", MSTestVersion)
            .PatchCodeWithReplace("$MicrosoftTestingPlatformVersion$", MicrosoftTestingPlatformVersion)
            .PatchCodeWithReplace("$ServerClientSourceVersion$", clientVersion);
        using TestAsset asset = await TestAsset.GenerateAssetAsync(AssetName, source);
        var environment = new Dictionary<string, string?>
        {
            ["NUGET_PACKAGES"] = Path.Combine(asset.TargetAssetPath, ".nuget-packages"),
            ["DOTNET_ROOT"] = Path.Combine(RootFinder.Find(), ".dotnet"),
            ["DOTNET_ROLL_FORWARD"] = "Major",
        };
        string project = Path.Combine(asset.TargetAssetPath, AssetName);
        string sourceGenArguments = metadataMode == MetadataMode.Reflection
            ? string.Empty
            : await AcceptanceSourceGen.PrepareBuildArgumentsAsync(project, metadataMode);
        DotnetMuxerResult build = await DotnetCli.RunAsync(
            $"build \"{project}\" -c {Constants.BuildConfiguration} {sourceGenArguments}",
            environmentVariables: environment.ToDictionary(pair => pair.Key, pair => pair.Value),
            failIfReturnValueIsNotZero: false,
            cancellationToken: TestContext.CancellationToken);
        Assert.AreEqual(0, build.ExitCode, build.StandardOutput + build.StandardError);
        var host = TestHost.LocateFrom(project, AssetName, "net8.0",
            buildConfiguration: Enum.Parse<BuildConfiguration>(Constants.BuildConfiguration), metadataMode: metadataMode);
        using var command = new CommandLine();
        int exitCode = await command.RunAsyncAndReturnExitCodeAsync(
            $"\"{host.FullName}\" \"{Path.Combine(asset.TargetAssetPath, "probe")}\" {metadataMode == MetadataMode.Reflection}",
            environmentVariables: environment.ToDictionary(pair => pair.Key, pair => pair.Value),
            cancellationToken: TestContext.CancellationToken);
        string diagnostics = string.Empty;
        if (exitCode != 0)
        {
            string probe = Path.Combine(asset.TargetAssetPath, "probe");
            if (Directory.Exists(probe))
            {
                diagnostics = string.Join(
                    Environment.NewLine,
                    Directory.GetFiles(probe, "*", SearchOption.AllDirectories)
                        .Where(path => path.EndsWith(".log", StringComparison.Ordinal) || path.EndsWith(".diag", StringComparison.Ordinal))
                        .Select(File.ReadAllText));
            }
        }

        Assert.AreEqual(0, exitCode, command.StandardOutput + command.ErrorOutput + diagnostics);
        Assert.Contains("CACHE: PARITY 19", command.StandardOutput);
        string uid = System.Text.RegularExpressions.Regex.Match(command.StandardOutput, @"CACHE: UID A (\S+)").Groups[1].Value;
        Assert.IsTrue(Guid.TryParse(uid, out _), command.StandardOutput);
        foreach (string route in new[] { "console-fqn", "console-uid", "dotnet-test-fqn", "dotnet-test-uid" })
        {
            string? baseline = null;
            foreach (bool disabled in new[] { true, false })
            {
                string directory = PrepareDirectory(route, disabled);
                string selector = route.EndsWith("-uid", StringComparison.Ordinal)
                    ? $"--filter-uid {uid}"
                    : "--filter \"FullyQualifiedName=StaticTests.A\"";
                string arguments = $"{selector} --report-trx --settings \"{Path.Combine(directory, "settings.runsettings")}\""
                    + $" --diagnostic --diagnostic-verbosity Trace --diagnostic-output-directory \"{directory}\" --results-directory \"{directory}\"";
                int routeExit;
                string output;
                if (route.StartsWith("console", StringComparison.Ordinal))
                {
                    using var console = new CommandLine();
                    routeExit = await console.RunAsyncAndReturnExitCodeAsync(
                        $"\"{host.FullName}\" {arguments}",
                        environmentVariables: environment.ToDictionary(pair => pair.Key, pair => pair.Value),
                        cancellationToken: TestContext.CancellationToken);
                    output = console.StandardOutput + console.ErrorOutput;
                }
                else
                {
                    DotnetMuxerResult result = await DotnetCli.RunAsync(
                        $"test --project \"{project}\" -c {Constants.BuildConfiguration} --no-build {sourceGenArguments} {arguments}",
                        environmentVariables: environment.ToDictionary(pair => pair.Key, pair => pair.Value),
                        failIfReturnValueIsNotZero: false,
                        cancellationToken: TestContext.CancellationToken);
                    routeExit = result.ExitCode;
                    output = result.StandardOutput + result.StandardError;
                }

                Assert.AreEqual(0, routeExit, output);
                string snapshot = ReadSnapshot(directory);
                if (baseline is null)
                {
                    baseline = snapshot;
                }
                else
                {
                    Assert.AreEqual(baseline, snapshot, route);
                }
            }
        }

        if (metadataMode == MetadataMode.Reflection)
        {
            DotnetMuxerResult vstestBuild = await DotnetCli.RunAsync(
                $"build \"{project}\" -c {Constants.BuildConfiguration} -p:UseVSTest=true",
                environmentVariables: environment.ToDictionary(pair => pair.Key, pair => pair.Value),
                failIfReturnValueIsNotZero: false,
                cancellationToken: TestContext.CancellationToken);
            Assert.AreEqual(0, vstestBuild.ExitCode, vstestBuild.StandardOutput + vstestBuild.StandardError);
            string assembly = Path.ChangeExtension(host.FullName, ".dll");
            string? baseline = null;
            foreach (bool disabled in new[] { true, false })
            {
                string directory = PrepareDirectory("vstest", disabled);
                using var vstest = new CommandLine();
                string dotnet = Path.Combine(environment["DOTNET_ROOT"]!, "dotnet" + Constants.ExecutableExtension);
                int vstestExit = await vstest.RunAsyncAndReturnExitCodeAsync(
                    $"\"{dotnet}\" vstest \"{assembly}\" /TestCaseFilter:\"FullyQualifiedName=StaticTests.A\" /Logger:trx /ResultsDirectory:\"{directory}\" /Diag:\"{Path.Combine(directory, "vstest.log")}\"",
                    environmentVariables: environment.ToDictionary(pair => pair.Key, pair => pair.Value),
                    cancellationToken: TestContext.CancellationToken);
                Assert.AreEqual(0, vstestExit, vstest.StandardOutput + vstest.ErrorOutput);
                Assert.IsTrue(File.Exists(Path.Combine(directory, "vstest.log")));
                string snapshot = ReadSnapshot(directory);
                if (baseline is null)
                {
                    baseline = snapshot;
                }
                else
                {
                    Assert.AreEqual(baseline, snapshot, "vstest");
                }
            }
        }

        string PrepareDirectory(string route, bool disabled)
        {
            string directory = Path.Combine(asset.TargetAssetPath, route + "-" + disabled);
            Directory.CreateDirectory(directory);
            File.WriteAllText(Path.Combine(directory, "rows.txt"), "2");
            File.WriteAllText(Path.Combine(directory, "settings.runsettings"), "<RunSettings />");
            environment["MSTEST_CACHE_PROBE_DIRECTORY"] = directory;
            environment["MSTEST_EXPERIMENTAL_DISCOVERY_CACHE"] = "1";
            environment["MSTEST_DISABLE_DISCOVERY_CACHE"] = disabled ? "1" : "0";
            return directory;
        }

        static string ReadSnapshot(string directory)
        {
            string[] reports = Directory.GetFiles(directory, "*.trx");
            Assert.HasCount(1, reports);
            string[] results = System.Xml.Linq.XDocument.Load(reports[0]).Descendants()
                .Where(element => element.Name.LocalName == "UnitTestResult")
                .Select(element => element.Attribute("testName")!.Value + "|" + element.Attribute("outcome")!.Value)
                .ToArray();
            Assert.HasCount(1, results);
            Assert.AreEqual("A|Passed", results[0]);
            string[] logFiles = Directory.GetFiles(directory).Where(path => path.EndsWith(".diag", StringComparison.Ordinal)
                || path.EndsWith(".log", StringComparison.Ordinal)).ToArray();
            Assert.IsNotEmpty(logFiles);
            string diagnostics = string.Join(
                Environment.NewLine,
                logFiles.Select(File.ReadAllText));
            Assert.IsFalse(string.IsNullOrWhiteSpace(diagnostics));
            Assert.DoesNotContain("MSTest discovery cache", diagnostics);
            return string.Join(Environment.NewLine, results.Concat(File.ReadAllLines(Path.Combine(directory, "events.txt")).Order()));
        }
    }
}
