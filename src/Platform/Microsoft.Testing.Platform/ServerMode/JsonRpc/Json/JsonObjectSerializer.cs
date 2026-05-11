// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.ServerMode.Json;

internal abstract class JsonObjectSerializer : JsonSerializer
{
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
    public Func<object, (string Key, object? Value)[]?> Properties { get; set; }
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.

    public (string Key, object? Value)[]? GetProperties(object o)
        => Properties == null
            ? throw new InvalidOperationException($"The '{nameof(Properties)}' delegate on '{GetType().Name}' has not been initialized. Ensure all serializer instances are fully configured before use.")
            : Properties(o);
}
