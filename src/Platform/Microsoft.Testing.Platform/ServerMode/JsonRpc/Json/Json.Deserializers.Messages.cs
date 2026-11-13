// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Text.Json;

using Microsoft.Testing.Platform.Extensions.Messages;

namespace Microsoft.Testing.Platform.ServerMode.Json;

internal sealed partial class Json
{
    private static void RegisterRpcMessageDeserializer(Dictionary<Type, JsonDeserializer> deserializers)
        => deserializers[typeof(RpcMessage)] = new JsonElementDeserializer<RpcMessage>((json, jsonElement) =>
        {
            ValidateJsonRpcHeader(json, jsonElement);

            if (jsonElement.TryGetProperty(JsonRpcStrings.Error, out _)
                && jsonElement.TryGetProperty(JsonRpcStrings.Id, out JsonElement errorId)
                && errorId.ValueKind == JsonValueKind.Null)
            {
                return json.Bind<ErrorMessage>(jsonElement);
            }

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

    private static void RegisterRequestDeserializers(Dictionary<Type, JsonDeserializer> deserializers)
    {
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
    }
}
