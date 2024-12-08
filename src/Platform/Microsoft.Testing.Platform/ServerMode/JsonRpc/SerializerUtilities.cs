// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

// Note: System.Text.Json is only available in .NET 6.0 and above.
//       As such, we have two separate implementations for the serialization code.
#if !NETCOREAPP
using Jsonite;
#endif
using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Helpers;

namespace Microsoft.Testing.Platform.ServerMode;

internal static class SerializerUtilities
{
    private static readonly Dictionary<Type, IObjectSerializer> Serializers;
    private static readonly Dictionary<Type, IObjectDeserializer> Deserializers;

    /// <summary>
    /// Initializes static members of the <see cref="SerializerUtilities"/> class.
    /// Is a known fact that this serializer jsonite based suffer of boxing/unboxing issue but it's only for netstandard2.0 and we don't optimize for it for now.
    /// We aim to rewrite all the serialization with the best perfomance using .NET System.Text.Json api inside the Json.cs file.
    /// </summary>
    static SerializerUtilities()
    {
        Serializers = [];
        Deserializers = [];

        Serializers[typeof(object)] = new ObjectSerializer<object>(_ => new Dictionary<string, object?>());
        Serializers[typeof(KeyValuePair<string, string>)] = new ObjectSerializer<KeyValuePair<string, string>>(o =>
        {
            Dictionary<string, object?> values = new()
            {
                { o.Key, o.Value },
            };
            return values;
        });

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

        Serializers[typeof(Artifact)] = new ObjectSerializer<Artifact>(res => new Dictionary<string, object?>
        {
            [JsonRpcStrings.Uri] = res.Uri,
            [JsonRpcStrings.Producer] = res.Producer,
            [JsonRpcStrings.Type] = res.Type,
            [JsonRpcStrings.DisplayName] = res.DisplayName,
            [JsonRpcStrings.Description] = res.Description,
        });

        Serializers[typeof(DiscoverResponseArgs)] = new ObjectSerializer<DiscoverResponseArgs>(_ => new Dictionary<string, object?>());

        Serializers[typeof(RunResponseArgs)] = new ObjectSerializer<RunResponseArgs>(res => new Dictionary<string, object?>
        {
            [JsonRpcStrings.Attachments] = res.Artifacts.Select(f => Serialize(f)).ToList<object>(),
        });

        Serializers[typeof(TestNodeUpdateMessage)] = new ObjectSerializer<TestNodeUpdateMessage>(ev =>
        {
            // TODO: Fill in the node properties
            Dictionary<string, object?> values = new()
            {
                [JsonRpcStrings.Node] = Serialize(ev.TestNode),
                [JsonRpcStrings.Parent] = ev.ParentTestNodeUid?.Value,
            };

            return values;
        });

        // Serialize event types.
        Serializers[typeof(TestNodeStateChangedEventArgs)] = new ObjectSerializer<TestNodeStateChangedEventArgs>(ev =>
        {
            Dictionary<string, object?> values = new()
            {
                [JsonRpcStrings.RunId] = ev.RunId,
                [JsonRpcStrings.Changes] = ev.Changes?.Select(ch => Serialize(ch)).ToList<object>(),
            };

            return values;
        });

        Serializers[typeof(TestNode)] = new ObjectSerializer<TestNode>(
            n =>
            {
                // RECALL TO UPDATE TESTS INSIDE FormatterUtilitiesTests.cs
                Dictionary<string, object?> properties = new()
                {
                    [JsonRpcStrings.Uid] = n.Uid.Value,
                    [JsonRpcStrings.DisplayName] = n.DisplayName,
                };

                TestMetadataProperty[] metadataProperties = n.Properties.OfType<TestMetadataProperty>();
                if (metadataProperties.Length > 0)
                {
#if NETCOREAPP
                    properties["traits"] = metadataProperties.Select(x => new KeyValuePair<string, string>(x.Key, x.Value));
#else
                    JsonArray collection = [];
                    foreach (TestMetadataProperty metadata in metadataProperties)
                    {
                        JsonObject o = new()
                        {
                            { metadata.Key, metadata.Value },
                        };
                        collection.Add(o);
                    }

                    properties["traits"] = collection;
#endif
                }

                foreach (IProperty property in n.Properties)
                {
                    if (property is SerializableKeyValuePairStringProperty keyValuePairProperty)
                    {
                        properties[keyValuePairProperty.Key] = keyValuePairProperty.Value;
                        continue;
                    }

                    if (property is SerializableNamedArrayStringProperty namedArrayStringProperty)
                    {
                        properties[namedArrayStringProperty.Name] = namedArrayStringProperty.Values;
                        continue;
                    }

                    if (property is SerializableNamedKeyValuePairsStringProperty namedKvpStringProperty)
                    {
#if NETCOREAPP
                        properties[namedKvpStringProperty.Name] = namedKvpStringProperty.Pairs;
#else
                        Jsonite.JsonArray collection = [];
                        foreach (KeyValuePair<string, string> item in namedKvpStringProperty.Pairs)
                        {
                            Jsonite.JsonObject o = new()
                            {
                                { item.Key, item.Value },
                            };
                            collection.Add(o);
                        }

                        properties[namedKvpStringProperty.Name] = collection;
#endif
                        continue;
                    }

                    if (property is TestFileLocationProperty fileLocationProperty)
                    {
                        properties["location.file"] = fileLocationProperty.FilePath;
                        properties["location.line-start"] = fileLocationProperty.LineSpan.Start.Line;
                        properties["location.line-end"] = fileLocationProperty.LineSpan.End.Line;
                        continue;
                    }

                    if (property is TestMethodIdentifierProperty testMethodIdentifierProperty)
                    {
                        properties["location.namespace"] = testMethodIdentifierProperty.Namespace;
                        properties["location.type"] = testMethodIdentifierProperty.TypeName;
                        properties["location.method"] = testMethodIdentifierProperty.ParameterTypeFullNames.Length > 0
                            ? $"{testMethodIdentifierProperty.MethodName}({string.Join(",", testMethodIdentifierProperty.ParameterTypeFullNames)})"
                            : testMethodIdentifierProperty.MethodName;
                        continue;
                    }

                    if (property is StandardOutputProperty consoleStandardOutputProperty)
                    {
                        properties["standardOutput"] = consoleStandardOutputProperty.StandardOutput;
                        continue;
                    }

                    if (property is StandardErrorProperty standardErrorProperty)
                    {
                        properties["standardError"] = standardErrorProperty.StandardError;
                        continue;
                    }

                    if (property is TestNodeStateProperty testNodeStateProperty)
                    {
                        properties["node-type"] = "action";
                        switch (property)
                        {
                            case DiscoveredTestNodeStateProperty:
                                {
                                    properties["execution-state"] = "discovered";
                                    break;
                                }

                            case InProgressTestNodeStateProperty:
                                {
                                    properties["execution-state"] = "in-progress";
                                    break;
                                }

                            case PassedTestNodeStateProperty:
                                {
                                    properties["execution-state"] = "passed";
                                    break;
                                }

                            case SkippedTestNodeStateProperty skippedTestNodeStateProperty:
                                {
                                    properties["execution-state"] = "skipped";

                                    if (!RoslynString.IsNullOrEmpty(skippedTestNodeStateProperty.Explanation))
                                    {
                                        properties["error.message"] = skippedTestNodeStateProperty.Explanation;
                                    }

                                    break;
                                }

                            case FailedTestNodeStateProperty failedTestNodeStateProperty:
                                {
                                    properties["execution-state"] = "failed";
                                    properties["error.message"] = failedTestNodeStateProperty.Explanation ?? failedTestNodeStateProperty.Exception?.Message;
                                    if (failedTestNodeStateProperty.Exception != null)
                                    {
                                        Exception exception = failedTestNodeStateProperty.Exception;
                                        properties["error.stacktrace"] = exception.StackTrace ?? string.Empty;
                                        properties["assert.actual"] = exception.Data["assert.actual"] ?? string.Empty;
                                        properties["assert.expected"] = exception.Data["assert.expected"] ?? string.Empty;
                                    }

                                    break;
                                }

                            case TimeoutTestNodeStateProperty timeoutTestNodeStateProperty:
                                {
                                    properties["execution-state"] = "timed-out";
                                    properties["error.message"] = timeoutTestNodeStateProperty.Explanation ?? timeoutTestNodeStateProperty.Exception?.Message;
                                    if (timeoutTestNodeStateProperty.Exception != null)
                                    {
                                        properties["error.stacktrace"] = timeoutTestNodeStateProperty.Exception.StackTrace ?? string.Empty;
                                    }

                                    break;
                                }

                            case ErrorTestNodeStateProperty errorTestNodeStateProperty:
                                {
                                    properties["execution-state"] = "error";
                                    properties["error.message"] = errorTestNodeStateProperty.Explanation ?? errorTestNodeStateProperty.Exception?.Message;
                                    if (errorTestNodeStateProperty.Exception != null)
                                    {
                                        properties["error.stacktrace"] = errorTestNodeStateProperty.Exception.StackTrace ?? string.Empty;
                                    }

                                    break;
                                }

                            case CancelledTestNodeStateProperty canceledTestNodeStateProperty:
                                {
                                    properties["execution-state"] = "canceled";
                                    properties["error.message"] = canceledTestNodeStateProperty.Explanation ?? canceledTestNodeStateProperty.Exception?.Message;
                                    if (canceledTestNodeStateProperty.Exception != null)
                                    {
                                        properties["error.stacktrace"] = canceledTestNodeStateProperty.Exception.StackTrace ?? string.Empty;
                                    }

                                    break;
                                }

                            default:
                                throw new NotSupportedException($"Unsupported TestNodeStateProperty '{testNodeStateProperty.GetType()}'");
                        }

                        continue;
                    }

                    if (property is TimingProperty timingProperty)
                    {
                        properties["time.start-utc"] = timingProperty.GlobalTiming.StartTime;
                        properties["time.stop-utc"] = timingProperty.GlobalTiming.EndTime;
                        properties["time.duration-ms"] = timingProperty.GlobalTiming.Duration.TotalMilliseconds;
                        continue;
                    }
                }

                if (!properties.ContainsKey("node-type"))
                {
                    properties["node-type"] = "group";
                }

                return properties;
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
                Jsonite.JsonArray collection = [];
                foreach (KeyValuePair<string, string?> item in ev.EnvironmentVariables)
                {
                    Jsonite.JsonObject o = new()
                    {
                        { item.Key, item.Value },
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

        // Deserialize a generic JSON-RPC message
        Deserializers[typeof(RpcMessage)] = new ObjectDeserializer<RpcMessage>(properties =>
        {
            ValidateJsonRpcHeader(properties);

            if (properties.TryGetValue(JsonRpcStrings.Method, out object? methodObj) && methodObj is not null)
            {
                string method = (string)methodObj;

                object? idObj = GetOptionalPropertyFromJson(properties, JsonRpcStrings.Id);

                IDictionary<string, object?> paramsObj = method != JsonRpcMethods.Exit
                    ? GetRequiredPropertyFromJson<IDictionary<string, object?>>(properties, JsonRpcStrings.Params)
                    : new Dictionary<string, object?>();

                int? id = idObj is null
                            ? null
                            : GetIdFromJson(idObj) ?? throw new MessageFormatException("id field should be a string or an int");

                // TODO: Should we be forgiving in the deserialization (if it throws)?
                // This way we'd be able to send an ErrorMessage back to the client,
                // rather than send a log notification.

                // Parse the specific methods
                object? @params = method switch
                {
                    JsonRpcMethods.Initialize => Deserialize<InitializeRequestArgs>(paramsObj),
                    JsonRpcMethods.TestingDiscoverTests => Deserialize<DiscoverRequestArgs>(paramsObj),
                    JsonRpcMethods.TestingRunTests => Deserialize<RunRequestArgs>(paramsObj),
                    JsonRpcMethods.CancelRequest => Deserialize<CancelRequestArgs>(paramsObj),
                    JsonRpcMethods.Exit => Deserialize<ExitRequestArgs>(paramsObj),

                    // Note: Let the server report unknown RPC request back to the client.
                    _ => null,
                };

                return id.HasValue
                    ? new RequestMessage(id.Value, method, @params)
                    : new NotificationMessage(method, @params);
            }
            else if (properties.TryGetValue(JsonRpcStrings.Error, out object? errorObj))
            {
                return Deserialize<ErrorMessage>(properties);
            }
            else if (properties.TryGetValue(JsonRpcStrings.Result, out object? resultObj))
            {
                // Note: Because the result message does not contain the original method name,
                //       it's not possible for us to do a typed deserialization.
                //       The best option we've got is to return a generic property bag.
                object? idObj = GetRequiredPropertyFromJson<object?>(properties, JsonRpcStrings.Id);
                var paramsObj = resultObj as IDictionary<string, object>;

                int id = GetIdFromJson(idObj) ?? throw new MessageFormatException("id field should be a string or an int");

                return new ResponseMessage(id, paramsObj);
            }

            throw new MessageFormatException();
        });

        // Deserialize requests
        Deserializers[typeof(InitializeRequestArgs)] = new ObjectDeserializer<InitializeRequestArgs>(properties =>
        {
            int processId = GetRequiredPropertyFromJson<int>(properties, JsonRpcStrings.ProcessId);
            ClientInfo clientInfo = Deserialize<ClientInfo>(properties);
            ClientCapabilities capabilities = Deserialize<ClientCapabilities>(properties);

            return new InitializeRequestArgs(processId, clientInfo, capabilities);
        });

        Deserializers[typeof(ClientInfo)] = new ObjectDeserializer<ClientInfo>(properties =>
        {
            IDictionary<string, object?> info = GetRequiredPropertyFromJson<IDictionary<string, object?>>(properties, JsonRpcStrings.ClientInfo);
            string name = GetRequiredPropertyFromJson<string>(info, JsonRpcStrings.Name);
            string protocolVersion = GetRequiredPropertyFromJson<string>(info, JsonRpcStrings.Version);

            return new ClientInfo(name, protocolVersion);
        });

        Deserializers[typeof(ClientCapabilities)] = new ObjectDeserializer<ClientCapabilities>(properties =>
        {
            IDictionary<string, object?> capabilities = GetRequiredPropertyFromJson<IDictionary<string, object?>>(properties, JsonRpcStrings.Capabilities);
            IDictionary<string, object?> testingCapabilities = GetRequiredPropertyFromJson<IDictionary<string, object?>>(capabilities, JsonRpcStrings.Testing);
            bool debuggerProvider = GetRequiredPropertyFromJson<bool>(testingCapabilities, JsonRpcStrings.DebuggerProvider);

            return new ClientCapabilities(debuggerProvider);
        });

        Deserializers[typeof(InitializeResponseArgs)] = new ObjectDeserializer<InitializeResponseArgs>(properties =>
        {
            int processId = GetRequiredPropertyFromJson<int>(properties, JsonRpcStrings.ProcessId);
            ServerInfo serverInfo = Deserialize<ServerInfo>(GetRequiredPropertyFromJson<IDictionary<string, object?>>(properties, JsonRpcStrings.ServerInfo));
            ServerCapabilities capabilities = Deserialize<ServerCapabilities>(GetRequiredPropertyFromJson<IDictionary<string, object?>>(properties, JsonRpcStrings.Capabilities));

            return new InitializeResponseArgs(processId, serverInfo, capabilities);
        });

        Deserializers[typeof(ServerInfo)] = new ObjectDeserializer<ServerInfo>(properties =>
        {
            string name = GetRequiredPropertyFromJson<string>(properties, JsonRpcStrings.Name);
            string protocolVersion = GetRequiredPropertyFromJson<string>(properties, JsonRpcStrings.Version);

            return new ServerInfo(name, protocolVersion);
        });

        Deserializers[typeof(ServerCapabilities)] = new ObjectDeserializer<ServerCapabilities>(properties =>
        {
            IDictionary<string, object?> testingCapabilities = GetRequiredPropertyFromJson<IDictionary<string, object?>>(properties, JsonRpcStrings.Testing);
            bool supportsDiscovery = GetRequiredPropertyFromJson<bool>(testingCapabilities, JsonRpcStrings.SupportsDiscovery);
            bool multiRequestSupport = GetRequiredPropertyFromJson<bool>(testingCapabilities, JsonRpcStrings.MultiRequestSupport);
            bool vstestProviderSupport = GetRequiredPropertyFromJson<bool>(testingCapabilities, JsonRpcStrings.VSTestProviderSupport);
            bool attachmentsSupport = GetRequiredPropertyFromJson<bool>(testingCapabilities, JsonRpcStrings.AttachmentsSupport);
            bool multiConnectionProvider = GetRequiredPropertyFromJson<bool>(testingCapabilities, JsonRpcStrings.MultiConnectionProvider);

            return new ServerCapabilities(new ServerTestingCapabilities(
                SupportsDiscovery: supportsDiscovery,
                MultiRequestSupport: multiRequestSupport,
                VSTestProviderSupport: vstestProviderSupport,
                SupportsAttachments: attachmentsSupport,
                MultiConnectionProvider: multiConnectionProvider));
        });

        Deserializers[typeof(DiscoverRequestArgs)] = new ObjectDeserializer<DiscoverRequestArgs>(properties =>
        {
            _ = Guid.TryParse(GetRequiredPropertyFromJson<string>(properties, JsonRpcStrings.RunId), out Guid runId);

            var testsJson = GetOptionalPropertyFromJson(properties, JsonRpcStrings.Tests) as ICollection<object>;

            ICollection<TestNode>? tests = testsJson?.OfType<IDictionary<string, object?>>()?.Select(obj => Deserialize<TestNode>(obj)).ToList();

            string? filter = GetOptionalPropertyFromJson(properties, JsonRpcStrings.Filter) as string;

            return new DiscoverRequestArgs(runId, tests, filter);
        });

        Deserializers[typeof(RunRequestArgs)] = new ObjectDeserializer<RunRequestArgs>(properties =>
        {
            _ = Guid.TryParse(GetRequiredPropertyFromJson<string>(properties, JsonRpcStrings.RunId), out Guid runId);

            var testsJson = GetOptionalPropertyFromJson(properties, JsonRpcStrings.Tests) as ICollection<object>;

            ICollection<TestNode>? tests = testsJson?.OfType<IDictionary<string, object?>>().Select(obj => Deserialize<TestNode>(obj)).ToList();
            string? filter = GetOptionalPropertyFromJson(properties, JsonRpcStrings.Filter) as string;

            return new RunRequestArgs(runId, tests, filter);
        });

        Deserializers[typeof(TestNode)] = new ObjectDeserializer<TestNode>(
            properties =>
            {
                string uid = string.Empty;
                string displayName = string.Empty;
                PropertyBag propertyBag = new();

                foreach (KeyValuePair<string, object?> p in properties)
                {
                    if (p.Key == JsonRpcStrings.Uid)
                    {
                        uid = p.Value as string ?? string.Empty;
                        continue;
                    }

                    if (p.Key == JsonRpcStrings.DisplayName)
                    {
                        displayName = p.Value as string ?? string.Empty;
                        continue;
                    }
                }

                if (properties.TryGetValue("location.file", out object? location_file))
                {
                    ApplicationStateGuard.Ensure(location_file is not null);
                    if (properties.TryGetValue("location.line-start", out object? location_lineStart) && properties.TryGetValue("location.line-end", out object? location_lineEnd))
                    {
                        ApplicationStateGuard.Ensure(location_lineStart is not null);
                        ApplicationStateGuard.Ensure(location_lineEnd is not null);
                        TestFileLocationProperty testFileLocationProperty = new(
                            (string)location_file,
                            new LinePositionSpan(new LinePosition((int)location_lineStart, 0), new LinePosition((int)location_lineEnd, 0)));
                        propertyBag.Add(testFileLocationProperty);
                    }
                }

                return new TestNode
                {
                    Uid = new TestNodeUid(uid),
                    DisplayName = displayName,
                    Properties = propertyBag,
                };
            });

        Deserializers[typeof(CancelRequestArgs)] = new ObjectDeserializer<CancelRequestArgs>(properties =>
        {
            object? idObj = GetOptionalPropertyFromJson(properties, JsonRpcStrings.Id);
            int id = GetIdFromJson(idObj) ?? throw new MessageFormatException("id field should be a string or an int");

            return new CancelRequestArgs(id);
        });

        Deserializers[typeof(ExitRequestArgs)] = new ObjectDeserializer<ExitRequestArgs>(_ => new ExitRequestArgs());

        // Deserialize an error
        Deserializers[typeof(ErrorMessage)] = new ObjectDeserializer<ErrorMessage>(properties =>
        {
            ValidateJsonRpcHeader(properties);
            object idObj = GetRequiredPropertyFromJson<object>(properties, JsonRpcStrings.Id);
            IDictionary<string, object> errorObj = GetRequiredPropertyFromJson<IDictionary<string, object>>(properties, JsonRpcStrings.Error);

#if !NETCOREAPP
            if (errorObj.TryGetValue(JsonRpcStrings.Data, out object? errorData))
            {
                if (errorData is JsonObject { Count: 0 })
                {
                    errorObj[JsonRpcStrings.Data] = null!;
                }
            }
#endif
            int id = GetIdFromJson(idObj) ?? throw new MessageFormatException("id field should be a string or an int");

            if (!errorObj.TryGetValue(JsonRpcStrings.Code, out object? codeObj) ||
                codeObj is not int code)
            {
                throw new MessageFormatException("error.code field missing");
            }

            if (!errorObj.TryGetValue(JsonRpcStrings.Message, out object? errorMessageObj) ||
                errorMessageObj is not string errorMessage)
            {
                throw new MessageFormatException("error.message field is missing");
            }

            object? data = errorObj.TryGetValue(JsonRpcStrings.Data, out object? dataJson)
                ? dataJson
                : null;

            return new ErrorMessage(
                Id: id,
                ErrorCode: code,
                Message: errorMessage,
                Data: data);
        });
    }

    public static IEnumerable<Type> SerializerTypes => Serializers.Keys;

    public static IEnumerable<Type> DeserializerTypes => Deserializers.Keys;

    public static T Deserialize<T>(IDictionary<string, object?> properties)
        => (T)Deserialize(typeof(T), properties);

    public static object Deserialize(Type t, IDictionary<string, object?> properties)
    {
        IObjectDeserializer deserializer = Deserializers[t];
        return deserializer.DeserializeObject(properties) ?? throw new InvalidOperationException();
    }

    public static IDictionary<string, object?> Serialize<T>(T obj)
        => Serialize(typeof(T), obj!);

    public static IDictionary<string, object?> SerializeObject(object obj)
        => Serialize(obj.GetType(), obj);

    public static IDictionary<string, object?> Serialize(Type t, object obj)
    {
        IObjectSerializer serializer = Serializers[t];
        return serializer.SerializeObject(obj);
    }

    private static void ValidateJsonRpcHeader(IDictionary<string, object?> properties)
    {
        // Note: The test anywhere supports only JSON-RPC version 2.0
        if (!properties.TryGetValue(JsonRpcStrings.JsonRpc, out object? rpcVersionObj)
            || rpcVersionObj is not string rpcVersion
            || rpcVersion != "2.0")
        {
            throw new MessageFormatException("jsonrpc field is not valid");
        }
    }

    private static object? GetOptionalPropertyFromJson(IDictionary<string, object?> properties, string propertyName)
        => properties.TryGetValue(propertyName, out object? propObj) ? propObj : null;

    private static T GetRequiredPropertyFromJson<T>(IDictionary<string, object?> properties, string propertyName)
        => (T)(GetOptionalPropertyFromJson(properties, propertyName) ?? throw new MessageFormatException($"'{propertyName}' field is missing"));

    private static int? GetIdFromJson(object? idObj)
        => idObj switch
        {
            int idInt => idInt,
            string idStr => int.TryParse(idStr, out int id)
                ? id
                : null,
            _ => null,
        };
}
