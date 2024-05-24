// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.IPC.Models;

namespace Microsoft.Testing.Platform.IPC.Serializers;

internal sealed class CommandLineOptionMessagesSerializer : BaseSerializer, INamedPipeSerializer
{
    public int Id => 3;

    public object Deserialize(Stream stream)
    {
        string moduleName = string.Empty;
        List<CommandLineOptionMessage>? commandLineOptionMessages = null;

        short fieldCount = ReadShort(stream);

        for (int i = 0; i < fieldCount; i++)
        {
            int fieldId = ReadShort(stream);
            var fieldSize = ReadInt(stream);

            switch (fieldId)
            {
                case CommandLineOptionMessagesFieldsId.ModuleName:
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
            string name = string.Empty, description = string.Empty, arity = string.Empty;
            bool isHidden = false, isBuiltIn = false;

            int fieldCount = ReadShort(stream);

            for (int j = 0; j < fieldCount; j++)
            {
                int fieldId = ReadShort(stream);
                var fieldSize = ReadInt(stream);

                switch (fieldId)
                {
                    case CommandLineOptionMessageFieldsId.Name:
                        name = ReadString(stream);
                        break;

                    case CommandLineOptionMessageFieldsId.Description:
                        description = ReadString(stream);
                        break;

                    case CommandLineOptionMessageFieldsId.Arity:
                        arity = ReadString(stream);
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

            commandLineOptionMessages.Add(new CommandLineOptionMessage(name, description, arity, isHidden, isBuiltIn));
        }

        return commandLineOptionMessages;
    }

    public void Serialize(object objectToSerialize, Stream stream)
    {
        RoslynDebug.Assert(stream.CanSeek, "We expect a seekable stream.");

        CommandLineOptionMessages commandLineOptionMessages = (CommandLineOptionMessages)objectToSerialize;

        WriteShort(stream, GetFieldCount(commandLineOptionMessages));

        WriteField(stream, CommandLineOptionMessagesFieldsId.ModuleName, commandLineOptionMessages.ModuleName);
        WriteCommandLineOptionMessagesPayload(stream, commandLineOptionMessages.CommandLineOptionMessageList);
    }

    private static void WriteCommandLineOptionMessagesPayload(Stream stream, CommandLineOptionMessage[] commandLineOptionMessageList)
    {
        if (IsNull(commandLineOptionMessageList))
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
            WriteField(stream, CommandLineOptionMessageFieldsId.Arity, commandLineOptionMessage.Arity);
            WriteField(stream, CommandLineOptionMessageFieldsId.IsHidden, commandLineOptionMessage.IsHidden);
            WriteField(stream, CommandLineOptionMessageFieldsId.IsBuiltIn, commandLineOptionMessage.IsBuiltIn);
        }

        // NOTE: We are able to seek only if we are using a MemoryStream
        // thus, the seek operation is fast as we are only changing the value of a property
        WriteAtPosition(stream, (int)(stream.Position - before), before - sizeof(int));
    }

    private static short GetFieldCount(CommandLineOptionMessages commandLineOptionMessages)
    {
        return (short)((RoslynString.IsNullOrEmpty(commandLineOptionMessages.ModuleName) ? 0 : 1) +
           (commandLineOptionMessages is null ? 0 : 1));
    }

    private static short GetFieldCount(CommandLineOptionMessage commandLineOptionMessage)
    {
        return (short)((short)(RoslynString.IsNullOrEmpty(commandLineOptionMessage.Name) ? 0 : 1) +
            (short)(RoslynString.IsNullOrEmpty(commandLineOptionMessage.Description) ? 0 : 1) +
            (short)(RoslynString.IsNullOrEmpty(commandLineOptionMessage.Arity) ? 0 : 1) +
            1 +
            1);
    }

    private static bool IsNull<T>(T[] items)
    {
        return items is null || items.Length == 0;
    }
}
