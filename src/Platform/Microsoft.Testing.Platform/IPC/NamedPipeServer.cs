// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.IO.Pipes;

using Microsoft.CodeAnalysis;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.Logging;

using Microsoft.Testing.Platform.Resources;

namespace Microsoft.Testing.Platform.IPC;

[Embedded]
#if !MTP_MSBUILD_TASKS
[UnsupportedOSPlatform("browser")]
#endif
internal sealed class NamedPipeServer : NamedPipeBase, IServer
{
    private const PipeOptions AsyncCurrentUserPipeOptions = PipeOptions.Asynchronous
#if NET
        | PipeOptions.CurrentUserOnly
#endif
        ;

    private static bool IsUnix => Path.DirectorySeparatorChar == '/';

    private readonly Func<IRequest, Task<IResponse>> _callback;
    private readonly IEnvironment _environment;
    private readonly NamedPipeServerStream _namedPipeServerStream;
    private readonly ILogger _logger;
    private readonly ITask _task;
    private readonly CancellationToken _cancellationToken;
    private Task? _loopTask;
    private bool _disposed;

    public NamedPipeServer(
        string name,
        Func<IRequest, Task<IResponse>> callback,
        IEnvironment environment,
        ILogger logger,
        ITask task,
        CancellationToken cancellationToken)
        : this(GetPipeName(name), callback, environment, logger, task, cancellationToken)
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
        if (pipeNameDescription is null)
        {
            throw new ArgumentNullException(nameof(pipeNameDescription));
        }

        _namedPipeServerStream = new((PipeName = pipeNameDescription).Name, PipeDirection.InOut, maxNumberOfServerInstances, PipeTransmissionMode.Byte, AsyncCurrentUserPipeOptions);
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
        await _namedPipeServerStream.WaitForConnectionAsync(cancellationToken).ConfigureAwait(false);
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
        while (true)
        {
            // Read the next request; null means the client disconnected
            object? requestObject = await ReadNextMessageAsync(_namedPipeServerStream, cancellationToken).ConfigureAwait(false);
            if (requestObject is null)
            {
                await _logger.LogDebugAsync($"Client disconnected from pipe '{PipeName.Name}', exiting read loop").ConfigureAwait(false);
                return;
            }

            // Dispatch the request and obtain the response
            IResponse response = await _callback((IRequest)requestObject).ConfigureAwait(false);

            // Serialize and send the response
            INamedPipeSerializer responseNamedPipeSerializer = GetSerializer(response.GetType());
            bool clientDisconnected = false;
            try
            {
                await WriteMessageAsync(_namedPipeServerStream, responseNamedPipeSerializer, response, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is IOException or ObjectDisposedException)
            {
                // The client disconnected while we were writing the reply. Treat it as a graceful disconnect
                // (symmetric with the read-side EOF handling above) so the server loop exits without crashing
                // the host.
                await _logger.LogDebugAsync($"Pipe {PipeName.Name} broken while writing reply; treating as client disconnect: {ex.Message}").ConfigureAwait(false);
                clientDisconnected = true;
            }

            if (clientDisconnected)
            {
                return;
            }
        }
    }

    public static PipeNameDescription GetPipeName(string name)
    {
        if (!IsUnix)
        {
            return new PipeNameDescription($"testingplatform.pipe.{name.Replace('\\', '.')}");
        }

        // Similar to https://github.com/dotnet/roslyn/blob/99bf83c7bc52fa1ff27cf792db38755d5767c004/src/Compilers/Shared/NamedPipeUtil.cs#L26-L42
        return new PipeNameDescription(Path.Combine("/tmp", name));
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
            ApplicationStateGuard.Ensure(_loopTask is not null);

            // To close gracefully we need to ensure that the client closed the stream in the InternalLoopAsync method (there is comment `// The client has disconnected`).
            if (!_loopTask.Wait(TimeoutHelper.DefaultHangTimeSpanTimeout))
            {
                _logger.LogError($"NamedPipeServer.Dispose: '{nameof(InternalLoopAsync)}' for pipe '{PipeName.Name}' did not complete within {TimeoutHelper.DefaultHangTimeSpanTimeout}. WasConnected={WasConnected}, LoopTaskStatus={_loopTask.Status}.");
                throw new InvalidOperationException(string.Format(
                    CultureInfo.InvariantCulture,
                    PlatformResources.InternalLoopAsyncDidNotExitSuccessfullyErrorMessage,
                    nameof(InternalLoopAsync)));
            }
        }

        _namedPipeServerStream.Dispose();
        DisposeBuffers();

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
                // To close gracefully we need to ensure that the client closed the stream in the InternalLoopAsync method (there is comment `// The client has disconnected`).
                await _loopTask.WaitAsync(TimeoutHelper.DefaultHangTimeSpanTimeout, _cancellationToken).ConfigureAwait(false);
            }
            catch (TimeoutException)
            {
                await _logger.LogErrorAsync($"NamedPipeServer.DisposeAsync: '{nameof(InternalLoopAsync)}' for pipe '{PipeName.Name}' did not complete within {TimeoutHelper.DefaultHangTimeSpanTimeout}. WasConnected={WasConnected}, LoopTaskStatus={_loopTask.Status}.").ConfigureAwait(false);
                throw new InvalidOperationException(string.Format(
                    CultureInfo.InvariantCulture,
                    PlatformResources.InternalLoopAsyncDidNotExitSuccessfullyErrorMessage,
                    nameof(InternalLoopAsync)));
            }
        }

        _namedPipeServerStream.Dispose();
        DisposeBuffers();

        _disposed = true;
    }
#endif
}
