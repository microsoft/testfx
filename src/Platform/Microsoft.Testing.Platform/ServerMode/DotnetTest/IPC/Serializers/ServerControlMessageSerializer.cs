// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.IPC.Models;

namespace Microsoft.Testing.Platform.IPC.Serializers;

/*
    |---FieldCount---| 2 bytes

    |---Kind Id---| (2 bytes)
    |---Kind Size---| (4 bytes)
    |---Kind Value---| (1 byte)
*/

internal sealed class ServerControlMessageSerializer : NamedPipeSerializer<ServerControlMessage>, INamedPipeSerializer
{
    public override int Id => ServerControlMessageFieldsId.MessagesSerializerId;

    protected override ServerControlMessage DeserializeCore(Stream stream)
    {
        byte kind = 0;

        ushort fieldCount = ReadUShort(stream);

        for (int i = 0; i < fieldCount; i++)
        {
            ushort fieldId = ReadUShort(stream);
            int fieldSize = ReadInt(stream);

            switch (fieldId)
            {
                case ServerControlMessageFieldsId.Kind:
                    kind = ReadByte(stream);

                    // Kind is a single byte today, but honor the declared field size so that a future
                    // protocol revision that widens it (or a frame that reports a different size) does not
                    // leave extra bytes unread and misalign the remaining fields.
                    if (fieldSize > 1)
                    {
                        SetPosition(stream, stream.Position + (fieldSize - 1));
                    }

                    break;

                default:
                    // If we don't recognize the field id, skip the payload corresponding to that field.
                    SetPosition(stream, stream.Position + fieldSize);
                    break;
            }
        }

        return new(kind);
    }

    protected override void SerializeCore(ServerControlMessage objectToSerialize, Stream stream)
    {
        DebugAssert(stream.CanSeek, "We expect a seekable stream.");

        // Kind is always written (it is a non-nullable byte).
        WriteUShort(stream, 1);
        WriteField(stream, ServerControlMessageFieldsId.Kind, objectToSerialize.Kind);
    }
}
