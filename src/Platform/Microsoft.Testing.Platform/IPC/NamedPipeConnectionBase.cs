// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if NET
using System.Buffers;
#endif

using System.IO.Pipes;

using Microsoft.CodeAnalysis;
#if NET
using Microsoft.Testing.Platform.Helpers;
#endif

namespace Microsoft.Testing.Platform.IPC;

// Duplex-stream framing/transport shared by NamedPipeServer, NamedPipeClient, and DotnetTestWebSocketClient. This is
// deliberately NOT part of the source-shared serializer registry (NamedPipeBase): the transport differs per repo
// (e.g. dotnet/sdk inlines its own read/write loop), so only the registry + serializers are shared. This type stays
// local to Microsoft.Testing.Platform. It operates on any duplex Stream (PipeStream, a WebSocket-backed Stream, ...);
// the only pipe-specific behavior (WaitForPipeDrain) is isolated behind an `is PipeStream` check.
[Embedded]
[SuppressMessage("Design", "CA1001:Types that own disposable fields should be disposable", Justification = "Disposal is delegated to subclasses via DisposeBuffers().")]
internal abstract class NamedPipeConnectionBase : NamedPipeBase
{
    private readonly MemoryStream _serializationBuffer = new();
    private readonly MemoryStream _messageBuffer = new();
    private readonly byte[] _readBuffer = new byte[250000];

    /// <summary>
    /// Serializes <paramref name="message"/> using <paramref name="serializer"/>, frames it with a
    /// 4-byte size header and 4-byte serializer-ID prefix, writes the frame to <paramref name="stream"/>,
    /// flushes, and (on Windows, when <paramref name="stream"/> is a named pipe) waits for the pipe to drain.
    /// </summary>
    /// <remarks>
    /// This method is transport-neutral: <paramref name="stream"/> may be a <see cref="PipeStream"/> (the
    /// named-pipe transport) or any other duplex <see cref="Stream"/> (e.g. a WebSocket-backed duplex stream).
    /// The only pipe-specific behavior - <see cref="PipeStream.WaitForPipeDrain"/> - is isolated behind an
    /// <c>is PipeStream</c> check so it only ever runs for the named-pipe transport.
    /// </remarks>
    protected async Task WriteMessageAsync(Stream stream, INamedPipeSerializer serializer, object message, CancellationToken cancellationToken)
    {
        // Serialize the message body
        _serializationBuffer.Position = 0;
        serializer.Serialize(message, _serializationBuffer);

        // Build the framed message:
        //   4 bytes  – total payload length (serializer ID + body)
        //   4 bytes  – serializer ID
        //   N bytes  – serialized body
        _messageBuffer.Position = 0;
        int sizeOfTheWholeMessage = (int)_serializationBuffer.Position + sizeof(int);

        try
        {
            // Write the message size header
#if NET
            byte[] bytes = ArrayPool<byte>.Shared.Rent(sizeof(int));
            try
            {
                if (!BitConverter.TryWriteBytes(bytes, sizeOfTheWholeMessage))
                {
                    // TryWriteBytes only fails if destination is too small; we rented at least sizeof(int).
                    throw ApplicationStateGuard.Unreachable();
                }

                await _messageBuffer.WriteAsync(bytes.AsMemory(0, sizeof(int)), cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(bytes);
            }
#else
            await _messageBuffer.WriteAsync(BitConverter.GetBytes(sizeOfTheWholeMessage), 0, sizeof(int), cancellationToken).ConfigureAwait(false);
#endif

            // Write the serializer ID
#if NET
            bytes = ArrayPool<byte>.Shared.Rent(sizeof(int));
            try
            {
                if (!BitConverter.TryWriteBytes(bytes, serializer.Id))
                {
                    throw ApplicationStateGuard.Unreachable();
                }

                await _messageBuffer.WriteAsync(bytes.AsMemory(0, sizeof(int)), cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(bytes);
            }
#else
            await _messageBuffer.WriteAsync(BitConverter.GetBytes(serializer.Id), 0, sizeof(int), cancellationToken).ConfigureAwait(false);
#endif

            // Write the serialized payload
#if NET
            await _messageBuffer.WriteAsync(_serializationBuffer.GetBuffer().AsMemory(0, (int)_serializationBuffer.Position), cancellationToken).ConfigureAwait(false);
#else
            await _messageBuffer.WriteAsync(_serializationBuffer.GetBuffer(), 0, (int)_serializationBuffer.Position, cancellationToken).ConfigureAwait(false);
#endif

            // Send the framed message to the pipe stream
#if NET
            await stream.WriteAsync(_messageBuffer.GetBuffer().AsMemory(0, (int)_messageBuffer.Position), cancellationToken).ConfigureAwait(false);
#else
            await stream.WriteAsync(_messageBuffer.GetBuffer(), 0, (int)_messageBuffer.Position, cancellationToken).ConfigureAwait(false);
#endif
            await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
            if (stream is PipeStream pipeStream && RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                pipeStream.WaitForPipeDrain();
            }
        }
        finally
        {
            _messageBuffer.Position = 0;
            _serializationBuffer.Position = 0;
        }
    }

    /// <summary>
    /// Reads one complete framed message from <paramref name="stream"/>, deserializes it, and returns
    /// the result.  Returns <see langword="null"/> when the stream reaches EOF (peer disconnected).
    /// </summary>
    /// <remarks>
    /// Transport-neutral: see the remarks on <see cref="WriteMessageAsync"/>.
    /// </remarks>
    protected async Task<object?> ReadNextMessageAsync(Stream stream, CancellationToken cancellationToken)
    {
        _messageBuffer.Position = 0;

        try
        {
            // Read the 4-byte size header. PipeStream.ReadAsync may return fewer bytes than requested
            // (e.g. in byte-mode pipes), so we must loop until we've accumulated the full header or hit EOF.
            int headerBytesRead = 0;
            while (headerBytesRead < sizeof(int))
            {
#if NET
                int n = await stream.ReadAsync(_readBuffer.AsMemory(headerBytesRead, sizeof(int) - headerBytesRead), cancellationToken).ConfigureAwait(false);
#else
                int n = await stream.ReadAsync(_readBuffer, headerBytesRead, sizeof(int) - headerBytesRead, cancellationToken).ConfigureAwait(false);
#endif
                if (n == 0)
                {
                    // EOF – peer disconnected (cleanly if headerBytesRead == 0, mid-header otherwise);
                    // caller decides how to handle this.
                    return null;
                }

                headerBytesRead += n;
            }

            int currentMessageSize = BitConverter.ToInt32(_readBuffer, 0);
            int missingBytesToReadOfWholeMessage = currentMessageSize;

            // Read the message body in chunks, using _readBuffer as a transfer buffer.
            while (missingBytesToReadOfWholeMessage > 0)
            {
                int toRead = Math.Min(_readBuffer.Length, missingBytesToReadOfWholeMessage);
#if NET
                int n = await stream.ReadAsync(_readBuffer.AsMemory(0, toRead), cancellationToken).ConfigureAwait(false);
#else
                int n = await stream.ReadAsync(_readBuffer, 0, toRead, cancellationToken).ConfigureAwait(false);
#endif
                if (n == 0)
                {
                    // EOF mid-message – treat the same as a clean disconnect.
                    return null;
                }

#if NET
                await _messageBuffer.WriteAsync(_readBuffer.AsMemory(0, n), cancellationToken).ConfigureAwait(false);
#else
                await _messageBuffer.WriteAsync(_readBuffer, 0, n, cancellationToken).ConfigureAwait(false);
#endif
                missingBytesToReadOfWholeMessage -= n;
            }

            // Full message received – deserialize and return
            _messageBuffer.Position = 0;
            int serializerId = BitConverter.ToInt32(_messageBuffer.GetBuffer(), 0);
            INamedPipeSerializer namedPipeSerializer = GetSerializer(serializerId);
            _messageBuffer.Position += sizeof(int); // skip the serializer ID
            return namedPipeSerializer.Deserialize(_messageBuffer);
        }
        finally
        {
            _messageBuffer.Position = 0;
        }
    }

    /// <summary>Disposes the shared serialization and message buffers.</summary>
    protected void DisposeBuffers()
    {
        _serializationBuffer.Dispose();
        _messageBuffer.Dispose();
    }
}
