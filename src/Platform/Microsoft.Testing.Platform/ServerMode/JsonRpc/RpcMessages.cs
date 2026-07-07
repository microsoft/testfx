// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Logging;

namespace Microsoft.Testing.Platform.ServerMode;

internal abstract record RpcMessage;

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
    RequestArgsBase(RunId, TestNodes, GraphFilter)
{
    // The compiler-generated record ToString renders the TestNodes collection using its default
    // Object.ToString (e.g. 'System.Collections.Generic.List`1[...]'), which is unreadable in the
    // diagnostic logs. Render the members explicitly instead.
    public override string ToString()
        => RpcMessageFormatting.FormatRequestArgs(nameof(DiscoverRequestArgs), RunId, TestNodes, GraphFilter);
}

internal record ResponseArgsBase;

internal sealed record DiscoverResponseArgs : ResponseArgsBase;

internal sealed record RunRequestArgs(Guid RunId, ICollection<TestNode>? TestNodes, string? GraphFilter) :
    RequestArgsBase(RunId, TestNodes, GraphFilter)
{
    public override string ToString()
        => RpcMessageFormatting.FormatRequestArgs(nameof(RunRequestArgs), RunId, TestNodes, GraphFilter);
}

internal sealed record RunResponseArgs(Artifact[] Artifacts) : ResponseArgsBase
{
    public override string ToString()
    {
        var builder = new StringBuilder();
        builder.Append(nameof(RunResponseArgs)).Append(" { ");
        builder.Append($"{nameof(Artifacts)} = ");
        RpcMessageFormatting.AppendItems(builder, Artifacts);
        builder.Append(" }");
        return builder.ToString();
    }
}

internal sealed record Artifact(string Uri, string Producer, string Type, string DisplayName, string? Description = null);

internal sealed record CancelRequestArgs(int CancelRequestId);

internal sealed record ExitRequestArgs;

/// <summary>
/// Sentinel params type used when a request's params failed to deserialize.
/// The request reaches the handler so a proper JSON-RPC error can be sent back
/// using the request's id rather than crashing the message-handling loop.
/// </summary>
internal sealed record InvalidRequestParamsArgs(int ErrorCode, string ErrorMessage);

internal sealed record ClientInfo(string Name, string Version);

internal sealed record ClientCapabilities(bool DebuggerProvider);

internal sealed record ServerInfo(string Name, string Version);

internal sealed record ServerCapabilities(ServerTestingCapabilities TestingCapabilities);

internal sealed record ServerTestingCapabilities(
    bool SupportsDiscovery,
    bool MultiRequestSupport,
    bool VSTestProviderSupport,
    bool SupportsAttachments,
    bool MultiConnectionProvider);

internal sealed record TestNodeStateChangedEventArgs(Guid RunId, TestNodeUpdateMessage[]? Changes)
{
    public override string ToString()
    {
        var builder = new StringBuilder();
        builder.Append(nameof(TestNodeStateChangedEventArgs)).Append(" { ");
        builder.Append($"{nameof(RunId)} = ").Append(RunId);
        builder.Append($", {nameof(Changes)} = ");
        RpcMessageFormatting.AppendItems(builder, Changes);
        builder.Append(" }");
        return builder.ToString();
    }
}

internal sealed record LogEventArgs(ServerLogMessage LogMessage);

internal sealed record TelemetryEventArgs(string EventName, IDictionary<string, object> Metrics)
{
    public override string ToString()
    {
        var builder = new StringBuilder();
        builder.Append(nameof(TelemetryEventArgs)).Append(" { ");
        builder.Append($"{nameof(EventName)} = ").Append(EventName);
        builder.Append($", {nameof(Metrics)} = ");
        RpcMessageFormatting.AppendDictionary(builder, Metrics);
        builder.Append(" }");
        return builder.ToString();
    }
}

internal sealed record ProcessInfoArgs(string Program, string? Args, string? WorkingDirectory, IDictionary<string, string?>? EnvironmentVariables)
{
    public override string ToString()
    {
        var builder = new StringBuilder();
        builder.Append(nameof(ProcessInfoArgs)).Append(" { ");
        builder.Append($"{nameof(Program)} = ").Append(Program);
        builder.Append($", {nameof(Args)} = ");
        RpcMessageFormatting.AppendValue(builder, Args);
        builder.Append($", {nameof(WorkingDirectory)} = ");
        RpcMessageFormatting.AppendValue(builder, WorkingDirectory);
        builder.Append($", {nameof(EnvironmentVariables)} = ");
        RpcMessageFormatting.AppendDictionary(builder, EnvironmentVariables);
        builder.Append(" }");
        return builder.ToString();
    }
}

internal sealed record AttachDebuggerInfoArgs(int ProcessId);

internal sealed record class TestsAttachments(RunTestAttachment[] Attachments)
{
    public override string ToString()
    {
        var builder = new StringBuilder();
        builder.Append(nameof(TestsAttachments)).Append(" { ");
        builder.Append($"{nameof(Attachments)} = ");
        RpcMessageFormatting.AppendItems(builder, Attachments);
        builder.Append(" }");
        return builder.ToString();
    }
}

internal sealed record class RunTestAttachment(string? Uri, string? Producer, string? Type, string? DisplayName, string? Description);

/// <summary>
/// Helpers to render JSON-RPC message members that hold collections or dictionaries. The compiler-generated
/// record <see cref="object.ToString"/> would otherwise print these members using the default
/// <see cref="object.ToString"/> (e.g. <c>System.Collections.Generic.List`1[...]</c>), which is unreadable
/// when the messages are dumped to the diagnostic logs.
/// </summary>
internal static class RpcMessageFormatting
{
    public static string FormatRequestArgs(string typeName, Guid runId, ICollection<TestNode>? testNodes, string? graphFilter)
    {
        var builder = new StringBuilder();
        builder.Append(typeName).Append(" { ");
        builder.Append("RunId = ").Append(runId);
        builder.Append(", TestNodes = ");
        AppendItems(builder, testNodes);
        builder.Append(", GraphFilter = ");
        AppendValue(builder, graphFilter);
        builder.Append(" }");
        return builder.ToString();
    }

    // Appends a value, rendering null explicitly as "<null>" so that a null value is not
    // confused with an empty string in the diagnostic logs.
    public static void AppendValue<T>(StringBuilder builder, T value)
    {
        if (value is null)
        {
            builder.Append("<null>");
            return;
        }

        builder.Append(value);
    }

    public static void AppendItems<T>(StringBuilder builder, IEnumerable<T>? items)
    {
        if (items is null)
        {
            builder.Append("<null>");
            return;
        }

        builder.Append('[');
        bool first = true;
        foreach (T item in items)
        {
            if (!first)
            {
                builder.Append(", ");
            }

            first = false;
            AppendValue(builder, item);
        }

        builder.Append(']');
    }

    public static void AppendDictionary<TValue>(StringBuilder builder, IDictionary<string, TValue>? dictionary)
    {
        if (dictionary is null)
        {
            builder.Append("<null>");
            return;
        }

        builder.Append('[');
        bool first = true;
        foreach (KeyValuePair<string, TValue> pair in dictionary)
        {
            if (!first)
            {
                builder.Append(", ");
            }

            first = false;
            builder.Append(pair.Key).Append(" = ");
            AppendValue(builder, pair.Value);
        }

        builder.Append(']');
    }
}
