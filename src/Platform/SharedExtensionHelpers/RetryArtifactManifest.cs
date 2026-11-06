// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.Logging;

namespace Microsoft.Testing.Extensions;

#pragma warning disable RS0051 // Recovery infrastructure is shared-source implementation detail, not package API.

internal static class RetryArtifactManifest
{
    public const long MaxBytes = 16L * 1024 * 1024;
    public const int MaxLineLength = 64 * 1024;
    public const int MaxRecords = 10_000;

    public static bool TrySplitEntry(
        string line,
        [NotNullWhen(true)] out string? encodedPath,
        [NotNullWhen(true)] out string? encodedKindOrNullSentinel)
    {
        int separatorIndex = line.IndexOf('\t');
        if (separatorIndex <= 0)
        {
            encodedPath = null;
            encodedKindOrNullSentinel = null;
            return false;
        }

        encodedPath = line.Substring(0, separatorIndex);
        encodedKindOrNullSentinel = line.Substring(separatorIndex + 1);
        return true;
    }

    public static string DecodePath(string encodedPath)
        => Encoding.UTF8.GetString(Convert.FromBase64String(encodedPath));

    public static string? DecodeKind(string encodedKindOrNullSentinel)
        => encodedKindOrNullSentinel == "-"
            ? null
            : Encoding.UTF8.GetString(Convert.FromBase64String(encodedKindOrNullSentinel));

    public static string WriteEntryWithEncodedKind(string path, string encodedKindOrNullSentinel)
        => $"{Convert.ToBase64String(Encoding.UTF8.GetBytes(path))}\t{encodedKindOrNullSentinel}";

    public static string WriteEntry(string path, string? kind)
        => WriteEntryWithEncodedKind(
            path,
            kind is null ? "-" : Convert.ToBase64String(Encoding.UTF8.GetBytes(kind)));

    public static async Task WriteArtifactAsync(
        IEnvironment environment,
        IFileSystem fileSystem,
        ILogger logger,
        string artifactPath,
        string? artifactKind,
        CancellationToken cancellationToken)
    {
        const string retryArtifactManifestEnvironmentVariable = "TESTINGPLATFORM_RETRY_RECOVERED_ARTIFACT_MANIFEST";
        string? manifestPath = environment.GetEnvironmentVariable(retryArtifactManifestEnvironmentVariable);
        if (manifestPath is null || manifestPath.Length == 0)
        {
            return;
        }

        try
        {
            string? directory = Path.GetDirectoryName(manifestPath);
            if (directory is not null && directory.Length > 0)
            {
                fileSystem.CreateDirectory(directory);
            }

            string entry = WriteEntry(artifactPath, artifactKind);
            using IFileStream stream = fileSystem.NewFileStream(
                manifestPath,
                FileMode.Append,
                FileAccess.Write,
                FileShare.Read);
            using var writer = new StreamWriter(stream.Stream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
#if NETCOREAPP
            await writer.WriteLineAsync(entry.AsMemory(), cancellationToken).ConfigureAwait(false);
#else
            cancellationToken.ThrowIfCancellationRequested();
            await writer.WriteLineAsync(entry).ConfigureAwait(false);
#endif
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            await logger.LogWarningAsync(
                $"Failed to register recovered report '{artifactPath}' with retry artifact consolidation: {ex.Message}").ConfigureAwait(false);
        }
    }
}

#pragma warning restore RS0051
