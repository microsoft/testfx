// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if NET
using System.Buffers;
#endif

using System.IO.Pipes;

using Microsoft.CodeAnalysis;
using Microsoft.Testing.Platform.Helpers;

namespace Microsoft.Testing.Platform.IPC;

[Embedded]
[SuppressMessage("Design", "CA1001:Types that own disposable fields should be disposable", Justification = "Disposal is delegated to subclasses via DisposeBuffers().")]
internal abstract class NamedPipeBase
{
    private readonly Dictionary<Type, INamedPipeSerializer> _typeSerializer = [];
    private readonly Dictionary<int, INamedPipeSerializer> _idSerializer = [];
    private readonly MemoryStream _serializationBuffer = new();
    private readonly MemoryStream _messageBuffer = new();
    private readonly byte[] _readBuffer = new byte[250000];

    public void RegisterSerializer(INamedPipeSerializer namedPipeSerializer, Type type)
    {
        _typeSerializer.Add(type, namedPipeSerializer);
        _idSerializer.Add(namedPipeSerializer.Id, namedPipeSerializer);
    }

    protected INamedPipeSerializer GetSerializer(int id)
        => _idSerializer[id];

    protected INamedPipeSerializer GetSerializer(Type type)
        => _typeSerializer[type];

    /// <summary>
    /// Serializes <paramref name="message"/> using <paramref name="serializer"/>, frames it with a
    /// 4-byte size header and 4-byte serializer-ID prefix, writes the frame to <paramref name="stream"/>,
    /// flushes, and (on Windows) waits for the pipe to drain.
    /// </summary>
#if !MTP_MSBUILD_TASKS
    [UnsupportedOSPlatform("browser")]
#endif
    protected async Task WriteMessageAsync(PipeStream stream, INamedPipeSerializer serializer, object message, CancellationToken cancellationToken)
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
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                stream.WaitForPipeDrain();
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
#if !MTP_MSBUILD_TASKS
    [UnsupportedOSPlatform("browser")]
#endif
    protected async Task<object?> ReadNextMessageAsync(PipeStream stream, CancellationToken cancellationToken)
    {
        int currentMessageSize = 0;
        int missingBytesToReadOfWholeMessage = 0;
        _messageBuffer.Position = 0;

        while (true)
        {
            int currentReadIndex = 0;
#if NET
            int currentReadBytes = await stream.ReadAsync(_readBuffer.AsMemory(currentReadIndex, _readBuffer.Length), cancellationToken).ConfigureAwait(false);
#else
            int currentReadBytes = await stream.ReadAsync(_readBuffer, currentReadIndex, _readBuffer.Length, cancellationToken).ConfigureAwait(false);
#endif

            if (currentReadBytes == 0)
            {
                // EOF – peer disconnected; caller decides how to handle this.
                return null;
            }

            // Reset per-chunk tracking
            int missingBytesToReadOfCurrentChunk = currentReadBytes;

            // If this is the start of a new message, read the 4-byte size header
            if (currentMessageSize == 0)
            {
                if (currentReadBytes < sizeof(int))
                {
                    throw ApplicationStateGuard.Unreachable();
                }

                currentMessageSize = BitConverter.ToInt32(_readBuffer, 0);
                missingBytesToReadOfCurrentChunk = currentReadBytes - sizeof(int);
                missingBytesToReadOfWholeMessage = currentMessageSize;
                currentReadIndex = sizeof(int);
            }

            if (missingBytesToReadOfCurrentChunk > 0)
            {
#if NET
                await _messageBuffer.WriteAsync(_readBuffer.AsMemory(currentReadIndex, missingBytesToReadOfCurrentChunk), cancellationToken).ConfigureAwait(false);
#else
                await _messageBuffer.WriteAsync(_readBuffer, currentReadIndex, missingBytesToReadOfCurrentChunk, cancellationToken).ConfigureAwait(false);
#endif
                missingBytesToReadOfWholeMessage -= missingBytesToReadOfCurrentChunk;
            }

            if (missingBytesToReadOfWholeMessage < 0)
            {
                throw ApplicationStateGuard.Unreachable();
            }

            if (missingBytesToReadOfWholeMessage == 0)
            {
                // Full message received – deserialize and return
                _messageBuffer.Position = 0;
                int serializerId = BitConverter.ToInt32(_messageBuffer.GetBuffer(), 0);
                INamedPipeSerializer namedPipeSerializer = GetSerializer(serializerId);
                _messageBuffer.Position += sizeof(int); // skip the serializer ID
                try
                {
                    return namedPipeSerializer.Deserialize(_messageBuffer);
                }
                finally
                {
                    _messageBuffer.Position = 0;
                }
            }
        }
    }

    /// <summary>Disposes the shared serialization and message buffers.</summary>
    protected void DisposeBuffers()
    {
        _serializationBuffer.Dispose();
        _messageBuffer.Dispose();
    }
}
