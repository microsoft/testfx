// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.ServerMode;

internal static partial class SerializerUtilities
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

        RegisterRpcMessageSerializers();
        RegisterTestNodeSerializers();
        RegisterDeserializers();
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
    {
        object? value = GetOptionalPropertyFromJson(properties, propertyName)
            ?? throw new MessageFormatException($"'{propertyName}' field is missing");

        return value is T typed
            ? typed
            : throw new MessageFormatException($"'{propertyName}' field has wrong type (expected {typeof(T).Name})");
    }

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
