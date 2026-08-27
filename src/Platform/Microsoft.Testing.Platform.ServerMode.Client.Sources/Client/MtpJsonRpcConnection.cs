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
    private readonly ConcurrentDictionary<(int Id, bool IsString), PendingRequest> _pendingRequests = new();
    private readonly SemaphoreSlim _writeLock = new(1, 1);
    private readonly CancellationTokenSource _readLoopCancellation = new();
    private readonly object _startLock = new();

    // True within the read loop's async execution flow. Dispose reads this to detect a re-entrant call
    // from a notification / server-request handler (both dispatched on the read-loop flow) and skip
    // synchronously waiting on the read loop from within itself. This uses AsyncLocal rather than
    // Task.CurrentId because the read loop is an async method: after its first await the continuation no
    // longer reports the Task.Run task's id, so Task.CurrentId would spuriously not match and Dispose
    // would self-wait for the full shutdown timeout. AsyncLocal rides the flow across every await.
    private readonly AsyncLocal<bool> _onReadLoopFlow = new();

    // Bounded wait for the read loop to observe cancellation / socket close during Dispose.
    private static readonly TimeSpan ReadLoopShutdownTimeout = TimeSpan.FromSeconds(5);

    private int _nextRequestId;
    private Task? _readLoop;
    private Func<RequestMessage, CancellationToken, Task<object?>>? _serverRequestHandler;
    private int _disposed;

    // Latched once when the connection reaches a terminal state (read loop exited or Dispose ran). A
    // non-null value means no read loop remains to complete a response, so new sends must fail fast.
    private Exception? _closedReason;

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
    /// <remarks>
    /// Handlers run synchronously on the single read-loop thread that also drains the socket, so
    /// notifications are delivered strictly in the order the server sent them (the ordering guarantee the
    /// client API relies on). A handler MUST NOT block for a long time or synchronously wait on another
    /// request to this client (for example <c>RunTestsAsync(...).GetAwaiter().GetResult()</c>): doing so
    /// stalls the read loop and, for a re-entrant client call, deadlocks because the response can never be
    /// read. Marshal to another thread if the handler needs to do slow work or call back into the client.
    /// </remarks>
    public event Action<NotificationMessage>? NotificationReceived;

    /// <summary>
    /// Gets or sets the handler for server-initiated requests (for example <c>client/attachDebugger</c>).
    /// The delegate returns the result object used to answer the request; returning <see langword="null"/>
    /// answers with a null result. The connection ALWAYS sends a response so the server never blocks — if no
    /// handler is set, or the handler throws, a null-result response is sent.
    /// </summary>
    public Func<RequestMessage, CancellationToken, Task<object?>>? ServerRequestHandler
    {
        get => Volatile.Read(ref _serverRequestHandler);
        set => Volatile.Write(ref _serverRequestHandler, value);
    }

    /// <summary>
    /// Starts the background read loop. Call once, after wiring <see cref="NotificationReceived"/> and
    /// <see cref="ServerRequestHandler"/>.
    /// </summary>
    public void Start()
    {
        lock (_startLock)
        {
            if (Volatile.Read(ref _disposed) != 0)
            {
                throw new ObjectDisposedException(nameof(MtpJsonRpcConnection));
            }

            _readLoop ??= Task.Run(() => ReadLoopAsync(_readLoopCancellation.Token));
        }
    }

    /// <summary>
    /// Sends a request and awaits its correlated response. If <paramref name="cancellationToken"/> fires
    /// before the response arrives, a <c>$/cancelRequest</c> notification is sent to the server and the
    /// returned task is canceled.
    /// </summary>
    public async Task<ResponseMessage> SendRequestAsync(string method, object? @params, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        // Fail fast if the connection has already reached a terminal state: no read loop remains to
        // complete a response, so registering the request would hang forever (a TCP write after the peer's
        // FIN can still land in the send buffer and succeed, so WriteMessageAsync would not surface the
        // closure).
        if (Volatile.Read(ref _closedReason) is { } closedBefore)
        {
            throw closedBefore;
        }

        int id = Interlocked.Increment(ref _nextRequestId);
        (int Id, bool IsString) requestKey = GetRequestKey(id, stringId: null);
        var pending = new PendingRequest(method);
        _pendingRequests[requestKey] = pending;

        // Re-check after registering: the read loop may have latched a terminal reason and run
        // FailAllPending between the check above and this insert, missing this entry. Observing the reason
        // here guarantees the request is completed rather than left waiting.
        if (Volatile.Read(ref _closedReason) is { } closedAfter)
        {
            _pendingRequests.TryRemove(requestKey, out _);
            throw closedAfter;
        }

        using CancellationTokenRegistration registration = cancellationToken.Register(
            () => CancelPendingRequest(id, cancellationToken));

        try
        {
            await WriteMessageAsync(new RequestMessage(id, method, @params), cancellationToken).ConfigureAwait(false);
            return await pending.Completion.Task.ConfigureAwait(false);
        }
        finally
        {
            _pendingRequests.TryRemove(requestKey, out _);
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
        // Mark this async flow as the read loop so a handler that calls Dispose (which runs on this flow)
        // is detected re-entrantly and does not synchronously wait on the loop from within it. Set before
        // the first await so the marker flows across every await and into every synchronous Dispatch.
        _onReadLoopFlow.Value = true;
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                RpcMessage? message = await _handler.ReadAsync(cancellationToken).ConfigureAwait(false);
                if (message is null)
                {
                    // Null signals a graceful or abrupt disconnect.
                    Close(new MtpServerConnectionClosedException());
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
            // Fail pending requests FIRST (via Close, which also latches the terminal state): a caller
            // awaiting a response must be released even if logging throws. SafeLog additionally guarantees
            // the logger cannot fault this loop.
            Close(new MtpServerClientException("The MTP client read loop failed.", ex));
            _logger.SafeLog(MtpClientLogLevel.Error, $"MTP client read loop failed: {ex}");
        }
    }

    private void Dispatch(RpcMessage message, CancellationToken cancellationToken)
    {
        switch (message)
        {
            case ResponseMessage response:
                if (_pendingRequests.TryGetValue(GetRequestKey(response.Id, response.StringId), out PendingRequest? successful))
                {
                    successful.Completion.TrySetResult(response);
                }

                break;

            case ErrorMessage error:
                if (_pendingRequests.TryGetValue(GetRequestKey(error.Id, error.StringId), out PendingRequest? failed))
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
            Func<RequestMessage, CancellationToken, Task<object?>>? handler = Volatile.Read(ref _serverRequestHandler);
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
            await WriteMessageAsync(
                new ResponseMessage(request.Id, result) { StringId = request.StringId },
                cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.SafeLog(MtpClientLogLevel.Warning, $"Failed to respond to server request '{request.Method}': {ex}");
        }
    }

    private void CancelPendingRequest(int id, CancellationToken cancellationToken)
    {
        if (!_pendingRequests.TryGetValue(GetRequestKey(id, stringId: null), out PendingRequest? pending))
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

    private void Close(Exception reason)
    {
        // Latch the terminal reason exactly once, THEN fail every pending request. The ordering is the
        // crux of the race fix with SendRequestAsync: a sender registers its pending request and then
        // re-reads _closedReason, so either this FailAllPending observes that request, or the sender
        // observes the latched reason — the request can never be left hanging with no one to complete it.
        if (Interlocked.CompareExchange(ref _closedReason, reason, null) is null)
        {
            FailAllPending(reason);
        }
    }

    private void FailAllPending(Exception exception)
    {
        foreach (KeyValuePair<(int Id, bool IsString), PendingRequest> entry in _pendingRequests)
        {
            if (_pendingRequests.TryRemove(entry.Key, out PendingRequest? pending))
            {
                pending.Completion.TrySetException(exception);
            }
        }
    }

    private static (int Id, bool IsString) GetRequestKey(int id, string? stringId)
        => (id, stringId is not null);

    public void Dispose()
    {
        Task? readLoop;
        lock (_startLock)
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 1)
            {
                return;
            }

            readLoop = _readLoop;
        }

        // Latch the terminal state and release anyone awaiting a response.
        Close(new ObjectDisposedException(nameof(MtpJsonRpcConnection)));

        _readLoopCancellation.Cancel();

        // Disposing the handler closes the underlying socket/streams, which unblocks a read loop parked in
        // a blocking ReadAsync that cancellation alone would not interrupt.
        try
        {
            (_handler as IDisposable)?.Dispose();
        }
        catch (Exception ex)
        {
            _logger.SafeLog(MtpClientLogLevel.Debug, $"Disposing the message handler threw: {ex}");
        }

        // Wait (bounded) for the read loop to actually finish, then intentionally do NOT dispose
        // _readLoopCancellation / _writeLock: leaking two lightweight primitives is strictly better than
        // disposing them out from under an in-flight write or the read loop's ReadAsync, which would
        // surface spurious ObjectDisposedExceptions (one thrown out of the write lock's finally block).
        // Guard against waiting on ourselves in case Dispose runs from a notification / server-request
        // handler executing on the read-loop flow (see _onReadLoopFlow).
        if (readLoop is not null && !_onReadLoopFlow.Value)
        {
            try
            {
                readLoop.Wait(ReadLoopShutdownTimeout);
            }
            catch (Exception ex)
            {
                _logger.SafeLog(MtpClientLogLevel.Debug, $"Waiting for the read loop to stop threw: {ex}");
            }
        }
    }

    private sealed class PendingRequest(string method)
    {
        public string Method { get; } = method;

        public TaskCompletionSource<ResponseMessage> Completion { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    }
}
