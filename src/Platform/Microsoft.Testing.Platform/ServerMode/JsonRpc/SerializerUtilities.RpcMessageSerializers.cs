// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

// Note: System.Text.Json is only available in .NET 6.0 and above.
//       As such, we have two separate implementations for the serialization code.
#if !NETCOREAPP
using Jsonite;
#endif

namespace Microsoft.Testing.Platform.ServerMode;

internal static partial class SerializerUtilities
{
    private static void RegisterRpcMessageSerializers()
    {
        // Serialize response types.
        Serializers[typeof(RequestMessage)] = new ObjectSerializer<RequestMessage>(req =>
        {
            Dictionary<string, object?> values = new()
            {
                [JsonRpcStrings.JsonRpc] = "2.0",
                [JsonRpcStrings.Id] = req.Id,
                [JsonRpcStrings.Method] = req.Method,
                [JsonRpcStrings.Params] = req.Params is null ? null : SerializeObject(req.Params),
            };

            return values;
        });

        Serializers[typeof(ResponseMessage)] = new ObjectSerializer<ResponseMessage>(res =>
        {
            Dictionary<string, object?> values = new()
            {
                [JsonRpcStrings.JsonRpc] = "2.0",
                [JsonRpcStrings.Id] = res.Id,
                [JsonRpcStrings.Result] = res.Result is null ? null : SerializeObject(res.Result),
            };

            return values;
        });

        Serializers[typeof(NotificationMessage)] = new ObjectSerializer<NotificationMessage>(notification =>
        {
            Dictionary<string, object?> values = new()
            {
                [JsonRpcStrings.JsonRpc] = "2.0",
                [JsonRpcStrings.Method] = notification.Method,
                [JsonRpcStrings.Params] = notification.Params is null ? null : SerializeObject(notification.Params),
            };

            return values;
        });

        Serializers[typeof(ErrorMessage)] = new ObjectSerializer<ErrorMessage>(error =>
        {
            Dictionary<string, object?> values = new()
            {
                [JsonRpcStrings.JsonRpc] = "2.0",
                [JsonRpcStrings.Code] = error.ErrorCode,
                [JsonRpcStrings.Id] = error.Id,
                [JsonRpcStrings.Error] = new Dictionary<string, object?>
                {
                    [JsonRpcStrings.Code] = error.ErrorCode,
                    [JsonRpcStrings.Data] = SerializeObject(error.Data ?? new()),
                    [JsonRpcStrings.Message] = error.Message,
                },
            };

            return values;
        });

        Serializers[typeof(InitializeResponseArgs)] = new ObjectSerializer<InitializeResponseArgs>(res =>
        {
            Dictionary<string, object?> values = new()
            {
                [JsonRpcStrings.ProcessId] = res.ProcessId,
                [JsonRpcStrings.ServerInfo] = Serialize(res.ServerInfo),
                [JsonRpcStrings.Capabilities] = Serialize(res.Capabilities),
            };

            return values;
        });

        Serializers[typeof(ServerInfo)] = new ObjectSerializer<ServerInfo>(info =>
        {
            Dictionary<string, object?> values = new()
            {
                [JsonRpcStrings.Name] = info.Name,
                [JsonRpcStrings.Version] = info.Version,
            };

            return values;
        });

        Serializers[typeof(ServerCapabilities)] = new ObjectSerializer<ServerCapabilities>(capabilities => new Dictionary<string, object?>
        {
            [JsonRpcStrings.Testing] = Serialize(capabilities.TestingCapabilities),
        });

        Serializers[typeof(ServerTestingCapabilities)] = new ObjectSerializer<ServerTestingCapabilities>(capabilities => new Dictionary<string, object?>
        {
            [JsonRpcStrings.SupportsDiscovery] = capabilities.SupportsDiscovery,
            [JsonRpcStrings.MultiRequestSupport] = capabilities.MultiRequestSupport,
            [JsonRpcStrings.VSTestProviderSupport] = capabilities.VSTestProviderSupport,
            [JsonRpcStrings.AttachmentsSupport] = capabilities.SupportsAttachments,
            [JsonRpcStrings.MultiConnectionProvider] = capabilities.MultiConnectionProvider,
        });

        Serializers[typeof(LogEventArgs)] = new ObjectSerializer<LogEventArgs>(ev =>
        {
            Dictionary<string, object?> values = new()
            {
                [JsonRpcStrings.Level] = ev.LogMessage.Level.ToString(),
                [JsonRpcStrings.Message] = ev.LogMessage.Message,
            };

            return values;
        });

        Serializers[typeof(CancelRequestArgs)] = new ObjectSerializer<CancelRequestArgs>(ev =>
        {
            Dictionary<string, object?> values = new()
            {
                [JsonRpcStrings.Id] = ev.CancelRequestId,
            };

            return values;
        });

        Serializers[typeof(TelemetryEventArgs)] = new ObjectSerializer<TelemetryEventArgs>(ev =>
        {
            Dictionary<string, object?> values = new()
            {
                [JsonRpcStrings.EventName] = ev.EventName,
                [JsonRpcStrings.Metrics] = ev.Metrics,
            };

            return values;
        });

        Serializers[typeof(ProcessInfoArgs)] = new ObjectSerializer<ProcessInfoArgs>(ev =>
        {
            Dictionary<string, object?> values = new()
            {
                [JsonRpcStrings.Program] = ev.Program,
                [JsonRpcStrings.Args] = ev.Args,
                [JsonRpcStrings.WorkingDirectory] = ev.WorkingDirectory,
            };

            if (ev.EnvironmentVariables is not null)
            {
#if NETCOREAPP
                values[JsonRpcStrings.EnvironmentVariables] = ev.EnvironmentVariables;
#else
                JsonArray collection = [];
                foreach (KeyValuePair<string, string?> kvp in ev.EnvironmentVariables)
                {
                    JsonObject o = new()
                    {
                        { kvp.Key, kvp.Value },
                    };
                    collection.Add(o);
                }

                values[JsonRpcStrings.EnvironmentVariables] = collection;
#endif
            }

            return values;
        });

        Serializers[typeof(AttachDebuggerInfoArgs)] = new ObjectSerializer<AttachDebuggerInfoArgs>(ev =>
        {
            Dictionary<string, object?> values = new()
            {
                [JsonRpcStrings.ProcessId] = ev.ProcessId,
            };

            return values;
        });

        Serializers[typeof(TestsAttachments)] = new ObjectSerializer<TestsAttachments>(ev =>
        {
            Dictionary<string, object?> values = new()
            {
                [JsonRpcStrings.Attachments] = ev.Attachments.Select(x => Serialize(x)).ToArray(),
            };

            return values;
        });

        Serializers[typeof(RunTestAttachment)] = new ObjectSerializer<RunTestAttachment>(ev =>
        {
            Dictionary<string, object?> values = new()
            {
                [JsonRpcStrings.Uri] = ev.Uri,
                [JsonRpcStrings.Producer] = ev.Producer,
                [JsonRpcStrings.Type] = ev.Type,
                [JsonRpcStrings.DisplayName] = ev.DisplayName,
                [JsonRpcStrings.Description] = ev.Description,
            };

            return values;
        });
    }
}
