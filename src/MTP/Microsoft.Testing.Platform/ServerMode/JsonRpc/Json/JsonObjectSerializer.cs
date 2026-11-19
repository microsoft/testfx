// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.ServerMode.Json;

internal abstract class JsonObjectSerializer : JsonSerializer
{
    public Func<object, (string Key, object? Value)[]?>? Properties { get; set; }

    public (string Key, object? Value)[]? GetProperties(object o)
    {
        Func<object, (string Key, object? Value)[]?> properties = Properties
            ?? throw new InvalidOperationException($"The '{nameof(Properties)}' delegate on '{GetType().Name}' has not been initialized. Assign the '{nameof(Properties)}' delegate before calling '{nameof(GetProperties)}'.");

        return properties(o);
    }
}
