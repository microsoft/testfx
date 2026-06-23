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
    string ResultsDirectory);

internal sealed record AzureDevOpsTestCaseResult(
    [property: JsonPropertyName("automatedTestName")] string AutomatedTestName,
    [property: JsonPropertyName("automatedTestStorage")] string AutomatedTestStorage,
    [property: JsonPropertyName("testCaseTitle")] string TestCaseTitle,
    [property: JsonPropertyName("outcome")] string Outcome,
    [property: JsonPropertyName("durationInMs")] long? DurationInMs,
    [property: JsonPropertyName("errorMessage")] string? ErrorMessage,
    [property: JsonPropertyName("stackTrace")] string? StackTrace,
    [property: JsonPropertyName("startedDate")] DateTimeOffset? StartedDate,
    [property: JsonPropertyName("completedDate")] DateTimeOffset? CompletedDate);

/// <summary>A test case result bundled with optional attachments to upload after the result is published.</summary>
internal sealed record AzureDevOpsTestCaseResultWithAttachments(
    AzureDevOpsTestCaseResult Result,
    IReadOnlyList<AzureDevOpsTestResultAttachment> Attachments);

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
