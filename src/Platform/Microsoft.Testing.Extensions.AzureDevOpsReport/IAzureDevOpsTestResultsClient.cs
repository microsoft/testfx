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
    /// Uploads an attachment to a specific test case result within a test run.
    /// </summary>
    Task UploadTestResultAttachmentAsync(AzureDevOpsPublishConfiguration configuration, int runId, int testCaseResultId, AzureDevOpsTestResultAttachment attachment, CancellationToken cancellationToken);

    /// <summary>
    /// Uploads an attachment to the test run itself (e.g. code coverage files).
    /// </summary>
    Task UploadTestRunAttachmentAsync(AzureDevOpsPublishConfiguration configuration, int runId, AzureDevOpsTestResultAttachment attachment, CancellationToken cancellationToken);

    Task UpdateTestRunStateAsync(AzureDevOpsPublishConfiguration configuration, int runId, string state, CancellationToken cancellationToken);
}
