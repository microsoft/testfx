// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Text.Json;

namespace Microsoft.Testing.Platform.ServerMode.Json;

internal sealed class JsonValueSerializer<T> : JsonValueSerializer
{
    public JsonValueSerializer(Action<Utf8JsonWriter, T> value) => Serialize = (w, o) => value(w, (T)o);
}
