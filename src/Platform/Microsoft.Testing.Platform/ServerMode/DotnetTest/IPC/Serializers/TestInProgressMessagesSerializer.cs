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
        TestInProgressMessage[]? inProgressMessages = [];

        (string? executionId, string? instanceId) = ReadExecutionScopedFields(stream, (fieldId, fieldSize) =>
        {
            if (fieldId == TestInProgressMessagesFieldsId.TestInProgressMessageList)
            {
                inProgressMessages = ReadInProgressMessagesPayload(stream);
                return true;
            }

            return false;
        });

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

            ReadFields(stream, (fieldId, fieldSize) =>
            {
                switch (fieldId)
                {
                    case TestInProgressMessageFieldsId.Uid:
                        uid = ReadStringValue(stream, fieldSize);
                        return true;

                    case TestInProgressMessageFieldsId.DisplayName:
                        displayName = ReadStringValue(stream, fieldSize);
                        return true;

                    default:
                        return false;
                }
            });

            inProgressMessages[i] = new TestInProgressMessage(uid, displayName);
        }

        return inProgressMessages;
    }

    protected override void SerializeCore(TestInProgressMessages objectToSerialize, Stream stream)
        => WriteExecutionScopedFields(
            stream,
            objectToSerialize.ExecutionId,
            objectToSerialize.InstanceId,
            (ushort)(IsNullOrEmpty(objectToSerialize.InProgressMessages) ? 0 : 1),
            s => WriteInProgressMessagesPayload(s, objectToSerialize.InProgressMessages));

    private static void WriteInProgressMessagesPayload(Stream stream, TestInProgressMessage[]? inProgressMessageList)
        => WriteListPayload(stream, TestInProgressMessagesFieldsId.TestInProgressMessageList, inProgressMessageList, static (s, inProgressMessage) =>
        {
            WriteUShort(s, GetFieldCount(inProgressMessage));

            WriteField(s, TestInProgressMessageFieldsId.Uid, inProgressMessage.Uid);
            WriteField(s, TestInProgressMessageFieldsId.DisplayName, inProgressMessage.DisplayName);
        });

    private static ushort GetFieldCount(TestInProgressMessage inProgressMessage) =>
        (ushort)((inProgressMessage.Uid is null ? 0 : 1) +
        (inProgressMessage.DisplayName is null ? 0 : 1));
}
