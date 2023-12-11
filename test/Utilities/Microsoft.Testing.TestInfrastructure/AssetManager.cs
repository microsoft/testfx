// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.TestInfrastructure;

public class AssetManager : IDisposable
{
    private static readonly string TestAssetsPath = Path.Combine("test", "TestAssets");

    private readonly TempDirectory _tempDirectory;
    private readonly string _testAssetsPath;
    private bool _isDisposed;

    public AssetManager(string sourceAsset, bool cleanup = true)
        : this(sourceAsset, sourceAsset, cleanup)
    {
    }

    public AssetManager(string sourceAsset, string targetAsset, bool cleanup = true)
        : this(sourceAsset, targetAsset, Path.Combine(RootFinder.Find(), TestAssetsPath), cleanup)
    {
    }

    public AssetManager(
        string sourceAsset,
        string targetAsset,
        string testAssetPath,
        bool cleanup = true)
    {
        SourceAsset = sourceAsset;
        TargetAsset = targetAsset;
        _testAssetsPath = testAssetPath;

        _tempDirectory = new(targetAsset, cleanup: cleanup);
    }

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
            _tempDirectory.Dispose();
        }

        _isDisposed = true;
    }

    public async Task CopyAssets()
    {
        string sourceAssetPath = Path.Combine(_testAssetsPath, SourceAsset);
        await _tempDirectory.CopyDirectoryAsync(sourceAssetPath, _tempDirectory.DirectoryPath);
    }

    public string SourceAsset { get; }

    public string TargetAsset { get; }

    public string TargetAssetPath => _tempDirectory.DirectoryPath;
}
