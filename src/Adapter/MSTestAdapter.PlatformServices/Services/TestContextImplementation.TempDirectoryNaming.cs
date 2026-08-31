// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if !WINDOWS_UWP && !WIN_UI

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices;

/// <summary>
/// Naming, sanitization and length budgeting of the per-test temporary directory of
/// <see cref="TestContextImplementation"/>.
/// </summary>
internal sealed partial class TestContextImplementation
{
    /// <summary>
    /// Number of hexadecimal characters of the uniqueness suffix appended to the directory name.
    /// This is a full 128-bit GUID (32 hex chars, <c>Guid.ToString("N")</c>) so that two contexts
    /// choosing the same suffix is cryptographically negligible even at very large scales — the
    /// <see cref="Directory.Exists"/> pre-check plus <c>Directory.CreateDirectory</c> is not an
    /// atomic exclusive create, so uniqueness must come from the entropy of the suffix rather than
    /// from the check.
    /// </summary>
    private const int TestTempDirectoryUniqueSuffixLength = 32;

    /// <summary>
    /// Characters that are reserved in a Windows file name. Sanitization strips these on every OS
    /// (not just via <see cref="Path.GetInvalidFileNameChars"/>, which on Unix returns only
    /// <c>/</c> and NUL) so the generated directory name is portable and never accidentally embeds
    /// a path separator or wildcard when the run is later inspected on a different platform.
    /// </summary>
    private const string WindowsReservedFileNameChars = "<>:\"/\\|?*";

    /// <summary>
    /// The Windows <c>MAX_PATH</c> limit. The feature targets this classic 260-character limit and
    /// deliberately does not rely on long-path opt-in (<c>LongPathsEnabled</c> / <c>\\?\</c>), which
    /// is not guaranteed to be enabled and is frequently not honored by external tools that E2E
    /// tests shell out to.
    /// </summary>
    private const int WindowsMaxPath = 260;

    /// <summary>
    /// Characters reserved <em>inside</em> the per-test temporary directory for the files the test
    /// itself writes (e.g. <c>subdir\result.json</c>). The adaptive budget guarantees at least this
    /// much headroom under <c>MAX_PATH</c> on Windows, so a test's own writes do not fail with a
    /// baffling <see cref="System.IO.PathTooLongException"/> originating in user code.
    /// </summary>
    private const int TestTempDirectoryReservedHeadroom = 80;

    /// <summary>
    /// Computes how many characters the readable portion of the directory name may use so that the
    /// full path plus the reserved headroom for the test's own files fits within <c>MAX_PATH</c>.
    /// May be negative when the base path alone already exhausts the budget.
    /// </summary>
    private static int ComputeReadableNameBudget(string baseDirectory)
        // full path = base + separator + <name> + '_' + <suffix>; reserve headroom for files inside.
        => WindowsMaxPath
            - TestTempDirectoryReservedHeadroom
            - baseDirectory.Length
            - 1 // directory separator between base and the temp directory name
            - 1 // the '_' between the readable name and the unique suffix
            - TestTempDirectoryUniqueSuffixLength;

    private string? GetTestTempDirectoryNameSource()
        => !StringEx.IsNullOrEmpty(TestDisplayName)
            ? TestDisplayName
            : _properties.TryGetValue(TestNameLabel, out object? testName) && testName is string testNameString
                ? testNameString
                : null;

    /// <summary>
    /// Sanitizes a test name into a safe, bounded path segment: invalid path characters and
    /// whitespace become underscores, runs of underscores collapse, and the result is truncated to
    /// <paramref name="maxLength"/> characters.
    /// </summary>
    private static string SanitizeTestTempDirectoryName(string? name, int maxLength)
    {
        if (maxLength <= 0 || StringEx.IsNullOrEmpty(name))
        {
            return string.Empty;
        }

        char[] invalidChars = Path.GetInvalidFileNameChars();
        var builder = new StringBuilder(name.Length);
        bool lastWasUnderscore = false;
        foreach (char c in name)
        {
            if (char.IsWhiteSpace(c) || char.IsControl(c)
                || Array.IndexOf(invalidChars, c) >= 0
                || WindowsReservedFileNameChars.IndexOf(c) >= 0)
            {
                if (!lastWasUnderscore)
                {
                    builder.Append('_');
                    lastWasUnderscore = true;
                }
            }
            else
            {
                builder.Append(c);
                lastWasUnderscore = false;
            }
        }

        string sanitized = builder.ToString().Trim('_');
        if (sanitized.Length > maxLength)
        {
            int cutLength = maxLength;

            // Avoid slicing through the middle of a surrogate pair, which would leave a lone
            // surrogate in the directory name and can produce an invalid path segment.
            if (char.IsHighSurrogate(sanitized[cutLength - 1]))
            {
                cutLength--;
            }

            sanitized = sanitized.Substring(0, cutLength).TrimEnd('_');
        }

        return sanitized;
    }
}

#endif
