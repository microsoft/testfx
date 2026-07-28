// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.IPC.Models;

namespace Microsoft.Testing.Platform.IPC.Serializers;

/*
    |---FieldCount---| 2 bytes

    |---Type Id---| (2 bytes)
    |---Type Size---| (4 bytes)
    |---Type Value---| (n bytes)

    |---SessionUid Id---| (2 bytes)
    |---SessionUid Size---| (4 bytes)
    |---SessionUid Value---| (n bytes)

    |---ExecutionId Id---| (2 bytes)
    |---ExecutionId Size---| (4 bytes)
    |---ExecutionId Value---| (n bytes)
*/

internal sealed class TestSessionEventSerializer : NamedPipeSerializer<TestSessionEvent>, INamedPipeSerializer
{
    public override int Id => TestSessionEventFieldsId.MessagesSerializerId;

    protected override TestSessionEvent DeserializeCore(Stream stream)
    {
        byte? type = null;
        string? sessionUid = null;
        string? executionId = null;

        ReadFields(stream, (fieldId, fieldSize) =>
        {
            switch (fieldId)
            {
                case TestSessionEventFieldsId.SessionType:
                    type = ReadByte(stream);
                    return true;

                case TestSessionEventFieldsId.SessionUid:
                    sessionUid = ReadStringValue(stream, fieldSize);
                    return true;

                case TestSessionEventFieldsId.ExecutionId:
                    executionId = ReadStringValue(stream, fieldSize);
                    return true;

                default:
                    return false;
            }
        });

        return new(type, sessionUid, executionId);
    }

    protected override void SerializeCore(TestSessionEvent objectToSerialize, Stream stream)
    {
        DebugAssert(stream.CanSeek, "We expect a seekable stream.");

        WriteUShort(stream, GetFieldCount(objectToSerialize));

        WriteField(stream, TestSessionEventFieldsId.SessionType, objectToSerialize.SessionType);
        WriteField(stream, TestSessionEventFieldsId.SessionUid, objectToSerialize.SessionUid);
        WriteField(stream, TestSessionEventFieldsId.ExecutionId, objectToSerialize.ExecutionId);
    }

    private static ushort GetFieldCount(TestSessionEvent testSessionEvent) =>
        (ushort)((testSessionEvent.SessionType is null ? 0 : 1) +
        (testSessionEvent.SessionUid is null ? 0 : 1) +
        (testSessionEvent.ExecutionId is null ? 0 : 1));
}
