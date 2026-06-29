// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.IO.Compression;

using Microsoft.Testing.Platform.Acceptance.IntegrationTests;
using Microsoft.Testing.Platform.Acceptance.IntegrationTests.Helpers;

namespace MSTest.Acceptance.IntegrationTests;

/// <summary>
/// Guards the (legacy, VSTest-bound) scenario where MSTest test classes are authored in a
/// <b>C++/CLI managed assembly</b> rather than C#. This is the realistic migration target for internal
/// teams moving off the deprecated CppUnitTestFramework for projects that can compile with <c>/clr</c>.
/// The test builds a tiny C++/CLI test assembly against the freshly-built MSTest packages and runs it
/// through <c>vstest.console.exe</c>, asserting the MSTest adapter discovers and executes the tests.
/// </summary>
[TestClass]
[OSCondition(OperatingSystems.Windows, IgnoreMessage = "C++/CLI requires the MSVC '/clr' toolset and is Windows-only.")]
public sealed class CppCliVSTestTests : AcceptanceTestBase<NopAssetFixture>
{
    private const string AssetName = "CppCliMSTest";

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task MSTestTestsInCppCliAssembly_AreDiscoveredAndRun_ByVSTest()
    {
        CancellationToken cancellationToken = TestContext.CancellationToken;

        // Classic C++/CLI (.NET Framework '/clr') only needs the MSVC toolset; the dedicated C++/CLI-support
        // component is for '/clr:netcore'. The toolset is absent on SDK-only build legs, so when it is missing
        // we make the test inconclusive rather than fail.
        string? vsInstallPath = await CppCliTestSupport.TryFindVsInstallWithCppToolsetAsync(cancellationToken);
        if (vsInstallPath is null)
        {
            Assert.Inconclusive("Skipping: no Visual Studio install with the MSVC C++ toolset (Microsoft.VisualStudio.Component.VC.Tools.x86.x64) was found.");
            return;
        }

        string msbuildExe = await FindMsbuildWithVsWhereAsync(cancellationToken);

        // C++/CLI projects cannot consume managed assemblies from a NuGet PackageReference (NuGet only wires
        // build/native assets into a vcxproj, not lib/*.dll references), so we extract the freshly-built
        // MSTest packages and reference the assemblies via <HintPath>. This still validates the just-built
        // framework + adapter against a managed C++/CLI test assembly.
        using var packagesDir = new TempDirectory();
        string frameworkNet462Dir = Path.Combine(ExtractShippingPackage(packagesDir, "MSTest.TestFramework", MSTestVersion), "lib", "net462");
        string adapterNet462Dir = Path.Combine(ExtractShippingPackage(packagesDir, "MSTest.TestAdapter", MSTestVersion), "buildTransitive", "net462");
        Assert.IsTrue(
            File.Exists(Path.Combine(frameworkNet462Dir, "MSTest.TestFramework.dll")),
            $"Expected MSTest.TestFramework.dll under '{frameworkNet462Dir}'.");

        using TestAsset testAsset = await TestAsset.GenerateAssetAsync(
            AssetName,
            SourceCode.PatchCodeWithReplace("$MSTestFrameworkDir$", frameworkNet462Dir),
            addDefaultNuGetConfigFile: false);

        // The acceptance host itself runs under a code-coverage profiler; those COR_*/CORECLR_* environment
        // variables would be inherited by the nested .NET Framework test host and break the VSTest run, so we
        // build a clean environment (everything except the code-coverage variables) for the child processes.
        Dictionary<string, string?> cleanEnvironment = CppCliTestSupport.BuildEnvironmentWithoutCodeCoverage();

        // Build the C++/CLI test assembly with full Visual Studio MSBuild; the dotnet SDK muxer cannot build a vcxproj.
        string vcxproj = Path.Combine(testAsset.TargetAssetPath, $"{AssetName}.vcxproj");
        string binlogFile = Path.Combine(TempDirectory.TestSuiteDirectory, $"{nameof(MSTestTestsInCppCliAssembly_AreDiscoveredAndRun_ByVSTest)}.binlog");
        using var buildCommandLine = new CommandLine();
        int buildExitCode = await buildCommandLine.RunAsyncAndReturnExitCodeAsync(
            $"\"{msbuildExe}\" \"{vcxproj}\" /p:Configuration=Debug /p:Platform=x64 /restore:false /bl:\"{binlogFile}\"",
            cleanEnvironment,
            cleanDefaultEnvironmentVariableIfCustomAreProvided: true,
            cancellationToken: cancellationToken);
        Assert.AreEqual(
            0,
            buildExitCode,
            $"C++/CLI build failed.{Environment.NewLine}{buildCommandLine.StandardOutput}{Environment.NewLine}{buildCommandLine.ErrorOutput}");

        string testDll = Path.Combine(testAsset.TargetAssetPath, "x64", "Debug", $"{AssetName}.dll");
        Assert.IsTrue(File.Exists(testDll), $"Built C++/CLI test assembly was not found at '{testDll}'.");

        // Run the managed C++/CLI assembly through VSTest using the freshly-built MSTest adapter. We use the
        // vstest.console.exe bundled with the located VS install (rather than the Microsoft.TestPlatform NuGet
        // package) so the test is self-contained and does not depend on that package being in the NuGet cache.
        string vstestConsolePath = Path.Combine(vsInstallPath, "Common7", "IDE", "Extensions", "TestPlatform", "vstest.console.exe");
        Assert.IsTrue(File.Exists(vstestConsolePath), $"vstest.console.exe was not found at '{vstestConsolePath}'.");
        using var runCommandLine = new CommandLine();
        int runExitCode = await runCommandLine.RunAsyncAndReturnExitCodeAsync(
            $"\"{vstestConsolePath}\" \"{testDll}\" /TestAdapterPath:\"{adapterNet462Dir}\" /Platform:x64",
            cleanEnvironment,
            // vstest.console.exe lives under Program Files; without a writable working directory the MSTest
            // adapter fails creating its 'TestResults\Deploy_*' folder there. Run from the asset folder.
            workingDirectory: testAsset.TargetAssetPath,
            cleanDefaultEnvironmentVariableIfCustomAreProvided: true,
            cancellationToken: cancellationToken);

        string output = runCommandLine.StandardOutput;
        Assert.AreEqual(
            0,
            runExitCode,
            $"VSTest run did not succeed.{Environment.NewLine}{output}{Environment.NewLine}{runCommandLine.ErrorOutput}");

        // Discovery + execution: each C++/CLI [TestMethod] surfaces (and passes) in the VSTest output.
        Assert.Contains("Add_TwoPlusTwo_IsFour", output);
        Assert.Contains("Strings_AreEqual", output);
        Assert.Contains("Booleans_Work", output);
    }

    private static string ExtractShippingPackage(TempDirectory destination, string packageId, string version)
    {
        string nupkg = Path.Combine(Constants.ArtifactsPackagesShipping, $"{packageId}.{version}.nupkg");
        Assert.IsTrue(File.Exists(nupkg), $"Expected packed package '{nupkg}' was not found. Did you build with '-pack'?");

        string target = Path.Combine(destination.Path, packageId);
        ZipFile.ExtractToDirectory(nupkg, target);
        return target;
    }

    private const string SourceCode = """
#file CppCliMSTest.vcxproj
<?xml version="1.0" encoding="utf-8"?>
<Project DefaultTargets="Build" xmlns="http://schemas.microsoft.com/developer/msbuild/2003">
  <ItemGroup Label="ProjectConfigurations">
    <ProjectConfiguration Include="Debug|x64">
      <Configuration>Debug</Configuration>
      <Platform>x64</Platform>
    </ProjectConfiguration>
  </ItemGroup>
  <PropertyGroup Label="Globals">
    <ProjectGuid>{B8C9F1A2-3D4E-4F5A-9B6C-7D8E9F0A1B2C}</ProjectGuid>
    <RootNamespace>CppCliMSTest</RootNamespace>
    <Keyword>ManagedCProj</Keyword>
    <WindowsTargetPlatformVersion>10.0</WindowsTargetPlatformVersion>
  </PropertyGroup>
  <Import Project="$(VCTargetsPath)\Microsoft.Cpp.Default.props" />
  <PropertyGroup Label="Configuration">
    <ConfigurationType>DynamicLibrary</ConfigurationType>
    <UseDebugLibraries>true</UseDebugLibraries>
    <PlatformToolset>v143</PlatformToolset>
    <CLRSupport>true</CLRSupport>
    <TargetFrameworkVersion>v4.8</TargetFrameworkVersion>
    <CharacterSet>Unicode</CharacterSet>
  </PropertyGroup>
  <Import Project="$(VCTargetsPath)\Microsoft.Cpp.props" />
  <ItemDefinitionGroup>
    <ClCompile>
      <WarningLevel>Level3</WarningLevel>
      <Optimization>Disabled</Optimization>
      <RuntimeLibrary>MultiThreadedDebugDLL</RuntimeLibrary>
    </ClCompile>
  </ItemDefinitionGroup>
  <ItemGroup>
    <ClCompile Include="Tests.cpp" />
  </ItemGroup>
  <ItemGroup>
    <Reference Include="MSTest.TestFramework">
      <HintPath>$MSTestFrameworkDir$\MSTest.TestFramework.dll</HintPath>
    </Reference>
    <Reference Include="MSTest.TestFramework.Extensions">
      <HintPath>$MSTestFrameworkDir$\MSTest.TestFramework.Extensions.dll</HintPath>
    </Reference>
  </ItemGroup>
  <Import Project="$(VCTargetsPath)\Microsoft.Cpp.targets" />
</Project>

#file Tests.cpp
using namespace System;
using namespace Microsoft::VisualStudio::TestTools::UnitTesting;

[TestClass]
public ref class CalculatorTests
{
public:
    [ClassInitialize]
    static void ClassInit(TestContext^ testContext)
    {
        Console::WriteLine("CalculatorTests.ClassInit called.");
    }

    [TestInitialize]
    void MethodInit()
    {
        Console::WriteLine("CalculatorTests.MethodInit called.");
    }

    [TestMethod]
    void Add_TwoPlusTwo_IsFour()
    {
        Assert::AreEqual(4, 2 + 2);
    }

    [TestMethod]
    void Strings_AreEqual()
    {
        Assert::AreEqual(gcnew String("Hello"), gcnew String("Hello"));
    }

    [TestMethod]
    void Booleans_Work()
    {
        Assert::IsTrue(1 == 1);
        Assert::IsFalse(1 == 2);
    }
};
""";
}
