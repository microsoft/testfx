// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

// Note: System.Text.Json is only available in .NET 6.0 and above.
//       As such, we have two separate implementations for the serialization code.
#if !NETCOREAPP
using Jsonite;
#endif
using Microsoft.Testing.Platform.Extensions.Messages;

namespace Microsoft.Testing.Platform.ServerMode;

internal static partial class SerializerUtilities
{
    private static void RegisterDeserializers()
    {
        // Deserialize a generic JSON-RPC message
        Deserializers[typeof(RpcMessage)] = new ObjectDeserializer<RpcMessage>(properties =>
        {
            ValidateJsonRpcHeader(properties);

            if (properties.TryGetValue(JsonRpcStrings.Method, out object? methodObj) && methodObj is not null)
            {
                string method = (string)methodObj;

                bool hasId = properties.TryGetValue(JsonRpcStrings.Id, out object? idObj);
                if (hasId && idObj is null)
                {
                    throw new MessageFormatException($"'{JsonRpcStrings.Id}' field cannot be null");
                }

                int? id = !hasId
                            ? null
                            : GetIdFromJson(idObj) ?? throw new MessageFormatException("id field should be a string or an int");
                string? stringId = idObj as string;

                object? @params;
                object? rawParams = GetOptionalPropertyFromJson(properties, JsonRpcStrings.Params);
                bool paramsRequired = method is JsonRpcMethods.Initialize
                    or JsonRpcMethods.TestingDiscoverTests
                    or JsonRpcMethods.TestingRunTests
                    or JsonRpcMethods.CancelRequest;
                if (paramsRequired && rawParams is not IDictionary<string, object?>)
                {
                    @params = new InvalidRequestParamsArgs(
                        ErrorCodes.InvalidParams,
                        rawParams is null ? "'params' field is missing" : "'params' field has wrong type (expected Object)");
                }
                else
                {
                    IDictionary<string, object?> paramsObj = rawParams as IDictionary<string, object?> ?? new Dictionary<string, object?>();
                    try
                    {
                        // Parse the specific methods
                        @params = method switch
                        {
                            JsonRpcMethods.Initialize => Deserialize<InitializeRequestArgs>(paramsObj),
                            JsonRpcMethods.TestingDiscoverTests => Deserialize<DiscoverRequestArgs>(paramsObj),
                            JsonRpcMethods.TestingRunTests => Deserialize<RunRequestArgs>(paramsObj),
                            JsonRpcMethods.CancelRequest => Deserialize<CancelRequestArgs>(paramsObj),
                            JsonRpcMethods.Exit => Deserialize<ExitRequestArgs>(paramsObj),

                            // Preserve server-to-client notification params when this formatter is used by a
                            // client or protocol test, matching the System.Text.Json path.
                            _ => rawParams is IDictionary<string, object?> ? paramsObj : null,
                        };
                    }
                    catch (Exception ex) when (ex is MessageFormatException or InvalidCastException)
                    {
                        // If params can't be deserialized for a request, capture the failure so
                        // we can later send back a properly coded JSON-RPC error using the request id.
                        // For notifications there's no one to respond to, but we still avoid
                        // crashing the message-handling loop by swallowing into the sentinel.
                        // We catch the broader set of deserialization-related exceptions because the
                        // request payload is untrusted client input and the lower-level helpers can
                        // throw types other than MessageFormatException.
                        @params = new InvalidRequestParamsArgs(ErrorCodes.InvalidParams, ex.Message);
                    }
                }

                return id.HasValue
                    ? new RequestMessage(id.Value, method, @params) { StringId = stringId }
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

                return new ResponseMessage(id, paramsObj) { StringId = idObj as string };
            }

            throw new MessageFormatException();
        });

        // Deserialize requests
        Deserializers[typeof(InitializeRequestArgs)] = new ObjectDeserializer<InitializeRequestArgs>(properties =>
        {
            int processId = GetRequiredPropertyFromJson<int>(properties, JsonRpcStrings.ProcessId);
            ClientInfo clientInfo = Deserialize<ClientInfo>(properties);
            ClientCapabilities capabilities = Deserialize<ClientCapabilities>(properties);
            object? protocolVersionsValue = GetOptionalPropertyFromJson(properties, JsonRpcStrings.ProtocolVersions);
            string[]? protocolVersions = null;
            if (protocolVersionsValue is not null)
            {
                if (protocolVersionsValue is not ICollection<object> protocolVersionsJson)
                {
                    throw new MessageFormatException($"'{JsonRpcStrings.ProtocolVersions}' field has wrong type (expected Array)");
                }

                protocolVersions = new string[protocolVersionsJson.Count];
                int index = 0;
                foreach (object? protocolVersion in protocolVersionsJson)
                {
                    protocolVersions[index++] = protocolVersion as string
                        ?? throw new MessageFormatException($"'{JsonRpcStrings.ProtocolVersions}' entries must be strings");
                }
            }

            return new InitializeRequestArgs(processId, clientInfo, capabilities)
            {
                ProtocolVersions = protocolVersions,
            };
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
            bool? isStateful = GetOptionalPropertyFromJson(testingCapabilities, JsonRpcStrings.IsStateful) switch
            {
                null => null,
                bool value => value,
                _ => throw new MessageFormatException($"'{JsonRpcStrings.IsStateful}' field has wrong type (expected {nameof(Boolean)})"),
            };

            return new ClientCapabilities(debuggerProvider, isStateful);
        });

        Deserializers[typeof(InitializeResponseArgs)] = new ObjectDeserializer<InitializeResponseArgs>(properties =>
        {
            int processId = GetRequiredPropertyFromJson<int>(properties, JsonRpcStrings.ProcessId);
            ServerInfo serverInfo = Deserialize<ServerInfo>(GetRequiredPropertyFromJson<IDictionary<string, object?>>(properties, JsonRpcStrings.ServerInfo));
            ServerCapabilities capabilities = Deserialize<ServerCapabilities>(GetRequiredPropertyFromJson<IDictionary<string, object?>>(properties, JsonRpcStrings.Capabilities));
            object? protocolVersionValue = GetOptionalPropertyFromJson(properties, JsonRpcStrings.ProtocolVersion);
            string? protocolVersion = protocolVersionValue switch
            {
                null => null,
                string value => value,
                _ => throw new MessageFormatException(
                    $"'{JsonRpcStrings.ProtocolVersion}' field has wrong type (expected String)"),
            };

            return new InitializeResponseArgs(processId, serverInfo, capabilities)
            {
                ProtocolVersion = protocolVersion,
            };
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
            string runIdString = GetRequiredPropertyFromJson<string>(properties, JsonRpcStrings.RunId);
            if (!Guid.TryParse(runIdString, out Guid runId))
            {
                throw new MessageFormatException(JsonRpcStrings.InvalidRunIdErrorMessage);
            }

            ICollection<TestNode>? tests = DeserializeOptionalTestNodes(properties);
            string? filter = GetOptionalTypedProperty<string>(properties, JsonRpcStrings.Filter);

            return new DiscoverRequestArgs(runId, tests, filter);
        });

        Deserializers[typeof(RunRequestArgs)] = new ObjectDeserializer<RunRequestArgs>(properties =>
        {
            string runIdString = GetRequiredPropertyFromJson<string>(properties, JsonRpcStrings.RunId);
            if (!Guid.TryParse(runIdString, out Guid runId))
            {
                throw new MessageFormatException(JsonRpcStrings.InvalidRunIdErrorMessage);
            }

            ICollection<TestNode>? tests = DeserializeOptionalTestNodes(properties);
            string? filter = GetOptionalTypedProperty<string>(properties, JsonRpcStrings.Filter);

            return new RunRequestArgs(runId, tests, filter);
        });

        Deserializers[typeof(TestNode)] = new ObjectDeserializer<TestNode>(
            properties =>
            {
                string uid = GetRequiredPropertyFromJson<string>(properties, JsonRpcStrings.Uid);
                string displayName = GetRequiredPropertyFromJson<string>(properties, JsonRpcStrings.DisplayName);
                if (RoslynString.IsNullOrWhiteSpace(uid))
                {
                    throw new MessageFormatException($"'{JsonRpcStrings.Uid}' field cannot be empty or whitespace");
                }

                PropertyBag propertyBag = new();

                if (properties.TryGetValue("location.file", out object? locationFileValue))
                {
                    if (locationFileValue is not string locationFile)
                    {
                        throw new MessageFormatException("'location.file' field has wrong type (expected String)");
                    }

                    bool hasLineStart = properties.TryGetValue("location.line-start", out object? locationLineStartValue);
                    bool hasLineEnd = properties.TryGetValue("location.line-end", out object? locationLineEndValue);
                    if (!hasLineStart || locationLineStartValue is not int locationLineStart
                        || !hasLineEnd || locationLineEndValue is not int locationLineEnd)
                    {
                        throw new MessageFormatException(
                            "'location.file', 'location.line-start', and 'location.line-end' fields must be specified together as strings and integers");
                    }

                    TestFileLocationProperty testFileLocationProperty = new(
                        locationFile,
                        new LinePositionSpan(
                            new LinePosition(locationLineStart, 0),
                            new LinePosition(locationLineEnd, 0)));
                    propertyBag.Add(testFileLocationProperty);
                }
                else if (properties.ContainsKey("location.line-start") || properties.ContainsKey("location.line-end"))
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

        Deserializers[typeof(CancelRequestArgs)] = new ObjectDeserializer<CancelRequestArgs>(properties =>
        {
            object? idObj = GetOptionalPropertyFromJson(properties, JsonRpcStrings.Id);
            int id = GetIdFromJson(idObj) ?? throw new MessageFormatException("id field should be a string or an int");

            return new CancelRequestArgs(id) { StringId = idObj as string };
        });

        Deserializers[typeof(ExitRequestArgs)] = new ObjectDeserializer<ExitRequestArgs>(_ => new ExitRequestArgs());

        // Deserialize an error
        Deserializers[typeof(ErrorMessage)] = new ObjectDeserializer<ErrorMessage>(properties =>
        {
            ValidateJsonRpcHeader(properties);
            object idObj = GetRequiredPropertyFromJson<object>(properties, JsonRpcStrings.Id);
            IDictionary<string, object> errorObj = GetRequiredPropertyFromJson<IDictionary<string, object>>(properties, JsonRpcStrings.Error);

#if !NETCOREAPP
            if (errorObj.TryGetValue(JsonRpcStrings.Data, out object? errorData) &&
                errorData is JsonObject { Count: 0 })
            {
                errorObj[JsonRpcStrings.Data] = null!;
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
                Data: data)
            {
                StringId = idObj as string,
            };
        });
    }

    private static ICollection<TestNode>? DeserializeOptionalTestNodes(IDictionary<string, object?> properties)
    {
        ICollection<object>? testsJson = GetOptionalTypedProperty<ICollection<object>>(properties, JsonRpcStrings.Tests);
        if (testsJson is null)
        {
            return null;
        }

        List<TestNode> tests = [];
        foreach (object? testJson in testsJson)
        {
            if (testJson is not IDictionary<string, object?> testProperties)
            {
                throw new MessageFormatException($"'{JsonRpcStrings.Tests}' entries must be objects");
            }

            tests.Add(Deserialize<TestNode>(testProperties));
        }

        return tests;
    }

    private static T? GetOptionalTypedProperty<T>(IDictionary<string, object?> properties, string propertyName)
        where T : class
    {
        object? value = GetOptionalPropertyFromJson(properties, propertyName);
        return value switch
        {
            null => null,
            T typed => typed,
            _ => throw new MessageFormatException(
                $"'{propertyName}' field has wrong type (expected {typeof(T).Name})"),
        };
    }
}
