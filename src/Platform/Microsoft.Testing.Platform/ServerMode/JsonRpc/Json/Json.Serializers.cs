// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Extensions.Messages;

namespace Microsoft.Testing.Platform.ServerMode.Json;

internal sealed partial class Json
{
    private static void RegisterDefaultSerializers(Dictionary<Type, JsonSerializer> serializers)
    {
        // Overridden default serializers for better performance using .NET runtime serialization APIs

        // Serialize response types.
        serializers[typeof(RequestMessage)] = new JsonObjectSerializer<RequestMessage>(request =>
        [
            (JsonRpcStrings.JsonRpc, "2.0"),
                 (JsonRpcStrings.Id, request.Id),
                 (JsonRpcStrings.Method, request.Method),
                 (JsonRpcStrings.Params, request.Params)
        ]);

        serializers[typeof(ResponseMessage)] = new JsonObjectSerializer<ResponseMessage>(response =>
        [
            (JsonRpcStrings.JsonRpc, "2.0"),
                 (JsonRpcStrings.Id, response.Id),
                 (JsonRpcStrings.Result, response.Result)
        ]);

        serializers[typeof(NotificationMessage)] = new JsonObjectSerializer<NotificationMessage>(notification =>
        [
            (JsonRpcStrings.JsonRpc, "2.0"),
                 (JsonRpcStrings.Method, notification.Method),
                 (JsonRpcStrings.Params, notification.Params)
        ]);

        serializers[typeof(ErrorMessage)] = new JsonObjectSerializer<ErrorMessage>(error =>
        {
            var errorMsg = new (string, object?)[]
            {
                (JsonRpcStrings.Code, error.ErrorCode),
                (JsonRpcStrings.Data, error.Data ?? new()),
                (JsonRpcStrings.Message, error.Message),
            };

            return
            [
                (JsonRpcStrings.JsonRpc, "2.0"),
                 (JsonRpcStrings.Id, error.Id),
                 (JsonRpcStrings.Error, errorMsg)
            ];
        });

        serializers[typeof(InitializeResponseArgs)] = new JsonObjectSerializer<InitializeResponseArgs>(response =>
        [
            (JsonRpcStrings.ProcessId, response.ProcessId),
                (JsonRpcStrings.ServerInfo, response.ServerInfo),
                (JsonRpcStrings.Capabilities, response.Capabilities)
        ]);

        serializers[typeof(ServerInfo)] = new JsonObjectSerializer<ServerInfo>(info =>
        [
            (JsonRpcStrings.Name, info.Name),
                (JsonRpcStrings.Version, info.Version)
        ]);

        serializers[typeof(ServerCapabilities)] = new JsonObjectSerializer<ServerCapabilities>(capabilities =>
        [
            (JsonRpcStrings.Testing, capabilities.TestingCapabilities)
        ]);

        serializers[typeof(ServerTestingCapabilities)] = new JsonObjectSerializer<ServerTestingCapabilities>(capabilities =>
        [
            (JsonRpcStrings.SupportsDiscovery, capabilities.SupportsDiscovery),
                (JsonRpcStrings.MultiRequestSupport, capabilities.MultiRequestSupport),
                (JsonRpcStrings.VSTestProviderSupport, capabilities.VSTestProviderSupport),
                (JsonRpcStrings.AttachmentsSupport, capabilities.SupportsAttachments),
                (JsonRpcStrings.MultiConnectionProvider, capabilities.MultiConnectionProvider)
        ]);

        serializers[typeof(Artifact)] = new JsonObjectSerializer<Artifact>(artifact =>
        [
            (JsonRpcStrings.Uri, artifact.Uri),
                (JsonRpcStrings.Producer, artifact.Producer),
                (JsonRpcStrings.Type, artifact.Type),
                (JsonRpcStrings.DisplayName, artifact.DisplayName),
                (JsonRpcStrings.Description, artifact.Description)
        ]);

        serializers[typeof(DiscoverResponseArgs)] = new JsonObjectSerializer<DiscoverResponseArgs>(response => []);

        serializers[typeof(RunResponseArgs)] = new JsonObjectSerializer<RunResponseArgs>(response =>
        [
            (JsonRpcStrings.Attachments, response.Artifacts)
        ]);

        serializers[typeof(TestNodeUpdateMessage)] = new JsonObjectSerializer<TestNodeUpdateMessage>(message =>
        [
            (JsonRpcStrings.Node, message.TestNode),
                (JsonRpcStrings.Parent, message.ParentTestNodeUid?.Value)
        ]);

        serializers[typeof(TestNodeStateChangedEventArgs)] = new JsonObjectSerializer<TestNodeStateChangedEventArgs>(message =>
        [
            (JsonRpcStrings.RunId, message.RunId),
                (JsonRpcStrings.Changes, message.Changes)
        ]);

        serializers[typeof(TestNode)] = new JsonObjectSerializer<TestNode>(BuildTestNodeProperties);

        serializers[typeof(LogEventArgs)] = new JsonObjectSerializer<LogEventArgs>(message =>
        [
            (JsonRpcStrings.Level, message.LogMessage.Level.ToString()),
                (JsonRpcStrings.Message, message.LogMessage.Message)
        ]);

        serializers[typeof(CancelRequestArgs)] = new JsonObjectSerializer<CancelRequestArgs>(request =>
        [
            (JsonRpcStrings.Id, request.CancelRequestId)
        ]);

        serializers[typeof(TelemetryEventArgs)] = new JsonObjectSerializer<TelemetryEventArgs>(ev =>
        [
            (JsonRpcStrings.EventName, ev.EventName),
                (JsonRpcStrings.Metrics, ev.Metrics)
        ]);

        serializers[typeof(ProcessInfoArgs)] = new JsonObjectSerializer<ProcessInfoArgs>(info =>
        [
            (JsonRpcStrings.Program, info.Program),
                (JsonRpcStrings.Args, info.Args),
                (JsonRpcStrings.WorkingDirectory, info.WorkingDirectory),
                (JsonRpcStrings.EnvironmentVariables, info.EnvironmentVariables)
        ]);

        serializers[typeof(AttachDebuggerInfoArgs)] = new JsonObjectSerializer<AttachDebuggerInfoArgs>(info =>
        [
            (JsonRpcStrings.ProcessId, info.ProcessId)
        ]);

        serializers[typeof(TestsAttachments)] = new JsonObjectSerializer<TestsAttachments>(info =>
        [
            (JsonRpcStrings.Attachments, info.Attachments)
        ]);

        serializers[typeof(RunTestAttachment)] = new JsonObjectSerializer<RunTestAttachment>(info =>
        [
            (JsonRpcStrings.Uri, info.Uri),
                    (JsonRpcStrings.Producer, info.Producer),
                    (JsonRpcStrings.Type, info.Type),
                    (JsonRpcStrings.DisplayName, info.DisplayName),
                    (JsonRpcStrings.Description, info.Description)
        ]);

        // Serializers
        serializers[typeof(string)] = new JsonValueSerializer<string>((w, v) => w.WriteStringValue(v));
        serializers[typeof(bool)] = new JsonValueSerializer<bool>((w, v) => w.WriteBooleanValue(v));
        serializers[typeof(char)] = new JsonValueSerializer<char>((w, v) => w.WriteStringValue($"{v}"));
        serializers[typeof(int)] = new JsonValueSerializer<int>((w, v) => w.WriteNumberValue(v));
        serializers[typeof(long)] = new JsonValueSerializer<long>((w, v) => w.WriteNumberValue(v));
        serializers[typeof(float)] = new JsonValueSerializer<float>((w, v) => w.WriteNumberValue(v));
        serializers[typeof(double)] = new JsonValueSerializer<double>((w, v) => w.WriteNumberValue(v));
        serializers[typeof(decimal)] = new JsonValueSerializer<decimal>((w, v) => w.WriteNumberValue(v));
        serializers[typeof(Guid)] = new JsonValueSerializer<Guid>((w, v) => w.WriteStringValue(v));
        serializers[typeof(DateTime)] = new JsonValueSerializer<DateTime>((w, v) => w.WriteRawValue($"\"{v:o}\"", skipInputValidation: true));
        serializers[typeof(DateTimeOffset)] = new JsonValueSerializer<DateTimeOffset>((w, v) => w.WriteRawValue($"\"{v:o}\"", skipInputValidation: true));

        // serializers[typeof(TimeSpan)] = new JsonValueSerializer<TimeSpan>((w, v) => w.WriteStringValue(v.ToString())); // Remove for now
        serializers[typeof((string, object?)[])] = new JsonObjectSerializer<(string, object?)[]>(n => n);
        serializers[typeof(Dictionary<string, object>)] = new JsonObjectSerializer<Dictionary<string, object>>(d => [.. d.Select(kvp => (kvp.Key, (object?)kvp.Value))]);
    }
}
