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

    // THROWAWAY SURVEY BRANCH — do not merge.
    // When this environment variable is truthy, every fixture (opt-out, ignoring SourceGenMetadataModes)
    // attempts to build these source-gen variants, and the outcome is RECORDED rather than asserted, so
    // a single CI run gauges how many acceptance assets cannot support source generation today.
    // Scoped to SourceGeneration for this first pass to bound CI cost; AOT is a planned second pass.
    private const string SurveyEnvironmentVariable = "MSTEST_ACCEPTANCE_SOURCEGEN_SURVEY";

    private static readonly MetadataMode[] SurveyModes = [MetadataMode.SourceGeneration];

    private static bool IsSurveyEnabled
    {
        get
        {
            string? value = Environment.GetEnvironmentVariable(SurveyEnvironmentVariable);
            return value is not null
                && (value == "1" || string.Equals(value, "true", StringComparison.OrdinalIgnoreCase));
        }
    }

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

        // THROWAWAY SURVEY BRANCH — do not merge.
        // Survey mode takes precedence over the normal opt-in path: build the survey modes for EVERY
        // fixture and record the outcome instead of asserting, so we can gauge source-gen support
        // across the whole acceptance suite from a single (green) CI run.
        if (IsSurveyEnabled)
        {
            await RunSourceGenSurveyAsync(testAsset, assetName, result, cancellationToken);
            return;
        }

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

    // THROWAWAY SURVEY BRANCH — do not merge.
    private async Task RunSourceGenSurveyAsync(TestAsset testAsset, string assetName, DotnetMuxerResult reflectionResult, CancellationToken cancellationToken)
    {
        // Build the union of the survey modes (measured for every fixture, opt-out) and any modes the
        // fixture already declares (so already-converted classes whose tests run in those modes still
        // find their bin/<sub> output and stay green). Everything is non-fatal in survey mode.
        MetadataMode[] modesToBuild = [.. SurveyModes.Concat(SourceGenMetadataModes).Distinct()];
        var results = new Dictionary<MetadataMode, DotnetMuxerResult?>();

        foreach (MetadataMode mode in modesToBuild)
        {
            // If even the plain reflection build failed, the asset is broken independently of source
            // generation; record a skip so it does not count as a source-gen failure.
            if (reflectionResult.ExitCode != 0)
            {
                results[mode] = null;
                continue;
            }

            try
            {
                string sourceGenArgs = await AcceptanceSourceGen.PrepareBuildArgumentsAsync(testAsset.TargetAssetPath, mode);
                results[mode] = await DotnetCli.RunAsync(
                    $"build {testAsset.TargetAssetPath} -c Release {sourceGenArgs}",
                    failIfReturnValueIsNotZero: false,
                    callerMemberName: $"{assetName}_{AcceptanceSourceGen.GetOutputSubFolder(mode)}_survey",
                    cancellationToken: cancellationToken);
            }
            catch (Exception ex)
            {
                // Never let the survey itself fail a test class; record and continue.
                SourceGenSurvey.Record(assetName, mode, result: null, skippedReason: $"survey-exception: {ex.GetType().Name}: {ex.Message}");
            }
        }

        // Record one survey line per measured (opt-out) mode.
        foreach (MetadataMode mode in SurveyModes)
        {
            results.TryGetValue(mode, out DotnetMuxerResult? modeResult);
            SourceGenSurvey.Record(
                assetName,
                mode,
                modeResult,
                skippedReason: reflectionResult.ExitCode != 0 ? "reflection-build-failed" : null);
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
