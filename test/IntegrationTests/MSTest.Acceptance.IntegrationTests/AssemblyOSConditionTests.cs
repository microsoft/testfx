// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.IO.Compression;
using System.Security.Cryptography;
using System.Xml.Linq;

using Microsoft.Testing.Platform.Acceptance.IntegrationTests;
using Microsoft.Testing.Platform.Acceptance.IntegrationTests.Helpers;

namespace MSTest.Acceptance.IntegrationTests;

[TestClass]
public sealed class AssemblyOSConditionTests : AcceptanceTestBase<NopAssetFixture>
{
    private const string AssetName = "AssemblyOSConditionConsumer";
    private const string EvidenceEnvironmentVariable = "ASSEMBLY_OS_CONDITION_EVIDENCE";
    private const string IncludeReason = "Assembly requires a matching operating system";
    private const string ExcludeReason = "Test is not supported on Linux, OSX, Windows, FreeBSD";

    private static readonly ExpectedResult[] ExpectedResults =
    [
        new("AllowedTests", "Plain", "Plain", "Passed", null, "body:Plain"),
        new("AllowedTests", "MethodIncludeMatches", "MethodIncludeMatches", "Passed", null, "body:MethodIncludeMatches"),
        new("AllowedTests", "MethodIncludeMisses", "MethodIncludeMisses", "NotExecuted", "method include excludes all", null),
        new("AllowedTests", "MethodExcludeMatches", "MethodExcludeMatches", "NotExecuted", "method exclude blocks all", null),
        new("AllowedTests", "MethodExcludeMisses", "MethodExcludeMisses", "Passed", null, "body:MethodExcludeMisses"),
        new("AllowedTests", "SameScopeAlternative", "SameScopeAlternative", "Passed", null, "body:SameScopeAlternative"),
        new("AllowedTests", "Rows", "RowEleven", "Passed", null, "body:Rows:11"),
        new("AllowedTests", "Rows", "RowTwentyNine", "Passed", null, "body:Rows:29"),
        new("NarrowedTests", "MethodCannotReenableClass", "MethodCannotReenableClass", "NotExecuted", "class condition excludes all", null),
        new("PeerTests", "Peer", "Peer", "Passed", null, "body:Peer"),
    ];

    public TestContext TestContext { get; set; } = null!;

    public static IEnumerable<TestDataRow<(string Scenario, MetadataMode MetadataMode)>> MtpCases
    {
        get
        {
            foreach (MetadataMode mode in MetadataModesToRun)
            {
                foreach (string scenario in new[] { "IncludeAll", "IncludeNone", "ExcludeAll", "ExcludeNone", "Absent" })
                {
                    yield return new((scenario, mode)) { DisplayName = $"AssemblyOS_{scenario}_{mode}" };
                }
            }
        }
    }

    [TestMethod]
    [DynamicData(nameof(MtpCases))]
    public async Task Mtp_AssemblyOSCondition_PreservesResultsReasonsAndLifecycle(string scenario, MetadataMode metadataMode)
    {
        // Per-case assets preserve the outer assembly's method-level scheduling without
        // sharing generated source, injection props, bin/obj, markers or report files.
        using TestAsset asset = await TestAsset.GenerateAssetAsync(AssetName, CreateSource(scenario));
        await BuildConsumerAsync(asset, metadataMode);
        var host = TestHost.LocateFrom(asset.TargetAssetPath, AssetName, TargetFrameworks.NetCurrent, metadataMode: metadataMode);
        using TempDirectory results = new();
        TestHostResult result = await host.ExecuteAsync(
            $"--show-test-results all --report-trx --report-trx-filename conditions.trx --results-directory \"{results.Path}\"",
            new()
            {
                [EvidenceEnvironmentVariable] = results.Path,
                ["TESTINGPLATFORM_UI_LANGUAGE"] = "en-US",
            },
            cancellationToken: TestContext.CancellationToken);

        await RecordRunAsync(asset, host, results.Path, result.Command, result.ExitCode, result.StandardOutput, result.StandardError);
        result.AssertExitCodeIs(0);
        result.AssertOutputContainsSummary(failed: 0, passed: AssemblyReason(scenario) is null ? 7 : 0, skipped: AssemblyReason(scenario) is null ? 3 : 10);
        AssertReport(results.Path, scenario);
        AssertLifecycle(results.Path, scenario);
        AssertProvenance(host, results.Path, metadataMode, useVSTest: false);
    }

    [TestMethod]
    [DataRow("IncludeAll", DisplayName = "AssemblyOS_VSTest_IncludeAll")]
    [DataRow("IncludeNone", DisplayName = "AssemblyOS_VSTest_IncludeNone")]
    public async Task Vstest_AssemblyOSCondition_PreservesResultsReasonsAndLifecycle(string scenario)
    {
        using TestAsset asset = await TestAsset.GenerateAssetAsync(AssetName, CreateSource(scenario));
        await BuildConsumerAsync(asset, MetadataMode.Reflection);
        var host = TestHost.LocateFrom(asset.TargetAssetPath, AssetName, TargetFrameworks.NetCurrent);
        using TempDirectory results = new();
        using CommandLine command = new();
        string dotnetRoot = Path.Combine(RootFinder.Find(), ".dotnet");
        string dotnet = Path.Combine(dotnetRoot, $"dotnet{Constants.ExecutableExtension}");
        string invocation = $"\"{dotnet}\" vstest \"{Path.Combine(host.DirectoryName, AssetName + ".dll")}\" /Logger:\"trx;LogFileName=conditions.trx\" /ResultsDirectory:\"{results.Path}\"";
        int exitCode = await command.RunAsyncAndReturnExitCodeAsync(
            invocation,
            environmentVariables: new Dictionary<string, string?>()
            {
                [EvidenceEnvironmentVariable] = results.Path,
                ["DOTNET_ROOT"] = dotnetRoot,
                ["DOTNET_CLI_UI_LANGUAGE"] = "en-US",
                ["VSLANG"] = "1033",
                ["TESTINGPLATFORM_AZDO_OUTPUT"] = "off",
                // vstest.console kills testhost 100 ms after closing the session by default,
                // which can interrupt the consumer's ProcessExit provenance probe.
                ["VSTEST_TESTHOST_SHUTDOWN_TIMEOUT"] = "60000",
            },
            workingDirectory: asset.TargetAssetPath,
            cancellationToken: TestContext.CancellationToken);

        await RecordRunAsync(asset, host, results.Path, invocation, exitCode, command.StandardOutput, command.ErrorOutput);
        Assert.AreEqual(0, exitCode, $"{command.StandardOutput}{Environment.NewLine}{command.ErrorOutput}");
        AssertReport(results.Path, scenario);
        AssertLifecycle(results.Path, scenario);
        AssertProvenance(host, results.Path, MetadataMode.Reflection, useVSTest: true);
    }

    private async Task BuildConsumerAsync(TestAsset asset, MetadataMode metadataMode)
    {
        string sourceGenArguments = metadataMode == MetadataMode.Reflection
            ? string.Empty
            : await AcceptanceSourceGen.PrepareBuildArgumentsAsync(asset.TargetAssetPath, metadataMode);
        string binlog = Path.Combine(asset.TargetAssetPath, "consumer-build.binlog");
        try
        {
            DotnetMuxerResult build = await DotnetCli.RunAsync(
                $"build \"{asset.TargetAssetPath}\" -c Release {sourceGenArguments} \"-bl:{binlog}\"",
                callerMemberName: $"AssemblyOS_{metadataMode}_{Guid.NewGuid():N}",
                cancellationToken: TestContext.CancellationToken);
            build.AssertExitCodeIs(0);
        }
        finally
        {
            AttachFile(binlog, "consumer-build.binlog");
        }
    }

    private async Task RecordRunAsync(TestAsset asset, TestHost host, string resultsDirectory, string command, int exitCode, string output, string error)
    {
        string log = Path.Combine(TestContext.TestTempDirectory!, "consumer-run.txt");
        await File.WriteAllTextAsync(log, $"""
            Consumer: {host.FullName}
            Asset: {asset.TargetAssetPath}
            Results: {resultsDirectory}
            MSTest: {MSTestVersion}
            MTP: {MicrosoftTestingPlatformVersion}
            Generator: {MSTestSourceGenerationVersion}
            Package cache: {Environment.GetEnvironmentVariable("NUGET_PACKAGES")}
            Command: {command}
            Exit: {exitCode}
            Standard output:
            {output}
            Standard error:
            {error}
            """, TestContext.CancellationToken);
        TestContext.AddResultFile(log);
        // Child TRX contains intentionally skipped cases; attach it as text rather
        // than another outer acceptance test-results module.
        foreach (string file in new[] { "conditions.trx", "lifecycle.txt", "products.txt", "metadata.txt" })
        {
            AttachFile(Path.Combine(resultsDirectory, file), file + ".txt");
        }

        foreach (string assets in Directory.GetFiles(Path.Combine(asset.TargetAssetPath, "obj"), "project.assets.json", SearchOption.AllDirectories))
        {
            AttachFile(assets, "consumer-project.assets.json");
        }
    }

    private void AttachFile(string source, string name)
    {
        if (File.Exists(source))
        {
            string destination = Path.Combine(TestContext.TestTempDirectory!, name);
            File.Copy(source, destination);
            TestContext.AddResultFile(destination);
        }
    }

    private static string? AssemblyReason(string scenario)
        => scenario switch
        {
            "IncludeNone" => IncludeReason,
            "ExcludeAll" => ExcludeReason,
            _ => null,
        };

    private static void AssertReport(string directory, string scenario)
    {
        XNamespace ns = "http://microsoft.com/schemas/VisualStudio/TeamTest/2010";
        var report = XDocument.Load(Path.Combine(directory, "conditions.trx"));
        XElement[] results = [.. report.Descendants(ns + "UnitTestResult").Where(result => !result.Descendants(ns + "UnitTestResult").Any())];
        var definitions = report.Descendants(ns + "UnitTest").ToDictionary(definition => definition.Attribute("id")!.Value);
        string? assemblyReason = AssemblyReason(scenario);

        Assert.AreSequenceEqual(
            ExpectedResults.Select(expected => (
                "AssemblyOSFeature." + expected.Class,
                expected.Method,
                expected.Name,
                assemblyReason is null ? expected.Outcome : "NotExecuted",
                assemblyReason ?? expected.Reason ?? string.Empty)),
            results.Select(result =>
            {
                XElement method = definitions[result.Attribute("testId")!.Value].Element(ns + "TestMethod")!;
                return (
                    method.Attribute("className")!.Value,
                    method.Attribute("name")!.Value,
                    result.Attribute("testName")!.Value,
                    result.Attribute("outcome")!.Value,
                    result.Element(ns + "Output")?.Element(ns + "ErrorInfo")?.Element(ns + "Message")?.Value ?? string.Empty);
            }),
            SequenceOrder.InAnyOrder);
        Assert.AreEqual(10, results.Select(result => result.Attribute("testId")!.Value).Distinct(StringComparer.Ordinal).Count());
        Assert.AreEqual(10, results.Select(result => result.Attribute("executionId")!.Value).Distinct(StringComparer.Ordinal).Count());
        foreach (XElement result in results)
        {
            Assert.AreNotEqual(Guid.Empty, Guid.Parse(result.Attribute("testId")!.Value));
            Assert.AreNotEqual(Guid.Empty, Guid.Parse(result.Attribute("executionId")!.Value));
        }

        XElement counters = report.Descendants(ns + "Counters").Single();
        Assert.AreEqual(10, (int)counters.Attribute("total")!);
        Assert.AreEqual(assemblyReason is null ? 7 : 0, (int)counters.Attribute("passed")!);
        Assert.AreEqual(assemblyReason is null ? 7 : 0, (int)counters.Attribute("executed")!);
        Assert.AreEqual(0, (int)counters.Attribute("failed")!);
        // VSTest's legacy notExecuted counter can be zero for skipped rows. The
        // exact leaf identities/outcomes above are authoritative for all 10 cases.
    }

    private static void AssertLifecycle(string directory, string scenario)
    {
        string path = Path.Combine(directory, "lifecycle.txt");
        string[] actual = File.Exists(path) ? File.ReadAllLines(path) : [];
        List<string> expected = [];
        if (AssemblyReason(scenario) is null)
        {
            expected.AddRange(["assembly.initialize", "assembly.cleanup", "AllowedTests.class.initialize", "AllowedTests.class.cleanup", "PeerTests.class.initialize", "PeerTests.class.cleanup"]);
            foreach (ExpectedResult result in ExpectedResults.Where(result => result.Outcome == "Passed"))
            {
                expected.AddRange(
                [
                    result.Class + ".constructor",
                    result.Class + ".test.initialize:" + result.Name,
                    result.Class + ".test.cleanup:" + result.Name,
                    result.Marker!,
                ]);
            }
        }

        Assert.AreSequenceEqual(expected, actual, SequenceOrder.InAnyOrder,
            "Rejected scopes must not construct tests, initialize, execute bodies or clean up; allowed scopes provide the positive control.");
    }

    private static void AssertProvenance(TestHost host, string directory, MetadataMode metadataMode, bool useVSTest)
    {
        string[] metadata = File.ReadAllLines(Path.Combine(directory, "metadata.txt"));
        Assert.AreSequenceEqual(
            new[]
            {
                metadataMode == MetadataMode.Reflection ? "ReflectionOperations" : "SourceGeneratedReflectionOperations",
                (metadataMode == MetadataMode.AotSourceGeneration).ToString(),
            },
            metadata);
        string[][] products = [.. File.ReadAllLines(Path.Combine(directory, "products.txt")).Select(line => line.Split('|'))];
        string[] required = useVSTest
            ? ["MSTest.TestFramework", "MSTestAdapter.PlatformServices"]
            : ["MSTest.TestFramework", "MSTestAdapter.PlatformServices", "Microsoft.Testing.Platform"];
        foreach (string name in required)
        {
            string[] product = Assert.ContainsSingle(products.Where(product => product[0] == name));
            Assert.HasCount(3, product);
            Assert.AreEqual(Path.GetFullPath(Path.Combine(host.DirectoryName, name + ".dll")), Path.GetFullPath(product[1]));
            (string packageId, string version) = name switch
            {
                "MSTest.TestFramework" => ("MSTest.TestFramework", MSTestVersion),
                "MSTestAdapter.PlatformServices" => ("MSTest.TestAdapter", MSTestVersion),
                _ => ("Microsoft.Testing.Platform", MicrosoftTestingPlatformVersion),
            };
            using ZipArchive package = ZipFile.OpenRead(Path.Combine(Constants.ArtifactsPackagesShipping, $"{packageId}.{version}.nupkg"));
            string[] currentPayloadHashes =
            [
                .. package.Entries.Where(entry => entry.Name == name + ".dll").Select(entry =>
                {
                    using Stream payload = entry.Open();
                    return Convert.ToHexString(SHA256.HashData(payload));
                }),
            ];
            Assert.Contains(product[2], currentPayloadHashes, $"{name} loaded by the consumer must match the current shipping package.");
        }
    }

    private static string CreateSource(string scenario)
    {
        string assemblyGuard = scenario switch
        {
            "IncludeAll" => "[assembly: OSCondition(Masks.All)]",
            "IncludeNone" => $"[assembly: OSCondition(ConditionMode.Include, Masks.None, IgnoreMessage = \"{IncludeReason}\")]",
            "ExcludeAll" => "[assembly: OSCondition(ConditionMode.Exclude, Masks.All)]",
            "ExcludeNone" => "[assembly: OSCondition(ConditionMode.Exclude, Masks.None)]",
            "Absent" => string.Empty,
            _ => throw new ArgumentOutOfRangeException(nameof(scenario)),
        };
        return SourceCode
            .PatchCodeWithReplace("$AssemblyGuard$", assemblyGuard)
            .PatchCodeWithReplace("$TargetFramework$", TargetFrameworks.NetCurrent)
            .PatchCodeWithReplace("$MSTestVersion$", MSTestVersion)
            .PatchCodeWithReplace("$MTPVersion$", MicrosoftTestingPlatformVersion)
            .PatchCodeWithReplace("$TestSdkVersion$", MicrosoftNETTestSdkVersion);
    }

    private sealed record ExpectedResult(string Class, string Method, string Name, string Outcome, string? Reason, string? Marker);

    private const string SourceCode = """
#file AssemblyOSConditionConsumer.csproj
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <EnableMSTestRunner>true</EnableMSTestRunner>
    <TargetFramework>$TargetFramework$</TargetFramework>
    <LangVersion>12</LangVersion>
    <Nullable>enable</Nullable>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="MSTest.TestFramework" Version="$MSTestVersion$" />
    <PackageReference Include="MSTest.TestAdapter" Version="$MSTestVersion$" />
    <PackageReference Include="Microsoft.Testing.Extensions.TrxReport" Version="$MTPVersion$" />
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="$TestSdkVersion$" />
  </ItemGroup>
</Project>

#file Tests.cs
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using AssemblyOSFeature;

[assembly: DoNotParallelize]
$AssemblyGuard$

namespace AssemblyOSFeature;

public static class Masks
{
    public const OperatingSystems All = OperatingSystems.Windows | OperatingSystems.Linux | OperatingSystems.OSX | OperatingSystems.FreeBSD;
    public const OperatingSystems None = (OperatingSystems)0;
}

internal static class Probe
{
    private static string Evidence => Environment.GetEnvironmentVariable("ASSEMBLY_OS_CONDITION_EVIDENCE")!;

    // This is a process/module probe, deliberately not a test/assembly initializer.
    // It still records the loaded runner when the entire test assembly is rejected.
    [ModuleInitializer]
    internal static void RegisterProvenance()
        => AppDomain.CurrentDomain.ProcessExit += (_, _) => RecordProvenance();

    internal static void Record(string value)
        => File.AppendAllText(Path.Combine(Evidence, "lifecycle.txt"), value + Environment.NewLine);

    private static void RecordProvenance()
    {
        Assembly[] loaded = AppDomain.CurrentDomain.GetAssemblies();
        Assembly adapter = loaded.Single(assembly => assembly.GetName().Name == "MSTestAdapter.PlatformServices");
        Type providerType = adapter.GetType("Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.PlatformServiceProvider", throwOnError: true)!;
        object provider = providerType.GetProperty("Instance", BindingFlags.Static | BindingFlags.NonPublic)!.GetValue(null)!;
        object operations = provider.GetType().GetProperty("ReflectionOperations")!.GetValue(provider)!;
        string[] products = loaded
            .Where(assembly => !assembly.IsDynamic && assembly.GetName().Name is "MSTest.TestFramework" or "MSTestAdapter.PlatformServices" or "Microsoft.Testing.Platform")
            .Select(assembly => $"{assembly.GetName().Name}|{assembly.Location}|{Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(assembly.Location)))}")
            .ToArray();
        // Publish each file atomically and write metadata last, so an interrupted
        // probe cannot leave partial evidence behind a complete-looking metadata file.
        WriteAtomically("products.txt", products);
        WriteAtomically("metadata.txt", new[]
        {
            operations.GetType().Name,
            (typeof(Probe).Assembly.GetType("MSTest.SourceGenerated.MSTestReflectionMetadata") is not null).ToString(),
        });
    }

    private static void WriteAtomically(string fileName, string[] lines)
    {
        string path = Path.Combine(Evidence, fileName);
        string temporaryPath = path + ".tmp";
        File.WriteAllLines(temporaryPath, lines);
        File.Move(temporaryPath, path, overwrite: true);
    }
}

[AttributeUsage(AttributeTargets.Method, AllowMultiple = true, Inherited = false)]
public sealed class AlternativeConditionAttribute : ConditionBaseAttribute
{
    public AlternativeConditionAttribute() : base(ConditionMode.Include) { }
    public override bool IsConditionMet => true;
    public override string GroupName => "OSCondition";
}

[TestClass]
public abstract class Lifecycle
{
    protected Lifecycle() => Probe.Record(GetType().Name + ".constructor");
    public TestContext TestContext { get; set; } = null!;

    [TestInitialize]
    public void Initialize() => Probe.Record(GetType().Name + ".test.initialize:" + TestContext.TestDisplayName);

    [TestCleanup]
    public void Cleanup() => Probe.Record(GetType().Name + ".test.cleanup:" + TestContext.TestDisplayName);
}

[TestClass]
public sealed class AllowedTests : Lifecycle
{
    [AssemblyInitialize]
    public static void InitializeAssembly(TestContext context) => Probe.Record("assembly.initialize");

    [AssemblyCleanup]
    public static void CleanupAssembly() => Probe.Record("assembly.cleanup");

    [ClassInitialize]
    public static void InitializeClass(TestContext context) => Probe.Record("AllowedTests.class.initialize");

    [ClassCleanup]
    public static void CleanupClass() => Probe.Record("AllowedTests.class.cleanup");

    [TestMethod]
    public void Plain() => Probe.Record("body:Plain");

    [TestMethod]
    [OSCondition(Masks.All)]
    public void MethodIncludeMatches() => Probe.Record("body:MethodIncludeMatches");

    [TestMethod]
    [OSCondition(Masks.None, IgnoreMessage = "method include excludes all")]
    public void MethodIncludeMisses() => Probe.Record("unexpected:MethodIncludeMisses");

    [TestMethod]
    [OSCondition(ConditionMode.Exclude, Masks.All, IgnoreMessage = "method exclude blocks all")]
    public void MethodExcludeMatches() => Probe.Record("unexpected:MethodExcludeMatches");

    [TestMethod]
    [OSCondition(ConditionMode.Exclude, Masks.None)]
    public void MethodExcludeMisses() => Probe.Record("body:MethodExcludeMisses");

    [TestMethod]
    [OSCondition(Masks.None)]
    [AlternativeCondition]
    public void SameScopeAlternative() => Probe.Record("body:SameScopeAlternative");

    [TestMethod(UnfoldingStrategy = TestDataSourceUnfoldingStrategy.Unfold)]
    [DataRow(11, DisplayName = "RowEleven")]
    [DataRow(29, DisplayName = "RowTwentyNine")]
    public void Rows(int value) => Probe.Record("body:Rows:" + value);
}

[TestClass]
[OSCondition(Masks.None, IgnoreMessage = "class condition excludes all")]
public sealed class NarrowedTests : Lifecycle
{
    [ClassInitialize]
    public static void InitializeClass(TestContext context) => Probe.Record("unexpected:NarrowedTests.class.initialize");

    [ClassCleanup]
    public static void CleanupClass() => Probe.Record("unexpected:NarrowedTests.class.cleanup");

    [TestMethod]
    [OSCondition(Masks.All)]
    public void MethodCannotReenableClass() => Probe.Record("unexpected:MethodCannotReenableClass");
}

[TestClass]
public sealed class PeerTests : Lifecycle
{
    [ClassInitialize]
    public static void InitializeClass(TestContext context) => Probe.Record("PeerTests.class.initialize");

    [ClassCleanup]
    public static void CleanupClass() => Probe.Record("PeerTests.class.cleanup");

    [TestMethod]
    public void Peer() => Probe.Record("body:Peer");
}
""";
}
