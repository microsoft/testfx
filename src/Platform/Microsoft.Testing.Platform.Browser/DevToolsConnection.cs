// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Net.WebSockets;
using System.Text.Json;

namespace Microsoft.Testing.Platform.Browser;

internal sealed class DevToolsConnection : IAsyncDisposable
{
    private readonly ClientWebSocket _webSocket = new();
    private readonly CancellationTokenSource _disposeCancellationTokenSource = new();
    private readonly SemaphoreSlim _sendLock = new(1, 1);
    private readonly ConcurrentDictionary<int, TaskCompletionSource<JsonElement>> _pendingCommands = new();
    private Task? _receiveTask;
    private int _nextCommandId;

    public event Action<string, JsonElement>? EventReceived;

    public async Task ConnectAsync(Uri endpoint, CancellationToken cancellationToken)
    {
        await _webSocket.ConnectAsync(endpoint, cancellationToken).ConfigureAwait(false);
        _receiveTask = ReceiveLoopAsync(_disposeCancellationTokenSource.Token);
    }

    public async Task<JsonElement> SendCommandAsync(string method, object? parameters, CancellationToken cancellationToken)
    {
        int id = Interlocked.Increment(ref _nextCommandId);
        var completion = new TaskCompletionSource<JsonElement>(TaskCreationOptions.RunContinuationsAsynchronously);
        if (!_pendingCommands.TryAdd(id, completion))
        {
            throw new BrowserLauncherException("Unable to register a Chromium DevTools command.");
        }

        byte[] payload = JsonSerializer.SerializeToUtf8Bytes(new { id, method, @params = parameters });
        await _sendLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await _webSocket.SendAsync(payload, WebSocketMessageType.Text, endOfMessage: true, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _sendLock.Release();
        }

        using CancellationTokenRegistration registration = cancellationToken.Register(() =>
        {
            if (_pendingCommands.TryRemove(id, out TaskCompletionSource<JsonElement>? pendingCommand))
            {
                pendingCommand.TrySetCanceled(cancellationToken);
            }
        });
        return await completion.Task.ConfigureAwait(false);
    }

    public async ValueTask DisposeAsync()
    {
        using var closeCancellationTokenSource = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        if (_webSocket.State == WebSocketState.Open)
        {
            try
            {
                await _webSocket.CloseOutputAsync(
                    WebSocketCloseStatus.NormalClosure,
                    "Microsoft Testing Platform browser run completed.",
                    closeCancellationTokenSource.Token).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is WebSocketException or OperationCanceledException or InvalidOperationException)
            {
            }
        }

        await _disposeCancellationTokenSource.CancelAsync().ConfigureAwait(false);
        if (_receiveTask is not null)
        {
            try
            {
                await _receiveTask.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
            }
        }

        CompletePendingCommands(new OperationCanceledException("The Chromium DevTools connection was disposed."));
        _sendLock.Dispose();
        _webSocket.Dispose();
        _disposeCancellationTokenSource.Dispose();
    }

    private async Task ReceiveLoopAsync(CancellationToken cancellationToken)
    {
        byte[] buffer = new byte[16 * 1024];
        try
        {
            while (!cancellationToken.IsCancellationRequested && _webSocket.State == WebSocketState.Open)
            {
                using var message = new MemoryStream();
                while (true)
                {
                    WebSocketReceiveResult result = await _webSocket.ReceiveAsync(
                        new ArraySegment<byte>(buffer),
                        cancellationToken).ConfigureAwait(false);
                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        return;
                    }

                    await message.WriteAsync(buffer.AsMemory(0, result.Count), cancellationToken).ConfigureAwait(false);
                    if (result.EndOfMessage)
                    {
                        break;
                    }
                }

                using var document = JsonDocument.Parse(message.ToArray());
                JsonElement root = document.RootElement;
                if (root.TryGetProperty("id", out JsonElement idElement)
                    && _pendingCommands.TryRemove(idElement.GetInt32(), out TaskCompletionSource<JsonElement>? completion))
                {
                    if (root.TryGetProperty("error", out JsonElement error))
                    {
                        completion.TrySetException(
                            new BrowserLauncherException($"Chromium DevTools command failed: {error.GetRawText()}"));
                    }
                    else
                    {
                        completion.TrySetResult(root.GetProperty("result").Clone());
                    }
                }
                else if (root.TryGetProperty("method", out JsonElement methodElement))
                {
                    EventReceived?.Invoke(
                        methodElement.GetString() ?? string.Empty,
                        root.TryGetProperty("params", out JsonElement parameters) ? parameters.Clone() : default);
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            CompletePendingCommands(ex);
        }
        finally
        {
            CompletePendingCommands(new BrowserLauncherException("The Chromium DevTools connection closed."));
        }
    }

    private void CompletePendingCommands(Exception exception)
    {
        foreach (KeyValuePair<int, TaskCompletionSource<JsonElement>> pendingCommand in _pendingCommands)
        {
            if (_pendingCommands.TryRemove(pendingCommand.Key, out TaskCompletionSource<JsonElement>? completion))
            {
                completion.TrySetException(exception);
            }
        }
    }
}
