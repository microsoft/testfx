// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.IPC.Models;

namespace Microsoft.Testing.Platform.IPC.Serializers;

/*
    |---FieldCount---| 2 bytes

    |---ExecutionId Id---| (2 bytes)
    |---ExecutionId Size---| (4 bytes)
    |---ExecutionId Value---| (n bytes)

    |---InstanceId---| (2 bytes)
    |---InstanceId Size---| (4 bytes)
    |---InstanceId Value---| (n bytes)

    |---DiscoveredTestMessageList Id---| (2 bytes)
    |---DiscoveredTestMessageList Size---| (4 bytes)
    |---DiscoveredTestMessageList Value---| (n bytes)
        |---DiscoveredTestMessageList Length---| (4 bytes)

        |---DiscoveredTestMessageList[0] FieldCount---| 2 bytes

        |---DiscoveredTestMessageList[0].Uid Id---| (2 bytes)
        |---DiscoveredTestMessageList[0].Uid Size---| (4 bytes)
        |---DiscoveredTestMessageList[0].Uid Value---| (n bytes)

        |---DiscoveredTestMessageList[0].DisplayName Id---| (2 bytes)
        |---DiscoveredTestMessageList[0].DisplayName Size---| (4 bytes)
        |---DiscoveredTestMessageList[0].DisplayName Value---| (n bytes)

        |---DiscoveredTestMessageList[0].FilePath Id---| (2 bytes)
        |---DiscoveredTestMessageList[0].FilePath Size---| (4 bytes)
        |---DiscoveredTestMessageList[0].FilePath Value---| (n bytes)

        |---DiscoveredTestMessageList[0].LineNumber Id---| (2 bytes)
        |---DiscoveredTestMessageList[0].LineNumber Size---| (4 bytes)
        |---DiscoveredTestMessageList[0].LineNumber Value---| (4 bytes)

        |---DiscoveredTestMessageList[0].Namespace Id---| (2 bytes)
        |---DiscoveredTestMessageList[0].Namespace Size---| (4 bytes)
        |---DiscoveredTestMessageList[0].Namespace Value---| (n bytes)

        |---DiscoveredTestMessageList[0].TypeName Id---| (2 bytes)
        |---DiscoveredTestMessageList[0].TypeName Size---| (4 bytes)
        |---DiscoveredTestMessageList[0].TypeName Value---| (n bytes)

        |---DiscoveredTestMessageList[0].MethodName Id---| (2 bytes)
        |---DiscoveredTestMessageList[0].MethodName Size---| (4 bytes)
        |---DiscoveredTestMessageList[0].MethodName Value---| (n bytes)

        |---DiscoveredTestMessageList[0].Traits Id---| (2 bytes)
        |---DiscoveredTestMessageList[0].Traits Size---| (4 bytes)
        |---DiscoveredTestMessageList[0].Traits Value---| (n bytes)
            |---DiscoveredTestMessageList[0].Traits Length---| (4 bytes)

            |---DiscoveredTestMessageList[0].Trits[0] FieldCount---| 2 bytes

            |---DiscoveredTestMessageList[0].Trits[0].Key Id---| (2 bytes)
            |---DiscoveredTestMessageList[0].Trits[0].Key Size---| (4 bytes)
            |---DiscoveredTestMessageList[0].Trits[0].Key Value---| (n bytes)

            |---DiscoveredTestMessageList[0].Trits[0].Value Id---| (2 bytes)
            |---DiscoveredTestMessageList[0].Trits[0].Value Size---| (4 bytes)
            |---DiscoveredTestMessageList[0].Trits[0].Value Value---| (n bytes)
*/

internal sealed class DiscoveredTestMessagesSerializer : BaseSerializer, INamedPipeSerializer
{
    public int Id => DiscoveredTestMessagesFieldsId.MessagesSerializerId;

    public object Deserialize(Stream stream)
    {
        string? executionId = null;
        string? instanceId = null;
        DiscoveredTestMessage[]? discoveredTestMessages = [];

        ushort fieldCount = ReadShort(stream);

        for (int i = 0; i < fieldCount; i++)
        {
            int fieldId = ReadShort(stream);
            int fieldSize = ReadInt(stream);

            switch (fieldId)
            {
                case DiscoveredTestMessagesFieldsId.ExecutionId:
                    executionId = ReadStringValue(stream, fieldSize);
                    break;

                case DiscoveredTestMessagesFieldsId.InstanceId:
                    instanceId = ReadStringValue(stream, fieldSize);
                    break;

                case DiscoveredTestMessagesFieldsId.DiscoveredTestMessageList:
                    discoveredTestMessages = ReadDiscoveredTestMessagesPayload(stream);
                    break;

                default:
                    // If we don't recognize the field id, skip the payload corresponding to that field
                    SetPosition(stream, stream.Position + fieldSize);
                    break;
            }
        }

        return new DiscoveredTestMessages(executionId, instanceId, discoveredTestMessages);
    }

    private static DiscoveredTestMessage[] ReadDiscoveredTestMessagesPayload(Stream stream)
    {
        int length = ReadInt(stream);
        var discoveredTestMessages = new DiscoveredTestMessage[length];
        for (int i = 0; i < length; i++)
        {
            string? uid = null;
            string? displayName = null;
            string? filePath = null;
            int? lineNumber = null;
            string? @namespace = null;
            string? typeName = null;
            string? methodName = null;
            TestMetadataProperty[] traits = [];

            int fieldCount = ReadShort(stream);

            for (int j = 0; j < fieldCount; j++)
            {
                int fieldId = ReadShort(stream);
                int fieldSize = ReadInt(stream);

                switch (fieldId)
                {
                    case DiscoveredTestMessageFieldsId.Uid:
                        uid = ReadStringValue(stream, fieldSize);
                        break;

                    case DiscoveredTestMessageFieldsId.DisplayName:
                        displayName = ReadStringValue(stream, fieldSize);
                        break;

                    case DiscoveredTestMessageFieldsId.FilePath:
                        filePath = ReadStringValue(stream, fieldSize);
                        break;

                    case DiscoveredTestMessageFieldsId.LineNumber:
                        lineNumber = ReadInt(stream);
                        break;

                    case DiscoveredTestMessageFieldsId.Namespace:
                        @namespace = ReadStringValue(stream, fieldSize);
                        break;

                    case DiscoveredTestMessageFieldsId.TypeName:
                        typeName = ReadStringValue(stream, fieldSize);
                        break;

                    case DiscoveredTestMessageFieldsId.MethodName:
                        methodName = ReadStringValue(stream, fieldSize);
                        break;

                    case DiscoveredTestMessageFieldsId.Traits:
                        traits = ReadTraitsPayload(stream);
                        break;

                    default:
                        SetPosition(stream, stream.Position + fieldSize);
                        break;
                }
            }

            discoveredTestMessages[i] = new DiscoveredTestMessage(uid, displayName, filePath, lineNumber, @namespace, typeName, methodName, traits);
        }

        return discoveredTestMessages;
    }

    private static TestMetadataProperty[] ReadTraitsPayload(Stream stream)
    {
        int length = ReadInt(stream);
        var traits = new TestMetadataProperty[length];
        for (int i = 0; i < length; i++)
        {
            string? key = null;
            string? value = null;
            int fieldCount = ReadShort(stream);

            for (int j = 0; j < fieldCount; j++)
            {
                int fieldId = ReadShort(stream);
                int fieldSize = ReadInt(stream);

                switch (fieldId)
                {
                    case TraitMessageFieldsId.Key:
                        key = ReadStringValue(stream, fieldSize);
                        break;

                    case TraitMessageFieldsId.Value:
                        value = ReadStringValue(stream, fieldSize);
                        break;

                    default:
                        SetPosition(stream, stream.Position + fieldSize);
                        break;
                }
            }

            Guard.NotNull(key);
            Guard.NotNull(value);
            traits[i] = new TestMetadataProperty(key, value);
        }

        return traits;
    }

    public void Serialize(object objectToSerialize, Stream stream)
    {
        RoslynDebug.Assert(stream.CanSeek, "We expect a seekable stream.");

        var discoveredTestMessages = (DiscoveredTestMessages)objectToSerialize;

        WriteShort(stream, GetFieldCount(discoveredTestMessages));

        WriteField(stream, DiscoveredTestMessagesFieldsId.ExecutionId, discoveredTestMessages.ExecutionId);
        WriteField(stream, DiscoveredTestMessagesFieldsId.InstanceId, discoveredTestMessages.InstanceId);
        WriteDiscoveredTestMessagesPayload(stream, discoveredTestMessages.DiscoveredMessages);
    }

    private static void WriteDiscoveredTestMessagesPayload(Stream stream, DiscoveredTestMessage[]? discoveredTestMessageList)
    {
        if (discoveredTestMessageList is null || discoveredTestMessageList.Length == 0)
        {
            return;
        }

        WriteShort(stream, DiscoveredTestMessagesFieldsId.DiscoveredTestMessageList);

        // We will reserve an int (4 bytes)
        // so that we fill the size later, once we write the payload
        WriteInt(stream, 0);

        long before = stream.Position;
        WriteInt(stream, discoveredTestMessageList.Length);
        foreach (DiscoveredTestMessage discoveredTestMessage in discoveredTestMessageList)
        {
            WriteShort(stream, GetFieldCount(discoveredTestMessage));

            WriteField(stream, DiscoveredTestMessageFieldsId.Uid, discoveredTestMessage.Uid);
            WriteField(stream, DiscoveredTestMessageFieldsId.DisplayName, discoveredTestMessage.DisplayName);
            WriteField(stream, DiscoveredTestMessageFieldsId.FilePath, discoveredTestMessage.FilePath);
            WriteField(stream, DiscoveredTestMessageFieldsId.LineNumber, discoveredTestMessage.LineNumber);
            WriteField(stream, DiscoveredTestMessageFieldsId.Namespace, discoveredTestMessage.Namespace);
            WriteField(stream, DiscoveredTestMessageFieldsId.TypeName, discoveredTestMessage.TypeName);
            WriteField(stream, DiscoveredTestMessageFieldsId.MethodName, discoveredTestMessage.MethodName);

            WriteTraitsPayload(stream, discoveredTestMessage.Traits);
        }

        // NOTE: We are able to seek only if we are using a MemoryStream
        // thus, the seek operation is fast as we are only changing the value of a property
        WriteAtPosition(stream, (int)(stream.Position - before), before - sizeof(int));
    }

    private static void WriteTraitsPayload(Stream stream, TestMetadataProperty[]? traits)
    {
        if (traits is null || traits.Length == 0)
        {
            return;
        }

        WriteShort(stream, DiscoveredTestMessageFieldsId.Traits);

        // We will reserve an int (4 bytes)
        // so that we fill the size later, once we write the payload
        WriteInt(stream, 0);

        long before = stream.Position;
        WriteInt(stream, traits.Length);
        foreach (TestMetadataProperty trait in traits)
        {
            WriteShort(stream, GetFieldCount(trait));

            WriteField(stream, TraitMessageFieldsId.Key, trait.Key);
            WriteField(stream, TraitMessageFieldsId.Value, trait.Value);
        }

        // NOTE: We are able to seek only if we are using a MemoryStream
        // thus, the seek operation is fast as we are only changing the value of a property
        WriteAtPosition(stream, (int)(stream.Position - before), before - sizeof(int));
    }

    private static ushort GetFieldCount(DiscoveredTestMessages discoveredTestMessages) =>
        (ushort)((discoveredTestMessages.ExecutionId is null ? 0 : 1) +
        (discoveredTestMessages.InstanceId is null ? 0 : 1) +
        (IsNullOrEmpty(discoveredTestMessages.DiscoveredMessages) ? 0 : 1));

    private static ushort GetFieldCount(DiscoveredTestMessage discoveredTestMessage) =>
        (ushort)((discoveredTestMessage.Uid is null ? 0 : 1) +
        (discoveredTestMessage.DisplayName is null ? 0 : 1) +
        (discoveredTestMessage.FilePath is null ? 0 : 1) +
        (discoveredTestMessage.LineNumber is null ? 0 : 1) +
        (discoveredTestMessage.Namespace is null ? 0 : 1) +
        (discoveredTestMessage.TypeName is null ? 0 : 1) +
        (discoveredTestMessage.MethodName is null ? 0 : 1) +
        (IsNullOrEmpty(discoveredTestMessage.Traits) ? 0 : 1));

    private static ushort GetFieldCount(TestMetadataProperty trait) =>
        (ushort)((trait.Key is null ? 0 : 1) +
        (trait.Value is null ? 0 : 1));
}
