// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.IO.Pipes;

using EasyNamedPipes;

using Microsoft.CodeAnalysis;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.Logging;
using Microsoft.Testing.Platform.Resources;

namespace Microsoft.Testing.Platform.IPC;

[Embedded]
internal sealed class NamedPipeServer : NamedPipeBase, IServer
{
#pragma warning disable CA1416 // Validate platform compatibility
    private const PipeOptions AsyncCurrentUserPipeOptions = PipeOptions.Asynchronous
#if NET
        | PipeOptions.CurrentUserOnly
#endif
        ;
#pragma warning restore CA1416 // Validate platform compatibility

    private static bool IsUnix => Path.DirectorySeparatorChar == '/';

    private readonly Func<IRequest, Task<IResponse>> _callback;
    private readonly IEnvironment _environment;
    private readonly NamedPipeServerStream _namedPipeServerStream;
    private readonly ILogger _logger;
    private readonly ITask _task;
    private readonly CancellationToken _cancellationToken;
    private readonly MemoryStream _serializationBuffer = new();
    private readonly MemoryStream _messageBuffer = new();
    private readonly byte[] _readBuffer = new byte[250000];
#if NET
    private readonly byte[] _sizeOfIntArray = new byte[sizeof(int)];
#endif
    private Task? _loopTask;
    private bool _disposed;

    public NamedPipeServer(
        string name,
        Func<IRequest, Task<IResponse>> callback,
        IEnvironment environment,
        ILogger logger,
        ITask task,
        CancellationToken cancellationToken)
        : this(GetPipeName(name, environment), callback, environment, logger, task, cancellationToken)
    {
    }

    public NamedPipeServer(
        PipeNameDescription pipeNameDescription,
        Func<IRequest, Task<IResponse>> callback,
        IEnvironment environment,
        ILogger logger,
        ITask task,
        CancellationToken cancellationToken)
        : this(pipeNameDescription, callback, environment, logger, task, maxNumberOfServerInstances: 1, cancellationToken)
    {
    }

    public NamedPipeServer(
        PipeNameDescription pipeNameDescription,
        Func<IRequest, Task<IResponse>> callback,
        IEnvironment environment,
        ILogger logger,
        ITask task,
        int maxNumberOfServerInstances,
        CancellationToken cancellationToken)
    {
        Guard.NotNull(pipeNameDescription);
#pragma warning disable CA1416 // Validate platform compatibility
        _namedPipeServerStream = new((PipeName = pipeNameDescription).Name, PipeDirection.InOut, maxNumberOfServerInstances, PipeTransmissionMode.Byte, AsyncCurrentUserPipeOptions);
#pragma warning restore CA1416
        _callback = callback;
        _environment = environment;
        _logger = logger;
        _task = task;
        _cancellationToken = cancellationToken;
    }

    public PipeNameDescription PipeName { get; }

    public bool WasConnected { get; private set; }

    public async Task WaitConnectionAsync(CancellationToken cancellationToken)
    {
        // NOTE: _cancellationToken field is usually the "test session" cancellation token.
        // And cancellationToken parameter may have hang mitigating timeout.
        // The parameter should only be used for the call of WaitForConnectionAsync and Task.Run call.
        // NOTE: The cancellation token passed to Task.Run will only have effect before the task is started by runtime.
        // Once it starts, it won't be considered.
        // Then, for the internal loop, we should use _cancellationToken, because we don't know for how long the loop will run.
        // So what we pass to InternalLoopAsync shouldn't have any timeout (it's usually linked to Ctrl+C).
        await _logger.LogDebugAsync($"Waiting for connection for the pipe name {PipeName.Name}").ConfigureAwait(false);
#pragma warning disable CA1416 // Validate platform compatibility
        await _namedPipeServerStream.WaitForConnectionAsync(cancellationToken).ConfigureAwait(false);
#pragma warning restore CA1416
        WasConnected = true;
        await _logger.LogDebugAsync($"Client connected to {PipeName.Name}").ConfigureAwait(false);
        _loopTask = _task.Run(
            async () =>
        {
            try
            {
                await InternalLoopAsync(_cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException ex) when (ex.CancellationToken == _cancellationToken)
            {
                // We are being canceled, so we don't need to wait anymore
            }
            catch (Exception ex)
            {
                await _logger.LogErrorAsync($"Exception on pipe: {PipeName.Name}", ex).ConfigureAwait(false);
                _environment.FailFast($"[NamedPipeServer] Unhandled exception:{_environment.NewLine}{ex}", ex);
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
            int currentReadIndex = 0;
#if NET
#pragma warning disable CA1416 // Validate platform compatibility
            int currentReadBytes = await _namedPipeServerStream.ReadAsync(_readBuffer.AsMemory(currentReadIndex, _readBuffer.Length), cancellationToken).ConfigureAwait(false);
#pragma warning restore CA1416
#else
            int currentReadBytes = await _namedPipeServerStream.ReadAsync(_readBuffer, currentReadIndex, _readBuffer.Length, cancellationToken).ConfigureAwait(false);
#endif
            if (currentReadBytes == 0)
            {
                // The client has disconnected
                return;
            }

            // Reset the current chunk size
            int missingBytesToReadOfCurrentChunk = currentReadBytes;

            // If currentRequestSize is 0, we need to read the message size
            if (currentMessageSize == 0)
            {
                // We need to read the message size, first 4 bytes
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
                // We need to read the rest of the message
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
                IResponse response = await _callback(deserializedObject).ConfigureAwait(false);

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
#if NET
                byte[] bytes = _sizeOfIntArray;
                ApplicationStateGuard.Ensure(BitConverter.TryWriteBytes(bytes, sizeOfTheWholeMessage), PlatformResources.UnexpectedExceptionDuringByteConversionErrorMessage);
                ApplicationStateGuard.Ensure(bytes.Length == sizeof(int));
                await _messageBuffer.WriteAsync(bytes, cancellationToken).ConfigureAwait(false);
#else
                await _messageBuffer.WriteAsync(BitConverter.GetBytes(sizeOfTheWholeMessage), 0, sizeof(int), cancellationToken).ConfigureAwait(false);
#endif

                // Write the serializer id
#if NET
                bytes = _sizeOfIntArray;
                ApplicationStateGuard.Ensure(BitConverter.TryWriteBytes(bytes, responseNamedPipeSerializer.Id), PlatformResources.UnexpectedExceptionDuringByteConversionErrorMessage);

                await _messageBuffer.WriteAsync(bytes.AsMemory(0, sizeof(int)), cancellationToken).ConfigureAwait(false);
#else
                await _messageBuffer.WriteAsync(BitConverter.GetBytes(responseNamedPipeSerializer.Id), 0, sizeof(int), cancellationToken).ConfigureAwait(false);
#endif

                // Write the message
#if NET
                await _messageBuffer.WriteAsync(_serializationBuffer.GetBuffer().AsMemory(0, (int)_serializationBuffer.Position), cancellationToken).ConfigureAwait(false);
#else
                await _messageBuffer.WriteAsync(_serializationBuffer.GetBuffer(), 0, (int)_serializationBuffer.Position, cancellationToken).ConfigureAwait(false);
#endif

                // Send the message
                try
                {
#if NET
#pragma warning disable CA1416 // Validate platform compatibility
                    await _namedPipeServerStream.WriteAsync(_messageBuffer.GetBuffer().AsMemory(0, (int)_messageBuffer.Position), cancellationToken).ConfigureAwait(false);
#pragma warning restore CA1416
#else
                    await _namedPipeServerStream.WriteAsync(_messageBuffer.GetBuffer(), 0, (int)_messageBuffer.Position, cancellationToken).ConfigureAwait(false);
#endif
#pragma warning disable CA1416 // Validate platform compatibility
                    await _namedPipeServerStream.FlushAsync(cancellationToken).ConfigureAwait(false);
#pragma warning restore CA1416
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

    // For compatibility only.
    // Old versions of MTP used to have this overload without IEnvironment.
    // Extensions (e.g, TRX) calls into this overload.
    // If core MTP is updated, but old version of TRX is still used, it will try to call this overload at runtime.
    // Without it, MissingMethodException will be thrown at runtime.
    public static PipeNameDescription GetPipeName(string name)
    {
        if (!IsUnix)
        {
            return new PipeNameDescription($"testingplatform.pipe.{name.Replace('\\', '.')}");
        }

        // Similar to https://github.com/dotnet/roslyn/blob/99bf83c7bc52fa1ff27cf792db38755d5767c004/src/Compilers/Shared/NamedPipeUtil.cs#L26-L42
        return new PipeNameDescription(Path.Combine("/tmp", name));
    }

    // For compatibility only.
    // Old versions of MTP used to have this overload without IEnvironment.
    // Extensions (e.g, TRX) calls into this overload.
    // If core MTP is updated, but old version of TRX is still used, it will try to call this overload at runtime.
    // Without it, MissingMethodException will be thrown at runtime.
    public static PipeNameDescription GetPipeName(string name, IEnvironment _)
        => GetPipeName(name);

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
            ApplicationStateGuard.Ensure(_loopTask is not null);

            // To close gracefully we need to ensure that the client closed the stream line 103.
            if (!_loopTask.Wait(TimeoutHelper.DefaultHangTimeSpanTimeout))
            {
                throw new InvalidOperationException(string.Format(
                    CultureInfo.InvariantCulture,
                    PlatformResources.InternalLoopAsyncDidNotExitSuccessfullyErrorMessage,
                    nameof(InternalLoopAsync)));
            }
        }

        _namedPipeServerStream.Dispose();

        _disposed = true;
    }

#if NET
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
            ApplicationStateGuard.Ensure(_loopTask is not null);

            try
            {
                // To close gracefully we need to ensure that the client closed the stream line 103.
                await _loopTask.WaitAsync(TimeoutHelper.DefaultHangTimeSpanTimeout, _cancellationToken).ConfigureAwait(false);
            }
            catch (TimeoutException)
            {
                throw new InvalidOperationException(string.Format(CultureInfo.InvariantCulture, PlatformResources.InternalLoopAsyncDidNotExitSuccessfullyErrorMessage, nameof(InternalLoopAsync)));
            }
        }

        _namedPipeServerStream.Dispose();

        _disposed = true;
    }
#endif
}
