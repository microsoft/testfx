// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Extensions.AzureDevOpsReport;

internal interface IAzureDevOpsTestResultsClient
{
    Task<int> CreateTestRunAsync(AzureDevOpsPublishConfiguration configuration, CancellationToken cancellationToken);

    /// <summary>
    /// Publishes a batch of test case results to Azure DevOps and returns the IDs assigned to each result
    /// in the same order as the input. Returns <see langword="null"/> if the request succeeded (HTTP 2xx)
    /// but the response could not be parsed or did not match the submitted batch; in that case the caller
    /// MUST NOT retry the publish (the results were already accepted) but cannot upload result-level
    /// attachments. Throws on transport/HTTP failures, which the caller may retry.
    /// </summary>
    Task<IReadOnlyList<int>?> PublishTestResultsAsync(AzureDevOpsPublishConfiguration configuration, int runId, IReadOnlyList<AzureDevOpsTestCaseResult> results, CancellationToken cancellationToken);

    /// <summary>
    /// Publishes a batch and returns the parent and server-assigned sub-result IDs.
    /// </summary>
    Task<IReadOnlyList<AzureDevOpsPublishedTestResult>?> PublishTestResultsWithSubResultsAsync(AzureDevOpsPublishConfiguration configuration, int runId, IReadOnlyList<AzureDevOpsTestCaseResult> results, CancellationToken cancellationToken);

    /// <summary>
    /// Updates results that were already published to the run, identified by
    /// <see cref="AzureDevOpsTestCaseResult.Id"/>. Used to turn a previously published result into a rerun
    /// carrying every attempt as a sub-result, instead of appending a second result for the same test.
    /// Throws on transport/HTTP failures, which the caller may retry: unlike a create, replaying an update
    /// is idempotent.
    /// </summary>
    Task UpdateTestResultsAsync(AzureDevOpsPublishConfiguration configuration, int runId, IReadOnlyList<AzureDevOpsTestCaseResult> results, CancellationToken cancellationToken);

    /// <summary>
    /// Updates a batch and returns the parent and server-assigned sub-result IDs.
    /// </summary>
    Task<IReadOnlyList<AzureDevOpsPublishedTestResult>?> UpdateTestResultsWithSubResultsAsync(AzureDevOpsPublishConfiguration configuration, int runId, IReadOnlyList<AzureDevOpsTestCaseResult> results, CancellationToken cancellationToken);

    /// <summary>
    /// Uploads an attachment to a specific test case result or sub-result within a test run.
    /// </summary>
    Task UploadTestResultAttachmentAsync(AzureDevOpsPublishConfiguration configuration, int runId, int testCaseResultId, int? testSubResultId, AzureDevOpsTestResultAttachment attachment, CancellationToken cancellationToken);

    /// <summary>
    /// Uploads an attachment to the test run itself (e.g. code coverage files).
    /// </summary>
    Task UploadTestRunAttachmentAsync(AzureDevOpsPublishConfiguration configuration, int runId, AzureDevOpsTestResultAttachment attachment, CancellationToken cancellationToken);

    Task UpdateTestRunStateAsync(AzureDevOpsPublishConfiguration configuration, int runId, string state, CancellationToken cancellationToken);
}
