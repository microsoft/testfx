// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform;
using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Logging;

namespace Microsoft.Testing.Extensions.VideoRecorder;

internal sealed partial class VideoRecorderSessionHandler
{
    private string BuildFileName(string? name)
    {
        string extension = _recorder!.SegmentExtension;
        string sanitized = Sanitize(name);
        string timestamp = _clock.UtcNow.ToString("yyyyMMdd_HHmmss_fff", CultureInfo.InvariantCulture);

        // A short random suffix guarantees uniqueness even if two clips share a name and are
        // produced within the same millisecond.
        string unique = Guid.NewGuid().ToString("N").Substring(0, 8);
        return sanitized.Length == 0
            ? $"recording_{timestamp}_{unique}.{extension}"
            : $"{sanitized}_{timestamp}_{unique}.{extension}";
    }

    private static string Sanitize(string? name)
    {
        if (RoslynString.IsNullOrWhiteSpace(name))
        {
            return string.Empty;
        }

        var builder = new StringBuilder(name!.Length);
        foreach (char c in name!)
        {
            builder.Append(Array.IndexOf(InvalidFileNameChars, c) >= 0 ? '_' : c);
        }

        return builder.ToString();
    }

    private async Task PublishArtifactAsync(string file, string displayName, string description)
    {
        if (_sessionUid is not { } sessionUid)
        {
            return;
        }

        await _messageBus.PublishAsync(
            this,
            new SessionFileArtifact(sessionUid, new FileInfo(file), displayName, description)).ConfigureAwait(false);
    }

    private void DeleteDirectoryQuietly(string? directory)
    {
        if (directory is null)
        {
            return;
        }

        try
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
        catch (Exception ex)
        {
            _logger.LogTrace($"Failed to delete segment directory '{directory}': {ex.Message}");
        }
    }

    private static void ApplyCommandLineOverrides(VideoRecorderOptions options, ICommandLineOptions commandLineOptions)
    {
        if (commandLineOptions.TryGetOptionArgumentList(VideoRecorderCommandLineProvider.EnableOptionName, out string[]? modeArguments)
            && modeArguments.Length > 0)
        {
            options.PersistMode = modeArguments[0].Equals(VideoRecorderCommandLineProvider.ModeAlways, StringComparison.OrdinalIgnoreCase)
                ? VideoRecorderPersistenceMode.Always
                : VideoRecorderPersistenceMode.OnFailure;
        }

        if (commandLineOptions.TryGetOptionArgumentList(VideoRecorderCommandLineProvider.SourceOptionName, out string[]? sourceArguments)
            && sourceArguments.Length > 0)
        {
            options.Source = sourceArguments[0].Equals(VideoRecorderCommandLineProvider.SourceWindow, StringComparison.OrdinalIgnoreCase)
                ? VideoCaptureSource.Window
                : VideoCaptureSource.Screen;
        }

        if (commandLineOptions.TryGetOptionArgumentList(VideoRecorderCommandLineProvider.GranularityOptionName, out string[]? granularityArguments)
            && granularityArguments.Length > 0)
        {
            options.Granularity = granularityArguments[0].Equals(VideoRecorderCommandLineProvider.GranularitySession, StringComparison.OrdinalIgnoreCase)
                ? VideoCaptureGranularity.PerSession
                : VideoCaptureGranularity.PerTest;
        }

        if (commandLineOptions.TryGetOptionArgumentList(VideoRecorderCommandLineProvider.ArgsOptionName, out string[]? recorderArguments)
            && recorderArguments.Length > 0)
        {
            options.ExtraRecorderArguments = recorderArguments[0];
        }

        if (commandLineOptions.TryGetOptionArgumentList(VideoRecorderCommandLineProvider.MaxDurationOptionName, out string[]? maxDurationArguments)
            && maxDurationArguments.Length > 0
            && int.TryParse(maxDurationArguments[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out int seconds)
            && seconds > 0)
        {
            options.MaxRetainedDuration = TimeSpan.FromSeconds(seconds);
        }

        if (commandLineOptions.TryGetOptionArgumentList(VideoRecorderCommandLineProvider.ChaptersOptionName, out string[]? chapterArguments)
            && chapterArguments.Length > 0)
        {
            options.IncludeChapters = !chapterArguments[0].Equals(VideoRecorderCommandLineProvider.ChaptersOff, StringComparison.OrdinalIgnoreCase);
        }
    }

    private sealed class TestRecord
    {
        public TestRecord(string displayName, DateTimeOffset start, DateTimeOffset end, bool isFailure, string outcome)
        {
            DisplayName = displayName;
            Start = start;
            End = end;
            IsFailure = isFailure;
            Outcome = outcome;
        }

        public string DisplayName { get; }

        public DateTimeOffset Start { get; }

        public DateTimeOffset End { get; }

        public bool IsFailure { get; }

        public string Outcome { get; }
    }
}
