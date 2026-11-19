// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Text.Json;

namespace Microsoft.Testing.Platform.ServerMode.Json;

internal sealed class JsonElementDeserializer<T>(Func<Json, JsonElement, T> createObject) : JsonDeserializer
{
    private readonly Func<Json, JsonElement, T> _activator = createObject;

    internal T CreateObject(Json json, JsonElement element) => _activator(json, element);
}
