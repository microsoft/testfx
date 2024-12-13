// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Text.Json;

namespace Microsoft.Testing.Platform.ServerMode.Json;

internal abstract class JsonCollectionDeserializer<TCollection> : JsonDeserializer
{
    internal abstract TCollection CreateObject(Json json, JsonElement element);
}

internal sealed class JsonCollectionDeserializer<TCollection, TItem>(Func<JsonElement, TCollection> createCollection, Action<TCollection, TItem> addItem) : JsonCollectionDeserializer<TCollection>
    where TCollection : ICollection<TItem>
{
    private readonly Func<JsonElement, TCollection> _createCollection = createCollection;
    private readonly Action<TCollection, TItem> _addItem = addItem;

    public TCollection CreateCollection(JsonElement jsonElement)
        => _createCollection(jsonElement);

    public void AddItem(TCollection collection, TItem item)
        => _addItem(collection, item);

    internal override TCollection CreateObject(Json json, JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Null)
        {
            return default!;
        }

        TCollection collection = CreateCollection(element);
        foreach (JsonElement item in element.EnumerateArray())
        {
            AddItem(collection, json.Bind<TItem>(item));
        }

        return collection;
    }
}
