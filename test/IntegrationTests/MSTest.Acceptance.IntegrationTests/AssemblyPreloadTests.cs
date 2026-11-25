// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Globalization;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.RegularExpressions;

using Microsoft.Testing.Platform.Acceptance.IntegrationTests;
using Microsoft.Testing.Platform.Acceptance.IntegrationTests.Helpers;

namespace MSTest.Acceptance.IntegrationTests;

/// <summary>
/// Runs a packed MSTest consumer to prove that <c>PreloadReferencedAssemblies</c> loads the transitive, non-framework
/// references of the test assembly before the first test starts, and that nothing is loaded when the setting is not
/// configured. See docs/decisions/mstest-assembly-preload.md.
/// </summary>
[TestClass]
public sealed class AssemblyPreloadTests : AcceptanceTestBase<NopAssetFixture>
{
    private const string AssetName = "PreloadReferencedAssemblies";
    private const string ProjectName = "PreloadConsumer";
    private const string ReportName = "preload-results.trx";
    private const string ReportCopyName = "preload-results.trx.xml";

    public TestContext TestContext { get; set; }

    // The runner and its configuration channel vary together: MTP reads testconfig.json, VSTest reads runsettings.
    // .NET Framework additionally covers the app-domain host that runs the preload in the child domain.
    public static IEnumerable<object[]> Cases
    {
        get
        {
            foreach (bool preload in new[] { false, true })
            {
                yield return [TargetFrameworks.NetCurrent, "MTP", preload, true];
                yield return [TargetFrameworks.NetCurrent, "VSTest", preload, true];
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    yield return ["net472", "MTP", preload, true];
                    yield return ["net472", "VSTest", preload, true];
                    yield return ["net472", "VSTest", preload, false];
                }
            }
        }
    }

    [TestMethod]
    [DynamicData(nameof(Cases))]
    public async Task ReferencedAssembliesAreLoadedBeforeTheFirstTestOnlyWhenRequested(string tfm, string runner, bool preload, bool disableAppDomain)
    {
        using TestAsset asset = await TestAsset.GenerateAssetAsync(AssetName, SourceCode
            .PatchCodeWithReplace("$TargetFramework$", tfm)
            .PatchCodeWithReplace("$MSTestVersion$", MSTestVersion)
            .PatchCodeWithReplace("$MicrosoftTestingPlatformVersion$", MicrosoftTestingPlatformVersion)
            .PatchCodeWithReplace("$MicrosoftNETTestSdkVersion$", MicrosoftNETTestSdkVersion)
            .PatchCodeWithReplace("$UseVSTest$", (runner == "VSTest").ToString().ToLowerInvariant()));

        string project = Path.Combine(asset.TargetAssetPath, "Tests", ProjectName + ".csproj");
        DotnetMuxerResult build = await DotnetCli.RunAsync(
            $"build \"{project}\" -c Release",
            workingDirectory: asset.TargetAssetPath,
            cancellationToken: TestContext.CancellationToken);
        TestContext.WriteLine($"Packed consumer build: {build.Args}; exit: {build.ExitCode}");
        build.AssertExitCodeIs(0);

        string evidenceDirectory = TestContext.TestTempDirectory!;
        string statePath = Path.Combine(evidenceDirectory, "preload-state.txt");
        var environment = new Dictionary<string, string?>
        {
            ["PRELOAD_STATE"] = statePath,
            ["DOTNET_CLI_UI_LANGUAGE"] = "en-US",
            ["VSLANG"] = "1033",
        };
        int exitCode;
        string output;
        string invocation;
        if (runner == "MTP")
        {
            // Not configuring the setting at all proves the default is off.
            string options = string.Empty;
            if (preload)
            {
                string config = Path.Combine(evidenceDirectory, "preload.testconfig.json");
                File.WriteAllText(config, """
                    { "mstest": { "execution": { "preloadReferencedAssemblies": true } } }
                    """);
                options = $" --config-file \"{config}\"";
            }

            var host = TestHost.LocateFrom(Path.Combine(asset.TargetAssetPath, "Tests"), ProjectName, tfm);
            TestHostResult result = await host.ExecuteAsync(
                $"--report-trx --report-trx-filename {ReportName} --results-directory \"{evidenceDirectory}\"{options}",
                environmentVariables: environment,
                cancellationToken: TestContext.CancellationToken);
            exitCode = result.ExitCode;
            output = result.StandardOutput + Environment.NewLine + result.StandardError;
            invocation = result.Command;
        }
        else
        {
            string settings = Path.Combine(evidenceDirectory, "preload.runsettings");
            new XDocument(
                new XElement(
                    "RunSettings",
                    new XElement("RunConfiguration", new XElement("DisableAppDomain", disableAppDomain)),
                    new XElement(
                        "MSTest",
                        new XElement("DeploymentEnabled", false),
                        new XElement("CaptureTraceOutput", false),
                        // Discovery in the .NET Framework isolation domain can independently load these
                        // references. Isolate preloading in both halves of that on/off comparison.
                        !disableAppDomain ? new XElement("DiscoverReferencedAssemblyFixtureProviders", false) : null,
                        preload ? new XElement("PreloadReferencedAssemblies", true) : null)))
                .Save(settings);

            string dotnetRoot = Path.Combine(RootFinder.Find(), ".dotnet");
            environment["DOTNET_ROOT"] = dotnetRoot;
            string dotnet = Path.Combine(dotnetRoot, "dotnet" + Constants.ExecutableExtension);
            string assembly = Path.Combine(asset.TargetAssetPath, "Tests", "bin", "Release", tfm, ProjectName + (tfm.StartsWith("net4", StringComparison.Ordinal) ? ".exe" : ".dll"));
            using var command = new CommandLine();
            invocation = $"\"{dotnet}\" vstest \"{assembly}\" /Settings:\"{settings}\" /Logger:\"trx;LogFileName={ReportName}\" /ResultsDirectory:\"{evidenceDirectory}\" /Logger:\"console;verbosity=normal\"";
            exitCode = await command.RunAsyncAndReturnExitCodeAsync(
                invocation,
                environmentVariables: environment,
                workingDirectory: asset.TargetAssetPath,
                cancellationToken: TestContext.CancellationToken);
            output = command.StandardOutput + Environment.NewLine + command.ErrorOutput;
        }

        TestContext.WriteLine($"Packed {runner} command: {invocation}; exit: {exitCode}{Environment.NewLine}{output}");
        Assert.AreEqual(0, exitCode, output);

        // Keep the child report's bytes without presenting it as another outer test module.
        string reportPath = Path.Combine(evidenceDirectory, ReportCopyName);
        File.Move(Path.Combine(evidenceDirectory, ReportName), reportPath);
        TestContext.AddResultFile(reportPath);
        var report = XDocument.Load(reportPath);
        XNamespace trx = "http://microsoft.com/schemas/VisualStudio/TeamTest/2010";
        XElement testResult = report.Descendants(trx + "UnitTestResult").Single();
        Assert.AreEqual("ObservesPreloadState", (string?)testResult.Attribute("testName"));
        Assert.AreEqual("Passed", (string?)testResult.Attribute("outcome"));

        Assert.IsTrue(File.Exists(statePath), "The child must report the state it observed when its first test started.");
        TestContext.AddResultFile(statePath);

        Dictionary<string, string> state = File.ReadAllLines(statePath).ToDictionary(
            line => line[..line.IndexOf('=')],
            line => line[(line.IndexOf('=') + 1)..]);
        VerifyLoadedAdapterIsThePackedOne(tfm, state["MSTestAdapter.PlatformServices"]);
        string expected = preload ? "True" : "False";
        Assert.AreEqual(expected, state["PreloadedDependency.Loaded"], "A direct reference of the test assembly.");
        Assert.AreEqual(expected, state["PreloadedLeaf.Loaded"], "A reference of a reference, reached only through the graph walk.");

        // VSTest relays the adapter's informational message to the console logger, so it is how a VSTest consumer
        // sees that the preloader ran, and how many assemblies it loaded.
        if (runner == "VSTest")
        {
            Match summary = Regex.Match(output, @"Preloaded (\d+) referenced assemblies before running tests\. (\d+) could not be loaded\.");
            Assert.AreEqual(preload, summary.Success, output);
            if (preload)
            {
                Assert.IsGreaterThanOrEqualTo(2, int.Parse(summary.Groups[1].Value, CultureInfo.InvariantCulture), "Both PreloadedDependency and PreloadedLeaf.");
                Assert.AreEqual("0", summary.Groups[2].Value, output);
            }
        }
    }

    private void VerifyLoadedAdapterIsThePackedOne(string tfm, string loaded)
    {
        string package = Path.Combine(Constants.ArtifactsPackagesShipping, $"MSTest.TestAdapter.{MSTestVersion}.nupkg");
        string packageTfm = tfm.StartsWith("net4", StringComparison.Ordinal) ? "net462" : tfm;
        using ZipArchive archive = ZipFile.OpenRead(package);
        ZipArchiveEntry entry = archive.GetEntry($"lib/{packageTfm}/MSTestAdapter.PlatformServices.dll")!;
        using Stream packedAssembly = entry.Open();
        string expectedHash = Convert.ToHexString(SHA256.HashData(packedAssembly));
        Assert.AreEqual(expectedHash, Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(loaded))),
            "The executing host must load the just-packed PlatformServices binary, not a stale same-version cache.");
        TestContext.WriteLine($"Loaded adapter: {loaded}; packed: {package}; SHA256: {expectedHash}");
    }

    private const string SourceCode = """
#file Tests/PreloadConsumer.csproj
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
    <ProjectReference Include="../PreloadedDependency/PreloadedDependency.csproj" />
  </ItemGroup>
</Project>

#file Tests/ObservationTests.cs
using System;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using Microsoft.VisualStudio.TestTools.UnitTesting;

[TestClass]
public sealed class ObservationTests
{
    [TestMethod]
    public void ObservesPreloadState()
    {
        // Nothing in this test calls into the referenced libraries, so they can only be loaded by the adapter.
        bool Loaded(string name) => AppDomain.CurrentDomain.GetAssemblies().Any(assembly => assembly.GetName().Name == name);
        File.WriteAllLines(
            Environment.GetEnvironmentVariable("PRELOAD_STATE")!,
            [
                "PreloadedDependency.Loaded=" + Loaded("PreloadedDependency"),
                "PreloadedLeaf.Loaded=" + Loaded("PreloadedLeaf"),
                "MSTestAdapter.PlatformServices=" + AppDomain.CurrentDomain.GetAssemblies().Single(assembly => assembly.GetName().Name == "MSTestAdapter.PlatformServices").Location,
            ]);
    }

    // Keep an AssemblyRef without letting the JIT resolve the dependency while compiling the test.
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static Type NeverCalledReference() => typeof(PreloadedDependency.Marker);
}

#file PreloadedDependency/PreloadedDependency.csproj
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>$TargetFramework$</TargetFramework>
    <LangVersion>preview</LangVersion>
  </PropertyGroup>
  <ItemGroup>
    <ProjectReference Include="../PreloadedLeaf/PreloadedLeaf.csproj" />
  </ItemGroup>
</Project>

#file PreloadedDependency/Marker.cs
using System;
using System.Runtime.CompilerServices;
namespace PreloadedDependency;
public sealed class Marker
{
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static Type NeverCalledReference() => typeof(PreloadedLeaf.Marker);
}

#file PreloadedLeaf/PreloadedLeaf.csproj
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>$TargetFramework$</TargetFramework>
  </PropertyGroup>
</Project>

#file PreloadedLeaf/Marker.cs
namespace PreloadedLeaf
{
    public sealed class Marker { }
}
""";
}
