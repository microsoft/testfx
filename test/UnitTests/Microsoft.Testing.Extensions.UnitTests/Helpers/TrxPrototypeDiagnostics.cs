// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Extensions.TrxReport.Abstractions.Streaming;

namespace Microsoft.Testing.Extensions.UnitTests.Helpers;

internal sealed class TrxPrototypeOperationTotals
{
    private readonly Dictionary<TrxFileOperationKind, int> _counts =
        Enum.GetValues(typeof(TrxFileOperationKind))
            .Cast<TrxFileOperationKind>()
            .ToDictionary(kind => kind, _ => 0);

    private readonly List<long> _replacementSizes = [];

    public IReadOnlyDictionary<TrxFileOperationKind, int> Counts => _counts;

    public long RequestedReadBytes { get; private set; }

    public long CommittedReadBytes { get; private set; }

    public long RequestedWriteBytes { get; private set; }

    public long CommittedWriteBytes { get; private set; }

    public int MaxWriteBytes { get; private set; }

    public long MaxFileBytes { get; private set; }

    public IReadOnlyList<long> ReplacementSizes => _replacementSizes;

    public int TotalOperations => _counts.Values.Sum();

    public int this[TrxFileOperationKind kind] => _counts[kind];

    public void Record(
        TrxFileOperationKind kind,
        long fileLength,
        int requestedBytes = 0,
        int committedBytes = 0)
    {
        _counts[kind]++;
        MaxFileBytes = Math.Max(MaxFileBytes, fileLength);
        if (kind == TrxFileOperationKind.Read)
        {
            RequestedReadBytes += requestedBytes;
            CommittedReadBytes += committedBytes;
        }
        else if (kind == TrxFileOperationKind.Write)
        {
            RequestedWriteBytes += requestedBytes;
            CommittedWriteBytes += committedBytes;
            MaxWriteBytes = Math.Max(MaxWriteBytes, requestedBytes);
        }
        else if (kind == TrxFileOperationKind.Replace)
        {
            _replacementSizes.Add(fileLength);
        }
    }
}

internal sealed class TrxPrototypeDiagnosticRun
{
    public required TrxPrototypeOperationTotals Operations { get; init; }

    public required byte[] JournalBytes { get; init; }

    public required byte[] SnapshotBytes { get; init; }

    public required int ResultCount { get; init; }

    public required int UniqueDefinitionCount { get; init; }

    public required int RunningCount { get; init; }

    public required int PassedCount { get; init; }

    public required int FailedCount { get; init; }

    public required int SkippedCount { get; init; }

    public required int TimeoutCount { get; init; }

    public required int PublishCount { get; init; }

    public required int ReflowCount { get; init; }

    public required int InitialDefinitionPadBytes { get; init; }

    public required int InitialEntryPadBytes { get; init; }

    public required int RemainingDefinitionPadBytes { get; init; }

    public required int RemainingEntryPadBytes { get; init; }

    public required int RemainingSummaryPadBytes { get; init; }

    public required int PaddedSnapshotBytes { get; init; }

    public required int CompactSnapshotBytes { get; init; }

    public required int FixtureInputResultCount { get; init; }

    public required TrxJournalSnapshotDiagnostics? JournalDiagnostics { get; init; }

    public required IReadOnlyList<TrxTestResult> Results { get; init; }

    public required IReadOnlyList<Guid> ExecutionIds { get; init; }

    public required IReadOnlyList<TrxPrototypeRunningTest> RunningTests { get; init; }
}

internal static class TrxPrototypeDiagnostics
{
    internal const int ResultCount = 10_000;
    internal const int UniqueDefinitionCount = 100;
    internal const int PublicationCadence = 2_500;
    internal const int InitialDefinitionPadBytes = 1_024;
    internal const int InitialEntryPadBytes = 1_024;
    internal const int SummaryPadBytes = 4_096;
    internal const int RunningCount = 3;

    private const string JournalPath = "diagnostics.trx.journal";

    public static TrxPrototypeDiagnosticRun RunPadded()
    {
        IReadOnlyList<TrxTestResult> results = CreateResults();
        IReadOnlyList<Guid> executionIds = CreateExecutionIds();
        var operations = new TrxCountingFileOperations();
        var publisher = new TrxSnapshotPublisherPrototype(operations);
        var writer = new TrxIncrementalWriterPrototype(
            operations,
            TrxPhase3EvidenceMatrix.TargetPath,
            TrxPhase3EvidenceMatrix.RunId,
            TrxPhase3EvidenceMatrix.RunName,
            TrxPhase3EvidenceMatrix.MachineName,
            TrxPhase3EvidenceMatrix.TestModule,
            TrxPhase3EvidenceMatrix.FrameworkUid,
            TrxPhase3EvidenceMatrix.FrameworkVersion,
            TrxPhase3EvidenceMatrix.StartTime,
            InitialDefinitionPadBytes,
            InitialEntryPadBytes,
            SummaryPadBytes,
            counterWidth: 5,
            runningSlotCount: 4,
            runningSlotByteCapacity: 384,
            publisher);
        writer.Initialize();
        for (int i = 0; i < results.Count; i++)
        {
            writer.AppendCompleted(results[i], executionIds[i]);
        }

        TrxPrototypeCompletion completion = CreateCompletion();
        writer.Complete(completion);
        byte[] padded = operations.GetFileBytes(TrxPhase3EvidenceMatrix.TargetPath);
        (int definitionWhitespace, int entryWhitespace, int summaryWhitespace) = CountStructuralWhitespace(padded);
        writer.Compact(completion);
        byte[] compact = operations.GetFileBytes(TrxPhase3EvidenceMatrix.TargetPath);
        OutcomeCounts counts = CountOutcomes(results);
        int publishCount = operations.Totals[TrxFileOperationKind.Replace];
        return new TrxPrototypeDiagnosticRun
        {
            Operations = operations.Totals,
            JournalBytes = [],
            SnapshotBytes = compact,
            ResultCount = results.Count,
            UniqueDefinitionCount = UniqueDefinitionCount,
            RunningCount = 0,
            PassedCount = counts.Passed,
            FailedCount = counts.Failed,
            SkippedCount = counts.Skipped,
            TimeoutCount = counts.Timeout,
            PublishCount = publishCount,
            ReflowCount = publishCount - 2,
            InitialDefinitionPadBytes = InitialDefinitionPadBytes,
            InitialEntryPadBytes = InitialEntryPadBytes,
            RemainingDefinitionPadBytes = definitionWhitespace,
            RemainingEntryPadBytes = entryWhitespace,
            RemainingSummaryPadBytes = summaryWhitespace,
            PaddedSnapshotBytes = padded.Length,
            CompactSnapshotBytes = compact.Length,
            FixtureInputResultCount = results.Count,
            JournalDiagnostics = null,
            Results = results,
            ExecutionIds = executionIds,
            RunningTests = [],
        };
    }

    public static TrxPrototypeDiagnosticRun RunJournal()
    {
        IReadOnlyList<TrxTestResult> results = CreateResults();
        IReadOnlyList<Guid> executionIds = CreateExecutionIds();
        IReadOnlyList<TrxPrototypeRunningTest> running = CreateRunningTests();
        var operations = new TrxCountingFileOperations();
        var journal = new TrxJournalSnapshotPrototype(
            operations,
            JournalPath,
            TrxPhase3EvidenceMatrix.TargetPath,
            TrxPhase3EvidenceMatrix.RunId,
            TrxPhase3EvidenceMatrix.RunName,
            TrxPhase3EvidenceMatrix.MachineName,
            TrxPhase3EvidenceMatrix.TestModule,
            TrxPhase3EvidenceMatrix.FrameworkUid,
            TrxPhase3EvidenceMatrix.FrameworkVersion,
            TrxPhase3EvidenceMatrix.StartTime);
        int publishCount = 0;
        for (int i = 0; i < results.Count; i++)
        {
            journal.Append(results[i], executionIds[i]);
            if ((i + 1) % PublicationCadence == 0)
            {
                journal.PublishSnapshot(
                    CreateCompletion(),
                    i + 1 == ResultCount ? running : []);
                publishCount++;
            }
        }

        byte[] journalBytes = operations.GetFileBytes(JournalPath);
        byte[] snapshot = operations.GetFileBytes(TrxPhase3EvidenceMatrix.TargetPath);
        OutcomeCounts counts = CountOutcomes(results);
        return new TrxPrototypeDiagnosticRun
        {
            Operations = operations.Totals,
            JournalBytes = journalBytes,
            SnapshotBytes = snapshot,
            ResultCount = results.Count,
            UniqueDefinitionCount = UniqueDefinitionCount,
            RunningCount = running.Count,
            PassedCount = counts.Passed,
            FailedCount = counts.Failed,
            SkippedCount = counts.Skipped,
            TimeoutCount = counts.Timeout,
            PublishCount = publishCount,
            ReflowCount = 0,
            InitialDefinitionPadBytes = 0,
            InitialEntryPadBytes = 0,
            RemainingDefinitionPadBytes = 0,
            RemainingEntryPadBytes = 0,
            RemainingSummaryPadBytes = 0,
            PaddedSnapshotBytes = 0,
            CompactSnapshotBytes = snapshot.Length,
            FixtureInputResultCount = results.Count,
            JournalDiagnostics = journal.Diagnostics,
            Results = results,
            ExecutionIds = executionIds,
            RunningTests = running,
        };
    }

    public static IReadOnlyList<TrxTestResult> CreateResults()
    {
        var results = new TrxTestResult[ResultCount];
        for (int i = 0; i < results.Length; i++)
        {
            int definition = i % UniqueDefinitionCount;
            IReadOnlyList<TrxTestMetadata>? metadata = i == 0
                ?
                [
                    new TrxTestMetadata
                    {
                        Key = "Description",
                        Value = "diagnostic é漢😀 <&> " + new string('m', 700),
                    },
                    new TrxTestMetadata { Key = "Owner", Value = "phase5-owner" },
                ]
                : null;
            IReadOnlyList<TrxStreamMessage>? messages = i % 777 == 0
                ?
                [
                    new TrxStreamMessage
                    {
                        Kind = TrxStreamMessageKind.StandardOutput,
                        Message = $"output-{i:D5}-é漢😀<&>-" + new string('o', 1_024),
                    },
                ]
                : null;
            results[i] = TrxPhase3EvidenceMatrix.CreateResult(
                700 + definition,
                $"diagnostic[{i:D5}]",
                (TrxTestOutcome)((i % 4) + 1),
                $"Diagnostic.Definition.{definition:D3}",
                metadata,
                categories: definition % 2 == 0 ? ["even", "phase5"] : ["odd"],
                messages);
        }

        return results;
    }

    public static IReadOnlyList<Guid> CreateExecutionIds()
    {
        var executionIds = new Guid[ResultCount];
        for (int i = 0; i < executionIds.Length; i++)
        {
            executionIds[i] = TrxPhase3EvidenceMatrix.ExecutionId(10_000 + i);
        }

        return executionIds;
    }

    public static IReadOnlyList<TrxPrototypeRunningTest> CreateRunningTests()
        =>
        [
            new TrxPrototypeRunningTest
            {
                Uid = TrxPhase3EvidenceMatrix.TestId(901),
                DisplayName = "running[0] é漢😀 <&>",
                ExecutionId = TrxPhase3EvidenceMatrix.ExecutionId(30_001),
                StartTime = TrxPhase3EvidenceMatrix.StartTime.AddMinutes(1),
            },
            new TrxPrototypeRunningTest
            {
                Uid = TrxPhase3EvidenceMatrix.TestId(902),
                DisplayName = "running[1] e\u0301 مرحبا",
                ExecutionId = TrxPhase3EvidenceMatrix.ExecutionId(30_002),
                StartTime = TrxPhase3EvidenceMatrix.StartTime.AddMinutes(2),
            },
            new TrxPrototypeRunningTest
            {
                Uid = TrxPhase3EvidenceMatrix.TestId(903),
                DisplayName = "running[2] >500 " + new string('r', 520),
                ExecutionId = TrxPhase3EvidenceMatrix.ExecutionId(30_003),
                StartTime = TrxPhase3EvidenceMatrix.StartTime.AddMinutes(3),
            },
        ];

    private static TrxPrototypeCompletion CreateCompletion()
        => new()
        {
            FinishTime = TrxPhase3EvidenceMatrix.FinishTime,
            ExitCode = 1,
            AttachmentWarnings = ["diagnostic attachment warning é漢😀 <&>"],
            CollectorAttachmentHrefs = ["diagnostic/collector-é漢😀<&>.bin"],
        };

    private static OutcomeCounts CountOutcomes(IReadOnlyList<TrxTestResult> results)
        => new(
            results.Count(result => result.Outcome == TrxTestOutcome.Passed),
            results.Count(result => result.Outcome == TrxTestOutcome.Failed),
            results.Count(result => result.Outcome == TrxTestOutcome.Skipped),
            results.Count(result => result.Outcome == TrxTestOutcome.Timeout));

    private static (int Definition, int Entry, int Summary) CountStructuralWhitespace(byte[] bytes)
    {
        XDocument document = TrxPrototypeScenarioFactory.LoadStrict(bytes);
        XNamespace ns = TrxDocumentClassifier.TeamTest2010Namespace;
        static int Count(XElement element)
            => Encoding.UTF8.GetByteCount(string.Concat(element.Nodes().OfType<XText>().Select(text => text.Value)));

        return (
            Count(document.Root!.Element(ns + "TestDefinitions")!),
            Count(document.Root!.Element(ns + "TestEntries")!),
            Count(document.Root!.Element(ns + "ResultSummary")!));
    }

    private sealed class OutcomeCounts(int passed, int failed, int skipped, int timeout)
    {
        public int Passed { get; } = passed;

        public int Failed { get; } = failed;

        public int Skipped { get; } = skipped;

        public int Timeout { get; } = timeout;
    }
}

internal sealed class TrxCountingFileOperations : ITrxPrototypeFileOperations
{
#pragma warning disable IDE0028 // Collection expressions cannot preserve the ordinal path comparer.
    private readonly Dictionary<string, MemoryStream> _files = new(StringComparer.Ordinal);
#pragma warning restore IDE0028
    private int _temporaryPath;

    public bool SupportsAtomicReplace => true;

    public TrxPrototypeOperationTotals Totals { get; } = new();

    public ITrxPrototypeFile Open(string path, FileMode mode, FileAccess access, FileShare share)
    {
        _files.TryGetValue(path, out MemoryStream? stream);
        if (mode == FileMode.CreateNew && stream is not null)
        {
            throw new IOException($"File '{path}' already exists.");
        }

        if ((mode == FileMode.Open || mode == FileMode.Truncate) && stream is null)
        {
            throw new FileNotFoundException($"File '{path}' does not exist.", path);
        }

        if (stream is null)
        {
            stream = new MemoryStream();
            _files.Add(path, stream);
        }

        if (mode is FileMode.Create or FileMode.Truncate)
        {
            stream.SetLength(0);
        }

        long position = mode == FileMode.Append ? stream.Length : 0;
        Totals.Record(TrxFileOperationKind.Open, stream.Length);
        return new CountingFile(this, stream, position, access);
    }

    public string CreateTemporarySiblingPath(string destinationPath)
    {
        _temporaryPath++;
        return $"{destinationPath}.diagnostic-{_temporaryPath:D4}.tmp";
    }

    public void ReplaceTemporarySibling(string temporaryPath, string destinationPath)
    {
        MemoryStream temporary = _files[temporaryPath];
        _ = _files.Remove(temporaryPath);
        _files[destinationPath] = temporary;
        Totals.Record(TrxFileOperationKind.Replace, temporary.Length);
    }

    public bool Exists(string path) => _files.ContainsKey(path);

    public void Delete(string path)
    {
        long length = _files.TryGetValue(path, out MemoryStream? stream) ? stream.Length : 0;
        _ = _files.Remove(path);
        Totals.Record(TrxFileOperationKind.Delete, length);
    }

    public byte[] GetFileBytes(string path) => _files[path].ToArray();

    private sealed class CountingFile(
        TrxCountingFileOperations owner,
        MemoryStream stream,
        long position,
        FileAccess access)
        : ITrxPrototypeFile
    {
        private long CurrentPosition { get; set; } = position;

        public long Length => stream.Length;

        public long Position => CurrentPosition;

        public int Read(byte[] buffer, int offset, int count)
        {
            EnsureReadable();
            stream.Position = CurrentPosition;
            int read = stream.Read(buffer, offset, count);
            CurrentPosition += read;
            owner.Totals.Record(TrxFileOperationKind.Read, stream.Length, count, read);
            return read;
        }

        public void Seek(long offset, SeekOrigin origin)
        {
            CurrentPosition = origin switch
            {
                SeekOrigin.Begin => offset,
                SeekOrigin.Current => checked(CurrentPosition + offset),
                SeekOrigin.End => checked(stream.Length + offset),
                _ => throw new ArgumentOutOfRangeException(nameof(origin)),
            };
            owner.Totals.Record(TrxFileOperationKind.Seek, stream.Length);
        }

        public void Write(byte[] buffer, int offset, int count)
        {
            EnsureWritable();
            stream.Position = CurrentPosition;
            stream.Write(buffer, offset, count);
            CurrentPosition += count;
            owner.Totals.Record(TrxFileOperationKind.Write, stream.Length, count, count);
        }

        public void Flush() => owner.Totals.Record(TrxFileOperationKind.Flush, stream.Length);

        public void SetLength(long length)
        {
            EnsureWritable();
            stream.SetLength(length);
            owner.Totals.Record(TrxFileOperationKind.SetLength, stream.Length);
        }

        public void Dispose()
        {
        }

        private void EnsureReadable()
        {
            if (access == FileAccess.Write)
            {
                throw new NotSupportedException("The diagnostic handle is write-only.");
            }
        }

        private void EnsureWritable()
        {
            if (access == FileAccess.Read)
            {
                throw new NotSupportedException("The diagnostic handle is read-only.");
            }
        }
    }
}
