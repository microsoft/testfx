// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

// Note: System.Text.Json is only available in .NET 6.0 and above.
//       As such, we have two separate implementations for the serialization code.
#if NETCOREAPP
using Microsoft.Testing.Platform.ServerMode.Json;
#endif

namespace Microsoft.Testing.Platform.ServerMode;

internal sealed class FormatterUtilities
{
    // The formatter selection mirrors the IMessageFormatter guard (#if NETCOREAPP): System.Text.Json on
    // .NET, Jsonite everywhere else. Using !NETCOREAPP (rather than NETSTANDARD2_0) keeps this correct when
    // the file is shipped as source and compiled on .NET Framework (net462), where NETSTANDARD2_0 is not
    // defined — there the STJ branch (ReadOnlyMemory<byte>, Json.Json) does not exist. Behavior is identical
    // for the platform build, which only ever compiles this file as netstandard2.0 or .NET.
#if !NETCOREAPP
    internal static IMessageFormatter CreateFormatter()
        => new MessageFormatter();

    internal sealed class MessageFormatter : IMessageFormatter
    {
        private static readonly Jsonite.JsonSettings NumericTextSettings = new()
        {
            ParseValuesAsStrings = true,
        };

        public string Id => "Jsonite";

        public T Deserialize<T>(string serializedUtf8Content)
        {
            var properties = (Jsonite.JsonObject)Jsonite.Json.Deserialize(serializedUtf8Content);
            PreserveExactRpcIds(properties, serializedUtf8Content);
            return SerializerUtilities.Deserialize<T>(properties);
        }

        public Task<string> SerializeAsync(object obj)
            => Task.FromResult(Jsonite.Json.Serialize(SerializerUtilities.Serialize(obj.GetType(), obj)));

        private static void PreserveExactRpcIds(Jsonite.JsonObject properties, string serializedContent)
        {
            bool hasFloatingPointMessageId = properties.TryGetValue(JsonRpcStrings.Id, out object? messageId)
                && messageId is double;
            Jsonite.JsonObject? paramsObject = properties.TryGetValue(JsonRpcStrings.Params, out object? paramsValue)
                ? paramsValue as Jsonite.JsonObject
                : null;
            bool hasFloatingPointCancellationId = properties.TryGetValue(JsonRpcStrings.Method, out object? method)
                && method is JsonRpcMethods.CancelRequest
                && paramsObject?.TryGetValue(JsonRpcStrings.Id, out object? cancellationId) == true
                && cancellationId is double;
            if (!hasFloatingPointMessageId && !hasFloatingPointCancellationId)
            {
                return;
            }

            var rawProperties = (Jsonite.JsonObject)Jsonite.Json.Deserialize(serializedContent, NumericTextSettings);
            if (hasFloatingPointMessageId)
            {
                PreserveExactRpcId(properties, rawProperties);
            }

            if (hasFloatingPointCancellationId
                && rawProperties[JsonRpcStrings.Params] is Jsonite.JsonObject rawParams)
            {
                PreserveExactRpcId(paramsObject!, rawParams);
            }
        }

        private static void PreserveExactRpcId(Jsonite.JsonObject properties, Jsonite.JsonObject rawProperties)
        {
            string rawId = (string)rawProperties[JsonRpcStrings.Id]!;
            properties[JsonRpcStrings.Id] = RpcIdParser.TryParseNumericId(rawId, out int id)
                ? id
                : rawId;
        }
    }
#else
    internal static IMessageFormatter CreateFormatter() => new MessageFormatter();

    internal sealed class MessageFormatter : IMessageFormatter
    {
        private readonly Json.Json _json;

        public MessageFormatter()
        {
            Dictionary<Type, JsonSerializer> serializers = [];
            Dictionary<Type, JsonDeserializer> deserializers = [];

            foreach (Type serializableType in SerializerUtilities.SerializerTypes)
            {
                serializers[serializableType] = new JsonObjectSerializer<object>(
                    o => [.. SerializerUtilities.Serialize(serializableType, o).Select(kvp => (kvp.Key, kvp.Value))]);
            }

            foreach (Type deserializableType in SerializerUtilities.DeserializerTypes)
            {
                // By default we wrap the jsonite serialization, we can override specific types inside the Json .NET runtime implementation.
                deserializers[deserializableType] = new JsonElementDeserializer<object>((json, doc) =>
                    SerializerUtilities.Deserialize(deserializableType, json.Bind<JsoniteProperties>(doc)!));
            }

            _json = new Json.Json(serializers, deserializers);
        }

        public string Id => "System.Text.Json";

        public T Deserialize<T>(ReadOnlyMemory<byte> serializedUtf8Content) => _json.Deserialize<T>(serializedUtf8Content);

        public Task<string> SerializeAsync(object obj) => _json.SerializeAsync(obj);
    }
#endif
}
