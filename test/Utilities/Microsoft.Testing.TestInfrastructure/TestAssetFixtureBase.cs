// Copyright (c) Microsoft Corporation. All rights reserved.
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
    private readonly TempDirectory _tempDirectory = new();
    private readonly TempDirectory _nugetGlobalPackagesDirectory;
    private bool _disposedValue;

    protected TestAssetFixtureBase(TempDirectory nugetGlobalPackagesDirectory)
        => _nugetGlobalPackagesDirectory = nugetGlobalPackagesDirectory;

    public string GetAssetPath(string assetID)
        => !_testAssets.TryGetValue(assetID, out TestAsset? testAsset)
            ? throw new ArgumentNullException(nameof(assetID), $"Cannot find target path for test asset '{assetID}'")
            : testAsset.TargetAssetPath;

    public async Task InitializeAsync()
    {
        // Generate all distinct projects into the same temporary base folder. Do this in series
        // because the work here is minimal and it gives us ability to refer from one project to another.
        IEnumerable<(string ID, string Name, string Code)> assets = GetAssetsToGenerate();
        foreach ((string id, string name, string code) in assets)
        {
            TestAsset generatedAsset = await TestAsset.GenerateAssetAsync(name, code, _tempDirectory);
            _testAssets.TryAdd(id, generatedAsset);
        }

#if NET
        await Parallel.ForEachAsync(_testAssets.Values, async (testAsset, _) =>
        {
            DotnetMuxerResult result = await DotnetCli.RunAsync($"build {testAsset.TargetAssetPath} -c Release", _nugetGlobalPackagesDirectory.Path, callerMemberName: testAsset.TargetAssetPath);
            testAsset.DotnetResult = result;
        });
#else
        await Task.WhenAll(_testAssets.Values.Select(async testAsset =>
        {
            DotnetMuxerResult result = await DotnetCli.RunAsync($"build {testAsset.TargetAssetPath} -c Release", _nugetGlobalPackagesDirectory.Path, callerMemberName: testAsset.Name);
            testAsset.DotnetResult = result;
        }));
#endif
    }

    public abstract IEnumerable<(string ID, string Name, string Code)> GetAssetsToGenerate();

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposedValue)
        {
            if (disposing)
            {
                Parallel.ForEach(_testAssets, (assetPair, _) => assetPair.Value.Dispose());
                _tempDirectory.Dispose();
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
