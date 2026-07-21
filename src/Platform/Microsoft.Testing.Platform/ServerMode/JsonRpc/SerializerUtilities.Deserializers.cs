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

                object? idObj = GetOptionalPropertyFromJson(properties, JsonRpcStrings.Id);

                IDictionary<string, object?> paramsObj = method != JsonRpcMethods.Exit
                    ? GetRequiredPropertyFromJson<IDictionary<string, object?>>(properties, JsonRpcStrings.Params)
                    : new Dictionary<string, object?>();

                int? id = idObj is null
                            ? null
                            : GetIdFromJson(idObj) ?? throw new MessageFormatException("id field should be a string or an int");

                object? @params;
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

                        // Note: Let the server report unknown RPC request back to the client.
                        _ => null,
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
            bool isStateful = GetOptionalPropertyFromJson(testingCapabilities, JsonRpcStrings.IsStateful) as bool? ?? false;

            return new ClientCapabilities(debuggerProvider, isStateful);
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
            string runIdString = GetRequiredPropertyFromJson<string>(properties, JsonRpcStrings.RunId);
            if (!Guid.TryParse(runIdString, out Guid runId))
            {
                throw new MessageFormatException(JsonRpcStrings.InvalidRunIdErrorMessage);
            }

            var testsJson = GetOptionalPropertyFromJson(properties, JsonRpcStrings.Tests) as ICollection<object>;

            ICollection<TestNode>? tests = testsJson?.OfType<IDictionary<string, object?>>()?.Select(obj => Deserialize<TestNode>(obj)).ToList();

            string? filter = GetOptionalPropertyFromJson(properties, JsonRpcStrings.Filter) as string;

            return new DiscoverRequestArgs(runId, tests, filter);
        });

        Deserializers[typeof(RunRequestArgs)] = new ObjectDeserializer<RunRequestArgs>(properties =>
        {
            string runIdString = GetRequiredPropertyFromJson<string>(properties, JsonRpcStrings.RunId);
            if (!Guid.TryParse(runIdString, out Guid runId))
            {
                throw new MessageFormatException(JsonRpcStrings.InvalidRunIdErrorMessage);
            }

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

                foreach (KeyValuePair<string, object?> kvp in properties)
                {
                    if (kvp.Key == JsonRpcStrings.Uid)
                    {
                        uid = kvp.Value as string ?? string.Empty;
                        continue;
                    }

                    if (kvp.Key == JsonRpcStrings.DisplayName)
                    {
                        displayName = kvp.Value as string ?? string.Empty;
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
                Data: data);
        });
    }
}
