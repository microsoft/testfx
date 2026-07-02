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

    |---Level Id---| (2 bytes)
    |---Level Size---| (4 bytes)
    |---Level Value---| (1 byte)

    |---Text Id---| (2 bytes)
    |---Text Size---| (4 bytes)
    |---Text Value---| (n bytes)
*/

internal sealed class DisplayMessageSerializer : NamedPipeSerializer<DisplayMessage>, INamedPipeSerializer
{
    public override int Id => DisplayMessageFieldsId.MessagesSerializerId;

    protected override DisplayMessage DeserializeCore(Stream stream)
    {
        string? executionId = null;
        string? instanceId = null;
        byte level = DisplayMessageLevels.Information;
        string? text = null;

        ushort fieldCount = ReadUShort(stream);

        for (int i = 0; i < fieldCount; i++)
        {
            ushort fieldId = ReadUShort(stream);
            int fieldSize = ReadInt(stream);

            switch (fieldId)
            {
                case DisplayMessageFieldsId.ExecutionId:
                    executionId = ReadStringValue(stream, fieldSize);
                    break;

                case DisplayMessageFieldsId.InstanceId:
                    instanceId = ReadStringValue(stream, fieldSize);
                    break;

                case DisplayMessageFieldsId.Level:
                    level = ReadByte(stream);

                    // Level is a single byte today, but honor the declared field size so that a future
                    // protocol revision that widens it (or a frame that reports a different size) does not
                    // leave extra bytes unread and misalign the remaining fields.
                    if (fieldSize > 1)
                    {
                        SetPosition(stream, stream.Position + (fieldSize - 1));
                    }

                    break;

                case DisplayMessageFieldsId.Text:
                    text = ReadStringValue(stream, fieldSize);
                    break;

                default:
                    // If we don't recognize the field id, skip the payload corresponding to that field
                    SetPosition(stream, stream.Position + fieldSize);
                    break;
            }
        }

        return new(executionId, instanceId, level, text);
    }

    protected override void SerializeCore(DisplayMessage objectToSerialize, Stream stream)
    {
        DebugAssert(stream.CanSeek, "We expect a seekable stream.");

        WriteUShort(stream, GetFieldCount(objectToSerialize));

        WriteField(stream, DisplayMessageFieldsId.ExecutionId, objectToSerialize.ExecutionId);
        WriteField(stream, DisplayMessageFieldsId.InstanceId, objectToSerialize.InstanceId);
        WriteField(stream, DisplayMessageFieldsId.Level, objectToSerialize.Level);
        WriteField(stream, DisplayMessageFieldsId.Text, objectToSerialize.Text);
    }

    // Level is always written (it is a non-nullable byte); the two id strings and the text are optional.
    private static ushort GetFieldCount(DisplayMessage message) =>
        (ushort)((message.ExecutionId is null ? 0 : 1) +
        (message.InstanceId is null ? 0 : 1) +
        1 +
        (message.Text is null ? 0 : 1));
}
