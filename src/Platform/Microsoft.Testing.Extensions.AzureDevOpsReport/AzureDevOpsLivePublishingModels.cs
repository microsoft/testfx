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

internal sealed record AzureDevOpsTestResultsPublisherOptions(
    int BatchSize,
    TimeSpan FlushInterval,
    int CoordinationReadRetryCount,
    TimeSpan CoordinationReadRetryDelay,
    TimeSpan CoordinationFinalizeTimeout,
    TimeSpan CoordinationFileExpiration)
{
    public AzureDevOpsTestResultsPublisherOptions(int batchSize, TimeSpan flushInterval, int coordinationReadRetryCount, TimeSpan coordinationReadRetryDelay)
        : this(batchSize, flushInterval, coordinationReadRetryCount, coordinationReadRetryDelay, TimeSpan.FromSeconds(30), TimeSpan.FromHours(4))
    {
    }

    public static AzureDevOpsTestResultsPublisherOptions Default { get; } = new(100, TimeSpan.FromSeconds(5), 40, TimeSpan.FromMilliseconds(250), TimeSpan.FromSeconds(30), TimeSpan.FromHours(4));
}
