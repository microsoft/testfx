﻿// Copyright (c) Microsoft Corporation. All rights reserved.
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

  |---SuccessfulTestMessageList Id---| (2 bytes)
  |---SuccessfulTestMessageList Size---| (4 bytes)
  |---SuccessfulTestMessageList Value---| (n bytes)
      |---SuccessfulTestMessageList Length---| (4 bytes)

      |---SuccessfulTestMessageList[0] FieldCount---| 2 bytes

      |---SuccessfulTestMessageList[0].Uid Id---| (2 bytes)
      |---SuccessfulTestMessageList[0].Uid Size---| (4 bytes)
      |---SuccessfulTestMessageList[0].Uid Value---| (n bytes)

      |---SuccessfulTestMessageList[0].DisplayName Id---| (2 bytes)
      |---SuccessfulTestMessageList[0].DisplayName Size---| (4 bytes)
      |---SuccessfulTestMessageList[0].DisplayName Value---| (n bytes)

      |---SuccessfulTestMessageList[0].State Id---| (2 bytes)
      |---SuccessfulTestMessageList[0].State Size---| (1 byte)
      |---SuccessfulTestMessageList[0].State Value---| (n bytes)

      |---SuccessfulTestMessageList[0].Duration Id---| (2 bytes)
      |---SuccessfulTestMessageList[0].Duration Size---| (8 bytes)
      |---SuccessfulTestMessageList[0].Duration Value---| (n bytes)

      |---SuccessfulTestMessageList[0].Reason Id---| (2 bytes)
      |---SuccessfulTestMessageList[0].Reason Size---| (4 bytes)
      |---SuccessfulTestMessageList[0].Reason Value---| (n bytes)

      |---SuccessfulTestMessageList[0].StandardOutput Id---| (2 bytes)
      |---SuccessfulTestMessageList[0].StandardOutput Size---| (4 bytes)
      |---SuccessfulTestMessageList[0].StandardOutput Value---| (n bytes)

      |---SuccessfulTestMessageList[0].StandardError Id---| (2 bytes)
      |---SuccessfulTestMessageList[0].StandardError Size---| (4 bytes)
      |---SuccessfulTestMessageList[0].StandardError Value---| (n bytes)

      |---SuccessfulTestMessageList[0].SessionUid Id---| (2 bytes)
      |---SuccessfulTestMessageList[0].SessionUid Size---| (4 bytes)
      |---SuccessfulTestMessageList[0].SessionUid Value---| (n bytes)

  |---FailedTestMessageList Id---| (2 bytes)
  |---FailedTestMessageList Size---| (4 bytes)
  |---FailedTestMessageList Value---| (n bytes)
      |---FailedTestMessageList Length---| (4 bytes)

      |---FailedTestMessageList[0] FieldCount---| 2 bytes

      |---FailedTestMessageList[0].Uid Id---| (2 bytes)
      |---FailedTestMessageList[0].Uid Size---| (4 bytes)
      |---FailedTestMessageList[0].Uid Value---| (n bytes)

      |---FailedTestMessageList[0].DisplayName Id---| (2 bytes)
      |---FailedTestMessageList[0].DisplayName Size---| (4 bytes)
      |---FailedTestMessageList[0].DisplayName Value---| (n bytes)

      |---FailedTestMessageList[0].State Id---| (2 bytes)
      |---FailedTestMessageList[0].State Size---| (1 byte)
      |---FailedTestMessageList[0].State Value---| (n bytes)

      |---SuccessfulTestMessageList[0].Duration Id---| (2 bytes)
      |---SuccessfulTestMessageList[0].Duration Size---| (8 bytes)
      |---SuccessfulTestMessageList[0].Duration Value---| (n bytes)

      |---FailedTestMessageList[0].Reason Id---| (2 bytes)
      |---FailedTestMessageList[0].Reason Size---| (4 bytes)
      |---FailedTestMessageList[0].Reason Value---| (n bytes)

      |---FailedTestMessageList[0].ErrorMessage Id---| (2 bytes)
      |---FailedTestMessageList[0].ErrorMessage Size---| (4 bytes)
      |---FailedTestMessageList[0].ErrorMessage Value---| (n bytes)

      |---FailedTestMessageList[0].ErrorStackTrace Id---| (2 bytes)
      |---FailedTestMessageList[0].ErrorStackTrace Size---| (4 bytes)
      |---FailedTestMessageList[0].ErrorStackTrace Value---| (n bytes)

      |---SuccessfulTestMessageList[0].StandardOutput Id---| (2 bytes)
      |---SuccessfulTestMessageList[0].StandardOutput Size---| (4 bytes)
      |---SuccessfulTestMessageList[0].StandardOutput Value---| (n bytes)

      |---SuccessfulTestMessageList[0].StandardError Id---| (2 bytes)
      |---SuccessfulTestMessageList[0].StandardError Size---| (4 bytes)
      |---SuccessfulTestMessageList[0].StandardError Value---| (n bytes)

      |---FailedTestMessageList[0].SessionUid Id---| (2 bytes)
      |---FailedTestMessageList[0].SessionUid Size---| (4 bytes)
      |---FailedTestMessageList[0].SessionUid Value---| (n bytes)
  */

internal sealed class TestResultMessagesSerializer : BaseSerializer, INamedPipeSerializer
{
    public int Id => TestResultMessagesFieldsId.MessagesSerializerId;

    public object Deserialize(Stream stream)
    {
        string? executionId = null;
        string? instanceId = null;
        List<SuccessfulTestResultMessage>? successfulTestResultMessages = null;
        List<FailedTestResultMessage>? failedTestResultMessages = null;

        ushort fieldCount = ReadShort(stream);

        for (int i = 0; i < fieldCount; i++)
        {
            int fieldId = ReadShort(stream);
            int fieldSize = ReadInt(stream);

            switch (fieldId)
            {
                case TestResultMessagesFieldsId.ExecutionId:
                    executionId = ReadStringValue(stream, fieldSize);
                    break;

                case TestResultMessagesFieldsId.InstanceId:
                    instanceId = ReadStringValue(stream, fieldSize);
                    break;

                case TestResultMessagesFieldsId.SuccessfulTestMessageList:
                    successfulTestResultMessages = ReadSuccessfulTestMessagesPayload(stream);
                    break;

                case TestResultMessagesFieldsId.FailedTestMessageList:
                    failedTestResultMessages = ReadFailedTestMessagesPayload(stream);
                    break;

                default:
                    // If we don't recognize the field id, skip the payload corresponding to that field
                    SetPosition(stream, stream.Position + fieldSize);
                    break;
            }
        }

        return new TestResultMessages(
            executionId,
            instanceId,
            successfulTestResultMessages is null ? [] : [.. successfulTestResultMessages],
            failedTestResultMessages is null ? [] : [.. failedTestResultMessages]);
    }

    private static List<SuccessfulTestResultMessage> ReadSuccessfulTestMessagesPayload(Stream stream)
    {
        List<SuccessfulTestResultMessage> successfulTestResultMessages = [];

        int length = ReadInt(stream);
        for (int i = 0; i < length; i++)
        {
            string? uid = null, displayName = null, reason = null, standardOutput = null, errorOutput = null, sessionUid = null;
            byte? state = null;
            long? duration = null;

            int fieldCount = ReadShort(stream);

            for (int j = 0; j < fieldCount; j++)
            {
                int fieldId = ReadShort(stream);
                int fieldSize = ReadInt(stream);

                switch (fieldId)
                {
                    case SuccessfulTestResultMessageFieldsId.Uid:
                        uid = ReadStringValue(stream, fieldSize);
                        break;

                    case SuccessfulTestResultMessageFieldsId.DisplayName:
                        displayName = ReadStringValue(stream, fieldSize);
                        break;

                    case SuccessfulTestResultMessageFieldsId.State:
                        state = ReadByte(stream);
                        break;

                    case SuccessfulTestResultMessageFieldsId.Duration:
                        duration = ReadLong(stream);
                        break;

                    case SuccessfulTestResultMessageFieldsId.Reason:
                        reason = ReadStringValue(stream, fieldSize);
                        break;

                    case SuccessfulTestResultMessageFieldsId.StandardOutput:
                        standardOutput = ReadStringValue(stream, fieldSize);
                        break;

                    case SuccessfulTestResultMessageFieldsId.ErrorOutput:
                        errorOutput = ReadStringValue(stream, fieldSize);
                        break;

                    case SuccessfulTestResultMessageFieldsId.SessionUid:
                        sessionUid = ReadStringValue(stream, fieldSize);
                        break;

                    default:
                        SetPosition(stream, stream.Position + fieldSize);
                        break;
                }
            }

            successfulTestResultMessages.Add(new SuccessfulTestResultMessage(uid, displayName, state, duration, reason, standardOutput, errorOutput, sessionUid));
        }

        return successfulTestResultMessages;
    }

    private static List<FailedTestResultMessage> ReadFailedTestMessagesPayload(Stream stream)
    {
        List<FailedTestResultMessage> failedTestResultMessages = [];

        int length = ReadInt(stream);
        for (int i = 0; i < length; i++)
        {
            string? uid = null, displayName = null, reason = null, sessionUid = null, standardOutput = null, errorOutput = null;
            ExceptionMessage[] exceptionMessages = [];
            byte? state = null;
            long? duration = null;

            int fieldCount = ReadShort(stream);

            for (int j = 0; j < fieldCount; j++)
            {
                int fieldId = ReadShort(stream);
                int fieldSize = ReadInt(stream);

                switch (fieldId)
                {
                    case FailedTestResultMessageFieldsId.Uid:
                        uid = ReadStringValue(stream, fieldSize);
                        break;

                    case FailedTestResultMessageFieldsId.DisplayName:
                        displayName = ReadStringValue(stream, fieldSize);
                        break;

                    case FailedTestResultMessageFieldsId.State:
                        state = ReadByte(stream);
                        break;

                    case FailedTestResultMessageFieldsId.Duration:
                        duration = ReadLong(stream);
                        break;

                    case FailedTestResultMessageFieldsId.Reason:
                        reason = ReadStringValue(stream, fieldSize);
                        break;

                    case FailedTestResultMessageFieldsId.ExceptionMessageList:
                        exceptionMessages = ReadExceptionMessagesPayload(stream);
                        break;

                    case FailedTestResultMessageFieldsId.StandardOutput:
                        standardOutput = ReadStringValue(stream, fieldSize);
                        break;

                    case FailedTestResultMessageFieldsId.ErrorOutput:
                        errorOutput = ReadStringValue(stream, fieldSize);
                        break;

                    case FailedTestResultMessageFieldsId.SessionUid:
                        sessionUid = ReadStringValue(stream, fieldSize);
                        break;

                    default:
                        SetPosition(stream, stream.Position + fieldSize);
                        break;
                }
            }

            failedTestResultMessages.Add(new FailedTestResultMessage(uid, displayName, state, duration, reason, exceptionMessages, standardOutput, errorOutput, sessionUid));
        }

        return failedTestResultMessages;
    }

    private static ExceptionMessage[] ReadExceptionMessagesPayload(Stream stream)
    {
        var exceptionMessages = new List<ExceptionMessage>();

        int length = ReadInt(stream);
        for (int i = 0; i < length; i++)
        {
            int fieldCount = ReadShort(stream);

            string? errorMessage = null;
            string? errorType = null;
            string? stackTrace = null;

            for (int j = 0; j < fieldCount; j++)
            {
                int fieldId = ReadShort(stream);
                int fieldSize = ReadInt(stream);

                switch (fieldId)
                {
                    case ExceptionMessageFieldsId.ErrorMessage:
                        errorMessage = ReadStringValue(stream, fieldSize);
                        break;

                    case ExceptionMessageFieldsId.ErrorType:
                        errorType = ReadStringValue(stream, fieldSize);
                        break;

                    case ExceptionMessageFieldsId.StackTrace:
                        stackTrace = ReadStringValue(stream, fieldSize);
                        break;
                }
            }

            exceptionMessages.Add(new ExceptionMessage(errorMessage, errorType, stackTrace));
        }

        return [.. exceptionMessages];
    }

    public void Serialize(object objectToSerialize, Stream stream)
    {
        RoslynDebug.Assert(stream.CanSeek, "We expect a seekable stream.");

        var testResultMessages = (TestResultMessages)objectToSerialize;

        WriteShort(stream, GetFieldCount(testResultMessages));

        WriteField(stream, TestResultMessagesFieldsId.ExecutionId, testResultMessages.ExecutionId);
        WriteField(stream, TestResultMessagesFieldsId.InstanceId, testResultMessages.InstanceId);
        WriteSuccessfulTestMessagesPayload(stream, testResultMessages.SuccessfulTestMessages);
        WriteFailedTestMessagesPayload(stream, testResultMessages.FailedTestMessages);
    }

    private static void WriteSuccessfulTestMessagesPayload(Stream stream, SuccessfulTestResultMessage[]? successfulTestResultMessages)
    {
        if (successfulTestResultMessages is null || successfulTestResultMessages.Length == 0)
        {
            return;
        }

        WriteShort(stream, TestResultMessagesFieldsId.SuccessfulTestMessageList);

        // We will reserve an int (4 bytes)
        // so that we fill the size later, once we write the payload
        WriteInt(stream, 0);

        long before = stream.Position;
        WriteInt(stream, successfulTestResultMessages.Length);
        foreach (SuccessfulTestResultMessage successfulTestResultMessage in successfulTestResultMessages)
        {
            WriteShort(stream, GetFieldCount(successfulTestResultMessage));

            WriteField(stream, SuccessfulTestResultMessageFieldsId.Uid, successfulTestResultMessage.Uid);
            WriteField(stream, SuccessfulTestResultMessageFieldsId.DisplayName, successfulTestResultMessage.DisplayName);
            WriteField(stream, SuccessfulTestResultMessageFieldsId.State, successfulTestResultMessage.State);
            WriteField(stream, SuccessfulTestResultMessageFieldsId.Duration, successfulTestResultMessage.Duration);
            WriteField(stream, SuccessfulTestResultMessageFieldsId.Reason, successfulTestResultMessage.Reason);
            WriteField(stream, SuccessfulTestResultMessageFieldsId.StandardOutput, successfulTestResultMessage.StandardOutput);
            WriteField(stream, SuccessfulTestResultMessageFieldsId.ErrorOutput, successfulTestResultMessage.ErrorOutput);
            WriteField(stream, SuccessfulTestResultMessageFieldsId.SessionUid, successfulTestResultMessage.SessionUid);
        }

        // NOTE: We are able to seek only if we are using a MemoryStream
        // thus, the seek operation is fast as we are only changing the value of a property
        WriteAtPosition(stream, (int)(stream.Position - before), before - sizeof(int));
    }

    private static void WriteFailedTestMessagesPayload(Stream stream, FailedTestResultMessage[]? failedTestResultMessages)
    {
        if (failedTestResultMessages is null || failedTestResultMessages.Length == 0)
        {
            return;
        }

        WriteShort(stream, TestResultMessagesFieldsId.FailedTestMessageList);

        // We will reserve an int (4 bytes)
        // so that we fill the size later, once we write the payload
        WriteInt(stream, 0);

        long before = stream.Position;
        WriteInt(stream, failedTestResultMessages.Length);
        foreach (FailedTestResultMessage failedTestResultMessage in failedTestResultMessages)
        {
            WriteShort(stream, GetFieldCount(failedTestResultMessage));

            WriteField(stream, FailedTestResultMessageFieldsId.Uid, failedTestResultMessage.Uid);
            WriteField(stream, FailedTestResultMessageFieldsId.DisplayName, failedTestResultMessage.DisplayName);
            WriteField(stream, FailedTestResultMessageFieldsId.State, failedTestResultMessage.State);
            WriteField(stream, FailedTestResultMessageFieldsId.Duration, failedTestResultMessage.Duration);
            WriteField(stream, FailedTestResultMessageFieldsId.Reason, failedTestResultMessage.Reason);
            WriteExceptionMessagesPayload(stream, failedTestResultMessage.Exceptions);
            WriteField(stream, FailedTestResultMessageFieldsId.StandardOutput, failedTestResultMessage.StandardOutput);
            WriteField(stream, FailedTestResultMessageFieldsId.ErrorOutput, failedTestResultMessage.ErrorOutput);
            WriteField(stream, FailedTestResultMessageFieldsId.SessionUid, failedTestResultMessage.SessionUid);
        }

        // NOTE: We are able to seek only if we are using a MemoryStream
        // thus, the seek operation is fast as we are only changing the value of a property
        WriteAtPosition(stream, (int)(stream.Position - before), before - sizeof(int));
    }

    private static void WriteExceptionMessagesPayload(Stream stream, ExceptionMessage[]? exceptionMessages)
    {
        if (exceptionMessages is null || exceptionMessages.Length == 0)
        {
            return;
        }

        WriteShort(stream, FailedTestResultMessageFieldsId.ExceptionMessageList);

        // We will reserve an int (4 bytes)
        // so that we fill the size later, once we write the payload
        WriteInt(stream, 0);

        long before = stream.Position;
        WriteInt(stream, exceptionMessages.Length);
        foreach (ExceptionMessage exceptionMessage in exceptionMessages)
        {
            WriteShort(stream, GetFieldCount(exceptionMessage));

            WriteField(stream, ExceptionMessageFieldsId.ErrorMessage, exceptionMessage.ErrorMessage);
            WriteField(stream, ExceptionMessageFieldsId.ErrorType, exceptionMessage.ErrorType);
            WriteField(stream, ExceptionMessageFieldsId.StackTrace, exceptionMessage.StackTrace);
        }

        // NOTE: We are able to seek only if we are using a MemoryStream
        // thus, the seek operation is fast as we are only changing the value of a property
        WriteAtPosition(stream, (int)(stream.Position - before), before - sizeof(int));
    }

    private static ushort GetFieldCount(TestResultMessages testResultMessages) =>
        (ushort)((testResultMessages.ExecutionId is null ? 0 : 1) +
        (testResultMessages.InstanceId is null ? 0 : 1) +
        (IsNullOrEmpty(testResultMessages.SuccessfulTestMessages) ? 0 : 1) +
        (IsNullOrEmpty(testResultMessages.FailedTestMessages) ? 0 : 1));

    private static ushort GetFieldCount(SuccessfulTestResultMessage successfulTestResultMessage) =>
        (ushort)((successfulTestResultMessage.Uid is null ? 0 : 1) +
        (successfulTestResultMessage.DisplayName is null ? 0 : 1) +
        (successfulTestResultMessage.State is null ? 0 : 1) +
        (successfulTestResultMessage.Duration is null ? 0 : 1) +
        (successfulTestResultMessage.Reason is null ? 0 : 1) +
        (successfulTestResultMessage.StandardOutput is null ? 0 : 1) +
        (successfulTestResultMessage.ErrorOutput is null ? 0 : 1) +
        (successfulTestResultMessage.SessionUid is null ? 0 : 1));

    private static ushort GetFieldCount(FailedTestResultMessage failedTestResultMessage) =>
        (ushort)((failedTestResultMessage.Uid is null ? 0 : 1) +
        (failedTestResultMessage.DisplayName is null ? 0 : 1) +
        (failedTestResultMessage.State is null ? 0 : 1) +
        (failedTestResultMessage.Duration is null ? 0 : 1) +
        (failedTestResultMessage.Reason is null ? 0 : 1) +
        (IsNullOrEmpty(failedTestResultMessage.Exceptions) ? 0 : 1) +
        (failedTestResultMessage.StandardOutput is null ? 0 : 1) +
        (failedTestResultMessage.ErrorOutput is null ? 0 : 1) +
        (failedTestResultMessage.SessionUid is null ? 0 : 1));

    private static ushort GetFieldCount(ExceptionMessage exceptionMessage) =>
        (ushort)((exceptionMessage.ErrorMessage is null ? 0 : 1) +
        (exceptionMessage.ErrorType is null ? 0 : 1) +
        (exceptionMessage.StackTrace is null ? 0 : 1));
}
