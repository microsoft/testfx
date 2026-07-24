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
    // defined — there the STJ branch (ReadOnlyMemory<char>, Json.Json) does not exist. Behavior is identical
    // for the platform build, which only ever compiles this file as netstandard2.0 or .NET.
#if !NETCOREAPP
    internal static IMessageFormatter CreateFormatter()
        => new MessageFormatter();

    internal sealed class MessageFormatter : IMessageFormatter
    {
        public string Id => "Jsonite";

        public T Deserialize<T>(string serializedUtf8Content)
            => SerializerUtilities.Deserialize<T>((Jsonite.JsonObject)Jsonite.Json.Deserialize(serializedUtf8Content));

        public Task<string> SerializeAsync(object obj)
            => Task.FromResult(Jsonite.Json.Serialize(SerializerUtilities.Serialize(obj.GetType(), obj)));
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

        public T Deserialize<T>(ReadOnlyMemory<char> serializedUtf8Content) => _json.Deserialize<T>(serializedUtf8Content);

        public Task<string> SerializeAsync(object obj) => _json.SerializeAsync(obj);
    }
#endif
}
