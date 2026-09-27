// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Extensions.TestHostControllers;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.Logging;

namespace Microsoft.Testing.Extensions;

#pragma warning disable RS0051 // Recovery infrastructure is shared-source implementation detail, not package API.

internal sealed partial class ReportProcessLifetimeHandler<TGenerator, TCapturedTestResult>
{
    private ReportJournalReadResult<TCapturedTestResult> ReadJournal(
        string path,
        ITestHostProcessInformation processInformation)
    {
        ReportJournalRecord<TCapturedTestResult>? header = null;
        List<TCapturedTestResult> results = [];
        List<ReportJournalParentEntry> parents = [];
        bool completed = false;
        string? completedReportFileName = null;
        int droppedJournalRecords = 0;
        bool isPartial = false;
        int recordCount = 0;

        using IFileStream stream = _fileSystem.NewFileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        var reader = new BoundedUtf8LineReader(stream.Stream, MaxJournalBytes, MaxJournalRecordBytes, MaxJournalRecordChars);
        while (recordCount < MaxJournalRecordCount)
        {
            BoundedLineReadResult readResult = reader.ReadLine(out string? line);
            if (readResult == BoundedLineReadResult.End)
            {
                break;
            }

            if (readResult == BoundedLineReadResult.LimitExceeded)
            {
                isPartial = true;
                _logger.LogWarning($"Stopped reading report recovery journal '{path}' because it exceeded a configured size limit.");
                break;
            }

            recordCount++;
            try
            {
                ReportJournalRecord<TCapturedTestResult>? record = _journalDeserializer(line!);
                if (record is null)
                {
                    _logger.LogDebug("Stopped reading the report recovery journal at an invalid record.");
                    isPartial = true;
                    break;
                }

                switch (record.Type)
                {
                    case ReportJournalRecordType.Header:
                        header = record;
                        break;
                    case ReportJournalRecordType.Test:
                        if (record.Result is not null)
                        {
                            results.Add(record.Result);
                        }

                        if (record.Parent is not null)
                        {
                            parents.Add(record.Parent);
                        }

                        break;
                    case ReportJournalRecordType.Completion:
                        completed = true;
                        completedReportFileName = record.ReportFileName;
                        droppedJournalRecords = record.DroppedJournalRecords;
                        break;
                }

                if (completed)
                {
                    break;
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug($"Stopped reading the report recovery journal at an incomplete record: {ex.Message}");
                isPartial = true;
                break;
            }
        }

        if (!completed && recordCount >= MaxJournalRecordCount)
        {
            isPartial = true;
            _logger.LogWarning($"Stopped reading report recovery journal '{path}' after the maximum of {MaxJournalRecordCount} records.");
        }

        var metadata = new RecoveredReportMetadata
        {
            StartTime = header?.StartTime ?? _clock.UtcNow,
            ProcessId = processInformation.PID,
            FrameworkUid = header?.FrameworkUid ?? "unknown",
            FrameworkVersion = header?.FrameworkVersion ?? string.Empty,
            FrameworkDisplayName = header?.FrameworkDisplayName ?? "unknown",
            IsIncomplete = true,
        };
        return new ReportJournalReadResult<TCapturedTestResult>(
            metadata,
            results,
            parents,
            completed,
            completedReportFileName,
            droppedJournalRecords,
            isPartial || droppedJournalRecords > 0);
    }
}

#pragma warning restore RS0051
