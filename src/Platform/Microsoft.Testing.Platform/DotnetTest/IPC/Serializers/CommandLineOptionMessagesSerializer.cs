﻿// Copyright (c) Microsoft Corporation. All rights reserved.
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

    |---CommandLineOptionMessageList[0] Name Id---| (2 bytes)
    |---CommandLineOptionMessageList[0] Name Size---| (4 bytes)
    |---CommandLineOptionMessageList[0] Name Value---| (n bytes)

    |---CommandLineOptionMessageList[1] Description Id---| (2 bytes)
    |---CommandLineOptionMessageList[1] Description Size---| (4 bytes)
    |---CommandLineOptionMessageList[1] Description Value---| (n bytes)

    |---CommandLineOptionMessageList[3] IsHidden Id---| (2 bytes)
    |---CommandLineOptionMessageList[3] IsHidden Size---| (4 bytes)
    |---CommandLineOptionMessageList[3] IsHidden Value---| (1 byte)

    |---CommandLineOptionMessageList[4] IsBuiltIn Id---| (2 bytes)
    |---CommandLineOptionMessageList[4] IsBuiltIn Size---| (4 bytes)
    |---CommandLineOptionMessageList[4] IsBuiltIn Value---| (1 byte)
*/

internal sealed class CommandLineOptionMessagesSerializer : BaseSerializer, INamedPipeSerializer
{
    public int Id => SerializerIds.CommandLineOptionMessagesSerializer;

    public object Deserialize(Stream stream)
    {
        string? moduleName = null;
        List<CommandLineOptionMessage>? commandLineOptionMessages = null;

        ushort fieldCount = ReadShort(stream);

        for (int i = 0; i < fieldCount; i++)
        {
            int fieldId = ReadShort(stream);
            int fieldSize = ReadInt(stream);

            switch (fieldId)
            {
                case CommandLineOptionMessagesFieldsId.ModulePath:
                    moduleName = ReadString(stream);
                    break;

                case CommandLineOptionMessagesFieldsId.CommandLineOptionMessageList:
                    commandLineOptionMessages = ReadCommandLineOptionMessagesPayload(stream);
                    break;

                default:
                    // If we don't recognize the field id, skip the payload corresponding to that field
                    SetPosition(stream, stream.Position + fieldSize);
                    break;
            }
        }

        return new CommandLineOptionMessages(moduleName, commandLineOptionMessages is null ? [] : [.. commandLineOptionMessages]);
    }

    private static List<CommandLineOptionMessage> ReadCommandLineOptionMessagesPayload(Stream stream)
    {
        List<CommandLineOptionMessage> commandLineOptionMessages = [];

        int length = ReadInt(stream);
        for (int i = 0; i < length; i++)
        {
            string? name = null, description = null;
            bool? isHidden = null, isBuiltIn = null;

            int fieldCount = ReadShort(stream);

            for (int j = 0; j < fieldCount; j++)
            {
                int fieldId = ReadShort(stream);
                int fieldSize = ReadInt(stream);

                switch (fieldId)
                {
                    case CommandLineOptionMessageFieldsId.Name:
                        name = ReadString(stream);
                        break;

                    case CommandLineOptionMessageFieldsId.Description:
                        description = ReadString(stream);
                        break;

                    case CommandLineOptionMessageFieldsId.IsHidden:
                        isHidden = ReadBool(stream);
                        break;

                    case CommandLineOptionMessageFieldsId.IsBuiltIn:
                        isBuiltIn = ReadBool(stream);
                        break;

                    default:
                        SetPosition(stream, stream.Position + fieldSize);
                        break;
                }
            }

            commandLineOptionMessages.Add(new CommandLineOptionMessage(name, description, isHidden, isBuiltIn));
        }

        return commandLineOptionMessages;
    }

    public void Serialize(object objectToSerialize, Stream stream)
    {
        RoslynDebug.Assert(stream.CanSeek, "We expect a seekable stream.");

        var commandLineOptionMessages = (CommandLineOptionMessages)objectToSerialize;

        WriteShort(stream, GetFieldCount(commandLineOptionMessages));

        WriteField(stream, CommandLineOptionMessagesFieldsId.ModulePath, commandLineOptionMessages.ModulePath);
        WriteCommandLineOptionMessagesPayload(stream, commandLineOptionMessages.CommandLineOptionMessageList);
    }

    private static void WriteCommandLineOptionMessagesPayload(Stream stream, CommandLineOptionMessage[]? commandLineOptionMessageList)
    {
        if (commandLineOptionMessageList is null || commandLineOptionMessageList.Length == 0)
        {
            return;
        }

        WriteShort(stream, CommandLineOptionMessagesFieldsId.CommandLineOptionMessageList);

        // We will reserve an int (4 bytes)
        // so that we fill the size later, once we write the payload
        WriteInt(stream, 0);

        long before = stream.Position;
        WriteInt(stream, commandLineOptionMessageList.Length);
        foreach (CommandLineOptionMessage commandLineOptionMessage in commandLineOptionMessageList)
        {
            WriteShort(stream, GetFieldCount(commandLineOptionMessage));

            WriteField(stream, CommandLineOptionMessageFieldsId.Name, commandLineOptionMessage.Name);
            WriteField(stream, CommandLineOptionMessageFieldsId.Description, commandLineOptionMessage.Description);
            WriteField(stream, CommandLineOptionMessageFieldsId.IsHidden, commandLineOptionMessage.IsHidden);
            WriteField(stream, CommandLineOptionMessageFieldsId.IsBuiltIn, commandLineOptionMessage.IsBuiltIn);
        }

        // NOTE: We are able to seek only if we are using a MemoryStream
        // thus, the seek operation is fast as we are only changing the value of a property
        WriteAtPosition(stream, (int)(stream.Position - before), before - sizeof(int));
    }

    private static ushort GetFieldCount(CommandLineOptionMessages commandLineOptionMessages) =>
        (ushort)((commandLineOptionMessages.ModulePath is null ? 0 : 1) +
        (commandLineOptionMessages is null ? 0 : 1));

    private static ushort GetFieldCount(CommandLineOptionMessage commandLineOptionMessage) =>
        (ushort)((commandLineOptionMessage.Name is null ? 0 : 1) +
        (commandLineOptionMessage.Description is null ? 0 : 1) +
        2);
}
