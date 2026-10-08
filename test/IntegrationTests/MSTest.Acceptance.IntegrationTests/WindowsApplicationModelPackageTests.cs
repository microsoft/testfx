// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.IO.Compression;

using Microsoft.Testing.Platform.Acceptance.IntegrationTests;

namespace MSTest.Acceptance.IntegrationTests;

/// <summary>
/// Verifies the physical Windows application-model assets in the configuration-matched MSTest packages.
/// </summary>
[TestClass]
public sealed class WindowsApplicationModelPackageTests
{
    [TestMethod]
    public void PackagedHostFilterTests_AreSelectedByApplicationModelJob()
    {
        string[] categories = typeof(PackagedAppNativeDotnetTestTests)
            .GetCustomAttributes(typeof(TestCategoryAttribute), inherit: false)
            .Cast<TestCategoryAttribute>()
            .SelectMany(attribute => attribute.TestCategories)
            .ToArray();

        Assert.AreSequenceEqual(["WindowsApplicationModel"], categories);
        Assert.ContainsSingle(typeof(PackagedAppNativeDotnetTestTests).GetCustomAttributes(typeof(MemberConditionAttribute), inherit: false));
    }

    private static readonly string[] RequiredTestAdapterEntries =
    [
        // Classic UWP.
        "build/uap10.0/MSTest.TestAdapter.props",
        "build/uap10.0/MSTest.TestAdapter.targets",
        "buildTransitive/uap10.0/Microsoft.Testing.Extensions.TrxReport.Abstractions.dll",
        "buildTransitive/uap10.0/Microsoft.Testing.Extensions.Retry.dll",
        "buildTransitive/uap10.0/Microsoft.Testing.Platform.dll",
        "buildTransitive/uap10.0/MSTest.TestAdapter.dll",
        "buildTransitive/uap10.0/MSTestAdapter.PlatformServices.dll",
        "buildTransitive/uap10.0/MSTest.TestAdapter.props",
        "buildTransitive/uap10.0/MSTest.TestAdapter.targets",
        "buildTransitive/uap10.0/Parallelize.targets",

        // Modern UWP and its normal net9.0 selection targets.
        "build/net9.0/MSTest.TestAdapter.props",
        "build/net9.0/MSTest.TestAdapter.targets",
        "buildTransitive/net9.0/MSTest.TestAdapter.props",
        "buildTransitive/net9.0/MSTest.TestAdapter.targets",
        "buildTransitive/net9.0/Parallelize.targets",
        "buildTransitive/net9.0/uwp/MSTest.TestAdapter.dll",
        "buildTransitive/net9.0/uwp/MSTestAdapter.PlatformServices.dll",

        // WinUI and its normal net8.0 selection targets.
        "build/net8.0/MSTest.TestAdapter.props",
        "build/net8.0/MSTest.TestAdapter.targets",
        "buildTransitive/net8.0/MSTest.TestAdapter.props",
        "buildTransitive/net8.0/MSTest.TestAdapter.targets",
        "buildTransitive/net8.0/Parallelize.targets",
        "buildTransitive/net8.0/winui/MSTest.TestAdapter.dll",
        "buildTransitive/net8.0/winui/MSTestAdapter.PlatformServices.dll",
    ];

    private static readonly string[] RequiredTestFrameworkEntries =
    [
        // Classic UWP.
        "lib/uap10.0/MSTest.TestFramework.dll",
        "lib/uap10.0/MSTest.TestFramework.xml",
        "lib/uap10.0/MSTest.TestFramework.Extensions.dll",
        "lib/uap10.0/MSTest.TestFramework.Extensions.xml",
        "build/uap10.0/MSTest.TestFramework.targets",
        "buildTransitive/uap10.0/MSTest.TestFramework.targets",

        // Modern UWP and its normal net9.0 framework assets.
        "lib/net9.0/MSTest.TestFramework.dll",
        "lib/net9.0/MSTest.TestFramework.xml",
        "build/net9.0/MSTest.TestFramework.targets",
        "buildTransitive/net9.0/MSTest.TestFramework.targets",
        "buildTransitive/net9.0/uwp/MSTest.TestFramework.Extensions.dll",
        "buildTransitive/net9.0/uwp/MSTest.TestFramework.Extensions.xml",

        // WinUI and its normal net8.0 framework assets.
        "lib/net8.0/MSTest.TestFramework.dll",
        "lib/net8.0/MSTest.TestFramework.xml",
        "build/net8.0/MSTest.TestFramework.targets",
        "buildTransitive/net8.0/MSTest.TestFramework.targets",
        "buildTransitive/net8.0/winui/MSTest.TestFramework.Extensions.dll",
        "buildTransitive/net8.0/winui/MSTest.TestFramework.Extensions.xml",
    ];

    [TestMethod]
    [OSCondition(OperatingSystems.Windows, IgnoreMessage = "Windows application-model package assets are produced only by Windows packs.")]
    public void PackedMSTestTestAdapter_ContainsRequiredWindowsApplicationModelAssets()
        => AssertPackageContainsAllEntries(
            GetExactCurrentPackagePath("MSTest.TestAdapter"),
            RequiredTestAdapterEntries);

    [TestMethod]
    [OSCondition(OperatingSystems.Windows, IgnoreMessage = "Windows application-model package assets are produced only by Windows packs.")]
    public void PackedMSTestTestAdapter_UwpPropsRegisterMSTestBuilderHook()
    {
        string packagePath = GetExactCurrentPackagePath("MSTest.TestAdapter");
        using ZipArchive archive = ZipFile.OpenRead(packagePath);
        ZipArchiveEntry propsEntry = archive.GetEntry("buildTransitive/uap10.0/MSTest.TestAdapter.props")
            ?? throw new AssertFailedException($"Package '{packagePath}' does not contain the classic UWP props.");
        using var reader = new StreamReader(propsEntry.Open());
        string props = reader.ReadToEnd();

        Assert.Contains("031F8871-2660-4208-8F6B-FC142B40ABFF", props, propsEntry.FullName);
        Assert.Contains(
            "Microsoft.VisualStudio.TestTools.UnitTesting.TestingPlatformBuilderHook",
            props,
            propsEntry.FullName);
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Windows, IgnoreMessage = "Windows application-model package assets are produced only by Windows packs.")]
    public void PackedMSTestTestFramework_ContainsRequiredWindowsApplicationModelAssets()
        => AssertPackageContainsAllEntries(
            GetExactCurrentPackagePath("MSTest.TestFramework"),
            RequiredTestFrameworkEntries);

    [TestMethod]
    public void PackedIntegration_AppModelControllerContainsCompleteMtpOnlyRuntime()
    {
        string packagePath = GetExactCurrentPackagePath(
            "Microsoft.Testing.Extensions.PackagedApp.MSBuild",
            AcceptanceTestBase.MicrosoftTestingPlatformVersion);
        using ZipArchive archive = ZipFile.OpenRead(packagePath);
        string[] entries = archive.Entries
            .Select(entry => entry.FullName)
            .Where(entry => entry.StartsWith("tools/AppModelController/", StringComparison.OrdinalIgnoreCase))
            .ToArray();
        Assert.IsEmpty(
            entries.Where(entry => entry.Contains('\\') || entry.Contains("//", StringComparison.Ordinal)).ToArray(),
            "Controller ZIP entries must use canonical single-slash paths on every producer host.");

        string[] targetFrameworks = ["net8.0", "net9.0"];
        foreach (string targetFramework in targetFrameworks)
        {
            Assert.Contains($"tools/AppModelController/{targetFramework}/mstest-appmodel-controller.exe", entries);
            Assert.Contains($"tools/AppModelController/{targetFramework}/mstest-appmodel-controller.dll", entries);
            Assert.Contains($"tools/AppModelController/{targetFramework}/mstest-appmodel-controller.deps.json", entries);
            Assert.Contains($"tools/AppModelController/{targetFramework}/mstest-appmodel-controller.runtimeconfig.json", entries);
            Assert.Contains($"tools/AppModelController/{targetFramework}/Microsoft.Testing.Platform.dll", entries);
            Assert.Contains($"tools/AppModelController/{targetFramework}/Microsoft.Testing.Extensions.PackagedApp.dll", entries);
            Assert.Contains($"tools/AppModelController/{targetFramework}/fr/Microsoft.Testing.Extensions.PackagedApp.resources.dll", entries);
            Assert.DoesNotContain($"tools/AppModelController/{targetFramework}/mstest-appmodel-controller", entries);

            ZipArchiveEntry depsEntry = archive.GetEntry($"tools/AppModelController/{targetFramework}/mstest-appmodel-controller.deps.json")!;
            using var reader = new StreamReader(depsEntry.Open());
            using var deps = System.Text.Json.JsonDocument.Parse(reader.ReadToEnd());
            string runtimeTarget = deps.RootElement.GetProperty("runtimeTarget").GetProperty("name").GetString()!;
            foreach (System.Text.Json.JsonProperty library in deps.RootElement.GetProperty("targets").GetProperty(runtimeTarget).EnumerateObject())
            {
                foreach (System.Text.Json.JsonProperty assetGroup in library.Value.EnumerateObject().Where(property => property.Name is "runtime" or "native" or "runtimeTargets" or "resources"))
                {
                    foreach (System.Text.Json.JsonProperty asset in assetGroup.Value.EnumerateObject())
                    {
                        string relativePath = assetGroup.Name switch
                        {
                            "resources" => $"{asset.Value.GetProperty("locale").GetString()}/{Path.GetFileName(asset.Name)}",
                            "runtimeTargets" => asset.Name,
                            _ => Path.GetFileName(asset.Name),
                        };
                        Assert.Contains($"tools/AppModelController/{targetFramework}/{relativePath}", entries, $"Missing runtime asset for {library.Name}.");
                    }
                }
            }
        }

        string[] forbiddenEntries = entries
            .Where(entry =>
                entry.Contains("Microsoft.NET.Test.Sdk", StringComparison.OrdinalIgnoreCase)
                || entry.Contains("MSTest.TestAdapter", StringComparison.OrdinalIgnoreCase)
                || entry.Contains("MSTest.TestFramework", StringComparison.OrdinalIgnoreCase)
                || entry.Contains("vstest", StringComparison.OrdinalIgnoreCase)
                || entry.Contains("UwpTestHostRuntimeProvider", StringComparison.OrdinalIgnoreCase))
            .ToArray();
        Assert.IsEmpty(
            forbiddenEntries,
            $"The MTP app-model controller must not carry MSTest or VSTest runtime/deployment assets:{Environment.NewLine}" +
            string.Join(Environment.NewLine, forbiddenEntries));
    }

    [TestMethod]
    public void PackedMSTestSdk_DelegatesToIntegrationWithoutDuplicatePayload()
    {
        using ZipArchive archive = ZipFile.OpenRead(GetExactCurrentPackagePath("MSTest.Sdk"));
        Assert.DoesNotContain(entry => entry.FullName.StartsWith("tools/", StringComparison.OrdinalIgnoreCase), archive.Entries);
        Assert.IsNull(archive.GetEntry("Sdk/Runner/ClassicUwpMtpBootstrap.cs"));
        using var reader = new StreamReader(archive.GetEntry("Sdk/Runner/Common.targets")!.Open());
        Assert.Contains("Microsoft.Testing.Extensions.PackagedApp.MSBuild", reader.ReadToEnd());
    }

    [TestMethod]
    public void PackedMSTestAdapter_DoesNotImplicitlyDistributeWindowsController()
    {
        using ZipArchive archive = ZipFile.OpenRead(GetExactCurrentPackagePath("MSTest.TestAdapter"));
        Assert.DoesNotContain(entry => entry.FullName.StartsWith("tools/", StringComparison.OrdinalIgnoreCase), archive.Entries);
        using var reader = new StreamReader(archive.GetEntry("MSTest.TestAdapter.nuspec")!.Open());
        var nuspec = XDocument.Parse(reader.ReadToEnd());
        Assert.DoesNotContain(
            element => (string?)element.Attribute("id") == "Microsoft.Testing.Extensions.PackagedApp.MSBuild",
            nuspec.Descendants().Where(element => element.Name.LocalName == "dependency"));
    }

    [TestMethod]
    public void PackedIntegration_SharesBuildAssetsAndPinsVersionAlignedDependencies()
    {
        string version = AcceptanceTestBase.MicrosoftTestingPlatformVersion;
        using ZipArchive archive = ZipFile.OpenRead(GetExactCurrentPackagePath("Microsoft.Testing.Extensions.PackagedApp.MSBuild", version));
        foreach (string folder in new[] { "build", "buildTransitive", "buildMultiTargeting" })
        {
            Assert.IsNotNull(archive.GetEntry($"{folder}/Microsoft.Testing.Extensions.PackagedApp.MSBuild.targets"));
        }

        Assert.IsNotNull(archive.GetEntry("buildTransitive/ClassicUwpMtpBootstrap.cs"));
        Assert.IsNotNull(archive.GetEntry("PACKAGE.md"));
        using var reader = new StreamReader(archive.GetEntry("Microsoft.Testing.Extensions.PackagedApp.MSBuild.nuspec")!.Open());
        var nuspec = XDocument.Parse(reader.ReadToEnd());
        XElement[] groups = nuspec.Descendants().Where(element => element.Name.LocalName == "group").ToArray();
        XElement classicGroup = groups.Single(group => (string?)group.Attribute("targetFramework") == ".NETStandard2.0");
        Assert.HasCount(1, classicGroup.Elements());
        Assert.AreEqual("Microsoft.Testing.Platform.MSBuild", (string?)classicGroup.Elements().Single().Attribute("id"));
        foreach (XElement dependency in groups.SelectMany(group => group.Elements()))
        {
            Assert.AreEqual($"[{version}]", (string?)dependency.Attribute("version"));
        }
    }

    private static string GetExactCurrentPackagePath(string packageId, string? version = null)
    {
        string expectedVersion = version ?? AcceptanceTestBase.MSTestVersion;
        string[] matches = Directory.Exists(Constants.ArtifactsPackagesShipping)
            ? Directory.GetFiles(Constants.ArtifactsPackagesShipping, $"{packageId}.*.nupkg", SearchOption.TopDirectoryOnly)
                .Where(path => char.IsDigit(Path.GetFileName(path)[packageId.Length + 1])).ToArray()
            : [];
        string expectedPath = Path.Combine(
            Constants.ArtifactsPackagesShipping,
            $"{packageId}.{expectedVersion}.nupkg");

        Assert.HasCount(
            1,
            matches,
            $"Expected exactly one configuration-matched '{packageId}' package in '{Constants.ArtifactsPackagesShipping}', but found {matches.Length}:" +
            $"{Environment.NewLine}{string.Join(Environment.NewLine, matches.Select(Path.GetFileName))}" +
            $"{Environment.NewLine}Run '.\\build.cmd -pack' for {Constants.BuildConfiguration}; stale packages must not be selected by timestamp.");
        Assert.AreEqual(
            expectedPath,
            matches[0],
            ignoreCase: true,
            $"The only '{packageId}' package does not match the exact locally packed MSTest version '{expectedVersion}'.");
        Assert.IsTrue(File.Exists(expectedPath), $"The expected current packed package '{expectedPath}' does not exist.");
        return expectedPath;
    }

    private static void AssertPackageContainsAllEntries(string packagePath, IEnumerable<string> expectedEntries)
    {
        using ZipArchive archive = ZipFile.OpenRead(packagePath);
        var actualEntries = archive.Entries
            .Select(entry => entry.FullName.Replace('\\', '/'))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        string[] missing = expectedEntries.Where(path => !actualEntries.Contains(path)).ToArray();

        Assert.IsEmpty(
            missing,
            $"Package '{packagePath}' is missing required Windows application-model assets:{Environment.NewLine}" +
            string.Join(Environment.NewLine, missing));
    }
}
