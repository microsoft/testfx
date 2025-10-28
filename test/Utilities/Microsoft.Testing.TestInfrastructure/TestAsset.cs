// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.TestInfrastructure;

public class TestAsset : IDisposable
{
    private const string FileTag = "#file";

    private readonly TempDirectory? _tempDirectory;
    private readonly string _assetCode;

    private bool _isDisposed;

    public TestAsset(string assetId, string assetCode, TempDirectory tempDirectory)
    {
        AssetId = assetId;
        _assetCode = assetCode;
        TargetAssetPath = Path.Combine(tempDirectory.Path, assetId);

        if (Directory.Exists(TargetAssetPath))
        {
            throw new InvalidOperationException($"Directory / asset id '{assetId}' in '{tempDirectory.Path}' already exists. Make sure the paths for your test assets are unique. Typically you need to look into GetAssetsToGenerate method on the test fixture that is used for running the failing test");
        }

        tempDirectory.CreateDirectory(assetId);
    }

    public TestAsset(string assetName, string assetCode)
        : this(assetName, assetCode, new TempDirectory(subDirectory: null))
    {
        AssetId = assetName;
        _assetCode = assetCode;
        // Assign temp directory because we own it.
        _tempDirectory = new TempDirectory(assetName);
        TargetAssetPath = _tempDirectory.Path;
    }

    public string AssetId { get; }

    public string TargetAssetPath { get; }

    public DotnetMuxerResult? DotnetResult { get; internal set; }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (_isDisposed)
        {
            return;
        }

        if (disposing)
        {
            if (DotnetResult is null || DotnetResult.ExitCode == 0)
            {
                _tempDirectory?.Dispose();
            }
        }

        _isDisposed = true;
    }

    private static (string Name, string Content) ParseFile(string fileContent)
    {
        int fileNameEndIndex = fileContent.Replace("\r\n", "\n").IndexOf("\n", StringComparison.InvariantCulture);
        if (fileNameEndIndex < 0)
        {
            return (string.Empty, string.Empty);
        }

        string name = fileContent[..fileNameEndIndex].Trim();
        string content = fileContent.Remove(0, fileNameEndIndex).TrimStart('\r', '\n');
        return (name, content);
    }

    public static async Task<TestAsset> GenerateAssetAsync(string assetName, string code, bool addDefaultNuGetConfigFile = true, bool addPublicFeeds = false)
    {
        TestAsset testAsset = new(assetName, addDefaultNuGetConfigFile ? string.Concat(code, GetNuGetConfig(addPublicFeeds)) : code);
        string[] splitFiles = testAsset._assetCode.Split([FileTag], StringSplitOptions.RemoveEmptyEntries);
        foreach (string fileContent in splitFiles)
        {
            (string, string) fileInfo = ParseFile(fileContent);
            await TempDirectory.WriteFileAsync(testAsset.TargetAssetPath, fileInfo.Item1, fileInfo.Item2);
        }

        return testAsset;
    }

    public static async Task<TestAsset> GenerateAssetAsync(string assetName, string code, TempDirectory tempDirectory, bool addDefaultNuGetConfigFile = true, bool addPublicFeeds = false)
    {
        TestAsset testAsset = new(assetName, addDefaultNuGetConfigFile ? string.Concat(code, GetNuGetConfig(addPublicFeeds)) : code, tempDirectory);
        string[] splitFiles = testAsset._assetCode.Split([FileTag], StringSplitOptions.RemoveEmptyEntries);
        foreach (string fileContent in splitFiles)
        {
            (string, string) fileInfo = ParseFile(fileContent);
            await TempDirectory.WriteFileAsync(testAsset.TargetAssetPath, fileInfo.Item1, fileInfo.Item2);
        }

        return testAsset;
    }

    public static string GetNuGetConfig(bool addPublicFeeds = false, bool addHashFileHeader = true)
    {
        string publicFeedsFragment = addPublicFeeds
            ? """
                <add key="test-tools" value="https://pkgs.dev.azure.com/dnceng/public/_packaging/test-tools/nuget/v3/index.json" />
            """
            : string.Empty;

        string publicFeedsMapping = addPublicFeeds
            ? """
            <packageSource key="test-tools">
                <package pattern="*" />
            </packageSource>
            """
            : string.Empty;

        string defaultNuGetConfig = $"""
<?xml version="1.0" encoding="utf-8"?>
<configuration>
    <packageSources>
        <clear/>
        {publicFeedsFragment}
        <add key="local-nonshipping" value="{Constants.ArtifactsPackagesNonShipping}" />
        <add key="local-shipping" value="{Constants.ArtifactsPackagesShipping}" />
        <add key="local-tmp-packages" value="{Constants.ArtifactsTmpPackages}" />
        <add key="dotnet-public" value="https://pkgs.dev.azure.com/dnceng/public/_packaging/dotnet-public/nuget/v3/index.json" />
        <!-- This feed is required for FSharp.Core until preview 1 or 2 is released -->
        <add key="dotnet10" value="https://pkgs.dev.azure.com/dnceng/public/_packaging/dotnet10/nuget/v3/index.json" />
    </packageSources>
    <config>
        <add key="globalPackagesFolder" value=".packages" />
    </config>
    <packageSourceMapping>
        {publicFeedsMapping}
        <packageSource key="local-nonshipping">
            <package pattern="*" />
        </packageSource>
        <packageSource key="local-shipping">
            <package pattern="*" />
        </packageSource>
        <packageSource key="local-tmp-packages">
            <package pattern="*" />
        </packageSource>
        <packageSource key="dotnet-public">
            <package pattern="*" />
        </packageSource>
        <packageSource key="dotnet10">
            <package pattern="*" />
        </packageSource>
    </packageSourceMapping>
</configuration>

""";

        return addHashFileHeader
            ? "#file NuGet.config\n" + defaultNuGetConfig
            : defaultNuGetConfig;
    }
}
