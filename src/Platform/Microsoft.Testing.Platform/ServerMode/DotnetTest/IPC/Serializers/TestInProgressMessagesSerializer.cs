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

    |---TestInProgressMessageList Id---| (2 bytes)
    |---TestInProgressMessageList Size---| (4 bytes)
    |---TestInProgressMessageList Value---| (n bytes)
        |---TestInProgressMessageList Length---| (4 bytes)

        |---TestInProgressMessageList[0] FieldCount---| 2 bytes

        |---TestInProgressMessageList[0].Uid Id---| (2 bytes)
        |---TestInProgressMessageList[0].Uid Size---| (4 bytes)
        |---TestInProgressMessageList[0].Uid Value---| (n bytes)

        |---TestInProgressMessageList[0].DisplayName Id---| (2 bytes)
        |---TestInProgressMessageList[0].DisplayName Size---| (4 bytes)
        |---TestInProgressMessageList[0].DisplayName Value---| (n bytes)
*/

internal sealed class TestInProgressMessagesSerializer : NamedPipeSerializer<TestInProgressMessages>, INamedPipeSerializer
{
    public override int Id => TestInProgressMessagesFieldsId.MessagesSerializerId;

    protected override TestInProgressMessages DeserializeCore(Stream stream)
    {
        string? executionId = null;
        string? instanceId = null;
        TestInProgressMessage[]? inProgressMessages = [];

        ushort fieldCount = ReadUShort(stream);

        for (int i = 0; i < fieldCount; i++)
        {
            int fieldId = ReadUShort(stream);
            int fieldSize = ReadInt(stream);

            switch (fieldId)
            {
                case TestInProgressMessagesFieldsId.ExecutionId:
                    executionId = ReadStringValue(stream, fieldSize);
                    break;

                case TestInProgressMessagesFieldsId.InstanceId:
                    instanceId = ReadStringValue(stream, fieldSize);
                    break;

                case TestInProgressMessagesFieldsId.TestInProgressMessageList:
                    inProgressMessages = ReadInProgressMessagesPayload(stream);
                    break;

                default:
                    // If we don't recognize the field id, skip the payload corresponding to that field
                    SetPosition(stream, stream.Position + fieldSize);
                    break;
            }
        }

        return new(executionId, instanceId, inProgressMessages);
    }

    private static TestInProgressMessage[] ReadInProgressMessagesPayload(Stream stream)
    {
        int length = ReadInt(stream);
        var inProgressMessages = new TestInProgressMessage[length];
        for (int i = 0; i < length; i++)
        {
            string? uid = null;
            string? displayName = null;

            int fieldCount = ReadUShort(stream);

            for (int j = 0; j < fieldCount; j++)
            {
                int fieldId = ReadUShort(stream);
                int fieldSize = ReadInt(stream);

                switch (fieldId)
                {
                    case TestInProgressMessageFieldsId.Uid:
                        uid = ReadStringValue(stream, fieldSize);
                        break;

                    case TestInProgressMessageFieldsId.DisplayName:
                        displayName = ReadStringValue(stream, fieldSize);
                        break;

                    default:
                        SetPosition(stream, stream.Position + fieldSize);
                        break;
                }
            }

            inProgressMessages[i] = new TestInProgressMessage(uid, displayName);
        }

        return inProgressMessages;
    }

    protected override void SerializeCore(TestInProgressMessages objectToSerialize, Stream stream)
    {
        DebugAssert(stream.CanSeek, "We expect a seekable stream.");

        WriteUShort(stream, GetFieldCount(objectToSerialize));

        WriteField(stream, TestInProgressMessagesFieldsId.ExecutionId, objectToSerialize.ExecutionId);
        WriteField(stream, TestInProgressMessagesFieldsId.InstanceId, objectToSerialize.InstanceId);
        WriteInProgressMessagesPayload(stream, objectToSerialize.InProgressMessages);
    }

    private static void WriteInProgressMessagesPayload(Stream stream, TestInProgressMessage[]? inProgressMessageList)
    {
        if (inProgressMessageList is null || inProgressMessageList.Length == 0)
        {
            return;
        }

        WriteUShort(stream, TestInProgressMessagesFieldsId.TestInProgressMessageList);

        // We will reserve an int (4 bytes)
        // so that we fill the size later, once we write the payload
        WriteInt(stream, 0);

        long before = stream.Position;
        WriteInt(stream, inProgressMessageList.Length);
        foreach (TestInProgressMessage inProgressMessage in inProgressMessageList)
        {
            WriteUShort(stream, GetFieldCount(inProgressMessage));

            WriteField(stream, TestInProgressMessageFieldsId.Uid, inProgressMessage.Uid);
            WriteField(stream, TestInProgressMessageFieldsId.DisplayName, inProgressMessage.DisplayName);
        }

        // NOTE: We are able to seek only if we are using a MemoryStream
        // thus, the seek operation is fast as we are only changing the value of a property
        WriteAtPosition(stream, (int)(stream.Position - before), before - sizeof(int));
    }

    private static ushort GetFieldCount(TestInProgressMessages inProgressMessages) =>
        (ushort)((inProgressMessages.ExecutionId is null ? 0 : 1) +
        (inProgressMessages.InstanceId is null ? 0 : 1) +
        (IsNullOrEmpty(inProgressMessages.InProgressMessages) ? 0 : 1));

    private static ushort GetFieldCount(TestInProgressMessage inProgressMessage) =>
        (ushort)((inProgressMessage.Uid is null ? 0 : 1) +
        (inProgressMessage.DisplayName is null ? 0 : 1));
}
