// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Diagnostics.CodeAnalysis;

using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.Hosts;
using Microsoft.Testing.Platform.Logging;
using Microsoft.Testing.Platform.Services;

namespace Microsoft.Testing.Platform.ServerMode;

internal sealed class PassiveNode : IDisposable
{
    private readonly IMessageHandlerFactory _messageHandlerFactory;
    private readonly ITestApplicationCancellationTokenSource _testApplicationCancellationTokenSource;
    private readonly IProcessHandler _processHandler;
    private readonly ILogger<PassiveNode> _logger;
    private readonly IAsyncMonitor _messageMonitor;
    private IMessageHandler? _messageHandler;

    public PassiveNode(
        IMessageHandlerFactory messageHandlerFactory,
        ITestApplicationCancellationTokenSource testApplicationCancellationTokenSource,
        IProcessHandler processHandler,
        IAsyncMonitorFactory asyncMonitorFactory,
        ILogger<PassiveNode> logger)
    {
        _messageHandlerFactory = messageHandlerFactory;
        _testApplicationCancellationTokenSource = testApplicationCancellationTokenSource;
        _processHandler = processHandler;
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
        await _logger.LogDebugAsync("Create message handler");
        _messageHandler = await _messageHandlerFactory.CreateMessageHandlerAsync(_testApplicationCancellationTokenSource.CancellationToken);

        // Wait the initial message
        await _logger.LogDebugAsync("Wait the initial message");
        RpcMessage? message = await _messageHandler.ReadAsync(_testApplicationCancellationTokenSource.CancellationToken);
        if (message is null)
        {
            return false;
        }

        // Log the message
        if (_logger.IsEnabled(LogLevel.Trace))
        {
            await _logger.LogTraceAsync(message!.ToString());
        }

        var requestMessage = (RequestMessage)message;
        var responseObject = new InitializeResponseArgs(
                        ProcessId: _processHandler.GetCurrentProcess().Id,
                        ServerInfo: new ServerInfo("test-anywhere", Version: ServerTestHost.ProtocolVersion),
                        Capabilities: new ServerCapabilities(
                            new ServerTestingCapabilities(
                                SupportsDiscovery: false,
                                MultiRequestSupport: false,
                                VSTestProviderSupport: false,
                                // This means we push attachments
                                SupportsAttachments: true,
                                // This means we're a push node
                                MultiConnectionProvider: true)));

        await SendResponseAsync(requestMessage.Id, responseObject, _testApplicationCancellationTokenSource.CancellationToken);
        return true;
    }

    private async Task SendResponseAsync(int reqId, object result, CancellationToken cancellationToken)
    {
        AssertInitialized();

        ResponseMessage response = new(reqId, result);
        using (await _messageMonitor.LockAsync(cancellationToken))
        {
            await _messageHandler.WriteRequestAsync(response, cancellationToken);
        }
    }

    public async Task SendAttachmentsAsync(TestsAttachments testsAttachments, CancellationToken cancellationToken)
    {
        AssertInitialized();

        NotificationMessage notification = new(JsonRpcMethods.TestingTestUpdatesAttachments, testsAttachments);
        using (await _messageMonitor.LockAsync(cancellationToken))
        {
            await _messageHandler.WriteRequestAsync(notification, cancellationToken);
        }
    }

    public void Dispose()
    {
        if (_messageHandler != null)
        {
            if (_messageHandler is IDisposable disposable)
            {
                disposable.Dispose();
            }
        }
    }
}
