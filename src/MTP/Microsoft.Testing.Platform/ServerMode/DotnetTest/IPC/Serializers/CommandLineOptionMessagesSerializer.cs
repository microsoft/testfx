// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.IPC.Models;

namespace Microsoft.Testing.Platform.IPC.Serializers;

/*
   |---FieldCount---| 2 bytes

   |---ModuleName Id---| (2 bytes)
   |---ModuleName Size---| (4 bytes)
   |---ModuleName Value---| (n bytes)

   |---CommandLineOptionMessageList Id---| (2 bytes)
   |---CommandLineOptionMessageList Size---| (4 bytes)
   |---CommandLineOptionMessageList Value---| (n bytes)
       |---CommandLineOptionMessageList Length---| (4 bytes)

       |---CommandLineOptionMessageList[0] FieldCount---| 2 bytes

       |---CommandLineOptionMessageList[0].Name Id---| (2 bytes)
       |---CommandLineOptionMessageList[0].Name Size---| (4 bytes)
       |---CommandLineOptionMessageList[0].Name Value---| (n bytes)

       |---CommandLineOptionMessageList[0].Description Id---| (2 bytes)
       |---CommandLineOptionMessageList[0].Description Size---| (4 bytes)
       |---CommandLineOptionMessageList[0].Description Value---| (n bytes)

       |---CommandLineOptionMessageList[0].IsHidden Id---| (2 bytes)
       |---CommandLineOptionMessageList[0].IsHidden Size---| (4 bytes)
       |---CommandLineOptionMessageList[0].IsHidden Value---| (1 byte)

       |---CommandLineOptionMessageList[0].IsBuiltIn Id---| (2 bytes)
       |---CommandLineOptionMessageList[0].IsBuiltIn Size---| (4 bytes)
       |---CommandLineOptionMessageList[0].IsBuiltIn Value---| (1 byte)

       |---CommandLineOptionMessageList[0].ProviderUid Id---| (2 bytes)
       |---CommandLineOptionMessageList[0].ProviderUid Size---| (4 bytes)
       |---CommandLineOptionMessageList[0].ProviderUid Value---| (n bytes)

       |---CommandLineOptionMessageList[0].MinimumArity Id---| (2 bytes)
       |---CommandLineOptionMessageList[0].MinimumArity Size---| (4 bytes)
       |---CommandLineOptionMessageList[0].MinimumArity Value---| (4 bytes)

       |---CommandLineOptionMessageList[0].MaximumArity Id---| (2 bytes)
       |---CommandLineOptionMessageList[0].MaximumArity Size---| (4 bytes)
       |---CommandLineOptionMessageList[0].MaximumArity Value---| (4 bytes)
   */

internal sealed class CommandLineOptionMessagesSerializer : NamedPipeSerializer<CommandLineOptionMessages>, INamedPipeSerializer
{
    public override int Id => CommandLineOptionMessagesFieldsId.MessagesSerializerId;

    protected override CommandLineOptionMessages DeserializeCore(Stream stream)
    {
        string? moduleName = null;
        CommandLineOptionMessage[]? commandLineOptionMessages = [];

        ReadFields(stream, (fieldId, fieldSize) =>
        {
            switch (fieldId)
            {
                case CommandLineOptionMessagesFieldsId.ModulePath:
                    moduleName = ReadStringValue(stream, fieldSize);
                    return true;

                case CommandLineOptionMessagesFieldsId.CommandLineOptionMessageList:
                    commandLineOptionMessages = ReadFieldPayload(stream, fieldSize, ReadCommandLineOptionMessagesPayload);
                    return true;

                default:
                    return false;
            }
        });

        return new(moduleName, commandLineOptionMessages);
    }

    private static CommandLineOptionMessage[] ReadCommandLineOptionMessagesPayload(Stream stream)
    {
        int length = ReadCollectionLength(stream, sizeof(ushort));
        var commandLineOptionMessages = new CommandLineOptionMessage[length];

        for (int i = 0; i < length; i++)
        {
            string? name = null, description = null, providerUid = null;
            bool? isHidden = null, isBuiltIn = null;
            int? minimumArity = null, maximumArity = null;

            ReadFields(stream, (fieldId, fieldSize) =>
            {
                switch (fieldId)
                {
                    case CommandLineOptionMessageFieldsId.Name:
                        name = ReadStringValue(stream, fieldSize);
                        return true;

                    case CommandLineOptionMessageFieldsId.Description:
                        description = ReadStringValue(stream, fieldSize);
                        return true;

                    case CommandLineOptionMessageFieldsId.IsHidden:
                        isHidden = ReadBool(stream);
                        return true;

                    case CommandLineOptionMessageFieldsId.IsBuiltIn:
                        isBuiltIn = ReadBool(stream);
                        return true;

                    case CommandLineOptionMessageFieldsId.ProviderUid:
                        providerUid = ReadStringValue(stream, fieldSize);
                        return true;

                    case CommandLineOptionMessageFieldsId.MinimumArity:
                        minimumArity = ReadInt(stream);
                        return true;

                    case CommandLineOptionMessageFieldsId.MaximumArity:
                        maximumArity = ReadInt(stream);
                        return true;

                    default:
                        return false;
                }
            });

            commandLineOptionMessages[i] = new CommandLineOptionMessage(
                name,
                description,
                isHidden,
                isBuiltIn,
                providerUid,
                minimumArity,
                maximumArity);
        }

        return commandLineOptionMessages;
    }

    protected override void SerializeCore(CommandLineOptionMessages objectToSerialize, Stream stream)
    {
        DebugAssert(stream.CanSeek, "We expect a seekable stream.");

        WriteUShort(stream, GetFieldCount(objectToSerialize));

        WriteField(stream, CommandLineOptionMessagesFieldsId.ModulePath, objectToSerialize.ModulePath);
        WriteCommandLineOptionMessagesPayload(stream, objectToSerialize.CommandLineOptionMessageList);
    }

    private static void WriteCommandLineOptionMessagesPayload(Stream stream, CommandLineOptionMessage[]? commandLineOptionMessageList)
        => WriteListPayload(stream, CommandLineOptionMessagesFieldsId.CommandLineOptionMessageList, commandLineOptionMessageList, static (s, commandLineOptionMessage) =>
        {
            WriteUShort(s, GetFieldCount(commandLineOptionMessage));

            WriteField(s, CommandLineOptionMessageFieldsId.Name, commandLineOptionMessage.Name);
            WriteField(s, CommandLineOptionMessageFieldsId.Description, commandLineOptionMessage.Description);
            WriteField(s, CommandLineOptionMessageFieldsId.IsHidden, commandLineOptionMessage.IsHidden);
            WriteField(s, CommandLineOptionMessageFieldsId.IsBuiltIn, commandLineOptionMessage.IsBuiltIn);
            WriteField(s, CommandLineOptionMessageFieldsId.ProviderUid, commandLineOptionMessage.ProviderUid);
            WriteField(s, CommandLineOptionMessageFieldsId.MinimumArity, commandLineOptionMessage.MinimumArity);
            WriteField(s, CommandLineOptionMessageFieldsId.MaximumArity, commandLineOptionMessage.MaximumArity);
        });

    private static ushort GetFieldCount(CommandLineOptionMessages commandLineOptionMessages) =>
        (ushort)((commandLineOptionMessages.ModulePath is null ? 0 : 1) +
        (IsNullOrEmpty(commandLineOptionMessages.CommandLineOptionMessageList) ? 0 : 1));

    private static ushort GetFieldCount(CommandLineOptionMessage commandLineOptionMessage) =>
        (ushort)((commandLineOptionMessage.Name is null ? 0 : 1) +
        (commandLineOptionMessage.Description is null ? 0 : 1) +
        (commandLineOptionMessage.IsHidden is null ? 0 : 1) +
        (commandLineOptionMessage.IsBuiltIn is null ? 0 : 1) +
        (commandLineOptionMessage.ProviderUid is null ? 0 : 1) +
        (commandLineOptionMessage.MinimumArity is null ? 0 : 1) +
        (commandLineOptionMessage.MaximumArity is null ? 0 : 1));
}
