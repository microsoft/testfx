// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.IPC;
using Microsoft.Testing.Platform.IPC.Models;
using Microsoft.Testing.Platform.IPC.Serializers;
using Microsoft.Testing.Platform.Logging;
using Microsoft.Testing.Platform.Services;

namespace Microsoft.Testing.Platform.Hosts;

[UnsupportedOSPlatform("browser")]
internal sealed class TestHostControllerCancellationListener : IDisposable
#if NET
#pragma warning disable SA1001 // Commas should be spaced correctly
    , IAsyncDisposable
#pragma warning restore SA1001 // Commas should be spaced correctly
#endif
{
    private static readonly TimeSpan ConnectionTimeout = TimeSpan.FromSeconds(30);

    private readonly CTRLPlusCCancellationTokenSource _testApplicationCancellationTokenSource;
    private readonly ILogger _logger;
    private readonly NamedPipeClient _client;
    private readonly CancellationTokenSource _shutdownCancellationTokenSource = new();
    private readonly Task _listenerTask;
    private int _cancellationRequestedByController;
    private int _disposed;

    public TestHostControllerCancellationListener(
        string pipeName,
        CTRLPlusCCancellationTokenSource testApplicationCancellationTokenSource,
        IEnvironment environment,
        ILogger logger)
    {
        _testApplicationCancellationTokenSource = testApplicationCancellationTokenSource;
        _logger = logger;
        _client = new NamedPipeClient(pipeName, environment, exitProcessOnConnectionLoss: false);
        _client.RegisterAllSerializers();
        _listenerTask = ListenAsync(_shutdownCancellationTokenSource.Token);
    }

    public bool WasCancellationRequestedByController
        => Volatile.Read(ref _cancellationRequestedByController) != 0;

    public bool ShouldReportCompletionAfterCancellation
        => WasCancellationRequestedByController
        || _testApplicationCancellationTokenSource.WasCancellationRequestedByConsole;

    private async Task ListenAsync(CancellationToken cancellationToken)
    {
        bool connected = false;
        try
        {
            using var connectCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            connectCancellationTokenSource.CancelAfter(ConnectionTimeout);
            await _client.ConnectAsync(connectCancellationTokenSource.Token).ConfigureAwait(false);
            connected = true;

            while (!cancellationToken.IsCancellationRequested)
            {
                ServerControlMessage message = await _client.RequestReplyAsync<WaitForServerControlRequest, ServerControlMessage>(
                    WaitForServerControlRequest.CachedInstance,
                    cancellationToken).ConfigureAwait(false);
                if (message.Kind == ServerControlKinds.CancelSession)
                {
                    RequestCancellation();
                    return;
                }

                if (message.Kind == ServerControlKinds.CloseChannel)
                {
                    return;
                }
            }
        }
        catch (Exception) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            await _logger.LogDebugAsync($"Test host controller cancellation channel stopped unexpectedly: {ex}").ConfigureAwait(false);
            if (connected)
            {
                RequestCancellation();
            }
        }
        finally
        {
            _client.Dispose();
        }
    }

    private void RequestCancellation()
    {
        Volatile.Write(ref _cancellationRequestedByController, 1);
        try
        {
            _testApplicationCancellationTokenSource.Cancel();
        }
        catch (AggregateException ex)
        {
            _logger.LogWarning($"Exception while propagating test host controller cancellation:\n{ex}");
        }
    }

    public void Dispose()
        => DisposeAsyncCore().GetAwaiter().GetResult();

#if NET
    public async ValueTask DisposeAsync()
        => await DisposeAsyncCore().ConfigureAwait(false);
#endif

    private async Task DisposeAsyncCore()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

#if NET
        await _shutdownCancellationTokenSource.CancelAsync().ConfigureAwait(false);
#else
        _shutdownCancellationTokenSource.Cancel();
#endif
        _client.Dispose();
        await _listenerTask.ConfigureAwait(false);
        _shutdownCancellationTokenSource.Dispose();
    }
}
