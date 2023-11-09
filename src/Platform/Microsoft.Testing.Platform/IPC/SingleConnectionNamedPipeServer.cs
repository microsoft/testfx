﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if NETCOREAPP
using System.Buffers;
#endif
using System.IO.Pipes;
using System.Runtime.InteropServices;

using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.Logging;

namespace Microsoft.Testing.Platform.IPC;

internal sealed class SingleConnectionNamedPipeServer : NamedPipeBase, IServer
{
    private readonly Func<IRequest, Task<IResponse>> _callback;
    private readonly IEnvironment _environment;
    private readonly NamedPipeServerStream _namedPipeServerStream;
    private readonly ILogger _logger;
    private readonly ITask _task;
    private readonly CancellationToken _cancellationToken;
    private readonly MemoryStream _serializationBuffer = new();
    private readonly MemoryStream _messageBuffer = new();
    private readonly byte[] _readBuffer = new byte[250000];
    private Task? _loopTask;
    private bool _disposed;

    public PipeNameDescription PipeName { get; private set; }

    public bool WasConnected { get; private set; }

    public SingleConnectionNamedPipeServer(
        PipeNameDescription pipeNameDescription,
        Func<IRequest, Task<IResponse>> callback,
        IEnvironment environment,
        ILogger logger,
        ITask task,
        CancellationToken cancellationToken)
    {
        ArgumentGuard.Ensure(pipeNameDescription != null, nameof(pipeNameDescription), "Pipe name cannot be null");
        _namedPipeServerStream = new((PipeName = pipeNameDescription).Name, PipeDirection.InOut, 1);
        _callback = callback;
        _environment = environment;
        _logger = logger;
        _task = task;
        _cancellationToken = cancellationToken;
    }

    public SingleConnectionNamedPipeServer(
        string name,
        Func<IRequest, Task<IResponse>> callback,
        IEnvironment environment,
        ILogger logger,
        ITask task,
        CancellationToken cancellationToken)
        : this(GetPipeName(name), callback, environment, logger, task, cancellationToken)
    {
    }

    public async Task WaitConnectionAsync(CancellationToken cancellationToken)
    {
        await _logger.LogDebugAsync($"Waiting for connection for the pipe name {PipeName.Name}");
        await _namedPipeServerStream.WaitForConnectionAsync(cancellationToken);
        WasConnected = true;
        await _logger.LogDebugAsync($"Client connected to {PipeName.Name}");
        _loopTask = _task.Run(
            async () =>
        {
            try
            {
                await InternalLoopAsync(_cancellationToken);
            }
            catch (OperationCanceledException ex) when (ex.CancellationToken == _cancellationToken)
            {
                // We are being cancelled, so we don't need to wait anymore
                return;
            }
            catch (Exception ex)
            {
                await _logger.LogErrorAsync($"Exception on pipe: {PipeName.Name}", ex);
                _environment.FailFast($"[SingleConnectionNamedPipeServer] Unhandled exception:{_environment.NewLine}{ex}", ex);
            }
        }, cancellationToken);
    }

    /// <summary>
    /// 4 bytes = message size
    /// ------- Payload -------
    /// 4 bytes = serializer id
    /// x bytes = object buffer.
    /// </summary>
    private async Task InternalLoopAsync(CancellationToken cancellationToken)
    {
        int currentMessageSize = 0;
        int missingBytesToReadOfWholeMessage = 0;
        while (true)
        {
            int missingBytesToReadOfCurrentChunk = 0;
            int currentReadIndex = 0;
#if NETCOREAPP
            int currentReadBytes = await _namedPipeServerStream.ReadAsync(_readBuffer.AsMemory(currentReadIndex, _readBuffer.Length), cancellationToken);
#else
            int currentReadBytes = await _namedPipeServerStream.ReadAsync(_readBuffer, currentReadIndex, _readBuffer.Length, cancellationToken);
#endif
            if (currentReadBytes == 0)
            {
                // The client has disconnected
                return;
            }

            // Reset the current chunk size
            missingBytesToReadOfCurrentChunk = currentReadBytes;

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
                await _messageBuffer.WriteAsync(_readBuffer.AsMemory(currentReadIndex, missingBytesToReadOfCurrentChunk), cancellationToken);
#else
                await _messageBuffer.WriteAsync(_readBuffer, currentReadIndex, missingBytesToReadOfCurrentChunk, cancellationToken);
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
                INamedPipeSerializer requestNamedPipeSerializer = GetSerializer(serializerId);

                // Deserialize the message
                _messageBuffer.Position += sizeof(int); // Skip the serializer id
                var deserializedObject = (IRequest)requestNamedPipeSerializer.Deserialize(_messageBuffer);

                // Call the callback
                IResponse response = await _callback(deserializedObject);

                // Write the message size
                _messageBuffer.Position = 0;

                // Get the response serializer
                INamedPipeSerializer responseNamedPipeSerializer = GetSerializer(response.GetType());

                // Serialize the response
                responseNamedPipeSerializer.Serialize(response, _serializationBuffer);

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
                    if (!BitConverter.TryWriteBytes(bytes, sizeOfTheWholeMessage))
                    {
                        throw new InvalidOperationException("Unexpected exception during the byte conversion");
                    }

                    await _messageBuffer.WriteAsync(bytes.AsMemory(0, sizeof(int)), cancellationToken);
                }
                finally
                {
                    ArrayPool<byte>.Shared.Return(bytes);
                }
#else
                await _messageBuffer.WriteAsync(BitConverter.GetBytes(sizeOfTheWholeMessage), 0, sizeof(int), cancellationToken);
#endif

                // Write the serializer id
#if NETCOREAPP
                bytes = ArrayPool<byte>.Shared.Rent(sizeof(int));
                try
                {
                    if (!BitConverter.TryWriteBytes(bytes, responseNamedPipeSerializer.Id))
                    {
                        throw new InvalidOperationException("Unexpected exception during the byte conversion");
                    }

                    await _messageBuffer.WriteAsync(bytes.AsMemory(0, sizeof(int)), cancellationToken);
                }
                finally
                {
                    ArrayPool<byte>.Shared.Return(bytes);
                }
#else
                await _messageBuffer.WriteAsync(BitConverter.GetBytes(responseNamedPipeSerializer.Id), 0, sizeof(int), cancellationToken);
#endif

                // Write the message
#if NETCOREAPP
                await _messageBuffer.WriteAsync(_serializationBuffer.GetBuffer().AsMemory(0, (int)_serializationBuffer.Position), cancellationToken);
#else
                await _messageBuffer.WriteAsync(_serializationBuffer.GetBuffer(), 0, (int)_serializationBuffer.Position, cancellationToken);
#endif

                // Send the message
                try
                {
#if NETCOREAPP
                    await _namedPipeServerStream.WriteAsync(_messageBuffer.GetBuffer().AsMemory(0, (int)_messageBuffer.Position), cancellationToken);
#else
                    await _namedPipeServerStream.WriteAsync(_messageBuffer.GetBuffer(), 0, (int)_messageBuffer.Position, cancellationToken);
#endif
                    await _namedPipeServerStream.FlushAsync(cancellationToken);
                    if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                    {
                        _namedPipeServerStream.WaitForPipeDrain();
                    }
                }
                finally
                {
                    // Reset the buffers
                    _messageBuffer.Position = 0;
                    _serializationBuffer.Position = 0;
                }

                // Reset the control variables
                currentMessageSize = 0;
                missingBytesToReadOfWholeMessage = 0;
            }
        }
    }

    public static PipeNameDescription GetPipeName(string name)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return new PipeNameDescription($"testingplatform.pipe.{name.Replace('\\', '.')}", false);
        }

        string directoryId = Path.Combine(Path.GetTempPath(), name);
        Directory.CreateDirectory(directoryId);
        return new PipeNameDescription(
            !Directory.Exists(directoryId)
            ? throw new InvalidOperationException($"Directory: {directoryId} doesn't exist.")
            : Path.Combine(directoryId, ".pipe"), true);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        if (WasConnected)
        {
            // If the loop task is null at this point we have race condition, means that the task didn't start yet and we already dispose.
            // This is unexpected and we throw an exception.
            if (_loopTask is null)
            {
                throw new InvalidOperationException("Unexpected null _loopTask");
            }

            // To close gracefully we need to ensure that the client closed the stream line 103.
            if (!_loopTask.Wait(TimeoutHelper.DefaultHangTimeSpanTimeout))
            {
                throw new InvalidOperationException("InternalLoopAsync() didn't exit as expected");
            }
        }

        _namedPipeServerStream.Dispose();
        PipeName.Dispose();

        _disposed = true;
    }

#if NETCOREAPP
    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        if (WasConnected)
        {
            // If the loop task is null at this point we have race condition, means that the task didn't start yet and we already dispose.
            // This is unexpected and we throw an exception.
            if (_loopTask is null)
            {
                throw new InvalidOperationException("Unexpected null _loopTask");
            }

            try
            {
                // To close gracefully we need to ensure that the client closed the stream line 103.
                await _loopTask.WaitAsync(TimeoutHelper.DefaultHangTimeSpanTimeout);
            }
            catch (TimeoutException)
            {
                throw new InvalidOperationException("InternalLoopAsync() didn't exit as expected");
            }
        }

        _namedPipeServerStream.Dispose();
        PipeName.Dispose();

        _disposed = true;
    }
#endif
}
