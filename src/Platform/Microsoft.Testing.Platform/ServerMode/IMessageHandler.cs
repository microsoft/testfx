// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.ServerMode;

internal interface IMessageHandler
{
    /// <summary>
    /// Reads the next <see cref="RpcMessage"/> payload from the client.
    /// </summary>
    /// <remarks>
    /// The reading is likely backed by a data stream and the message handler is responsible
    /// for reading the data stream and deserialization of the message.
    /// If the reading from the stream is canceled will throw an exception.
    /// If deserialization of the message fails, also will throw an exception.
    /// </remarks>
    Task<RpcMessage?> ReadAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Writes the next <see cref="RpcMessage"/> to the client.
    /// </summary>
    /// <remarks>
    /// The writing is likely backed by a data stream and the message handler is responsible
    /// for writing to the data stream and serialization of the message.
    /// If serialization of the message fails, will throw an exception.
    /// </remarks>
    Task WriteRequestAsync(RpcMessage message, CancellationToken cancellationToken);
}
