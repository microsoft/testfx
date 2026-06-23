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
    private bool _disposedValue;

    /// <summary>
    /// Gets a value indicating whether the <see cref="MetadataMode.SourceGeneration"/> build variant
    /// of the asset was produced. It is <see langword="false"/> when the fixture opts out via
    /// <see cref="SkipSourceGenVariant"/> or when the variant is globally disabled.
    /// </summary>
    public bool SourceGenVariantBuilt { get; private set; }

    /// <summary>
    /// Gets the result of the <see cref="MetadataMode.SourceGeneration"/> build, or <see langword="null"/>
    /// when the variant was not built. Useful for diagnostics when a source-gen build fails.
    /// </summary>
    public DotnetMuxerResult? SourceGenBuildResult { get; private set; }

    /// <summary>
    /// Override and return <see langword="true"/> to skip the <see cref="MetadataMode.SourceGeneration"/>
    /// build variant for assets that cannot run under source generation (for example VSTest-host assets,
    /// NativeAOT/Trim/Aspire/Playwright/ServerMode assets, or assets that rely on source-generator gaps
    /// such as inherited <c>[TestClass]</c>, generic test methods, or cross-assembly reflection).
    /// </summary>
    protected virtual bool SkipSourceGenVariant => false;

    public string GetAssetPath(string assetID)
        => !_testAssets.TryGetValue(assetID, out TestAsset? testAsset)
            ? throw new ArgumentNullException(nameof(assetID), $"Cannot find target path for test asset '{assetID}'")
            : testAsset.TargetAssetPath;

    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        (string assetId, string assetName, string assetCode) = GetAssetsToGenerate();
        TestAsset testAsset = await TestAsset.GenerateAssetAsync(assetId, assetCode, _tempDirectory);
        DotnetMuxerResult result = await DotnetCli.RunAsync($"build {testAsset.TargetAssetPath} -c Release", callerMemberName: assetName, cancellationToken: cancellationToken);
        testAsset.DotnetResult = result;
        _testAssets.TryAdd(assetId, testAsset);

        // Default-on: build a second variant with MSTest.SourceGeneration injected, into an isolated
        // bin/SourceGen + obj/SourceGen output, so the same behavioral assertions also validate the
        // source-generated metadata path. Only attempt it when the reflection build succeeded.
        if (!SkipSourceGenVariant && !AcceptanceSourceGen.IsGloballyDisabled && result.ExitCode == 0)
        {
            string sourceGenArgs = await AcceptanceSourceGen.PrepareBuildArgumentsAsync(testAsset.TargetAssetPath);
            DotnetMuxerResult sourceGenResult = await DotnetCli.RunAsync(
                $"build {testAsset.TargetAssetPath} -c Release {sourceGenArgs}",
                callerMemberName: $"{assetName}_SourceGen",
                cancellationToken: cancellationToken);
            SourceGenBuildResult = sourceGenResult;
            SourceGenVariantBuilt = sourceGenResult.ExitCode == 0;
        }
    }

    /// <summary>
    /// Returns a test asset to generate. A test asset has an id, name and code. A test asset is typically a project and all its files. Like MyTests.csproj, Program.cs, runsettings.runsettings etc.
    /// The asset id determines the name of the sub-folder into which all those files will be placed.
    /// The asset name, identifies the file that will be built within that folder, this name does not have to be unique, so you can re-use similar sources in multiple assets, e.g. when one option needs to change
    /// but rest of the project remains the same.
    /// Code is the code that is split into separate files on the #file comments in the code.
    /// </summary>
    /// <returns></returns>
    public abstract (string ID, string Name, string Code) GetAssetsToGenerate();

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
