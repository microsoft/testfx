// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.ServerMode.Json;

internal sealed class JsonObjectSerializer<T> : JsonObjectSerializer
{
    public JsonObjectSerializer(Func<T, (string Key, object? Value)[]?> value) => Properties = (object o) => value((T)o);
}
