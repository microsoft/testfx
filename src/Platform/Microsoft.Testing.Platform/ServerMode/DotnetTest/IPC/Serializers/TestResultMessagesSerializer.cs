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

internal sealed class TestResultMessagesSerializer : NamedPipeSerializer<TestResultMessages>, INamedPipeSerializer
{
    public override int Id => TestResultMessagesFieldsId.MessagesSerializerId;

    protected override TestResultMessages DeserializeCore(Stream stream)
    {
        string? executionId = null;
        string? instanceId = null;
        SuccessfulTestResultMessage[]? successfulTestResultMessages = null;
        FailedTestResultMessage[]? failedTestResultMessages = null;

        ReadFields(stream, (fieldId, fieldSize) =>
        {
            switch (fieldId)
            {
                case TestResultMessagesFieldsId.ExecutionId:
                    executionId = ReadStringValue(stream, fieldSize);
                    return true;

                case TestResultMessagesFieldsId.InstanceId:
                    instanceId = ReadStringValue(stream, fieldSize);
                    return true;

                case TestResultMessagesFieldsId.SuccessfulTestMessageList:
                    successfulTestResultMessages = ReadSuccessfulTestMessagesPayload(stream);
                    return true;

                case TestResultMessagesFieldsId.FailedTestMessageList:
                    failedTestResultMessages = ReadFailedTestMessagesPayload(stream);
                    return true;

                default:
                    return false;
            }
        });

        return new(
            executionId,
            instanceId,
            successfulTestResultMessages ?? [],
            failedTestResultMessages ?? []);
    }

    private static SuccessfulTestResultMessage[] ReadSuccessfulTestMessagesPayload(Stream stream)
    {
        int length = ReadInt(stream);
        var successfulTestResultMessages = new SuccessfulTestResultMessage[length];
        for (int i = 0; i < length; i++)
        {
            string? uid = null, displayName = null, reason = null, standardOutput = null, errorOutput = null, sessionUid = null;
            byte? state = null;
            long? duration = null;

            ReadFields(stream, (fieldId, fieldSize) =>
            {
                switch (fieldId)
                {
                    case SuccessfulTestResultMessageFieldsId.Uid:
                        uid = ReadStringValue(stream, fieldSize);
                        return true;

                    case SuccessfulTestResultMessageFieldsId.DisplayName:
                        displayName = ReadStringValue(stream, fieldSize);
                        return true;

                    case SuccessfulTestResultMessageFieldsId.State:
                        state = ReadByte(stream);
                        return true;

                    case SuccessfulTestResultMessageFieldsId.Duration:
                        duration = ReadLong(stream);
                        return true;

                    case SuccessfulTestResultMessageFieldsId.Reason:
                        reason = ReadStringValue(stream, fieldSize);
                        return true;

                    case SuccessfulTestResultMessageFieldsId.StandardOutput:
                        standardOutput = ReadStringValue(stream, fieldSize);
                        return true;

                    case SuccessfulTestResultMessageFieldsId.ErrorOutput:
                        errorOutput = ReadStringValue(stream, fieldSize);
                        return true;

                    case SuccessfulTestResultMessageFieldsId.SessionUid:
                        sessionUid = ReadStringValue(stream, fieldSize);
                        return true;

                    default:
                        return false;
                }
            });

            successfulTestResultMessages[i] = new SuccessfulTestResultMessage(uid, displayName, state, duration, reason, standardOutput, errorOutput, sessionUid);
        }

        return successfulTestResultMessages;
    }

    private static FailedTestResultMessage[] ReadFailedTestMessagesPayload(Stream stream)
    {
        int length = ReadInt(stream);
        var failedTestResultMessages = new FailedTestResultMessage[length];
        for (int i = 0; i < length; i++)
        {
            string? uid = null, displayName = null, reason = null, sessionUid = null, standardOutput = null, errorOutput = null;
            string? expected = null, actual = null;
            ExceptionMessage[] exceptionMessages = [];
            byte? state = null;
            long? duration = null;

            ReadFields(stream, (fieldId, fieldSize) =>
            {
                switch (fieldId)
                {
                    case FailedTestResultMessageFieldsId.Uid:
                        uid = ReadStringValue(stream, fieldSize);
                        return true;

                    case FailedTestResultMessageFieldsId.DisplayName:
                        displayName = ReadStringValue(stream, fieldSize);
                        return true;

                    case FailedTestResultMessageFieldsId.State:
                        state = ReadByte(stream);
                        return true;

                    case FailedTestResultMessageFieldsId.Duration:
                        duration = ReadLong(stream);
                        return true;

                    case FailedTestResultMessageFieldsId.Reason:
                        reason = ReadStringValue(stream, fieldSize);
                        return true;

                    case FailedTestResultMessageFieldsId.ExceptionMessageList:
                        exceptionMessages = ReadExceptionMessagesPayload(stream);
                        return true;

                    case FailedTestResultMessageFieldsId.StandardOutput:
                        standardOutput = ReadStringValue(stream, fieldSize);
                        return true;

                    case FailedTestResultMessageFieldsId.ErrorOutput:
                        errorOutput = ReadStringValue(stream, fieldSize);
                        return true;

                    case FailedTestResultMessageFieldsId.SessionUid:
                        sessionUid = ReadStringValue(stream, fieldSize);
                        return true;

                    case FailedTestResultMessageFieldsId.Expected:
                        expected = ReadStringValue(stream, fieldSize);
                        return true;

                    case FailedTestResultMessageFieldsId.Actual:
                        actual = ReadStringValue(stream, fieldSize);
                        return true;

                    default:
                        return false;
                }
            });

            failedTestResultMessages[i] = new FailedTestResultMessage(uid, displayName, state, duration, reason, exceptionMessages, standardOutput, errorOutput, sessionUid, expected, actual);
        }

        return failedTestResultMessages;
    }

    private static ExceptionMessage[] ReadExceptionMessagesPayload(Stream stream)
    {
        int length = ReadInt(stream);
        var exceptionMessages = new ExceptionMessage[length];

        for (int i = 0; i < length; i++)
        {
            string? errorMessage = null;
            string? errorType = null;
            string? stackTrace = null;

            ReadFields(stream, (fieldId, fieldSize) =>
            {
                switch (fieldId)
                {
                    case ExceptionMessageFieldsId.ErrorMessage:
                        errorMessage = ReadStringValue(stream, fieldSize);
                        return true;

                    case ExceptionMessageFieldsId.ErrorType:
                        errorType = ReadStringValue(stream, fieldSize);
                        return true;

                    case ExceptionMessageFieldsId.StackTrace:
                        stackTrace = ReadStringValue(stream, fieldSize);
                        return true;

                    default:
                        return false;
                }
            });

            exceptionMessages[i] = new ExceptionMessage(errorMessage, errorType, stackTrace);
        }

        return exceptionMessages;
    }

    protected override void SerializeCore(TestResultMessages objectToSerialize, Stream stream)
    {
        DebugAssert(stream.CanSeek, "We expect a seekable stream.");

        WriteUShort(stream, GetFieldCount(objectToSerialize));

        WriteField(stream, TestResultMessagesFieldsId.ExecutionId, objectToSerialize.ExecutionId);
        WriteField(stream, TestResultMessagesFieldsId.InstanceId, objectToSerialize.InstanceId);
        WriteSuccessfulTestMessagesPayload(stream, objectToSerialize.SuccessfulTestMessages);
        WriteFailedTestMessagesPayload(stream, objectToSerialize.FailedTestMessages);
    }

    private static void WriteSuccessfulTestMessagesPayload(Stream stream, SuccessfulTestResultMessage[]? successfulTestResultMessages)
        => WriteListPayload(stream, TestResultMessagesFieldsId.SuccessfulTestMessageList, successfulTestResultMessages, static (s, successfulTestResultMessage) =>
        {
            WriteUShort(s, GetFieldCount(successfulTestResultMessage));

            WriteField(s, SuccessfulTestResultMessageFieldsId.Uid, successfulTestResultMessage.Uid);
            WriteField(s, SuccessfulTestResultMessageFieldsId.DisplayName, successfulTestResultMessage.DisplayName);
            WriteField(s, SuccessfulTestResultMessageFieldsId.State, successfulTestResultMessage.State);
            WriteField(s, SuccessfulTestResultMessageFieldsId.Duration, successfulTestResultMessage.Duration);
            WriteField(s, SuccessfulTestResultMessageFieldsId.Reason, successfulTestResultMessage.Reason);
            WriteField(s, SuccessfulTestResultMessageFieldsId.StandardOutput, successfulTestResultMessage.StandardOutput);
            WriteField(s, SuccessfulTestResultMessageFieldsId.ErrorOutput, successfulTestResultMessage.ErrorOutput);
            WriteField(s, SuccessfulTestResultMessageFieldsId.SessionUid, successfulTestResultMessage.SessionUid);
        });

    private static void WriteFailedTestMessagesPayload(Stream stream, FailedTestResultMessage[]? failedTestResultMessages)
        => WriteListPayload(stream, TestResultMessagesFieldsId.FailedTestMessageList, failedTestResultMessages, static (s, failedTestResultMessage) =>
        {
            WriteUShort(s, GetFieldCount(failedTestResultMessage));

            WriteField(s, FailedTestResultMessageFieldsId.Uid, failedTestResultMessage.Uid);
            WriteField(s, FailedTestResultMessageFieldsId.DisplayName, failedTestResultMessage.DisplayName);
            WriteField(s, FailedTestResultMessageFieldsId.State, failedTestResultMessage.State);
            WriteField(s, FailedTestResultMessageFieldsId.Duration, failedTestResultMessage.Duration);
            WriteField(s, FailedTestResultMessageFieldsId.Reason, failedTestResultMessage.Reason);
            WriteExceptionMessagesPayload(s, failedTestResultMessage.Exceptions);
            WriteField(s, FailedTestResultMessageFieldsId.StandardOutput, failedTestResultMessage.StandardOutput);
            WriteField(s, FailedTestResultMessageFieldsId.ErrorOutput, failedTestResultMessage.ErrorOutput);
            WriteField(s, FailedTestResultMessageFieldsId.SessionUid, failedTestResultMessage.SessionUid);
            WriteField(s, FailedTestResultMessageFieldsId.Expected, failedTestResultMessage.Expected);
            WriteField(s, FailedTestResultMessageFieldsId.Actual, failedTestResultMessage.Actual);
        });

    private static void WriteExceptionMessagesPayload(Stream stream, ExceptionMessage[]? exceptionMessages)
        => WriteListPayload(stream, FailedTestResultMessageFieldsId.ExceptionMessageList, exceptionMessages, static (s, exceptionMessage) =>
        {
            WriteUShort(s, GetFieldCount(exceptionMessage));

            WriteField(s, ExceptionMessageFieldsId.ErrorMessage, exceptionMessage.ErrorMessage);
            WriteField(s, ExceptionMessageFieldsId.ErrorType, exceptionMessage.ErrorType);
            WriteField(s, ExceptionMessageFieldsId.StackTrace, exceptionMessage.StackTrace);
        });

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
        (failedTestResultMessage.SessionUid is null ? 0 : 1) +
        (failedTestResultMessage.Expected is null ? 0 : 1) +
        (failedTestResultMessage.Actual is null ? 0 : 1));

    private static ushort GetFieldCount(ExceptionMessage exceptionMessage) =>
        (ushort)((exceptionMessage.ErrorMessage is null ? 0 : 1) +
        (exceptionMessage.ErrorType is null ? 0 : 1) +
        (exceptionMessage.StackTrace is null ? 0 : 1));
}
