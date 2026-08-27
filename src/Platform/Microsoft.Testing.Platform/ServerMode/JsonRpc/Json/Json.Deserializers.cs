// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Text.Json;

using Microsoft.Testing.Platform.Extensions.Messages;

namespace Microsoft.Testing.Platform.ServerMode.Json;

internal sealed partial class Json
{
    private static void RegisterDefaultDeserializers(Dictionary<Type, JsonDeserializer> deserializers)
    {
        // Deserializers
        deserializers[typeof(string)] = new JsonElementDeserializer<string>((json, jsonDocument) => jsonDocument.GetString()!);
        deserializers[typeof(bool)] = new JsonElementDeserializer<bool>((json, jsonDocument) => jsonDocument.GetBoolean());
        deserializers[typeof(int)] = new JsonElementDeserializer<int>((json, jsonDocument) => jsonDocument.GetInt32());
        deserializers[typeof(decimal)] = new JsonElementDeserializer<decimal>((json, jsonDocument) => jsonDocument.GetDecimal());
        deserializers[typeof(DateTime)] = new JsonElementDeserializer<DateTime>((json, jsonDocument) => jsonDocument.GetDateTime());

        deserializers[typeof(IDictionary<string, object?>)] = new JsonElementDeserializer<IDictionary<string, object?>>((json, jsonDocument) =>
        {
            Dictionary<string, object?> items = [];
            foreach (JsonProperty kvp in jsonDocument.EnumerateObject())
            {
                // Indexer assignment (last value wins) rather than Add, so a duplicate JSON key in
                // untrusted input does not throw and crash the read loop. This mirrors how Jsonite and
                // System.Text.Json tolerate duplicate keys.
                items[kvp.Name] = kvp.Value.ValueKind switch
                {
                    JsonValueKind.String => kvp.Value.GetString(),
                    JsonValueKind.Number => ReadNumber(kvp.Value),
                    JsonValueKind.True => true,
                    JsonValueKind.False => false,
                    JsonValueKind.Object => json.Bind<IDictionary<string, object?>>(kvp.Value),
                    JsonValueKind.Array => json.Bind<object[]>(kvp.Value),
                    JsonValueKind.Null => null,
                    _ => throw new InvalidOperationException($"key: {kvp.Name}, value: {kvp.Value}, type: {kvp.Value.ValueKind}"),
                };
            }

            return items;
        });

        // A generic JSON array becomes an object?[] whose elements are decoded with the same rules as the
        // IDictionary deserializer above. The server's own IDictionary deserializer already depends on this
        // (see the JsonValueKind.Array branch) but never exercised it, because the server only ever
        // deserializes client-to-server REQUESTS whose params are strongly typed. A client reusing this engine
        // to read server-to-client responses/notifications (which DO carry arrays, e.g. run attachments or
        // test-node changes) needs it, so register it here.
        deserializers[typeof(object[])] = new JsonElementDeserializer<object[]>((json, jsonDocument) =>
        {
            var items = new List<object?>();
            foreach (JsonElement element in jsonDocument.EnumerateArray())
            {
                switch (element.ValueKind)
                {
                    case JsonValueKind.String:
                        items.Add(element.GetString());
                        break;
                    case JsonValueKind.Number:
                        items.Add(ReadNumber(element));
                        break;
                    case JsonValueKind.True:
                        items.Add(true);
                        break;
                    case JsonValueKind.False:
                        items.Add(false);
                        break;
                    case JsonValueKind.Object:
                        items.Add(json.Bind<IDictionary<string, object?>>(element));
                        break;
                    case JsonValueKind.Array:
                        items.Add(json.Bind<object[]>(element));
                        break;
                    case JsonValueKind.Null:
                        items.Add(null);
                        break;
                    default:
                        throw new InvalidOperationException($"value: {element}, type: {element.ValueKind}");
                }
            }

            return items.ToArray()!;
        });

        deserializers[typeof(RpcMessage)] = new JsonElementDeserializer<RpcMessage>((json, jsonElement) =>
        {
            ValidateJsonRpcHeader(json, jsonElement);

            if (json.TryBind(jsonElement, out string? method, JsonRpcStrings.Method))
            {
                bool hasId = TryGetRpcId(jsonElement, out int id, out string? stringId);

                object? @params = null;
                if (jsonElement.TryGetProperty(JsonRpcStrings.Params, out JsonElement value))
                {
                    try
                    {
                        // Parse the specific methods
                        @params = method switch
                        {
                            JsonRpcMethods.Initialize => json.Bind<InitializeRequestArgs>(value),
                            JsonRpcMethods.TestingDiscoverTests => json.Bind<DiscoverRequestArgs>(value),
                            JsonRpcMethods.TestingRunTests => json.Bind<RunRequestArgs>(value),
                            JsonRpcMethods.CancelRequest => json.Bind<CancelRequestArgs>(value),
                            JsonRpcMethods.Exit => json.Bind<ExitRequestArgs>(value),

                            // Note: the server only strongly-types the request methods above. Any other
                            // method reaching this decoder is a server-to-client notification (for example
                            // testing/testUpdates/tests, client/log, telemetry/update,
                            // testing/testUpdates/attachments) being read by a CLIENT reusing this engine.
                            // Keep its params as a raw property bag so the client can decode them itself,
                            // instead of dropping them. For the server this only affects unknown methods,
                            // which it ignores anyway.
                            _ => value.ValueKind == JsonValueKind.Object
                                ? json.Bind<IDictionary<string, object?>>(value)
                                : null,
                        };
                    }
                    catch (Exception ex) when (ex is MessageFormatException or InvalidOperationException or JsonException or ArgumentException)
                    {
                        // If params can't be deserialized for a request, capture the failure so
                        // we can later send back a properly coded JSON-RPC error using the request id.
                        // For notifications there's no one to respond to, but we still avoid
                        // crashing the message-handling loop by swallowing into the sentinel.
                        // We catch the broader set of deserialization-related exceptions because the
                        // request payload is untrusted client input and the lower-level helpers
                        // (e.g. JsonElement.GetString() on a non-string element, or an ArgumentException
                        // from a nested typed binder) can throw types other than MessageFormatException.
                        @params = new InvalidRequestParamsArgs(ErrorCodes.InvalidParams, ex.Message);
                    }
                }
                else if (method is JsonRpcMethods.Initialize
                    or JsonRpcMethods.TestingDiscoverTests
                    or JsonRpcMethods.TestingRunTests
                    or JsonRpcMethods.CancelRequest)
                {
                    @params = new InvalidRequestParamsArgs(ErrorCodes.InvalidParams, "'params' field is missing");
                }

                return hasId
                    ? new RequestMessage(id, method!, @params) { StringId = stringId }
                    : new NotificationMessage(method!, @params);
            }

            if (jsonElement.TryGetProperty(JsonRpcStrings.Result, out JsonElement element))
            {
                // Note: Because the result message does not contain the original method name,
                //       it's not possible for us to do a typed deserialization.
                //       The best option we've got is to return a generic property bag.
                int id = BindRpcId(jsonElement, out string? stringId);

                IDictionary<string, object?>? result = element.ValueKind == JsonValueKind.Null ? null :
                    json.Bind<IDictionary<string, object?>>(jsonElement, JsonRpcStrings.Result);

                return new ResponseMessage(id, result) { StringId = stringId };
            }

            return json.TryBind(jsonElement, out ErrorMessage? errorMessage) ? errorMessage! : throw new MessageFormatException();
        });

        deserializers[typeof(InitializeRequestArgs)] = new JsonElementDeserializer<InitializeRequestArgs>((json, jsonElement) =>
        {
            json.TryArrayBind(jsonElement, out string[]? protocolVersions, JsonRpcStrings.ProtocolVersions);
            if (protocolVersions is not null)
            {
                for (int i = 0; i < protocolVersions.Length; i++)
                {
                    protocolVersions[i] = protocolVersions[i]
                        ?? throw new MessageFormatException($"'{JsonRpcStrings.ProtocolVersions}' entries must be strings");
                }
            }

            return new InitializeRequestArgs(
                ProcessId: json.Bind<int>(jsonElement, JsonRpcStrings.ProcessId),
                ClientInfo: json.Bind<ClientInfo>(jsonElement, JsonRpcStrings.ClientInfo),
                Capabilities: json.Bind<ClientCapabilities>(jsonElement, JsonRpcStrings.Capabilities))
            {
                ProtocolVersions = protocolVersions,
            };
        });

        deserializers[typeof(ClientInfo)] = new JsonElementDeserializer<ClientInfo>((json, jsonElement) => new ClientInfo(
                Name: json.Bind<string>(jsonElement, JsonRpcStrings.Name),
                Version: json.Bind<string>(jsonElement, JsonRpcStrings.Version)));

        deserializers[typeof(ClientCapabilities)] = new JsonElementDeserializer<ClientCapabilities>((json, jsonElement) =>
        {
            jsonElement.TryGetProperty(JsonRpcStrings.Testing, out JsonElement testing);

            bool isStateful = testing.ValueKind == JsonValueKind.Object
                && testing.TryGetProperty(JsonRpcStrings.IsStateful, out JsonElement statefulElement)
                && statefulElement.ValueKind == JsonValueKind.True;

            return new ClientCapabilities(
                    DebuggerProvider: json.Bind<bool>(testing, JsonRpcStrings.DebuggerProvider),
                    IsStateful: isStateful);
        });

        deserializers[typeof(InitializeResponseArgs)] = new JsonElementDeserializer<InitializeResponseArgs>(
          (json, jsonElement) =>
          {
              string? protocolVersion = null;
              if (jsonElement.TryGetProperty(JsonRpcStrings.ProtocolVersion, out JsonElement protocolVersionElement)
                  && protocolVersionElement.ValueKind != JsonValueKind.Null)
              {
                  protocolVersion = protocolVersionElement.ValueKind == JsonValueKind.String
                      ? protocolVersionElement.GetString()
                      : throw new MessageFormatException(
                          $"'{JsonRpcStrings.ProtocolVersion}' field has wrong type (expected String)");
              }

              return new InitializeResponseArgs(
                  ProcessId: json.Bind<int>(jsonElement, JsonRpcStrings.ProcessId),
                  ServerInfo: json.Bind<ServerInfo>(jsonElement, JsonRpcStrings.ServerInfo),
                  Capabilities: json.Bind<ServerCapabilities>(jsonElement, JsonRpcStrings.Capabilities))
              {
                  ProtocolVersion = protocolVersion,
              };
          });

        deserializers[typeof(ServerInfo)] = new JsonElementDeserializer<ServerInfo>(
          (json, jsonElement) => new ServerInfo(
                  Name: json.Bind<string>(jsonElement, JsonRpcStrings.Name),
                  Version: json.Bind<string>(jsonElement, JsonRpcStrings.Version)));

        deserializers[typeof(ServerCapabilities)] = new JsonElementDeserializer<ServerCapabilities>(
          (json, jsonElement) => new ServerCapabilities(
                  TestingCapabilities: json.Bind<ServerTestingCapabilities>(jsonElement, JsonRpcStrings.Testing)));

        deserializers[typeof(ServerTestingCapabilities)] = new JsonElementDeserializer<ServerTestingCapabilities>(
          (json, jsonElement) => new ServerTestingCapabilities(
              SupportsDiscovery: json.Bind<bool>(jsonElement, JsonRpcStrings.SupportsDiscovery),
              MultiRequestSupport: json.Bind<bool>(jsonElement, JsonRpcStrings.MultiRequestSupport),
              VSTestProviderSupport: json.Bind<bool>(jsonElement, JsonRpcStrings.VSTestProviderSupport),
              SupportsAttachments: json.Bind<bool>(jsonElement, JsonRpcStrings.AttachmentsSupport),
              MultiConnectionProvider: json.Bind<bool>(jsonElement, JsonRpcStrings.MultiConnectionProvider)));

        deserializers[typeof(DiscoverRequestArgs)] = new JsonElementDeserializer<DiscoverRequestArgs>((json, jsonElement) =>
        {
            string runId = json.Bind<string>(jsonElement, JsonRpcStrings.RunId);
            if (!Guid.TryParse(runId, out Guid result))
            {
                throw new MessageFormatException(JsonRpcStrings.InvalidRunIdErrorMessage);
            }

            json.TryArrayBind(jsonElement, out TestNode[]? testNodes, JsonRpcStrings.Tests);
            json.TryBind(jsonElement, out string? graphFilter, JsonRpcStrings.Filter);

            return new DiscoverRequestArgs(
                RunId: result,
                TestNodes: testNodes,
                GraphFilter: graphFilter);
        });

        deserializers[typeof(RunRequestArgs)] = new JsonElementDeserializer<RunRequestArgs>((json, jsonElement) =>
        {
            string runId = json.Bind<string>(jsonElement, JsonRpcStrings.RunId);
            if (!Guid.TryParse(runId, out Guid result))
            {
                throw new MessageFormatException(JsonRpcStrings.InvalidRunIdErrorMessage);
            }

            json.TryArrayBind(jsonElement, out TestNode[]? testNodes, JsonRpcStrings.Tests);
            json.TryBind(jsonElement, out string? graphFilter, JsonRpcStrings.Filter);

            return new RunRequestArgs(
                RunId: result,
                TestNodes: testNodes,
                GraphFilter: graphFilter);
        });

        deserializers[typeof(TestNode)] = new JsonElementDeserializer<TestNode>(
            (json, properties) =>
            {
                PropertyBag propertyBag = new();
                string uid = json.Bind<string>(properties, JsonRpcStrings.Uid)
                    ?? throw new MessageFormatException($"'{JsonRpcStrings.Uid}' field cannot be null");
                string displayName = json.Bind<string>(properties, JsonRpcStrings.DisplayName)
                    ?? throw new MessageFormatException($"'{JsonRpcStrings.DisplayName}' field cannot be null");
                if (RoslynString.IsNullOrWhiteSpace(uid))
                {
                    throw new MessageFormatException($"'{JsonRpcStrings.Uid}' field cannot be empty or whitespace");
                }

                if (json.TryBind(properties, out string? locationFile, "location.file"))
                {
                    if (locationFile is null)
                    {
                        throw new MessageFormatException("'location.file' field cannot be null");
                    }

                    bool hasLineStart = json.TryBind(properties, out int locationLineStart, "location.line-start");
                    bool hasLineEnd = json.TryBind(properties, out int locationLineEnd, "location.line-end");
                    if (!hasLineStart || !hasLineEnd)
                    {
                        throw new MessageFormatException(
                            "'location.file', 'location.line-start', and 'location.line-end' fields must be specified together");
                    }

                    TestFileLocationProperty testFileLocationProperty = new(
                        locationFile,
                        new LinePositionSpan(new LinePosition(locationLineStart, 0), new LinePosition(locationLineEnd, 0)));
                    propertyBag.Add(testFileLocationProperty);
                }
                else if (properties.TryGetProperty("location.line-start", out _)
                    || properties.TryGetProperty("location.line-end", out _))
                {
                    throw new MessageFormatException(
                        "'location.file', 'location.line-start', and 'location.line-end' fields must be specified together");
                }

                return new TestNode
                {
                    Uid = new TestNodeUid(uid),
                    DisplayName = displayName,
                    Properties = propertyBag,
                };
            });

        deserializers[typeof(CancelRequestArgs)] = new JsonElementDeserializer<CancelRequestArgs>(
          (json, jsonElement) => TryGetRpcId(jsonElement, out int id, out string? stringId)
              ? new CancelRequestArgs(id) { StringId = stringId }
              : throw new MessageFormatException("id field is missing"));

        deserializers[typeof(ExitRequestArgs)] = new JsonElementDeserializer<ExitRequestArgs>(
          (json, jsonElement) => new ExitRequestArgs());

        deserializers[typeof(ErrorMessage)] = new JsonElementDeserializer<ErrorMessage>(
          (json, jsonElement) =>
          {
              ValidateJsonRpcHeader(json, jsonElement);

              int id = BindRpcId(jsonElement, out string? stringId);
              JsonElement error = jsonElement.GetProperty(JsonRpcStrings.Error);

              int code = json.Bind<int>(error, JsonRpcStrings.Code);
              string message = json.Bind<string>(error, JsonRpcStrings.Message);

              object? data = error.TryGetProperty(JsonRpcStrings.Data, out JsonElement dataElement)
                  ? ReadUntypedValue(json, dataElement)
                  : null;
              if (data is IDictionary<string, object?> { Count: 0 })
              {
                  data = null;
              }

              return new ErrorMessage(
                  Id: id,
                  ErrorCode: code,
                  Message: message ?? string.Empty,
                  Data: data)
              {
                  StringId = stringId,
              };
          });
    }

    /// <summary>
    /// Decodes a JSON number that lands in an untyped <see cref="object"/> slot (a property-bag value or a
    /// generic array element) into the same .NET numeric type the Jsonite reader (the net462 / netstandard2.0
    /// path) produces, so both formatter paths hand identical boxed types to consumers.
    /// </summary>
    /// <remarks>
    /// A plain <c>GetInt32()</c> throws <see cref="System.FormatException"/> on the non-Int32 numbers real MTP
    /// notifications carry (durations as doubles, timestamps / counts as longs). We therefore widen exactly the
    /// way Jsonite does: an integer becomes <see cref="int"/>, then <see cref="long"/>, <see cref="ulong"/>, or
    /// <see cref="decimal"/>; a value with a fractional part or exponent becomes <see cref="double"/>. This is a
    /// pure superset of the old behavior — every value that used to decode as <see cref="int"/> still does, only
    /// the ones that used to throw now widen.
    /// </remarks>
    // IDE0046 (prefer conditional expression) is suppressed on purpose: collapsing these guarded returns into
    // a single ?: chain that ends in double gives the whole expression the static type double, so every integer
    // branch implicitly widens and boxes as double — silently defeating the type preservation this method exists
    // for (and which the JsonTests regression tests assert). Keep the explicit returns.
#pragma warning disable IDE0046 // Convert to conditional expression
    private static object ReadNumber(JsonElement element)
    {
        if (element.TryGetInt32(out int intValue))
        {
            return intValue;
        }

        if (element.TryGetInt64(out long longValue))
        {
            return longValue;
        }

        if (element.TryGetUInt64(out ulong ulongValue))
        {
            return ulongValue;
        }

        string rawValue = element.GetRawText();
        if (rawValue.IndexOf('.') is -1
            && rawValue.IndexOf('e') is -1
            && rawValue.IndexOf('E') is -1
            && element.TryGetDecimal(out decimal decimalValue))
        {
            return decimalValue;
        }

        return element.GetDouble();
    }
#pragma warning restore IDE0046 // Convert to conditional expression

    private static object? ReadUntypedValue(Json json, JsonElement element)
        => element.ValueKind switch
        {
            JsonValueKind.String => element.GetString(),
            JsonValueKind.Number => ReadNumber(element),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Object => json.Bind<IDictionary<string, object?>>(element),
            JsonValueKind.Array => json.Bind<object[]>(element),
            JsonValueKind.Null => null,
            _ => throw new MessageFormatException($"Unsupported JSON value kind '{element.ValueKind}'"),
        };

    private static bool TryGetRpcId(JsonElement jsonElement, out int id, out string? stringId)
    {
        if (!jsonElement.TryGetProperty(JsonRpcStrings.Id, out JsonElement idElement))
        {
            id = default;
            stringId = null;
            return false;
        }

        if (idElement.ValueKind == JsonValueKind.Null)
        {
            throw new MessageFormatException($"'{JsonRpcStrings.Id}' field cannot be null");
        }

        stringId = idElement.ValueKind == JsonValueKind.String ? idElement.GetString() : null;
        id = ReadRpcId(idElement);
        return true;
    }

    private static int BindRpcId(JsonElement jsonElement, out string? stringId)
        => jsonElement.TryGetProperty(JsonRpcStrings.Id, out JsonElement idElement)
            ? ReadRpcIdAndCaptureString(idElement, out stringId)
            : throw new MessageFormatException($"'{JsonRpcStrings.Id}' field is missing");

    private static int ReadRpcIdAndCaptureString(JsonElement idElement, out string? stringId)
    {
        stringId = idElement.ValueKind == JsonValueKind.String ? idElement.GetString() : null;
        return ReadRpcId(idElement);
    }

    private static int ReadRpcId(JsonElement idElement)
        => idElement.ValueKind switch
        {
            JsonValueKind.Number when RpcIdParser.TryParseNumericId(idElement.GetRawText(), out int numericId) => numericId,
            JsonValueKind.String when int.TryParse(
                idElement.GetString(),
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out int stringId)
                && idElement.GetString() == stringId.ToString(CultureInfo.InvariantCulture) => stringId,
            _ => throw new MessageFormatException($"'{JsonRpcStrings.Id}' field should be an int or a numeric string"),
        };
}
