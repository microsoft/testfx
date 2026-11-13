// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Acceptance.IntegrationTests;
using Microsoft.Testing.Platform.Acceptance.IntegrationTests.Helpers;
using Microsoft.Testing.Platform.Helpers;

namespace MSTest.Acceptance.IntegrationTests;

/// <summary>
/// Smoke coverage that a <c>UseWinUI</c> project resolves and loads the WinUI-flavored MSTest
/// assemblies and runs to completion. The asset deliberately does not reference
/// <c>Microsoft.WindowsAppSDK</c>, which keeps it buildable by the dotnet muxer on every leg; the real
/// WinUI end-to-end coverage lives in <see cref="UnpackagedWinUITests"/>.
/// </summary>
[TestClass]
public sealed class WinUITests : AcceptanceTestBase<WinUITests.TestAssetFixture>
{
    private static readonly string WinUITargetFramework = $"{TargetFrameworks.NetCurrent}-windows10.0.19041.0";

    [TestMethod]
    [OSCondition(OperatingSystems.Windows, IgnoreMessage = "WinUI is Windows-only")]
    public async Task SimpleWinUITestCase()
    {
        var testHost = TestHost.LocateFrom(AssetFixture.ProjectPath, TestAssetFixture.ProjectName, WinUITargetFramework);
        TestHostResult testHostResult = await testHost.ExecuteAsync(cancellationToken: TestContext.CancellationToken);

        // Assert
        testHostResult.AssertExitCodeIs(ExitCode.Success);
        testHostResult.AssertOutputContainsSummary(failed: 0, passed: 1, skipped: 0);
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    [DataRow("", DisplayName = "Deployment_Reflection")]
    [DataRow("Rooting", DisplayName = "Deployment_Rooting")]
    [DataRow("ReflectionFree", DisplayName = "Deployment_ReflectionFree")]
    public async Task DeploymentItemsFailBeforeBodiesIncludingInheritedAttributes(string sourceGenMode)
    {
        const string assetName = "WinUIDeploymentContract";
        string generator = sourceGenMode.Length == 0
            ? string.Empty
            : $"<PackageReference Include=\"MSTest.SourceGeneration\" Version=\"{MSTestSourceGenerationVersion}\" />";
        string source = DeploymentSource
            .Replace("$TargetFramework$", WinUITargetFramework, StringComparison.Ordinal)
            .Replace("$MSTestVersion$", MSTestVersion, StringComparison.Ordinal)
            .Replace("$PlatformVersion$", MicrosoftTestingPlatformVersion, StringComparison.Ordinal)
            .Replace("$Generator$", generator, StringComparison.Ordinal)
            .Replace("$SourceGenMode$", sourceGenMode, StringComparison.Ordinal)
            .Replace("$ReflectionType$", sourceGenMode.Length == 0 ? "ReflectionOperations" : "SourceGeneratedReflectionOperations", StringComparison.Ordinal);
        using TestAsset asset = await TestAsset.GenerateAssetAsync(assetName, source);
        DotnetMuxerResult build = await DotnetCli.RunAsync(
            "build -c Release -warnaserror -bl:deployment.binlog",
            workingDirectory: asset.TargetAssetPath,
            cancellationToken: TestContext.CancellationToken);
        build.AssertExitCodeIs(0);
        if (sourceGenMode.Length != 0)
        {
            Assert.IsNotEmpty(Directory.GetFiles(Path.Join(asset.TargetAssetPath, "obj"), "*MSTestReflectionMetadata*.g.cs", SearchOption.AllDirectories));
        }

        var host = TestHost.LocateFrom(asset.TargetAssetPath, assetName, WinUITargetFramework);
        TestHostResult result = await host.ExecuteAsync(
            "--report-trx --report-trx-filename deployment.trx",
            cancellationToken: TestContext.CancellationToken);
        result.AssertExitCodeIs(ExitCode.AtLeastOneTestFailed);
        result.AssertOutputContainsSummary(failed: 4, passed: 1, skipped: 0);
        string reportPath = Assert.ContainsSingle(Directory.GetFiles(asset.TargetAssetPath, "deployment.trx", SearchOption.AllDirectories));
        XNamespace ns = "http://microsoft.com/schemas/VisualStudio/TeamTest/2010";
        var report = XDocument.Load(reportPath);
        XElement[] results = [.. report.Descendants(ns + "UnitTestResult")];
        Assert.AreSequenceEqual(
            ["ClassDeployment:Failed", "InheritedClassDeployment:Failed", "InheritedMethodDeployment:Failed", "MetadataProviderIsSelected:Passed", "MethodDeployment:Failed"],
            results.Select(test => $"{test.Attribute("testName")!.Value}:{test.Attribute("outcome")!.Value}").Order(StringComparer.Ordinal));
        foreach (XElement failed in results.Where(test => (string?)test.Attribute("outcome") == "Failed"))
        {
            Assert.Contains(
                "DeploymentItemAttribute is not supported by UWP or WinUI test hosts.",
                failed.Descendants(ns + "Message").Single().Value);
            Assert.DoesNotContain("DEPLOYMENT_BODY_EXECUTED", failed.Value);
        }

        TestContext.WriteLine($"Packed WinUI deployment guard: {sourceGenMode}; TRX: {reportPath}");
    }

    public sealed class TestAssetFixture() : TestAssetFixtureBase()
    {
        public const string ProjectName = "WinUITests";

        public string ProjectPath => GetAssetPath(ProjectName);

        public override (string ID, string Name, string Code) GetAssetsToGenerate() => (ProjectName, ProjectName,
                SourceCode
                .PatchCodeWithReplace("$TargetFramework$", WinUITargetFramework)
                .PatchCodeWithReplace("$MSTestVersion$", MSTestVersion));

        private const string SourceCode = """
#file WinUITests.csproj
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <EnableMSTestRunner>true</EnableMSTestRunner>
    <TargetFramework>$TargetFramework$</TargetFramework>
    <UseWinUI>true</UseWinUI>
    <!--
      No package identity. Without a Microsoft.WindowsAppSDK reference this property does not change the
      build; it records the intent, and the process genuinely runs without identity, which is what makes
      this asset cover the unpackaged WIN_UI adapter paths (AppModel.IsPackagedProcess() returning false
      and GetCurrentPackagePath() returning null).
    -->
    <WindowsPackageType>None</WindowsPackageType>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="MSTest.WinUI" Version="$MSTestVersion$" />
  </ItemGroup>

</Project>

#file UnitTest1.cs
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

[TestClass]
public class TestClass1
{
    public TestContext TestContext { get; set; }

    [TestMethod]
    public void TestMethod1()
    {
        string[] directories = { "TestRunDirectory", "DeploymentDirectory", "ResultsDirectory", "TestRunResultsDirectory", "TestResultsDirectory", "TestTempDirectory" };
        foreach (string directory in directories)
        {
            TestContext.Properties[directory] = "not-a-ui-directory";
            Assert.AreEqual("not-a-ui-directory", TestContext.Properties[directory]);
            Assert.IsNull(typeof(TestContext).GetProperty(directory)!.GetValue(TestContext), directory);
        }
    }
}
""";
    }

    public TestContext TestContext { get; set; }

    private const string DeploymentSource = """
        #file WinUIDeploymentContract.csproj
        <Project Sdk="Microsoft.NET.Sdk">
          <PropertyGroup>
            <TargetFramework>$TargetFramework$</TargetFramework>
            <OutputType>Exe</OutputType>
            <EnableMSTestRunner>true</EnableMSTestRunner>
            <UseWinUI>true</UseWinUI>
            <WindowsPackageType>None</WindowsPackageType>
            <ImplicitUsings>enable</ImplicitUsings>
            <Nullable>enable</Nullable>
            <EmitCompilerGeneratedFiles>true</EmitCompilerGeneratedFiles>
            <MSTestSourceGenMode>$SourceGenMode$</MSTestSourceGenMode>
          </PropertyGroup>
          <ItemGroup>
            <PackageReference Include="MSTest.WinUI" Version="$MSTestVersion$" />
            <PackageReference Include="Microsoft.Testing.Platform" Version="$PlatformVersion$" />
            <PackageReference Include="Microsoft.Testing.Extensions.TrxReport" Version="$PlatformVersion$" />
            $Generator$
          </ItemGroup>
        </Project>

        #file DeploymentTests.cs
        using System.Reflection;
        using Microsoft.VisualStudio.TestTools.UnitTesting;

        [TestClass]
        public sealed class MethodDeploymentTests
        {
            [TestMethod]
            [DeploymentItem("not-deployed.txt")]
            public void MethodDeployment() => Assert.Fail("DEPLOYMENT_BODY_EXECUTED");

            [TestMethod]
            public void MetadataProviderIsSelected()
            {
                Assembly services = AppDomain.CurrentDomain.GetAssemblies().Single(assembly => assembly.GetName().Name == "MSTestAdapter.PlatformServices");
                Type providerType = services.GetType("Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.PlatformServiceProvider", true)!;
                object provider = providerType.GetProperty("Instance", BindingFlags.Static | BindingFlags.NonPublic)!.GetValue(null)!;
                object reflection = providerType.GetProperty("ReflectionOperations")!.GetValue(provider)!;
                Assert.AreEqual("$ReflectionType$", reflection.GetType().Name);
            }
        }

        [TestClass]
        [DeploymentItem("not-deployed.txt")]
        public sealed class ClassDeploymentTests
        {
            [TestMethod]
            public void ClassDeployment() => Assert.Fail("DEPLOYMENT_BODY_EXECUTED");
        }

        public abstract class MethodDeploymentBase
        {
            [TestMethod]
            [DeploymentItem("not-deployed.txt")]
            public void InheritedMethodDeployment() => Assert.Fail("DEPLOYMENT_BODY_EXECUTED");
        }

        [TestClass]
        public sealed class InheritedMethodDeploymentTests : MethodDeploymentBase;

        [DeploymentItem("not-deployed.txt")]
        public abstract class ClassDeploymentBase;

        [TestClass]
        public sealed class InheritedClassDeploymentTests : ClassDeploymentBase
        {
            [TestMethod]
            public void InheritedClassDeployment() => Assert.Fail("DEPLOYMENT_BODY_EXECUTED");
        }
        """;
}
