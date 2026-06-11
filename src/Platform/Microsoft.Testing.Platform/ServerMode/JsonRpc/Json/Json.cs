// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Text.Json;

using Microsoft.Testing.Platform.Helpers;

namespace Microsoft.Testing.Platform.ServerMode.Json;

internal sealed partial class Json
{
    private readonly Dictionary<Type, JsonDeserializer> _deserializers = [];
    private readonly Dictionary<Type, JsonSerializer> _serializers = [];
    private readonly ObjectPool<MemoryStream> _memoryStreamPool = new(() => new MemoryStream());

    public Json(Dictionary<Type, JsonSerializer>? serializers = null, Dictionary<Type, JsonDeserializer>? deserializers = null)
    {
        RegisterDefaultSerializers(_serializers);
        RegisterDefaultDeserializers(_deserializers);

        // Try to add serializers passed from outside
        if (serializers is not null)
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

        if (deserializers is not null)
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
#pragma warning disable CA2007 // Consider calling ConfigureAwait on the awaited task
            await using Utf8JsonWriter writer = new(stream);
#pragma warning restore CA2007 // Consider calling ConfigureAwait on the awaited task
            await SerializeAsync(obj, writer).ConfigureAwait(false);
            await writer.FlushAsync().ConfigureAwait(false);
            return Encoding.UTF8.GetString(stream.GetBuffer().AsMemory().Span[..(int)stream.Position]);
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

    internal T Bind<T>(JsonElement element, string? property = null)
        => property is not null && !element.TryGetProperty(property, out element)
            ? throw new MessageFormatException($"'{property}' field is missing")
            : Deserialize<T>(element);

    internal bool TryBind<T>(JsonElement element, out T? value, string? property = null)
    {
        if (property is not null && !element.TryGetProperty(property, out element))
        {
            value = default;
            return false;
        }

        value = Deserialize<T>(element);
        return true;
    }

    internal bool TryArrayBind<T>(JsonElement element, out T[]? value, string? property = null)
    {
        if (property is not null && !element.TryGetProperty(property, out element))
        {
            value = default;
            return false;
        }

        value = [.. element.EnumerateArray().Select(Deserialize<T>)];
        return true;
    }

    private T Deserialize<T>(JsonElement element)
    {
        bool deserializerFound = _deserializers.TryGetValue(typeof(T), out JsonDeserializer? deserializer);

        if (deserializerFound && deserializer is JsonElementDeserializer<T> objectDeserializer)
        {
            return objectDeserializer.CreateObject(this, element);
        }

        if (deserializerFound && deserializer is JsonCollectionDeserializer<T> collectionDeserializer)
        {
            return collectionDeserializer.CreateObject(this, element);
        }

        if (deserializerFound && deserializer is JsonElementDeserializer<object> baseObjectDeserializer)
        {
            return (T)baseObjectDeserializer.CreateObject(this, element);
        }

        if (deserializerFound && deserializer is JsonCollectionDeserializer<object> baseCollectionDeserializer)
        {
            return (T)baseCollectionDeserializer.CreateObject(this, element);
        }

        // Cannot find deserializer
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
                (string Key, object? Value)[]? properties = objectConverter.GetProperties(obj);
                if (properties is not null)
                {
                    foreach ((string property, object? value) in properties)
                    {
                        writer.WritePropertyName(property);
                        await SerializeAsync(value, writer).ConfigureAwait(false);
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
                    await SerializeAsync(o, writer).ConfigureAwait(false);
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

    private static void ValidateJsonRpcHeader(Json json, JsonElement jsonElement)
    {
        string? rpcVersion = json.Bind<string>(jsonElement, JsonRpcStrings.JsonRpc);

        // Note: The test anywhere supports only JSON-RPC version 2.0
        if (rpcVersion is null or not "2.0")
        {
            throw new MessageFormatException("jsonrpc field is not valid");
        }
    }
}
