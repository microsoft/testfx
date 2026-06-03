// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Net;
using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Security;
using System.Text.Json;
using System.Text.Json.Serialization;

using Microsoft.Testing.Extensions.AzureDevOpsReport.Resources;
using Microsoft.Testing.Platform.Helpers;

namespace Microsoft.Testing.Extensions.AzureDevOpsReport;

internal sealed class AzureDevOpsTestResultsClient : IAzureDevOpsTestResultsClient
{
    private const string ApiVersion = "7.1";
    private const int MaxAttempts = 3;
    private const int BaseDelayMilliseconds = 500;
    private static readonly TimeSpan AttemptTimeout = TimeSpan.FromSeconds(15);
    private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(60);

    // Attachments can be up to 16 MB and base64 encoding bloats them ~4/3x. Allow generously longer timeouts.
    private static readonly TimeSpan AttachmentRequestTimeout = TimeSpan.FromMinutes(5);
    private static readonly HttpMethod PatchMethod = new("PATCH");
    private static readonly JsonSerializerOptions JsonSerializerOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private static readonly HttpClient SharedHttpClient = CreateHttpClient();

    private readonly HttpClient _httpClient;
    private readonly ITask _task;
    private readonly IClock _clock;

    public AzureDevOpsTestResultsClient(ITask task, IClock clock)
        : this(SharedHttpClient, task, clock)
    {
    }

    internal AzureDevOpsTestResultsClient(HttpClient httpClient, ITask task, IClock clock)
    {
        _httpClient = httpClient;
        _task = task;
        _clock = clock;
    }

    public async Task<int> CreateTestRunAsync(AzureDevOpsPublishConfiguration configuration, CancellationToken cancellationToken)
    {
        using HttpRequestMessage request = CreateRequest(
            HttpMethod.Post,
            BuildRunsUri(configuration.CollectionUri, configuration.Project),
            configuration.AccessToken,
            new CreateTestRunRequest(configuration.RunName, true, new BuildReference(configuration.BuildId), AzureDevOpsLivePublishingConstants.InProgressTestRunState));

        CreateTestRunResponse response = await SendAsync<CreateTestRunResponse>(request, cancellationToken).ConfigureAwait(false);
        return response.Id > 0
            ? response.Id
            : throw new InvalidOperationException(AzureDevOpsResources.AzureDevOpsLivePublishingInvalidResponse);
    }

    [UnconditionalSuppressMessage("ReflectionAnalysis", "IL2026", Justification = "Response types are internal, fixed, and controlled by this extension.")]
    [UnconditionalSuppressMessage("Aot", "IL3050", Justification = "Response types are internal, fixed, and controlled by this extension.")]
    public async Task<IReadOnlyList<int>?> PublishTestResultsAsync(AzureDevOpsPublishConfiguration configuration, int runId, IReadOnlyList<AzureDevOpsTestCaseResult> results, CancellationToken cancellationToken)
    {
        using HttpRequestMessage request = CreateRequest(
            HttpMethod.Post,
            BuildResultsUri(configuration.CollectionUri, configuration.Project, runId),
            configuration.AccessToken,
            results);

        // Transport/HTTP failures throw from SendCoreAsync — caller retries.
        using var requestTimeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        requestTimeoutSource.CancelAfter(RequestTimeout);
        using HttpResponseMessage response = await SendCoreAsync(request, requestTimeoutSource.Token, cancellationToken, AttemptTimeout).ConfigureAwait(false);
        string payload = await ReadAsStringAsync(response.Content, requestTimeoutSource.Token).ConfigureAwait(false);

        // From this point on the AzDO server has accepted the results. Failing to parse the response
        // must not cause the caller to retry the publish (that would duplicate result rows).
        try
        {
            PublishTestResultsResponse? parsed = JsonSerializer.Deserialize<PublishTestResultsResponse>(payload, JsonSerializerOptions);
            if (parsed?.Value is null || parsed.Value.Length != results.Count)
            {
                return null;
            }

            int[] ids = new int[results.Count];
            for (int i = 0; i < results.Count; i++)
            {
                if (parsed.Value[i].Id <= 0
                    || !string.Equals(parsed.Value[i].AutomatedTestName, results[i].AutomatedTestName, StringComparison.Ordinal))
                {
                    return null;
                }

                ids[i] = parsed.Value[i].Id;
            }

            return ids;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    public async Task UploadTestResultAttachmentAsync(AzureDevOpsPublishConfiguration configuration, int runId, int testCaseResultId, AzureDevOpsTestResultAttachment attachment, CancellationToken cancellationToken)
    {
        AttachmentRequest? payload = TryBuildAttachmentRequest(attachment);
        if (payload is null)
        {
            return;
        }

        using HttpRequestMessage request = CreateRequest(
            HttpMethod.Post,
            BuildResultAttachmentsUri(configuration.CollectionUri, configuration.Project, runId, testCaseResultId),
            configuration.AccessToken,
            payload);

        await SendAsync(request, cancellationToken, AttachmentRequestTimeout).ConfigureAwait(false);
    }

    public async Task UploadTestRunAttachmentAsync(AzureDevOpsPublishConfiguration configuration, int runId, AzureDevOpsTestResultAttachment attachment, CancellationToken cancellationToken)
    {
        AttachmentRequest? payload = TryBuildAttachmentRequest(attachment);
        if (payload is null)
        {
            return;
        }

        using HttpRequestMessage request = CreateRequest(
            HttpMethod.Post,
            BuildRunAttachmentsUri(configuration.CollectionUri, configuration.Project, runId),
            configuration.AccessToken,
            payload);

        await SendAsync(request, cancellationToken, AttachmentRequestTimeout).ConfigureAwait(false);
    }

    public Task UpdateTestRunStateAsync(AzureDevOpsPublishConfiguration configuration, int runId, string state, CancellationToken cancellationToken)
    {
        using HttpRequestMessage request = CreateRequest(
            PatchMethod,
            BuildRunUri(configuration.CollectionUri, configuration.Project, runId),
            configuration.AccessToken,
            new UpdateTestRunStateRequest(state));

        return SendAsync(request, cancellationToken);
    }

    private static HttpClient CreateHttpClient()
    {
        HttpClientHandler handler = new()
        {
            AutomaticDecompression = DecompressionMethods.Deflate | DecompressionMethods.GZip,
        };

        return new HttpClient(handler, disposeHandler: false)
        {
            Timeout = Timeout.InfiniteTimeSpan,
        };
    }

    private static Uri BuildRunsUri(string collectionUri, string project)
        => new(new Uri(collectionUri, UriKind.Absolute), $"{Uri.EscapeDataString(project)}/_apis/test/runs?api-version={ApiVersion}");

    private static Uri BuildRunUri(string collectionUri, string project, int runId)
        => new(new Uri(collectionUri, UriKind.Absolute), $"{Uri.EscapeDataString(project)}/_apis/test/runs/{runId}?api-version={ApiVersion}");

    private static Uri BuildResultsUri(string collectionUri, string project, int runId)
        => new(new Uri(collectionUri, UriKind.Absolute), $"{Uri.EscapeDataString(project)}/_apis/test/runs/{runId}/results?api-version={ApiVersion}");

    private static Uri BuildResultAttachmentsUri(string collectionUri, string project, int runId, int testCaseResultId)
        => new(new Uri(collectionUri, UriKind.Absolute), $"{Uri.EscapeDataString(project)}/_apis/test/runs/{runId}/results/{testCaseResultId}/attachments?api-version={ApiVersion}");

    private static Uri BuildRunAttachmentsUri(string collectionUri, string project, int runId)
        => new(new Uri(collectionUri, UriKind.Absolute), $"{Uri.EscapeDataString(project)}/_apis/test/runs/{runId}/attachments?api-version={ApiVersion}");

    private static AttachmentRequest? TryBuildAttachmentRequest(AzureDevOpsTestResultAttachment attachment)
    {
        byte[]? bytes;
        if (attachment.FilePath is { Length: > 0 } filePath)
        {
            FileInfo fileInfo;
            try
            {
                fileInfo = new FileInfo(filePath);
                if (!fileInfo.Exists)
                {
                    return null;
                }

                if (fileInfo.Length > AzureDevOpsLivePublishingConstants.MaxAttachmentSizeBytes)
                {
                    return null;
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or SecurityException or PathTooLongException)
            {
                return null;
            }

            try
            {
                bytes = File.ReadAllBytes(filePath);
                if (bytes.Length > AzureDevOpsLivePublishingConstants.MaxAttachmentSizeBytes)
                {
                    return null;
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or SecurityException or PathTooLongException)
            {
                return null;
            }
        }
        else if (attachment.InlineContent is { } inline)
        {
            // Inline content (stdout/stderr) is already truncated by the publisher to MaxInlineAttachmentBytes.
            bytes = Encoding.UTF8.GetBytes(inline);
        }
        else
        {
            return null;
        }

        return new AttachmentRequest(
            Convert.ToBase64String(bytes),
            attachment.FileName,
            attachment.Comment,
            attachment.AttachmentType);
    }

    [UnconditionalSuppressMessage("ReflectionAnalysis", "IL2026", Justification = "Payload types are internal, fixed, and controlled by this extension.")]
    [UnconditionalSuppressMessage("Aot", "IL3050", Justification = "Payload types are internal, fixed, and controlled by this extension.")]
    private static HttpRequestMessage CreateRequest<TPayload>(HttpMethod method, Uri uri, string accessToken, TPayload payload)
    {
        HttpRequestMessage request = CreateRequest(method, uri, accessToken);
        request.Content = new StringContent(JsonSerializer.Serialize(payload, JsonSerializerOptions), Encoding.UTF8, "application/json");
        return request;
    }

    private static HttpRequestMessage CreateRequest(HttpMethod method, Uri uri, string accessToken)
    {
        HttpRequestMessage request = new(method, uri);
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes($":{accessToken}")));
        request.Headers.Accept.ParseAdd($"application/json; api-version={ApiVersion}");
        return request;
    }

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
                    HttpResponseMessage response = await _httpClient.SendAsync(currentRequest, attemptTimeoutSource.Token).ConfigureAwait(false);
                    if (response.IsSuccessStatusCode)
                    {
                        return response;
                    }

                    if (!ShouldRetry(response.StatusCode, attempt))
                    {
                        string responseBody = await ReadAsStringAsync(response.Content, requestCancellationToken).ConfigureAwait(false);
                        response.Dispose();
                        throw new InvalidOperationException(string.Format(CultureInfo.InvariantCulture, AzureDevOpsResources.AzureDevOpsLivePublishingHttpError, (int)response.StatusCode, responseBody));
                    }

                    TimeSpan delay = GetDelay(response, attempt);
                    response.Dispose();
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

    private static Task<string> ReadAsStringAsync(HttpContent content, CancellationToken cancellationToken)
#if NET
        => content.ReadAsStringAsync(cancellationToken);
#else
        => content.ReadAsStringAsync();
#endif

    private sealed record CreateTestRunRequest(
        [property: JsonPropertyName("name")] string Name,
        [property: JsonPropertyName("automated")] bool Automated,
        [property: JsonPropertyName("build")] BuildReference Build,
        [property: JsonPropertyName("state")] string State);

    private sealed record BuildReference([property: JsonPropertyName("id")] int Id);

    private sealed record CreateTestRunResponse([property: JsonPropertyName("id")] int Id);

    private sealed record UpdateTestRunStateRequest([property: JsonPropertyName("state")] string State);

    private sealed record PublishTestResultsResponse(
        [property: JsonPropertyName("count")] int Count,
        [property: JsonPropertyName("value")] PublishedTestResult[]? Value);

    private sealed record PublishedTestResult(
        [property: JsonPropertyName("id")] int Id,
        [property: JsonPropertyName("automatedTestName")] string? AutomatedTestName);

    private sealed record AttachmentRequest(
        [property: JsonPropertyName("stream")] string Stream,
        [property: JsonPropertyName("fileName")] string FileName,
        [property: JsonPropertyName("comment")] string? Comment,
        [property: JsonPropertyName("attachmentType")] string AttachmentType);
}
