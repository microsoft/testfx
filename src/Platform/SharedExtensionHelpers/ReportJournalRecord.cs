// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Extensions.TestFramework;

namespace Microsoft.Testing.Extensions;

#pragma warning disable RS0051 // Recovery infrastructure is shared-source implementation detail, not package API.

internal enum ReportJournalRecordType
{
    Header,
    Test,
    Completion,
}

internal sealed class ReportJournalRecord<TCapturedTestResult>
    where TCapturedTestResult : class
{
    public ReportJournalRecordType Type { get; set; }

    public DateTimeOffset? StartTime { get; set; }

    public int? ProcessId { get; set; }

    public string? FrameworkUid { get; set; }

    public string? FrameworkVersion { get; set; }

    public string? FrameworkDisplayName { get; set; }

    public TCapturedTestResult? Result { get; set; }

    public ReportJournalParentEntry? Parent { get; set; }

    public string? ReportFileName { get; set; }

    public int DroppedJournalRecords { get; set; }

    public static ReportJournalRecord<TCapturedTestResult> CreateHeader(
        DateTimeOffset startTime,
        int processId,
        ITestFramework testFramework)
        => new()
        {
            Type = ReportJournalRecordType.Header,
            StartTime = startTime,
            ProcessId = processId,
            FrameworkUid = testFramework.Uid,
            FrameworkVersion = testFramework.Version,
            FrameworkDisplayName = testFramework.DisplayName,
        };

    public static ReportJournalRecord<TCapturedTestResult> CreateTest(
        TCapturedTestResult? result,
        ReportJournalParentEntry? parent)
        => new()
        {
            Type = ReportJournalRecordType.Test,
            Result = result,
            Parent = parent,
        };

    public static ReportJournalRecord<TCapturedTestResult> CreateCompletion(
        string reportFileName,
        int droppedJournalRecords)
        => new()
        {
            Type = ReportJournalRecordType.Completion,
            ReportFileName = reportFileName,
            DroppedJournalRecords = droppedJournalRecords,
        };
}

internal sealed class ReportJournalParentEntry
{
    public string Uid { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public string? ParentUid { get; set; }
}

internal sealed record ReportJournalReadResult<TCapturedTestResult>(
    RecoveredReportMetadata Metadata,
    IReadOnlyList<TCapturedTestResult> Results,
    IReadOnlyList<ReportJournalParentEntry> Parents,
    bool Completed,
    string? CompletedReportFileName,
    int DroppedJournalRecords,
    bool IsPartial)
    where TCapturedTestResult : class;

internal sealed class RecoveredReportMetadata
{
    public DateTimeOffset StartTime { get; set; }

    public int ProcessId { get; set; }

    public string FrameworkUid { get; set; } = string.Empty;

    public string FrameworkVersion { get; set; } = string.Empty;

    public string FrameworkDisplayName { get; set; } = string.Empty;

    public bool IsIncomplete { get; set; }
}

internal sealed class RecoveredTestFramework(RecoveredReportMetadata metadata) : ITestFramework
{
    public string Uid => metadata.FrameworkUid;

    public string Version => metadata.FrameworkVersion;

    public string DisplayName => metadata.FrameworkDisplayName;

    public string Description => DisplayName;

    public Task<bool> IsEnabledAsync() => Task.FromResult(true);

    public Task<CreateTestSessionResult> CreateTestSessionAsync(CreateTestSessionContext context) => throw new NotSupportedException();

    public Task<CloseTestSessionResult> CloseTestSessionAsync(CloseTestSessionContext context) => throw new NotSupportedException();

    public Task ExecuteRequestAsync(ExecuteRequestContext context) => throw new NotSupportedException();
}

#pragma warning restore RS0051
