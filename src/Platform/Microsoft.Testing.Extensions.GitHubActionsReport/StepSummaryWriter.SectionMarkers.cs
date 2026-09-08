// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.Logging;

namespace Microsoft.Testing.Extensions.GitHubActionsReport;

internal sealed partial class StepSummaryWriter
{
    /// <summary>
    /// Measures the shared summary file, or returns <see langword="null"/> when it does not exist or cannot be
    /// read. Best-effort by design: the length sizes a budget and explains a refusal, so failing to read it must
    /// degrade the report rather than fail the run.
    /// </summary>
    internal long? GetSummaryLength()
    {
        try
        {
            if (!_fileSystem.ExistFile(Path))
            {
                return null;
            }

            using IFileStream stream = _fileSystem.NewFileStream(Path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            return stream.Stream.Length;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            if (_logger.IsEnabled(LogLevel.Trace))
            {
                _logger.LogTrace($"Could not measure '{Path}': {ex.Message}");
            }

            return null;
        }
    }

    /// <summary>
    /// Measures the shared summary file, discounting the section this run is about to replace.
    /// </summary>
    /// <remarks>
    /// Reprocessing writes over its own previous output rather than adding to it, so counting that output as
    /// occupied space would make a re-run condense or drop modules that fit perfectly well once the old block
    /// is gone.
    /// </remarks>
    internal long GetSummaryLengthExcludingSection(string aggregationId)
    {
        try
        {
            if (!_fileSystem.ExistFile(Path))
            {
                return 0;
            }

            string existing;
            using (IFileStream stream = _fileSystem.NewFileStream(Path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
            {
                // Discounting our own section means reading the file, and its size is set by producers this
                // extension does not control. Past the ceiling, report the raw length rather than reading: it
                // over-states the occupied space only by the size of this run's own previous block, and it makes
                // the caller degrade or refuse instead of allocating whatever another producer happened to write.
                long length = stream.Stream.Length;
                if (length > MaxReadableSummaryBytes)
                {
                    return length;
                }

                using var reader = new StreamReader(stream.Stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
                existing = reader.ReadToEnd();
            }

            int start = IndexOfMarkerLine(existing, BuildSectionStartMarker(aggregationId));
            if (start < 0)
            {
                return Encoding.UTF8.GetByteCount(existing);
            }

            string endMarker = BuildSectionEndMarker(aggregationId);
            int end = IndexOfMarkerLine(existing, endMarker, start);
            return end < 0
                ? Encoding.UTF8.GetByteCount(existing)
                : Encoding.UTF8.GetByteCount(existing.Remove(start, end + endMarker.Length - start));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            if (_logger.IsEnabled(LogLevel.Trace))
            {
                _logger.LogTrace($"Could not measure '{Path}': {ex.Message}");
            }

            return 0;
        }
    }

    private static string BuildSectionStartMarker(string aggregationId)
        => $"<!-- microsoft-testing-platform:{GitHubActionsSummaryArtifactPostProcessor.Provider}:{aggregationId}:start -->";

    private static string BuildSectionEndMarker(string aggregationId)
        => $"<!-- microsoft-testing-platform:{GitHubActionsSummaryArtifactPostProcessor.Provider}:{aggregationId}:end -->";

    /// <summary>
    /// Enumerates the lines of <paramref name="content"/> that are <em>not</em> inside a fenced code block,
    /// yielding each line's start offset and its text.
    /// </summary>
    /// <remarks>
    /// Rendered failure messages and stack traces are copied verbatim into fenced blocks, so anything this
    /// extension uses as a structural marker can also appear there as ordinary user-controlled text. Every
    /// structural scan of the summary therefore has to skip fenced content, or a test could forge a marker.
    /// Fences are chosen longer than the longest backtick run in the body they wrap, so a fence only closes on a
    /// run at least as long as the one that opened it.
    /// </remarks>
    private static IEnumerable<(int Start, string Line)> EnumerateUnfencedLines(string content)
    {
        int fenceLength = 0;
        int start = 0;
        while (start <= content.Length)
        {
            int newline = content.IndexOf('\n', start);
            int end = newline < 0 ? content.Length : newline;
            string line = content.Substring(start, end - start).TrimEnd('\r');

            int backticks = 0;
            while (backticks < line.Length && line[backticks] == '`')
            {
                backticks++;
            }

            if (fenceLength == 0)
            {
                if (backticks >= 3)
                {
                    fenceLength = backticks;
                }
                else
                {
                    yield return (start, line);
                }
            }
            else if (backticks >= fenceLength && line.Length == backticks)
            {
                fenceLength = 0;
            }

            if (newline < 0)
            {
                yield break;
            }

            start = newline + 1;
        }
    }

    /// <summary>
    /// Returns the offset of the first line equal to <paramref name="marker"/> that is not inside a fenced code
    /// block, or <c>-1</c>.
    /// </summary>
    private static int IndexOfMarkerLine(string content, string marker, int searchFrom = 0)
    {
        foreach ((int start, string line) in EnumerateUnfencedLines(content))
        {
            if (start >= searchFrom && string.Equals(line, marker, StringComparison.Ordinal))
            {
                return start;
            }
        }

        return -1;
    }

    /// <summary>
    /// Counts the full test project sections this extension has written to the shared summary file.
    /// </summary>
    /// <remarks>
    /// Only a marker occupying a whole line outside a fenced block counts, otherwise a test could inflate the
    /// project count the truncation note reports simply by printing the marker in its failure output.
    /// </remarks>
    internal static int CountProjectSections(string summary)
    {
        int count = 0;
        foreach ((_, string line) in EnumerateUnfencedLines(summary))
        {
            if (string.Equals(line, GitHubActionsSummaryReporter.ProjectSectionMarker, StringComparison.Ordinal))
            {
                count++;
            }
        }

        return count;
    }
}
