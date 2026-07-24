// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.ServerMode.Client;

/// <summary>
/// The JSON-RPC transport engine for an MTP server-mode connection. It owns an <see cref="IMessageHandler"/>
/// (the reused <c>TcpMessageHandler</c> in production), runs a background read loop, correlates responses to
/// their requests, and surfaces server-initiated notifications and requests.
/// </summary>
/// <remarks>
/// This type is transport-only: it knows nothing about <c>initialize</c>/<c>discover</c>/<c>run</c>
/// semantics. The higher-level <c>MtpServerClient</c> builds its typed API on top of
/// <see cref="SendRequestAsync"/>, <see cref="SendNotificationAsync"/>, and <see cref="NotificationReceived"/>.
/// Because the connection sits above <see cref="IMessageHandler"/>, tests can drive it over a real loopback
/// socket without any additional seams.
/// </remarks>
internal sealed class MtpJsonRpcConnection : IDisposable
{
    private readonly IMessageHandler _handler;
    private readonly IMtpClientLogger _logger;
    private readonly ConcurrentDictionary<int, PendingRequest> _pendingRequests = new();
    private readonly SemaphoreSlim _writeLock = new(1, 1);
    private readonly CancellationTokenSource _readLoopCancellation = new();

    private int _nextRequestId;
    private Task? _readLoop;
    private int _disposed;

    public MtpJsonRpcConnection(IMessageHandler handler, IMtpClientLogger? logger = null)
    {
        _handler = handler ?? throw new ArgumentNullException(nameof(handler));
        _logger = logger ?? NullMtpClientLogger.Instance;
    }

    /// <summary>
    /// Raised for every server-to-client notification. The handler receives the method name and the raw
    /// params payload (an <c>IDictionary&lt;string, object?&gt;</c> or <see langword="null"/>); the client API
    /// layer decodes it based on the method.
    /// </summary>
    public event Action<NotificationMessage>? NotificationReceived;

    /// <summary>
    /// Gets or sets the handler for server-initiated requests (for example <c>client/attachDebugger</c>).
    /// The delegate returns the result object used to answer the request; returning <see langword="null"/>
    /// answers with a null result. The connection ALWAYS sends a response so the server never blocks — if no
    /// handler is set, or the handler throws, a null-result response is sent.
    /// </summary>
    public Func<RequestMessage, CancellationToken, Task<object?>>? ServerRequestHandler { get; set; }

    /// <summary>
    /// Starts the background read loop. Call once, after wiring <see cref="NotificationReceived"/> and
    /// <see cref="ServerRequestHandler"/>.
    /// </summary>
    public void Start()
        => _readLoop ??= Task.Run(() => ReadLoopAsync(_readLoopCancellation.Token));

    /// <summary>
    /// Sends a request and awaits its correlated response. If <paramref name="cancellationToken"/> fires
    /// before the response arrives, a <c>$/cancelRequest</c> notification is sent to the server and the
    /// returned task is canceled.
    /// </summary>
    public async Task<ResponseMessage> SendRequestAsync(string method, object? @params, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        int id = Interlocked.Increment(ref _nextRequestId);
        var pending = new PendingRequest(method);
        _pendingRequests[id] = pending;

        using CancellationTokenRegistration registration = cancellationToken.Register(
            () => CancelPendingRequest(id, cancellationToken));

        try
        {
            await WriteMessageAsync(new RequestMessage(id, method, @params), cancellationToken).ConfigureAwait(false);
            return await pending.Completion.Task.ConfigureAwait(false);
        }
        finally
        {
            _pendingRequests.TryRemove(id, out _);
        }
    }

    /// <summary>
    /// Sends a fire-and-forget notification to the server.
    /// </summary>
    public Task SendNotificationAsync(string method, object? @params, CancellationToken cancellationToken)
        => WriteMessageAsync(new NotificationMessage(method, @params), cancellationToken);

    private async Task WriteMessageAsync(RpcMessage message, CancellationToken cancellationToken)
    {
        await _writeLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            // Acquiring the write lock is cancellable, but the frame write itself is NOT: once a
            // Content-Length frame starts going out, cancelling mid-write would leave a partial frame on
            // the wire and the very next write (for example a $/cancelRequest) would desync the server's
            // framing. Pass CancellationToken.None so a started frame always completes atomically.
            await _handler.WriteRequestAsync(message, CancellationToken.None).ConfigureAwait(false);
        }
        finally
        {
            _writeLock.Release();
        }
    }

    private async Task ReadLoopAsync(CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                RpcMessage? message = await _handler.ReadAsync(cancellationToken).ConfigureAwait(false);
                if (message is null)
                {
                    // Null signals a graceful or abrupt disconnect.
                    FailAllPending(new MtpServerConnectionClosedException());
                    return;
                }

                Dispatch(message, cancellationToken);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Expected during teardown.
        }
        catch (Exception ex)
        {
            // Fail pending requests FIRST: a caller awaiting a response must be released even if logging
            // throws. SafeLog additionally guarantees the logger cannot fault this loop.
            FailAllPending(new MtpServerClientException("The MTP client read loop failed.", ex));
            _logger.SafeLog(MtpClientLogLevel.Error, $"MTP client read loop failed: {ex}");
        }
    }

    private void Dispatch(RpcMessage message, CancellationToken cancellationToken)
    {
        switch (message)
        {
            case ResponseMessage response:
                if (_pendingRequests.TryGetValue(response.Id, out PendingRequest? successful))
                {
                    successful.Completion.TrySetResult(response);
                }

                break;

            case ErrorMessage error:
                if (_pendingRequests.TryGetValue(error.Id, out PendingRequest? failed))
                {
                    failed.Completion.TrySetException(new MtpServerErrorException(error.ErrorCode, error.Message));
                }

                break;

            case NotificationMessage notification:
                RaiseNotification(notification);
                break;

            case RequestMessage request:
                _ = HandleServerRequestAsync(request, cancellationToken);
                break;
        }
    }

    private void RaiseNotification(NotificationMessage notification)
    {
        try
        {
            NotificationReceived?.Invoke(notification);
        }
        catch (Exception ex)
        {
            _logger.SafeLog(MtpClientLogLevel.Warning, $"A handler for notification '{notification.Method}' threw: {ex}");
        }
    }

    private async Task HandleServerRequestAsync(RequestMessage request, CancellationToken cancellationToken)
    {
        object? result = null;
        try
        {
            Func<RequestMessage, CancellationToken, Task<object?>>? handler = ServerRequestHandler;
            if (handler is not null)
            {
                result = await handler(request, cancellationToken).ConfigureAwait(false);
            }
        }
        catch (Exception ex)
        {
            _logger.SafeLog(MtpClientLogLevel.Warning, $"The handler for server request '{request.Method}' threw: {ex}");
        }

        // Always answer so the server is never left waiting.
        try
        {
            await WriteMessageAsync(new ResponseMessage(request.Id, result), cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.SafeLog(MtpClientLogLevel.Warning, $"Failed to respond to server request '{request.Method}': {ex}");
        }
    }

    private void CancelPendingRequest(int id, CancellationToken cancellationToken)
    {
        if (!_pendingRequests.TryGetValue(id, out PendingRequest? pending))
        {
            return;
        }

        pending.Completion.TrySetCanceled(cancellationToken);

        // Best-effort notify the server to stop the in-flight work.
        _ = SendCancelNotificationAsync(id);
    }

    private async Task SendCancelNotificationAsync(int id)
    {
        try
        {
            await SendNotificationAsync(JsonRpcMethods.CancelRequest, new CancelRequestArgs(id), CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.SafeLog(MtpClientLogLevel.Debug, $"Failed to send $/cancelRequest for request {id}: {ex}");
        }
    }

    private void FailAllPending(Exception exception)
    {
        foreach (KeyValuePair<int, PendingRequest> entry in _pendingRequests)
        {
            if (_pendingRequests.TryRemove(entry.Key, out PendingRequest? pending))
            {
                pending.Completion.TrySetException(exception);
            }
        }
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 1)
        {
            return;
        }

        _readLoopCancellation.Cancel();
        FailAllPending(new ObjectDisposedException(nameof(MtpJsonRpcConnection)));

        try
        {
            (_handler as IDisposable)?.Dispose();
        }
        catch (Exception ex)
        {
            _logger.SafeLog(MtpClientLogLevel.Debug, $"Disposing the message handler threw: {ex}");
        }

        _readLoopCancellation.Dispose();
        _writeLock.Dispose();
    }

    private sealed class PendingRequest(string method)
    {
        public string Method { get; } = method;

        public TaskCompletionSource<ResponseMessage> Completion { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    }
}
