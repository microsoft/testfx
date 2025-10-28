// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using EasyNamedPipes;

using Microsoft.CodeAnalysis;

namespace Microsoft.Testing.Platform.IPC.Models;

[Embedded]
internal sealed record SuccessfulTestResultMessage(
    string? Uid,
    string? DisplayName,
    byte? State,
    long? Duration,
    string? Reason,
    string? StandardOutput,
    string? ErrorOutput,
    string? SessionUid);

[Embedded]
internal sealed record FailedTestResultMessage(
    string? Uid,
    string? DisplayName,
    byte? State,
    long? Duration,
    string? Reason,
    ExceptionMessage[]? Exceptions,
    string? StandardOutput,
    string? ErrorOutput,
    string? SessionUid);

[Embedded]
internal sealed record ExceptionMessage(
    string? ErrorMessage,
    string? ErrorType,
    string? StackTrace);

[Embedded]
[PipeSerializableMessage("DotNetTestProtocol", 6)]
internal sealed record TestResultMessages(
    [property: PipePropertyId(1)] string? ExecutionId,
    [property: PipePropertyId(2)] string? InstanceId,
    [property: PipePropertyId(3)] SuccessfulTestResultMessage[] SuccessfulTestMessages,
    [property: PipePropertyId(4)] FailedTestResultMessage[] FailedTestMessages) : IRequest;
