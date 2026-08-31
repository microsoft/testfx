// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Net;
using System.Net.Sockets;

namespace Microsoft.Testing.Platform.ServerMode.Client;

/// <summary>
/// The transport setup shared by every way of launching a Microsoft.Testing.Platform (MTP) application in
/// JSON-RPC server mode: registering the client serializers before the formatter is built, binding the
/// loopback listener the application dials back to, racing the accept against server failure / caller
/// cancellation / the connection timeout, and wrapping the accepted socket in the platform's own
/// <see cref="TcpMessageHandler"/> plus a <see cref="MtpJsonRpcConnection"/>.
/// </summary>
/// <remarks>
/// <see cref="MtpServerProcess"/> (external process) and <see cref="MtpServerInProcessHost"/> (callback in
/// the caller's own process) differ only in how the server is started and how "the server already stopped"
/// is observed. Everything else lives here so the two launch paths cannot drift apart.
/// </remarks>
internal static class MtpServerConnector
{
    internal const string ServerArgument = "--server";
    internal const string JsonRpcProtocolArgument = "jsonrpc";
    internal const string ClientHostArgument = "--client-host";
    internal const string ClientPortArgument = "--client-port";
    internal const string NoBannerArgument = "--no-banner";

    /// <summary>
    /// The loopback address handed to the server. The listener binds <see cref="IPAddress.Loopback"/>, so the
    /// dotted form is passed explicitly rather than relying on the server's <c>localhost</c> default, which
    /// resolves through DNS.
    /// </summary>
    internal const string LoopbackHost = "127.0.0.1";

    /// <summary>
    /// How often the connect wait re-checks whether the launched server has already stopped, so a server that
    /// dies during startup fails fast instead of blocking for the full connection timeout. It is also the
    /// bounded grace given to a still-pending accept once the server is seen to have stopped.
    /// </summary>
    internal static readonly TimeSpan ServerStoppedPollInterval = TimeSpan.FromMilliseconds(100);

    /// <summary>
    /// Registers the client serializers and creates the message formatter, in that order.
    /// </summary>
    /// <remarks>
    /// The ordering is load-bearing: the .NET System.Text.Json formatter snapshots the registered
    /// serializer/deserializer type sets into its per-type engine at construction time, so a formatter built
    /// before registration would silently miss the client payload types.
    /// </remarks>
    public static IMessageFormatter CreateFormatter()
    {
        SerializerUtilities.RegisterClientSerializers();
        return FormatterUtilities.CreateFormatter();
    }

    /// <summary>
    /// Binds and starts a loopback TCP listener on an OS-assigned ephemeral port.
    /// </summary>
    /// <param name="port">Receives the bound port to hand to the server.</param>
    public static TcpListener StartLoopbackListener(out int port)
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        try
        {
            port = ((IPEndPoint)listener.LocalEndpoint).Port;
        }
        catch
        {
            // Reading the bound endpoint should not fail after a successful Start, but if it does the
            // listener is already holding a port; release it rather than leaking a bound socket.
            listener.Stop();
            throw;
        }

        return listener;
    }

    /// <summary>
    /// Builds the complete server-mode argument array for an application hosted in the caller's own process.
    /// </summary>
    /// <param name="port">The port of the client's loopback listener.</param>
    /// <remarks>
    /// The protocol and host are stated explicitly (rather than relying on the platform's defaults) because
    /// an embedded host forwards this array verbatim to <c>TestApplication.CreateBuilderAsync</c>, where it is
    /// also the documentation of what the client asked for.
    /// </remarks>
    public static string[] BuildInProcessServerArguments(int port)
    =>
    [
        ServerArgument,
        JsonRpcProtocolArgument,
        ClientHostArgument,
        LoopbackHost,
        ClientPortArgument,
        port.ToString(CultureInfo.InvariantCulture),
        NoBannerArgument,
    ];

    /// <summary>
    /// Waits for the launched server to dial back, racing the accept against server failure, caller
    /// cancellation and <paramref name="connectionTimeout"/> so the wait is never unbounded.
    /// </summary>
    /// <param name="listener">The already-started loopback listener.</param>
    /// <param name="tryGetServerStoppedFailure">
    /// Probed on every poll. Returns the exception to throw when the server has already stopped without
    /// connecting back, or <see langword="null"/> while it is still running.
    /// </param>
    /// <param name="createTimeoutFailure">Creates the exception thrown when the connection timeout elapses.</param>
    /// <param name="connectionTimeout">Upper bound on the wait for the server to connect back.</param>
    /// <param name="serverCompletion">
    /// Optional completion signal for the launched server. When supplied it joins the wait so a server that
    /// stops is observed immediately instead of on the next poll tick.
    /// </param>
    /// <param name="cancellationToken">Cancels the wait.</param>
    /// <remarks>
    /// On any failure the still-pending accept is neutralized before the exception propagates, so a late
    /// dial-back cannot leak a connected socket and the accept task can never end up unobserved.
    /// </remarks>
    public static async Task<TcpClient> AcceptAsync(
        TcpListener listener,
        Func<Exception?> tryGetServerStoppedFailure,
        Func<Exception> createTimeoutFailure,
        TimeSpan connectionTimeout,
        Task? serverCompletion,
        CancellationToken cancellationToken)
    {
        Task<TcpClient>? acceptTask = null;
        TcpClient? acceptedClient = null;
        try
        {
#if NET8_0_OR_GREATER
            acceptTask = listener.AcceptTcpClientAsync(cancellationToken).AsTask();
#else
            acceptTask = listener.AcceptTcpClientAsync();
#endif

            var connectStopwatch = Stopwatch.StartNew();
            while (!acceptTask.IsCompleted)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (tryGetServerStoppedFailure() is { } serverStopped)
                {
                    // The server stopped, but it may have dialed back microseconds before stopping. Give the
                    // pending accept one bounded chance so a usable connection is never discarded in favor of
                    // a misleading "stopped before connecting back" failure.
                    _ = await Task.WhenAny(acceptTask, Task.Delay(ServerStoppedPollInterval, CancellationToken.None)).ConfigureAwait(false);
                    if (!acceptTask.IsCompleted)
                    {
                        throw serverStopped;
                    }

                    break;
                }

                if (connectStopwatch.Elapsed >= connectionTimeout)
                {
                    throw createTimeoutFailure();
                }

                var delayTask = Task.Delay(ServerStoppedPollInterval, cancellationToken);
                _ = serverCompletion is null
                    ? await Task.WhenAny(acceptTask, delayTask).ConfigureAwait(false)
                    : await Task.WhenAny(acceptTask, delayTask, serverCompletion).ConfigureAwait(false);
            }

            cancellationToken.ThrowIfCancellationRequested();
            acceptedClient = await acceptTask.ConfigureAwait(false);
            acceptedClient.NoDelay = true;
            return acceptedClient;
        }
        catch
        {
            if (acceptedClient is null && acceptTask is not null)
            {
                NeutralizePendingAccept(acceptTask);
            }

            acceptedClient?.Dispose();
            throw;
        }
    }

    /// <summary>
    /// Wraps an accepted socket in the platform's own framing handler and the client's JSON-RPC connection.
    /// </summary>
    /// <remarks>
    /// The read loop is intentionally NOT started: the owner attaches its notification and server-request
    /// handlers first and only then calls <see cref="MtpJsonRpcConnection.Start"/>, so no server-to-client
    /// message can slip past before the handlers are attached.
    /// </remarks>
    public static MtpJsonRpcConnection CreateConnection(TcpClient client, IMessageFormatter formatter, IMtpClientLogger logger)
    {
        // A NetworkStream is duplex, so the same stream serves the read and write directions.
        NetworkStream stream = client.GetStream();
        var handler = new TcpMessageHandler(client, stream, stream, formatter);
        return new MtpJsonRpcConnection(handler, logger);
    }

    /// <summary>
    /// Stops the listener, logging rather than propagating a socket failure so teardown never masks the
    /// failure that triggered it.
    /// </summary>
    public static void SafeStop(TcpListener listener, IMtpClientLogger logger)
    {
        try
        {
            listener.Stop();
        }
        catch (SocketException ex)
        {
            logger.SafeLog(MtpClientLogLevel.Debug, $"Stopping the TCP listener threw: {ex}");
        }
    }

    /// <summary>
    /// Gets the id of the process the client itself runs in.
    /// </summary>
    public static int GetCurrentProcessId()
    {
        using var current = Process.GetCurrentProcess();
        return current.Id;
    }

    /// <summary>
    /// Waits for <paramref name="task"/> for at most <paramref name="timeout"/> and reports whether it
    /// completed. Never throws, and never propagates the task's own failure: the caller decides how to report
    /// it, which keeps a shutdown failure from masking the primary one.
    /// </summary>
    public static async Task<bool> WaitBoundedAsync(Task task, TimeSpan timeout)
    {
        if (task.IsCompleted)
        {
            return true;
        }

        Task completed = await Task.WhenAny(task, Task.Delay(timeout, CancellationToken.None)).ConfigureAwait(false);
        return completed == task;
    }

    /// <summary>
    /// Reads a completed task's failure so it is observed (never surfacing as an
    /// <c>UnobservedTaskException</c>) and reports it through the logger instead of throwing.
    /// </summary>
    public static void ObserveFailure(Task task, IMtpClientLogger logger, string description)
    {
        if (!task.IsCompleted)
        {
            // Still running: attach a continuation so a later failure is still observed.
            _ = task.ContinueWith(
                static t => _ = t.Exception,
                CancellationToken.None,
                TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);
            return;
        }

        if (task.Exception is { } exception)
        {
            logger.SafeLog(MtpClientLogLevel.Error, $"{description}: {exception}");
        }
    }

    /// <summary>
    /// Neutralizes an accept that is still pending on a failed launch: any socket it eventually yields is
    /// disposed, and any fault it eventually reports is observed.
    /// </summary>
    private static void NeutralizePendingAccept(Task<TcpClient> acceptTask)
        => _ = acceptTask.ContinueWith(
            static t =>
            {
                if (t.Status == TaskStatus.RanToCompletion)
                {
                    t.Result.Dispose();
                }
                else
                {
                    _ = t.Exception;
                }
            },
            CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
}
