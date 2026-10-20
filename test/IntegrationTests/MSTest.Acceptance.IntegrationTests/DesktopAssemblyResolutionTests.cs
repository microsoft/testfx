// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Acceptance.IntegrationTests;
using Microsoft.Testing.Platform.Acceptance.IntegrationTests.Helpers;

namespace MSTest.Acceptance.IntegrationTests;

[TestClass]
[OSCondition(OperatingSystems.Windows)]
public sealed class DesktopAssemblyResolutionTests : AcceptanceTestBase<NopAssetFixture>
{
    private const string AssetName = "DesktopAssemblyResolution";
    private const string DependencyName = "ResolutionDependency";
    private const string ChildTestName = "LoadsDependencyFromConfiguredSearchDirectory";
    // Nested VSTest reports are attachments, not outer MTP modules for the CLI's .trx artifact merger.
    private const string ChildReportFileName = "child-results.trx.xml";

    public TestContext TestContext { get; set; }

    [TestMethod]
    public Task ParentDomainShouldHonorSearchDirectoriesSpecifiedInRunsettings()
        => RunTestsAsync(disableAppDomain: true);

    [TestMethod]
    public Task ChildDomainResolutionPathsShouldHaveSearchDirectoriesSpecifiedInRunsettings()
        => RunTestsAsync(disableAppDomain: false);

    private async Task RunTestsAsync(bool disableAppDomain)
    {
        // Each method owns its projects, settings and reports; method-level acceptance scheduling stays enabled.
        using TestAsset asset = await TestAsset.GenerateAssetAsync(
            AssetName,
            SourceCode
                .PatchCodeWithReplace("$TargetFramework$", TargetFrameworks.NetFramework[0])
                .PatchCodeWithReplace("$MSTestVersion$", MSTestVersion)
                .PatchCodeWithReplace("$MicrosoftTestingPlatformVersion$", MicrosoftTestingPlatformVersion)
                .PatchCodeWithReplace("$MicrosoftNETTestSdkVersion$", MicrosoftNETTestSdkVersion));
        string projectDirectory = Path.Combine(asset.TargetAssetPath, "Tests");
        string projectPath = Path.Combine(projectDirectory, $"{AssetName}.csproj");
        DotnetMuxerResult build = await DotnetCli.RunAsync(
            $"build \"{projectPath}\" -c Release",
            workingDirectory: asset.TargetAssetPath,
            cancellationToken: TestContext.CancellationToken);
        TestContext.WriteLine($"Generated build: {build.Args}; exit: {build.ExitCode}");
        build.AssertExitCodeIs(0);

        string dependencyDirectory = Path.Combine(asset.TargetAssetPath, "Dependency", "bin", "Release", TargetFrameworks.NetFramework[0]);
        string dependencyPath = Path.Combine(dependencyDirectory, $"{DependencyName}.dll");
        string testOutputDirectory = Path.Combine(projectDirectory, "bin", "Release", TargetFrameworks.NetFramework[0]);
        Assert.IsTrue(File.Exists(dependencyPath));
        Assert.IsFalse(
            File.Exists(Path.Combine(testOutputDirectory, $"{DependencyName}.dll")),
            "The dependency must not be available through the test output's normal probing path.");

        string settingsPath = Path.Combine(asset.TargetAssetPath, "resolution.runsettings");
        new XDocument(
            new XElement(
                "RunSettings",
                new XElement("RunConfiguration", new XElement("DisableAppDomain", disableAppDomain)),
                new XElement(
                    "MSTestV2",
                    new XElement("DeploymentEnabled", false),
                    new XElement(
                        "AssemblyResolution",
                        new XElement("Directory", new XAttribute("path", Path.Combine(asset.TargetAssetPath, "missing"))),
                        new XElement("Directory", new XAttribute("path", dependencyDirectory)))),
                new XElement(
                    "TestRunParameters",
                    new XElement("Parameter", new XAttribute("name", "ExpectChildDomain"), new XAttribute("value", !disableAppDomain)),
                    new XElement("Parameter", new XAttribute("name", "ExpectedDependencyPath"), new XAttribute("value", dependencyPath)))))
            .Save(settingsPath);

        string evidenceDirectory = TestContext.TestTempDirectory!;
        foreach (string file in new[] { "project.assets.json", $"{AssetName}.csproj.nuget.g.props", $"{AssetName}.csproj.nuget.g.targets" })
        {
            string copy = Path.Combine(evidenceDirectory, file);
            File.Copy(Path.Combine(projectDirectory, "obj", file), copy);
            TestContext.AddResultFile(copy);
        }

        string nugetConfigCopy = Path.Combine(evidenceDirectory, "NuGet.config");
        File.Copy(Path.Combine(asset.TargetAssetPath, "NuGet.config"), nugetConfigCopy);
        TestContext.AddResultFile(nugetConfigCopy);

        DotnetMuxerResult run = await DotnetCli.RunAsync(
            $"test \"{projectPath}\" -c Release --no-build --settings \"{settingsPath}\" --logger \"trx;LogFileName={ChildReportFileName}\" --results-directory \"{evidenceDirectory}\"",
            workingDirectory: asset.TargetAssetPath,
            failIfReturnValueIsNotZero: false,
            cancellationToken: TestContext.CancellationToken);
        TestContext.WriteLine($"Generated test: {run.Args}; exit: {run.ExitCode}");
        run.AssertExitCodeIs(0);

        string reportPath = Path.Combine(evidenceDirectory, ChildReportFileName);
        TestContext.AddResultFile(reportPath);
        var report = XDocument.Load(reportPath);
        XNamespace trx = "http://microsoft.com/schemas/VisualStudio/TeamTest/2010";
        XElement counters = report.Descendants(trx + "Counters").Single();
        Assert.AreEqual("1", (string?)counters.Attribute("total"));
        Assert.AreEqual("1", (string?)counters.Attribute("executed"));
        Assert.AreEqual("1", (string?)counters.Attribute("passed"));
        Assert.AreEqual("0", (string?)counters.Attribute("failed"));
        Assert.AreEqual("0", (string?)counters.Attribute("notExecuted"));
        XElement result = report.Descendants(trx + "UnitTestResult").Single();
        Assert.AreEqual(ChildTestName, (string?)result.Attribute("testName"));
        Assert.AreEqual("Passed", (string?)result.Attribute("outcome"));
    }

    // These are deliberately reflection-mode VSTest consumers: source-generated metadata does not exercise
    // the .NET Framework TestSourceHost AppDomain/resolver boundary.
    private const string SourceCode = """
#file global.json
{
  "test": {
    "runner": "VSTest"
  }
}

#file Dependency/ResolutionDependency.csproj
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>$TargetFramework$</TargetFramework>
  </PropertyGroup>
</Project>

#file Dependency/Probe.cs
namespace ResolutionDependency
{
    public static class Probe
    {
        public static string ReadValue() => "resolved from the configured search directory";
    }
}

#file Tests/DesktopAssemblyResolution.csproj
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>$TargetFramework$</TargetFramework>
    <IsTestProject>true</IsTestProject>
    <LangVersion>preview</LangVersion>
    <Nullable>enable</Nullable>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="$MicrosoftNETTestSdkVersion$" />
    <PackageReference Include="MSTest.TestAdapter" Version="$MSTestVersion$" />
    <PackageReference Include="MSTest.TestFramework" Version="$MSTestVersion$" />
    <PackageReference Include="Microsoft.Testing.Platform" Version="$MicrosoftTestingPlatformVersion$" />
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="../Dependency/ResolutionDependency.csproj" ReferenceOutputAssembly="false" />
  </ItemGroup>
</Project>

#file Tests/ResolutionTests.cs
using System;
using System.IO;
using System.Reflection;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DesktopAssemblyResolution;

[TestClass]
public sealed class ResolutionTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public void LoadsDependencyFromConfiguredSearchDirectory()
    {
        bool expectChildDomain = bool.Parse((string)TestContext.Properties["ExpectChildDomain"]!);
        bool isChildDomain = AppDomain.CurrentDomain.FriendlyName.StartsWith("TestSourceHost: Enumerating source (", StringComparison.Ordinal);
        Assert.AreEqual(expectChildDomain, isChildDomain, AppDomain.CurrentDomain.FriendlyName);
        Assert.IsFalse(File.Exists(Path.Combine(Path.GetDirectoryName(typeof(ResolutionTests).Assembly.Location)!, "ResolutionDependency.dll")));

        Assembly dependency = Assembly.Load("ResolutionDependency");
        Assert.AreEqual(
            (string)TestContext.Properties["ExpectedDependencyPath"]!,
            dependency.Location);
        object? value = dependency.GetType("ResolutionDependency.Probe", throwOnError: true)!
            .GetMethod("ReadValue")!.Invoke(null, null);
        Assert.AreEqual("resolved from the configured search directory", value);

        TestContext.WriteLine("DOMAIN=" + AppDomain.CurrentDomain.FriendlyName);
        TestContext.WriteLine("DEPENDENCY=" + dependency.Location);
        TestContext.WriteLine("FRAMEWORK=" + typeof(Assert).Assembly.Location);
        TestContext.WriteLine("ADAPTER=" + Assembly.Load("MSTest.TestAdapter").Location);
        TestContext.WriteLine("PLATFORM_SERVICES=" + Assembly.Load("MSTestAdapter.PlatformServices").Location);
        TestContext.WriteLine("MTP=" + Assembly.Load("Microsoft.Testing.Platform").Location);
    }
}
""";
}
