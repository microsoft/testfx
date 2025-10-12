// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if NETCOREAPP
using System.Buffers;
#endif

using System.IO.Pipes;

using Microsoft.Testing.Platform.Helpers;

#if NET
using Microsoft.Testing.Platform.Resources;
#endif

namespace Microsoft.Testing.Platform.IPC;

#pragma warning disable CA1416 // Validate platform compatibility
internal sealed class NamedPipeClient : NamedPipeBase, IClient
{
    private const PipeOptions CurrentUserPipeOptions = PipeOptions.None
#if NET
        | PipeOptions.CurrentUserOnly
#endif
        ;

    private readonly NamedPipeClientStream _namedPipeClientStream;
    private readonly SemaphoreSlim _lock = new(1, 1);

    private readonly MemoryStream _serializationBuffer = new();
    private readonly MemoryStream _messageBuffer = new();
    private readonly byte[] _readBuffer = new byte[250000];
    private readonly IEnvironment _environment;

    private bool _disposed;

    public NamedPipeClient(string name)
        : this(name, new SystemEnvironment())
    {
    }

    public NamedPipeClient(string name, IEnvironment environment)
    {
        Guard.NotNull(name);
        _namedPipeClientStream = new(".", name, PipeDirection.InOut, CurrentUserPipeOptions);
        PipeName = name;
        _environment = environment;
    }

    public string PipeName { get; }

    public bool IsConnected => _namedPipeClientStream.IsConnected;

    public async Task ConnectAsync(CancellationToken cancellationToken)
        => await _namedPipeClientStream.ConnectAsync(cancellationToken).ConfigureAwait(false);

    public async Task<TResponse> RequestReplyAsync<TRequest, TResponse>(TRequest request, CancellationToken cancellationToken)
       where TRequest : IRequest
       where TResponse : IResponse
    {
        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            INamedPipeSerializer requestNamedPipeSerializer = GetSerializer(typeof(TRequest));

            // Ask to serialize the body
            _serializationBuffer.Position = 0;
            requestNamedPipeSerializer.Serialize(request, _serializationBuffer);

            // Write the message size
            _messageBuffer.Position = 0;

            // The length of the message is the size of the message plus one byte to store the serializer id
            // Space for the message
            int sizeOfTheWholeMessage = (int)_serializationBuffer.Position;

            // Space for the serializer id
            sizeOfTheWholeMessage += sizeof(int);

            // Write the message size
#if NETCOREAPP
            byte[] bytes = ArrayPool<byte>.Shared.Rent(sizeof(int));
            try
            {
                ApplicationStateGuard.Ensure(BitConverter.TryWriteBytes(bytes, sizeOfTheWholeMessage), PlatformResources.UnexpectedExceptionDuringByteConversionErrorMessage);
                await _messageBuffer.WriteAsync(bytes.AsMemory(0, sizeof(int)), cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(bytes);
            }
#else
            await _messageBuffer.WriteAsync(BitConverter.GetBytes(sizeOfTheWholeMessage), 0, sizeof(int), cancellationToken).ConfigureAwait(false);
#endif

            // Write the serializer id
#if NETCOREAPP
            bytes = ArrayPool<byte>.Shared.Rent(sizeof(int));
            try
            {
                ApplicationStateGuard.Ensure(BitConverter.TryWriteBytes(bytes, requestNamedPipeSerializer.Id), PlatformResources.UnexpectedExceptionDuringByteConversionErrorMessage);
                await _messageBuffer.WriteAsync(bytes.AsMemory(0, sizeof(int)), cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(bytes);
            }
#else
            await _messageBuffer.WriteAsync(BitConverter.GetBytes(requestNamedPipeSerializer.Id), 0, sizeof(int), cancellationToken).ConfigureAwait(false);
#endif

            try
            {
                // Write the message
#if NETCOREAPP
                await _messageBuffer.WriteAsync(_serializationBuffer.GetBuffer().AsMemory(0, (int)_serializationBuffer.Position), cancellationToken).ConfigureAwait(false);
#else
                await _messageBuffer.WriteAsync(_serializationBuffer.GetBuffer(), 0, (int)_serializationBuffer.Position, cancellationToken).ConfigureAwait(false);
#endif
            }
            finally
            {
                // Reset the serialization buffer
                _serializationBuffer.Position = 0;
            }

            // Send the message
            try
            {
#if NETCOREAPP
                await _namedPipeClientStream.WriteAsync(_messageBuffer.GetBuffer().AsMemory(0, (int)_messageBuffer.Position), cancellationToken).ConfigureAwait(false);
#else
                await _namedPipeClientStream.WriteAsync(_messageBuffer.GetBuffer(), 0, (int)_messageBuffer.Position, cancellationToken).ConfigureAwait(false);
#endif
                await _namedPipeClientStream.FlushAsync(cancellationToken).ConfigureAwait(false);
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    _namedPipeClientStream.WaitForPipeDrain();
                }
            }
            finally
            {
                // Reset the buffers
                _messageBuffer.Position = 0;
                _serializationBuffer.Position = 0;
            }

            // Read the response
            int currentMessageSize = 0;
            int missingBytesToReadOfWholeMessage = 0;
            while (true)
            {
                int currentReadIndex = 0;
#if NETCOREAPP
                int currentReadBytes = await _namedPipeClientStream.ReadAsync(_readBuffer.AsMemory(currentReadIndex, _readBuffer.Length), cancellationToken).ConfigureAwait(false);
#else
                int currentReadBytes = await _namedPipeClientStream.ReadAsync(_readBuffer, currentReadIndex, _readBuffer.Length, cancellationToken).ConfigureAwait(false);
#endif

                if (currentReadBytes == 0)
                {
                    // We are reading a message response.
                    // If we cannot get a response, there is no way we can recover and continue executing.
                    // This can happen if the other processes gets killed or crashes while while it's sending the response.
                    // This is especially important for 'dotnet test', where the user can simply kill the dotnet.exe process themselves.
                    // In that case, we want the MTP process to also die.
                    // Exit code 1 indicates abnormal termination due to IPC connection loss.
                    _environment.Exit(ExitCodes.GenericFailure);
                }

                // Reset the current chunk size
                int missingBytesToReadOfCurrentChunk = currentReadBytes;

                // If currentRequestSize is 0, we need to read the message size
                if (currentMessageSize == 0)
                {
                    // We need to read the message size, first 4 bytes
                    currentMessageSize = BitConverter.ToInt32(_readBuffer, 0);
                    missingBytesToReadOfCurrentChunk = currentReadBytes - sizeof(int);
                    missingBytesToReadOfWholeMessage = currentMessageSize;
                    currentReadIndex = sizeof(int);
                }

                if (missingBytesToReadOfCurrentChunk > 0)
                {
                    // We need to read the rest of the message
#if NETCOREAPP
                    await _messageBuffer.WriteAsync(_readBuffer.AsMemory(currentReadIndex, missingBytesToReadOfCurrentChunk), cancellationToken).ConfigureAwait(false);
#else
                    await _messageBuffer.WriteAsync(_readBuffer, currentReadIndex, missingBytesToReadOfCurrentChunk, cancellationToken).ConfigureAwait(false);
#endif
                    missingBytesToReadOfWholeMessage -= missingBytesToReadOfCurrentChunk;
                }

                // If we have read all the message, we can deserialize it
                if (missingBytesToReadOfWholeMessage == 0)
                {
                    // Deserialize the message
                    _messageBuffer.Position = 0;

                    // Get the serializer id
                    int serializerId = BitConverter.ToInt32(_messageBuffer.GetBuffer(), 0);

                    // Get the serializer
                    _messageBuffer.Position += sizeof(int); // Skip the serializer id
                    INamedPipeSerializer responseNamedPipeSerializer = GetSerializer(serializerId);

                    // Deserialize the message
                    try
                    {
                        return (TResponse)responseNamedPipeSerializer.Deserialize(_messageBuffer);
                    }
                    finally
                    {
                        // Reset the message buffer
                        _messageBuffer.Position = 0;
                    }
                }
            }
        }
        finally
        {
            _lock.Release();
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _lock.Dispose();
        _serializationBuffer.Dispose();
        _messageBuffer.Dispose();
        _namedPipeClientStream.Dispose();
        _disposed = true;
    }

#if NETCOREAPP
    [Obsolete("All owned fields are disposed synchronously. Introduction of DisposeAsync here is unnecessary complexity.")]
    // NOTE: While NamedPipeClient is internal API, it's breaking to change it as it's consumed via IVT by MTP extensions.
    // If we removed DisposeAsync in newer MTP version, but an old MTP extension is used with newer MTP version, we will get MissingMethodException.
    // It might be more safe to obsolete for now, and potentially remove after few versions are released when most users will
    // already be on those newer versions, and the risk of break is reduced.
    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _lock.Dispose();
        await _serializationBuffer.DisposeAsync().ConfigureAwait(false);
        await _messageBuffer.DisposeAsync().ConfigureAwait(false);
        await _namedPipeClientStream.DisposeAsync().ConfigureAwait(false);
        _disposed = true;
    }
#endif
}
