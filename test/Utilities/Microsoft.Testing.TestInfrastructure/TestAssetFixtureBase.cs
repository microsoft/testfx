﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.TestInfrastructure;

public interface ITestAssetFixture : IDisposable
{
    Task InitializeAsync();
}

public sealed class NopAssetFixture : ITestAssetFixture
{
    public Task InitializeAsync() => Task.CompletedTask;

    public void Dispose()
    {
    }
}

public abstract class TestAssetFixtureBase : ITestAssetFixture
{
    private readonly ConcurrentDictionary<string /* asset ID */, TestAsset> _testAssets = new();
    private readonly TempDirectory _nugetGlobalPackagesDirectory;
    private bool _disposedValue;

    protected TestAssetFixtureBase(TempDirectory nugetGlobalPackagesDirectory)
        => _nugetGlobalPackagesDirectory = nugetGlobalPackagesDirectory;

    public string GetAssetPath(string assetID)
        => !_testAssets.TryGetValue(assetID, out TestAsset? testAsset)
            ? throw new ArgumentNullException(nameof(assetID), $"Cannot find target path for test asset '{assetID}'")
            : testAsset.TargetAssetPath;

    public async Task InitializeAsync()
#if NET
        => await Parallel.ForEachAsync(GetAssetsToGenerate(), async (asset, _) =>
        {
            TestAsset testAsset = await TestAsset.GenerateAssetAsync(asset.Name, asset.Code);
            DotnetMuxerResult result = await DotnetCli.RunAsync($"build {testAsset.TargetAssetPath} -c Release", _nugetGlobalPackagesDirectory.Path, callerMemberName: asset.Name);
            testAsset.DotnetResult = result;
            _testAssets.TryAdd(asset.ID, testAsset);
        });
#else
        => await Task.WhenAll(GetAssetsToGenerate().Select(async asset =>
        {
            TestAsset testAsset = await TestAsset.GenerateAssetAsync(asset.Name, asset.Code);
            DotnetMuxerResult result = await DotnetCli.RunAsync($"build {testAsset.TargetAssetPath} -c Release", _nugetGlobalPackagesDirectory.Path);
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
