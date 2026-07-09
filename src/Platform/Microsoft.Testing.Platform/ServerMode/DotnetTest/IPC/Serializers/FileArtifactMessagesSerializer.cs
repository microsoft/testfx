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

    |---FileArtifactMessageList Id---| (2 bytes)
    |---FileArtifactMessageList Size---| (4 bytes)
    |---FileArtifactMessageList Value---| (n bytes)
        |---FileArtifactMessageList Length---| (4 bytes)

        |---FileArtifactMessageList[0] FieldCount---| 2 bytes

        |---FileArtifactMessageList[0].FullPath Id---| (2 bytes)
        |---FileArtifactMessageList[0].FullPath Size---| (4 bytes)
        |---FileArtifactMessageList[0].FullPath Value---| (n bytes)

        |---FileArtifactMessageList[0].DisplayName Id---| (2 bytes)
        |---FileArtifactMessageList[0].DisplayName Size---| (4 bytes)
        |---FileArtifactMessageList[0].DisplayName Value---| (n bytes)

        |---FileArtifactMessageList[0].Description Id---| (2 bytes)
        |---FileArtifactMessageList[0].Description Size---| (4 bytes)
        |---FileArtifactMessageList[0].Description Value---| (n bytes)

        |---FileArtifactMessageList[0].TestUid Id---| (2 bytes)
        |---FileArtifactMessageList[0].TestUid Size---| (4 bytes)
        |---FileArtifactMessageList[0].TestUid Value---| (n bytes)

        |---FileArtifactMessageList[0].TestDisplayName Id---| (2 bytes)
        |---FileArtifactMessageList[0].TestDisplayName Size---| (4 bytes)
        |---FileArtifactMessageList[0].TestDisplayName Value---| (n bytes)

        |---FileArtifactMessageList[0].SessionUid Id---| (2 bytes)
        |---FileArtifactMessageList[0].SessionUid Size---| (4 bytes)
        |---FileArtifactMessageList[0].SessionUid Value---| (n bytes)
    */

internal sealed class FileArtifactMessagesSerializer : NamedPipeSerializer<FileArtifactMessages>, INamedPipeSerializer
{
    public override int Id => FileArtifactMessagesFieldsId.MessagesSerializerId;

    protected override FileArtifactMessages DeserializeCore(Stream stream)
    {
        string? executionId = null;
        string? instanceId = null;
        FileArtifactMessage[] fileArtifactMessages = [];

        ReadFields(stream, (fieldId, fieldSize) =>
        {
            switch (fieldId)
            {
                case FileArtifactMessagesFieldsId.ExecutionId:
                    executionId = ReadStringValue(stream, fieldSize);
                    return true;

                case FileArtifactMessagesFieldsId.InstanceId:
                    instanceId = ReadStringValue(stream, fieldSize);
                    return true;

                case FileArtifactMessagesFieldsId.FileArtifactMessageList:
                    fileArtifactMessages = ReadFileArtifactMessagesPayload(stream);
                    return true;

                default:
                    return false;
            }
        });

        return new(executionId, instanceId, fileArtifactMessages);
    }

    private static FileArtifactMessage[] ReadFileArtifactMessagesPayload(Stream stream)
    {
        int length = ReadInt(stream);
        var fileArtifactMessages = new FileArtifactMessage[length];

        for (int i = 0; i < length; i++)
        {
            string? fullPath = null, displayName = null, description = null, testUid = null, testDisplayName = null, sessionUid = null;

            ReadFields(stream, (fieldId, fieldSize) =>
            {
                switch (fieldId)
                {
                    case FileArtifactMessageFieldsId.FullPath:
                        fullPath = ReadStringValue(stream, fieldSize);
                        return true;

                    case FileArtifactMessageFieldsId.DisplayName:
                        displayName = ReadStringValue(stream, fieldSize);
                        return true;

                    case FileArtifactMessageFieldsId.Description:
                        description = ReadStringValue(stream, fieldSize);
                        return true;

                    case FileArtifactMessageFieldsId.TestUid:
                        testUid = ReadStringValue(stream, fieldSize);
                        return true;

                    case FileArtifactMessageFieldsId.TestDisplayName:
                        testDisplayName = ReadStringValue(stream, fieldSize);
                        return true;

                    case FileArtifactMessageFieldsId.SessionUid:
                        sessionUid = ReadStringValue(stream, fieldSize);
                        return true;

                    default:
                        return false;
                }
            });

            fileArtifactMessages[i] = new FileArtifactMessage(fullPath, displayName, description, testUid, testDisplayName, sessionUid);
        }

        return fileArtifactMessages;
    }

    protected override void SerializeCore(FileArtifactMessages objectToSerialize, Stream stream)
    {
        DebugAssert(stream.CanSeek, "We expect a seekable stream.");

        WriteUShort(stream, GetFieldCount(objectToSerialize));

        WriteField(stream, FileArtifactMessagesFieldsId.ExecutionId, objectToSerialize.ExecutionId);
        WriteField(stream, FileArtifactMessagesFieldsId.InstanceId, objectToSerialize.InstanceId);
        WriteFileArtifactMessagesPayload(stream, objectToSerialize.FileArtifacts);
    }

    private static void WriteFileArtifactMessagesPayload(Stream stream, FileArtifactMessage[]? fileArtifactMessageList)
        => WriteListPayload(stream, FileArtifactMessagesFieldsId.FileArtifactMessageList, fileArtifactMessageList, static (s, fileArtifactMessage) =>
        {
            WriteUShort(s, GetFieldCount(fileArtifactMessage));

            WriteField(s, FileArtifactMessageFieldsId.FullPath, fileArtifactMessage.FullPath);
            WriteField(s, FileArtifactMessageFieldsId.DisplayName, fileArtifactMessage.DisplayName);
            WriteField(s, FileArtifactMessageFieldsId.Description, fileArtifactMessage.Description);
            WriteField(s, FileArtifactMessageFieldsId.TestUid, fileArtifactMessage.TestUid);
            WriteField(s, FileArtifactMessageFieldsId.TestDisplayName, fileArtifactMessage.TestDisplayName);
            WriteField(s, FileArtifactMessageFieldsId.SessionUid, fileArtifactMessage.SessionUid);
        });

    private static ushort GetFieldCount(FileArtifactMessages fileArtifactMessages) =>
        (ushort)((fileArtifactMessages.ExecutionId is null ? 0 : 1) +
        (fileArtifactMessages.InstanceId is null ? 0 : 1) +
        (IsNullOrEmpty(fileArtifactMessages.FileArtifacts) ? 0 : 1));

    private static ushort GetFieldCount(FileArtifactMessage fileArtifactMessage) =>
        (ushort)((fileArtifactMessage.FullPath is null ? 0 : 1) +
        (fileArtifactMessage.DisplayName is null ? 0 : 1) +
        (fileArtifactMessage.Description is null ? 0 : 1) +
        (fileArtifactMessage.TestUid is null ? 0 : 1) +
        (fileArtifactMessage.TestDisplayName is null ? 0 : 1) +
        (fileArtifactMessage.SessionUid is null ? 0 : 1));
}
