// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.IO.Pipes;

using Microsoft.CodeAnalysis;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.Logging;

namespace Microsoft.Testing.Platform.IPC;

[Embedded]
#if !MTP_MSBUILD_TASKS
[UnsupportedOSPlatform("browser")]
#endif
internal sealed partial class NamedPipeServer : NamedPipeConnectionBase, IServer
{
    private const PipeOptions AsyncCurrentUserPipeOptions = PipeOptions.Asynchronous
#if NET
        | PipeOptions.CurrentUserOnly
#endif
        ;

    private static bool IsUnix => Path.DirectorySeparatorChar == '/';

    // Maximum length, in bytes, of the path stored in sockaddr_un.sun_path for a Unix domain socket.
    // The smallest limit across supported platforms is macOS' 104 bytes (Linux allows 108); we use the
    // smaller value minus one for the NUL terminator so the resolved path stays portable.
    internal const int MaxUnixDomainSocketPathLengthInBytes = 104 - 1;

    private readonly Func<IRequest, Task<IResponse>> _callback;
    private readonly IEnvironment _environment;
    private readonly NamedPipeServerStream _namedPipeServerStream;
    private readonly ILogger _logger;
    private readonly ITask _task;
    private readonly CancellationToken _cancellationToken;
#if NET9_0_OR_GREATER
    private readonly Lock _lifecycleSync = new();
#else
    private readonly object _lifecycleSync = new();
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
        : this(GetPipeName(name), callback, environment, logger, task, cancellationToken)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="NamedPipeServer"/> class, optionally authorizing a set
    /// of security identities on the pipe in addition to the creating user.
    /// </summary>
    /// <param name="name">The friendly pipe name.</param>
    /// <param name="callback">The request handler.</param>
    /// <param name="environment">The environment abstraction.</param>
    /// <param name="logger">The logger.</param>
    /// <param name="task">The task abstraction.</param>
    /// <param name="authorizedSecurityIdentities">
    /// Security identities that must additionally be able to connect, or <see langword="null"/>/empty for
    /// the default behavior.
    /// </param>
    /// <param name="cancellationToken">The cancellation token.</param>
    public NamedPipeServer(
        string name,
        Func<IRequest, Task<IResponse>> callback,
        IEnvironment environment,
        ILogger logger,
        ITask task,
        IReadOnlyList<string>? authorizedSecurityIdentities,
        CancellationToken cancellationToken)
        : this(GetPipeName(name), callback, environment, logger, task, maxNumberOfServerInstances: 1, authorizedSecurityIdentities, cancellationToken)
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
        : this(pipeNameDescription, callback, environment, logger, task, maxNumberOfServerInstances, authorizedSecurityIdentities: null, cancellationToken)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="NamedPipeServer"/> class, optionally authorizing a set
    /// of security identities on the pipe in addition to the creating user.
    /// </summary>
    /// <param name="pipeNameDescription">The pipe name.</param>
    /// <param name="callback">The request handler.</param>
    /// <param name="environment">The environment abstraction.</param>
    /// <param name="logger">The logger.</param>
    /// <param name="task">The task abstraction.</param>
    /// <param name="maxNumberOfServerInstances">The maximum number of concurrent server instances.</param>
    /// <param name="authorizedSecurityIdentities">
    /// Security identities that must additionally be able to connect, or <see langword="null"/>/empty for
    /// the default behavior. Every entry must satisfy
    /// <see cref="NamedPipeServerSecurity.IsAuthorizableSandboxedApplicationIdentity"/> — on Windows the only identity
    /// that may be added is that of a single sandboxed application, which the OS expresses as an
    /// AppContainer package SID. The caller is responsible for filtering and reporting rejected entries.
    /// Ignored on operating systems that cannot express such an identity.
    /// </param>
    /// <param name="cancellationToken">The cancellation token.</param>
    public NamedPipeServer(
        PipeNameDescription pipeNameDescription,
        Func<IRequest, Task<IResponse>> callback,
        IEnvironment environment,
        ILogger logger,
        ITask task,
        int maxNumberOfServerInstances,
        IReadOnlyList<string>? authorizedSecurityIdentities,
        CancellationToken cancellationToken)
    {
        if (pipeNameDescription is null)
        {
            throw new ArgumentNullException(nameof(pipeNameDescription));
        }

        PipeName = RequiresExplicitSecurity(authorizedSecurityIdentities)
            ? new PipeNameDescription(NamedPipeServerSecurity.GetPipeNameForSandboxedApplication(pipeNameDescription.Name))
            : pipeNameDescription;
        _namedPipeServerStream = CreateServerStream(PipeName.Name, maxNumberOfServerInstances, authorizedSecurityIdentities);
        _callback = callback;
        _environment = environment;
        _logger = logger;
        _task = task;
        _cancellationToken = cancellationToken;
    }

    public PipeNameDescription PipeName { get; }

#if IS_MTP_UNIT_TESTS
    internal NamedPipeServerStream GetServerStream() => _namedPipeServerStream;
#endif

    public bool WasConnected { get; private set; }

    public async Task WaitConnectionAsync(CancellationToken cancellationToken)
    {
        // NOTE: _cancellationToken field is usually the "test session" cancellation token, while the parameter
        // may include a connection timeout. Once the connection is accepted, always schedule the loop: using the
        // parameter for Task.Run creates a race where cancellation between acceptance and scheduling leaves a
        // canceled loop task, and disposal then mistakes that for a loop failure. InternalLoopAsync observes the
        // server lifetime token for its whole lifetime.
        // Stryker disable once Boolean: continuation scheduling does not change the connection handshake.
        await _logger.LogDebugAsync($"Waiting for connection for the pipe name {PipeName.Name}").ConfigureAwait(false);
        // Stryker disable once Boolean: continuation scheduling does not change connection acceptance.
        await _namedPipeServerStream.WaitForConnectionAsync(cancellationToken).ConfigureAwait(false);
        lock (_lifecycleSync)
        {
            if (_disposed)
            {
                return;
            }

            _loopTask = _task.Run(
                async () =>
                {
                    try
                    {
                        // Stryker disable once Boolean: continuation scheduling does not change loop completion.
                        await InternalLoopAsync(_cancellationToken).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException ex) when (ex.CancellationToken == _cancellationToken)
                    {
                        // We are being canceled, so we don't need to wait anymore
                    }
                    catch (Exception ex) when (
                        _cancellationToken.IsCancellationRequested
                        && ex is OperationCanceledException or IOException or ObjectDisposedException)
                    {
                        // .NET Framework does not reliably interrupt a pending pipe read when only the cancellation
                        // token is canceled. Disposal closes the stream to release that read; the resulting transport
                        // exception is expected shutdown rather than a server failure.
                    }
                    catch (Exception ex)
                    {
                        await _logger.LogErrorAsync($"Exception on pipe: {PipeName.Name}", ex).ConfigureAwait(false);
                        _environment.FailFast($"[NamedPipeServer] Unhandled exception:{_environment.NewLine}{ex}", ex);
                    }
                }, CancellationToken.None);
            WasConnected = true;
        }

        // Stryker disable once Boolean: continuation scheduling does not change the connection diagnostic.
        await _logger.LogDebugAsync($"Client connected to {PipeName.Name}").ConfigureAwait(false);
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
            // Stryker disable once Boolean: continuation scheduling does not change the received request.
            object? requestObject = await ReadNextMessageAsync(_namedPipeServerStream, cancellationToken).ConfigureAwait(false);
            if (requestObject is null)
            {
                // Stryker disable once Boolean: continuation scheduling does not change the disconnect diagnostic.
                await _logger.LogDebugAsync($"Client disconnected from pipe '{PipeName.Name}', exiting read loop").ConfigureAwait(false);
                return;
            }

            // Dispatch the request and obtain the response
            // Stryker disable once Boolean: continuation scheduling does not change the callback response.
            IResponse response = await _callback((IRequest)requestObject).ConfigureAwait(false);

            // Serialize and send the response
            INamedPipeSerializer responseNamedPipeSerializer = GetSerializer(response.GetType());
            bool transportClosed = false;
            try
            {
                // Stryker disable once Boolean: continuation scheduling does not change the response bytes.
                await WriteMessageAsync(_namedPipeServerStream, responseNamedPipeSerializer, response, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is IOException or ObjectDisposedException)
            {
                // The pipe closed while we were writing the reply. Treat transport closure as graceful
                // (symmetric with the read-side EOF handling above) so the server loop exits without crashing.
                await TryLogDebugAsync($"Pipe '{PipeName.Name}' closed while writing reply; exiting server loop.").ConfigureAwait(false);
                transportClosed = true;
            }

            if (transportClosed)
            {
                return;
            }
        }
    }

    private async Task TryLogDebugAsync(string message)
    {
        Task loggingTask = _task.Run(
            async () =>
            {
                try
                {
                    await _logger.LogDebugAsync(message).ConfigureAwait(false);
                }
                catch (Exception)
                {
                    // A graceful disconnect must remain graceful even when a logging provider fails.
                }
            },
            CancellationToken.None);

        await Task.WhenAny(loggingTask, Task.Delay(TimeSpan.FromSeconds(1))).ConfigureAwait(false);
    }
}
