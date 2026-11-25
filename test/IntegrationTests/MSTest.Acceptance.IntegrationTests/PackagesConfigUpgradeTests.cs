// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Globalization;
using System.Text;
using System.Text.Json;

using Microsoft.Testing.Platform.Acceptance.IntegrationTests;
using Microsoft.Testing.Platform.Acceptance.IntegrationTests.Helpers;

namespace MSTest.Acceptance.IntegrationTests;

/// <summary>
/// Guards the packages.config upgrade path. Updating a packages.config project in place keeps its existing
/// references, so a project first installed from an adapter version without lib references has no
/// MSTest.TestAdapter references; the adapter's build targets must still add them so tests are discovered.
/// </summary>
[TestClass]
[OSCondition(OperatingSystems.Windows, IgnoreMessage = "packages.config projects require Visual Studio MSBuild and .NET Framework.")]
public sealed class PackagesConfigUpgradeTests : AcceptanceTestBase<NopAssetFixture>
{
    private const string AssetName = "PackagesConfigUpgrade";

    // Nearest-first build/<framework> folders for net48, mirroring the folder NuGet imports on packages.config install.
    private static readonly string[] BuildFrameworkPreference =
    [
        "net48", "net472", "net471", "net47", "net462", "net461", "net46", "net452", "net451", "net45", "net403", "net40", "net35", "net20",
        "netstandard2.0", "netstandard1.6", "netstandard1.5", "netstandard1.4", "netstandard1.3", "netstandard1.2", "netstandard1.1", "netstandard1.0",
    ];

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    [DataRow("FrenchOnly", true, true, true, "fr", "fr")]
    [DataRow("ResourcesDisabled", true, true, false, "fr", "")]
    [DataRow("NotTestApplication", true, false, true, "fr", "")]
    [DataRow("UpgradedPackagesConfig", false, true, true, "fr", "fr")]
    public async Task ClassicUwpNonSdkAssetPolicy_FiltersResolvedAdapterAssets(
        string scenario,
        bool includeAdapterReferences,
        bool isTestApplication,
        bool copyResources,
        string satelliteLanguages,
        string expectedCulture)
    {
        CancellationToken cancellationToken = TestContext.CancellationToken;
        string? vsInstallPath = await TryFindVsInstallWithMSBuildAsync(cancellationToken);
        string? msbuildExe = vsInstallPath is null ? null : CppCliTestSupport.TryGetMSBuildPathFromVsInstall(vsInstallPath);
        if (msbuildExe is null)
        {
            Assert.Inconclusive("Skipping: Visual Studio MSBuild is required for non-SDK reference resolution.");
            return;
        }

        using TestAsset testAsset = await TestAsset.GenerateAssetAsync(
            $"ClassicUwpNonSdkPolicy{scenario}",
            ClassicUwpPolicySource.PatchCodeWithReplace("$MSTestVersion$", MSTestVersion));
        await DotnetCli.RunAsync(
            $"restore \"{Path.Combine(testAsset.TargetAssetPath, "restore", "Restore.csproj")}\"",
            workingDirectory: testAsset.TargetAssetPath,
            cancellationToken: cancellationToken);

        using var assets = JsonDocument.Parse(File.ReadAllText(Path.Combine(testAsset.TargetAssetPath, "restore", "obj", "project.assets.json")));
        JsonElement root = assets.RootElement;
        string packagesRoot = root.GetProperty("packageFolders").EnumerateObject().First().Name;
        JsonProperty adapter = root.GetProperty("libraries").EnumerateObject()
            .Single(library => library.Name.StartsWith("MSTest.TestAdapter/", StringComparison.OrdinalIgnoreCase));
        string packageDirectory = Path.GetFullPath(Path.Combine(packagesRoot, adapter.Value.GetProperty("path").GetString()!));
        string projectPath = Path.Combine(testAsset.TargetAssetPath, "Policy.proj");
        string reportPath = Path.Combine(testAsset.TargetAssetPath, "assets.txt");
        string binlogPath = Path.Combine(testAsset.TargetAssetPath, "policy.binlog");

        using var build = new CommandLine();
        int exitCode = await build.RunAsyncAndReturnExitCodeAsync(
            $"\"{msbuildExe}\" \"{projectPath}\" /t:ReportAssets /p:AdapterPackageRoot=\"{packageDirectory}\" " +
            $"/p:IncludeAdapterReferences={includeAdapterReferences} " +
            $"/p:IsTestApplication={isTestApplication} /p:EnableMSTestV2CopyResources={copyResources} " +
            $"/p:SatelliteResourceLanguages={satelliteLanguages} /bl:\"{binlogPath}\"",
            CppCliTestSupport.BuildEnvironmentWithoutCodeCoverage(),
            workingDirectory: testAsset.TargetAssetPath,
            cleanDefaultEnvironmentVariableIfCustomAreProvided: true,
            cancellationToken: cancellationToken);
        Assert.AreEqual(0, exitCode, $"{build.StandardOutput}{Environment.NewLine}{build.ErrorOutput}");

        string adapterLib = Path.Combine(packageDirectory, "lib", "uap10.0.16299") + Path.DirectorySeparatorChar;
        string[] actual = File.ReadAllLines(reportPath)
            .Select(line => line.Split('|'))
            .Where(parts => parts.Length == 3 && parts[1].StartsWith(adapterLib, StringComparison.OrdinalIgnoreCase))
            .Select(parts => $"{parts[0]}|{Path.GetFileName(parts[1])}|{parts[2]}")
            .Order(StringComparer.Ordinal)
            .ToArray();
        List<string> expected = [];
        if (isTestApplication)
        {
            expected.Add("Copy|MSTest.TestAdapter.dll|");
            expected.Add("Copy|MSTestAdapter.PlatformServices.dll|");
        }

        if (expectedCulture.Length > 0)
        {
            expected.Add($"Copy|MSTest.TestAdapter.resources.dll|{expectedCulture}\\");
            expected.Add($"Satellite|MSTest.TestAdapter.resources.dll|{expectedCulture}\\");
        }

        Assert.AreSequenceEqual(expected.Order(StringComparer.Ordinal).ToArray(), actual);
    }

    [TestMethod]
    public async Task UpgradedProjectWithoutAdapterReferences_CopiesAdapterAndRunsTests()
    {
        CancellationToken cancellationToken = TestContext.CancellationToken;
        string? vsInstallPath = await TryFindVsInstallWithMSBuildAsync(cancellationToken);
        if (vsInstallPath is null)
        {
            Assert.Inconclusive("Skipping: no Visual Studio install with MSBuild (Microsoft.Component.MSBuild) was found.");
            return;
        }

        string? msbuildExe = CppCliTestSupport.TryGetMSBuildPathFromVsInstall(vsInstallPath);
        if (msbuildExe is null)
        {
            Assert.Inconclusive($"Skipping: MSBuild.exe was not found under the located Visual Studio install '{vsInstallPath}'.");
            return;
        }

        string vstestConsolePath = Path.Combine(vsInstallPath, "Common7", "IDE", "Extensions", "TestPlatform", "vstest.console.exe");
        if (!File.Exists(vstestConsolePath))
        {
            Assert.Inconclusive($"Skipping: vstest.console.exe was not found under the located Visual Studio install '{vsInstallPath}'.");
            return;
        }

        using TestAsset testAsset = await TestAsset.GenerateAssetAsync(AssetName, SourceCode.PatchCodeWithReplace("$MSTestVersion$", MSTestVersion));

        // NuGet cannot restore packages.config from the command line without nuget.exe, so restore the same packages
        // through an SDK project and lay out the packages.config project from the resolved assets.
        string restoreDirectory = Path.Combine(testAsset.TargetAssetPath, "restore");
        await DotnetCli.RunAsync(
            $"restore \"{Path.Combine(restoreDirectory, "Restore.csproj")}\"",
            workingDirectory: restoreDirectory,
            cancellationToken: cancellationToken);

        string projectDirectory = Path.Combine(testAsset.TargetAssetPath, "pc");
        string project = WritePackagesConfigProject(Path.Combine(restoreDirectory, "obj", "project.assets.json"), projectDirectory);

        // The acceptance host runs under a code-coverage profiler whose variables break nested .NET Framework hosts.
        Dictionary<string, string?> environment = CppCliTestSupport.BuildEnvironmentWithoutCodeCoverage();
        environment["VSLANG"] = "1033";

        string binlogFile = Path.Combine(TempDirectory.TestSuiteDirectory, $"{nameof(UpgradedProjectWithoutAdapterReferences_CopiesAdapterAndRunsTests)}.binlog");
        using var buildCommandLine = new CommandLine();
        int buildExitCode = await buildCommandLine.RunAsyncAndReturnExitCodeAsync(
            $"\"{msbuildExe}\" \"{project}\" /p:Configuration=Debug /restore:false /bl:\"{binlogFile}\"",
            environment,
            workingDirectory: projectDirectory,
            cleanDefaultEnvironmentVariableIfCustomAreProvided: true,
            cancellationToken: cancellationToken);
        Assert.AreEqual(
            0,
            buildExitCode,
            $"packages.config build failed.{Environment.NewLine}{buildCommandLine.StandardOutput}{Environment.NewLine}{buildCommandLine.ErrorOutput}");

        string outputDirectory = Path.Combine(projectDirectory, "bin", "Debug");
        Assert.IsTrue(File.Exists(Path.Combine(outputDirectory, "MSTest.TestAdapter.dll")), $"MSTest.TestAdapter.dll was not copied to '{outputDirectory}'.");
        Assert.IsTrue(File.Exists(Path.Combine(outputDirectory, "MSTestAdapter.PlatformServices.dll")), $"MSTestAdapter.PlatformServices.dll was not copied to '{outputDirectory}'.");

        // No /TestAdapterPath: VSTest must discover the adapter next to the test assembly.
        using var runCommandLine = new CommandLine();
        int runExitCode = await runCommandLine.RunAsyncAndReturnExitCodeAsync(
            $"\"{vstestConsolePath}\" \"{Path.Combine(outputDirectory, "PcTests.dll")}\" /logger:\"console;verbosity=normal\"",
            environment,
            // vstest.console.exe lives under Program Files; run from a writable folder for the adapter deployment directory.
            workingDirectory: projectDirectory,
            cleanDefaultEnvironmentVariableIfCustomAreProvided: true,
            cancellationToken: cancellationToken);

        string output = runCommandLine.StandardOutput;
        Assert.AreEqual(
            0,
            runExitCode,
            $"VSTest run did not succeed.{Environment.NewLine}{output}{Environment.NewLine}{runCommandLine.ErrorOutput}");
        Assert.Contains("Passed TestMethod1", output);
        Assert.Contains("Passed TestMethod2", output);
        Assert.Contains("Total tests: 2", output);
        Assert.Contains("Passed: 2", output);
    }

    private static async Task<string?> TryFindVsInstallWithMSBuildAsync(CancellationToken cancellationToken)
    {
        string vswherePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
            "Microsoft Visual Studio",
            "Installer",
            "vswhere.exe");
        if (!File.Exists(vswherePath))
        {
            return null;
        }

        using var commandLine = new CommandLine();
        int exitCode = await commandLine.RunAsyncAndReturnExitCodeAsync(
            $"\"{vswherePath}\" -latest -prerelease -requires Microsoft.Component.MSBuild -property installationPath",
            cancellationToken: cancellationToken);
        if (exitCode != 0)
        {
            return null;
        }

        string installPath = commandLine.StandardOutput.Trim();
        return string.IsNullOrEmpty(installPath) ? null : installPath;
    }

    // Writes what NuGet's packages.config install would produce for the restored packages: packages.config,
    // HintPath references, and build props/targets imports. The packages are referenced in place from the
    // restore's global packages folder. MSTest.TestAdapter references are intentionally omitted to simulate an
    // in-place upgrade from an adapter version that did not ship them as references.
    private static string WritePackagesConfigProject(string assetsFile, string projectDirectory)
    {
        using var assets = JsonDocument.Parse(File.ReadAllText(assetsFile));
        JsonElement root = assets.RootElement;
        string packagesRoot = root.GetProperty("packageFolders").EnumerateObject().First().Name;
        // The restore project has a single framework and no runtime identifier; NuGet may key it by framework name or alias.
        JsonElement targets = root.GetProperty("targets").EnumerateObject().Single(target => !target.Name.Contains('/')).Value;

        StringBuilder packagesConfig = new();
        StringBuilder references = new();
        StringBuilder propsImports = new();
        StringBuilder targetsImports = new();
        foreach (JsonProperty library in root.GetProperty("libraries").EnumerateObject()
            .Where(library => library.Value.GetProperty("type").GetString() == "package")
            .OrderBy(library => library.Name, StringComparer.OrdinalIgnoreCase))
        {
            string[] identity = library.Name.Split('/');
            string id = identity[0];
            string packageDirectory = Path.GetFullPath(Path.Combine(packagesRoot, library.Value.GetProperty("path").GetString()!));
            packagesConfig.AppendLine(CultureInfo.InvariantCulture, $"""  <package id="{id}" version="{identity[1]}" targetFramework="net48" />""");

            if (!string.Equals(id, "MSTest.TestAdapter", StringComparison.OrdinalIgnoreCase)
                && targets.TryGetProperty(library.Name, out JsonElement target)
                && target.TryGetProperty("runtime", out JsonElement runtime))
            {
                foreach (string item in runtime.EnumerateObject().Select(item => item.Name).Where(item => item.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)))
                {
                    references.AppendLine(CultureInfo.InvariantCulture, $"""
    <Reference Include="{Path.GetFileNameWithoutExtension(item)}">
      <HintPath>{Path.Combine(packageDirectory, item.Replace('/', Path.DirectorySeparatorChar))}</HintPath>
    </Reference>
""");
                }
            }

            string buildDirectory = Path.Combine(packageDirectory, "build");
            buildDirectory = BuildFrameworkPreference.Select(framework => Path.Combine(buildDirectory, framework)).FirstOrDefault(Directory.Exists) ?? buildDirectory;
            AppendImport(propsImports, Path.Combine(buildDirectory, $"{id}.props"));
            AppendImport(targetsImports, Path.Combine(buildDirectory, $"{id}.targets"));
        }

        File.WriteAllText(Path.Combine(projectDirectory, "packages.config"), $"""
<?xml version="1.0" encoding="utf-8"?>
<packages>
{packagesConfig}</packages>
""");

        string project = Path.Combine(projectDirectory, "PcTests.csproj");
        File.WriteAllText(project, $$"""
<?xml version="1.0" encoding="utf-8"?>
<Project ToolsVersion="15.0" xmlns="http://schemas.microsoft.com/developer/msbuild/2003">
{{propsImports}}  <Import Project="$(MSBuildExtensionsPath)\$(MSBuildToolsVersion)\Microsoft.Common.props" Condition="Exists('$(MSBuildExtensionsPath)\$(MSBuildToolsVersion)\Microsoft.Common.props')" />
  <PropertyGroup>
    <Configuration Condition=" '$(Configuration)' == '' ">Debug</Configuration>
    <Platform Condition=" '$(Platform)' == '' ">AnyCPU</Platform>
    <OutputType>Library</OutputType>
    <RootNamespace>PcTests</RootNamespace>
    <AssemblyName>PcTests</AssemblyName>
    <TargetFrameworkVersion>v4.8</TargetFrameworkVersion>
    <DebugSymbols>true</DebugSymbols>
    <DebugType>portable</DebugType>
    <OutputPath>bin\$(Configuration)\</OutputPath>
    <AutoGenerateBindingRedirects>true</AutoGenerateBindingRedirects>
    <GenerateBindingRedirectsOutputType>true</GenerateBindingRedirectsOutputType>
  </PropertyGroup>
  <ItemGroup>
    <Reference Include="System" />
    <Reference Include="System.Core" />
{{references}}  </ItemGroup>
  <ItemGroup>
    <Compile Include="UnitTest1.cs" />
    <None Include="packages.config" />
  </ItemGroup>
  <Import Project="$(MSBuildToolsPath)\Microsoft.CSharp.targets" />
{{targetsImports}}</Project>
""");
        return project;

        static void AppendImport(StringBuilder imports, string path)
        {
            if (File.Exists(path))
            {
                imports.AppendLine(CultureInfo.InvariantCulture, $"""  <Import Project="{path}" Condition="Exists('{path}')" />""");
            }
        }
    }

    private const string SourceCode = """
#file restore/Restore.csproj
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net48</TargetFramework>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="MSTest.TestAdapter" Version="$MSTestVersion$" />
    <PackageReference Include="MSTest.TestFramework" Version="$MSTestVersion$" />
  </ItemGroup>
</Project>

#file pc/UnitTest1.cs
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace PcTests
{
    [TestClass]
    public class UnitTest1
    {
        [TestMethod]
        public void TestMethod1()
        {
        }

        [TestMethod]
        public void TestMethod2()
        {
        }
    }
}
""";

    private const string ClassicUwpPolicySource = """
#file restore/Restore.csproj
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="MSTest.TestAdapter" Version="$MSTestVersion$" />
  </ItemGroup>
</Project>

#file Policy.proj
<Project ToolsVersion="Current" xmlns="http://schemas.microsoft.com/developer/msbuild/2003">
  <PropertyGroup>
    <TargetFrameworkVersion>v4.8</TargetFrameworkVersion>
    <OutputType>Library</OutputType>
    <AssemblyName>PolicyProbe</AssemblyName>
    <OutputPath>$(MSBuildThisFileDirectory)bin\</OutputPath>
    <IntermediateOutputPath>$(MSBuildThisFileDirectory)obj-policy\</IntermediateOutputPath>
    <EnableMSTestRunner>true</EnableMSTestRunner>
  </PropertyGroup>
  <Import Project="$(AdapterPackageRoot)\build\uap10.0\MSTest.TestAdapter.props" />
  <ItemGroup Condition="'$(IncludeAdapterReferences)' == 'true'">
    <Reference Include="MSTest.TestAdapter">
      <HintPath>$(AdapterPackageRoot)\lib\uap10.0.16299\MSTest.TestAdapter.dll</HintPath>
    </Reference>
    <Reference Include="MSTestAdapter.PlatformServices">
      <HintPath>$(AdapterPackageRoot)\lib\uap10.0.16299\MSTestAdapter.PlatformServices.dll</HintPath>
    </Reference>
  </ItemGroup>
  <Import Project="$(MSBuildToolsPath)\Microsoft.Common.targets" />
  <Import Project="$(AdapterPackageRoot)\build\uap10.0\MSTest.TestAdapter.targets" />
  <!-- Exercise the policy on the same satellite item shapes produced by non-SDK reference resolution. -->
  <Target Name="InjectSatelliteItems" BeforeTargets="ResolveAssemblyReferences">
    <ItemGroup>
      <ReferenceCopyLocalPaths Include="$(AdapterPackageRoot)\lib\uap10.0.16299\fr\MSTest.TestAdapter.resources.dll">
        <DestinationSubDirectory>fr\</DestinationSubDirectory>
      </ReferenceCopyLocalPaths>
      <ReferenceCopyLocalPaths Include="$(AdapterPackageRoot)\lib\uap10.0.16299\ja\MSTest.TestAdapter.resources.dll">
        <DestinationSubDirectory>ja\</DestinationSubDirectory>
      </ReferenceCopyLocalPaths>
      <ReferenceSatellitePaths Include="@(ReferenceCopyLocalPaths)" Condition="'%(ReferenceCopyLocalPaths.DestinationSubDirectory)' != ''" />
    </ItemGroup>
  </Target>
  <Target Name="ReportAssets" DependsOnTargets="ResolveReferences">
    <WriteLinesToFile File="$(MSBuildThisFileDirectory)assets.txt" Overwrite="true"
                      Lines="@(ReferenceCopyLocalPaths->'Copy|%(FullPath)|%(DestinationSubDirectory)');@(ReferenceSatellitePaths->'Satellite|%(FullPath)|%(DestinationSubDirectory)');@(Content->'Content|%(FullPath)|%(Link)')" />
  </Target>
</Project>
""";
}
