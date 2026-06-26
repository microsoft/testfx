// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.TestInfrastructure;

/// <summary>
/// THROWAWAY SURVEY HELPER — do not merge.
///
/// <para>
/// Records, per acceptance asset, whether it can be built under a source-generation
/// <see cref="MetadataMode"/>. <see cref="TestAssetFixtureBase"/> drives this in survey mode by
/// attempting the source-gen build for every fixture (opt-out) and reporting the outcome here
/// instead of failing the test. The goal is to gauge how many existing acceptance assets cannot
/// support source generation today, and why.
/// </para>
///
/// <para>
/// Each outcome is emitted as a single tagged console line (so it surfaces in the CI test-step log,
/// which streams test-host stdout when <c>TestingPlatformCaptureOutput=false</c>) and appended to a
/// report file under <c>artifacts/log/sourcegen-survey</c> as a backup.
/// </para>
/// </summary>
public static class SourceGenSurvey
{
    /// <summary>
    /// Distinctive console tag so survey lines can be grepped out of the (noisy) CI test log.
    /// </summary>
    public const string Tag = "##SGSURVEY##";

    private static readonly Lock Gate = new();

    private static readonly Lazy<string> LazyReportFile = new(CreateReportFile);

    private static readonly string Os =
        RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "windows"
        : RuntimeInformation.IsOSPlatform(OSPlatform.Linux) ? "linux"
        : RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? "macos"
        : "unknown";

    /// <summary>
    /// Records the outcome of a source-gen build attempt for an asset.
    /// </summary>
    /// <param name="assetName">The acceptance asset (project) name.</param>
    /// <param name="mode">The source-gen metadata mode that was attempted.</param>
    /// <param name="result">The build result, or <see langword="null"/> when the build was skipped.</param>
    /// <param name="skippedReason">When <paramref name="result"/> is <see langword="null"/>, why it was skipped.</param>
    public static void Record(string assetName, MetadataMode mode, DotnetMuxerResult? result, string? skippedReason = null)
    {
        string outcome = result is null
            ? "SKIP"
            : result.ExitCode == 0 ? "PASS" : "FAIL";

        string reason = result is null
            ? skippedReason ?? "skipped"
            : result.ExitCode == 0 ? string.Empty : ExtractFirstError(result);

        // Single line, key=value, reason last (it may contain spaces).
        string line = $"{Tag} os={Os} mode={mode} result={outcome} asset={assetName} reason={reason}";

        lock (Gate)
        {
            Console.WriteLine(line);
            try
            {
                File.AppendAllText(LazyReportFile.Value, line + Environment.NewLine);
            }
            catch
            {
                // Best-effort backup file; the console line is the source of truth.
            }
        }
    }

    private static string ExtractFirstError(DotnetMuxerResult result)
    {
        foreach (string outputLine in result.StandardOutputLines)
        {
            int idx = outputLine.IndexOf(": error ", StringComparison.Ordinal);
            if (idx >= 0)
            {
                // Keep it compact and single-line: from the error code onward, trimmed.
                return outputLine[(idx + 2)..].Trim();
            }
        }

        return $"exitcode={result.ExitCode} (no compiler error line found)";
    }

    private static string CreateReportFile()
    {
        string dir = Path.Combine(Constants.Root, "artifacts", "log", "sourcegen-survey");
        Directory.CreateDirectory(dir);
        return Path.Combine(dir, $"results-{Os}-{Environment.ProcessId}.txt");
    }
}
