// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Net;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

using Microsoft.Testing.Platform;
using Microsoft.Testing.Platform.Helpers;

namespace Microsoft.Testing.Extensions.AzureDevOpsReport;

internal interface IAzureDevOpsHistoryClient
{
    Task<IReadOnlyList<AzureDevOpsTestRun>> GetRunsAsync(AzureDevOpsHistoryQuery query, int maximumRunCount, CancellationToken cancellationToken);

    Task<AzureDevOpsTestResultsPage> GetResultsAsync(AzureDevOpsHistoryQuery query, string runUrl, int skip, int top, string? continuationToken, CancellationToken cancellationToken);
}

internal sealed class AzureDevOpsHistoryClient : IAzureDevOpsHistoryClient
{
    private const int BaseDelayMs = 500;
    private const int ErrorContentMaxLength = 500;
    private const int MaxAttempts = 3;
    private const int RunsPageSize = 200;
    private const string ApiVersion = "7.1";
    private const string ContinuationTokenHeaderName = "x-ms-continuationtoken";
    private static readonly System.Net.Http.Headers.MediaTypeWithQualityHeaderValue JsonMediaType = new("application/json");
    private static readonly System.Net.Http.Headers.ProductInfoHeaderValue UserAgent = new("Microsoft.Testing.Extensions.AzureDevOpsReport", ExtensionVersion.DefaultSemVer);
    private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(20);
    private static readonly HttpClient SharedHttpClient = new();

    private readonly ITask _task;
    private readonly IClock _clock;
    private readonly HttpClient _httpClient;

    public AzureDevOpsHistoryClient(ITask task, IClock clock, HttpClient? httpClient = null)
    {
        _task = task;
        _clock = clock;
        _httpClient = httpClient ?? SharedHttpClient;
    }

    public async Task<IReadOnlyList<AzureDevOpsTestRun>> GetRunsAsync(AzureDevOpsHistoryQuery query, int maximumRunCount, CancellationToken cancellationToken)
    {
        if (maximumRunCount <= 0)
        {
            return [];
        }

        List<AzureDevOpsTestRun> runs = [];
        string? continuationToken = null;
        string? previousContinuationToken = null;
        int skip = 0;

        while (runs.Count < maximumRunCount)
        {
            int top = Math.Min(RunsPageSize, maximumRunCount - runs.Count);
            AzureDevOpsTestRunsPage page = await SendWithRetryAsync(
                () => CreateRunsRequest(query, skip, top, continuationToken),
                ParseRunsAsync,
                cancellationToken).ConfigureAwait(false);

            if (page.Runs.Count == 0)
            {
                return runs;
            }

            foreach (AzureDevOpsTestRun run in page.Runs)
            {
                runs.Add(run);
                if (runs.Count == maximumRunCount)
                {
                    return runs;
                }
            }

            if (page.ContinuationToken is null)
            {
                if (continuationToken is not null || page.Runs.Count < top)
                {
                    return runs;
                }

                skip += page.Runs.Count;
                continue;
            }

            if (page.ContinuationToken == previousContinuationToken)
            {
                return runs;
            }

            previousContinuationToken = page.ContinuationToken;
            continuationToken = page.ContinuationToken;
        }

        return runs;
    }

    public Task<AzureDevOpsTestResultsPage> GetResultsAsync(AzureDevOpsHistoryQuery query, string runUrl, int skip, int top, string? continuationToken, CancellationToken cancellationToken)
        => SendWithRetryAsync(() => CreateResultsRequest(query, runUrl, skip, top, continuationToken), ParseResultsAsync, cancellationToken);

    internal static string CreateRunsRequestUri(AzureDevOpsHistoryQuery query, int skip, int top, string? continuationToken = null)
    {
        string collectionUri = EnsureTrailingSlash(query.CollectionUri);
        System.Text.StringBuilder builder = new();
        builder
            .Append(collectionUri)
            .Append(Uri.EscapeDataString(query.TeamProject))
            .Append("/_apis/test/Runs")
            .Append("?minLastUpdatedDate=")
            .Append(Uri.EscapeDataString(query.MinLastUpdatedDate.ToString("O", CultureInfo.InvariantCulture)))
            .Append("&maxLastUpdatedDate=")
            .Append(Uri.EscapeDataString(query.MaxLastUpdatedDate.ToString("O", CultureInfo.InvariantCulture)))
            .Append("&definitions=")
            .Append(Uri.EscapeDataString(query.BuildDefinitionId))
            .Append("&automated=true")
            .Append("&$top=")
            .Append(top.ToString(CultureInfo.InvariantCulture));

        if (continuationToken is null)
        {
            builder.Append("&$skip=").Append(skip.ToString(CultureInfo.InvariantCulture));
        }
        else
        {
            builder.Append("&continuationToken=").Append(Uri.EscapeDataString(continuationToken));
        }

        builder.Append("&api-version=").Append(ApiVersion);
        return builder.ToString();
    }

    private static HttpRequestMessage CreateRunsRequest(AzureDevOpsHistoryQuery query, int skip, int top, string? continuationToken)
        => CreateRequest(query.AccessToken, CreateRunsRequestUri(query, skip, top, continuationToken));

    private static HttpRequestMessage CreateResultsRequest(AzureDevOpsHistoryQuery query, string runUrl, int skip, int top, string? continuationToken)
        => CreateRequest(query.AccessToken, CreateResultsRequestUri(runUrl, skip, top, continuationToken));

    private static string CreateResultsRequestUri(string runUrl, int skip, int top, string? continuationToken)
    {
        System.Text.StringBuilder builder = new();
        builder
            .Append(runUrl.TrimEnd('/'))
            .Append("/results?outcomes=Failed,Passed")
            .Append("&$top=")
            .Append(top.ToString(CultureInfo.InvariantCulture));

        if (continuationToken is null)
        {
            builder.Append("&$skip=").Append(skip.ToString(CultureInfo.InvariantCulture));
        }
        else
        {
            builder.Append("&continuationToken=").Append(Uri.EscapeDataString(continuationToken));
        }

        builder.Append("&api-version=").Append(ApiVersion);
        return builder.ToString();
    }

    private static HttpRequestMessage CreateRequest(string accessToken, string requestUri)
    {
        HttpRequestMessage request = new(HttpMethod.Get, requestUri);
        string encodedToken = Convert.ToBase64String(System.Text.Encoding.ASCII.GetBytes($":{accessToken}"));
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", encodedToken);
        request.Headers.Accept.Add(JsonMediaType);
        request.Headers.UserAgent.Add(UserAgent);
        return request;
    }

    private async Task<T> SendWithRetryAsync<T>(Func<HttpRequestMessage> requestFactory, Func<HttpResponseMessage, Task<T>> responseParser, CancellationToken cancellationToken)
    {
        for (int attempt = 1; ; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                using HttpRequestMessage request = requestFactory();
                using var timeoutCancellationTokenSource = new CancellationTokenSource(RequestTimeout);
                using var linkedCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCancellationTokenSource.Token);
                using HttpResponseMessage response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, linkedCancellationTokenSource.Token).ConfigureAwait(false);
                if (response.IsSuccessStatusCode)
                {
                    return await responseParser(response).ConfigureAwait(false);
                }

                if (attempt >= MaxAttempts || !IsTransient(response.StatusCode))
                {
                    string responseContent = await ReadResponseContentAsync(response).ConfigureAwait(false);
                    string reasonPhrase = RoslynString.IsNullOrWhiteSpace(response.ReasonPhrase) ? response.StatusCode.ToString() : response.ReasonPhrase!;
                    string responseSuffix = responseContent.Length == 0 ? string.Empty : $" {responseContent}";
                    throw new HttpRequestException($"Azure DevOps history request failed with status code {(int)response.StatusCode} ({reasonPhrase}).{responseSuffix}");
                }

                await _task.Delay(GetRetryDelay(response, attempt), cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested && attempt < MaxAttempts)
            {
                await _task.Delay(GetRetryDelay(response: null, attempt), cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException ex) when (!cancellationToken.IsCancellationRequested)
            {
                throw new HttpRequestException("Azure DevOps history request timed out.", ex);
            }
            catch (HttpRequestException) when (attempt < MaxAttempts)
            {
                await _task.Delay(GetRetryDelay(response: null, attempt), cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private TimeSpan GetRetryDelay(HttpResponseMessage? response, int attempt)
    {
        if (response?.Headers.RetryAfter?.Delta is TimeSpan retryAfterDelta)
        {
            return retryAfterDelta;
        }

        if (response?.Headers.RetryAfter?.Date is DateTimeOffset retryAfterDate)
        {
            TimeSpan delay = retryAfterDate - _clock.UtcNow;
            if (delay > TimeSpan.Zero)
            {
                return delay;
            }
        }

        return TimeSpan.FromMilliseconds(BaseDelayMs * Math.Pow(2, attempt - 1));
    }

    private static bool IsTransient(HttpStatusCode statusCode)
        => statusCode is HttpStatusCode.RequestTimeout
            or HttpStatusCode.InternalServerError
            or HttpStatusCode.BadGateway
            or HttpStatusCode.ServiceUnavailable
            or HttpStatusCode.GatewayTimeout
            || (int)statusCode == 429;

    private static async Task<AzureDevOpsTestRunsPage> ParseRunsAsync(HttpResponseMessage response)
    {
        AzureDevOpsRunsResponse? payload = await DeserializeResponseAsync(response, AzureDevOpsHistoryClientJsonContext.Default.AzureDevOpsRunsResponse).ConfigureAwait(false);
        return payload?.Value is null
            ? new AzureDevOpsTestRunsPage([], GetContinuationToken(response))
            : new AzureDevOpsTestRunsPage(
                [.. payload.Value
                    .Where(static run => !RoslynString.IsNullOrWhiteSpace(run.Url))
                    .Select(static run => new AzureDevOpsTestRun(run.Url!))],
                GetContinuationToken(response));
    }

    private static async Task<AzureDevOpsTestResultsPage> ParseResultsAsync(HttpResponseMessage response)
    {
        AzureDevOpsResultsResponse? payload = await DeserializeResponseAsync(response, AzureDevOpsHistoryClientJsonContext.Default.AzureDevOpsResultsResponse).ConfigureAwait(false);
        string? continuationToken = GetContinuationToken(response);

        return payload?.Value is null
            ? new AzureDevOpsTestResultsPage([], continuationToken)
            : new AzureDevOpsTestResultsPage(
                [.. payload.Value
                    .Where(static result => !RoslynString.IsNullOrWhiteSpace(result.AutomatedTestName) && !RoslynString.IsNullOrWhiteSpace(result.Outcome))
                    .Select(static result => new AzureDevOpsTestResult(result.AutomatedTestName!, result.Outcome!))],
                continuationToken);
    }

    private static async Task<T?> DeserializeResponseAsync<T>(HttpResponseMessage response, JsonTypeInfo<T> jsonTypeInfo)
    {
#pragma warning disable CA2016 // CancellationToken overload is unavailable on all target frameworks.
        string content = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
#pragma warning restore CA2016
        return System.Text.Json.JsonSerializer.Deserialize(content, jsonTypeInfo);
    }

    private static string EnsureTrailingSlash(string value)
        => value.EndsWith("/", StringComparison.Ordinal) ? value : value + "/";

    private static string? GetContinuationToken(HttpResponseMessage response)
        => response.Headers.TryGetValues(ContinuationTokenHeaderName, out IEnumerable<string>? values)
            ? values.FirstOrDefault()
            : null;

    private static async Task<string> ReadResponseContentAsync(HttpResponseMessage response)
    {
#pragma warning disable CA2016 // CancellationToken overload is unavailable on all target frameworks.
        string responseContent = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
#pragma warning restore CA2016
        return responseContent.Length <= ErrorContentMaxLength
            ? responseContent
            : responseContent.Substring(0, ErrorContentMaxLength);
    }
}

internal sealed class AzureDevOpsHistoryQuery
{
    public AzureDevOpsHistoryQuery(string collectionUri, string teamProject, string accessToken, string buildDefinitionId, DateTimeOffset minLastUpdatedDate, DateTimeOffset maxLastUpdatedDate)
    {
        CollectionUri = collectionUri;
        TeamProject = teamProject;
        AccessToken = accessToken;
        BuildDefinitionId = buildDefinitionId;
        MinLastUpdatedDate = minLastUpdatedDate;
        MaxLastUpdatedDate = maxLastUpdatedDate;
    }

    public string CollectionUri { get; }

    public string TeamProject { get; }

    public string AccessToken { get; }

    public string BuildDefinitionId { get; }

    public DateTimeOffset MinLastUpdatedDate { get; }

    public DateTimeOffset MaxLastUpdatedDate { get; }
}

internal sealed class AzureDevOpsRunResponse
{
    [JsonPropertyName("url")]
    public string? Url { get; set; }
}

internal sealed class AzureDevOpsRunsResponse
{
    [JsonPropertyName("value")]
    public AzureDevOpsRunResponse[]? Value { get; set; }
}

internal sealed class AzureDevOpsTestRun
{
    public AzureDevOpsTestRun(string url)
        => Url = url;

    public string Url { get; }
}

internal sealed class AzureDevOpsTestRunsPage
{
    public AzureDevOpsTestRunsPage(IReadOnlyList<AzureDevOpsTestRun> runs, string? continuationToken)
    {
        Runs = runs;
        ContinuationToken = continuationToken;
    }

    public IReadOnlyList<AzureDevOpsTestRun> Runs { get; }

    public string? ContinuationToken { get; }
}

internal sealed class AzureDevOpsResultResponse
{
    [JsonPropertyName("automatedTestName")]
    public string? AutomatedTestName { get; set; }

    [JsonPropertyName("outcome")]
    public string? Outcome { get; set; }
}

internal sealed class AzureDevOpsResultsResponse
{
    [JsonPropertyName("value")]
    public AzureDevOpsResultResponse[]? Value { get; set; }
}

internal sealed class AzureDevOpsTestResult
{
    public AzureDevOpsTestResult(string automatedTestName, string outcome)
    {
        AutomatedTestName = automatedTestName;
        Outcome = outcome;
    }

    public string AutomatedTestName { get; }

    public string Outcome { get; }
}

internal sealed class AzureDevOpsTestResultsPage
{
    public AzureDevOpsTestResultsPage(IReadOnlyList<AzureDevOpsTestResult> results, string? continuationToken)
    {
        Results = results;
        ContinuationToken = continuationToken;
    }

    public IReadOnlyList<AzureDevOpsTestResult> Results { get; }

    public string? ContinuationToken { get; }
}
