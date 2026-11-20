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
    [DataRow("lib/netstandard2.0", false, false, DisplayName = "NetStandardForwarders")]
    [DataRow("lib/net462", true, true, DisplayName = "NetFrameworkForwarders")]
    [DataRow("lib/net10.0", true, false, DisplayName = "Net10Forwarders")]
    [DataRow("lib/net11.0", true, false, DisplayName = "Net11Forwarders")]
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
        Assert.AreSequenceEqual(
            ["<Module>"],
            metadata.TypeDefinitions.Select(handle => metadata.GetString(metadata.GetTypeDefinition(handle).Name)));
        ExportedType[] forwardedTypes = metadata.ExportedTypes
            .Select(metadata.GetExportedType)
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
            Assert.AreEqual("Microsoft.VisualStudio.TestTools.UnitTesting", metadata.GetString(type.Namespace));
            Assert.IsTrue(type.IsForwarder);
            Assert.AreEqual(HandleKind.AssemblyReference, type.Implementation.Kind);
            Assert.AreEqual(
                "MSTest.TestFramework",
                metadata.GetString(metadata.GetAssemblyReference((AssemblyReferenceHandle)type.Implementation).Name));
        }
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    [DataRow("net10.0-windows10.0.18362.0", false, true, true, "uwp", false, DisplayName = "ModernUwpMinimum")]
    [DataRow("net10.0-windows10.0.26100.0", false, false, false, "", false, DisplayName = "OrdinaryWindows")]
    [DataRow("net10.0-windows10.0.26100.0", true, false, false, "winui", false, DisplayName = "WinUI")]
    [DataRow("net10.0-windows10.0.26100.0", false, true, true, "uwp", false, DisplayName = "ModernUwp")]
    [DataRow("net10.0-windows10.0.26100.0", false, false, true, "", false, DisplayName = "ExplicitUwpToolsOptOut")]
    [DataRow("net10.0-windows10.0.26100.0", true, false, false, "winui", true, DisplayName = "LateImportWinUI")]
    [DataRow("net10.0-windows10.0.26100.0", false, true, true, "uwp", true, DisplayName = "LateImportModernUwp")]
    [DataRow("net11.0-windows10.0.26100.0", false, false, false, "", false, DisplayName = "Net11OrdinaryWindows")]
    [DataRow("net11.0-windows10.0.26100.0", true, false, false, "winui", false, DisplayName = "Net11WinUI")]
    [DataRow("net11.0-windows10.0.26100.0", false, true, true, "uwp", false, DisplayName = "Net11ModernUwp")]
    public async Task PackedConsumersUseSharedCoreAndSelectedUiPayload(
        string targetFramework, bool useWinUI, bool useUwpTools, bool useUwp, string uiPayload, bool lateImport)
    {
        string coreAsset = $"lib/{targetFramework.Split('-')[0]}";
        string uiAssembly = uiPayload switch
        {
            "winui" => "MSTest.WinUI",
            "uwp" => "MSTest.Uwp",
            _ => string.Empty,
        };

        // UI applications reference MSTest.TestAdapter too: the UI framework payload ships in MSTest.TestFramework and the
        // shared WinUI/UWP adapter build ships in MSTest.TestAdapter, both outside lib/<tfm> and only for net10.0.
        string modelProperties = $"""
            <UseWinUI>{useWinUI}</UseWinUI>
                <UseUwpTools>{useUwpTools}</UseUwpTools>
                <UseUwp>{useUwp}</UseUwp>
            """;

        // Directory.Build.targets is imported after the restored package targets, so late rows prove the payload is
        // selected at execution time. The parent import keeps the shared test-infrastructure targets.
        string source = ConsumerSource
            .Replace("$ModelProperties$", lateImport ? string.Empty : modelProperties, StringComparison.Ordinal)
            .Replace("$LateModelProperties$", lateImport ? modelProperties : string.Empty, StringComparison.Ordinal)
            .Replace("$TargetFramework$", targetFramework, StringComparison.Ordinal)
            .Replace("$MSTestVersion$", MSTestVersion, StringComparison.Ordinal)
            .Replace("$UiAssembly$", uiAssembly, StringComparison.Ordinal)
            .Replace("$UiReference$", uiAssembly.Length == 0 ? string.Empty : $"<PackageReference Include=\"MSTest.TestAdapter\" Version=\"{MSTestVersion}\" />", StringComparison.Ordinal);
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
        List<(string Package, string Name, string Asset)> assemblies =
        [
            ("MSTest.TestFramework", "MSTest.TestFramework", coreAsset),
            ("MSTest.TestFramework", "MSTest.TestFramework.Extensions", coreAsset),
        ];
        if (uiAssembly.Length != 0)
        {
            assemblies.Add(("MSTest.TestFramework", uiAssembly, $"buildTransitive/net10.0/ui/{uiPayload}"));
            assemblies.Add(("MSTest.TestAdapter", "MSTest.TestAdapter", "buildTransitive/net10.0/ui"));
            assemblies.Add(("MSTest.TestAdapter", "MSTestAdapter.PlatformServices", "buildTransitive/net10.0/ui"));
        }

        string output = Path.Join(asset.TargetAssetPath, "bin", "Release", targetFramework);
        string[] resolved = File.ReadAllLines(Path.Join(asset.TargetAssetPath, "resolved-framework-assets.txt"));
        var expectedResolved = new List<string>();
        foreach ((string packageId, string name, string folder) in assemblies)
        {
            string package = Path.Join(Constants.ArtifactsPackagesShipping, $"{packageId}.{MSTestVersion}.nupkg");
            string restored = Path.Join(cache, packageId.ToLowerInvariant(), MSTestVersion);
            string cachedPackage = Path.Join(restored, $"{packageId.ToLowerInvariant()}.{MSTestVersion}.nupkg");
            Assert.AreEqual(
                Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(package))),
                Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(cachedPackage))));
            using ZipArchive archive = ZipFile.OpenRead(package);
            string relative = $"{folder}/{name}.dll";
            using Stream packedAssembly = archive.GetEntry(relative)!.Open();
            string hash = Convert.ToHexString(SHA256.HashData(packedAssembly));
            string cachedAssembly = Path.GetFullPath(Path.Join(restored, relative));
            string copiedAssembly = Path.Join(output, name + ".dll");
            Assert.IsTrue(File.Exists(copiedAssembly), $"{relative} was not copied to the build output.");
            Assert.AreEqual(hash, Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(cachedAssembly))), relative);
            Assert.AreEqual(hash, Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(copiedAssembly))), relative);
            expectedResolved.Add($"compile={cachedAssembly}");
            expectedResolved.Add($"runtime={cachedAssembly}");
            TestContext.WriteLine($"{relative}: SHA256={hash}");
        }

        Assert.AreSequenceEqual(expectedResolved.Order(StringComparer.Ordinal), resolved.Order(StringComparer.Ordinal));
        using ZipArchive corePackage = ZipFile.OpenRead(Path.Join(Constants.ArtifactsPackagesShipping, $"MSTest.TestFramework.{MSTestVersion}.nupkg"));
        using Stream packedResource = corePackage.GetEntry($"{coreAsset}/fr/MSTest.TestFramework.resources.dll")!.Open();
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
        string expectedOutput = uiAssembly.Length == 0
            ? $"""
            {Path.Join(output, "MSTest.TestFramework.dll")}
            {Path.Join(output, "MSTest.TestFramework.Extensions.dll")}
            {copiedResource}
            Packed framework ownership and API shape passed.
            """
            : $"""
            {Path.Join(output, "MSTest.TestFramework.dll")}
            {Path.Join(output, "MSTest.TestFramework.Extensions.dll")}
            {Path.Join(output, uiAssembly + ".dll")}
            {copiedResource}
            Packed framework ownership and API shape passed.
            """;
        Assert.AreEqual(expectedOutput.ReplaceLineEndings(), muxer.StandardOutput.ReplaceLineEndings());
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
            $ModelProperties$
            <RestorePackagesPath>$(MSBuildProjectDirectory)\.packages</RestorePackagesPath>
            <MarkPackageReferencesAsExternallyResolved>false</MarkPackageReferencesAsExternallyResolved>
          </PropertyGroup>
          <ItemGroup>
            <PackageReference Include="MSTest.TestFramework" Version="$MSTestVersion$" />
            $UiReference$
          </ItemGroup>
          <Target Name="ReportFrameworkAssets" AfterTargets="Build">
            <ItemGroup>
              <_FrameworkCompile Include="@(ReferencePath)" Condition=" ('%(Filename)' == 'MSTest.TestFramework' OR '%(Filename)' == 'MSTest.TestFramework.Extensions' OR '%(Filename)' == '$UiAssembly$' OR '%(Filename)' == 'MSTest.TestAdapter' OR '%(Filename)' == 'MSTestAdapter.PlatformServices') AND '%(Extension)' == '.dll' " />
              <_FrameworkRuntime Include="@(ReferenceCopyLocalPaths)" Condition=" ('%(Filename)' == 'MSTest.TestFramework' OR '%(Filename)' == 'MSTest.TestFramework.Extensions' OR '%(Filename)' == '$UiAssembly$' OR '%(Filename)' == 'MSTest.TestAdapter' OR '%(Filename)' == 'MSTestAdapter.PlatformServices') AND '%(Extension)' == '.dll' " />
            </ItemGroup>
            <WriteLinesToFile File="$(MSBuildProjectDirectory)\resolved-framework-assets.txt" Lines="@(_FrameworkCompile->'compile=%(FullPath)');@(_FrameworkRuntime->'runtime=%(FullPath)')" Overwrite="true" />
          </Target>
        </Project>

        #file Directory.Build.targets
        <Project>
          <Import Project="$([MSBuild]::GetPathOfFileAbove('Directory.Build.targets', '$(MSBuildThisFileDirectory)../'))" />
          <PropertyGroup>
            $LateModelProperties$
          </PropertyGroup>
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
        Assert.IsNotNull(core.GetType("Microsoft.VisualStudio.TestTools.UnitTesting.DeploymentItemAttribute"));
        string[] directories = ["TestRunDirectory", "DeploymentDirectory", "ResultsDirectory", "TestRunResultsDirectory", "TestResultsDirectory", "TestTempDirectory"];
        foreach (string directory in directories)
        {
            Assert.IsNotNull(typeof(TestContext).GetProperty(directory), directory);
        }

        Assert.IsNull(typeof(TestContext).GetProperty("DataRow"));
        Assert.IsNull(typeof(TestContext).GetProperty("DataConnection"));
        Console.WriteLine(core.Location);
        Console.WriteLine(extensions.Location);
        if ("$UiAssembly$".Length != 0)
        {
            Assembly ui = Assembly.Load("$UiAssembly$");
            Assert.IsNull(ui.GetType(typeof(TestContext).FullName!));
            Assembly services = Assembly.Load("MSTestAdapter.PlatformServices");
            Type implementation = services.GetType("Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.TestContextImplementation", true)!;
            var properties = directories.ToDictionary(name => name, name => (object?)"must-not-be-a-directory");
            var context = (TestContext)implementation.GetConstructors(BindingFlags.Instance | BindingFlags.NonPublic)
                .Single(constructor => constructor.GetParameters().Length == 5)
                .Invoke([null, null, properties, null, null]);
            foreach (string directory in directories)
            {
                Assert.AreEqual("must-not-be-a-directory", context.Properties[directory]);
                Assert.IsNull(typeof(TestContext).GetProperty(directory)!.GetValue(context), directory);
            }

            ((IDisposable)context).Dispose();
            Console.WriteLine(ui.Location);
        }

        Console.WriteLine(core.GetSatelliteAssembly(System.Globalization.CultureInfo.GetCultureInfo("fr")).Location);
        Console.WriteLine("Packed framework ownership and API shape passed.");
        """;
}
