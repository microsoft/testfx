// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Text.Json.Serialization;

namespace Microsoft.Testing.Extensions.AzureDevOpsReport;

internal static class AzureDevOpsLivePublishingConstants
{
    public const string AbortedTestOutcome = "Aborted";
    public const string AbortedTestRunState = "Aborted";
    public const string CompletedTestRunState = "Completed";
    public const string FailedTestOutcome = "Failed";
    public const string InProgressTestRunState = "InProgress";
    public const int MaxRunNameLength = 256;
    public const string NotExecutedTestOutcome = "NotExecuted";
    public const string PassedTestOutcome = "Passed";

    /// <summary>
    /// Value of <c>resultGroupType</c> that tells Azure DevOps a result is the parent of several attempts
    /// of the same test rather than a leaf result.
    /// </summary>
    /// <remarks>
    /// Azure DevOps serializes <c>ResultGroupType</c> in camelCase on the wire, so the literal is
    /// <c>rerun</c> and not <c>Rerun</c>.
    /// </remarks>
    public const string RerunResultGroupType = "rerun";

    /// <summary>Maximum number of sub-results Azure DevOps accepts under a single result.</summary>
    /// <remarks>
    /// Matches the limit the Azure Pipelines agent enforces client-side before publishing, where exceeding
    /// it truncates rather than fails. A retry sequence never approaches this, so the cap only guards
    /// against an unbounded attempt history.
    /// </remarks>
    public const int MaxSubResultsPerResult = 1000;

    /// <summary>Maximum size (in bytes) of a single attachment uploaded to Azure DevOps. Files larger than this are skipped.</summary>
    public const long MaxAttachmentSizeBytes = 16L * 1024 * 1024;

    /// <summary>Maximum size (in bytes) of an inline (string-based) attachment such as stdout/stderr. Content beyond this is truncated.</summary>
    public const int MaxInlineAttachmentBytes = 256 * 1024;
}

internal static class AzureDevOpsAttachmentTypes
{
    public const string CodeCoverage = "CodeCoverage";
    public const string ConsoleLog = "ConsoleLog";
    public const string GeneralAttachment = "GeneralAttachment";
}

internal sealed record AzureDevOpsPublishConfiguration(
    string CollectionUri,
    string Project,
    string AccessToken,
    int BuildId,
    string RunName,
    string AutomatedTestStorage,
    string ResultsDirectory)
{
    /// <summary>
    /// Gets the stage/phase/job the test run belongs to, when running in a pipeline that exposes it.
    /// </summary>
    /// <remarks>
    /// Declared as a property rather than a positional parameter so that adding it does not change the
    /// record's constructor and deconstructor signatures.
    /// </remarks>
    public AzureDevOpsPipelineReference? PipelineReference { get; init; }
}

/// <summary>
/// Identifies the pipeline stage, phase and job that produced a test run.
/// </summary>
/// <remarks>
/// Azure DevOps uses this to attribute the run to the right stage/job of a multi-stage pipeline. Without
/// it, the run is only linked to the build as a whole, so per-stage test reporting is unavailable.
/// Every part is optional because the corresponding pipeline variables are not always defined.
/// </remarks>
internal sealed record AzureDevOpsPipelineReference(
    string? StageName,
    int? StageAttempt,
    string? PhaseName,
    int? PhaseAttempt,
    string? JobName,
    int? JobAttempt);

internal sealed record AzureDevOpsTestCaseResult(
    [property: JsonPropertyName("automatedTestName")] string AutomatedTestName,
    [property: JsonPropertyName("automatedTestStorage")] string AutomatedTestStorage,
    [property: JsonPropertyName("testCaseTitle")] string TestCaseTitle,
    [property: JsonPropertyName("outcome")] string Outcome,
    [property: JsonPropertyName("durationInMs")] long? DurationInMs,
    [property: JsonPropertyName("errorMessage")] string? ErrorMessage,
    [property: JsonPropertyName("stackTrace")] string? StackTrace,
    [property: JsonPropertyName("startedDate")] DateTimeOffset? StartedDate,
    [property: JsonPropertyName("completedDate")] DateTimeOffset? CompletedDate)
{
    /// <summary>
    /// Gets the id Azure DevOps assigned to this result, set only when updating an already published result.
    /// </summary>
    /// <remarks>
    /// Declared as properties rather than positional parameters so that adding them does not change the
    /// record's constructor and deconstructor signatures. <see cref="Id"/> is populated only when updating
    /// an existing result. <see cref="ResultGroupType"/> and <see cref="SubResults"/> are populated when
    /// appending attempts to that result, including the follow-up update that gives a newly created result's
    /// first attachment a sub-result to target.
    /// </remarks>
    [JsonPropertyName("id")]
    public int? Id { get; init; }

    /// <summary>
    /// Gets the hierarchy type of the result; <see cref="AzureDevOpsLivePublishingConstants.RerunResultGroupType"/>
    /// marks it as the parent of several attempts.
    /// </summary>
    [JsonPropertyName("resultGroupType")]
    public string? ResultGroupType { get; init; }

    /// <summary>Gets the individual attempts of this test, oldest first.</summary>
    [JsonPropertyName("subResults")]
    public IReadOnlyList<AzureDevOpsTestSubResult>? SubResults { get; init; }
}

/// <summary>
/// One attempt of a test that ran more than once, published under the parent result rather than as a
/// result of its own.
/// </summary>
/// <remarks>
/// <see cref="SequenceId"/> is the attempt ordinal. Azure DevOps documents it only as an "index number",
/// with no stated requirement to be 1-based or contiguous, but the agent's own publisher emits it that way
/// and the Tests tab orders attempts by it, so this extension does the same.
/// </remarks>
internal sealed record AzureDevOpsTestSubResult(
    [property: JsonPropertyName("sequenceId")] int SequenceId,
    [property: JsonPropertyName("displayName")] string DisplayName,
    [property: JsonPropertyName("outcome")] string Outcome,
    [property: JsonPropertyName("durationInMs")] long? DurationInMs,
    [property: JsonPropertyName("errorMessage")] string? ErrorMessage,
    [property: JsonPropertyName("stackTrace")] string? StackTrace,
    [property: JsonPropertyName("startedDate")] DateTimeOffset? StartedDate,
    [property: JsonPropertyName("completedDate")] DateTimeOffset? CompletedDate);

/// <summary>A test case result bundled with optional attachments to upload after the result is published.</summary>
internal sealed record AzureDevOpsTestCaseResultWithAttachments(
    AzureDevOpsTestCaseResult Result,
    IReadOnlyList<AzureDevOpsTestResultAttachment> Attachments)
{
    /// <summary>
    /// Gets earlier executions that an in-process retry performed before <see cref="Result"/> became the
    /// test's final outcome.
    /// </summary>
    public IReadOnlyList<AzureDevOpsTestCaseResultWithAttachments> PreviousAttempts { get; init; } = [];
}

internal sealed class AzureDevOpsPublishedTestResult
{
    public AzureDevOpsPublishedTestResult(int id, IReadOnlyDictionary<int, int> subResultIdsBySequenceId)
    {
        Id = id;
        SubResultIdsBySequenceId = subResultIdsBySequenceId;
    }

    public int Id { get; }

    public IReadOnlyDictionary<int, int> SubResultIdsBySequenceId { get; }

    public bool TryGetSubResultId(int sequenceId, out int subResultId)
        => SubResultIdsBySequenceId.TryGetValue(sequenceId, out subResultId);
}

/// <summary>
/// Describes an attachment to upload to Azure DevOps (either to a test result or to the test run).
/// The payload can come from a file on disk (<see cref="FilePath"/>) or from inline string content (<see cref="InlineContent"/>).
/// Exactly one of <see cref="FilePath"/> or <see cref="InlineContent"/> is non-null.
/// </summary>
internal sealed class AzureDevOpsTestResultAttachment
{
    private AzureDevOpsTestResultAttachment(string fileName, string attachmentType, string? comment, string? filePath, string? inlineContent)
    {
        FileName = fileName;
        AttachmentType = attachmentType;
        Comment = comment;
        FilePath = filePath;
        InlineContent = inlineContent;
    }

    public string FileName { get; }

    public string AttachmentType { get; }

    public string? Comment { get; }

    public string? FilePath { get; }

    public string? InlineContent { get; }

    public static AzureDevOpsTestResultAttachment FromFile(string filePath, string attachmentType, string? comment = null)
        => new(Path.GetFileName(filePath), attachmentType, comment, filePath, inlineContent: null);

    public static AzureDevOpsTestResultAttachment FromString(string content, string fileName, string attachmentType, string? comment = null)
        => new(fileName, attachmentType, comment, filePath: null, inlineContent: content);
}

internal sealed record AzureDevOpsTestResultsPublisherOptions(
    int BatchSize,
    TimeSpan FlushInterval,
    int CoordinationReadRetryCount,
    TimeSpan CoordinationReadRetryDelay,
    TimeSpan CoordinationFinalizeTimeout,
    TimeSpan CoordinationFileExpiration,
    TimeSpan CoordinationJoinerMaxWaitTime)
{
    public AzureDevOpsTestResultsPublisherOptions(int batchSize, TimeSpan flushInterval, int coordinationReadRetryCount, TimeSpan coordinationReadRetryDelay)
        : this(batchSize, flushInterval, coordinationReadRetryCount, coordinationReadRetryDelay, TimeSpan.FromSeconds(30), TimeSpan.FromHours(4), TimeSpan.FromMinutes(2))
    {
    }

    public AzureDevOpsTestResultsPublisherOptions(int batchSize, TimeSpan flushInterval, int coordinationReadRetryCount, TimeSpan coordinationReadRetryDelay, TimeSpan coordinationFinalizeTimeout, TimeSpan coordinationFileExpiration)
        : this(batchSize, flushInterval, coordinationReadRetryCount, coordinationReadRetryDelay, coordinationFinalizeTimeout, coordinationFileExpiration, TimeSpan.FromMinutes(2))
    {
    }

    /// <summary>
    /// Gets the longest the owner will hold a run open waiting for peers that are provably still running.
    /// </summary>
    /// <remarks>
    /// <see cref="CoordinationFinalizeTimeout"/> is the grace period for participants that cannot be
    /// proven alive; this is the hard cap that also applies to live ones, so a leaked process cannot stall
    /// a build indefinitely. Generous because it bounds a whole test project, not a single test: the cost
    /// of stopping too early is that the peer's remaining results are rejected by Azure DevOps.
    /// </remarks>
    public TimeSpan CoordinationFinalizeMaxWaitTime { get; init; } = TimeSpan.FromMinutes(15);

    public static AzureDevOpsTestResultsPublisherOptions Default { get; } = new(100, TimeSpan.FromSeconds(5), 40, TimeSpan.FromMilliseconds(250), TimeSpan.FromSeconds(30), TimeSpan.FromHours(4), TimeSpan.FromMinutes(2));
}

internal enum LeaseFileStatus
{
    /// <summary>The lease file is not present on disk.</summary>
    NotFound,

    /// <summary>The lease file was parsed and the lease is still valid.</summary>
    Active,

    /// <summary>The lease file was parsed and the lease has expired.</summary>
    Expired,

    /// <summary>The lease file is present but could not be read or parsed; it may be mid-write by another process.</summary>
    TransientReadError,
}

internal readonly record struct LeaseReadResult(LeaseFileStatus Status, AzureDevOpsLeaseFile? Lease);
