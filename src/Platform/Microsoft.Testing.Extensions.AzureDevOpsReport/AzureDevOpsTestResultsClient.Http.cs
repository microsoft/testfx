// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Net;
using System.Net.Sockets;
using System.Text.Json;

using Microsoft.Testing.Extensions.AzureDevOpsReport.Resources;

#if !NETCOREAPP
using Polyfills;
#endif

namespace Microsoft.Testing.Extensions.AzureDevOpsReport;

internal sealed partial class AzureDevOpsTestResultsClient
{
    private static HttpClient CreateHttpClient()
        => new(CreateHttpClientHandler(), disposeHandler: false)
        {
            Timeout = Timeout.InfiniteTimeSpan,
        };

    /// <summary>
    /// Creates the handler backing the shared <see cref="HttpClient"/>, opting into transparent
    /// response decompression only where the platform supports it.
    /// </summary>
    /// <remarks>
    /// The <c>browser</c> and <c>wasi</c> handlers delegate to <c>fetch</c> / <c>wasi:http</c>, which
    /// decode <c>gzip</c>/<c>deflate</c> responses themselves, and their
    /// <see cref="HttpClientHandler.AutomaticDecompression"/> setter throws
    /// <see cref="PlatformNotSupportedException"/>. Skipping the opt-in there is therefore not a
    /// behavior change. See <see href="https://github.com/microsoft/testfx/issues/10313"/>.
    /// </remarks>
    internal static HttpClientHandler CreateHttpClientHandler()
    {
        HttpClientHandler handler = new();
        if (!OperatingSystem.IsWasi())
        {
            handler.AllowAutoRedirect = false;
        }

        if (ShouldOptInToAutomaticDecompression(handler))
        {
            handler.AutomaticDecompression = DecompressionMethods.Deflate | DecompressionMethods.GZip;
        }

        return handler;
    }

    /// <summary>
    /// Determines whether <see cref="HttpClientHandler.AutomaticDecompression"/> can be set on
    /// <paramref name="handler"/>. The <c>OperatingSystem.IsBrowser()</c> check is what the
    /// platform-compatibility annotation on the property requires; the
    /// <see cref="HttpClientHandler.SupportsAutomaticDecompression"/> probe additionally covers the
    /// other handlers (for example <c>wasi</c>) that do not implement it.
    /// </summary>
    internal static bool ShouldOptInToAutomaticDecompression(HttpClientHandler handler)
        => !OperatingSystem.IsBrowser() && handler.SupportsAutomaticDecompression;

    [UnconditionalSuppressMessage("ReflectionAnalysis", "IL2026", Justification = "Response types are internal, fixed, and controlled by this extension.")]
    [UnconditionalSuppressMessage("Aot", "IL3050", Justification = "Response types are internal, fixed, and controlled by this extension.")]
    private async Task<TResponse> SendAsync<TResponse>(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        using var requestTimeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        requestTimeoutSource.CancelAfter(RequestTimeout);

        using HttpResponseMessage response = await SendCoreAsync(request, requestTimeoutSource.Token, cancellationToken, AttemptTimeout).ConfigureAwait(false);
        string payload = await ReadAsStringAsync(response.Content, requestTimeoutSource.Token).ConfigureAwait(false);
        TResponse? deserialized = JsonSerializer.Deserialize<TResponse>(payload, JsonSerializerOptions);
        return deserialized ?? throw new InvalidOperationException(AzureDevOpsResources.AzureDevOpsLivePublishingInvalidResponse);
    }

    private async Task SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        using var requestTimeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        requestTimeoutSource.CancelAfter(RequestTimeout);
        using HttpResponseMessage ignoredResponse = await SendCoreAsync(request, requestTimeoutSource.Token, cancellationToken, AttemptTimeout).ConfigureAwait(false);
    }

    private async Task SendAsync(HttpRequestMessage request, CancellationToken cancellationToken, TimeSpan requestTimeout)
    {
        using var requestTimeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        requestTimeoutSource.CancelAfter(requestTimeout);
        using HttpResponseMessage ignoredResponse = await SendCoreAsync(request, requestTimeoutSource.Token, cancellationToken, requestTimeout).ConfigureAwait(false);
    }

    private async Task<HttpResponseMessage> SendCoreAsync(HttpRequestMessage request, CancellationToken requestCancellationToken, CancellationToken userCancellationToken, TimeSpan attemptTimeout)
    {
        Exception? lastException = null;

        try
        {
            for (int attempt = 1; attempt <= MaxAttempts; attempt++)
            {
                using HttpRequestMessage currentRequest = await CloneAsync(request, requestCancellationToken).ConfigureAwait(false);
                using var attemptTimeoutSource = CancellationTokenSource.CreateLinkedTokenSource(requestCancellationToken);
                attemptTimeoutSource.CancelAfter(attemptTimeout);

                try
                {
                    HttpResponseMessage response = await _httpClient.SendAsync(currentRequest, HttpCompletionOption.ResponseHeadersRead, attemptTimeoutSource.Token).ConfigureAwait(false);
                    if (response.IsSuccessStatusCode)
                    {
                        if (string.Equals(response.Content.Headers.ContentType?.MediaType, "text/html", StringComparison.OrdinalIgnoreCase))
                        {
                            int statusCode = (int)response.StatusCode;
                            string contentType = response.Content.Headers.ContentType?.ToString() ?? "text/html";
                            response.Dispose();
                            throw new InvalidOperationException(string.Format(
                                CultureInfo.InvariantCulture,
                                AzureDevOpsResources.AzureDevOpsLivePublishingUnexpectedContentType,
                                statusCode,
                                contentType));
                        }

                        return response;
                    }

                    TimeSpan delay;
                    try
                    {
                        if (!ShouldRetry(response.StatusCode, attempt))
                        {
                            if (IsAuthenticationFailure(response))
                            {
                                string status = response.StatusCode == 0
                                    ? response.ReasonPhrase ?? "opaqueredirect"
                                    : ((int)response.StatusCode).ToString(CultureInfo.InvariantCulture);
                                throw new InvalidOperationException(string.Format(
                                    CultureInfo.InvariantCulture,
                                    AzureDevOpsResources.AzureDevOpsLivePublishingAuthenticationFailure,
                                    status));
                            }

                            string responseBody = await ReadAsStringAsync(response.Content, requestCancellationToken).ConfigureAwait(false);
                            throw new InvalidOperationException(string.Format(CultureInfo.InvariantCulture, AzureDevOpsResources.AzureDevOpsLivePublishingHttpError, (int)response.StatusCode, responseBody));
                        }

                        delay = GetDelay(response, attempt);
                    }
                    finally
                    {
                        response.Dispose();
                    }

                    await _task.Delay(delay, requestCancellationToken).ConfigureAwait(false);
                }
                catch (Exception ex) when (ShouldRetry(ex, userCancellationToken, requestCancellationToken, attempt))
                {
                    lastException = ex;
                    await _task.Delay(GetExponentialBackoffDelay(attempt), requestCancellationToken).ConfigureAwait(false);
                }
            }
        }
        catch (OperationCanceledException) when (!userCancellationToken.IsCancellationRequested)
        {
            // An internal timeout (per-attempt or request-level) fired on the final retry attempt.
            // Convert to a non-cancellation exception so publishing failures never propagate as
            // OperationCanceledException and fault the data consumer.
            throw new InvalidOperationException(AzureDevOpsResources.AzureDevOpsLivePublishingRequestFailed, lastException);
        }

        throw new InvalidOperationException(AzureDevOpsResources.AzureDevOpsLivePublishingRequestFailed, lastException);
    }

    private static bool ShouldRetry(HttpStatusCode statusCode, int attempt)
        => attempt < MaxAttempts && ((int)statusCode is >= 500 or 429);

    private static bool IsAuthenticationFailure(HttpResponseMessage response)
        => response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Redirect
            || (response.StatusCode == 0 && string.Equals(response.ReasonPhrase, "opaqueredirect", StringComparison.Ordinal));

    private static bool ShouldRetry(Exception exception, CancellationToken userCancellationToken, CancellationToken requestCancellationToken, int attempt)
        => attempt < MaxAttempts
            && !userCancellationToken.IsCancellationRequested
            && !requestCancellationToken.IsCancellationRequested
            && exception is HttpRequestException or IOException or SocketException or TaskCanceledException;

    private TimeSpan GetDelay(HttpResponseMessage response, int attempt)
    {
        if (response.StatusCode == (HttpStatusCode)429 && response.Headers.RetryAfter is { } retryAfter)
        {
            if (retryAfter.Delta is { } delta && delta > TimeSpan.Zero)
            {
                return delta;
            }

            if (retryAfter.Date is { } date)
            {
                TimeSpan delay = date - _clock.UtcNow;
                if (delay > TimeSpan.Zero)
                {
                    return delay;
                }
            }
        }

        return GetExponentialBackoffDelay(attempt);
    }

    private static TimeSpan GetExponentialBackoffDelay(int attempt)
        => TimeSpan.FromMilliseconds(BaseDelayMilliseconds * Math.Pow(2, attempt - 1));

    private static async Task<HttpRequestMessage> CloneAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        HttpRequestMessage clone = new(request.Method, request.RequestUri)
        {
            Version = request.Version,
#if NET
            VersionPolicy = request.VersionPolicy,
#endif
        };

#if NET
        foreach (KeyValuePair<string, object?> option in request.Options)
        {
            clone.Options.Set(new HttpRequestOptionsKey<object?>(option.Key), option.Value);
        }
#endif

        foreach (KeyValuePair<string, IEnumerable<string>> header in request.Headers)
        {
            clone.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }

        if (request.Content is not null)
        {
            byte[] payload = await ReadAsByteArrayAsync(request.Content, cancellationToken).ConfigureAwait(false);
            clone.Content = new ByteArrayContent(payload);

            foreach (KeyValuePair<string, IEnumerable<string>> header in request.Content.Headers)
            {
                clone.Content.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }
        }

        return clone;
    }

    private static Task<byte[]> ReadAsByteArrayAsync(HttpContent content, CancellationToken cancellationToken)
#if NET
        => content.ReadAsByteArrayAsync(cancellationToken);
#else
        => content.ReadAsByteArrayAsync();
#endif

    private static async Task<string> ReadAsStringAsync(HttpContent content, CancellationToken cancellationToken)
    {
#if NET
        return await content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
#else
        using Stream contentStream = await content.ReadAsStreamAsync().ConfigureAwait(false);
        using MemoryStream bufferedContent = new();
        byte[] buffer = new byte[81920];
        int bytesRead;
        while ((bytesRead = await contentStream.ReadAsync(buffer, 0, buffer.Length, cancellationToken).ConfigureAwait(false)) > 0)
        {
            await bufferedContent.WriteAsync(buffer, 0, bytesRead, cancellationToken).ConfigureAwait(false);
        }

        bufferedContent.Position = 0;
        string? charset = content.Headers.ContentType?.CharSet;
        Encoding encoding = charset is { Length: > 0 }
            ? Encoding.GetEncoding(charset.Trim('"'))
            : Encoding.UTF8;
        using StreamReader reader = new(bufferedContent, encoding, detectEncodingFromByteOrderMarks: true);
        return await reader.ReadToEndAsync().ConfigureAwait(false);
#endif
    }
}
