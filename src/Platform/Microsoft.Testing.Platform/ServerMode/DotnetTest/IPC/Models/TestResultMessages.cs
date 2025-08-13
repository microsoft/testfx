// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.IPC.Models;

internal sealed record SuccessfulTestResultMessage(string? Uid, string? DisplayName, byte? State, long? Duration, string? Reason, string? StandardOutput, string? ErrorOutput, string? SessionUid);

internal sealed record FailedTestResultMessage(string? Uid, string? DisplayName, byte? State, long? Duration, string? Reason, ExceptionMessage[]? Exceptions, string? StandardOutput, string? ErrorOutput, string? SessionUid);

internal sealed record InProgressTestResultMessage(string? Uid, string? DisplayName, string? SessionUid);

internal sealed record ExceptionMessage(string? ErrorMessage, string? ErrorType, string? StackTrace);

internal sealed record TestResultMessages(string? ExecutionId, string? InstanceId, SuccessfulTestResultMessage[] SuccessfulTestMessages, FailedTestResultMessage[] FailedTestMessages) : IRequest;
