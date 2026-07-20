// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform;

namespace Microsoft.Testing.Extensions.TrxReport.Abstractions.Streaming;

internal sealed class TrxSnapshotPublisherPrototype
{
    private readonly ITrxPrototypeFileOperations _operations;

    public TrxSnapshotPublisherPrototype(ITrxPrototypeFileOperations operations)
    {
        _operations = operations ?? throw new ArgumentNullException(nameof(operations));
    }

    public void Publish(string destinationPath, Action<ITrxPrototypeFile> writeCompleteDocument)
    {
        if (RoslynString.IsNullOrEmpty(destinationPath))
        {
            throw new ArgumentException("The destination path cannot be null or empty.", nameof(destinationPath));
        }

        if (writeCompleteDocument is null)
        {
            throw new ArgumentNullException(nameof(writeCompleteDocument));
        }

        if (!_operations.SupportsAtomicReplace)
        {
            throw new PlatformNotSupportedException(
                "Atomic overwrite replacement is unavailable on this runtime. The TRX prototype will not delete the destination before moving a temporary file.");
        }

        string temporaryPath = _operations.CreateTemporarySiblingPath(destinationPath);
        ValidateTemporarySibling(destinationPath, temporaryPath);

        try
        {
            using (ITrxPrototypeFile temporary = _operations.Open(
                       temporaryPath,
                       FileMode.CreateNew,
                       FileAccess.ReadWrite,
                       FileShare.Read | FileShare.Delete))
            {
                writeCompleteDocument(temporary);
                temporary.Flush();
            }

            _operations.ReplaceTemporarySibling(temporaryPath, destinationPath);
        }
        catch
        {
            try
            {
                if (_operations.Exists(temporaryPath))
                {
                    _operations.Delete(temporaryPath);
                }
            }
            catch
            {
                // Abrupt termination, permissions, or sharing can also prevent best-effort temp cleanup.
                // The destination is deliberately never a cleanup target.
            }

            throw;
        }
    }

    private static void ValidateTemporarySibling(string destinationPath, string temporaryPath)
    {
        if (RoslynString.IsNullOrEmpty(temporaryPath))
        {
            throw new InvalidOperationException("The temporary sibling path cannot be null or empty.");
        }

        string fullDestinationPath = Path.GetFullPath(destinationPath);
        string fullTemporaryPath = Path.GetFullPath(temporaryPath);
        StringComparison pathComparison = Path.DirectorySeparatorChar == '\\'
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;

        if (string.Equals(fullDestinationPath, fullTemporaryPath, pathComparison)
            || !string.Equals(
                Path.GetDirectoryName(fullDestinationPath),
                Path.GetDirectoryName(fullTemporaryPath),
                pathComparison))
        {
            throw new InvalidOperationException(
                $"The temporary path '{temporaryPath}' must be a distinct sibling of destination '{destinationPath}' so replacement stays on one filesystem.");
        }
    }
}
