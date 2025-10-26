// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.IPC;

/// <summary>
/// Marks a Request or Response type for automatic serializer generation.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = false, Inherited = false)]
internal sealed class GenerateSerializerAttribute : Attribute
{
    /// <summary>
    /// Initializes a new instance of the <see cref="GenerateSerializerAttribute"/> class.
    /// </summary>
    /// <param name="serializerId">The unique ID for the serializer.</param>
    public GenerateSerializerAttribute(int serializerId)
    {
        SerializerId = serializerId;
    }

    /// <summary>
    /// Gets the unique ID for the serializer.
    /// </summary>
    public int SerializerId { get; }
}
