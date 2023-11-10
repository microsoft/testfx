// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Logging;

namespace Microsoft.Testing.Platform.ServerMode;

internal abstract class RpcMessage()
{
}

/// <summary>
/// A request is a message for which the server should return a corresponding
/// <see cref="ErrorMessage"/> or <see cref="ResponseMessage"/>.
/// </summary>
internal sealed class RequestMessage(int id, string method, object? @params) : RpcMessage
{
    public int Id { get; } = id;

    public string Method { get; } = method;

    public object? Params { get; } = @params;
}

/// <summary>
/// A notification message is a message that notifies the server of an event.
/// There's no corresponding response that the server should send back and as such
/// no Id is specified when sending a notification.
/// </summary>
internal sealed class NotificationMessage(string method, object? @params) : RpcMessage
{
    public string Method { get; } = method;

    public object? Params { get; } = @params;
}

/// <summary>
/// An error message is sent if some exception was thrown when processing the request.
/// </summary>
internal sealed class ErrorMessage(int id, int errorCode, string message, object? data) : RpcMessage
{
    public int Id { get; } = id;

    public int ErrorCode { get; } = errorCode;

    public string Message { get; } = message;

    public object? Data { get; } = data;
}

/// <summary>
/// An response message is sent if a request is handled successfully.
/// </summary>
/// <remarks>
/// If the RPC handler returns a <see cref="Task"/> the <paramref name="result"/>
/// will be returned as <c>null</c>.
/// </remarks>
internal sealed class ResponseMessage(int id, object? result) : RpcMessage
{
    public int Id { get; } = id;

    public object? Result { get; } = result;
}

internal sealed class InitializeRequestArgs(int processId, ClientInfo clientInfo, ClientCapabilities capabilities)
{
    public int ProcessId { get; } = processId;

    public ClientInfo ClientInfo { get; } = clientInfo;

    public ClientCapabilities Capabilities { get; } = capabilities;
}

internal sealed class InitializeResponseArgs(ServerInfo serverInfo, ServerCapabilities capabilities)
{
    public ServerInfo ServerInfo { get; } = serverInfo;

    public ServerCapabilities Capabilities { get; } = capabilities;
}

internal abstract class RequestArgsBase(Guid runId, ICollection<TestNode>? testNodes, string? graphFilter)
{
    public Guid RunId { get; } = runId;

    public ICollection<TestNode>? TestNodes { get; } = testNodes;

    public string? GraphFilter { get; } = graphFilter;
}

internal sealed class DiscoverRequestArgs(Guid runId, ICollection<TestNode>? testNodes, string? graphFilter) :
    RequestArgsBase(runId, testNodes, graphFilter);

internal abstract class ResponseArgsBase;

internal sealed class DiscoverResponseArgs() : ResponseArgsBase;

internal sealed class RunRequestArgs(Guid runId, ICollection<TestNode>? testNodes, string? graphFilter) :
    RequestArgsBase(runId, testNodes, graphFilter);

internal sealed class RunResponseArgs(Artifact[] artifacts) : ResponseArgsBase
{
    public Artifact[] Artifacts { get; } = artifacts;
}

internal sealed class Artifact(string uri, string producer, string type, string displayName, string? description = null)
{
    public string Uri { get; } = uri;

    public string Producer { get; } = producer;

    public string Type { get; } = type;

    public string DisplayName { get; } = displayName;

    public string? Description { get; } = description;
}

internal sealed class CancelRequestArgs(int cancelRequestId)
{
    public int CancelRequestId { get; } = cancelRequestId;
}

internal sealed class ExitRequestArgs();

internal sealed class ClientInfo(string name, string version)
{
    public string Name { get; } = name;

    public string Version { get; } = version;
}

internal sealed class ClientCapabilities(bool debuggerProvider)
{
    public bool DebuggerProvider { get; } = debuggerProvider;
}

internal sealed class ClientTestingCapabilities(bool debuggerProvider)
{
    public bool DebuggerProvider { get; } = debuggerProvider;
}

internal sealed class ServerInfo(string name, string version)
{
    public string Name { get; } = name;

    public string Version { get; } = version;
}

internal sealed class ServerCapabilities(ServerTestingCapabilities testingCapabilities)
{
    public ServerTestingCapabilities TestingCapabilities { get; } = testingCapabilities;
}

internal sealed class ServerTestingCapabilities(
    bool supportsDiscovery,
    bool multiRequestSupport,
    bool vstestProviderSupport)
{
    public bool SupportsDiscovery { get; } = supportsDiscovery;

    public bool MultiRequestSupport { get; } = multiRequestSupport;

    public bool VSTestProviderSupport { get; } = vstestProviderSupport;
}

internal sealed class TestNodeStateChangedEventArgs(Guid runId, TestNodeUpdateMessage[]? changes)
{
    public Guid RunId { get; } = runId;

    public TestNodeUpdateMessage[]? Changes { get; } = changes;
}

internal sealed class LogEventArgs(ServerLogMessage logMessage)
{
    public ServerLogMessage LogMessage { get; } = logMessage;
}

internal sealed class TelemetryEventArgs(string eventName, IDictionary<string, object> metrics)
{
    public string EventName { get; } = eventName;

    public IDictionary<string, object> Metrics { get; } = metrics;
}

internal sealed class ProcessInfoArgs(string program, string? args, string? workingDirectory, IDictionary<string, string?>? environmentVariables)
{
    public string Program { get; } = program;

    public string? Args { get; } = args;

    public string? WorkingDirectory { get; } = workingDirectory;

    public IDictionary<string, string?>? EnvironmentVariables { get; } = environmentVariables;
}

internal sealed class AttachDebuggerInfoArgs(int processId)
{
    public int ProcessId { get; } = processId;
}
