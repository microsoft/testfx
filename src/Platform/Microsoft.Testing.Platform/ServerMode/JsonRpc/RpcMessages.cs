// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Logging;

namespace Microsoft.Testing.Platform.ServerMode;

internal abstract record RpcMessage();

/// <summary>
/// A request is a message for which the server should return a corresponding
/// <see cref="ErrorMessage"/> or <see cref="ResponseMessage"/>.
/// </summary>
internal sealed record RequestMessage(int Id, string Method, object? Params) : RpcMessage;

/// <summary>
/// A notification message is a message that notifies the server of an event.
/// There's no corresponding response that the server should send back and as such
/// no Id is specified when sending a notification.
/// </summary>
internal sealed record NotificationMessage(string Method, object? Params) : RpcMessage;

/// <summary>
/// An error message is sent if some exception was thrown when processing the request.
/// </summary>
internal sealed record ErrorMessage(int Id, int ErrorCode, string Message, object? Data) : RpcMessage;

/// <summary>
/// An response message is sent if a request is handled successfully.
/// </summary>
/// <remarks>
/// If the RPC handler returns a <see cref="Task"/> the <paramref name="Result"/>
/// will be returned as <c>null</c>.
/// </remarks>
internal sealed record ResponseMessage(int Id, object? Result) : RpcMessage;

internal sealed record InitializeRequestArgs(int ProcessId, ClientInfo ClientInfo, ClientCapabilities Capabilities);

internal sealed record InitializeResponseArgs(int? ProcessId, ServerInfo ServerInfo, ServerCapabilities Capabilities);

internal record RequestArgsBase(Guid RunId, ICollection<TestNode>? TestNodes, string? GraphFilter);

internal sealed record DiscoverRequestArgs(Guid RunId, ICollection<TestNode>? TestNodes, string? GraphFilter) :
    RequestArgsBase(RunId, TestNodes, GraphFilter);

internal record ResponseArgsBase;

internal sealed record DiscoverResponseArgs : ResponseArgsBase;

internal sealed record RunRequestArgs(Guid RunId, ICollection<TestNode>? TestNodes, string? GraphFilter) :
    RequestArgsBase(RunId, TestNodes, GraphFilter);

internal sealed record RunResponseArgs(Artifact[] Artifacts) : ResponseArgsBase;

internal sealed record Artifact(string Uri, string Producer, string Type, string DisplayName, string? Description = null);

internal sealed record CancelRequestArgs(int CancelRequestId);

internal sealed record ExitRequestArgs;

internal sealed record ClientInfo(string Name, string Version);

internal sealed record ClientCapabilities(bool DebuggerProvider);

internal sealed record ClientTestingCapabilities(bool DebuggerProvider);

internal sealed record ServerInfo(string Name, string Version);

internal sealed record ServerCapabilities(ServerTestingCapabilities TestingCapabilities);

internal sealed record ServerTestingCapabilities(
    bool SupportsDiscovery,
    bool MultiRequestSupport,
    bool VSTestProviderSupport,
    bool SupportsAttachments,
    bool MultiConnectionProvider);

internal sealed record TestNodeStateChangedEventArgs(Guid RunId, TestNodeUpdateMessage[]? Changes);

internal sealed record LogEventArgs(ServerLogMessage LogMessage);

internal sealed record TelemetryEventArgs(string EventName, IDictionary<string, object> Metrics);

internal sealed record ProcessInfoArgs(string Program, string? Args, string? WorkingDirectory, IDictionary<string, string?>? EnvironmentVariables);

internal sealed record AttachDebuggerInfoArgs(int ProcessId);

internal sealed record class TestsAttachments(RunTestAttachment[] Attachments);

internal sealed record class RunTestAttachment(string? Uri, string? Producer, string? Type, string? DisplayName, string? Description);
