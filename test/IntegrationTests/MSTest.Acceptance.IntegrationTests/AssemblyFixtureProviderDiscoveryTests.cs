// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;

using Microsoft.Testing.Platform.Acceptance.IntegrationTests;
using Microsoft.Testing.Platform.Acceptance.IntegrationTests.Helpers;

namespace MSTest.Acceptance.IntegrationTests;

[TestClass]
public sealed partial class AssemblyFixtureProviderDiscoveryTests : AcceptanceTestBase<NopAssetFixture>
{
    private const string AssetName = "FixtureDiscovery";
    private const string ProjectName = "FixtureConsumer";
    private const string ChildRunReportName = "child-results.trx";
    private const string ChildReportName = "child-results.trx.xml";

    public TestContext TestContext { get; set; }

    public static IEnumerable<object[]> UnmarkedCases
        => ExecutionCases.SelectMany(execution => new[] { "none", "initialize", "cleanup", "both" }
            .Select(hooks => new object[] { execution[0], execution[1], hooks }));

    public static IEnumerable<object[]> ProviderCases
        => ExecutionCases.SelectMany(execution => new[] { "direct", "transitive", "consumer" }
            .Select(placement => new object[] { execution[0], execution[1], placement }));

    public static IEnumerable<object[]> LocalCases
        => ExecutionCases.SelectMany(execution => new[] { "initialize", "cleanup", "both" }
            .Select(hooks => new object[] { execution[0], execution[1], hooks }));

    public static IEnumerable<object[]> DuplicateCases
        => ExecutionCases.SelectMany(execution => new[] { "initialize", "cleanup" }
            .Select(slot => new object[] { execution[0], execution[1], slot }));

    private static IEnumerable<object[]> ExecutionCases
    {
        get
        {
            string[] frameworks = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                ? [TargetFrameworks.NetCurrent, "net472"]
                : [TargetFrameworks.NetCurrent];
            foreach (string framework in frameworks)
            {
                yield return [framework, "MTP"];
                yield return [framework, "VSTest"];
            }
        }
    }

    [TestMethod]
    [DynamicData(nameof(UnmarkedCases))]
    public async Task UnmarkedDependenciesRemainUnloadedAndDeletable(string tfm, string runner, string hooks)
    {
        using TestAsset asset = await CreateAssetAsync(tfm, hooks, "UnmarkedDependency", "UnmarkedDependency.Marker", """
        foreach (string name in new[] { "UnmarkedDependency", "UnmarkedLeaf" })
        {
            bool loaded = AppDomain.CurrentDomain.GetAssemblies().Any(assembly => assembly.GetName().Name == name);
            string path = Path.Combine(Path.GetDirectoryName(typeof(DiscoveryTests).Assembly.Location)!, name + ".dll");
            Assert.IsTrue(File.Exists(path), "The dependency must exist before testing deletion: " + path);
            Exception? deletionFailure = null;
            try
            {
                File.Delete(path);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                deletionFailure = exception;
            }

            Console.WriteLine(name + ": loaded=" + loaded + "; deleted=" + !File.Exists(path) + "; error=" + deletionFailure);
            Assert.IsFalse(loaded, name + " was loaded by fixture discovery. Deletion error: " + deletionFailure);
            Assert.IsNull(deletionFailure, "A metadata probe must not retain a DLL handle: " + deletionFailure);
            Assert.IsFalse(File.Exists(path), "The unused dependency must be deletable: " + path);
        }
        """, useVSTest: runner == "VSTest");

        await BuildConsumerAsync(asset);
        await RunConsumerAsync(asset, tfm, runner, ExpectedLocalEvents(hooks));
    }

    [TestMethod]
    [DynamicData(nameof(ProviderCases))]
    public async Task PassiveProvidersAreDiscoveredWithoutExecutingReferenceMethods(string tfm, string runner, string placement)
    {
        string reference = placement == "transitive" ? "Intermediate" : "ProviderLibrary";
        string referenceType = placement == "transitive" ? "Intermediate.Marker" : "ProviderLibrary.SharedFixtures";
        using TestAsset asset = await CreateAssetAsync(tfm, "none", reference, referenceType, ProviderAssertions,
            consumerMarker: placement == "consumer", libraryMarker: placement != "consumer", useVSTest: runner == "VSTest");

        await BuildConsumerAsync(asset);
        await RunConsumerAsync(asset, tfm, runner, "provider-initialize | test | provider-cleanup");
    }

    [TestMethod]
    [DynamicData(nameof(LocalCases))]
    public async Task LocalFixturesOverrideOnlyTheirOwnProviderSlot(string tfm, string runner, string hooks)
    {
        string assertions = hooks == "both"
            ? """
        Assert.IsFalse(AppDomain.CurrentDomain.GetAssemblies().Any(assembly => assembly.GetName().Name == "ProviderLibrary"),
            "Both local hooks must bypass provider discovery.");
        """
            : ProviderAssertions.Replace("$ExpectedInitialized$", hooks == "initialize" ? "false" : "true", StringComparison.Ordinal);
        using TestAsset asset = await CreateAssetAsync(tfm, hooks, "ProviderLibrary", "ProviderLibrary.SharedFixtures", assertions, useVSTest: runner == "VSTest");

        await BuildConsumerAsync(asset);
        string expectedEvents = hooks switch
        {
            "initialize" => "local-initialize | test | provider-cleanup",
            "cleanup" => "provider-initialize | test | local-cleanup",
            _ => "local-initialize | test | local-cleanup",
        };
        await RunConsumerAsync(asset, tfm, runner, expectedEvents);
    }

    [TestMethod]
    [DynamicData(nameof(DuplicateCases))]
    public async Task DuplicateProviderSlotsRetainTheirDiagnostics(string tfm, string runner, string slot)
    {
        using TestAsset asset = await CreateAssetAsync(tfm, "none", "ProviderLibrary", "ProviderLibrary.SharedFixtures",
            "Assert.Fail(\"A duplicate provider must prevent the test body from running.\");",
            duplicateSlot: slot, useVSTest: runner == "VSTest");

        await BuildConsumerAsync(asset);
        await RunConsumerAsync(asset, tfm, runner, string.Empty, expectedDiagnostic: slot == "initialize" ? "UTA013" : "UTA014");
    }

    private static string ExpectedLocalEvents(string hooks) => hooks switch
    {
        "initialize" => "local-initialize | test",
        "cleanup" => "test | local-cleanup",
        "both" => "local-initialize | test | local-cleanup",
        _ => "test",
    };

    private static Task<TestAsset> CreateAssetAsync(
        string tfm,
        string hooks,
        string reference,
        string referenceType,
        string assertions,
        bool consumerMarker = false,
        bool libraryMarker = true,
        string? duplicateSlot = null,
        string extraSource = "",
        string signing = "",
        bool useVSTest = false)
    {
        string initialize = hooks is "initialize" or "both"
            ? """
    [AssemblyInitialize]
    public static void Initialize(TestContext context) => Events.Record("local-initialize");
"""
            : string.Empty;
        string cleanup = hooks is "cleanup" or "both"
            ? """
    [AssemblyCleanup]
    public static void Cleanup() => Events.Record("local-cleanup");
"""
            : string.Empty;
        string duplicate = duplicateSlot switch
        {
            "initialize" => """
    [AssemblyInitialize]
    public static void Initialize(TestContext context) { }
""",
            "cleanup" => """
    [AssemblyCleanup]
    public static void Cleanup() { }
""",
            _ => string.Empty,
        };

        return TestAsset.GenerateAssetAsync(AssetName, (SourceCode + extraSource)
            .PatchCodeWithReplace("$TargetFramework$", tfm)
            .PatchCodeWithReplace("$MSTestVersion$", MSTestVersion)
            .PatchCodeWithReplace("$MicrosoftTestingPlatformVersion$", MicrosoftTestingPlatformVersion)
            .PatchCodeWithReplace("$MicrosoftNETTestSdkVersion$", MicrosoftNETTestSdkVersion)
            .PatchCodeWithReplace("$Reference$", reference)
            .PatchCodeWithReplace("$ReferenceType$", referenceType)
            .PatchCodeWithReplace("$Assertions$", assertions.Replace("$ExpectedInitialized$", "true", StringComparison.Ordinal))
            .PatchCodeWithReplace("$Initialize$", initialize)
            .PatchCodeWithReplace("$Cleanup$", cleanup)
            .PatchCodeWithReplace("$ConsumerMarker$", consumerMarker ? "[assembly: AssemblyFixtureProvider(typeof(ProviderLibrary.SharedFixtures))]" : string.Empty)
            .PatchCodeWithReplace("$LibraryMarker$", libraryMarker ? "[assembly: AssemblyFixtureProvider(typeof(ProviderLibrary.SharedFixtures))]" : string.Empty)
            .PatchCodeWithReplace("$DuplicateMarker$", duplicateSlot is not null ? "[assembly: AssemblyFixtureProvider(typeof(ProviderLibrary.SecondFixtures))]" : string.Empty)
            .PatchCodeWithReplace("$DuplicateMethod$", duplicate)
            .PatchCodeWithReplace("$Signing$", signing)
            .PatchCodeWithReplace("$DeferredResolution$", "false")
            .PatchCodeWithReplace("$UseVSTest$", useVSTest.ToString().ToLowerInvariant()));
    }

    private async Task BuildConsumerAsync(TestAsset asset, string projectDirectory = "Tests", string projectName = ProjectName)
    {
        string project = Path.Combine(asset.TargetAssetPath, projectDirectory, projectName + ".csproj");
        DotnetMuxerResult build = await DotnetCli.RunAsync(
            $"build \"{project}\" -c Release",
            workingDirectory: asset.TargetAssetPath,
            cancellationToken: TestContext.CancellationToken);
        TestContext.WriteLine($"Packed consumer build: {build.Args}; exit: {build.ExitCode}");
        build.AssertExitCodeIs(0);
    }

    private async Task RunConsumerAsync(
        TestAsset asset,
        string tfm,
        string runner,
        string expectedEvents,
        string? expectedDiagnostic = null,
        bool disableAppDomain = true,
        bool assertFallbackTrace = false,
        string hostProjectDirectory = "Tests",
        string hostProjectName = ProjectName)
    {
        string outputDirectory = Path.Combine(asset.TargetAssetPath, "Tests", "bin", "Release", tfm);
        string evidenceDirectory = TestContext.TestTempDirectory!;
        string eventsPath = Path.Combine(evidenceDirectory, "events.txt");
        string loadedPath = Path.Combine(evidenceDirectory, "loaded-binaries.txt");
        string diagnosticsDirectory = Path.Combine(evidenceDirectory, "diagnostics");
        Directory.CreateDirectory(diagnosticsDirectory);
        string settings = Path.Combine(asset.TargetAssetPath, "discovery.runsettings");
        new XDocument(
            new XElement(
                "RunSettings",
                new XElement("RunConfiguration", new XElement("DisableAppDomain", disableAppDomain)),
                new XElement(
                    "MSTest",
                    new XElement("DeploymentEnabled", false),
                    new XElement("CaptureTraceOutput", false))))
            .Save(settings);
        var environment = new Dictionary<string, string?>
        {
            ["FIXTURE_DISCOVERY_EVENTS"] = eventsPath,
            ["FIXTURE_DISCOVERY_LOADED"] = loadedPath,
        };
        int exitCode;
        string output;
        string invocation;
        if (runner == "MTP")
        {
            var host = TestHost.LocateFrom(Path.Combine(asset.TargetAssetPath, hostProjectDirectory), hostProjectName, tfm);
            TestHostResult result = await host.ExecuteAsync(
                $"--settings \"{settings}\" --report-trx --report-trx-filename {ChildRunReportName} --results-directory \"{evidenceDirectory}\""
                    + (assertFallbackTrace ? $" --diagnostic --diagnostic-verbosity Trace --diagnostic-output-directory \"{diagnosticsDirectory}\"" : string.Empty),
                environmentVariables: environment,
                cancellationToken: TestContext.CancellationToken);
            exitCode = result.ExitCode;
            output = result.StandardOutput + Environment.NewLine + result.StandardError;
            invocation = result.Command;
        }
        else
        {
            string dotnetRoot = Path.Combine(RootFinder.Find(), ".dotnet");
            environment["DOTNET_ROOT"] = dotnetRoot;
            string dotnet = Path.Combine(dotnetRoot, "dotnet" + Constants.ExecutableExtension);
            string assembly = Path.Combine(outputDirectory, ProjectName + (tfm.StartsWith("net4", StringComparison.Ordinal) ? ".exe" : ".dll"));
            using var command = new CommandLine();
            invocation = $"\"{dotnet}\" vstest \"{assembly}\" /Settings:\"{settings}\" /Logger:\"trx;LogFileName={ChildRunReportName}\""
                    + $" /ResultsDirectory:\"{evidenceDirectory}\""
                    + (assertFallbackTrace ? $" /Diag:\"{Path.Combine(diagnosticsDirectory, "vstest.log")}\"" : string.Empty);
            exitCode = await command.RunAsyncAndReturnExitCodeAsync(
                invocation,
                environmentVariables: environment,
                workingDirectory: asset.TargetAssetPath,
                cancellationToken: TestContext.CancellationToken);
            output = command.StandardOutput + Environment.NewLine + command.ErrorOutput;
        }

        TestContext.WriteLine($"Packed {runner} command: {invocation}; exit: {exitCode}{Environment.NewLine}{output}");
        string runReportPath = Path.Combine(evidenceDirectory, ChildRunReportName);
        string reportPath = Path.Combine(evidenceDirectory, ChildReportName);
        Assert.IsTrue(File.Exists(runReportPath), $"The child must produce a TRX report.{Environment.NewLine}{output}");
        // Keep the child report's bytes without presenting it as another outer test module.
        File.Move(runReportPath, reportPath);
        TestContext.AddResultFile(reportPath);
        VerifyPackageProvenance(asset, tfm, loadedPath, expectedDiagnostic is null, runner == "VSTest");
        // A duplicate is a source-discovery error, not an executed failing test.
        int expectedExit = expectedDiagnostic is null ? 0 : runner == "MTP" ? 7 : 1;
        Assert.AreEqual(expectedExit, exitCode, output);
        if (expectedDiagnostic is not null)
        {
            Assert.Contains(expectedDiagnostic, output);
        }

        var report = XDocument.Load(reportPath);
        XNamespace trx = "http://microsoft.com/schemas/VisualStudio/TeamTest/2010";
        XElement[] childResults = report.Descendants(trx + "UnitTestResult").ToArray();
        int expectedResults = expectedDiagnostic is not null ? 0 : 1;
        Assert.HasCount(expectedResults, childResults);
        if (expectedResults != 0)
        {
            Assert.AreEqual("DiscoversFixtures", (string?)childResults[0].Attribute("testName"));
            Assert.AreEqual(expectedDiagnostic is null ? "Passed" : "Failed", (string?)childResults[0].Attribute("outcome"));
        }

        XElement counters = report.Descendants(trx + "Counters").Single();
        Assert.AreEqual(expectedResults.ToString(CultureInfo.InvariantCulture), (string?)counters.Attribute("total"));
        Assert.AreEqual(expectedResults.ToString(CultureInfo.InvariantCulture), (string?)counters.Attribute("executed"));
        Assert.AreEqual("0", (string?)counters.Attribute("notExecuted"));
        if (expectedDiagnostic is not null)
        {
            Assert.AreEqual("Failed", (string?)report.Descendants(trx + "ResultSummary").Single().Attribute("outcome"));
        }

        string actualEvents = File.Exists(eventsPath) ? string.Join(" | ", File.ReadAllLines(eventsPath)) : string.Empty;
        Assert.AreEqual(expectedEvents, actualEvents);
        if (File.Exists(eventsPath))
        {
            TestContext.AddResultFile(eventsPath);
        }

        if (assertFallbackTrace)
        {
            string diagnostics = string.Join(Environment.NewLine, Directory.EnumerateFiles(diagnosticsDirectory, "*", SearchOption.AllDirectories)
                .Where(path => path.EndsWith(".log", StringComparison.OrdinalIgnoreCase))
                .Concat(Directory.EnumerateFiles(diagnosticsDirectory, "*.diag", SearchOption.AllDirectories))
                .Select(File.ReadAllText));
            Assert.Contains("No metadata path for referenced assembly ProviderLibrary", diagnostics);
            Assert.Contains("using runtime resolution for AssemblyFixtureProvider discovery", diagnostics);
        }
    }

    private void VerifyPackageProvenance(TestAsset asset, string tfm, string loadedPath, bool requireLoadedBinaries, bool useVSTest)
    {
        string projectDirectory = Path.Combine(asset.TargetAssetPath, "Tests");
        string evidenceDirectory = TestContext.TestTempDirectory!;
        string assetsPath = Path.Combine(projectDirectory, "obj", "project.assets.json");
        string assetsCopy = Path.Combine(evidenceDirectory, "project.assets.json");
        File.Copy(assetsPath, assetsCopy);
        TestContext.AddResultFile(assetsCopy);
        string configCopy = Path.Combine(evidenceDirectory, "NuGet.config");
        File.Copy(Path.Combine(asset.TargetAssetPath, "NuGet.config"), configCopy);
        TestContext.AddResultFile(configCopy);

        using var assets = JsonDocument.Parse(File.ReadAllText(assetsPath));
        Assert.AreEqual(useVSTest, assets.RootElement.GetProperty("libraries").TryGetProperty($"Microsoft.NET.Test.Sdk/{MicrosoftNETTestSdkVersion}", out _),
            "Native MTP consumers must exercise the adapter dependency closure without Microsoft.NET.Test.Sdk.");
        _ = assets.RootElement.GetProperty("libraries").GetProperty($"MSTest.TestFramework/{MSTestVersion}");
        _ = assets.RootElement.GetProperty("libraries").GetProperty($"Microsoft.Testing.Platform/{MicrosoftTestingPlatformVersion}");
        JsonElement library = assets.RootElement.GetProperty("libraries").GetProperty($"MSTest.TestAdapter/{MSTestVersion}");
        string relativePackagePath = library.GetProperty("path").GetString()!;
        string packageRoot = assets.RootElement.GetProperty("packageFolders").EnumerateObject()
            .Select(folder => Path.Combine(folder.Name, relativePackagePath))
            .Single(Directory.Exists);
        string package = Path.Combine(Constants.ArtifactsPackagesShipping, $"MSTest.TestAdapter.{MSTestVersion}.nupkg");
        string packageTfm = tfm.StartsWith("net4", StringComparison.Ordinal) ? "net462" : tfm;
        using ZipArchive archive = ZipFile.OpenRead(package);
        ZipArchiveEntry entry = archive.GetEntry($"buildTransitive/{packageTfm}/MSTestAdapter.PlatformServices.dll")!;
        using Stream packedAssembly = entry.Open();
        string expectedHash = Convert.ToHexString(SHA256.HashData(packedAssembly));
        string restoredAssembly = Path.Combine(packageRoot, "buildTransitive", packageTfm, "MSTestAdapter.PlatformServices.dll");
        Assert.AreEqual(expectedHash, Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(restoredAssembly))),
            "The generated consumer must restore the just-packed adapter, not a stale same-version cache.");
        TestContext.WriteLine($"MSTest.TestAdapter/{MSTestVersion}; package: {package}; SHA256: {Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(package)))}");
        TestContext.WriteLine($"Resolved adapter: {restoredAssembly}; SHA256: {expectedHash}");

        if (requireLoadedBinaries)
        {
            Assert.IsTrue(File.Exists(loadedPath), "The real child must report its loaded adapter path.");
            TestContext.AddResultFile(loadedPath);
            string services = File.ReadAllLines(loadedPath).Single(line => line.StartsWith("MSTestAdapter.PlatformServices=", StringComparison.Ordinal))["MSTestAdapter.PlatformServices=".Length..];
            Assert.AreEqual(expectedHash, Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(services))),
                "The executing host must load the just-packed PlatformServices binary.");
        }
    }

    private const string ProviderAssertions = """
        Assembly? provider = AppDomain.CurrentDomain.GetAssemblies().SingleOrDefault(assembly => assembly.GetName().Name == "ProviderLibrary");
        Assert.IsNotNull(provider, "The passive provider must be loaded before the test starts.");
        Assert.AreEqual($ExpectedInitialized$, (bool)provider.GetType("ProviderLibrary.SharedFixtures")!.GetField("Initialized")!.GetValue(null)!);
        Assert.IsFalse(AppDomain.CurrentDomain.GetAssemblies().Any(assembly => assembly.GetName().Name == "Intermediate"),
            "An unmarked intermediate must be traversed as metadata, not loaded.");
""";

    private const string SourceCode = """
#file Tests/FixtureConsumer.csproj
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <EnableMSTestRunner>true</EnableMSTestRunner>
    <TargetFramework>$TargetFramework$</TargetFramework>
    <LangVersion>preview</LangVersion>
    <Nullable>enable</Nullable>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="MSTest.TestAdapter" Version="$MSTestVersion$" />
    <PackageReference Include="MSTest.TestFramework" Version="$MSTestVersion$" />
    <PackageReference Include="Microsoft.Testing.Extensions.TrxReport" Version="$MicrosoftTestingPlatformVersion$" />
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="$MicrosoftNETTestSdkVersion$" Condition="'$UseVSTest$' == 'true'" />
    <ProjectReference Include="../$Reference$/$Reference$.csproj" />
  </ItemGroup>
</Project>

#file Tests/DiscoveryTests.cs
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using Microsoft.VisualStudio.TestTools.UnitTesting;

$ConsumerMarker$

[TestClass]
public sealed class DiscoveryTests
{
    [TestMethod]
    public void DiscoversFixtures()
    {
        File.WriteAllLines(Environment.GetEnvironmentVariable("FIXTURE_DISCOVERY_LOADED")!,
            AppDomain.CurrentDomain.GetAssemblies()
                .Where(assembly => assembly.GetName().Name is "MSTest.TestAdapter" or "MSTestAdapter.PlatformServices" or "MSTest.TestFramework")
                .Select(assembly => assembly.GetName().Name + "=" + assembly.Location));
$Assertions$
        Events.Record("test");
    }

    // Keep an AssemblyRef without letting the JIT resolve the dependency while compiling the test.
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static Type NeverCalledReference() => typeof($ReferenceType$);
}

[TestClass]
public sealed class LocalFixtures
{
$Initialize$
$Cleanup$
}

internal static class Events
{
    public static void Record(string value)
        => File.AppendAllText(Environment.GetEnvironmentVariable("FIXTURE_DISCOVERY_EVENTS")!, value + Environment.NewLine);
}

#file UnmarkedDependency/UnmarkedDependency.csproj
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>$TargetFramework$</TargetFramework>
    <LangVersion>preview</LangVersion>
  </PropertyGroup>
  <ItemGroup>
    <ProjectReference Include="../UnmarkedLeaf/UnmarkedLeaf.csproj" />
  </ItemGroup>
</Project>

#file UnmarkedDependency/Marker.cs
using System;
using System.Runtime.CompilerServices;
namespace UnmarkedDependency;
public sealed class Marker
{
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static Type NeverCalledReference() => typeof(UnmarkedLeaf.Marker);
}

#file UnmarkedLeaf/UnmarkedLeaf.csproj
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>$TargetFramework$</TargetFramework>
  </PropertyGroup>
</Project>

#file UnmarkedLeaf/Marker.cs
namespace UnmarkedLeaf
{
    public sealed class Marker { }
}

#file Intermediate/Intermediate.csproj
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>$TargetFramework$</TargetFramework>
    <LangVersion>preview</LangVersion>
  </PropertyGroup>
  <ItemGroup>
    <ProjectReference Include="../ProviderLibrary/ProviderLibrary.csproj" />
  </ItemGroup>
</Project>

#file Intermediate/Marker.cs
using System;
using System.Runtime.CompilerServices;
namespace Intermediate;
public sealed class Marker
{
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static Type NeverCalledReference() => typeof(ProviderLibrary.SharedFixtures);
}

#file ProviderLibrary/ProviderLibrary.csproj
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>$TargetFramework$</TargetFramework>
    <LangVersion>preview</LangVersion>
$Signing$
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="MSTest.TestFramework" Version="$MSTestVersion$" />
  </ItemGroup>
</Project>

#file ProviderLibrary/SharedFixtures.cs
using System;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;

$LibraryMarker$
$DuplicateMarker$

namespace ProviderLibrary;
public static class SharedFixtures
{
    public static bool Initialized;

    [AssemblyInitialize]
    public static void Initialize(TestContext context)
    {
        Initialized = true;
        File.AppendAllText(Environment.GetEnvironmentVariable("FIXTURE_DISCOVERY_EVENTS"), "provider-initialize" + Environment.NewLine);
    }

    [AssemblyCleanup]
    public static void Cleanup()
        => File.AppendAllText(Environment.GetEnvironmentVariable("FIXTURE_DISCOVERY_EVENTS"), "provider-cleanup" + Environment.NewLine);
}

public static class SecondFixtures
{
$DuplicateMethod$
}
""";
}
