// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.TestInfrastructure;

public interface ITestAssetFixture : IDisposable
{
    Task InitializeAsync(CancellationToken cancellationToken);
}

public sealed class NopAssetFixture : ITestAssetFixture
{
    public Task InitializeAsync(CancellationToken cancellationToken) => Task.CompletedTask;

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

    public async Task InitializeAsync(CancellationToken cancellationToken) =>
        // Generate all projects into the same temporary base folder, but separate subdirectories, so we can reference one from other.
#if NET
        await Parallel.ForEachAsync(GetAssetsToGenerate(), async (asset, _) =>
        {
            TestAsset testAsset = await TestAsset.GenerateAssetAsync(asset.ID, asset.Code, _tempDirectory);
            DotnetMuxerResult result = await DotnetCli.RunAsync($"build {testAsset.TargetAssetPath} -c Release", _nugetGlobalPackagesDirectory.Path, callerMemberName: asset.Name, cancellationToken: cancellationToken);
            testAsset.DotnetResult = result;
            _testAssets.TryAdd(asset.ID, testAsset);
        });
#else
        await Task.WhenAll(GetAssetsToGenerate().Select(async asset =>
        {
            TestAsset testAsset = await TestAsset.GenerateAssetAsync(asset.Name, asset.Code, _tempDirectory);
            DotnetMuxerResult result = await DotnetCli.RunAsync($"build {testAsset.TargetAssetPath} -c Release", _nugetGlobalPackagesDirectory.Path, callerMemberName: asset.Name, cancellationToken: cancellationToken);
            testAsset.DotnetResult = result;
            _testAssets.TryAdd(asset.ID, testAsset);
        }));
#endif

    /// <summary>
    /// Returns a list test assets to generate. A test asset has an id, name and code. A test asset is typically a project and all its files. Like MyTests.csproj, Program.cs, runsettings.runsettings etc.
    /// The asset id determines the name of the sub-folder into which all those files will be placed, this id has to be unique within the collection returned by this method.
    /// The asset name, identifies the file that will be built within that folder, this name does not have to be unique, so you can re-use similar sources in multiple assets, e.g. when one option needs to change
    /// but rest of the project remains the same.
    /// Code is the code that is split into separate files on the #file comments in the code.
    /// </summary>
    /// <returns></returns>
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
