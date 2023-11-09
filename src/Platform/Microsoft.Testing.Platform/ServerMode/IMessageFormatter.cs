// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.ServerMode;

/// <summary>
/// A message formatter converts objects into a serialized format and can deserialize data into
/// corresponding objects.
/// </summary>
internal interface IMessageFormatter
{
    string Id { get; }

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
    Task<string> SerializeAsync(object obj);
}
