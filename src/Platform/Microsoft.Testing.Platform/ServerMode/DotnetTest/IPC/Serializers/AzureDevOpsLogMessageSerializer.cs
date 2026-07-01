// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.IPC.Models;

namespace Microsoft.Testing.Platform.IPC.Serializers;

/*
    |---FieldCount---| 2 bytes

    |---ExecutionId Id---| (2 bytes)
    |---ExecutionId Size---| (4 bytes)
    |---ExecutionId Value---| (n bytes)

    |---InstanceId Id---| (2 bytes)
    |---InstanceId Size---| (4 bytes)
    |---InstanceId Value---| (n bytes)

    |---LogText Id---| (2 bytes)
    |---LogText Size---| (4 bytes)
    |---LogText Value---| (n bytes)
*/

internal sealed class AzureDevOpsLogMessageSerializer : NamedPipeSerializer<AzureDevOpsLogMessage>, INamedPipeSerializer
{
    public override int Id => AzureDevOpsLogMessageFieldsId.MessagesSerializerId;

    protected override AzureDevOpsLogMessage DeserializeCore(Stream stream)
    {
        string? executionId = null;
        string? instanceId = null;
        string? logText = null;

        ushort fieldCount = ReadUShort(stream);

        for (int i = 0; i < fieldCount; i++)
        {
            ushort fieldId = ReadUShort(stream);
            int fieldSize = ReadInt(stream);

            switch (fieldId)
            {
                case AzureDevOpsLogMessageFieldsId.ExecutionId:
                    executionId = ReadStringValue(stream, fieldSize);
                    break;

                case AzureDevOpsLogMessageFieldsId.InstanceId:
                    instanceId = ReadStringValue(stream, fieldSize);
                    break;

                case AzureDevOpsLogMessageFieldsId.LogText:
                    logText = ReadStringValue(stream, fieldSize);
                    break;

                default:
                    // If we don't recognize the field id, skip the payload corresponding to that field
                    SetPosition(stream, stream.Position + fieldSize);
                    break;
            }
        }

        return new(executionId, instanceId, logText);
    }

    protected override void SerializeCore(AzureDevOpsLogMessage objectToSerialize, Stream stream)
    {
        DebugAssert(stream.CanSeek, "We expect a seekable stream.");

        WriteUShort(stream, GetFieldCount(objectToSerialize));

        WriteField(stream, AzureDevOpsLogMessageFieldsId.ExecutionId, objectToSerialize.ExecutionId);
        WriteField(stream, AzureDevOpsLogMessageFieldsId.InstanceId, objectToSerialize.InstanceId);
        WriteField(stream, AzureDevOpsLogMessageFieldsId.LogText, objectToSerialize.LogText);
    }

    private static ushort GetFieldCount(AzureDevOpsLogMessage message) =>
        (ushort)((message.ExecutionId is null ? 0 : 1) +
        (message.InstanceId is null ? 0 : 1) +
        (message.LogText is null ? 0 : 1));
}
