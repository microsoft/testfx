// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Concurrent;

using Microsoft.Testing.Internal.Framework;

namespace Microsoft.Testing.TestInfrastructure;

public abstract class TestAssetFixtureBase : IDisposable, IAsyncInitializable
{
    private readonly ConcurrentDictionary<string /* asset ID */, TestAsset> _testAssets = new();
    private readonly TempDirectory _nugetGlobalPackagesDirectory;
    private bool _disposedValue;

    protected TestAssetFixtureBase(TempDirectory nugetGlobalPackagesDirectory)
    {
        _nugetGlobalPackagesDirectory = nugetGlobalPackagesDirectory;
    }

    public string GetAssetPath(string assetID)
        => !_testAssets.TryGetValue(assetID, out var testAsset)
            ? throw new ArgumentNullException(nameof(assetID), $"Cannot find target path for test asset '{assetID}'")
            : testAsset.TargetAssetPath;

    public async Task InitializeAsync(InitializationContext context)
#if NET
        => await Parallel.ForEachAsync(GetAssetsToGenerate(), async (asset, _) =>
        {
            var testAsset = await TestAsset.GenerateAssetAsync(asset.Name, asset.Code);
            var result = await DotnetCli.RunAsync($"build -m:1 -nodeReuse:false {testAsset.TargetAssetPath} -c Release", _nugetGlobalPackagesDirectory.Path);
            testAsset.DotnetResult = result;
            _testAssets.TryAdd(asset.ID, testAsset);
        });
#else
        => await Task.WhenAll(GetAssetsToGenerate().Select(async asset =>
        {
            var testAsset = await TestAsset.GenerateAssetAsync(asset.Name, asset.Code);
            var result = await DotnetCli.RunAsync($"build -m:1 -nodeReuse:false {testAsset.TargetAssetPath} -c Release", _nugetGlobalPackagesDirectory.Path);
            testAsset.DotnetResult = result;
            _testAssets.TryAdd(asset.ID, testAsset);
        }));
#endif

    public abstract IEnumerable<(string ID, string Name, string Code)> GetAssetsToGenerate();

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposedValue)
        {
            if (disposing)
            {
                Parallel.ForEach(_testAssets, (assetPair, _) => assetPair.Value.Dispose());
            }

            _disposedValue = true;
        }
    }

    public void Dispose()
    {
        // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }
}
