// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Security.Cryptography;
using System.Text;

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
    private const string CacheModeEnvironmentVariable = "TESTFX_ACCEPTANCE_MSBUILD_CACHE_MODE";
    private const string CacheRootEnvironmentVariable = "TESTFX_ACCEPTANCE_MSBUILD_CACHE_ROOT";
    private const string CacheLogRootEnvironmentVariable = "TESTFX_ACCEPTANCE_MSBUILD_CACHE_LOG_ROOT";

    private static int s_cacheBinlogCounter;

    private readonly ConcurrentDictionary<string /* asset ID */, TestAsset> _testAssets = new();
    private TempDirectory? _tempDirectory;
    private bool _disposedValue;

    /// <summary>
    /// The metadata modes every acceptance asset is built under by default, in addition to the
    /// always-built <see cref="MetadataMode.Reflection"/> build. This is <b>opt-out</b>: a source-gen
    /// survey across the whole acceptance corpus showed every asset except <c>FrameworkOnlyTests</c>
    /// builds cleanly under <see cref="MetadataMode.SourceGeneration"/>, so it is on by default and a
    /// failing build throws (see <see cref="InitializeAsync"/>).
    /// <para>
    /// <see cref="MetadataMode.AotSourceGeneration"/> is intentionally not part of the default yet: it
    /// has not been validated across the whole corpus, so fixtures that want it (and run tests against
    /// it) opt in explicitly via <see cref="SourceGenMetadataModes"/>.
    /// </para>
    /// </summary>
    private static readonly IReadOnlyList<MetadataMode> DefaultSourceGenMetadataModes = [MetadataMode.SourceGeneration];

    /// <summary>
    /// Override to change which source-gen metadata modes this fixture builds, in addition to the
    /// always-built <see cref="MetadataMode.Reflection"/> build. Defaults to
    /// <see cref="DefaultSourceGenMetadataModes"/> (opt-out). A mode returned here is expected to build
    /// successfully; a failed build throws with the captured build output (see <see cref="InitializeAsync"/>).
    /// <para>
    /// Return an empty list to opt an asset out entirely — for assets that genuinely cannot build under
    /// source generation (for example <c>FrameworkOnlyTests</c>, which references only the test
    /// framework and not the adapter that carries the source-generated metadata hook). Note this only
    /// governs which variants are <i>built</i>; an asset's tests still run reflection-only unless the
    /// test methods are parameterized by <c>MetadataMode</c> and threaded through
    /// <c>TestHost.LocateFrom</c>.
    /// </para>
    /// </summary>
    protected virtual IReadOnlyList<MetadataMode> SourceGenMetadataModes => DefaultSourceGenMetadataModes;

    public string GetAssetPath(string assetID)
        => !_testAssets.TryGetValue(assetID, out TestAsset? testAsset)
            ? throw new ArgumentNullException(nameof(assetID), $"Cannot find target path for test asset '{assetID}'")
            : testAsset.TargetAssetPath;

    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        (string assetId, string assetName, string assetCode) = GetAssetsToGenerate();
        CacheConfiguration? cacheConfiguration = GetCacheConfiguration(assetId, assetCode);
        _tempDirectory = cacheConfiguration is null
            ? new TempDirectory()
            : TempDirectory.CreateStable(cacheConfiguration.AssetKey);

        TestAsset testAsset = await TestAsset.GenerateAssetAsync(assetId, assetCode, _tempDirectory);
        DotnetMuxerResult result = await BuildAssetAsync(
            testAsset,
            assetName,
            extraBuildArguments: string.Empty,
            cacheConfiguration,
            cacheVariant: "Reflection",
            cancellationToken);
        testAsset.DotnetResult = result;
        _testAssets.TryAdd(assetId, testAsset);

        // For each source-gen metadata mode the fixture builds (opt-out: see SourceGenMetadataModes,
        // which defaults to SourceGeneration), build a variant with the matching generator injected,
        // into an isolated bin/<sub> + obj/<sub> output, so the source-generated metadata path is at
        // least compiled (and, for parameterized fixtures, exercised). The build is run with
        // failIfReturnValueIsNotZero:false so we can surface the captured output if it fails (rather
        // than the less actionable default exception from DotnetCli.RunAsync).
        if (!AcceptanceSourceGen.IsGloballyDisabled)
        {
            foreach (MetadataMode mode in SourceGenMetadataModes)
            {
                string sourceGenArgs = await AcceptanceSourceGen.PrepareBuildArgumentsAsync(testAsset.TargetAssetPath, mode);
                string outputSubFolder = AcceptanceSourceGen.GetOutputSubFolder(mode);
                DotnetMuxerResult sourceGenResult = await BuildAssetAsync(
                    testAsset,
                    $"{assetName}_{outputSubFolder}",
                    sourceGenArgs,
                    cacheConfiguration,
                    outputSubFolder,
                    cancellationToken,
                    failIfReturnValueIsNotZero: false);

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
                _tempDirectory?.Dispose();
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

    private async Task<DotnetMuxerResult> BuildAssetAsync(
        TestAsset testAsset,
        string binlogBaseFileName,
        string extraBuildArguments,
        CacheConfiguration? cacheConfiguration,
        string cacheVariant,
        CancellationToken cancellationToken,
        bool failIfReturnValueIsNotZero = true)
    {
        string dotnetBuildArguments =
            $"build {testAsset.TargetAssetPath} -c Release "
            + "-p:MSBuildTreatWarningsAsErrors=true -p:TreatWarningsAsErrors=true "
            + extraBuildArguments;
        if (cacheConfiguration is null)
        {
            return await DotnetCli.RunAsync(
                dotnetBuildArguments,
                failIfReturnValueIsNotZero: failIfReturnValueIsNotZero,
                // Warning promotion is supplied explicitly in dotnetBuildArguments and by
                // AcceptanceSourceGen for source-generated builds.
                warnAsError: false,
                callerMemberName: binlogBaseFileName,
                cancellationToken: cancellationToken);
        }

        string projectPath = ResolveEntryProject(testAsset.TargetAssetPath, testAsset.AssetId);
        int cacheBuildId = Interlocked.Increment(ref s_cacheBinlogCounter);
        string binlogPath = Path.Combine(
            TempDirectory.TestSuiteDirectory,
            $"{binlogBaseFileName}-{cacheBuildId}.binlog");
        string localCacheRoot = Path.Combine(
            cacheConfiguration.CacheRoot,
            "Content",
            cacheConfiguration.AssetKey,
            cacheVariant);
        string logDirectory = Path.Combine(
            cacheConfiguration.LogRoot,
            cacheConfiguration.AssetKey,
            cacheVariant);
        string readOnly = cacheConfiguration.Mode == "read" ? "true" : "false";
        string msbuildScript = Path.Combine(TempDirectory.RepoRoot, "eng", "common", "msbuild.ps1");
        string msbuildExtraBuildArguments = extraBuildArguments.Replace("-p:", "/p:", StringComparison.Ordinal);
        string commandLine =
            $"pwsh -NoLogo -NoProfile -File \"{msbuildScript}\" "
            + "-warnAsError:$false -nodeReuse:$false "
            + $"\"{projectPath}\" /restore /graph /m:1 /reportfileaccesses /nr:false /t:Build /v:minimal "
            + $"/bl:\"{binlogPath}\" /p:Configuration=Release "
            + "/p:MSBuildTreatWarningsAsErrors=true /p:TreatWarningsAsErrors=true "
            + "/p:SuppressNETCoreSdkPreviewMessage=true "
            + "/p:MSBuildCachePackageEnabled=true /p:MSBuildCacheEnabled=true "
            + $"/p:MSBuildCacheCacheUniverse=testfx-acceptance-v1-{Constants.BuildConfiguration} "
            + $"/p:MSBuildCacheLocalCacheRootPath=\"{localCacheRoot}\" "
            + $"/p:MSBuildCacheLogDirectory=\"{logDirectory}\" "
            + $"/p:MSBuildCacheRemoteCacheIsReadOnly={readOnly} "
            + "/p:MSBuildCacheIdenticalDuplicateOutputPatterns=\\** "
            + msbuildExtraBuildArguments;

        using CommandLine cacheBuild = new();
        Dictionary<string, string?> environmentVariables = DotnetCli.CreateEnvironmentVariables();
        // AcceptanceFixture intentionally randomizes its in-repo package folder so repeated local packs
        // cannot reuse stale same-version packages. That random path would become part of every cache
        // fingerprint. Cache builds use an isolated package root outside the checkout instead; package
        // contents remain fingerprinted and the cache normalizes this root across agents.
        environmentVariables["NUGET_PACKAGES"] = Path.Combine(cacheConfiguration.CacheRoot, "NuGetPackages");
        int exitCode;
        {
            using DotnetCli.CommandSlot commandSlot = await DotnetCli.AcquireCommandSlotAsync(cancellationToken);
            exitCode = await cacheBuild.RunAsyncAndReturnExitCodeAsync(
                commandLine,
                environmentVariables,
                workingDirectory: testAsset.TargetAssetPath,
                cleanDefaultEnvironmentVariableIfCustomAreProvided: true,
                cancellationToken: cancellationToken);
        }

        if (exitCode == 0)
        {
            ReportCacheStatistics(
                testAsset.AssetId,
                cacheVariant,
                cacheConfiguration,
                cacheBuildId,
                [.. cacheBuild.StandardOutputLines, .. cacheBuild.ErrorOutputLines]);

            if (cacheVariant == "Reflection")
            {
                // The cache provider's build assets are recorded in project.assets.json during the
                // cache-enabled restore. Nested `dotnet test --no-build` commands would otherwise
                // load ProjectCachePlugin without graph/file-access settings and fail while computing
                // run arguments. Rewrite only the normal restore state without the cache references;
                // compiled bin outputs remain unchanged and source-gen uses its isolated obj folder.
                await DotnetCli.RunAsync(
                    $"restore \"{projectPath}\"",
                    environmentVariables: new() { ["NUGET_PACKAGES"] = environmentVariables["NUGET_PACKAGES"] },
                    warnAsError: false,
                    callerMemberName: $"{binlogBaseFileName}_PrepareExecution",
                    cancellationToken: cancellationToken);
            }

            return new DotnetMuxerResult(
                commandLine,
                exitCode,
                cacheBuild.StandardOutput,
                cacheBuild.StandardOutputLines,
                cacheBuild.ErrorOutput,
                cacheBuild.ErrorOutputLines,
                binlogPath);
        }

        // Cache authentication, transport, or plugin failures must not make acceptance validation less
        // reliable. Re-run through the established dotnet build path; a real source failure is then
        // reported exactly as it was before acceptance caching was enabled.
        Console.WriteLine(
            $"The cached {cacheVariant} build of acceptance asset '{testAsset.AssetId}' failed with exit code {exitCode}; "
            + $"falling back to dotnet build.{Environment.NewLine}"
            + $"StandardOutput:{Environment.NewLine}{cacheBuild.StandardOutput}{Environment.NewLine}"
            + $"StandardError:{Environment.NewLine}{cacheBuild.ErrorOutput}");
        WriteCacheSummary(
            testAsset.AssetId,
            cacheVariant,
            cacheConfiguration,
            cacheBuildId,
            outcome: "Fallback",
            hasStatistics: false,
            hitCount: 0,
            missCount: 0,
            savedSeconds: 0);
        CleanCacheOutputs(testAsset.TargetAssetPath, cacheVariant);
        return await DotnetCli.RunAsync(
            dotnetBuildArguments + " --no-incremental",
            failIfReturnValueIsNotZero: failIfReturnValueIsNotZero,
            warnAsError: false,
            callerMemberName: $"{binlogBaseFileName}_CacheFallback",
            cancellationToken: cancellationToken);
    }

    private static void ReportCacheStatistics(
        string assetId,
        string cacheVariant,
        CacheConfiguration cacheConfiguration,
        int cacheBuildId,
        IReadOnlyList<string> outputLines)
    {
        bool hasStatistics = TryReadCacheStatistics(
            outputLines,
            out int hitCount,
            out int missCount,
            out double savedSeconds);
        double hitRatio = hasStatistics && hitCount + missCount > 0
            ? (double)hitCount / (hitCount + missCount)
            : 0;

        Console.WriteLine(
            hasStatistics
                ? $"Acceptance MSBuild cache [{cacheConfiguration.Mode}] {assetId}/{cacheVariant}: "
                    + $"{hitCount} hit node(s), {missCount} miss node(s), {hitRatio:P1} hit ratio, "
                    + $"{savedSeconds:F1} project-seconds saved."
                : $"Acceptance MSBuild cache [{cacheConfiguration.Mode}] {assetId}/{cacheVariant}: "
                    + "build succeeded, but MSBuildCache emitted no cache statistics.");

        WriteCacheSummary(
            assetId,
            cacheVariant,
            cacheConfiguration,
            cacheBuildId,
            outcome: "Succeeded",
            hasStatistics,
            hitCount,
            missCount,
            savedSeconds);
    }

    private static void WriteCacheSummary(
        string assetId,
        string cacheVariant,
        CacheConfiguration cacheConfiguration,
        int cacheBuildId,
        string outcome,
        bool hasStatistics,
        int hitCount,
        int missCount,
        double savedSeconds)
    {
        try
        {
            string summaryDirectory = Path.Combine(cacheConfiguration.LogRoot, "Summaries");
            Directory.CreateDirectory(summaryDirectory);
            File.WriteAllLines(
                Path.Combine(
                    summaryDirectory,
                    $"{cacheConfiguration.AssetKey}-{cacheVariant}-{Environment.ProcessId}-{cacheBuildId}.txt"),
                [
                    $"AssetId={assetId}",
                    $"Variant={cacheVariant}",
                    $"Mode={cacheConfiguration.Mode}",
                    $"Outcome={outcome}",
                    $"HasStatistics={hasStatistics}",
                    $"Hits={hitCount}",
                    $"Misses={missCount}",
                    $"SavedProjectSeconds={savedSeconds.ToString(System.Globalization.CultureInfo.InvariantCulture)}",
                ]);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Console.WriteLine(
                $"Could not persist acceptance MSBuild cache statistics for {assetId}/{cacheVariant}: {ex.Message}");
        }
    }

    private static bool TryReadCacheStatistics(
        IReadOnlyList<string> outputLines,
        out int hitCount,
        out int missCount,
        out double savedSeconds)
    {
        bool hasHitCount = TryReadCacheCount(outputLines, "Cache Hit Count:", out hitCount);
        bool hasMissCount = TryReadCacheCount(outputLines, "Cache Miss Count:", out missCount);
        bool hasSavedTime = TryReadSavedProjectSeconds(outputLines, out savedSeconds);
        bool hasSavedTimeText = outputLines.Any(line => line.Contains("(saved ", StringComparison.Ordinal));

        return hasHitCount
            && hasMissCount
            && (hasSavedTime || (hitCount == 0 && !hasSavedTimeText));
    }

    private static bool TryReadCacheCount(IReadOnlyList<string> outputLines, string label, out int count)
    {
        string? line = outputLines.LastOrDefault(line => line.TrimStart().StartsWith(label, StringComparison.Ordinal));
        if (line is null)
        {
            count = 0;
            return false;
        }

        ReadOnlySpan<char> value = line.AsSpan(line.IndexOf(label, StringComparison.Ordinal) + label.Length).TrimStart();
        int separator = value.IndexOf(' ');
        if (separator >= 0)
        {
            value = value[..separator];
        }

        return int.TryParse(value, out count);
    }

    private static bool TryReadSavedProjectSeconds(IReadOnlyList<string> outputLines, out double savedSeconds)
    {
        const string SavedPrefix = "(saved ";
        const string ProjectUnitPrefix = " project-";

        string? line = outputLines.LastOrDefault(line => line.Contains(SavedPrefix, StringComparison.Ordinal));
        if (line is null)
        {
            savedSeconds = 0;
            return false;
        }

        int start = line.IndexOf(SavedPrefix, StringComparison.Ordinal);
        int end = line.IndexOf(ProjectUnitPrefix, StringComparison.Ordinal);
        if (start < 0 || end <= start)
        {
            savedSeconds = 0;
            return false;
        }

        start += SavedPrefix.Length;
        ReadOnlySpan<char> savedValueText = line.AsSpan(start, end - start);
        if (!double.TryParse(
                savedValueText,
                System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.CurrentCulture,
                out double savedValue)
            && !double.TryParse(
                savedValueText,
                System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture,
                out savedValue))
        {
            savedSeconds = 0;
            return false;
        }

        if (!double.IsFinite(savedValue) || savedValue < 0)
        {
            savedSeconds = 0;
            return false;
        }

        string unit = line.AsSpan(end + ProjectUnitPrefix.Length).TrimEnd(')').ToString();
        switch (unit)
        {
            case "seconds":
                savedSeconds = savedValue;
                return true;

            case "minutes":
                savedSeconds = savedValue * 60;
                return true;

            case "hours":
                savedSeconds = savedValue * 60 * 60;
                return true;

            default:
                savedSeconds = 0;
                return false;
        }
    }

    private static void CleanCacheOutputs(string assetPath, string cacheVariant)
    {
        string[] outputDirectories = [.. Directory
            .EnumerateDirectories(assetPath, "*", SearchOption.AllDirectories)
            .Where(path =>
            {
                string directoryName = Path.GetFileName(path);
                if (cacheVariant == "Reflection")
                {
                    return directoryName is "bin" or "obj";
                }

                string? parentDirectoryName = Path.GetFileName(Path.GetDirectoryName(path));
                return directoryName == cacheVariant && parentDirectoryName is "bin" or "obj";
            })
            .OrderByDescending(static path => path.Length)];

        foreach (string outputDirectory in outputDirectories)
        {
            Directory.Delete(outputDirectory, recursive: true);
        }
    }

    private CacheConfiguration? GetCacheConfiguration(string assetId, string assetCode)
    {
        string? mode = Environment.GetEnvironmentVariable(CacheModeEnvironmentVariable);
        if (string.IsNullOrEmpty(mode) || mode == "disabled")
        {
            return null;
        }

        if (mode is not ("read" or "write"))
        {
            throw new InvalidOperationException(
                $"{CacheModeEnvironmentVariable} must be 'disabled', 'read', or 'write', but was '{mode}'.");
        }

        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException("Acceptance MSBuild caching requires full Visual Studio MSBuild on Windows.");
        }

        // Fork PRs do not receive the OAuth token. They retain the established dotnet build path rather
        // than failing every generated asset while trying to access a cache they cannot authenticate to.
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("SYSTEM_ACCESSTOKEN")))
        {
            return null;
        }

        string cacheRoot = GetRequiredEnvironmentVariable(CacheRootEnvironmentVariable);
        string logRoot = GetRequiredEnvironmentVariable(CacheLogRootEnvironmentVariable);
        string identity = string.Join(
            "\0",
            GetType().Assembly.GetName().Name,
            GetType().FullName,
            assetId,
            assetCode);
        string assetKey = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(identity)))[..32];
        return new CacheConfiguration(mode, cacheRoot, logRoot, assetKey);
    }

    private static string GetRequiredEnvironmentVariable(string name)
        => Environment.GetEnvironmentVariable(name) is { Length: > 0 } value
            ? value
            : throw new InvalidOperationException($"{name} must be set when acceptance MSBuild caching is enabled.");

    private static string ResolveEntryProject(string assetPath, string assetId)
    {
        string[] preferredExtensions = [".slnx", ".sln", ".csproj", ".vbproj", ".fsproj"];
        foreach (string extension in preferredExtensions)
        {
            string preferredPath = Path.Combine(assetPath, assetId + extension);
            if (File.Exists(preferredPath))
            {
                return preferredPath;
            }
        }

        string[] candidates = [.. Directory
            .EnumerateFiles(assetPath, "*", SearchOption.TopDirectoryOnly)
            .Where(path => preferredExtensions.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase))];

        return candidates.Length == 1
            ? candidates[0]
            : throw new InvalidOperationException(
                $"Expected one entry project for cached acceptance asset '{assetId}' in '{assetPath}', but found {candidates.Length}: '{string.Join("', '", candidates)}'.");
    }

    private sealed record CacheConfiguration(string Mode, string CacheRoot, string LogRoot, string AssetKey);
}
