// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections;
using System.Text;
using System.Text.Json;

using Microsoft.Testing.Platform.Helpers;

namespace Microsoft.Testing.Platform.ServerMode.Json;

internal sealed class Json
{
    private readonly Dictionary<Type, JsonDeserializer> _deserializers = [];
    private readonly Dictionary<Type, JsonSerializer> _serializers = [];
    private readonly ObjectPool<MemoryStream> _memoryStreamPool = new(() => new MemoryStream());

    public Json(Dictionary<Type, JsonSerializer>? serializers = null, Dictionary<Type, JsonDeserializer>? deserializers = null)
    {
        // At the moment we're sometime not using custom performant serialization because we need to support
        // netstandard2.0 and to flat with jsonite we use a Dictionary<string, object?>
        // We share the serialization logic inside SerializerUtilities.
        _deserializers[typeof(JsoniteProperties)] = new JsonElementDeserializer<JsoniteProperties>(
        (json, jsonDocument) =>
        {
            var obj = new JsoniteProperties();
            foreach (JsonProperty property in jsonDocument.EnumerateObject())
            {
                // !!!DANGER!!!
                // This is a big boxing source, we have to implement custom json serialization for better performance.
                // And avoid to land here if serialization is already implemented for the type.
                object? value = DeserializeObject(json, property.Value);
                obj.Add(property.Name, value!);

                // !!!DANGER!!!
            }

            return obj.Count == 0 ? null! : obj;

            static object? DeserializeObject(Json json, JsonElement value)
                => value.ValueKind switch
                {
                    JsonValueKind.Object => json.Bind<JsoniteProperties>(value),
                    JsonValueKind.Array => value.EnumerateArray().Select(element => DeserializeObject(json, element)).ToList(),
                    JsonValueKind.String => value.GetString(),
                    JsonValueKind.Number => value.GetInt32(),
                    JsonValueKind.True => true,
                    JsonValueKind.False => false,
                    _ => null,
                };
        });

        // Overriden default serializers for better performance using .NET runtime serialization Apis

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
        _deserializers[typeof(int)] = new JsonElementDeserializer<int>((json, jsonDocument) => jsonDocument.GetInt32());
        _deserializers[typeof(decimal)] = new JsonElementDeserializer<decimal>((json, jsonDocument) => jsonDocument.GetDecimal());
        _deserializers[typeof(DateTime)] = new JsonElementDeserializer<DateTime>((json, jsonDocument) => jsonDocument.GetDateTime());

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
