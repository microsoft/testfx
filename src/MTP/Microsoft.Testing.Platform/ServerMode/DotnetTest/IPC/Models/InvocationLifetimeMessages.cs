// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.IPC.Models;

// Protocol 1.5.0 coordinated invocation-lifetime messages. They are sent on the data pipe only by the process the SDK
// accepted as invocation-lifetime owner (handshake property InvocationLifetimeCoordinationAccepted); the SDK replies
// with VoidResponse.

// An environment change produced by invocation setup. HasValue distinguishes removal from an empty value.
// Values may be secrets: they must never be logged, traced, or echoed.
internal sealed record InvocationLifetimeOutput(string Name, bool HasValue, string? Value)
{
    public override string ToString() => $"{nameof(InvocationLifetimeOutput)} {{ {nameof(Name)} = {Name}, {nameof(HasValue)} = {HasValue} }}";
}

internal sealed record InvocationSetupCompleted(
    string? ExecutionId,
    string? InstanceId,
    bool Succeeded,
    string? ErrorMessage,
    InvocationLifetimeOutput[] Outputs) : IRequest
{
    public override string ToString() => $"{nameof(InvocationSetupCompleted)} {{ {nameof(Succeeded)} = {Succeeded}, OutputCount = {Outputs.Length} }}";
}

internal sealed record ModuleTestsCompleted(string? ExecutionId, string? InstanceId, int ExitCode) : IRequest;

internal sealed record InvocationCleanupCompleted(
    string? ExecutionId,
    string? InstanceId,
    bool Succeeded,
    string? ErrorMessage) : IRequest;
