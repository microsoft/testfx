// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Text.Json;
using System.Text.Json.Serialization;

using Microsoft.Testing.Extensions.AzureDevOpsReport.Resources;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.Logging;

namespace Microsoft.Testing.Extensions.AzureDevOpsReport;

internal sealed partial class AzureDevOpsTestResultsClient : IAzureDevOpsTestResultsClient
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

    private static readonly JsonSerializerOptions UpdateJsonSerializerOptions = new()
    {
        // PATCH must send null outcome details explicitly so a passing retry clears the error and stack
        // trace left by the prior failed attempt. Omitting them would leave those server fields unchanged.
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
    };

    private static readonly HttpClient SharedHttpClient = CreateHttpClient();

    private readonly HttpClient _httpClient;
    private readonly ITask _task;
    private readonly IClock _clock;
    private readonly ILogger? _logger;

    public AzureDevOpsTestResultsClient(ITask task, IClock clock)
        : this(SharedHttpClient, task, clock, logger: null)
    {
    }

    public AzureDevOpsTestResultsClient(ITask task, IClock clock, ILoggerFactory loggerFactory)
        : this(SharedHttpClient, task, clock, loggerFactory.CreateLogger<AzureDevOpsTestResultsClient>())
    {
    }

    internal AzureDevOpsTestResultsClient(HttpClient httpClient, ITask task, IClock clock)
        : this(httpClient, task, clock, logger: null)
    {
    }

    internal AzureDevOpsTestResultsClient(HttpClient httpClient, ITask task, IClock clock, ILogger? logger)
    {
        _httpClient = httpClient;
        _task = task;
        _clock = clock;
        _logger = logger;
    }

    public async Task<int> CreateTestRunAsync(AzureDevOpsPublishConfiguration configuration, CancellationToken cancellationToken)
    {
        using HttpRequestMessage request = CreateRequest(
            HttpMethod.Post,
            BuildRunsUri(configuration.CollectionUri, configuration.Project),
            configuration.AccessToken,
            new CreateTestRunRequest(
                configuration.RunName,
                true,
                new BuildReference(configuration.BuildId),
                AzureDevOpsLivePublishingConstants.InProgressTestRunState,
                _clock.UtcNow,
                BuildPipelineReference(configuration)));

        CreateTestRunResponse response = await SendAsync<CreateTestRunResponse>(request, cancellationToken).ConfigureAwait(false);
        return response.Id > 0
            ? response.Id
            : throw new InvalidOperationException(AzureDevOpsResources.AzureDevOpsLivePublishingInvalidResponse);
    }

    /// <remarks>
    /// Azure DevOps requires <c>pipelineReference.pipelineId</c> to match <c>build.id</c>; without the
    /// stage/phase/job references the run cannot be attributed to a stage of a multi-stage pipeline.
    /// </remarks>
    private static PipelineReference? BuildPipelineReference(AzureDevOpsPublishConfiguration configuration)
        => configuration.PipelineReference is not { } pipelineReference
            ? null
            : new PipelineReference(
                configuration.BuildId,
                pipelineReference.StageName is null ? null : new StageReference(pipelineReference.StageName, pipelineReference.StageAttempt),
                pipelineReference.PhaseName is null ? null : new PhaseReference(pipelineReference.PhaseName, pipelineReference.PhaseAttempt),
                pipelineReference.JobName is null ? null : new JobReference(pipelineReference.JobName, pipelineReference.JobAttempt));

    [UnconditionalSuppressMessage("ReflectionAnalysis", "IL2026", Justification = "Response types are internal, fixed, and controlled by this extension.")]
    [UnconditionalSuppressMessage("Aot", "IL3050", Justification = "Response types are internal, fixed, and controlled by this extension.")]
    public async Task<IReadOnlyList<int>?> PublishTestResultsAsync(AzureDevOpsPublishConfiguration configuration, int runId, IReadOnlyList<AzureDevOpsTestCaseResult> results, CancellationToken cancellationToken)
    {
        IReadOnlyList<AzureDevOpsPublishedTestResult>? publishedResults =
            await PublishTestResultsWithSubResultsAsync(configuration, runId, results, cancellationToken).ConfigureAwait(false);
        return publishedResults?.Select(static result => result.Id).ToArray();
    }

    [UnconditionalSuppressMessage("ReflectionAnalysis", "IL2026", Justification = "Response types are internal, fixed, and controlled by this extension.")]
    [UnconditionalSuppressMessage("Aot", "IL3050", Justification = "Response types are internal, fixed, and controlled by this extension.")]
    public async Task<IReadOnlyList<AzureDevOpsPublishedTestResult>?> PublishTestResultsWithSubResultsAsync(AzureDevOpsPublishConfiguration configuration, int runId, IReadOnlyList<AzureDevOpsTestCaseResult> results, CancellationToken cancellationToken)
    {
        using HttpRequestMessage request = CreateRequest(
            HttpMethod.Post,
            BuildResultsUri(configuration.CollectionUri, configuration.Project, runId),
            configuration.AccessToken,
            results);

        // Do not throw for an unexpected 2xx content type: Azure DevOps may have accepted this
        // non-idempotent POST, and retrying it could create duplicate result rows.
        using var requestTimeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        requestTimeoutSource.CancelAfter(RequestTimeout);
        using HttpResponseMessage response = await SendCoreAsync(request, requestTimeoutSource.Token, cancellationToken, AttemptTimeout, throwOnUnexpectedContentType: false).ConfigureAwait(false);

        // From this point on the AzDO server has accepted the results. Failing to parse the response
        // must not cause the caller to retry the publish (that would duplicate result rows).
        return await TryReadAndParsePublishedResultsAsync(response, requestTimeoutSource.Token, cancellationToken, results, validateAutomatedTestName: true).ConfigureAwait(false);
    }

    /// <summary>
    /// Updates results that were already published to the run, folding a further attempt of the same test
    /// into the result that represents it.
    /// </summary>
    public async Task UpdateTestResultsAsync(AzureDevOpsPublishConfiguration configuration, int runId, IReadOnlyList<AzureDevOpsTestCaseResult> results, CancellationToken cancellationToken)
        => _ = await UpdateTestResultsWithSubResultsAsync(configuration, runId, results, cancellationToken).ConfigureAwait(false);

    [UnconditionalSuppressMessage("ReflectionAnalysis", "IL2026", Justification = "Response types are internal, fixed, and controlled by this extension.")]
    [UnconditionalSuppressMessage("Aot", "IL3050", Justification = "Response types are internal, fixed, and controlled by this extension.")]
    public async Task<IReadOnlyList<AzureDevOpsPublishedTestResult>?> UpdateTestResultsWithSubResultsAsync(AzureDevOpsPublishConfiguration configuration, int runId, IReadOnlyList<AzureDevOpsTestCaseResult> results, CancellationToken cancellationToken)
    {
        using HttpRequestMessage request = CreateRequest(
            PatchMethod,
            BuildResultsUri(configuration.CollectionUri, configuration.Project, runId),
            configuration.AccessToken,
            results,
            UpdateJsonSerializerOptions);

        using var requestTimeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        requestTimeoutSource.CancelAfter(RequestTimeout);
        using HttpResponseMessage response = await SendCoreAsync(request, requestTimeoutSource.Token, cancellationToken, AttemptTimeout).ConfigureAwait(false);

        return await TryReadAndParsePublishedResultsAsync(response, requestTimeoutSource.Token, cancellationToken, results, validateAutomatedTestName: false).ConfigureAwait(false);
    }

    public async Task UploadTestResultAttachmentAsync(AzureDevOpsPublishConfiguration configuration, int runId, int testCaseResultId, int? testSubResultId, AzureDevOpsTestResultAttachment attachment, CancellationToken cancellationToken)
    {
        AttachmentRequest? payload = TryBuildAttachmentRequest(attachment);
        if (payload is null)
        {
            return;
        }

        using HttpRequestMessage request = CreateRequest(
            HttpMethod.Post,
            BuildResultAttachmentsUri(configuration.CollectionUri, configuration.Project, runId, testCaseResultId, testSubResultId),
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
}
