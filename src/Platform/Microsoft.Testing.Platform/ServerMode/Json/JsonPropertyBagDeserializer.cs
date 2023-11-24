// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Text.Json;

namespace Microsoft.Testing.Platform.ServerMode.Json;

internal sealed class JsonPropertyCollectionDeserializer<T>(Func<Json, IEnumerable<JsonProperty>, T> creator) : JsonDeserializer
{
    private readonly Func<Json, IEnumerable<JsonProperty>, T> _creator = creator;

    internal T CreateObject(Json json, IEnumerable<JsonProperty> properties)
        => _creator(json, properties);
}
