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
#if NETCOREAPP
    // The content is passed as UTF-8 bytes because that is what the wire carries (Content-Length counts
    // bytes) and what System.Text.Json parses, so no transcoding is needed on the read path.
    //
    // Implementations must fully materialize the result before returning: JsonDocument.Parse does not copy
    // the buffer it is given, and the caller is free to reuse or pool it once this returns.
    T Deserialize<T>(ReadOnlyMemory<byte> serializedUtf8Content);

#else
    // Jsonite only accepts a string, so the net462/netstandard2.0 path still decodes before parsing.
    T Deserialize<T>(string serializedUtf8Content);

#endif

    /// <summary>
    /// Serializes the given object into a string.
    /// </summary>
    /// <param name="obj">The object to serialized.</param>
    /// <returns>The object serialized.</returns>
    Task<string> SerializeAsync(object obj);
}
