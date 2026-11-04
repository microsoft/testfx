// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.Acceptance.IntegrationTests;

[TestClass]
public sealed class TestAssetTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task GenerateAssetAsync_WithoutDirectory_PreparesOnlyTheOwnedRoot()
    {
        string assetName = $"{nameof(GenerateAssetAsync_WithoutDirectory_PreparesOnlyTheOwnedRoot)}_{Guid.NewGuid():N}";
        using TestAsset testAsset = await TestAsset.GenerateAssetAsync(
            assetName,
            """
            #file Data.txt
            asset
            """);

        string[] preparedRoots = [.. Directory.EnumerateDirectories(TempDirectory.TestSuiteDirectory)
            .Where(root => Directory.Exists(Path.Combine(root, assetName)))];

        Assert.HasCount(1, preparedRoots);
        Assert.AreEqual(Path.GetDirectoryName(testAsset.TargetAssetPath), preparedRoots[0]);
        Assert.AreEqual(assetName, testAsset.AssetId);
        Assert.AreSequenceEqual(
            ["asset"],
            await File.ReadAllLinesAsync(Path.Combine(testAsset.TargetAssetPath, "Data.txt"), TestContext.CancellationToken));
        Assert.IsTrue(File.Exists(Path.Combine(testAsset.TargetAssetPath, "NuGet.config")));
    }

    [TestMethod]
    public async Task GenerateAssetAsync_WithSameName_KeepsAssetsIndependent()
    {
        string assetName = $"{nameof(GenerateAssetAsync_WithSameName_KeepsAssetsIndependent)}_{Guid.NewGuid():N}";
        using TestAsset first = await TestAsset.GenerateAssetAsync(
            assetName,
            """
            #file Data.txt
            first
            """,
            addDefaultNuGetConfigFile: false);
        using TestAsset second = await TestAsset.GenerateAssetAsync(
            assetName,
            """
            #file Data.txt
            second
            """,
            addDefaultNuGetConfigFile: false);

        Assert.AreNotEqual(first.TargetAssetPath, second.TargetAssetPath);
        Assert.AreSequenceEqual(
            ["first"],
            await File.ReadAllLinesAsync(Path.Combine(first.TargetAssetPath, "Data.txt"), TestContext.CancellationToken));
        Assert.AreSequenceEqual(
            ["second"],
            await File.ReadAllLinesAsync(Path.Combine(second.TargetAssetPath, "Data.txt"), TestContext.CancellationToken));
    }

    [TestMethod]
    public async Task Dispose_WithBorrowedDirectory_LeavesCallerOwnedFiles()
    {
        using TempDirectory tempDirectory = new();
        await TempDirectory.WriteFileAsync(tempDirectory.Path, "Caller.txt", "caller");
        using TestAsset testAsset = await TestAsset.GenerateAssetAsync(
            nameof(Dispose_WithBorrowedDirectory_LeavesCallerOwnedFiles),
            """
            #file Data.txt
            asset
            """,
            tempDirectory,
            addDefaultNuGetConfigFile: false);

        testAsset.Dispose();

        Assert.AreSequenceEqual(
            ["caller"],
            await File.ReadAllLinesAsync(Path.Combine(tempDirectory.Path, "Caller.txt"), TestContext.CancellationToken));
        Assert.AreSequenceEqual(
            ["asset"],
            await File.ReadAllLinesAsync(Path.Combine(testAsset.TargetAssetPath, "Data.txt"), TestContext.CancellationToken));
    }
}
