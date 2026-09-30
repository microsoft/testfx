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

      |---SuccessfulTestMessageList[0].RetryAttemptNumber Id---| (2 bytes)
      |---SuccessfulTestMessageList[0].RetryAttemptNumber Size---| (4 bytes)
      |---SuccessfulTestMessageList[0].RetryAttemptNumber Value---| (n bytes)

      |---SuccessfulTestMessageList[0].IsSuperseded Id---| (2 bytes)
      |---SuccessfulTestMessageList[0].IsSuperseded Size---| (1 byte)
      |---SuccessfulTestMessageList[0].IsSuperseded Value---| (n bytes)

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

      |---FailedTestMessageList[0].Expected Id---| (2 bytes)
      |---FailedTestMessageList[0].Expected Size---| (4 bytes)
      |---FailedTestMessageList[0].Expected Value---| (n bytes)

      |---FailedTestMessageList[0].Actual Id---| (2 bytes)
      |---FailedTestMessageList[0].Actual Size---| (4 bytes)
      |---FailedTestMessageList[0].Actual Value---| (n bytes)

      |---FailedTestMessageList[0].RetryAttemptNumber Id---| (2 bytes)
      |---FailedTestMessageList[0].RetryAttemptNumber Size---| (4 bytes)
      |---FailedTestMessageList[0].RetryAttemptNumber Value---| (n bytes)

      |---FailedTestMessageList[0].IsSuperseded Id---| (2 bytes)
      |---FailedTestMessageList[0].IsSuperseded Size---| (1 byte)
      |---FailedTestMessageList[0].IsSuperseded Value---| (n bytes)
  */

internal sealed class TestResultMessagesSerializer : NamedPipeSerializer<TestResultMessages>, INamedPipeSerializer
{
    public override int Id => TestResultMessagesFieldsId.MessagesSerializerId;

    protected override TestResultMessages DeserializeCore(Stream stream)
    {
        string? executionId = null;
        string? instanceId = null;
        SuccessfulTestResultMessage[]? successfulTestResultMessages = [];
        FailedTestResultMessage[]? failedTestResultMessages = [];

        ReadFields(stream, (fieldId, fieldSize) =>
        {
            if (TryReadExecutionScopedField(stream, fieldId, fieldSize, ref executionId, ref instanceId))
            {
                return true;
            }

            switch (fieldId)
            {
                case TestResultMessagesFieldsId.SuccessfulTestMessageList:
                    successfulTestResultMessages = ReadFieldPayload(stream, fieldSize, ReadSuccessfulTestMessagesPayload);
                    return true;

                case TestResultMessagesFieldsId.FailedTestMessageList:
                    failedTestResultMessages = ReadFieldPayload(stream, fieldSize, ReadFailedTestMessagesPayload);
                    return true;

                default:
                    return false;
            }
        });

        return new(
            executionId,
            instanceId,
            successfulTestResultMessages,
            failedTestResultMessages);
    }

    private static SuccessfulTestResultMessage[] ReadSuccessfulTestMessagesPayload(Stream stream)
    {
        int length = ReadCollectionLength(stream, sizeof(ushort));
        var successfulTestResultMessages = new SuccessfulTestResultMessage[length];
        for (int i = 0; i < length; i++)
        {
            CommonTestResultFields fields = ReadFields(stream, default(CommonTestResultFields), static (stream, fieldId, fieldSize, fields) =>
                TryReadCommonTestResultField(
                    stream,
                    fieldId,
                    fieldSize,
                    ref fields,
                    SuccessfulTestResultMessageFieldsId.StandardOutput,
                    SuccessfulTestResultMessageFieldsId.ErrorOutput,
                    SuccessfulTestResultMessageFieldsId.SessionUid,
                    SuccessfulTestResultMessageFieldsId.RetryAttemptNumber,
                    SuccessfulTestResultMessageFieldsId.IsSuperseded)
                    ? fields
                    : null);

            successfulTestResultMessages[i] = new SuccessfulTestResultMessage(fields.Uid, fields.DisplayName, fields.State, fields.Duration, fields.Reason, fields.StandardOutput, fields.ErrorOutput, fields.SessionUid, fields.RetryAttemptNumber, fields.IsSuperseded);
        }

        return successfulTestResultMessages;
    }

    private static FailedTestResultMessage[] ReadFailedTestMessagesPayload(Stream stream)
    {
        int length = ReadCollectionLength(stream, sizeof(ushort));
        var failedTestResultMessages = new FailedTestResultMessage[length];
        for (int i = 0; i < length; i++)
        {
            FailedTestResultFields fields = ReadFields(
                stream,
                new FailedTestResultFields { ExceptionMessages = [] },
                static (stream, fieldId, fieldSize, fields) =>
                {
                    switch (fieldId)
                    {
                        case FailedTestResultMessageFieldsId.ExceptionMessageList:
                            fields.ExceptionMessages = ReadFieldPayload(stream, fieldSize, ReadExceptionMessagesPayload);
                            return fields;

                        case FailedTestResultMessageFieldsId.Expected:
                            fields.Expected = ReadStringValue(stream, fieldSize);
                            return fields;

                        case FailedTestResultMessageFieldsId.Actual:
                            fields.Actual = ReadStringValue(stream, fieldSize);
                            return fields;

                        default:
                            return TryReadCommonTestResultField(
                                stream,
                                fieldId,
                                fieldSize,
                                ref fields.Common,
                                FailedTestResultMessageFieldsId.StandardOutput,
                                FailedTestResultMessageFieldsId.ErrorOutput,
                                FailedTestResultMessageFieldsId.SessionUid,
                                FailedTestResultMessageFieldsId.RetryAttemptNumber,
                                FailedTestResultMessageFieldsId.IsSuperseded)
                                ? fields
                                : null;
                    }
                });

            failedTestResultMessages[i] = new FailedTestResultMessage(
                fields.Common.Uid,
                fields.Common.DisplayName,
                fields.Common.State,
                fields.Common.Duration,
                fields.Common.Reason,
                fields.ExceptionMessages,
                fields.Common.StandardOutput,
                fields.Common.ErrorOutput,
                fields.Common.SessionUid,
                fields.Expected,
                fields.Actual,
                fields.Common.RetryAttemptNumber,
                fields.Common.IsSuperseded);
        }

        return failedTestResultMessages;
    }

    // The Uid, DisplayName, State, Duration and Reason field ids are identical for successful and failed test
    // result messages, so they can be matched directly. The StandardOutput, ErrorOutput, SessionUid,
    // RetryAttemptNumber and IsSuperseded field ids differ between the two message types (a failed message
    // inserts ExceptionMessageList/Expected/Actual before them), so they are passed in by the caller.
    private static bool TryReadCommonTestResultField(Stream stream, ushort fieldId, int fieldSize, ref CommonTestResultFields fields, ushort standardOutputFieldId, ushort errorOutputFieldId, ushort sessionUidFieldId, ushort retryAttemptNumberFieldId, ushort isSupersededFieldId)
    {
        switch (fieldId)
        {
            case SuccessfulTestResultMessageFieldsId.Uid:
                fields.Uid = ReadStringValue(stream, fieldSize);
                return true;

            case SuccessfulTestResultMessageFieldsId.DisplayName:
                fields.DisplayName = ReadStringValue(stream, fieldSize);
                return true;

            case SuccessfulTestResultMessageFieldsId.State:
                fields.State = ReadByte(stream);
                return true;

            case SuccessfulTestResultMessageFieldsId.Duration:
                fields.Duration = ReadLong(stream);
                return true;

            case SuccessfulTestResultMessageFieldsId.Reason:
                fields.Reason = ReadStringValue(stream, fieldSize);
                return true;
        }

        if (fieldId == standardOutputFieldId)
        {
            fields.StandardOutput = ReadStringValue(stream, fieldSize);
            return true;
        }

        if (fieldId == errorOutputFieldId)
        {
            fields.ErrorOutput = ReadStringValue(stream, fieldSize);
            return true;
        }

        if (fieldId == sessionUidFieldId)
        {
            fields.SessionUid = ReadStringValue(stream, fieldSize);
            return true;
        }

        if (fieldId == retryAttemptNumberFieldId)
        {
            fields.RetryAttemptNumber = ReadInt(stream);
            return true;
        }

        if (fieldId == isSupersededFieldId)
        {
            fields.IsSuperseded = ReadBool(stream);
            return true;
        }

        return false;
    }

    private static ExceptionMessage[] ReadExceptionMessagesPayload(Stream stream)
    {
        int length = ReadCollectionLength(stream, sizeof(ushort));
        var exceptionMessages = new ExceptionMessage[length];

        for (int i = 0; i < length; i++)
        {
            ExceptionFields fields = ReadFields(stream, default(ExceptionFields), static (stream, fieldId, fieldSize, fields) =>
            {
                switch (fieldId)
                {
                    case ExceptionMessageFieldsId.ErrorMessage:
                        fields.ErrorMessage = ReadStringValue(stream, fieldSize);
                        return fields;

                    case ExceptionMessageFieldsId.ErrorType:
                        fields.ErrorType = ReadStringValue(stream, fieldSize);
                        return fields;

                    case ExceptionMessageFieldsId.StackTrace:
                        fields.StackTrace = ReadStringValue(stream, fieldSize);
                        return fields;

                    default:
                        return null;
                }
            });

            exceptionMessages[i] = new ExceptionMessage(fields.ErrorMessage, fields.ErrorType, fields.StackTrace);
        }

        return exceptionMessages;
    }

    protected override void SerializeCore(TestResultMessages objectToSerialize, Stream stream)
    {
        WriteExecutionScopedHeader(
            stream,
            objectToSerialize.ExecutionId,
            objectToSerialize.InstanceId,
            (ushort)((IsNullOrEmpty(objectToSerialize.SuccessfulTestMessages) ? 0 : 1) +
                (IsNullOrEmpty(objectToSerialize.FailedTestMessages) ? 0 : 1)));

        WriteSuccessfulTestMessagesPayload(stream, objectToSerialize.SuccessfulTestMessages);
        WriteFailedTestMessagesPayload(stream, objectToSerialize.FailedTestMessages);
    }

    private static void WriteSuccessfulTestMessagesPayload(Stream stream, SuccessfulTestResultMessage[]? successfulTestResultMessages)
        => WriteListPayload(stream, TestResultMessagesFieldsId.SuccessfulTestMessageList, successfulTestResultMessages, static (s, successfulTestResultMessage) =>
        {
            WriteUShort(s, GetFieldCount(successfulTestResultMessage));

            WriteCommonTestResultLeadingFields(s, successfulTestResultMessage.Uid, successfulTestResultMessage.DisplayName, successfulTestResultMessage.State, successfulTestResultMessage.Duration, successfulTestResultMessage.Reason);
            WriteField(s, SuccessfulTestResultMessageFieldsId.StandardOutput, successfulTestResultMessage.StandardOutput);
            WriteField(s, SuccessfulTestResultMessageFieldsId.ErrorOutput, successfulTestResultMessage.ErrorOutput);
            WriteField(s, SuccessfulTestResultMessageFieldsId.SessionUid, successfulTestResultMessage.SessionUid);
            WriteField(s, SuccessfulTestResultMessageFieldsId.RetryAttemptNumber, successfulTestResultMessage.RetryAttemptNumber);
            WriteField(s, SuccessfulTestResultMessageFieldsId.IsSuperseded, successfulTestResultMessage.IsSuperseded);
        });

    private static void WriteFailedTestMessagesPayload(Stream stream, FailedTestResultMessage[]? failedTestResultMessages)
        => WriteListPayload(stream, TestResultMessagesFieldsId.FailedTestMessageList, failedTestResultMessages, static (s, failedTestResultMessage) =>
        {
            WriteUShort(s, GetFieldCount(failedTestResultMessage));

            WriteCommonTestResultLeadingFields(s, failedTestResultMessage.Uid, failedTestResultMessage.DisplayName, failedTestResultMessage.State, failedTestResultMessage.Duration, failedTestResultMessage.Reason);
            WriteExceptionMessagesPayload(s, failedTestResultMessage.Exceptions);
            WriteField(s, FailedTestResultMessageFieldsId.StandardOutput, failedTestResultMessage.StandardOutput);
            WriteField(s, FailedTestResultMessageFieldsId.ErrorOutput, failedTestResultMessage.ErrorOutput);
            WriteField(s, FailedTestResultMessageFieldsId.SessionUid, failedTestResultMessage.SessionUid);
            WriteField(s, FailedTestResultMessageFieldsId.Expected, failedTestResultMessage.Expected);
            WriteField(s, FailedTestResultMessageFieldsId.Actual, failedTestResultMessage.Actual);
            WriteField(s, FailedTestResultMessageFieldsId.RetryAttemptNumber, failedTestResultMessage.RetryAttemptNumber);
            WriteField(s, FailedTestResultMessageFieldsId.IsSuperseded, failedTestResultMessage.IsSuperseded);
        });

    // The Uid, DisplayName, State, Duration and Reason field ids are identical for successful and failed test
    // result messages, so the leading fields can be written by a single shared helper.
    private static void WriteCommonTestResultLeadingFields(Stream stream, string? uid, string? displayName, byte? state, long? duration, string? reason)
    {
        WriteField(stream, SuccessfulTestResultMessageFieldsId.Uid, uid);
        WriteField(stream, SuccessfulTestResultMessageFieldsId.DisplayName, displayName);
        WriteField(stream, SuccessfulTestResultMessageFieldsId.State, state);
        WriteField(stream, SuccessfulTestResultMessageFieldsId.Duration, duration);
        WriteField(stream, SuccessfulTestResultMessageFieldsId.Reason, reason);
    }

    private static void WriteExceptionMessagesPayload(Stream stream, ExceptionMessage[]? exceptionMessages)
        => WriteListPayload(stream, FailedTestResultMessageFieldsId.ExceptionMessageList, exceptionMessages, static (s, exceptionMessage) =>
        {
            WriteUShort(s, GetFieldCount(exceptionMessage));

            WriteField(s, ExceptionMessageFieldsId.ErrorMessage, exceptionMessage.ErrorMessage);
            WriteField(s, ExceptionMessageFieldsId.ErrorType, exceptionMessage.ErrorType);
            WriteField(s, ExceptionMessageFieldsId.StackTrace, exceptionMessage.StackTrace);
        });

    private static ushort GetFieldCount(SuccessfulTestResultMessage successfulTestResultMessage) =>
        (ushort)((successfulTestResultMessage.Uid is null ? 0 : 1) +
        (successfulTestResultMessage.DisplayName is null ? 0 : 1) +
        (successfulTestResultMessage.State is null ? 0 : 1) +
        (successfulTestResultMessage.Duration is null ? 0 : 1) +
        (successfulTestResultMessage.Reason is null ? 0 : 1) +
        (successfulTestResultMessage.StandardOutput is null ? 0 : 1) +
        (successfulTestResultMessage.ErrorOutput is null ? 0 : 1) +
        (successfulTestResultMessage.SessionUid is null ? 0 : 1) +
        (successfulTestResultMessage.RetryAttemptNumber is null ? 0 : 1) +
        (successfulTestResultMessage.IsSuperseded is null ? 0 : 1));

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
        (failedTestResultMessage.Actual is null ? 0 : 1) +
        (failedTestResultMessage.RetryAttemptNumber is null ? 0 : 1) +
        (failedTestResultMessage.IsSuperseded is null ? 0 : 1));

    private static ushort GetFieldCount(ExceptionMessage exceptionMessage) =>
        (ushort)((exceptionMessage.ErrorMessage is null ? 0 : 1) +
        (exceptionMessage.ErrorType is null ? 0 : 1) +
        (exceptionMessage.StackTrace is null ? 0 : 1));

    // Mutable holder for the fields shared by successful and failed test result messages, used while reading so
    // the common field-parsing logic can be shared across both message types.
    private struct CommonTestResultFields
    {
        public string? Uid { get; set; }

        public string? DisplayName { get; set; }

        public byte? State { get; set; }

        public long? Duration { get; set; }

        public string? Reason { get; set; }

        public string? StandardOutput { get; set; }

        public string? ErrorOutput { get; set; }

        public string? SessionUid { get; set; }

        public int? RetryAttemptNumber { get; set; }

        public bool? IsSuperseded { get; set; }
    }

    private struct FailedTestResultFields
    {
        public CommonTestResultFields Common;

        public ExceptionMessage[] ExceptionMessages { get; set; }

        public string? Expected { get; set; }

        public string? Actual { get; set; }
    }

    private struct ExceptionFields
    {
        public string? ErrorMessage { get; set; }

        public string? ErrorType { get; set; }

        public string? StackTrace { get; set; }
    }
}
