// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.IPC.Models;

namespace Microsoft.Testing.Platform.IPC.Serializers;

internal sealed class FileArtifactTransferRequestSerializer : NamedPipeSerializer<FileArtifactTransferRequest>, INamedPipeSerializer
{
    private const int MaximumChunkBytes = 1024 * 1024;

    public override int Id => FileArtifactTransferRequestFieldsId.MessagesSerializerId;

    protected override FileArtifactTransferRequest DeserializeCore(Stream stream)
    {
        string? executionId = null, instanceId = null, transferId = null, fileName = null;
        long totalLength = 0, offset = 0;
        byte[] data = [];
        bool isFinal = false;
        bool hasExecutionId = false, hasInstanceId = false, hasTransferId = false, hasFileName = false;
        bool hasTotalLength = false, hasOffset = false, hasData = false, hasIsFinal = false;

        ReadFields(stream, (fieldId, fieldSize) =>
        {
            switch (fieldId)
            {
                case FileArtifactTransferRequestFieldsId.ExecutionId:
                    executionId = ReadStringValue(stream, fieldSize);
                    hasExecutionId = true;
                    return true;

                case FileArtifactTransferRequestFieldsId.InstanceId:
                    instanceId = ReadStringValue(stream, fieldSize);
                    hasInstanceId = true;
                    return true;

                case FileArtifactTransferRequestFieldsId.TransferId:
                    transferId = ReadStringValue(stream, fieldSize);
                    hasTransferId = true;
                    return true;

                case FileArtifactTransferRequestFieldsId.FileName:
                    fileName = ReadStringValue(stream, fieldSize);
                    hasFileName = true;
                    return true;

                case FileArtifactTransferRequestFieldsId.TotalLength:
                    EnsureFieldSize(fieldSize, sizeof(long), nameof(FileArtifactTransferRequest.TotalLength));
                    totalLength = ReadLong(stream);
                    hasTotalLength = true;
                    return true;

                case FileArtifactTransferRequestFieldsId.Offset:
                    EnsureFieldSize(fieldSize, sizeof(long), nameof(FileArtifactTransferRequest.Offset));
                    offset = ReadLong(stream);
                    hasOffset = true;
                    return true;

                case FileArtifactTransferRequestFieldsId.Data:
                    data = ReadBytes(stream, fieldSize);
                    hasData = true;
                    return true;

                case FileArtifactTransferRequestFieldsId.IsFinal:
                    EnsureFieldSize(fieldSize, sizeof(bool), nameof(FileArtifactTransferRequest.IsFinal));
                    isFinal = ReadBool(stream);
                    hasIsFinal = true;
                    return true;

                default:
                    return false;
            }
        });

        return !hasExecutionId || !hasInstanceId || !hasTransferId || !hasFileName
            || !hasTotalLength || !hasOffset || !hasData || !hasIsFinal
                ? throw new InvalidDataException("A file-artifact transfer request is missing one or more required fields.")
                : totalLength < 0 || offset < 0
                    ? throw new InvalidDataException("A file-artifact transfer request cannot contain a negative length or offset.")
                    : new(executionId, instanceId, transferId, fileName, totalLength, offset, data, isFinal);
    }

    protected override void SerializeCore(FileArtifactTransferRequest objectToSerialize, Stream stream)
    {
        WriteUShort(stream, (ushort)(4
            + (objectToSerialize.ExecutionId is null ? 0 : 1)
            + (objectToSerialize.InstanceId is null ? 0 : 1)
            + (objectToSerialize.TransferId is null ? 0 : 1)
            + (objectToSerialize.FileName is null ? 0 : 1)));
        WriteField(stream, FileArtifactTransferRequestFieldsId.ExecutionId, objectToSerialize.ExecutionId);
        WriteField(stream, FileArtifactTransferRequestFieldsId.InstanceId, objectToSerialize.InstanceId);
        WriteField(stream, FileArtifactTransferRequestFieldsId.TransferId, objectToSerialize.TransferId);
        WriteField(stream, FileArtifactTransferRequestFieldsId.FileName, objectToSerialize.FileName);
        WriteField(stream, FileArtifactTransferRequestFieldsId.TotalLength, objectToSerialize.TotalLength);
        WriteField(stream, FileArtifactTransferRequestFieldsId.Offset, objectToSerialize.Offset);
        WriteUShort(stream, FileArtifactTransferRequestFieldsId.Data);
        WriteInt(stream, objectToSerialize.Data.Length);
        stream.Write(objectToSerialize.Data, 0, objectToSerialize.Data.Length);
        WriteField(stream, FileArtifactTransferRequestFieldsId.IsFinal, objectToSerialize.IsFinal);
    }

    private static byte[] ReadBytes(Stream stream, int size)
    {
        if (size is < 0 or > MaximumChunkBytes)
        {
            throw new InvalidDataException(
                $"A file-artifact transfer chunk must be between 0 and {MaximumChunkBytes} bytes.");
        }

        byte[] bytes = new byte[size];
        int offset = 0;
        while (offset < bytes.Length)
        {
            int read = stream.Read(bytes, offset, bytes.Length - offset);
            if (read == 0)
            {
                throw new EndOfStreamException();
            }

            offset += read;
        }

        return bytes;
    }

    private static void EnsureFieldSize(int fieldSize, int expectedSize, string fieldName)
    {
        if (fieldSize != expectedSize)
        {
            throw new InvalidDataException($"Invalid '{fieldName}' field size '{fieldSize}'.");
        }
    }
}
