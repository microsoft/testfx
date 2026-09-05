// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.IPC;
using Microsoft.Testing.Platform.IPC.Models;
using Microsoft.Testing.Platform.IPC.Serializers;
using Microsoft.Testing.Platform.Logging;

namespace Microsoft.Testing.Platform.Hosts;

[UnsupportedOSPlatform("browser")]
internal sealed class TestHostControllerCancellationServer : IDisposable
#if NET
#pragma warning disable SA1001 // Commas should be spaced correctly
    , IAsyncDisposable
#pragma warning restore SA1001 // Commas should be spaced correctly
#endif
{
    private readonly CancellationTokenSource _acceptCancellationTokenSource = new();
    private readonly CancellationTokenSource _serverLifetimeCancellationTokenSource = new();
    private readonly TaskCompletionSource<ServerControlMessage> _controlMessage =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    private readonly TaskCompletionSource<bool> _requestReceived =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    private readonly NamedPipeServer _server;
    private Task? _waitConnectionTask;
    private int _disposed;

    public TestHostControllerCancellationServer(
        IReadOnlyList<string>? authorizedSecurityIdentities,
        IEnvironment environment,
        ILoggerFactory loggerFactory,
        ITask task)
    {
        _server = new NamedPipeServer(
            $"CONTROLTOHOST_{Guid.NewGuid():N}",
            HandleRequestAsync,
            environment,
            loggerFactory.CreateLogger<TestHostControllerCancellationServer>(),
            task,
            authorizedSecurityIdentities,
            _serverLifetimeCancellationTokenSource.Token);
        _server.RegisterAllSerializers();
    }

    public string PipeName => _server.PipeName.Name;

    public void Start()
        => _waitConnectionTask = _server.WaitConnectionAsync(_acceptCancellationTokenSource.Token);

    public void RequestCancellation()
        => _controlMessage.TrySetResult(new ServerControlMessage(ServerControlKinds.CancelSession));

    public Task WaitForRequestAsync()
        => _requestReceived.Task;

    private async Task<IResponse> HandleRequestAsync(IRequest request)
    {
        if (request is not WaitForServerControlRequest)
        {
            throw new NotSupportedException($"Request '{request}' not supported");
        }

        _requestReceived.TrySetResult(true);
        return await _controlMessage.Task.ConfigureAwait(false);
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

        _controlMessage.TrySetResult(new ServerControlMessage(ServerControlKinds.CloseChannel));
#if NET
        await _acceptCancellationTokenSource.CancelAsync().ConfigureAwait(false);
#else
        _acceptCancellationTokenSource.Cancel();
#endif
        if (!_requestReceived.Task.IsCompleted)
        {
#if NET
            await _serverLifetimeCancellationTokenSource.CancelAsync().ConfigureAwait(false);
#else
            _serverLifetimeCancellationTokenSource.Cancel();
#endif
        }

        if (_waitConnectionTask is not null)
        {
            try
            {
                await _waitConnectionTask.ConfigureAwait(false);
            }
            catch (OperationCanceledException ex) when (ex.CancellationToken == _acceptCancellationTokenSource.Token)
            {
            }
        }

        await DisposeHelper.DisposeAsync(_server).ConfigureAwait(false);
        _acceptCancellationTokenSource.Dispose();
        _serverLifetimeCancellationTokenSource.Dispose();
    }
}
