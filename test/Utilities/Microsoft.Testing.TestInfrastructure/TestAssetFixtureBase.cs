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
    /// Override to declare which source-gen metadata modes this fixture builds, in addition to the
    /// always-built <see cref="MetadataMode.Reflection"/> build. This is opt-in: the default is empty
    /// so an asset is only ever built under a source-gen mode once it has been validated to support
    /// it. A mode returned here is expected to build successfully; a failed build throws with the
    /// captured build output (see <see cref="InitializeAsync"/>).
    /// <para>
    /// Assets that cannot run under source generation (for example VSTest-host assets,
    /// NativeAOT/Trim/Aspire/Playwright/ServerMode assets, or assets that rely on source-generator
    /// gaps such as inherited <c>[TestClass]</c>, generic test methods, or cross-assembly reflection)
    /// simply leave this empty and keep building reflection-only.
    /// </para>
    /// </summary>
    protected virtual IReadOnlyList<MetadataMode> SourceGenMetadataModes => [];

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

        // Opt-in: for each source-gen metadata mode the fixture declares, build a second variant with
        // the matching generator injected, into an isolated bin/<sub> + obj/<sub> output, so the same
        // behavioral assertions also validate the source-generated metadata path. The build is run
        // with failIfReturnValueIsNotZero:false so we can surface the captured output if it fails
        // (rather than the less actionable default exception from DotnetCli.RunAsync).
        if (!AcceptanceSourceGen.IsGloballyDisabled)
        {
            foreach (MetadataMode mode in SourceGenMetadataModes)
            {
                string sourceGenArgs = await AcceptanceSourceGen.PrepareBuildArgumentsAsync(testAsset.TargetAssetPath, mode);
                DotnetMuxerResult sourceGenResult = await DotnetCli.RunAsync(
                    $"build {testAsset.TargetAssetPath} -c Release {sourceGenArgs}",
                    failIfReturnValueIsNotZero: false,
                    callerMemberName: $"{assetName}_{AcceptanceSourceGen.GetOutputSubFolder(mode)}",
                    cancellationToken: cancellationToken);

                if (sourceGenResult.ExitCode != 0)
                {
                    throw new InvalidOperationException(
                        $"The {mode} build of acceptance asset '{assetName}' failed with exit code {sourceGenResult.ExitCode}.{Environment.NewLine}{sourceGenResult}");
                }
            }
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
