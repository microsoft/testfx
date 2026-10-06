// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.Logging;
using Microsoft.Testing.Platform.Services;

namespace Microsoft.Testing.Platform.ServerMode;

internal sealed class PassiveNode : IDisposable
{
    private readonly IMessageHandlerFactory _messageHandlerFactory;
    private readonly CancellationToken _applicationShutdownToken;
    private readonly IEnvironment _environment;
    private readonly ILogger<PassiveNode> _logger;
    private readonly IAsyncMonitor _messageMonitor;
    private readonly CancellationTokenRegistration _shutdownRegistration;
#if NET9_0_OR_GREATER
    private readonly Lock _lifecycleLock = new();
#else
    private readonly object _lifecycleLock = new();
#endif
    private IMessageHandler? _messageHandler;
    private bool _disposed;

    public PassiveNode(
        IMessageHandlerFactory messageHandlerFactory,
        ITestApplicationCancellationTokenSource testApplicationCancellationTokenSource,
        IEnvironment environment,
        IAsyncMonitorFactory asyncMonitorFactory,
        ILogger<PassiveNode> logger)
    {
        _messageHandlerFactory = messageHandlerFactory;
        _applicationShutdownToken = testApplicationCancellationTokenSource.CancellationToken;
        _environment = environment;
        _messageMonitor = asyncMonitorFactory.Create();
        _logger = logger;
        _shutdownRegistration = _applicationShutdownToken.Register(CloseTransport);
    }

    [MemberNotNull(nameof(_messageHandler))]
    public void AssertInitialized()
    {
        if (_messageHandler is null)
        {
            throw new InvalidOperationException();
        }
    }

    public async Task<bool> ConnectAsync()
    {
        try
        {
            return await ConnectCoreAsync().ConfigureAwait(false);
        }
        catch (Exception ex) when (_applicationShutdownToken.IsCancellationRequested
            && ex is IOException or System.Net.Sockets.SocketException or ObjectDisposedException)
        {
            throw new OperationCanceledException(null, ex, _applicationShutdownToken);
        }
    }

    private async Task<bool> ConnectCoreAsync()
    {
        CancellationToken shutdown = _applicationShutdownToken;
        shutdown.ThrowIfCancellationRequested();
        lock (_lifecycleLock)
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(PassiveNode));
            }
        }

        // Create message handler
        await _logger.LogDebugAsync("Create message handler").ConfigureAwait(false);
        IMessageHandler handler = await _messageHandlerFactory.CreateMessageHandlerAsync(shutdown).ConfigureAwait(false);
        bool disposed;
        lock (_lifecycleLock)
        {
            disposed = _disposed;
            if (!disposed)
            {
                _messageHandler = handler;
            }
        }

        if (disposed)
        {
            (handler as IDisposable)?.Dispose();
            throw new ObjectDisposedException(nameof(PassiveNode));
        }

        // Cancellation may have run its callback before the factory published the handler.
        if (shutdown.IsCancellationRequested)
        {
            (handler as TcpMessageHandler)?.CloseConnection();
            shutdown.ThrowIfCancellationRequested();
        }

        // Wait the initial message
        await _logger.LogDebugAsync("Wait the initial message").ConfigureAwait(false);
        RpcMessage? message = await handler.ReadAsync(shutdown).ConfigureAwait(false);
        if (message is null)
        {
            return false;
        }

        // Log the message
        if (_logger.IsEnabled(LogLevel.Trace))
        {
            await _logger.LogTraceAsync(message.ToString()).ConfigureAwait(false);
        }

        if (message is not RequestMessage requestMessage)
        {
            return false;
        }

        if (requestMessage.Method != JsonRpcMethods.Initialize)
        {
            await SendErrorAsync(
                requestMessage.Id,
                ErrorCodes.ServerNotInitialized,
                "The server must be initialized before this request can be processed.",
                _applicationShutdownToken,
                requestMessage.StringId).ConfigureAwait(false);
            return false;
        }

        if (requestMessage.Params is not InitializeRequestArgs initializeRequest)
        {
            await SendErrorAsync(
                requestMessage.Id,
                ErrorCodes.InvalidParams,
                "The initialize request params are invalid.",
                _applicationShutdownToken,
                requestMessage.StringId).ConfigureAwait(false);
            return false;
        }

        string? negotiatedProtocolVersion = JsonRpcProtocolVersions.Negotiate(initializeRequest.ProtocolVersions);
        if (negotiatedProtocolVersion is null)
        {
            await SendErrorAsync(
                requestMessage.Id,
                ErrorCodes.ProtocolVersionNotSupported,
                $"None of the client's protocol versions are supported. Server versions: {string.Join(", ", JsonRpcProtocolVersions.Supported)}.",
                _applicationShutdownToken,
                requestMessage.StringId).ConfigureAwait(false);
            return false;
        }

        var responseObject = new InitializeResponseArgs(
                        ProcessId: _environment.ProcessId,
                        ServerInfo: new ServerInfo("test-anywhere", Version: PlatformVersion.Version),
                        Capabilities: new ServerCapabilities(
                            new ServerTestingCapabilities(
                                SupportsDiscovery: false,
                                MultiRequestSupport: false,
                                VSTestProviderSupport: false,
                                // This means we push attachments
                                SupportsAttachments: true,
                                // This means we're a push node
                                MultiConnectionProvider: true)
                            {
                                // Attachment-only peers do not own an RPC output device.
                                ShowMessage = initializeRequest.Capabilities.ShowMessage.HasValue ? false : null,
                            }))
        {
            ProtocolVersion = negotiatedProtocolVersion,
        };

        await SendResponseAsync(
            requestMessage.Id,
            responseObject,
            _applicationShutdownToken,
            requestMessage.StringId).ConfigureAwait(false);
        return true;
    }

    private async Task SendErrorAsync(
        int reqId,
        int errorCode,
        string message,
        CancellationToken cancellationToken,
        string? stringId)
    {
        AssertInitialized();

        ErrorMessage error = new(reqId, errorCode, message, Data: null) { StringId = stringId };
        using (await _messageMonitor.LockAsync(cancellationToken).ConfigureAwait(false))
        {
            await WriteMessageAsync(error, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task SendResponseAsync(
        int reqId,
        object result,
        CancellationToken cancellationToken,
        string? stringId)
    {
        AssertInitialized();

        ResponseMessage response = new(reqId, result) { StringId = stringId };
        using (await _messageMonitor.LockAsync(cancellationToken).ConfigureAwait(false))
        {
            await WriteMessageAsync(response, cancellationToken).ConfigureAwait(false);
        }
    }

    public async Task SendAttachmentsAsync(TestsAttachments testsAttachments, CancellationToken cancellationToken)
    {
        AssertInitialized();

        NotificationMessage notification = new(JsonRpcMethods.TestingTestUpdatesAttachments, testsAttachments);
        using (await _messageMonitor.LockAsync(cancellationToken).ConfigureAwait(false))
        {
            await WriteMessageAsync(notification, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task WriteMessageAsync(RpcMessage message, CancellationToken cancellationToken)
    {
        AssertInitialized();
        try
        {
            await _messageHandler.WriteRequestAsync(message, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (_applicationShutdownToken.IsCancellationRequested
            && ex is IOException or System.Net.Sockets.SocketException or ObjectDisposedException)
        {
            // Closing a committed frame during application shutdown is cancellation, not a host failure.
            throw new OperationCanceledException(null, ex, _applicationShutdownToken);
        }
    }

    public void Dispose()
    {
        IMessageHandler? handler;
        lock (_lifecycleLock)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            handler = _messageHandler;
        }

        // Wait for an in-flight shutdown callback without holding the lock it acquires.
        _shutdownRegistration.Dispose();
        (handler as IDisposable)?.Dispose();
    }

    private void CloseTransport()
    {
        IMessageHandler? handler;
        lock (_lifecycleLock)
        {
            handler = _messageHandler;
        }

        (handler as TcpMessageHandler)?.CloseConnection();
    }
}
