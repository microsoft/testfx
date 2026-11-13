// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Text.Json;

namespace Microsoft.Testing.Platform.ServerMode.Json;

internal sealed partial class Json
{
    private static void RegisterPrimitiveDeserializers(Dictionary<Type, JsonDeserializer> deserializers)
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
}
