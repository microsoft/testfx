// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.IPC.Models;

namespace Microsoft.Testing.Platform.IPC.Serializers;

/*
    |---FieldCount---| 2 bytes

    |---Kind Id---| (2 bytes)
    |---Kind Size---| (4 bytes)
    |---Kind Value---| (1 byte)

    Protocol 1.5.0 (RunInvocationCleanup only), each omitted when null:
    |---AggregateExitCode Id---| (2 bytes) |---Size---| (4 bytes) |---Value---| (4 bytes)
    |---IsCanceled Id---| (2 bytes) |---Size---| (4 bytes) |---Value---| (1 byte)
*/

internal sealed class ServerControlMessageSerializer : NamedPipeSerializer<ServerControlMessage>, INamedPipeSerializer
{
    public override int Id => ServerControlMessageFieldsId.MessagesSerializerId;

    protected override ServerControlMessage DeserializeCore(Stream stream)
    {
        byte kind = 0;
        int? aggregateExitCode = null;
        bool? isCanceled = null;

        ReadFields(stream, (fieldId, fieldSize) =>
        {
            switch (fieldId)
            {
                case ServerControlMessageFieldsId.Kind:
                    kind = ReadByte(stream);
                    return true;

                case ServerControlMessageFieldsId.AggregateExitCode:
                    aggregateExitCode = ReadFieldPayload(stream, fieldSize, ReadInt);
                    return true;

                case ServerControlMessageFieldsId.IsCanceled:
                    isCanceled = ReadFieldPayload(stream, fieldSize, ReadBool);
                    return true;

                default:
                    return false;
            }
        });

        return new(kind, aggregateExitCode, isCanceled);
    }

    protected override void SerializeCore(ServerControlMessage objectToSerialize, Stream stream)
    {
        DebugAssert(stream.CanSeek, "We expect a seekable stream.");

        // Kind is always written (it is a non-nullable byte).
        WriteUShort(stream, (ushort)(1
            + (objectToSerialize.AggregateExitCode is null ? 0 : 1)
            + (objectToSerialize.IsCanceled is null ? 0 : 1)));
        WriteField(stream, ServerControlMessageFieldsId.Kind, objectToSerialize.Kind);
        WriteField(stream, ServerControlMessageFieldsId.AggregateExitCode, objectToSerialize.AggregateExitCode);
        WriteField(stream, ServerControlMessageFieldsId.IsCanceled, objectToSerialize.IsCanceled);
    }
}
