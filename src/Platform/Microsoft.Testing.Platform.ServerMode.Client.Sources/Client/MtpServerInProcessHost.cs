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

    private int _disposed;

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
    }

    /// <inheritdoc />
    public MtpJsonRpcConnection Connection { get; }

    /// <summary>
    /// Gets the id of the process the application runs in. The application is hosted in the caller's own
    /// process, so this is always the current process id.
    /// </summary>
    public int ProcessId => MtpServerConnector.GetCurrentProcessId();

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

            if (serverTask is not null)
            {
                // The launch is being abandoned, so ask the callback to stop straight away rather than first
                // spending the full shutdown timeout on a graceful wait: there is no connected transport whose
                // closure could signal it, and the caller (often a canceling one) is waiting on this unwind.
                SafeCancel(serverCancellation!, logger);
                await ShutdownServerAsync(serverTask, serverCancellation!, options.ServerShutdownTimeout, logger).ConfigureAwait(false);
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
    /// </remarks>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 1)
        {
            return;
        }

        // Close the transport first. A server-mode application exits its message loop when the client's
        // connection reaches EOF, so this is the graceful stop signal even for a callback that ignores the
        // cancellation token (the common case: TestApplication.RunAsync takes none).
        Connection.Dispose();
        SafeDispose(_client, _logger, "Disposing the accepted client socket");
        MtpServerConnector.SafeStop(_listener, _logger);

        // Run the bounded wait off the caller's synchronization context: a host that disposes from a UI
        // thread must not deadlock against a continuation that wants that same thread.
        Task.Run(() => ShutdownServerAsync(_serverTask, _serverCancellation, _shutdownTimeout, _logger))
            .GetAwaiter()
            .GetResult();

        SafeDispose(_serverCancellation, _logger, "Disposing the server cancellation source");
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
                "The in-process Microsoft.Testing.Platform application was canceled before connecting back.");
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
    private static async Task ShutdownServerAsync(
        Task<int> serverTask,
        CancellationTokenSource serverCancellation,
        TimeSpan shutdownTimeout,
        IMtpClientLogger logger)
    {
        if (!await MtpServerConnector.WaitBoundedAsync(serverTask, shutdownTimeout).ConfigureAwait(false))
        {
            logger.SafeLog(
                MtpClientLogLevel.Debug,
                $"The in-process MTP application did not stop within {shutdownTimeout.TotalSeconds:N0}s of the transport closing; requesting cancellation.");
            SafeCancel(serverCancellation, logger);

            if (!await MtpServerConnector.WaitBoundedAsync(serverTask, CancellationGrace).ConfigureAwait(false))
            {
                logger.SafeLog(
                    MtpClientLogLevel.Warning,
                    $"The in-process MTP application is still running {CancellationGrace.TotalSeconds:N0}s after cancellation was requested; abandoning it.");
            }
        }

        MtpServerConnector.ObserveFailure(serverTask, logger, "The in-process MTP application failed");
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
