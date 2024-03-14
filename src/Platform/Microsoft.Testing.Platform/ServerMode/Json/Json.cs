// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections;
using System.Text;
using System.Text.Json;

using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Helpers;

namespace Microsoft.Testing.Platform.ServerMode.Json;

internal sealed class Json
{
    private readonly Dictionary<Type, JsonDeserializer> _deserializers = [];
    private readonly Dictionary<Type, JsonSerializer> _serializers = [];
    private readonly ObjectPool<MemoryStream> _memoryStreamPool = new(() => new MemoryStream());

    public Json(Dictionary<Type, JsonSerializer>? serializers = null, Dictionary<Type, JsonDeserializer>? deserializers = null)
    {
        // Overridden default serializers for better performance using .NET runtime serialization APIs

        // Serialize response types.
        _serializers[typeof(RequestMessage)] = new JsonObjectSerializer<RequestMessage>(request =>
         {
             var list = new (string, object?)[4]
             {
                 (JsonRpcStrings.JsonRpc, "2.0"),
                 (JsonRpcStrings.Id, request.Id),
                 (JsonRpcStrings.Method, request.Method),
                 (JsonRpcStrings.Params, request.Params),
             };

             return list;
         });

        _serializers[typeof(ResponseMessage)] = new JsonObjectSerializer<ResponseMessage>(response =>
        {
            var list = new (string, object?)[3]
            {
                 (JsonRpcStrings.JsonRpc, "2.0"),
                 (JsonRpcStrings.Id, response.Id),
                 (JsonRpcStrings.Result, response.Result),
            };

            return list;
        });

        _serializers[typeof(NotificationMessage)] = new JsonObjectSerializer<NotificationMessage>(notification =>
        {
            var list = new (string, object?)[3]
            {
                 (JsonRpcStrings.JsonRpc, "2.0"),
                 (JsonRpcStrings.Method, notification.Method),
                 (JsonRpcStrings.Params, notification.Params),
            };

            return list;
        });

        _serializers[typeof(ErrorMessage)] = new JsonObjectSerializer<ErrorMessage>(error =>
        {
            object errorMsg = new Dictionary<string, object?>(3)
            {
                { JsonRpcStrings.Code, error.ErrorCode },
                { JsonRpcStrings.Data, error.Data ?? new() },
                { JsonRpcStrings.Message, error.Message },
            };

            var list = new (string, object?)[4]
            {
                 (JsonRpcStrings.JsonRpc, "2.0"),
                 (JsonRpcStrings.Code, error.ErrorCode),
                 (JsonRpcStrings.Id, error.Id),
                 (JsonRpcStrings.Error, errorMsg),
            };

            return list;
        });

        _serializers[typeof(InitializeResponseArgs)] = new JsonObjectSerializer<InitializeResponseArgs>(response =>
        {
            var list = new (string, object?)[2]
            {
                (JsonRpcStrings.ServerInfo, response.ServerInfo),
                (JsonRpcStrings.Capabilities, response.Capabilities),
            };

            return list;
        });

        _serializers[typeof(ServerInfo)] = new JsonObjectSerializer<ServerInfo>(info =>
        {
            var list = new (string, object?)[2]
            {
                (JsonRpcStrings.Name, info.Name),
                (JsonRpcStrings.Version, info.Version),
            };

            return list;
        });

        _serializers[typeof(ServerCapabilities)] = new JsonObjectSerializer<ServerCapabilities>(capabilities =>
        {
            var list = new (string, object?)[1]
            {
                (JsonRpcStrings.Testing, capabilities.TestingCapabilities),
            };

            return list;
        });

        _serializers[typeof(ServerTestingCapabilities)] = new JsonObjectSerializer<ServerTestingCapabilities>(capabilities =>
        {
            var list = new (string, object?)[3]
            {
                (JsonRpcStrings.SupportsDiscovery, capabilities.SupportsDiscovery),
                (JsonRpcStrings.MultiRequestSupport, capabilities.MultiRequestSupport),
                (JsonRpcStrings.VSTestProviderSupport, capabilities.VSTestProviderSupport),
            };

            return list;
        });

        _serializers[typeof(Artifact)] = new JsonObjectSerializer<Artifact>(artifact =>
        {
            var list = new (string, object?)[5]
            {
                (JsonRpcStrings.Uri, artifact.Uri),
                (JsonRpcStrings.Producer, artifact.Producer),
                (JsonRpcStrings.Type, artifact.Type),
                (JsonRpcStrings.DisplayName, artifact.DisplayName),
                (JsonRpcStrings.Description, artifact.Description),
            };

            return list;
        });

        _serializers[typeof(DiscoverResponseArgs)] = new JsonObjectSerializer<DiscoverResponseArgs>(response =>
        {
            return Array.Empty<(string, object?)>();
        });

        _serializers[typeof(RunResponseArgs)] = new JsonObjectSerializer<RunResponseArgs>(response =>
        {
            var list = new (string, object?)[1]
            {
                (JsonRpcStrings.Attachments, response.Artifacts),
            };

            return list;
        });

        _serializers[typeof(TestNodeUpdateMessage)] = new JsonObjectSerializer<TestNodeUpdateMessage>(message =>
        {
            var list = new (string, object?)[2]
            {
                (JsonRpcStrings.Node, message.TestNode),
                (JsonRpcStrings.Parent, message.ParentTestNodeUid?.Value),
            };

            return list;
        });

        _serializers[typeof(TestNodeStateChangedEventArgs)] = new JsonObjectSerializer<TestNodeStateChangedEventArgs>(message =>
        {
            var list = new (string, object?)[2]
            {
                (JsonRpcStrings.RunId, message.RunId),
                (JsonRpcStrings.Changes, message.Changes),
            };

            return list;
        });

        _serializers[typeof(TestNode)] = new JsonObjectSerializer<TestNode>(message =>
        {
            List<(string Name, object? Value)> properties = new()
            {
                (JsonRpcStrings.Uid,  message.Uid.Value),
                (JsonRpcStrings.DisplayName, message.DisplayName),
            };

            TestMetadataProperty[] metadataProperties = message.Properties.OfType<TestMetadataProperty>();
            bool isNodeTypeAction = false;

            if (metadataProperties.Length > 0)
            {
#if NETCOREAPP
                properties.Add(("traits", metadataProperties.Select(x => new KeyValuePair<string, string>(x.Key, x.Value))));
#else
                var collection = new List<(string, object?)>(metadataProperties.Length);
                foreach (TestMetadataProperty metadata in metadataProperties)
                {
                    collection.Add((metadata.Key, metadata.Value));

                }

                properties.Add(("traits", collection));
#endif
            }

            foreach (IProperty property in message.Properties)
            {
                if (property is SerializableKeyValuePairStringProperty keyValuePairProperty)
                {
                    properties.Add((keyValuePairProperty.Key, keyValuePairProperty.Value));
                    continue;
                }

                if (property is SerializableNamedArrayStringProperty namedArrayStringProperty)
                {
                    properties.Add((namedArrayStringProperty.Name, namedArrayStringProperty.Values));
                    continue;
                }

                if (property is SerializableNamedKeyValuePairsStringProperty namedKvpStringProperty)
                {
#if NETCOREAPP
                    properties.Add((namedKvpStringProperty.Name, namedKvpStringProperty.Pairs));
#else
                    var collection = new List<(string, object?)>(namedKvpStringProperty.Pairs.Length);
                    foreach (KeyValuePair<string, string> item in namedKvpStringProperty.Pairs)
                    {
                        collection.Add((item.Key, item.Value));
                    }

                    properties.Add((namedKvpStringProperty.Name, collection));
#endif
                    continue;
                }

                if (property is TestFileLocationProperty fileLocationProperty)
                {
                    properties.Add(("location.file", fileLocationProperty.FilePath));
                    properties.Add(("location.line-start", fileLocationProperty.LineSpan.Start.Line));
                    properties.Add(("location.line-end", fileLocationProperty.LineSpan.End.Line));
                    continue;
                }

                if (property is TestMethodIdentifierProperty testMethodIdentifierProperty)
                {
                    properties.Add(("location.namespace", testMethodIdentifierProperty.Namespace));
                    properties.Add(("location.type", testMethodIdentifierProperty.TypeName));
                    properties.Add(("location.method", testMethodIdentifierProperty.ParameterTypeFullNames.Length > 0
                        ? $"{testMethodIdentifierProperty.MethodName}({string.Join(",", testMethodIdentifierProperty.ParameterTypeFullNames)})"
                        : testMethodIdentifierProperty.MethodName));
                    continue;
                }

                if (property is TestNodeStateProperty testNodeStateProperty)
                {
                    properties.Add(("node-type", "action"));
                    isNodeTypeAction = true;
                    switch (property)
                    {
                        case DiscoveredTestNodeStateProperty _:
                            {
                                properties.Add(("execution-state", "discovered"));
                                break;
                            }

                        case InProgressTestNodeStateProperty _:
                            {
                                properties.Add(("execution-state", "in-progress"));
                                break;
                            }

                        case PassedTestNodeStateProperty _:
                            {
                                properties.Add(("execution-state", "passed"));
                                break;
                            }

                        case SkippedTestNodeStateProperty _:
                            {
                                properties.Add(("execution-state", "skipped"));
                                break;
                            }

                        case FailedTestNodeStateProperty failedTestNodeStateProperty:
                            {
                                properties.Add(("execution-state", "failed"));
                                properties.Add(("error.message", failedTestNodeStateProperty?.Explanation ?? failedTestNodeStateProperty?.Exception?.Message));
                                if (failedTestNodeStateProperty?.Exception != null)
                                {
                                    Exception exception = failedTestNodeStateProperty.Exception;
                                    properties.Add(("error.stacktrace", exception.StackTrace ?? string.Empty));
                                    properties.Add(("assert.actual", exception.Data["assert.actual"] ?? string.Empty));
                                    properties.Add(("assert.expected", exception.Data["assert.expected"] ?? string.Empty));
                                }

                                break;
                            }

                        case TimeoutTestNodeStateProperty timeoutTestNodeStateProperty:
                            {
                                properties.Add(("execution-state", "timed-out"));
                                properties.Add(("error.message", timeoutTestNodeStateProperty?.Explanation ?? timeoutTestNodeStateProperty?.Exception?.Message));
                                if (timeoutTestNodeStateProperty?.Exception != null)
                                {
                                    properties.Add(("error.stacktrace", timeoutTestNodeStateProperty?.Exception?.StackTrace ?? string.Empty));
                                }

                                break;
                            }

                        case ErrorTestNodeStateProperty errorTestNodeStateProperty:
                            {
                                properties.Add(("execution-state", "error"));
                                properties.Add(("error.message", errorTestNodeStateProperty?.Explanation ?? errorTestNodeStateProperty?.Exception?.Message));
                                if (errorTestNodeStateProperty?.Exception != null)
                                {
                                    properties.Add(("error.stacktrace", errorTestNodeStateProperty?.Exception?.StackTrace ?? string.Empty));
                                }

                                break;
                            }

                        case CancelledTestNodeStateProperty cancelledTestNodeStateProperty:
                            {
                                properties.Add(("execution-state", "cancelled"));
                                properties.Add(("error.message", cancelledTestNodeStateProperty?.Explanation ?? cancelledTestNodeStateProperty?.Exception?.Message));
                                if (cancelledTestNodeStateProperty?.Exception != null)
                                {
                                    properties.Add(("error.stacktrace", cancelledTestNodeStateProperty?.Exception?.StackTrace ?? string.Empty));
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
                    properties.Add(("time.start-utc", timingProperty.GlobalTiming.StartTime));
                    properties.Add(("time.stop-utc", timingProperty.GlobalTiming.EndTime));
                    properties.Add(("time.duration-ms", timingProperty.GlobalTiming.Duration.TotalMilliseconds));
                    continue;
                }
            }

            if (!isNodeTypeAction)
            {
                properties.Add(("node-type", "group"));
            }

            return properties.ToArray();
        });

        _serializers[typeof(LogEventArgs)] = new JsonObjectSerializer<LogEventArgs>(message =>
        {
            var list = new (string, object?)[2]
            {
                (JsonRpcStrings.Level, message.LogMessage.Level.ToString()),
                (JsonRpcStrings.Message, message.LogMessage.Message),
            };

            return list;
        });

        _serializers[typeof(CancelRequestArgs)] = new JsonObjectSerializer<CancelRequestArgs>(request =>
        {
            var list = new (string, object?)[1]
            {
                (JsonRpcStrings.Id, request.CancelRequestId),
            };

            return list;
        });

        _serializers[typeof(TelemetryEventArgs)] = new JsonObjectSerializer<TelemetryEventArgs>(ev =>
        {
            var list = new (string, object?)[2]
            {
                (JsonRpcStrings.EventName, ev.EventName),
                (JsonRpcStrings.Metrics, ev.Metrics),
            };

            return list;
        });

        _serializers[typeof(ProcessInfoArgs)] = new JsonObjectSerializer<ProcessInfoArgs>(info =>
        {
            var list = new (string, object?)[4]
            {
                (JsonRpcStrings.Program, info.Program),
                (JsonRpcStrings.Args, info.Args),
                (JsonRpcStrings.WorkingDirectory, info.WorkingDirectory),
                (JsonRpcStrings.EnvironmentVariables, info.EnvironmentVariables),
            };

            return list;
        });

        _serializers[typeof(AttachDebuggerInfoArgs)] = new JsonObjectSerializer<AttachDebuggerInfoArgs>(info =>
        {
            var list = new (string, object?)[1]
            {
                (JsonRpcStrings.ProcessId, info.ProcessId),
            };

            return list;
        });

        // Serializers
        _serializers[typeof(string)] = new JsonValueSerializer<string>((w, v) => w.WriteStringValue(v));
        _serializers[typeof(bool)] = new JsonValueSerializer<bool>((w, v) => w.WriteBooleanValue(v));
        _serializers[typeof(char)] = new JsonValueSerializer<char>((w, v) => w.WriteStringValue($"{v}"));
        _serializers[typeof(int)] = new JsonValueSerializer<int>((w, v) => w.WriteNumberValue(v));
        _serializers[typeof(long)] = new JsonValueSerializer<long>((w, v) => w.WriteNumberValue(v));
        _serializers[typeof(float)] = new JsonValueSerializer<float>((w, v) => w.WriteNumberValue(v));
        _serializers[typeof(double)] = new JsonValueSerializer<double>((w, v) => w.WriteNumberValue(v));
        _serializers[typeof(decimal)] = new JsonValueSerializer<decimal>((w, v) => w.WriteNumberValue(v));
        _serializers[typeof(Guid)] = new JsonValueSerializer<Guid>((w, v) => w.WriteStringValue(v));
        _serializers[typeof(DateTime)] = new JsonValueSerializer<DateTime>((w, v) => w.WriteRawValue($"\"{v:o}\"", skipInputValidation: true));
        _serializers[typeof(DateTimeOffset)] = new JsonValueSerializer<DateTimeOffset>((w, v) => w.WriteRawValue($"\"{v:o}\"", skipInputValidation: true));

        // _serializers[typeof(TimeSpan)] = new JsonValueSerializer<TimeSpan>((w, v) => w.WriteStringValue(v.ToString())); // Remove for now
        _serializers[typeof((string, object?)[])] = new JsonObjectSerializer<(string, object?)[]>(n => n);
        _serializers[typeof(Dictionary<string, object>)] = new JsonObjectSerializer<Dictionary<string, object>>(d => d.Select(kvp => (kvp.Key, (object?)kvp.Value)).ToArray());

        // Deserializers
        _deserializers[typeof(string)] = new JsonElementDeserializer<string>((json, jsonDocument) => jsonDocument.GetString()!);
        _deserializers[typeof(bool)] = new JsonElementDeserializer<bool>((json, jsonDocument) => jsonDocument.GetBoolean());
        _deserializers[typeof(int)] = new JsonElementDeserializer<int>((json, jsonDocument) => jsonDocument.GetInt32());
        _deserializers[typeof(decimal)] = new JsonElementDeserializer<decimal>((json, jsonDocument) => jsonDocument.GetDecimal());
        _deserializers[typeof(DateTime)] = new JsonElementDeserializer<DateTime>((json, jsonDocument) => jsonDocument.GetDateTime());

        _deserializers[typeof(IDictionary<string, object?>)] = new JsonElementDeserializer<IDictionary<string, object?>>((json, jsonDocument) =>
        {
            var items = new Dictionary<string, object?>();
            foreach (var kvp in jsonDocument.EnumerateObject())
            {
                switch (kvp.Value.ValueKind)
                {
                    case JsonValueKind.String:
                        items.Add(kvp.Name, kvp.Value.GetString());
                        break;
                    case JsonValueKind.Number:
                        items.Add(kvp.Name, kvp.Value.GetInt32());
                        break;
                    case JsonValueKind.True:
                        items.Add(kvp.Name, true);
                        break;
                    case JsonValueKind.False:
                        items.Add(kvp.Name, false);
                        break;
                    case JsonValueKind.Object:
                        items.Add(kvp.Name, json.Bind<IDictionary<string, object?>>(kvp.Value));
                        break;
                    case JsonValueKind.Array:
                        items.Add(kvp.Name, json.Bind<object[]>(kvp.Value));
                        break;
                    case JsonValueKind.Null:
                        items.Add(kvp.Name, null);
                        break;
                    default:
                        throw new InvalidOperationException($"key: {kvp.Name}, value: {kvp.Value}, type: {kvp.Value.ValueKind}");
                }
            }

            return items;
        });

        _deserializers[typeof(ErrorMessage)] = new JsonElementDeserializer<ErrorMessage>(
          (json, jsonElement) =>
          {
              int id = json.Bind<int>(jsonElement, JsonRpcStrings.Id);
              IDictionary<string, object?> errorObj = json.Bind<IDictionary<string, object?>>(jsonElement, JsonRpcStrings.Error);

#if !NETCOREAPP
              if (errorObj.TryGetValue(JsonRpcStrings.Data, out object? errorData))
              {
                  if (errorData is Dictionary<string, object?> error && error.Count == 0)
                  {
                      errorObj[JsonRpcStrings.Data] = null!;
                  }
              }
#endif

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
                  Message: errorMessage ?? string.Empty,
                  Data: null);
          });

        _deserializers[typeof(InitializeResponseArgs)] = new JsonElementDeserializer<InitializeResponseArgs>(
          (json, jsonElement) =>
          {
              return new InitializeResponseArgs(
                  ServerInfo: json.Bind<ServerInfo>(jsonElement, JsonRpcStrings.ServerInfo),
                  Capabilities: json.Bind<ServerCapabilities>(jsonElement, JsonRpcStrings.Capabilities));
          });

        _deserializers[typeof(ServerInfo)] = new JsonElementDeserializer<ServerInfo>(
          (json, jsonElement) =>
          {
              return new ServerInfo(
                  Name: json.Bind<string>(jsonElement, JsonRpcStrings.Name),
                  Version: json.Bind<string>(jsonElement, JsonRpcStrings.Version));
          });

        _deserializers[typeof(ServerCapabilities)] = new JsonElementDeserializer<ServerCapabilities>(
          (json, jsonElement) =>
          {
              return new ServerCapabilities(
                  TestingCapabilities: json.Bind<ServerTestingCapabilities>(jsonElement, JsonRpcStrings.Testing));
          });

        _deserializers[typeof(ServerTestingCapabilities)] = new JsonElementDeserializer<ServerTestingCapabilities>(
          (json, jsonElement) =>
          {
              return new ServerTestingCapabilities(
                        SupportsDiscovery: json.Bind<bool>(jsonElement, JsonRpcStrings.SupportsDiscovery),
                        MultiRequestSupport: json.Bind<bool>(jsonElement, JsonRpcStrings.MultiRequestSupport),
                        VSTestProviderSupport: json.Bind<bool>(jsonElement, JsonRpcStrings.VSTestProviderSupport));
          });

        _deserializers[typeof(TestNode)] = new JsonElementDeserializer<TestNode>(
            (json, properties) =>
            {
                var propertyBag = new PropertyBag();
                string uid = json.Bind<string>(properties, JsonRpcStrings.Uid) ?? string.Empty;
                string displayName = json.Bind<string>(properties, JsonRpcStrings.DisplayName);

                var locationFile = json.Bind<string>(properties, "location.file");
                if (locationFile is not null)
                {
                    ApplicationStateGuard.Ensure(locationFile is not null);
                    var locationLineStart = json.Bind<int>(properties, "location.line-start");
                    var locationLineEnd = json.Bind<int>(properties, "location.line-end");

                    var testFileLocationProperty = new TestFileLocationProperty(
                        locationFile,
                        new LinePositionSpan(new LinePosition(locationLineStart, 0), new LinePosition(locationLineEnd, 0)));
                    propertyBag.Add(testFileLocationProperty);
                }

                return new TestNode
                {
                    Uid = new TestNodeUid(uid),
                    DisplayName = displayName,
                    Properties = propertyBag,
                };
            });

        _deserializers[typeof(CancelRequestArgs)] = new JsonElementDeserializer<CancelRequestArgs>(
          (json, jsonElement) =>
          {
              int id = json.Bind<int>(jsonElement, JsonRpcStrings.Id);
              return new CancelRequestArgs(id);
          });

        // Try to add serializers passed from outside
        if (serializers != null)
        {
            foreach (KeyValuePair<Type, JsonSerializer> serializer in serializers)
            {
                // If we have already the serializer registered we use the one that is already registered.
                // It's expected to be superior in performance
                if (_serializers.ContainsKey(serializer.Key))
                {
                    continue;
                }

                _serializers[serializer.Key] = serializer.Value;
            }
        }

        if (deserializers != null)
        {
            foreach (KeyValuePair<Type, JsonDeserializer> deserializer in deserializers)
            {
                // If we have already the deserializer registered we use the one that is already registered.
                // It's expected to be superior in performance
                if (_deserializers.ContainsKey(deserializer.Key))
                {
                    continue;
                }

                _deserializers[deserializer.Key] = deserializer.Value;
            }
        }
    }

    public async Task<string> SerializeAsync(object obj)
    {
        MemoryStream stream = _memoryStreamPool.Allocate();
        try
        {
            stream.Position = 0;
            await using var writer = new Utf8JsonWriter(stream);
            await SerializeAsync(obj, writer);
            await writer.FlushAsync();
#if NETCOREAPP
            return Encoding.UTF8.GetString(stream.GetBuffer().AsMemory().Span[..(int)stream.Position]);
#else
            return Encoding.UTF8.GetString(stream.ToArray());
#endif
        }
        finally
        {
            _memoryStreamPool.Free(stream, CancellationToken.None);
        }
    }

    public T Deserialize<T>(ReadOnlyMemory<char> utf8Json)
    {
        using var document = JsonDocument.Parse(utf8Json);
        return Bind<T>(document.RootElement, null);
    }

    internal T Bind<T>(IEnumerable<JsonProperty> properties)
        => !_deserializers.TryGetValue(typeof(T), out JsonDeserializer? deserializer)
            ? throw new InvalidOperationException($"Cannot find deserializer for {typeof(T)}.")
            : deserializer is not JsonPropertyCollectionDeserializer<T> propertyBagDeserializer
            ? throw new InvalidOperationException("we need property bag deserializer")
            : propertyBagDeserializer.CreateObject(this, properties);

    internal T Bind<T>(JsonElement element, string? property = null)
    {
        if (property != null)
        {
            try
            {
                element = element.GetProperty(property);
            }
            catch (KeyNotFoundException ex)
            {
                throw new KeyNotFoundException($"Key '{property}' was not found in the dictionary.", ex);
            }
        }

        bool deserializerFound = _deserializers.TryGetValue(typeof(T), out JsonDeserializer? deserializer);

        if (deserializerFound && deserializer is JsonElementDeserializer<T> objectDeserializer)
        {
            T obj = objectDeserializer.CreateObject(this, element);
            return obj;
        }

        if (deserializerFound && deserializer is JsonCollectionDeserializer<T> collectionDeserializer)
        {
            T obj = collectionDeserializer.CreateObject(this, element);
            return obj;
        }

        if (deserializerFound && deserializer is JsonElementDeserializer<object> baseObjectDeserializer)
        {
            var obj = (T)baseObjectDeserializer.CreateObject(this, element);
            return obj;
        }

        if (deserializerFound && deserializer is JsonCollectionDeserializer<object> baseCollectionDeserializer)
        {
            var obj = (T)baseCollectionDeserializer.CreateObject(this, element);
            return obj;
        }

        throw new InvalidOperationException($"Cannot find deserializer for {typeof(T)}.");
    }

    private async Task SerializeAsync(object? obj, Utf8JsonWriter writer)
    {
        // Serialize null
        if (obj == null)
        {
            writer.WriteNullValue();
            return;
        }

        // Try grabbing an explicit converter
        if (_serializers.TryGetValue(obj.GetType(), out JsonSerializer? converter))
        {
            if (converter is JsonObjectSerializer objectConverter)
            {
                writer.WriteStartObject();
                (string Key, object? Value)[]? properties = objectConverter.Properties(obj);
                if (properties != null)
                {
                    int count = 1;
                    foreach ((string property, object? value) in properties)
                    {
                        writer.WritePropertyName(property);
                        await SerializeAsync(value, writer);
                        count++;
                    }
                }

                writer.WriteEndObject();
            }
            else
            {
                ((JsonValueSerializer)converter).Serialize(writer, obj);
            }

            // If we found one, then return.
            return;
        }

        // If we did not find a converter try checking if this is a collection.
        if (obj is IEnumerable e)
        {
            writer.WriteStartArray();
            foreach (object? o in e)
            {
                if (o == null)
                {
                    writer.WriteNullValue();
                }
                else
                {
                    await SerializeAsync(o, writer);
                }
            }

            writer.WriteEndArray();

            // If it was a collection return.
            return;
        }

        // We could not find a converter so throw.
        if (converter == null)
        {
            throw new InvalidOperationException($"Flattener missing {obj.GetType()}.");
        }
    }
}
