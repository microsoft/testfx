// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

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

        |---DiscoveredTestMessageList[0].ParameterTypeFullNames Id---| (2 bytes)
        |---DiscoveredTestMessageList[0].ParameterTypeFullNames Size---| (4 bytes)
        |---DiscoveredTestMessageList[0].ParameterTypeFullNames Value---| (n bytes)
            |---DiscoveredTestMessageList[0].ParameterTypeFullNames Length---| (4 bytes)

            |---DiscoveredTestMessageList[0].ParameterTypeFullNames[0].Key Size---| (4 bytes)
            |---DiscoveredTestMessageList[0].ParameterTypeFullNames[0].Key Value---| (n bytes)
*/

internal sealed class DiscoveredTestMessagesSerializer : NamedPipeSerializer<DiscoveredTestMessages>, INamedPipeSerializer
{
    public override int Id => DiscoveredTestMessagesFieldsId.MessagesSerializerId;

    protected override DiscoveredTestMessages DeserializeCore(Stream stream)
    {
        string? executionId = null;
        string? instanceId = null;
        DiscoveredTestMessage[]? discoveredTestMessages = [];

        ReadFields(stream, (fieldId, fieldSize) =>
        {
            if (TryReadExecutionScopedField(stream, fieldId, fieldSize, ref executionId, ref instanceId))
            {
                return true;
            }

            if (fieldId == DiscoveredTestMessagesFieldsId.DiscoveredTestMessageList)
            {
                discoveredTestMessages = ReadDiscoveredTestMessagesPayload(stream);
                return true;
            }

            return false;
        });

        return new(executionId, instanceId, discoveredTestMessages);
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
            TraitMessage[] traits = [];
            string[] parameterTypeFullNames = [];

            ReadFields(stream, (fieldId, fieldSize) =>
            {
                switch (fieldId)
                {
                    case DiscoveredTestMessageFieldsId.Uid:
                        uid = ReadStringValue(stream, fieldSize);
                        return true;

                    case DiscoveredTestMessageFieldsId.DisplayName:
                        displayName = ReadStringValue(stream, fieldSize);
                        return true;

                    case DiscoveredTestMessageFieldsId.FilePath:
                        filePath = ReadStringValue(stream, fieldSize);
                        return true;

                    case DiscoveredTestMessageFieldsId.LineNumber:
                        lineNumber = ReadInt(stream);
                        return true;

                    case DiscoveredTestMessageFieldsId.Namespace:
                        @namespace = ReadStringValue(stream, fieldSize);
                        return true;

                    case DiscoveredTestMessageFieldsId.TypeName:
                        typeName = ReadStringValue(stream, fieldSize);
                        return true;

                    case DiscoveredTestMessageFieldsId.MethodName:
                        methodName = ReadStringValue(stream, fieldSize);
                        return true;

                    case DiscoveredTestMessageFieldsId.Traits:
                        traits = ReadTraitsPayload(stream);
                        return true;

                    case DiscoveredTestMessageFieldsId.ParameterTypeFullNames:
                        parameterTypeFullNames = ReadParameterTypeFullNamesPayload(stream);
                        return true;

                    default:
                        return false;
                }
            });

            discoveredTestMessages[i] = new DiscoveredTestMessage(uid, displayName, filePath, lineNumber, @namespace, typeName, methodName, parameterTypeFullNames, traits);
        }

        return discoveredTestMessages;
    }

    private static string[] ReadParameterTypeFullNamesPayload(Stream stream)
    {
        int length = ReadInt(stream);
        string[] parameterTypeFullNames = new string[length];

        for (int i = 0; i < length; i++)
        {
            parameterTypeFullNames[i] = ReadString(stream);
        }

        return parameterTypeFullNames;
    }

    private static TraitMessage[] ReadTraitsPayload(Stream stream)
    {
        int length = ReadInt(stream);
        var traits = new TraitMessage[length];
        for (int i = 0; i < length; i++)
        {
            string? key = null;
            string? value = null;

            ReadFields(stream, (fieldId, fieldSize) =>
            {
                switch (fieldId)
                {
                    case TraitMessageFieldsId.Key:
                        key = ReadStringValue(stream, fieldSize);
                        return true;

                    case TraitMessageFieldsId.Value:
                        value = ReadStringValue(stream, fieldSize);
                        return true;

                    default:
                        return false;
                }
            });

            _ = key ?? throw new InvalidOperationException("Trait key is required.");
            _ = value ?? throw new InvalidOperationException("Trait value is required.");
            traits[i] = new TraitMessage(key, value);
        }

        return traits;
    }

    protected override void SerializeCore(DiscoveredTestMessages objectToSerialize, Stream stream)
    {
        WriteExecutionScopedHeader(
            stream,
            objectToSerialize.ExecutionId,
            objectToSerialize.InstanceId,
            (ushort)(IsNullOrEmpty(objectToSerialize.DiscoveredMessages) ? 0 : 1));

        WriteDiscoveredTestMessagesPayload(stream, objectToSerialize.DiscoveredMessages);
    }

    private static void WriteDiscoveredTestMessagesPayload(Stream stream, DiscoveredTestMessage[]? discoveredTestMessageList)
        => WriteListPayload(stream, DiscoveredTestMessagesFieldsId.DiscoveredTestMessageList, discoveredTestMessageList, static (s, discoveredTestMessage) =>
        {
            WriteUShort(s, GetFieldCount(discoveredTestMessage));

            WriteField(s, DiscoveredTestMessageFieldsId.Uid, discoveredTestMessage.Uid);
            WriteField(s, DiscoveredTestMessageFieldsId.DisplayName, discoveredTestMessage.DisplayName);
            WriteField(s, DiscoveredTestMessageFieldsId.FilePath, discoveredTestMessage.FilePath);
            WriteField(s, DiscoveredTestMessageFieldsId.LineNumber, discoveredTestMessage.LineNumber);
            WriteField(s, DiscoveredTestMessageFieldsId.Namespace, discoveredTestMessage.Namespace);
            WriteField(s, DiscoveredTestMessageFieldsId.TypeName, discoveredTestMessage.TypeName);
            WriteField(s, DiscoveredTestMessageFieldsId.MethodName, discoveredTestMessage.MethodName);
            WriteParameterTypeFullNamesPayload(s, discoveredTestMessage.ParameterTypeFullNames);
            WriteTraitsPayload(s, discoveredTestMessage.Traits);
        });

    private static void WriteTraitsPayload(Stream stream, TraitMessage[]? traits)
        => WriteListPayload(stream, DiscoveredTestMessageFieldsId.Traits, traits, static (s, trait) =>
        {
            WriteUShort(s, GetFieldCount(trait));

            WriteField(s, TraitMessageFieldsId.Key, trait.Key);
            WriteField(s, TraitMessageFieldsId.Value, trait.Value);
        });

    private static void WriteParameterTypeFullNamesPayload(Stream stream, string[]? parameterTypeFullNames)
        => WriteListPayload(stream, DiscoveredTestMessageFieldsId.ParameterTypeFullNames, parameterTypeFullNames, static (s, parameterTypeFullName) => WriteString(s, parameterTypeFullName));

    private static ushort GetFieldCount(DiscoveredTestMessage discoveredTestMessage) =>
        (ushort)((discoveredTestMessage.Uid is null ? 0 : 1) +
        (discoveredTestMessage.DisplayName is null ? 0 : 1) +
        (discoveredTestMessage.FilePath is null ? 0 : 1) +
        (discoveredTestMessage.LineNumber is null ? 0 : 1) +
        (discoveredTestMessage.Namespace is null ? 0 : 1) +
        (discoveredTestMessage.TypeName is null ? 0 : 1) +
        (discoveredTestMessage.MethodName is null ? 0 : 1) +
        (IsNullOrEmpty(discoveredTestMessage.ParameterTypeFullNames) ? 0 : 1) +
        (IsNullOrEmpty(discoveredTestMessage.Traits) ? 0 : 1));

    private static ushort GetFieldCount(TraitMessage trait) =>
        (ushort)((trait.Key is null ? 0 : 1) +
        (trait.Value is null ? 0 : 1));
}
