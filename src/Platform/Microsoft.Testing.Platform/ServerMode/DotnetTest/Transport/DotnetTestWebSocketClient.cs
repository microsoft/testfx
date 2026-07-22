// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Net.WebSockets;

using Microsoft.Testing.Platform.Helpers;

namespace Microsoft.Testing.Platform.IPC;

/// <summary>
/// A <see cref="IClient"/> implementation of the 'dotnet test' pipe protocol (a.k.a. dotnettestcli) over a
/// WebSocket transport instead of a named pipe. This is the transport used on runtimes that cannot open
/// <see cref="System.IO.Pipes"/> connections - today, <c>browser-wasm</c> (via <c>BrowserWebSocketDuplexStream</c>);
/// it is also available on every other runtime (via <see cref="ClientWebSocketDuplexStream"/>, wrapping
/// <see cref="ClientWebSocket"/>) for testing and for scenarios where a loopback WebSocket gateway is preferable
/// to a named pipe (e.g. crossing a container/sandbox boundary).
/// </summary>
/// <remarks>
/// The wire protocol (message/serializer/version contract, framing) is entirely unchanged from the named-pipe
/// transport: this class reuses <see cref="NamedPipeConnectionBase.WriteMessageAsync"/> and
/// <see cref="NamedPipeConnectionBase.ReadNextMessageAsync"/> against whichever duplex <see cref="Stream"/>
/// <see cref="ConnectAsync"/> produces. Only the connection bootstrap (how the duplex channel is established
/// and authenticated) differs from <see cref="NamedPipeClient"/>.
/// </remarks>
internal sealed class DotnetTestWebSocketClient : NamedPipeConnectionBase, IClient
{
    private readonly Uri _endpoint;
    private readonly string _authToken;
    private readonly IEnvironment _environment;
    private readonly bool _exitProcessOnConnectionLoss;
    private readonly SemaphoreSlim _lock = new(1, 1);

    private Stream? _stream;
    private ClientWebSocket? _clientWebSocket;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="DotnetTestWebSocketClient"/> class for the primary data
    /// channel (exits the process on connection loss).
    /// </summary>
    /// <param name="endpoint">
    /// The loopback WebSocket endpoint (e.g. <c>ws://127.0.0.1:12345/dotnettest</c>) that the SDK/gateway is
    /// already listening on. Resolved and supplied by the SDK, mirroring how the named-pipe transport receives an
    /// already-resolved OS pipe name rather than computing one itself.
    /// </param>
    /// <param name="authToken">
    /// A per-run secret the SDK generated for this connection. It is never logged; see
    /// <see cref="BuildAuthenticatedUri(Uri, string)"/> for how it is transmitted.
    /// </param>
    /// <param name="environment">The environment abstraction used for process-exit on connection loss.</param>
    public DotnetTestWebSocketClient(Uri endpoint, string authToken, IEnvironment environment)
        : this(endpoint, authToken, environment, exitProcessOnConnectionLoss: true)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="DotnetTestWebSocketClient"/> class.
    /// </summary>
    /// <param name="endpoint">
    /// The loopback WebSocket endpoint (e.g. <c>ws://127.0.0.1:12345/dotnettest</c>) that the SDK/gateway is
    /// already listening on. Resolved and supplied by the SDK, mirroring how the named-pipe transport receives an
    /// already-resolved OS pipe name rather than computing one itself.
    /// </param>
    /// <param name="authToken">
    /// A per-run secret the SDK generated for this connection. It is never logged; see
    /// <see cref="BuildAuthenticatedUri(Uri, string)"/> for how it is transmitted.
    /// </param>
    /// <param name="environment">The environment abstraction used for process-exit on connection loss.</param>
    /// <param name="exitProcessOnConnectionLoss">
    /// See <see cref="NamedPipeClient(string, IEnvironment, bool)"/> for the semantics; the primary data channel
    /// uses <see langword="true"/>, auxiliary channels use <see langword="false"/>.
    /// </param>
    public DotnetTestWebSocketClient(Uri endpoint, string authToken, IEnvironment environment, bool exitProcessOnConnectionLoss)
    {
        _endpoint = endpoint;
        _authToken = authToken;
        _environment = environment;
        _exitProcessOnConnectionLoss = exitProcessOnConnectionLoss;
    }

    public bool IsConnected => _stream is not null;

    public async Task ConnectAsync(CancellationToken cancellationToken)
    {
        Uri authenticatedEndpoint = BuildAuthenticatedUri(_endpoint, _authToken);

#if NET7_0_OR_GREATER
        if (OperatingSystem.IsBrowser())
        {
            _stream = await BrowserWebSocketDuplexStream.ConnectAsync(authenticatedEndpoint, cancellationToken).ConfigureAwait(false);
            return;
        }
#endif
        var clientWebSocket = new ClientWebSocket();
        try
        {
            await clientWebSocket.ConnectAsync(authenticatedEndpoint, cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            clientWebSocket.Dispose();
            throw;
        }

        _clientWebSocket = clientWebSocket;
        _stream = new ClientWebSocketDuplexStream(clientWebSocket);
    }

    /// <summary>
    /// Appends the per-run authentication token to <paramref name="endpoint"/> as a query-string parameter.
    /// </summary>
    /// <remarks>
    /// The browser's native <c>WebSocket</c> constructor only accepts a URL and an optional subprotocol list - it
    /// cannot set arbitrary request headers on the upgrade handshake - so a bearer header is not an option for the
    /// browser transport. A query-string token is the same approach ASP.NET Core SignalR uses for its WebSocket
    /// transport for the identical reason. This is also why CORS cannot serve as authentication here: CORS governs
    /// which origins may *read* a cross-origin response, not who may *open* a WebSocket connection in the first
    /// place, so without an explicit token any process that can reach the loopback port could connect. Callers must
    /// never log the returned <see cref="Uri"/> verbatim (or any log line derived from it) since it carries the
    /// secret - diagnostics should log only <see cref="Uri.Host"/>/<see cref="Uri.Port"/>/<see cref="Uri.AbsolutePath"/>.
    /// </remarks>
    internal static Uri BuildAuthenticatedUri(Uri endpoint, string token)
    {
        string tokenFragment = $"dotnetTestToken={Uri.EscapeDataString(token)}";
        string original = endpoint.OriginalString;
        int fragmentIndex = original.IndexOf('#');
        string beforeFragment = fragmentIndex >= 0 ? original[..fragmentIndex] : original;
        string fragment = fragmentIndex >= 0 ? original[fragmentIndex..] : string.Empty;
        string separator = beforeFragment.Contains('?') ? "&" : "?";
        return new Uri($"{beforeFragment}{separator}{tokenFragment}{fragment}", UriKind.Absolute);
    }

    public async Task<TResponse> RequestReplyAsync<TRequest, TResponse>(TRequest request, CancellationToken cancellationToken)
        where TRequest : IRequest
        where TResponse : IResponse
    {
        RoslynDebug.Assert(_stream is not null);

        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            INamedPipeSerializer requestSerializer = GetSerializer(typeof(TRequest));

            try
            {
                await WriteMessageAsync(_stream, requestSerializer, request!, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is IOException or ObjectDisposedException or WebSocketException)
            {
                // The peer disconnected while we were writing the request. Mirror NamedPipeClient's handling: if
                // we cannot deliver the request there's no way to recover, so exit abnormally instead of
                // surfacing a raw transport error to the caller (for the primary data channel).
                if (!_exitProcessOnConnectionLoss)
                {
                    throw;
                }

                _environment.Exit((int)ExitCode.GenericFailure);
                throw;
            }

            object? response;
            try
            {
                response = await ReadNextMessageAsync(_stream, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is IOException or ObjectDisposedException or WebSocketException)
            {
                // Unlike a named pipe (which reports a clean disconnect as a 0-byte read, i.e. a null response
                // below), an abrupt WebSocket disconnect - e.g. the peer's process being killed mid-response,
                // before it can complete the WebSocket close handshake - surfaces here as a thrown exception
                // instead of a null result. Route it through the same connection-loss handling as a null
                // response so both failure shapes are indistinguishable to the caller.
                await HandleMissingResponseAsync(ex).ConfigureAwait(false);
                throw; // Unreachable: HandleMissingResponseAsync always throws or terminates the process.
            }

            if (response is null)
            {
                await HandleMissingResponseAsync(cause: null).ConfigureAwait(false);
            }

            return (TResponse)response!;
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>
    /// Reacts to the transport being closed by the peer before a response was received - whether observed as a
    /// null/EOF read or as a thrown transport exception (see the two callers in
    /// <see cref="RequestReplyAsync{TRequest, TResponse}"/>). Mirrors <see cref="NamedPipeClient"/>'s identical
    /// connection-loss policy: when the primary data channel (<c>exitProcessOnConnectionLoss: true</c>) loses its
    /// peer there is no way to recover, so the process exits abnormally after a best-effort diagnostic; an
    /// auxiliary channel (<c>exitProcessOnConnectionLoss: false</c>) instead always throws so the caller can react
    /// cooperatively.
    /// </summary>
    /// <remarks>Never returns normally: either throws, or terminates the process via <c>_environment.Exit</c>.</remarks>
    private async Task HandleMissingResponseAsync(Exception? cause)
    {
        if (!_exitProcessOnConnectionLoss)
        {
            throw new IOException("The WebSocket transport was closed by the peer before a response was received.", cause);
        }

        // Surface a diagnostic on stderr so the user has a chance to understand why this process is exiting. We
        // deliberately use Console.Error (and not stdout) to avoid corrupting any machine-readable output that
        // may be flowing through stdout.
        try
        {
            await Console.Error.WriteLineAsync($"[DotnetTestWebSocketClient] The WebSocket transport was closed by the peer before a response was received. The peer process likely exited or was killed. Terminating with exit code {(int)ExitCode.GenericFailure}.").ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is IOException or ObjectDisposedException or InvalidOperationException or NotSupportedException or ArgumentException or OperationCanceledException)
        {
            // Best-effort diagnostic only; never let logging failures shadow the original problem.
        }

        _environment.Exit((int)ExitCode.GenericFailure);

        // _environment.Exit normally terminates the process and never returns. Guard against alternate
        // IEnvironment implementations (e.g. tests) that don't terminate by throwing explicitly.
        throw new InvalidOperationException("The WebSocket transport was closed by the peer before a response was received.", cause);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _lock.Dispose();
        DisposeBuffers();
        _stream?.Dispose();
        _clientWebSocket?.Dispose();
    }
}
