// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Net.Sockets;

namespace Microsoft.Testing.Platform.ServerMode.Client;

/// <summary>
/// Hosts a Microsoft.Testing.Platform (MTP) application in the caller's own process and owns the resulting
/// <see cref="MtpJsonRpcConnection"/>.
/// </summary>
/// <remarks>
/// This is the embedded counterpart of <see cref="MtpServerProcess"/>, for hosts that cannot spawn a child
/// process (MAUI, Android/iOS test apps, single-process tooling). The client still opens the loopback TCP
/// listener and still speaks the real server-mode wire protocol; only the "start the application" step is
/// delegated to a caller-supplied callback, which receives the complete server-mode argument array and is
/// expected to build and run an MTP <c>TestApplication</c> with it.
/// </remarks>
internal sealed class MtpServerInProcessHost : IMtpServerHost
{
    /// <summary>
    /// Extra bounded wait granted after the server callback is asked to cancel, on top of
    /// <see cref="MtpServerClientOptions.ServerShutdownTimeout"/>.
    /// </summary>
    private static readonly TimeSpan CancellationGrace = TimeSpan.FromSeconds(5);

    private readonly TcpListener _listener;
    private readonly TcpClient _client;
    private readonly Task<int> _serverTask;
    private readonly CancellationTokenSource _serverCancellation;
    private readonly TimeSpan _shutdownTimeout;
    private readonly IMtpClientLogger _logger;
    private readonly object _shutdownLock = new();

    private Task? _shutdown;

    /// <summary>
    /// The exit code captured during teardown, boxed so reads and writes are atomic on every target platform.
    /// </summary>
    private object? _capturedExitCode;

    private MtpServerInProcessHost(
        TcpListener listener,
        TcpClient client,
        MtpJsonRpcConnection connection,
        Task<int> serverTask,
        CancellationTokenSource serverCancellation,
        TimeSpan shutdownTimeout,
        IMtpClientLogger logger)
    {
        _listener = listener;
        _client = client;
        Connection = connection;
        _serverTask = serverTask;
        _serverCancellation = serverCancellation;
        _shutdownTimeout = shutdownTimeout;
        _logger = logger;

        // Snapshot once: the id cannot change for the lifetime of the host, and resolving it allocates and
        // disposes a Process instance, which a property read must not do on every access.
        ProcessId = MtpServerConnector.GetCurrentProcessId();
    }

    /// <inheritdoc />
    public MtpJsonRpcConnection Connection { get; }

    /// <summary>
    /// Gets the id of the process the application runs in. The application is hosted in the caller's own
    /// process, so this is always the current process id.
    /// </summary>
    public int ProcessId { get; }

    /// <summary>
    /// Gets the exit code the hosted application returned, or <see langword="null"/> while it is still
    /// running (or when it failed or was abandoned rather than returning one).
    /// </summary>
    public int? ExitCode
        => Volatile.Read(ref _capturedExitCode) is int exitCode ? exitCode : null;

    /// <summary>
    /// Starts the application through <paramref name="serverEntryPoint"/> and waits for it to connect back.
    /// </summary>
    /// <param name="serverEntryPoint">
    /// Builds and runs the MTP application. It receives the complete server-mode argument array (which it must
    /// forward verbatim to the test application) plus a token that is canceled when the client gives up
    /// waiting for the application to stop, and returns the application's exit code.
    /// </param>
    /// <param name="options">Client options (connection timeout, shutdown timeout, logger).</param>
    /// <param name="cancellationToken">Cancels the launch and the connection wait.</param>
    /// <exception cref="PlatformNotSupportedException">The current platform has no loopback TCP support.</exception>
    /// <exception cref="MtpServerConnectionClosedException">
    /// The application failed, was canceled, or exited before connecting back, or did not connect back within
    /// <see cref="MtpServerClientOptions.ConnectionTimeout"/>. The callback's own exception, when there is one,
    /// is the inner exception.
    /// </exception>
    /// <remarks>
    /// When the launch fails the callback is abandoned promptly: its token is canceled straight away (there is
    /// no connected transport whose closure could signal it) and it is then given the same fixed 5-second
    /// grace disposal uses, so an unwinding caller never waits for
    /// <see cref="MtpServerClientOptions.ServerShutdownTimeout"/>.
    /// </remarks>
    public static async Task<MtpServerInProcessHost> StartAsync(
        Func<string[], CancellationToken, Task<int>> serverEntryPoint,
        MtpServerClientOptions options,
        CancellationToken cancellationToken)
    {
        if (serverEntryPoint is null)
        {
            throw new ArgumentNullException(nameof(serverEntryPoint));
        }

        if (options is null)
        {
            throw new ArgumentNullException(nameof(options));
        }

        cancellationToken.ThrowIfCancellationRequested();
        IMtpClientLogger logger = options.Logger ?? NullMtpClientLogger.Instance;

        // The server-mode transport is loopback TCP. The browser sandbox has no listening sockets, so fail
        // with a precise diagnostic instead of an opaque socket error: this API does not enable WASM hosting.
        if (MtpClientOperatingSystem.IsBrowser())
        {
            throw new PlatformNotSupportedException(
                "Hosting a Microsoft.Testing.Platform application in process requires a loopback TCP listener, "
                + "which is not available on browser/WASM.");
        }

        if (options.EnvironmentVariables.Count > 0)
        {
            logger.SafeLog(
                MtpClientLogLevel.Warning,
                $"{nameof(MtpServerClientOptions)}.{nameof(MtpServerClientOptions.EnvironmentVariables)} is ignored when the "
                + "application is hosted in process: it shares the caller's environment. Set the variables before starting the host.");
        }

        IMessageFormatter formatter = MtpServerConnector.CreateFormatter();

        TcpListener? listener = null;
        CancellationTokenSource? serverCancellation = null;
        Task<int>? serverTask = null;
        TcpClient? acceptedClient = null;
        try
        {
            listener = MtpServerConnector.StartLoopbackListener(out int port);
            string[] serverArguments = MtpServerConnector.BuildInProcessServerArguments(port);
            logger.SafeLog(
                MtpClientLogLevel.Debug,
                $"Hosting an MTP server in process with '{string.Join(" ", serverArguments)}'.");

            // The token is deliberately NOT linked to cancellationToken: that token scopes the launch, while
            // this one scopes the hosted application's lifetime. Linking them would tear the application down
            // when a caller reuses a request-scoped token for the launch.
            serverCancellation = new CancellationTokenSource();
            serverTask = StartServerAsync(serverEntryPoint, serverArguments, serverCancellation.Token);

            acceptedClient = await MtpServerConnector.AcceptAsync(
                listener,
                () => TryGetServerStoppedFailure(serverTask),
                () => new MtpServerConnectionClosedException(
                    $"The in-process Microsoft.Testing.Platform application did not connect back within {options.ConnectionTimeout.TotalSeconds:N0}s. "
                    + "Make sure the callback forwards the supplied server-mode arguments to the test application."),
                options.ConnectionTimeout,
                serverTask,
                cancellationToken).ConfigureAwait(false);

            MtpJsonRpcConnection connection = MtpServerConnector.CreateConnection(acceptedClient, formatter, logger);
            return new MtpServerInProcessHost(
                listener,
                acceptedClient,
                connection,
                serverTask,
                serverCancellation,
                options.ServerShutdownTimeout,
                logger);
        }
        catch
        {
            // Tear down whatever was already created. Every step is individually guarded and reported through
            // the logger so a teardown failure can never replace the primary launch failure rethrown below.
            SafeDispose(acceptedClient, logger, "Disposing the accepted client socket");

            if (listener is not null)
            {
                MtpServerConnector.SafeStop(listener, logger);
            }

            if (serverTask is not null
                && !await ShutdownServerAsync(serverTask, serverCancellation!, TimeSpan.Zero, logger).ConfigureAwait(false))
            {
                // The launch is being abandoned, so skip the graceful wait entirely: there is no connected
                // transport whose closure could signal the callback, and the caller (often a canceling one) is
                // waiting on this unwind. A zero graceful timeout goes straight to cancel-then-grace.
                // The callback is still running and still holds the token; disposing its source now would
                // turn a clean abandonment into an ObjectDisposedException inside the caller's own code.
                throw;
            }

            SafeDispose(serverCancellation, logger, "Disposing the server cancellation source");
            throw;
        }
    }

    /// <summary>
    /// Closes the transport and waits for the hosted application to finish.
    /// </summary>
    /// <remarks>
    /// The wait is bounded: at most <see cref="MtpServerClientOptions.ServerShutdownTimeout"/> after the
    /// transport is closed, plus a further 5 seconds after the callback's token is canceled. If the callback
    /// is still running after that it is abandoned (its failure is still observed and logged) rather than
    /// hanging the caller. Disposal never throws, so it cannot mask a failure that is already propagating.
    /// <para>
    /// The wait happens synchronously on the calling thread. Prefer <see cref="ShutdownAsync"/> on platforms
    /// with a responsiveness watchdog; disposing afterwards then returns immediately.
    /// </para>
    /// </remarks>
    public void Dispose()
    {
        // Joins the one teardown rather than starting a second, so a Dispose that races or follows
        // ShutdownAsync still returns only once the application has actually stopped.
#pragma warning disable VSTHRD002 // Synchronously waiting on tasks - this IS the synchronous disposal path; ShutdownAsync is the awaitable one.
        try
        {
            StartShutdownAsync().GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            // ShutdownAsync surfaces the callback's own post-connect failure. Dispose reports it instead so it
            // cannot replace an exception already unwinding through a using statement or finally block.
            _logger.SafeLog(MtpClientLogLevel.Error, $"The in-process MTP application failed during disposal: {ex}");
        }
#pragma warning restore VSTHRD002
    }

    /// <inheritdoc />
    public Task ShutdownAsync()
        => StartShutdownAsync();

    /// <summary>
    /// Returns the single teardown task, starting it on the first call.
    /// </summary>
    /// <remarks>
    /// Teardown runs on the thread pool so the synchronous <see cref="Dispose"/> wait cannot deadlock against
    /// a continuation that wants the caller's synchronization context (a UI thread, typically). Because every
    /// caller receives the same task, <see cref="Dispose"/> and <see cref="ShutdownAsync"/> are idempotent
    /// with respect to each other and to themselves.
    /// <para>
    /// <see cref="Task.Run(Func{Task})"/> captures the ambient execution context, so the connection's
    /// read-loop <see cref="AsyncLocal{T}"/> marker still flows into the teardown. Disposing from inside a
    /// notification handler therefore continues to skip the connection's read-loop self-wait instead of
    /// stalling for its shutdown timeout.
    /// </para>
    /// </remarks>
    private Task StartShutdownAsync()
    {
        lock (_shutdownLock)
        {
            return _shutdown ??= Task.Run(ShutdownCoreAsync);
        }
    }

    private async Task ShutdownCoreAsync()
    {
        CancellationToken serverCancellationToken = _serverCancellation.Token;
        try
        {
            // Start the application timeout before disposing the connection. Connection.Dispose closes the
            // transport promptly but can then spend up to five seconds waiting for a blocked read-loop handler;
            // overlapping that wait keeps the complete teardown within shutdown timeout plus cancellation grace.
            Task<bool> serverShutdown = ShutdownServerAsync(_serverTask, _serverCancellation, _shutdownTimeout, _logger);

            // Close the transport first. A server-mode application exits its message loop when the client's
            // connection reaches EOF, so this is the graceful stop signal even for a callback that ignores the
            // cancellation token (the common case: TestApplication.RunAsync takes none).
            Connection.Dispose();
            SafeDispose(_client, _logger, "Disposing the accepted client socket");
            MtpServerConnector.SafeStop(_listener, _logger);

            bool stopped = await serverShutdown.ConfigureAwait(false);

            if (_serverTask.Status == TaskStatus.RanToCompletion)
            {
                // Reading Result cannot block here: the status check already established the task completed
                // successfully. Awaiting instead would be wrong, because a faulted or abandoned task must not
                // throw out of a teardown that is documented never to throw.
#pragma warning disable VSTHRD103 // Result synchronously blocks
                Volatile.Write(ref _capturedExitCode, _serverTask.Result);
#pragma warning restore VSTHRD103
            }

            if (stopped)
            {
                // Only safe once nothing holds the token any more: an abandoned callback (or a cancellation
                // registration still executing) would see token.WaitHandle / CreateLinkedTokenSource throw
                // inside the caller's own code.
                SafeDispose(_serverCancellation, _logger, "Disposing the server cancellation source");
            }
        }
        catch (Exception ex)
        {
            // Teardown infrastructure failures are reported rather than replacing the callback's own failure.
            _logger.SafeLog(MtpClientLogLevel.Error, $"Tearing down the in-process MTP application threw: {ex}");
        }

        // ShutdownAsync is the explicit failure-observing path. Rethrow a callback fault or independent
        // cancellation after every owned resource has been handled; cancellation requested by this teardown
        // is expected, and an abandoned callback is observed by ShutdownServerAsync's continuation instead.
        if (_serverTask.IsFaulted)
        {
            _ = await _serverTask.ConfigureAwait(false);
        }
        else if (_serverTask.IsCanceled)
        {
            try
            {
                _ = await _serverTask.ConfigureAwait(false);
            }
            catch (OperationCanceledException ex)
                when (ex.CancellationToken == serverCancellationToken || serverCancellationToken.IsCancellationRequested)
            {
                // The callback honored cancellation after teardown requested it, either directly through the
                // supplied token or through a linked token that carries a different identity.
            }
        }
    }

    /// <summary>
    /// Invokes the callback on the thread pool so the launch never blocks the caller, and turns a synchronous
    /// throw (or a null task) into a faulted task the connect race can report.
    /// </summary>
    private static Task<int> StartServerAsync(
        Func<string[], CancellationToken, Task<int>> serverEntryPoint,
        string[] serverArguments,
        CancellationToken serverCancellationToken)
        => Task.Run(
            () => serverEntryPoint(serverArguments, serverCancellationToken)
                ?? throw new MtpServerClientException("The in-process server callback returned a null task."),
            CancellationToken.None);

    /// <summary>
    /// Reports why the hosted application can no longer connect back, or <see langword="null"/> while it is
    /// still running.
    /// </summary>
    private static Exception? TryGetServerStoppedFailure(Task<int> serverTask)
    {
        if (!serverTask.IsCompleted)
        {
            return null;
        }

        if (serverTask.IsCanceled)
        {
            return new MtpServerConnectionClosedException(
                "The in-process Microsoft.Testing.Platform application was canceled before connecting back.",
                new TaskCanceledException(serverTask));
        }

        // Unwrap the AggregateException so the callback's own exception is the inner exception a caller sees.
        if (serverTask.Exception is { } exception)
        {
            Exception failure = exception.InnerExceptions.Count == 1 ? exception.InnerExceptions[0] : exception;
            return new MtpServerConnectionClosedException(
                "The in-process Microsoft.Testing.Platform application failed before connecting back.",
                failure);
        }

        return new MtpServerConnectionClosedException(
            $"The in-process Microsoft.Testing.Platform application exited with code {serverTask.Result} before connecting back. "
            + "Make sure the callback forwards the supplied server-mode arguments to the test application.");
    }

    /// <summary>
    /// Waits for the hosted application to finish, escalating to cancellation and then to abandonment. Never
    /// throws: the application's own failure is observed and logged so it cannot mask the caller's failure.
    /// </summary>
    /// <param name="serverTask">The running callback.</param>
    /// <param name="serverCancellation">The cancellation source handed to the callback.</param>
    /// <param name="gracefulTimeout">
    /// How long the callback is given to stop on its own before its token is canceled.
    /// <see cref="TimeSpan.Zero"/> skips the graceful wait, which is what an abandoned launch wants: nothing
    /// has been connected, so there is no transport closure for the callback to observe.
    /// </param>
    /// <param name="logger">Sink for the shutdown diagnostics.</param>
    /// <returns>
    /// <see langword="true"/> when the callback finished and no cancellation registration is still running,
    /// <see langword="false"/> otherwise. The caller uses this to decide whether it may dispose the
    /// cancellation source: anything still holding the token would see <c>token.WaitHandle</c> or
    /// <c>CancellationTokenSource.CreateLinkedTokenSource(token)</c> throw
    /// <see cref="ObjectDisposedException"/> inside the caller's own code.
    /// </returns>
    private static async Task<bool> ShutdownServerAsync(
        Task<int> serverTask,
        CancellationTokenSource serverCancellation,
        TimeSpan gracefulTimeout,
        IMtpClientLogger logger)
    {
        bool stopped = true;
        if (!await MtpServerConnector.WaitBoundedAsync(serverTask, gracefulTimeout).ConfigureAwait(false))
        {
            if (gracefulTimeout > TimeSpan.Zero)
            {
                logger.SafeLog(
                    MtpClientLogLevel.Debug,
                    $"The in-process MTP application did not stop within {gracefulTimeout.TotalSeconds:N0}s of the transport closing; requesting cancellation.");
            }

            // Cancel() runs registrations synchronously on the calling thread, so a caller registration that
            // blocks would stop the grace below from ever starting and make this "bounded" wait unbounded.
            // Kick it off separately and start the grace regardless.
            var cancelling = Task.Run(() => SafeCancel(serverCancellation, logger));
            MtpServerConnector.ObserveFailure(cancelling, logger, "Canceling the in-process MTP application failed");

            if (!await MtpServerConnector.WaitBoundedAsync(serverTask, CancellationGrace).ConfigureAwait(false))
            {
                stopped = false;
                logger.SafeLog(
                    MtpClientLogLevel.Warning,
                    $"The in-process MTP application is still running {CancellationGrace.TotalSeconds:N0}s after cancellation was requested; abandoning it.");
            }
            else if (!cancelling.IsCompleted)
            {
                // A cancellation registration is still executing and still holds the token. Report the source
                // as unsafe to dispose: leaking one CancellationTokenSource beats a use-after-dispose.
                stopped = false;
            }
        }

        MtpServerConnector.ObserveFailure(serverTask, logger, "The in-process MTP application failed");
        return stopped;
    }

    private static void SafeCancel(CancellationTokenSource cancellationTokenSource, IMtpClientLogger logger)
    {
        try
        {
            cancellationTokenSource.Cancel();
        }
        catch (Exception ex) when (ex is ObjectDisposedException or AggregateException)
        {
            logger.SafeLog(MtpClientLogLevel.Debug, $"Canceling the in-process MTP application threw: {ex}");
        }
    }

    private static void SafeDispose(IDisposable? disposable, IMtpClientLogger logger, string description)
    {
        if (disposable is null)
        {
            return;
        }

        try
        {
            disposable.Dispose();
        }
        catch (Exception ex) when (ex is SocketException or ObjectDisposedException or IOException)
        {
            logger.SafeLog(MtpClientLogLevel.Debug, $"{description} threw: {ex}");
        }
    }
}
