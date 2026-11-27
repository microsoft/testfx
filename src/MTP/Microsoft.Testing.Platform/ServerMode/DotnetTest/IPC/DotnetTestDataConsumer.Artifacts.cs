// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.IPC.Models;
using Microsoft.Testing.Platform.IPC.Serializers;

namespace Microsoft.Testing.Platform.IPC;

internal sealed partial class DotnetTestDataConsumer
{
    internal static bool TryDeferTrxArtifacts(FileArtifactMessages message, string? attemptRoot, string? publicationDirectory)
    {
        if (attemptRoot is null
            || message.FileArtifacts is not [{ FullPath: { } path }]
            || !Path.GetExtension(path).Equals(".trx", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        string prefix = Path.GetFullPath(attemptRoot).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            + Path.DirectorySeparatorChar;
        StringComparison comparison = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;
        if (!Path.GetFullPath(path).StartsWith(prefix, comparison))
        {
            return false;
        }

        ApplicationStateGuard.Ensure(publicationDirectory is not null);
        Directory.CreateDirectory(publicationDirectory);
        string temporaryPath = Path.Combine(publicationDirectory, $"{Guid.NewGuid():N}.tmp");
        using (var stream = new FileStream(temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
        {
            new FileArtifactMessagesSerializer().Serialize(message, stream);
        }

        File.Move(temporaryPath, Path.ChangeExtension(temporaryPath, ".bin"));
        return true;
    }

    internal static FileArtifactMessages ReadDeferredTrxArtifacts(string manifestPath)
    {
        using var stream = new FileStream(manifestPath, FileMode.Open, FileAccess.Read, FileShare.Read);
        return new FileArtifactMessagesSerializer().Deserialize(stream);
    }

    // Maps a session-scoped artifact to its wire message. Extracted so the producer-to-wire mapping
    // (in particular the Kind carried for post-processing grouping) is unit-testable without a live pipe.
    internal static FileArtifactMessages CreateFileArtifactMessages(string? executionId, SessionFileArtifact sessionFileArtifact)
        => new(
            executionId,
            DotnetTestConnection.InstanceId,
            [
                new FileArtifactMessage(
                    sessionFileArtifact.FileInfo.FullName,
                    sessionFileArtifact.DisplayName,
                    sessionFileArtifact.Description ?? string.Empty,
                    string.Empty,
                    string.Empty,
                    sessionFileArtifact.SessionUid.Value,
                    sessionFileArtifact.Kind),
            ]);

    // Maps a (non session-scoped) artifact to its wire message. See the SessionFileArtifact overload.
    internal static FileArtifactMessages CreateFileArtifactMessages(string? executionId, FileArtifact fileArtifact)
        => new(
            executionId,
            DotnetTestConnection.InstanceId,
            [
                new FileArtifactMessage(
                    fileArtifact.FileInfo.FullName,
                    fileArtifact.DisplayName,
                    fileArtifact.Description ?? string.Empty,
                    string.Empty,
                    string.Empty,
                    string.Empty,
                    fileArtifact.Kind),
            ]);
}
