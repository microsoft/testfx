// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.Logging;
using Microsoft.Testing.Platform.Services;

namespace Microsoft.Testing.Platform.ServerMode;

internal sealed class PassiveNode : IDisposable
{
    private readonly IMessageHandlerFactory _messageHandlerFactory;
    private readonly ITestApplicationCancellationTokenSource _testApplicationCancellationTokenSource;
    private readonly IEnvironment _environment;
    private readonly ILogger<PassiveNode> _logger;
    private readonly IAsyncMonitor _messageMonitor;
    private IMessageHandler? _messageHandler;

    public PassiveNode(
        IMessageHandlerFactory messageHandlerFactory,
        ITestApplicationCancellationTokenSource testApplicationCancellationTokenSource,
        IEnvironment environment,
        IAsyncMonitorFactory asyncMonitorFactory,
        ILogger<PassiveNode> logger)
    {
        _messageHandlerFactory = messageHandlerFactory;
        _testApplicationCancellationTokenSource = testApplicationCancellationTokenSource;
        _environment = environment;
        _messageMonitor = asyncMonitorFactory.Create();
        _logger = logger;
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
        // Create message handler
        await _logger.LogDebugAsync("Create message handler").ConfigureAwait(false);
        _messageHandler = await _messageHandlerFactory.CreateMessageHandlerAsync(_testApplicationCancellationTokenSource.CancellationToken).ConfigureAwait(false);

        // Wait the initial message
        await _logger.LogDebugAsync("Wait the initial message").ConfigureAwait(false);
        RpcMessage? message = await _messageHandler.ReadAsync(_testApplicationCancellationTokenSource.CancellationToken).ConfigureAwait(false);
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
                _testApplicationCancellationTokenSource.CancellationToken,
                requestMessage.StringId).ConfigureAwait(false);
            return false;
        }

        if (requestMessage.Params is not InitializeRequestArgs initializeRequest)
        {
            await SendErrorAsync(
                requestMessage.Id,
                ErrorCodes.InvalidParams,
                "The initialize request params are invalid.",
                _testApplicationCancellationTokenSource.CancellationToken,
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
                _testApplicationCancellationTokenSource.CancellationToken,
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
                                MultiConnectionProvider: true)))
        {
            ProtocolVersion = negotiatedProtocolVersion,
        };

        await SendResponseAsync(
            requestMessage.Id,
            responseObject,
            _testApplicationCancellationTokenSource.CancellationToken,
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
            await _messageHandler.WriteRequestAsync(error, cancellationToken).ConfigureAwait(false);
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
            await _messageHandler.WriteRequestAsync(response, cancellationToken).ConfigureAwait(false);
        }
    }

    public async Task SendAttachmentsAsync(TestsAttachments testsAttachments, CancellationToken cancellationToken)
    {
        AssertInitialized();

        NotificationMessage notification = new(JsonRpcMethods.TestingTestUpdatesAttachments, testsAttachments);
        using (await _messageMonitor.LockAsync(cancellationToken).ConfigureAwait(false))
        {
            await _messageHandler.WriteRequestAsync(notification, cancellationToken).ConfigureAwait(false);
        }
    }

    public void Dispose()
    {
        if (_messageHandler is IDisposable disposable)
        {
            disposable.Dispose();
        }
    }
}
