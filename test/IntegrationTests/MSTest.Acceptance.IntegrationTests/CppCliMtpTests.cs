// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Acceptance.IntegrationTests;
using Microsoft.Testing.Platform.Acceptance.IntegrationTests.Helpers;
using Microsoft.Testing.Platform.Helpers;

namespace MSTest.Acceptance.IntegrationTests;

/// <summary>
/// Guards that MSTest test classes authored in a <b>C++/CLI managed assembly</b> can be hosted on
/// <b>Microsoft.Testing.Platform (MTP)</b> as a self-contained test executable (no <c>vstest.console.exe</c>).
/// A C++/CLI <c>Application</c> with a managed <c>main</c> calls the same MTP hosting API the generated C#
/// entry point uses (<c>TestApplication.CreateBuilderAsync</c> / <c>AddMSTest</c> / <c>BuildAsync</c> /
/// <c>RunAsync</c>), and the test asserts the produced exe discovers and runs the tests.
/// </summary>
/// <remarks>
/// C++/CLI <c>.vcxproj</c> projects do not resolve NuGet assets, so the MTP + MSTest .NET Framework runtime
/// closure is harvested by building a tiny C# <c>net472</c> <c>EnableMSTestRunner</c> project (which lets NuGet
/// resolve the full dependency closure into its output), then deployed next to the C++/CLI exe.
/// </remarks>
[TestClass]
[OSCondition(OperatingSystems.Windows, IgnoreMessage = "C++/CLI requires the MSVC '/clr' toolset and is Windows-only.")]
public sealed class CppCliMtpTests : AcceptanceTestBase<NopAssetFixture>
{
    private const string AssetName = "CppCliMtp";
    private const string HarvestClosureRelativePath = @"harvest\bin\Release\net472";

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task MSTestTestsInCppCliAssembly_AreHostedAndRun_ByMtp()
    {
        CancellationToken cancellationToken = TestContext.CancellationToken;

        // Classic C++/CLI (.NET Framework '/clr') only needs the MSVC toolset; absent on SDK-only build legs.
        string? vsInstallPath = await CppCliTestSupport.TryFindVsInstallWithCppToolsetAsync(cancellationToken);
        if (vsInstallPath is null)
        {
            Assert.Inconclusive("Skipping: no Visual Studio install with the MSVC C++ toolset (Microsoft.VisualStudio.Component.VC.Tools.x86.x64) was found.");
            return;
        }

        // Derive MSBuild from the same VS install we validated above, so the C++ targets/toolset are
        // guaranteed available (locating MSBuild independently could pick a different install on multi-VS machines).
        string? msbuildExe = CppCliTestSupport.TryGetMSBuildPathFromVsInstall(vsInstallPath);
        if (msbuildExe is null)
        {
            Assert.Inconclusive($"Skipping: MSBuild.exe was not found under the located Visual Studio install '{vsInstallPath}'.");
            return;
        }

        using TestAsset testAsset = await TestAsset.GenerateAssetAsync(
            AssetName,
            SourceCode.PatchCodeWithReplace("$MSTestVersion$", MSTestVersion));

        Dictionary<string, string?> cleanEnvironment = CppCliTestSupport.BuildEnvironmentWithoutCodeCoverage();

        // 1) Harvest the MTP + MSTest net472 runtime closure by building the C# project with the dotnet SDK,
        //    letting NuGet resolve the full dependency closure into harvest\bin\Release\net472.
        DotnetMuxerResult harvestResult = await DotnetCli.RunAsync(
            $"build \"{Path.Combine(testAsset.TargetAssetPath, "harvest", "Harvest.csproj")}\" -c Release",
            workingDirectory: testAsset.TargetAssetPath,
            failIfReturnValueIsNotZero: false,
            cancellationToken: cancellationToken);
        Assert.AreEqual(0, harvestResult.ExitCode, $"Harvesting the MTP closure failed.{Environment.NewLine}{harvestResult}");

        string closureDir = Path.Combine(testAsset.TargetAssetPath, HarvestClosureRelativePath);
        Assert.IsTrue(
            File.Exists(Path.Combine(closureDir, "Microsoft.Testing.Platform.dll")),
            $"Expected the harvested MTP closure under '{closureDir}'.");

        // 2) Build the C++/CLI MTP host exe with full Visual Studio MSBuild (the dotnet muxer cannot build a vcxproj).
        //    The vcxproj references the harvested closure assemblies via <HintPath>.
        string vcxproj = Path.Combine(testAsset.TargetAssetPath, $"{AssetName}.vcxproj");
        string binlogFile = Path.Combine(TempDirectory.TestSuiteDirectory, $"{nameof(MSTestTestsInCppCliAssembly_AreHostedAndRun_ByMtp)}.binlog");
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

        string exeDir = Path.Combine(testAsset.TargetAssetPath, "x64", "Debug");
        string testExe = Path.Combine(exeDir, $"{AssetName}.exe");
        Assert.IsTrue(File.Exists(testExe), $"Built C++/CLI MTP host was not found at '{testExe}'.");

        // 3) Deploy the runtime closure (and the binding-redirect app.config) next to the C++/CLI exe.
        foreach (string dll in Directory.GetFiles(closureDir, "*.dll"))
        {
            File.Copy(dll, Path.Combine(exeDir, Path.GetFileName(dll)), overwrite: true);
        }

        string harvestConfig = Path.Combine(closureDir, "Harvest.exe.config");
        if (File.Exists(harvestConfig))
        {
            File.Copy(harvestConfig, Path.Combine(exeDir, $"{AssetName}.exe.config"), overwrite: true);
        }

        // 4) Run the C++/CLI exe as an MTP test host and assert discovery + execution.
        using var runCommandLine = new CommandLine();
        int runExitCode = await runCommandLine.RunAsyncAndReturnExitCodeAsync(
            $"\"{testExe}\" --output Detailed",
            cleanEnvironment,
            workingDirectory: exeDir,
            cleanDefaultEnvironmentVariableIfCustomAreProvided: true,
            cancellationToken: cancellationToken);

        string output = runCommandLine.StandardOutput;
        Assert.AreEqual(
            0,
            runExitCode,
            $"MTP run did not succeed.{Environment.NewLine}{output}{Environment.NewLine}{runCommandLine.ErrorOutput}");

        Assert.Contains("Add_TwoPlusTwo_IsFour", output);
        Assert.Contains("Strings_AreEqual", output);
        Assert.Contains("Booleans_Work", output);
        // The MTP summary line is emitted by the platform host, proving the exe really hosted MTP.
        Assert.Contains("Passed!", output);
    }

    private const string SourceCode = """
#file harvest/Harvest.csproj
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net472</TargetFramework>
    <EnableMSTestRunner>true</EnableMSTestRunner>
    <GenerateTestingPlatformEntryPoint>true</GenerateTestingPlatformEntryPoint>
    <PlatformTarget>x64</PlatformTarget>
    <LangVersion>latest</LangVersion>
    <!-- This project only exists to let NuGet resolve the MTP + MSTest runtime closure for the C++/CLI host. -->
    <NoWarn>$(NoWarn);MSTEST0032</NoWarn>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="MSTest.TestAdapter" Version="$MSTestVersion$" />
    <PackageReference Include="MSTest.TestFramework" Version="$MSTestVersion$" />
  </ItemGroup>

</Project>

#file harvest/HarvestPlaceholder.cs
using Microsoft.VisualStudio.TestTools.UnitTesting;

[TestClass]
public class HarvestPlaceholder
{
    // Present only so the harvester is a valid test project; the C++/CLI exe carries the real tests.
    [TestMethod]
    public void Placeholder()
    {
    }
}

#file CppCliMtp.vcxproj
<?xml version="1.0" encoding="utf-8"?>
<Project DefaultTargets="Build" xmlns="http://schemas.microsoft.com/developer/msbuild/2003">
  <ItemGroup Label="ProjectConfigurations">
    <ProjectConfiguration Include="Debug|x64">
      <Configuration>Debug</Configuration>
      <Platform>x64</Platform>
    </ProjectConfiguration>
  </ItemGroup>
  <PropertyGroup Label="Globals">
    <ProjectGuid>{C1D2E3F4-5A6B-4C7D-8E9F-0A1B2C3D4E5F}</ProjectGuid>
    <RootNamespace>CppCliMtp</RootNamespace>
    <Keyword>ManagedCProj</Keyword>
    <WindowsTargetPlatformVersion>10.0</WindowsTargetPlatformVersion>
  </PropertyGroup>
  <Import Project="$(VCTargetsPath)\Microsoft.Cpp.Default.props" />
  <PropertyGroup Label="Configuration">
    <ConfigurationType>Application</ConfigurationType>
    <UseDebugLibraries>true</UseDebugLibraries>
    <PlatformToolset>$(DefaultPlatformToolset)</PlatformToolset>
    <CLRSupport>true</CLRSupport>
    <TargetFrameworkVersion>v4.7.2</TargetFrameworkVersion>
    <CharacterSet>Unicode</CharacterSet>
  </PropertyGroup>
  <Import Project="$(VCTargetsPath)\Microsoft.Cpp.props" />
  <PropertyGroup>
    <ClosureDir>$(MSBuildThisFileDirectory)harvest\bin\Release\net472</ClosureDir>
  </PropertyGroup>
  <ItemDefinitionGroup>
    <ClCompile>
      <WarningLevel>Level3</WarningLevel>
      <Optimization>Disabled</Optimization>
      <RuntimeLibrary>MultiThreadedDebugDLL</RuntimeLibrary>
    </ClCompile>
    <Link>
      <SubSystem>Console</SubSystem>
    </Link>
  </ItemDefinitionGroup>
  <ItemGroup>
    <ClCompile Include="MtpHost.cpp" />
  </ItemGroup>
  <ItemGroup>
    <Reference Include="Microsoft.Testing.Platform">
      <HintPath>$(ClosureDir)\Microsoft.Testing.Platform.dll</HintPath>
    </Reference>
    <Reference Include="MSTest.TestAdapter">
      <HintPath>$(ClosureDir)\MSTest.TestAdapter.dll</HintPath>
    </Reference>
    <Reference Include="MSTest.TestFramework">
      <HintPath>$(ClosureDir)\MSTest.TestFramework.dll</HintPath>
    </Reference>
    <Reference Include="MSTest.TestFramework.Extensions">
      <HintPath>$(ClosureDir)\MSTest.TestFramework.Extensions.dll</HintPath>
    </Reference>
  </ItemGroup>
  <Import Project="$(VCTargetsPath)\Microsoft.Cpp.targets" />
</Project>

#file MtpHost.cpp
using namespace System;
using namespace System::Collections::Generic;
using namespace System::Reflection;
using namespace Microsoft::VisualStudio::TestTools::UnitTesting;
using namespace Microsoft::Testing::Platform::Builder;

[TestClass]
public ref class MtpCalculatorTests
{
public:
    [TestMethod]
    void Add_TwoPlusTwo_IsFour() { Assert::AreEqual(4, 2 + 2); }

    [TestMethod]
    void Strings_AreEqual() { Assert::AreEqual(gcnew String("Hello"), gcnew String("Hello")); }

    [TestMethod]
    void Booleans_Work() { Assert::IsTrue(1 == 1); Assert::IsFalse(1 == 2); }
};

ref class AssemblyProvider
{
public:
    static IEnumerable<Assembly^>^ Get()
    {
        return gcnew array<Assembly^>{ Assembly::GetExecutingAssembly() };
    }
};

// Managed entry point hosting MTP. C++/CLI has no 'await', so Tasks are driven via ->Result.
int main(array<String^>^ args)
{
    ITestApplicationBuilder^ builder = TestApplication::CreateBuilderAsync(args)->Result;

    // AddMSTest is a C# extension method; call it as a static method from C++/CLI.
    Func<IEnumerable<Assembly^>^>^ getAssemblies = gcnew Func<IEnumerable<Assembly^>^>(&AssemblyProvider::Get);
    TestApplicationBuilderExtensions::AddMSTest(builder, getAssemblies);

    ITestApplication^ app = builder->BuildAsync()->Result;
    try
    {
        return app->RunAsync()->Result;
    }
    finally
    {
        delete app;
    }
}
""";
}
