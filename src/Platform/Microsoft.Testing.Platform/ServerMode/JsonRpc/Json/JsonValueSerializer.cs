// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Text.Json;

namespace Microsoft.Testing.Platform.ServerMode.Json;

internal abstract class JsonValueSerializer(Action<Utf8JsonWriter, object> serialize) : JsonSerializer
{
    public Action<Utf8JsonWriter, object> Serialize { get; } = serialize;
}
