// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Text.Json;

namespace Microsoft.Testing.Platform.ServerMode.Json;

internal sealed partial class Json
{
    private static bool? GetOptionalBoolean(JsonElement element, string propertyName)
        => !element.TryGetProperty(propertyName, out JsonElement value)
            ? null
            : value.ValueKind switch
            {
                JsonValueKind.Null => null,
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                _ => throw new MessageFormatException($"'{propertyName}' field has wrong type (expected {nameof(Boolean)})"),
            };

    private static void RegisterDefaultDeserializers(Dictionary<Type, JsonDeserializer> deserializers)
    {
        RegisterPrimitiveDeserializers(deserializers);
        RegisterRpcMessageDeserializer(deserializers);
        RegisterInitializeDeserializers(deserializers);
        RegisterRequestDeserializers(deserializers);
        RegisterErrorDeserializer(deserializers);
    }
}
