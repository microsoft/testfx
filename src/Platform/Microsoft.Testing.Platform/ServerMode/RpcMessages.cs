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
internal record RequestMessage(int Id, string Method, object? Params) : RpcMessage;

/// <summary>
/// A notification message is a message that notifies the server of an event.
/// There's no corresponding response that the server should send back and as such
/// no Id is specified when sending a notification.
/// </summary>
internal record NotificationMessage(string Method, object? Params) : RpcMessage;

/// <summary>
/// An error message is sent if some exception was thrown when processing the request.
/// </summary>
internal record ErrorMessage(int Id, int ErrorCode, string Message, object? Data) : RpcMessage;

/// <summary>
/// An response message is sent if a request is handled successfully.
/// </summary>
/// <remarks>
/// If the RPC handler returns a <see cref="Task"/> the <paramref name="Result"/>
/// will be returned as <c>null</c>.
/// </remarks>
internal record ResponseMessage(int Id, object? Result) : RpcMessage;

internal record InitializeRequestArgs(int ProcessId, ClientInfo ClientInfo, ClientCapabilities Capabilities);

internal record InitializeResponseArgs(ServerInfo ServerInfo, ServerCapabilities Capabilities);

internal record RequestArgsBase(Guid RunId, ICollection<TestNode>? TestNodes, string? GraphFilter);

internal record DiscoverRequestArgs(Guid RunId, ICollection<TestNode>? TestNodes, string? GraphFilter) :
    RequestArgsBase(RunId, TestNodes, GraphFilter);

internal record ResponseArgsBase;

internal record DiscoverResponseArgs : ResponseArgsBase;

internal record RunRequestArgs(Guid RunId, ICollection<TestNode>? TestNodes, string? GraphFilter) :
    RequestArgsBase(RunId, TestNodes, GraphFilter);

internal record RunResponseArgs(Artifact[] Artifacts) : ResponseArgsBase;

internal record Artifact(string Uri, string Producer, string Type, string DisplayName, string? Description = null);

internal record CancelRequestArgs(int CancelRequestId);

internal record ExitRequestArgs;

internal record ClientInfo(string Name, string Version);

internal record ClientCapabilities(bool DebuggerProvider);

internal record ClientTestingCapabilities(bool DebuggerProvider);

internal record ServerInfo(string Name, string Version);

internal record ServerCapabilities(ServerTestingCapabilities TestingCapabilities);

internal record ServerTestingCapabilities(
    bool SupportsDiscovery,
    bool MultiRequestSupport,
    bool VSTestProviderSupport);

internal record TestNodeStateChangedEventArgs(Guid RunId, TestNodeUpdateMessage[]? Changes);

internal record LogEventArgs(ServerLogMessage LogMessage);

internal record TelemetryEventArgs(string EventName, IDictionary<string, object> Metrics);

internal record ProcessInfoArgs(string Program, string? Args, string? WorkingDirectory, IDictionary<string, string?>? EnvironmentVariables);

internal record AttachDebuggerInfoArgs(int ProcessId);
