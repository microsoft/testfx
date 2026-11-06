// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.IO.Compression;
using System.Security.Cryptography;
using System.Xml.Linq;

using Microsoft.Testing.Platform.Acceptance.IntegrationTests;
using Microsoft.Testing.Platform.Acceptance.IntegrationTests.Helpers;

namespace MSTest.Acceptance.IntegrationTests;

[TestClass]
[DoNotParallelize]
public sealed class CleanupCancellationIsolationTests : AcceptanceTestBase<CleanupCancellationIsolationTests.TestAssetFixture>
{
    private static string ConsumerTargetFramework => TargetFrameworks.Net[^1];

    public static IEnumerable<TestDataRow<(string Scenario, MetadataMode MetadataMode, bool NonCooperativeTimeout)>> CleanupCases
    {
        get
        {
            yield return new(("normal", MetadataMode.Reflection, false)) { DisplayName = "GlobalCleanup_Normal" };
            yield return new(("error", MetadataMode.Reflection, false)) { DisplayName = "GlobalCleanup_UnrelatedLocalFailure" };
            yield return new(("cancel-return", MetadataMode.Reflection, false)) { DisplayName = "GlobalCleanup_LocalCancellationWithoutException" };
            yield return new(("cancel-throw", MetadataMode.Reflection, false)) { DisplayName = "GlobalCleanup_LocalCancellationException" };
            yield return new(("cancel-unrelated", MetadataMode.Reflection, false)) { DisplayName = "GlobalCleanup_CancellationWithUnrelatedLocalFailure" };
            yield return new(("timeout", MetadataMode.Reflection, false)) { DisplayName = "GlobalCleanup_LocalCooperativeTimeout" };
            yield return new(("timeout-global-throw", MetadataMode.Reflection, false)) { DisplayName = "GlobalCleanup_PreservesLocalTimeoutWhenGlobalCleanupThrows" };
            yield return new(("timeout", MetadataMode.Reflection, true)) { DisplayName = "GlobalCleanup_LocalCooperativeTimeout_NonCooperativeTest" };
            yield return new(("global-cancel-return", MetadataMode.Reflection, true)) { DisplayName = "GlobalCleanup_TerminalCancellation_NonCooperativeTest" };
            if (!AcceptanceSourceGen.IsGloballyDisabled)
            {
                yield return new(("timeout", MetadataMode.SourceGeneration, false)) { DisplayName = "GlobalCleanup_LocalCooperativeTimeout_SourceGeneration" };
                yield return new(("timeout", MetadataMode.SourceGeneration, true)) { DisplayName = "GlobalCleanup_LocalCooperativeTimeout_NonCooperativeTest_SourceGeneration" };
                yield return new(("global-cancel-return", MetadataMode.SourceGeneration, true)) { DisplayName = "GlobalCleanup_TerminalCancellation_NonCooperativeTest_SourceGeneration" };
            }
        }
    }

    [TestMethod]
    [DynamicData(nameof(CleanupCases))]
    public async Task GlobalCleanupAfterLocalCancellationReleasesOwnedResource(string scenario, MetadataMode metadataMode, bool nonCooperativeTimeout)
    {
        using TempDirectory results = new();
        string evidenceDirectory = Path.Combine(results.Path, "evidence");
        using ZipArchive package = ZipFile.OpenRead(Path.Combine(Constants.ArtifactsPackagesShipping, $"MSTest.TestAdapter.{MSTestVersion}.nupkg"));
        ZipArchiveEntry? adapterPayload = package.GetEntry($"buildTransitive/{ConsumerTargetFramework}/MSTestAdapter.PlatformServices.dll");
        Assert.IsNotNull(adapterPayload);
        using Stream adapterStream = adapterPayload.Open();
        string expectedAdapterHash = Convert.ToHexString(SHA256.HashData(adapterStream));
        string filter = nonCooperativeTimeout
            ? "Name=NoBaseTestNonCooperative|Name=WithBaseTestNonCooperative"
            : "Name=NoBaseTest|Name=WithBaseTest";
        var host = TestHost.LocateFrom(AssetFixture.ProjectPath, TestAssetFixture.ProjectName, ConsumerTargetFramework, metadataMode: metadataMode);
        TestHostResult result = await host.ExecuteAsync(
            $"--filter \"{filter}\" --report-trx --report-trx-filename cleanup.trx --results-directory \"{results.Path}\"",
            environmentVariables: new()
            {
                ["LOCAL_CLEANUP_SCENARIO"] = scenario,
                ["CLEANUP_EVIDENCE_DIRECTORY"] = evidenceDirectory,
                ["NONCOOPERATIVE_TEST_TIMEOUT"] = nonCooperativeTimeout.ToString(),
                ["DOTNET_CLI_UI_LANGUAGE"] = "en-US",
            },
            cancellationToken: TestContext.CancellationToken);

        string[] expectedClasses = ["NoBaseCleanup", "WithBaseCleanup"];
        foreach (string className in expectedClasses)
        {
            string[] provenance = File.ReadAllLines(Path.Combine(evidenceDirectory, className, "product-provenance.txt"));
            Assert.HasCount(3, provenance);
            Assert.AreEqual("MSTestAdapter.PlatformServices.dll", Path.GetFileName(provenance[0]));
            Assert.Contains(MSTestVersion, provenance[1]);
            Assert.AreEqual(expectedAdapterHash, provenance[2], $"Loaded adapter does not match the current package for {className}.");
            TestContext.WriteLine($"Loaded adapter for {className}: {string.Join(" | ", provenance)}");
        }

        bool passed = scenario == "normal";
        result.AssertExitCodeIs(passed ? 0 : 2);
        result.AssertOutputContainsSummary(failed: passed ? 0 : 2, passed: passed ? 2 : 0, skipped: 0);

        XNamespace ns = "http://microsoft.com/schemas/VisualStudio/TeamTest/2010";
        var report = XDocument.Load(Path.Combine(results.Path, "cleanup.trx"));
        XElement[] testResults = report.Descendants(ns + "UnitTestResult")
            .Where(testResult => !testResult.Descendants(ns + "UnitTestResult").Any())
            .ToArray();
        string[] expectedNames = nonCooperativeTimeout
            ? ["NoBaseTestNonCooperative", "WithBaseTestNonCooperative"]
            : ["NoBaseTest", "WithBaseTest"];
        Assert.AreSequenceEqual(
            expectedNames,
            testResults.Select(testResult => (string?)testResult.Attribute("testName")).OrderBy(name => name, StringComparer.Ordinal).ToArray());
        Assert.AreEqual(2, testResults.Select(testResult => (string?)testResult.Attribute("executionId")).Distinct().Count());
        var classes = report.Descendants(ns + "UnitTest")
            .ToDictionary(
                definition => (string)definition.Attribute("id")!,
                definition => (string)definition.Element(ns + "TestMethod")!.Attribute("className")!,
                StringComparer.Ordinal);

        foreach (XElement testResult in testResults)
        {
            string testName = (string)testResult.Attribute("testName")!;
            string className = testName == expectedNames[0] ? expectedClasses[0] : expectedClasses[1];
            Assert.AreEqual(className, classes[(string)testResult.Attribute("testId")!]);
            Assert.AreEqual(passed ? "Passed" : "Failed", (string?)testResult.Attribute("outcome"), testName);

            string directory = Path.Combine(evidenceDirectory, className);
            Assert.IsFalse(File.Exists(Path.Combine(directory, "owned-resource.txt")), $"Global cleanup did not release {className}'s resource in {scenario}.");
            string expectedTrace = scenario is ("normal" or "global-cancel-return") && className == "WithBaseCleanup"
                ? """
                  global-initialize
                  test-body
                  local-cleanup
                  base-cleanup
                  dispose
                  global-cleanup

                  """
                : """
                  global-initialize
                  test-body
                  local-cleanup
                  dispose
                  global-cleanup

                  """;
            Assert.AreEqual(
                expectedTrace.ReplaceLineEndings(),
                File.ReadAllText(Path.Combine(directory, "events.txt")).ReplaceLineEndings(),
                className);

            if (scenario.StartsWith("timeout", StringComparison.Ordinal))
            {
                string error = testResult.Descendants(ns + "Message").Single().Value;
                Assert.Contains($"{className}.Cleanup", error);
                Assert.Contains("timed out after 1000ms", error);
                if (scenario == "timeout-global-throw")
                {
                    Assert.Contains("global cleanup also failed", error);
                    Assert.Contains("GlobalFixtures.Cleanup", error);
                }
            }
            else if (scenario is "error" or "cancel-unrelated")
            {
                Assert.Contains("local cleanup failed", testResult.Descendants(ns + "Message").Single().Value);
            }
            else if (scenario == "global-cancel-return")
            {
                Assert.AreEqual($"Test '{testName}' was canceled", testResult.Descendants(ns + "Message").Single().Value);
            }
        }
    }

    public TestContext TestContext { get; set; } = null!;

    public sealed class TestAssetFixture : TestAssetFixtureBase
    {
        public const string ProjectName = "CleanupCancellationIsolation";

        public string ProjectPath => GetAssetPath(ProjectName);

        public override (string ID, string Name, string Code) GetAssetsToGenerate()
            => (ProjectName, ProjectName, SourceCode
                .PatchTargetFrameworks(ConsumerTargetFramework)
                .PatchCodeWithReplace("$MSTestVersion$", MSTestVersion)
                .PatchCodeWithReplace("$MicrosoftTestingPlatformVersion$", MicrosoftTestingPlatformVersion));

        private const string SourceCode = """
#file CleanupCancellationIsolation.csproj
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <EnableMSTestRunner>true</EnableMSTestRunner>
    <TargetFrameworks>$TargetFrameworks$</TargetFrameworks>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="MSTest.TestAdapter" Version="$MSTestVersion$" />
    <PackageReference Include="MSTest.TestFramework" Version="$MSTestVersion$" />
    <PackageReference Include="Microsoft.Testing.Extensions.TrxReport" Version="$MicrosoftTestingPlatformVersion$" />
  </ItemGroup>
</Project>

#file Tests.cs
using System.Reflection;
using System.Security.Cryptography;
using Microsoft.VisualStudio.TestTools.UnitTesting;

[assembly: DoNotParallelize]

[TestClass]
public sealed class GlobalFixtures
{
    [GlobalTestInitialize]
    public static void Initialize(TestContext context)
    {
        Directory.CreateDirectory(Probe.DirectoryFor(context));
        File.WriteAllText(Probe.ResourceFor(context), "resource owned by global initialization");
        Probe.Record(context, "global-initialize");
        Assembly[] adapters = AppDomain.CurrentDomain.GetAssemblies()
            .Where(assembly => assembly.GetName().Name == "MSTestAdapter.PlatformServices")
            .ToArray();
        if (adapters.Length != 1)
        {
            throw new InvalidOperationException($"Expected one loaded MSTestAdapter.PlatformServices assembly, found {adapters.Length}.");
        }

        Assembly adapter = adapters[0];
        File.WriteAllLines(Path.Combine(Probe.DirectoryFor(context), "product-provenance.txt"),
        [
            adapter.Location,
            adapter.GetCustomAttribute<AssemblyInformationalVersionAttribute>()!.InformationalVersion,
            Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(adapter.Location))),
        ]);
    }

    [GlobalTestCleanup]
    public static async Task Cleanup(TestContext context)
    {
        Probe.Record(context, "global-cleanup");
        if (Environment.GetEnvironmentVariable("NONCOOPERATIVE_TEST_TIMEOUT") == "True")
        {
            await Task.Delay(500, context.CancellationToken);
        }

        File.Delete(Probe.ResourceFor(context));
        if (Probe.Scenario == "global-cancel-return")
        {
            context.CancellationTokenSource.Cancel();
        }

        if (Probe.Scenario == "timeout-global-throw")
        {
            throw new InvalidOperationException("global cleanup also failed");
        }
    }
}

[TestClass]
public abstract class ProbeBase : IDisposable
{
    public TestContext TestContext { get; set; } = null!;

    public void Dispose() => Probe.Record(TestContext, "dispose");
}

[TestClass]
public abstract class InheritedCleanup : ProbeBase
{
    [TestCleanup]
    public void BaseCleanup() => Probe.Record(TestContext, "base-cleanup");
}

[TestClass]
public sealed class NoBaseCleanup : ProbeBase
{
    [TestMethod]
    public void NoBaseTest() => Probe.Record(TestContext, "test-body");

    [TestMethod]
    [Timeout(10000)]
    public void NoBaseTestNonCooperative() => Probe.Record(TestContext, "test-body");

    [TestCleanup]
    [Timeout(1000, CooperativeCancellation = true)]
    public async Task Cleanup() => await Probe.CleanupAsync(TestContext);
}

[TestClass]
public sealed class WithBaseCleanup : InheritedCleanup
{
    [TestMethod]
    public void WithBaseTest() => Probe.Record(TestContext, "test-body");

    [TestMethod]
    [Timeout(10000)]
    public void WithBaseTestNonCooperative() => Probe.Record(TestContext, "test-body");

    [TestCleanup]
    [Timeout(1000, CooperativeCancellation = true)]
    public async Task Cleanup() => await Probe.CleanupAsync(TestContext);
}

internal static class Probe
{
    public static string Scenario => Environment.GetEnvironmentVariable("LOCAL_CLEANUP_SCENARIO")
        ?? throw new InvalidOperationException("LOCAL_CLEANUP_SCENARIO must be set.");

    public static string DirectoryFor(TestContext context) => Path.Combine(
        Environment.GetEnvironmentVariable("CLEANUP_EVIDENCE_DIRECTORY")
            ?? throw new InvalidOperationException("CLEANUP_EVIDENCE_DIRECTORY must be set."),
        context.FullyQualifiedTestClassName);

    public static string ResourceFor(TestContext context) => Path.Combine(DirectoryFor(context), "owned-resource.txt");

    public static void Record(TestContext context, string step)
        => File.AppendAllText(Path.Combine(DirectoryFor(context), "events.txt"), step + Environment.NewLine);

    public static async Task CleanupAsync(TestContext context)
    {
        Record(context, "local-cleanup");
        if (Scenario.StartsWith("timeout", StringComparison.Ordinal))
        {
            await Task.Delay(Timeout.Infinite, context.CancellationToken);
        }
        else if (Scenario is "cancel-return" or "cancel-throw" or "cancel-unrelated")
        {
            context.CancellationTokenSource.Cancel();
            if (Scenario == "cancel-throw")
            {
                context.CancellationToken.ThrowIfCancellationRequested();
            }
        }

        if (Scenario is "error" or "cancel-unrelated")
        {
            throw new InvalidOperationException("local cleanup failed");
        }
    }
}
""";
    }
}
