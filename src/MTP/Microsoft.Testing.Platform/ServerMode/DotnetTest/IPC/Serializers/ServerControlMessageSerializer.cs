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

        ReadFields(stream, (fieldId, _) =>
        {
            switch (fieldId)
            {
                case ServerControlMessageFieldsId.Kind:
                    kind = ReadByte(stream);
                    return true;

                default:
                    return false;
            }
        });

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
