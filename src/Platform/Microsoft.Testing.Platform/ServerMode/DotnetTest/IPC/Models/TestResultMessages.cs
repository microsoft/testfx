// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using EasyNamedPipes;

namespace Microsoft.Testing.Platform.IPC.Models;

internal sealed record SuccessfulTestResultMessage(
    [property: PipePropertyId(1)] string? Uid,
    [property: PipePropertyId(2)] string? DisplayName,
    [property: PipePropertyId(3)] byte? State,
    [property: PipePropertyId(4)] long? Duration,
    [property: PipePropertyId(5)] string? Reason,
    [property: PipePropertyId(6)] string? StandardOutput,
    [property: PipePropertyId(7)] string? ErrorOutput,
    [property: PipePropertyId(8)] string? SessionUid);

internal sealed record FailedTestResultMessage(
    [property: PipePropertyId(1)] string? Uid,
    [property: PipePropertyId(2)] string? DisplayName,
    [property: PipePropertyId(3)] byte? State,
    [property: PipePropertyId(4)] long? Duration,
    [property: PipePropertyId(5)] string? Reason,
    [property: PipePropertyId(6)] ExceptionMessage[]? Exceptions,
    [property: PipePropertyId(7)] string? StandardOutput,
    [property: PipePropertyId(8)] string? ErrorOutput,
    [property: PipePropertyId(9)] string? SessionUid);

internal sealed record ExceptionMessage(
    [property: PipePropertyId(1)] string? ErrorMessage,
    [property: PipePropertyId(2)] string? ErrorType,
    [property: PipePropertyId(3)] string? StackTrace);

[PipeSerializableMessage(ProtocolConstants.ProtocolName, 6)]
internal sealed record TestResultMessages(
    [property: PipePropertyId(1)] string? ExecutionId,
    [property: PipePropertyId(2)] string? InstanceId,
    [property: PipePropertyId(3)] SuccessfulTestResultMessage[] SuccessfulTestMessages,
    [property: PipePropertyId(4)] FailedTestResultMessage[] FailedTestMessages)
    : IRequest;
