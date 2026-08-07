// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.IO.Compression;

using Microsoft.Testing.Platform.Acceptance.IntegrationTests;

namespace MSTest.Acceptance.IntegrationTests;

/// <summary>
/// Verifies the physical Windows application-model assets in the configuration-matched MSTest packages.
/// </summary>
[TestClass]
[OSCondition(OperatingSystems.Windows, IgnoreMessage = "Windows application-model package assets are produced only by Windows packs.")]
public sealed class WindowsApplicationModelPackageTests
{
    private static readonly string[] RequiredTestAdapterEntries =
    [
        // Classic UWP.
        "build/uap10.0/MSTest.TestAdapter.props",
        "build/uap10.0/MSTest.TestAdapter.targets",
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
    public void PackedMSTestTestAdapter_ContainsRequiredWindowsApplicationModelAssets()
        => AssertPackageContainsAllEntries(
            GetExactCurrentPackagePath("MSTest.TestAdapter"),
            RequiredTestAdapterEntries);

    [TestMethod]
    public void PackedMSTestTestFramework_ContainsRequiredWindowsApplicationModelAssets()
        => AssertPackageContainsAllEntries(
            GetExactCurrentPackagePath("MSTest.TestFramework"),
            RequiredTestFrameworkEntries);

    private static string GetExactCurrentPackagePath(string packageId)
    {
        string expectedVersion = AcceptanceTestBase.MSTestVersion;
        string[] matches = Directory.Exists(Constants.ArtifactsPackagesShipping)
            ? Directory.GetFiles(Constants.ArtifactsPackagesShipping, $"{packageId}.*.nupkg", SearchOption.TopDirectoryOnly)
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
