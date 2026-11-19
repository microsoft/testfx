// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Text.Json;

namespace Microsoft.Testing.Extensions.GitHubActionsReport;

internal static partial class GitHubActionsHistoryStore
{
    internal const int SchemaVersion = 1;
    internal const int MaxSamplesPerTest = 1000;
    internal const int MaxTotalSamples = 10_000;
    internal const long MaxSnapshotBytes = 16 * 1024 * 1024;

    private static readonly TimeSpan LockWaitBudget = TimeSpan.FromSeconds(30);

    public static async Task<GitHubActionsHistorySnapshot> ReadAsync(
        string path,
        DateTimeOffset cutoff,
        CancellationToken cancellationToken)
    {
        if (!File.Exists(path))
        {
            return new GitHubActionsHistorySnapshot();
        }

        string fullPath = Path.GetFullPath(path);
        string lockPath = fullPath + ".lock";
        var stopwatch = Stopwatch.StartNew();
        for (int attempt = 1; ; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                using FileStream lockStream = new(lockPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
                return ReadCore(fullPath, cutoff);
            }
            catch (Exception ex) when (IsRetryableFileException(ex) && stopwatch.Elapsed < LockWaitBudget)
            {
                await Task.Delay(GetRetryDelay(attempt), cancellationToken).ConfigureAwait(false);
            }
        }
    }

    public static async Task WriteMergedAsync(
        string path,
        int historyWindowInDays,
        DateTimeOffset now,
        IReadOnlyList<GitHubActionsHistorySample> currentSamples,
        CancellationToken cancellationToken)
    {
        string fullPath = Path.GetFullPath(path);
        string? directory = Path.GetDirectoryName(fullPath);
        if (directory is not null)
        {
            Directory.CreateDirectory(directory);
        }

        string lockPath = fullPath + ".lock";
        var stopwatch = Stopwatch.StartNew();
        for (int attempt = 1; ; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                using FileStream lockStream = new(lockPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
                GitHubActionsHistorySnapshot existing;
                try
                {
                    existing = ReadCore(fullPath, now.AddDays(-historyWindowInDays));
                }
                catch (Exception ex) when (ex is FormatException or JsonException)
                {
                    existing = new GitHubActionsHistorySnapshot();
                }

                GitHubActionsHistorySnapshot merged = Merge(
                    existing,
                    currentSamples,
                    now,
                    now.AddDays(-historyWindowInDays));
                await WriteAtomicAsync(fullPath, merged, cancellationToken).ConfigureAwait(false);
                return;
            }
            catch (Exception ex) when (IsRetryableFileException(ex) && stopwatch.Elapsed < LockWaitBudget)
            {
                await Task.Delay(GetRetryDelay(attempt), cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private static GitHubActionsHistorySnapshot ReadCore(string path, DateTimeOffset cutoff)
    {
        if (!File.Exists(path))
        {
            return new GitHubActionsHistorySnapshot();
        }

        using FileStream stream = File.OpenRead(path);
        if (stream.Length > MaxSnapshotBytes)
        {
            throw new FormatException(
                $"GitHub Actions test history snapshot '{path}' exceeds the {MaxSnapshotBytes.ToString(CultureInfo.InvariantCulture)}-byte limit.");
        }

        GitHubActionsHistorySnapshot? snapshot = JsonSerializer.Deserialize(stream, GitHubActionsHistoryJsonContext.Default.GitHubActionsHistorySnapshot);
        Validate(snapshot, path);
        snapshot!.Samples =
        [
            .. snapshot.Samples
                .Where(sample => sample.TimestampUtc >= cutoff)
                .OrderBy(static sample => sample.TimestampUtc)
                .ThenBy(static sample => sample.FullyQualifiedName, StringComparer.Ordinal)
                .ThenBy(static sample => sample.TestId, StringComparer.Ordinal),
        ];
        return snapshot;
    }

    private static async Task WriteAtomicAsync(
        string path,
        GitHubActionsHistorySnapshot snapshot,
        CancellationToken cancellationToken)
    {
        string tempPath = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            using (var stream = new FileStream(tempPath, FileMode.CreateNew, FileAccess.Write, FileShare.Read))
            {
                await JsonSerializer.SerializeAsync(
                    stream,
                    snapshot,
                    GitHubActionsHistoryJsonContext.Default.GitHubActionsHistorySnapshot,
                    cancellationToken).ConfigureAwait(false);
            }

            long snapshotLength = new FileInfo(tempPath).Length;
            if (snapshotLength > MaxSnapshotBytes)
            {
                throw new FormatException(
                    $"GitHub Actions test history snapshot would be {snapshotLength.ToString(CultureInfo.InvariantCulture)} bytes, exceeding the {MaxSnapshotBytes.ToString(CultureInfo.InvariantCulture)}-byte limit.");
            }

            if (File.Exists(path))
            {
                File.Replace(tempPath, path, destinationBackupFileName: null);
            }
            else
            {
                File.Move(tempPath, path);
            }
        }
        finally
        {
            try
            {
                File.Delete(tempPath);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // Best-effort cleanup must not hide a successful write or its primary failure.
            }
        }
    }

    private static bool IsRetryableFileException(Exception exception)
        => exception is IOException or UnauthorizedAccessException;

    private static TimeSpan GetRetryDelay(int attempt)
        => TimeSpan.FromMilliseconds(Math.Min(1000, 50 * Math.Pow(2, Math.Min(attempt - 1, 5))));
}
