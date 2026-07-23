// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Net.Http.Headers;

using Microsoft.CodeAnalysis;
#if !NETCOREAPP
using Microsoft.Testing.Platform.Helpers;
#endif

namespace Microsoft.Testing.Platform.IPC;

/// <summary>
/// Carries the existing framed <c>dotnettestcli</c> request/reply protocol over authenticated HTTP POSTs.
/// </summary>
[Embedded]
internal sealed class DotnetTestHttpClient : NamedPipeConnectionBase, IClient
{
    private const string MediaType = "application/octet-stream";
    private const int MaximumResponseFrameSize = 256 * 1024 * 1024;

    private readonly Uri _endpoint;
    private readonly string _authToken;
    private readonly HttpClient _httpClient;
    private readonly bool _disposeHttpClient;
    private readonly SemaphoreSlim _requestLock = new(1, 1);
    private readonly CancellationTokenSource _disposeCancellationTokenSource = new();
#if NET9_0_OR_GREATER
    private readonly Lock _lifecycleLock = new();
#else
    private readonly object _lifecycleLock = new();
#endif

    private volatile bool _connected;
    private volatile bool _disposed;
    private int _activeOperations;
    private bool _disposeCancellationCompleted;
    private bool _cleanupCompleted;

    public DotnetTestHttpClient(Uri endpoint, string authToken)
        : this(
            endpoint,
            authToken,
            new HttpClient(new HttpClientHandler { AllowAutoRedirect = false }),
            disposeHttpClient: true)
    {
    }

    internal DotnetTestHttpClient(Uri endpoint, string authToken, HttpClient httpClient, bool disposeHttpClient)
    {
        _endpoint = endpoint;
        _authToken = authToken;
        _httpClient = httpClient;
        _httpClient.Timeout = Timeout.InfiniteTimeSpan;
        _disposeHttpClient = disposeHttpClient;
    }

    public bool IsConnected => _connected && !_disposed;

    public Task ConnectAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (_lifecycleLock)
        {
            ThrowIfDisposed();

            // HTTP has no persistent connection to establish. The first protocol request is the handshake POST,
            // avoiding a separate health probe and its additional browser preflight.
            _connected = true;
        }

        return Task.CompletedTask;
    }

    public async Task<TResponse> RequestReplyAsync<TRequest, TResponse>(TRequest request, CancellationToken cancellationToken)
        where TRequest : IRequest
        where TResponse : IResponse
    {
        CancellationTokenSource operationCancellationTokenSource;
        lock (_lifecycleLock)
        {
            ThrowIfDisposed();
            if (!_connected)
            {
                throw new InvalidOperationException("The dotnet test HTTP transport is not connected.");
            }

            operationCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken,
                _disposeCancellationTokenSource.Token);
            _activeOperations++;
        }

        bool requestLockAcquired = false;
        try
        {
            await _requestLock.WaitAsync(operationCancellationTokenSource.Token).ConfigureAwait(false);
            requestLockAcquired = true;
            operationCancellationTokenSource.Token.ThrowIfCancellationRequested();
            ThrowIfDisposed();

            using var framedRequest = new MemoryStream();
            await WriteMessageAsync(
                framedRequest,
                GetSerializer(typeof(TRequest)),
                request,
                operationCancellationTokenSource.Token).ConfigureAwait(false);

            var content = new ByteArrayContent(framedRequest.ToArray());
            content.Headers.ContentType = new MediaTypeHeaderValue(MediaType);

            var httpRequest = new HttpRequestMessage(HttpMethod.Post, _endpoint)
            {
                Content = content,
            };
            httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _authToken);

            Task<HttpResponseMessage> sendTask = _httpClient.SendAsync(
                httpRequest,
                HttpCompletionOption.ResponseHeadersRead,
                operationCancellationTokenSource.Token);
            bool disposeRequest = true;
            try
            {
                using HttpResponseMessage httpResponse = await sendTask.WaitAsync(operationCancellationTokenSource.Token).ConfigureAwait(false);

                if (!httpResponse.IsSuccessStatusCode)
                {
                    throw new IOException(
                        $"The dotnet test HTTP gateway returned status code {(int)httpResponse.StatusCode}.");
                }

                HttpContent? responseContent = httpResponse.Content;
                string? responseMediaType = responseContent?.Headers.ContentType?.MediaType;
                if (responseContent is null || !MediaType.Equals(responseMediaType, StringComparison.OrdinalIgnoreCase))
                {
                    throw new IOException(
                        $"The dotnet test HTTP gateway returned content type '{responseMediaType ?? "missing"}' instead of '{MediaType}'.");
                }

                if (responseContent.Headers.ContentLength is long contentLength
                    && (contentLength < (sizeof(int) * 2) || contentLength > MaximumResponseFrameSize))
                {
                    throw new IOException($"The dotnet test HTTP gateway returned an invalid response frame length of {contentLength} bytes.");
                }

#if NET
                using Stream responseStream = await responseContent.ReadAsStreamAsync(operationCancellationTokenSource.Token).ConfigureAwait(false);
#else
                using Stream responseStream = await responseContent.ReadAsStreamAsync().ConfigureAwait(false);
#endif
                object response = await ReadNextMessageAsync(
                    responseStream,
                    operationCancellationTokenSource.Token,
                    MaximumResponseFrameSize).ConfigureAwait(false)
                    ?? throw new IOException("The dotnet test HTTP gateway returned an empty or truncated response frame.");

                byte[] trailingByte = new byte[1];
                int trailingByteCount = await responseStream.ReadAsync(
                    trailingByte,
                    0,
                    trailingByte.Length,
                    operationCancellationTokenSource.Token).ConfigureAwait(false);
                return (trailingByteCount, response) switch
                {
                    (not 0, _) => throw new IOException("The dotnet test HTTP gateway returned more than one response frame."),
                    (0, TResponse typedResponse) => typedResponse,
                    _ => throw new IOException(
                        $"The dotnet test HTTP gateway returned '{response.GetType().Name}' when '{typeof(TResponse).Name}' was expected."),
                };
            }
            catch (OperationCanceledException)
            {
                AbortTransportAfterCancellation();
                if (!sendTask.IsCompleted)
                {
                    ObserveAndDisposeIncompleteSend(sendTask, httpRequest);
                    disposeRequest = false;
                    requestLockAcquired = false;
                }

                throw;
            }
            finally
            {
                if (disposeRequest)
                {
                    httpRequest.Dispose();
                }
            }
        }
        finally
        {
            if (requestLockAcquired)
            {
                _requestLock.Release();
            }

            operationCancellationTokenSource.Dispose();
            CompleteOperation();
        }
    }

    public void Dispose()
    {
        lock (_lifecycleLock)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _connected = false;
        }

        try
        {
            _disposeCancellationTokenSource.Cancel();
        }
        finally
        {
            lock (_lifecycleLock)
            {
                _disposeCancellationCompleted = true;
                TryCleanup();
            }
        }
    }

    private void CompleteOperation()
    {
        lock (_lifecycleLock)
        {
            _activeOperations--;
            TryCleanup();
        }
    }

    private void AbortTransportAfterCancellation()
    {
        lock (_lifecycleLock)
        {
            _connected = false;
        }

        _disposeCancellationTokenSource.Cancel();
        _httpClient.CancelPendingRequests();
        if (_disposeHttpClient)
        {
            _httpClient.Dispose();
        }
    }

    private static void ObserveAndDisposeIncompleteSend(Task<HttpResponseMessage> sendTask, HttpRequestMessage request)
        => _ = sendTask.ContinueWith(
            static (task, state) =>
            {
                if (task.Status == TaskStatus.RanToCompletion)
                {
                    task.Result.Dispose();
                }
                else if (task.IsFaulted)
                {
                    _ = task.Exception;
                }

                ((HttpRequestMessage)state!).Dispose();
            },
            request,
            CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);

    private void TryCleanup()
    {
        if (_cleanupCompleted || !_disposed || !_disposeCancellationCompleted || _activeOperations != 0)
        {
            return;
        }

        _cleanupCompleted = true;
        _requestLock.Dispose();
        _disposeCancellationTokenSource.Dispose();
        DisposeBuffers();
        if (_disposeHttpClient)
        {
            _httpClient.Dispose();
        }
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(DotnetTestHttpClient));
        }
    }
}
