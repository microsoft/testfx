// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Text.Json;

namespace Microsoft.Testing.Platform.ServerMode.Json;

internal static class JsonExtensions
{
    public static IEnumerable<JsonProperty> AllExcept(this JsonElement element, params string[] properties)
        => element.EnumerateObject().Where(p => !properties.Any(n => p.NameEquals(n)));
}
