// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.IO.Compression;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Security.Cryptography;
using System.Text.Json;

using Microsoft.Testing.Platform.Acceptance.IntegrationTests;
using Microsoft.Testing.Platform.Acceptance.IntegrationTests.Helpers;

namespace MSTest.Acceptance.IntegrationTests;

[TestClass]
public sealed class CoreFrameworkAssemblyTests : AcceptanceTestBase<NopAssetFixture>
{
    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    [DataRow("lib/netstandard2.0", false, false, DisplayName = "NetStandardForwarders")]
    [DataRow("lib/net462", true, true, DisplayName = "NetFrameworkForwarders")]
    [DataRow("lib/uap10.0", false, false, DisplayName = "ClassicUwpForwarders")]
    [DataRow("buildTransitive/net10.0", true, false, DisplayName = "Net10Forwarders")]
    [DataRow("buildTransitive/net11.0", true, false, DisplayName = "Net11Forwarders")]
    [DataRow("buildTransitive/net10.0/uwp", false, false, DisplayName = "ModernUwpForwarders")]
    [DataRow("buildTransitive/net10.0/winui", false, false, DisplayName = "WinUIForwarders")]
    public void PackedExtensionsForwardMovedTypesToCore(string assetFolder, bool supportsDeployment, bool supportsPrivateHelpers)
    {
        string package = Path.Join(Constants.ArtifactsPackagesShipping, $"MSTest.TestFramework.{MSTestVersion}.nupkg");
        using ZipArchive archive = ZipFile.OpenRead(package);
        ZipArchiveEntry? entry = archive.GetEntry($"{assetFolder}/MSTest.TestFramework.Extensions.dll");
        Assert.IsNotNull(entry);
        using Stream packedAssembly = entry.Open();
        using var image = new MemoryStream();
        packedAssembly.CopyTo(image);
        image.Position = 0;
        using var pe = new PEReader(image);
        MetadataReader metadata = pe.GetMetadataReader();
        ExportedType[] forwardedTypes = metadata.ExportedTypes
            .Select(metadata.GetExportedType)
            .Where(type => metadata.GetString(type.Namespace) == "Microsoft.VisualStudio.TestTools.UnitTesting")
            .ToArray();
        List<string> expected = ["ITestRunInfo", "MessageLevel", "PlannedTest", "TestContext", "TestRun"];
        if (supportsDeployment)
        {
            expected.Add("DeploymentItemAttribute");
        }

        if (supportsPrivateHelpers)
        {
            expected.AddRange(["DataSourceElement", "DataSourceElementCollection", "PrivateObject", "PrivateType", "TestConfiguration", "TestConfigurationSection"]);
        }

        Assert.AreSequenceEqual(
            expected.Order(StringComparer.Ordinal),
            forwardedTypes.Select(type => metadata.GetString(type.Name)).Order(StringComparer.Ordinal));
        foreach (ExportedType type in forwardedTypes)
        {
            Assert.IsTrue(type.IsForwarder);
            Assert.AreEqual(HandleKind.AssemblyReference, type.Implementation.Kind);
            Assert.AreEqual(
                "MSTest.TestFramework",
                metadata.GetString(metadata.GetAssemblyReference((AssemblyReferenceHandle)type.Implementation).Name));
        }
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    [DataRow("net10.0-windows10.0.26100.0", false, false, false, "lib/net10.0", DisplayName = "OrdinaryWindows")]
    [DataRow("net10.0-windows10.0.26100.0", true, false, false, "buildTransitive/net10.0/winui", DisplayName = "WinUI")]
    [DataRow("net10.0-windows10.0.26100.0", false, true, true, "buildTransitive/net10.0/uwp", DisplayName = "ModernUwp")]
    [DataRow("net10.0-windows10.0.26100.0", false, false, true, "lib/net10.0", DisplayName = "ExplicitUwpToolsOptOut")]
    [DataRow("net11.0-windows10.0.26100.0", false, false, false, "lib/net11.0", DisplayName = "Net11OrdinaryWindows")]
    [DataRow("net11.0-windows10.0.26100.0", true, false, false, "buildTransitive/net10.0/winui", DisplayName = "Net11WinUI")]
    [DataRow("net11.0-windows10.0.26100.0", false, true, true, "buildTransitive/net10.0/uwp", DisplayName = "Net11ModernUwp")]
    public async Task PackedConsumersUseMatchingCoreAndExtensions(
        string targetFramework, bool useWinUI, bool useUwpTools, bool useUwp, string coreAsset)
    {
        bool desktop = coreAsset.StartsWith("lib/", StringComparison.Ordinal);
        string source = ConsumerSource
            .Replace("$TargetFramework$", targetFramework, StringComparison.Ordinal)
            .Replace("$MSTestVersion$", MSTestVersion, StringComparison.Ordinal)
            .Replace("$UseWinUI$", useWinUI.ToString(), StringComparison.Ordinal)
            .Replace("$UseUwpTools$", useUwpTools.ToString(), StringComparison.Ordinal)
            .Replace("$UseUwp$", useUwp.ToString(), StringComparison.Ordinal)
            .Replace("$Desktop$", desktop ? "true" : "false", StringComparison.Ordinal);
        using TestAsset asset = await TestAsset.GenerateAssetAsync("CoreFrameworkConsumer", source);
        string cache = Path.Join(asset.TargetAssetPath, ".packages");
        var environment = new Dictionary<string, string?> { ["NUGET_PACKAGES"] = cache };
        DotnetMuxerResult build = await DotnetCli.RunAsync(
            "build -c Release -bl:consumer.binlog",
            workingDirectory: asset.TargetAssetPath,
            environmentVariables: environment,
            cancellationToken: TestContext.CancellationToken);
        build.AssertExitCodeIs(0);

        using var assets = JsonDocument.Parse(File.ReadAllText(Path.Join(asset.TargetAssetPath, "obj", "project.assets.json")));
        string[] packageFolders = assets.RootElement.GetProperty("packageFolders").EnumerateObject()
            .Select(folder => Path.GetFullPath(folder.Name).TrimEnd(Path.DirectorySeparatorChar)).ToArray();
        Assert.Contains(Path.GetFullPath(cache).TrimEnd(Path.DirectorySeparatorChar), packageFolders);
        Assert.AreEqual(
            Path.GetFullPath(cache),
            Path.GetFullPath(assets.RootElement.GetProperty("project").GetProperty("restore").GetProperty("packagesPath").GetString()!));
        string package = Path.Join(Constants.ArtifactsPackagesShipping, $"MSTest.TestFramework.{MSTestVersion}.nupkg");
        string restored = Path.Join(cache, "mstest.testframework", MSTestVersion);
        string cachedPackage = Path.Join(restored, $"mstest.testframework.{MSTestVersion}.nupkg");
        Assert.AreEqual(
            Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(package))),
            Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(cachedPackage))));
        string extensionsAsset = desktop ? $"buildTransitive/{targetFramework.Split('-')[0]}" : coreAsset;
        (string Name, string Asset)[] assemblies =
        [
            ("MSTest.TestFramework", coreAsset),
            ("MSTest.TestFramework.Extensions", extensionsAsset),
        ];
        string output = Path.Join(asset.TargetAssetPath, "bin", "Release", targetFramework);
        string[] resolved = File.ReadAllLines(Path.Join(asset.TargetAssetPath, "resolved-framework-assets.txt"));
        var expectedResolved = new List<string>();
        using ZipArchive archive = ZipFile.OpenRead(package);
        foreach ((string name, string folder) in assemblies)
        {
            string relative = $"{folder}/{name}.dll";
            using Stream packedAssembly = archive.GetEntry(relative)!.Open();
            string hash = Convert.ToHexString(SHA256.HashData(packedAssembly));
            string cachedAssembly = Path.GetFullPath(Path.Join(restored, relative));
            string copiedAssembly = Path.Join(output, name + ".dll");
            Assert.AreEqual(hash, Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(cachedAssembly))), relative);
            Assert.AreEqual(hash, Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(copiedAssembly))), relative);
            expectedResolved.Add($"compile={cachedAssembly}");
            expectedResolved.Add($"runtime={cachedAssembly}");
            TestContext.WriteLine($"{relative}: SHA256={hash}");
        }

        Assert.AreSequenceEqual(expectedResolved.Order(StringComparer.Ordinal), resolved.Order(StringComparer.Ordinal));
        using Stream packedResource = archive.GetEntry($"{coreAsset}/fr/MSTest.TestFramework.resources.dll")!.Open();
        string copiedResource = Path.Join(output, "fr", "MSTest.TestFramework.resources.dll");
        Assert.AreEqual(
            Convert.ToHexString(SHA256.HashData(packedResource)),
            Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(copiedResource))));
        using DotnetMuxer muxer = new(environment);
        int exit = await muxer.ExecuteAsync(
            $"\"{Path.Join(output, "CoreFrameworkConsumer.dll")}\"",
            workingDirectory: asset.TargetAssetPath,
            cancellationToken: TestContext.CancellationToken);
        Assert.AreEqual(0, exit, $"{muxer.StandardOutput}{muxer.StandardError}");
        Assert.AreEqual(
            $"""
            {Path.Join(output, "MSTest.TestFramework.dll")}
            {Path.Join(output, "MSTest.TestFramework.Extensions.dll")}
            {copiedResource}
            Packed framework ownership and API shape passed.
            """.ReplaceLineEndings(),
            muxer.StandardOutput.ReplaceLineEndings());
        TestContext.WriteLine($"Package: {package}; cache: {restored}");
        TestContext.WriteLine($"Package folders: {string.Join("; ", packageFolders)}");
        TestContext.WriteLine(string.Join(Environment.NewLine, resolved));
        TestContext.WriteLine(muxer.StandardOutput);
    }

    public TestContext TestContext { get; set; }

    private const string ConsumerSource = """
        #file CoreFrameworkConsumer.csproj
        <Project Sdk="Microsoft.NET.Sdk">
          <PropertyGroup>
            <TargetFramework>$TargetFramework$</TargetFramework>
            <OutputType>Exe</OutputType>
            <ImplicitUsings>enable</ImplicitUsings>
            <Nullable>enable</Nullable>
            <LangVersion>12.0</LangVersion>
            <UseWinUI>$UseWinUI$</UseWinUI>
            <UseUwpTools>$UseUwpTools$</UseUwpTools>
            <UseUwp>$UseUwp$</UseUwp>
            <RestorePackagesPath>$(MSBuildProjectDirectory)\.packages</RestorePackagesPath>
            <MarkPackageReferencesAsExternallyResolved>false</MarkPackageReferencesAsExternallyResolved>
          </PropertyGroup>
          <ItemGroup>
            <PackageReference Include="MSTest.TestFramework" Version="$MSTestVersion$" />
          </ItemGroup>
          <Target Name="ReportFrameworkAssets" AfterTargets="Build">
            <ItemGroup>
              <_FrameworkCompile Include="@(ReferencePath)" Condition=" ('%(Filename)' == 'MSTest.TestFramework' OR '%(Filename)' == 'MSTest.TestFramework.Extensions') AND '%(Extension)' == '.dll' " />
              <_FrameworkRuntime Include="@(ReferenceCopyLocalPaths)" Condition=" ('%(Filename)' == 'MSTest.TestFramework' OR '%(Filename)' == 'MSTest.TestFramework.Extensions') AND '%(Extension)' == '.dll' " />
            </ItemGroup>
            <WriteLinesToFile File="$(MSBuildProjectDirectory)\resolved-framework-assets.txt" Lines="@(_FrameworkCompile->'compile=%(FullPath)');@(_FrameworkRuntime->'runtime=%(FullPath)')" Overwrite="true" />
          </Target>
        </Project>

        #file Program.cs
        using System.Reflection;
        using Microsoft.VisualStudio.TestTools.UnitTesting;

        Assembly core = typeof(TestMethodAttribute).Assembly;
        Assert.AreSame(core, typeof(TestContext).Assembly);
        Assembly extensions = Assembly.Load("MSTest.TestFramework.Extensions");
        string[] forwardedTypes = ["TestContext", "MessageLevel", "TestRun", "ITestRunInfo", "PlannedTest"];
        foreach (string typeName in forwardedTypes)
        {
            string fullName = $"Microsoft.VisualStudio.TestTools.UnitTesting.{typeName}";
            Type expected = core.GetType(fullName, throwOnError: true)!;
            Assert.AreSame(expected, extensions.GetType(fullName, throwOnError: true), typeName);
            Assert.AreSame(expected, Type.GetType($"{fullName}, {extensions.FullName}", throwOnError: true), typeName);
        }

        Assert.IsNotNull(core.GetType("Microsoft.VisualStudio.TestTools.UnitTesting.ITestClassInstanceFactory"));
        Assert.IsNotNull(core.GetType("Microsoft.VisualStudio.TestTools.UnitTesting.ITestClassInstanceLease"));
        Assert.AreEqual($Desktop$, core.GetType("Microsoft.VisualStudio.TestTools.UnitTesting.DeploymentItemAttribute") is not null);
        string[] directories = ["TestRunDirectory", "DeploymentDirectory", "ResultsDirectory", "TestRunResultsDirectory", "TestResultsDirectory", "TestTempDirectory"];
        foreach (string directory in directories)
        {
            Assert.AreEqual($Desktop$, typeof(TestContext).GetProperty(directory) is not null, directory);
        }

        Assert.IsNull(typeof(TestContext).GetProperty("DataRow"));
        Assert.IsNull(typeof(TestContext).GetProperty("DataConnection"));
        Console.WriteLine(core.Location);
        Console.WriteLine(extensions.Location);
        Console.WriteLine(core.GetSatelliteAssembly(System.Globalization.CultureInfo.GetCultureInfo("fr")).Location);
        Console.WriteLine("Packed framework ownership and API shape passed.");
        """;
}
