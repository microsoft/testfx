// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.ServerMode;

/// <summary>
/// A message formatter converts objects into a serialized format and can deserialize data into
/// corresponding objects.
/// </summary>
internal interface IMessageFormatter
{
    /// <summary>
    /// Gets the identifier of the formatter.
    /// </summary>
    string Id { get; }

    /// <summary>
    /// Deserializes the given serialized content into an object of type <typeparamref name="T"/>.
    /// </summary>
    /// <typeparam name="T">The type of the object to deserialize.</typeparam>
    /// <param name="serializedUtf8Content">The serialized utf-8 content.</param>
    /// <returns>The deserialized object.</returns>
    // Note: The current design might impose performance overhead, since the data
    //       is not directly deserialized from a stream, but rather first a string is extracted
    //       and allocated and then a string is deserialized.
    //       We could create a version that uses System.Buffers APIs that would only be supported for
    //       .NET 6 and above.
#if NETCOREAPP
    T Deserialize<T>(ReadOnlyMemory<char> serializedUtf8Content);

#else
    T Deserialize<T>(string serializedUtf8Content);

#endif

    /// <summary>
    /// Serializes the given object into a string.
    /// </summary>
    /// <param name="obj">The object to serialized.</param>
    /// <returns>The object serialized.</returns>
    Task<string> SerializeAsync(object obj);
}
