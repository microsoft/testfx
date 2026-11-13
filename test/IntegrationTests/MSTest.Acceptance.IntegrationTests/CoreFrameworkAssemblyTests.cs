// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.IO.Compression;
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
    [DataRow("net10.0-windows10.0.26100.0", false, false, false, "", DisplayName = "OrdinaryWindows")]
    [DataRow("net10.0-windows10.0.26100.0", true, false, false, "MSTest.WinUI", DisplayName = "WinUI")]
    [DataRow("net10.0-windows10.0.26100.0", false, true, true, "MSTest.Uwp", DisplayName = "ModernUwp")]
    [DataRow("net10.0-windows10.0.26100.0", false, false, true, "", DisplayName = "ExplicitUwpToolsOptOut")]
    [DataRow("net11.0-windows10.0.26100.0", true, false, false, "MSTest.WinUI", DisplayName = "Net11WinUI")]
    [DataRow("net11.0-windows10.0.26100.0", false, true, true, "MSTest.Uwp", DisplayName = "Net11ModernUwp")]
    public async Task PackedConsumersUseSharedCoreAndSelectedUiPackage(
        string targetFramework, bool useWinUI, bool useUwpTools, bool useUwp, string uiPackage)
    {
        string coreAsset = $"lib/{targetFramework.Split('-')[0]}";
        string uiTfm = useWinUI ? "net10.0-windows10.0.18362" : "net10.0-windows10.0.17763";
        string source = ConsumerSource
            .Replace("$TargetFramework$", targetFramework, StringComparison.Ordinal)
            .Replace("$MSTestVersion$", MSTestVersion, StringComparison.Ordinal)
            .Replace("$UseWinUI$", useWinUI.ToString(), StringComparison.Ordinal)
            .Replace("$UseUwpTools$", useUwpTools.ToString(), StringComparison.Ordinal)
            .Replace("$UseUwp$", useUwp.ToString(), StringComparison.Ordinal)
            .Replace("$UiPackage$", uiPackage, StringComparison.Ordinal)
            .Replace("$UiReference$", uiPackage.Length == 0 ? string.Empty : $"<PackageReference Include=\"{uiPackage}\" Version=\"{MSTestVersion}\" />", StringComparison.Ordinal);
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
        ];
        if (uiPackage.Length != 0)
        {
            assemblies.Add((uiPackage, uiPackage, $"lib/{uiTfm}"));
            assemblies.Add((uiPackage, "MSTest.TestAdapter", $"buildTransitive/{uiTfm}"));
            assemblies.Add((uiPackage, "MSTestAdapter.PlatformServices", $"buildTransitive/{uiTfm}"));
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
        string expectedOutput = uiPackage.Length == 0
            ? $"""
            {Path.Join(output, "MSTest.TestFramework.dll")}
            {copiedResource}
            Packed framework ownership and API shape passed.
            """
            : $"""
            {Path.Join(output, "MSTest.TestFramework.dll")}
            {Path.Join(output, uiPackage + ".dll")}
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
            <UseWinUI>$UseWinUI$</UseWinUI>
            <UseUwpTools>$UseUwpTools$</UseUwpTools>
            <UseUwp>$UseUwp$</UseUwp>
            <RestorePackagesPath>$(MSBuildProjectDirectory)\.packages</RestorePackagesPath>
            <MarkPackageReferencesAsExternallyResolved>false</MarkPackageReferencesAsExternallyResolved>
          </PropertyGroup>
          <ItemGroup>
            <PackageReference Include="MSTest.TestFramework" Version="$MSTestVersion$" />
            $UiReference$
          </ItemGroup>
          <Target Name="ReportFrameworkAssets" AfterTargets="Build">
            <ItemGroup>
              <_FrameworkCompile Include="@(ReferencePath)" Condition=" ('%(Filename)' == 'MSTest.TestFramework' OR '%(Filename)' == '$UiPackage$' OR '%(Filename)' == 'MSTest.TestAdapter' OR '%(Filename)' == 'MSTestAdapter.PlatformServices') AND '%(Extension)' == '.dll' " />
              <_FrameworkRuntime Include="@(ReferenceCopyLocalPaths)" Condition=" ('%(Filename)' == 'MSTest.TestFramework' OR '%(Filename)' == '$UiPackage$' OR '%(Filename)' == 'MSTest.TestAdapter' OR '%(Filename)' == 'MSTestAdapter.PlatformServices') AND '%(Extension)' == '.dll' " />
            </ItemGroup>
            <WriteLinesToFile File="$(MSBuildProjectDirectory)\resolved-framework-assets.txt" Lines="@(_FrameworkCompile->'compile=%(FullPath)');@(_FrameworkRuntime->'runtime=%(FullPath)')" Overwrite="true" />
          </Target>
        </Project>

        #file Program.cs
        using System.Reflection;
        using Microsoft.VisualStudio.TestTools.UnitTesting;

        Assembly core = typeof(TestMethodAttribute).Assembly;
        Assert.AreSame(core, typeof(TestContext).Assembly);
        Assert.IsFalse(File.Exists(Path.Join(AppContext.BaseDirectory, "MSTest.TestFramework.Extensions.dll")));
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
        if ("$UiPackage$".Length != 0)
        {
            Assembly ui = Assembly.Load("$UiPackage$");
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
