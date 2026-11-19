// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Text.Json;

namespace Microsoft.Testing.Platform.ServerMode.Json;

internal abstract class JsonCollectionDeserializer<TCollection> : JsonDeserializer
{
    internal abstract TCollection CreateObject(Json json, JsonElement element);
}
