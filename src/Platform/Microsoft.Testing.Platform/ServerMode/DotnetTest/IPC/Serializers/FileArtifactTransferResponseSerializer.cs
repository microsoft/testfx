// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.IPC.Models;

namespace Microsoft.Testing.Platform.IPC.Serializers;

internal sealed class FileArtifactTransferResponseSerializer : NamedPipeSerializer<FileArtifactTransferResponse>, INamedPipeSerializer
{
    public override int Id => FileArtifactTransferResponseFieldsId.MessagesSerializerId;

    protected override FileArtifactTransferResponse DeserializeCore(Stream stream)
    {
        long nextOffset = 0;
        string? fullPath = null, error = null;
        bool hasNextOffset = false;

        ReadFields(stream, (fieldId, fieldSize) =>
        {
            switch (fieldId)
            {
                case FileArtifactTransferResponseFieldsId.NextOffset:
                    if (fieldSize != sizeof(long))
                    {
                        throw new InvalidDataException($"Invalid Int64 field size '{fieldSize}'.");
                    }

                    nextOffset = ReadLong(stream);
                    hasNextOffset = true;
                    return true;

                case FileArtifactTransferResponseFieldsId.FullPath:
                    fullPath = ReadStringValue(stream, fieldSize);
                    return true;

                case FileArtifactTransferResponseFieldsId.Error:
                    error = ReadStringValue(stream, fieldSize);
                    return true;

                default:
                    return false;
            }
        });

        return !hasNextOffset || nextOffset < 0
            ? throw new InvalidDataException(
                "A file-artifact transfer response must contain a non-negative next offset.")
            : new(nextOffset, fullPath, error);
    }

    protected override void SerializeCore(FileArtifactTransferResponse objectToSerialize, Stream stream)
    {
        WriteUShort(stream, (ushort)(1
            + (objectToSerialize.FullPath is null ? 0 : 1)
            + (objectToSerialize.Error is null ? 0 : 1)));
        WriteField(stream, FileArtifactTransferResponseFieldsId.NextOffset, objectToSerialize.NextOffset);
        WriteField(stream, FileArtifactTransferResponseFieldsId.FullPath, objectToSerialize.FullPath);
        WriteField(stream, FileArtifactTransferResponseFieldsId.Error, objectToSerialize.Error);
    }
}
