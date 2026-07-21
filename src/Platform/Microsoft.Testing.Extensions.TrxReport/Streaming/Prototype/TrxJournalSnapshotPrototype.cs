// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform;

namespace Microsoft.Testing.Extensions.TrxReport.Abstractions.Streaming;

internal sealed class TrxPrototypeRunningTest
{
    public required string Uid { get; init; }

    public required string DisplayName { get; init; }

    public required Guid ExecutionId { get; init; }

    public required DateTimeOffset StartTime { get; init; }
}

internal sealed class TrxJournalSnapshotDiagnostics
{
    public int AppendedRecordCount { get; init; }

    public int PublishedRecordCount { get; init; }

    public int PublishedDefinitionCount { get; init; }

    public int MaxEncodedRecordBytes { get; init; }

    public int MaxRenderedFragmentBytes { get; init; }

    public int PeakLogicalBufferBytes { get; init; }

    public int CurrentReplayRecordCount { get; init; }

    public int PeakReplayRecordCount { get; init; }

    public int CurrentRecordBufferBytes { get; init; }

    public int PeakRecordBufferBytes { get; init; }

    public int CurrentDefinitionIdCount { get; init; }

    public int PeakDefinitionIdCount { get; init; }

    public bool RetainsResultCollection { get; init; }

    public bool RetainsXDocument { get; init; }
}

internal sealed class TrxJournalSnapshotPrototype
{
    private const int MaximumPayloadLength = 64 * 1024 * 1024;
    private const int ExecutionIdByteCount = 16;
    private const string NamespaceUri = "http://microsoft.com/schemas/VisualStudio/TeamTest/2010";
    private const string UncategorizedTestListId = "8C84FA94-04C1-424b-9868-57A2D4851A1D";

    private static readonly UTF8Encoding Utf8 = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    private readonly ITrxPrototypeFileOperations _operations;
    private readonly TrxSnapshotPublisherPrototype _publisher;
    private readonly string _journalPath;
    private readonly string _destinationPath;
    private readonly Guid _runId;
    private readonly string _runName;
    private readonly string _machineName;
    private readonly DateTimeOffset _startTime;
    private readonly TrxPrototypeXmlRenderer _renderer;
    private int _appendedRecordCount;
    private int _maxEncodedRecordBytes;
    private bool _journalTailValidated;

    public TrxJournalSnapshotPrototype(
        ITrxPrototypeFileOperations operations,
        string journalPath,
        string destinationPath,
        Guid runId,
        string runName,
        string machineName,
        string testModule,
        string frameworkUid,
        string frameworkVersion,
        DateTimeOffset startTime)
    {
        _operations = operations ?? throw new ArgumentNullException(nameof(operations));
        ThrowIfNullOrEmpty(journalPath, nameof(journalPath));
        ThrowIfNullOrEmpty(destinationPath, nameof(destinationPath));
        ThrowIfNullOrEmpty(runName, nameof(runName));
        ThrowIfNullOrEmpty(machineName, nameof(machineName));
        ThrowIfNullOrEmpty(testModule, nameof(testModule));
        ThrowIfNullOrEmpty(frameworkUid, nameof(frameworkUid));
        ThrowIfNullOrEmpty(frameworkVersion, nameof(frameworkVersion));
        if (string.Equals(
                Path.GetFullPath(journalPath),
                Path.GetFullPath(destinationPath),
                Path.DirectorySeparatorChar == '\\' ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal))
        {
            throw new ArgumentException("The journal and snapshot paths must be different.", nameof(destinationPath));
        }

        _journalPath = journalPath;
        _destinationPath = destinationPath;
        _runId = runId;
        _runName = runName;
        _machineName = machineName;
        _startTime = startTime;
        _renderer = new TrxPrototypeXmlRenderer(machineName, testModule, frameworkUid, frameworkVersion);
        _publisher = new TrxSnapshotPublisherPrototype(operations);
    }

    public TrxJournalSnapshotDiagnostics Diagnostics { get; private set; } = new();

    public void Append(TrxTestResult result, Guid executionId)
    {
        if (result is null)
        {
            throw new ArgumentNullException(nameof(result));
        }

        byte[] encoded = EncodeRecord(result, executionId);
        EnsureJournalTailIsValid();
        using (ITrxPrototypeFile journal = _operations.Open(
                   _journalPath,
                   FileMode.Append,
                   FileAccess.Write,
                   FileShare.Read | FileShare.Delete))
        {
            journal.Write(encoded, 0, encoded.Length);
            journal.Flush();
        }

        _appendedRecordCount++;
        _maxEncodedRecordBytes = Math.Max(_maxEncodedRecordBytes, encoded.Length);
        Diagnostics = new TrxJournalSnapshotDiagnostics
        {
            AppendedRecordCount = _appendedRecordCount,
            MaxEncodedRecordBytes = _maxEncodedRecordBytes,
            PeakLogicalBufferBytes = _maxEncodedRecordBytes,
            CurrentReplayRecordCount = 0,
            PeakReplayRecordCount = 0,
            CurrentRecordBufferBytes = 0,
            PeakRecordBufferBytes = 0,
            CurrentDefinitionIdCount = 0,
            PeakDefinitionIdCount = 0,
            RetainsResultCollection = false,
            RetainsXDocument = false,
        };
    }

    public void PublishSnapshot(
        TrxPrototypeCompletion completion,
        IReadOnlyList<TrxPrototypeRunningTest>? runningTests = null)
    {
        if (completion is null)
        {
            throw new ArgumentNullException(nameof(completion));
        }

        runningTests ??= [];
        ValidateRunningTests(runningTests);

        var metrics = new SnapshotMetrics(_appendedRecordCount, _maxEncodedRecordBytes);
        _publisher.Publish(
            _destinationPath,
            destination => WriteSnapshot(destination, completion, runningTests, metrics));
        _maxEncodedRecordBytes = Math.Max(_maxEncodedRecordBytes, metrics.MaxEncodedRecordBytes);
        Diagnostics = metrics.ToDiagnostics();
    }

    private void EnsureJournalTailIsValid()
    {
        if (_journalTailValidated)
        {
            return;
        }

        if (!_operations.Exists(_journalPath))
        {
            _journalTailValidated = true;
            return;
        }

        var metrics = new SnapshotMetrics(_appendedRecordCount, _maxEncodedRecordBytes);
        using ITrxPrototypeFile journal = _operations.Open(
            _journalPath,
            FileMode.Open,
            FileAccess.ReadWrite,
            FileShare.Read | FileShare.Delete);
        long validLength = 0;
        while (TryReadRecord(journal, metrics, out _))
        {
            validLength = journal.Position;
        }

        if (validLength < journal.Length)
        {
            journal.SetLength(validLength);
            journal.Flush();
        }

        _journalTailValidated = true;
    }

    private void WriteSnapshot(
        ITrxPrototypeFile destination,
        TrxPrototypeCompletion completion,
        IReadOnlyList<TrxPrototypeRunningTest> runningTests,
        SnapshotMetrics metrics)
    {
        WriteFragment(destination, RenderPrefix(completion.FinishTime), currentRecordBytes: 0, metrics);

        var counts = new SnapshotCounts { InProgress = runningTests.Count };
        Replay(
            record =>
            {
                byte[] fragment = _renderer.RenderCompletedResult(record.Result, record.ExecutionId, _startTime);
                WriteFragment(destination, fragment, record.EncodedByteCount, metrics);
                counts.Add(record.Result.Outcome);
                metrics.PublishedRecordCount++;
            },
            metrics);

        foreach (TrxPrototypeRunningTest running in runningTests)
        {
            WriteFragment(destination, RenderRunningTest(running), currentRecordBytes: 0, metrics);
        }

        WriteFragment(destination, Utf8.GetBytes("</Results><TestDefinitions>"), currentRecordBytes: 0, metrics);

#pragma warning disable IDE0028 // Collection expressions cannot preserve the ordinal test-id comparer.
        var definitionIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
#pragma warning restore IDE0028
        try
        {
            Replay(
                record =>
                {
                    string testId = TrxPrototypeXmlRenderer.GetTestId(record.Result.Uid);
                    if (definitionIds.Add(testId))
                    {
                        metrics.SetCurrentDefinitionIdCount(definitionIds.Count);
                        byte[] fragment = _renderer.RenderDefinition(record.Result, record.ExecutionId);
                        WriteFragment(destination, fragment, record.EncodedByteCount, metrics);
                        metrics.PublishedDefinitionCount++;
                    }
                },
                metrics);
        }
        finally
        {
            definitionIds.Clear();
            metrics.SetCurrentDefinitionIdCount(0);
        }

        WriteFragment(destination, Utf8.GetBytes("</TestDefinitions><TestEntries>"), currentRecordBytes: 0, metrics);
        Replay(
            record =>
            {
                byte[] fragment = TrxPrototypeXmlRenderer.RenderEntry(record.Result.Uid, record.ExecutionId);
                WriteFragment(destination, fragment, record.EncodedByteCount, metrics);
            },
            metrics);

        WriteFragment(destination, RenderSuffix(completion, counts), currentRecordBytes: 0, metrics);
    }

    private void Replay(Action<JournalRecord> visit, SnapshotMetrics metrics)
    {
        if (!_operations.Exists(_journalPath))
        {
            return;
        }

        using ITrxPrototypeFile journal = _operations.Open(
            _journalPath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite | FileShare.Delete);
        while (TryReadRecord(journal, metrics, out JournalRecord? record))
        {
            metrics.MaxEncodedRecordBytes = Math.Max(metrics.MaxEncodedRecordBytes, record!.EncodedByteCount);
            metrics.BeginReplayRecord();
            try
            {
                visit(record);
            }
            finally
            {
                metrics.EndReplayRecord();
            }
        }
    }

    private static bool TryReadRecord(
        ITrxPrototypeFile journal,
        SnapshotMetrics metrics,
        out JournalRecord? record)
    {
        record = null;
        byte[] lengthBytes = new byte[sizeof(int)];
        metrics.SetCurrentRecordBufferBytes(lengthBytes.Length);
        try
        {
            int lengthBytesRead = ReadExactly(journal, lengthBytes, 0, lengthBytes.Length);
            if (lengthBytesRead < lengthBytes.Length)
            {
                return false;
            }

            int payloadLength = BitConverter.ToInt32(lengthBytes, 0);
            if (payloadLength is <= 0 or > MaximumPayloadLength)
            {
                return false;
            }

            int remainingLength = checked(payloadLength + ExecutionIdByteCount);
            byte[] remainder = new byte[remainingLength];
            metrics.SetCurrentRecordBufferBytes(checked(lengthBytes.Length + remainder.Length));
            if (ReadExactly(journal, remainder, 0, remainder.Length) < remainder.Length)
            {
                return false;
            }

            byte[] serializedResult = new byte[checked(sizeof(int) + payloadLength)];
            metrics.SetCurrentRecordBufferBytes(
                checked(lengthBytes.Length + remainder.Length + serializedResult.Length));
            Array.Copy(lengthBytes, serializedResult, lengthBytes.Length);
            Array.Copy(remainder, 0, serializedResult, lengthBytes.Length, payloadLength);
            TrxTestResult? result;
            try
            {
                using var stream = new MemoryStream(serializedResult, writable: false);
                using IEnumerator<TrxTestResult> records = TrxTestResultSerializer.ReadAll(stream).GetEnumerator();
                if (!records.MoveNext())
                {
                    return false;
                }

                result = records.Current;
            }
            catch (Exception ex) when (ex is EndOfStreamException or IOException or FormatException)
            {
                return false;
            }

            byte[] executionIdBytes = new byte[ExecutionIdByteCount];
            metrics.SetCurrentRecordBufferBytes(
                checked(lengthBytes.Length + remainder.Length + serializedResult.Length + executionIdBytes.Length));
            Array.Copy(remainder, payloadLength, executionIdBytes, 0, executionIdBytes.Length);
            record = new JournalRecord(
                result,
                new Guid(executionIdBytes),
                checked(serializedResult.Length + ExecutionIdByteCount));
            return true;
        }
        finally
        {
            metrics.SetCurrentRecordBufferBytes(0);
        }
    }

    private byte[] RenderPrefix(DateTimeOffset finishTime)
    {
        XNamespace ns = NamespaceUri;
        string root = SerializeElement(
            new XElement(
                ns + "TestRun",
                new XAttribute("id", _runId),
                new XAttribute("name", Sanitize(_runName))));
        root = root.Substring(0, root.LastIndexOf("/>", StringComparison.Ordinal)) + ">";
        string times = SerializeElement(
            new XElement(
                "Times",
                new XAttribute("creation", FormatTimestamp(_startTime)),
                new XAttribute("queuing", FormatTimestamp(_startTime)),
                new XAttribute("start", FormatTimestamp(_startTime)),
                new XAttribute("finish", FormatTimestamp(finishTime))));
        string settings = SerializeElement(
            new XElement(
                "TestSettings",
                new XAttribute("name", "default"),
                new XAttribute("id", Guid.Empty),
                new XElement(
                    "Deployment",
                    new XAttribute("runDeploymentRoot", Sanitize(_runName)))));
        return Utf8.GetBytes(string.Concat(
            "<?xml version=\"1.0\" encoding=\"utf-8\"?>",
            root,
            times,
            settings,
            "<Results>"));
    }

    private byte[] RenderSuffix(TrxPrototypeCompletion completion, SnapshotCounts counts)
    {
        string outcome = completion.IsTestHostCrashed
            || completion.ExitCode != 0
            || counts.Failed > 0
            || counts.Timeout > 0
                ? "Failed"
                : "Completed";
        string testLists = SerializeElement(
            new XElement(
                "TestLists",
                new XElement(
                    "TestList",
                    new XAttribute("name", "Results Not in a List"),
                    new XAttribute("id", UncategorizedTestListId)),
                new XElement(
                    "TestList",
                    new XAttribute("name", "All Loaded Results"),
                    new XAttribute("id", "19431567-8539-422a-85D7-44EE4E166BDA"))));
        return Utf8.GetBytes(string.Concat(
            "</TestEntries>",
            testLists,
            "<ResultSummary outcome=\"",
            outcome,
            "\">",
            RenderCounters(counts),
            Utf8.GetString(_renderer.RenderSummaryAdditions(completion)),
            "</ResultSummary></TestRun>"));
    }

    private byte[] RenderRunningTest(TrxPrototypeRunningTest running)
        => Utf8.GetBytes(
            SerializeElement(
                new XElement(
                    "UnitTestResult",
                    new XAttribute("executionId", running.ExecutionId),
                    new XAttribute("testId", TrxPrototypeXmlRenderer.GetTestId(running.Uid)),
                    new XAttribute("testName", Sanitize(running.DisplayName)),
                    new XAttribute("computerName", Sanitize(_machineName)),
                    new XAttribute("startTime", FormatTimestamp(running.StartTime)),
                    new XAttribute("outcome", "InProgress"))));

    private static string RenderCounters(SnapshotCounts counts)
        => string.Concat(
            "<Counters total=\"", Format(counts.Total),
            "\" executed=\"", Format(counts.Executed),
            "\" passed=\"", Format(counts.Passed),
            "\" failed=\"", Format(counts.Failed),
            "\" error=\"0\" timeout=\"", Format(counts.Timeout),
            "\" aborted=\"0\" inconclusive=\"0\" passedButRunAborted=\"0\" notRunnable=\"0\" notExecuted=\"",
            Format(counts.Skipped),
            "\" disconnected=\"0\" warning=\"0\" completed=\"0\" inProgress=\"",
            Format(counts.InProgress),
            "\" pending=\"0\"/>");

    private static string Format(int value) => value.ToString(CultureInfo.InvariantCulture);

    private static string FormatTimestamp(DateTimeOffset value)
        => value.ToString("O", CultureInfo.InvariantCulture);

    private static byte[] EncodeRecord(TrxTestResult result, Guid executionId)
    {
        using var stream = new MemoryStream(capacity: 256);
        using (var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true))
        {
            TrxTestResultSerializer.Write(writer, result);
        }

        if (stream.Length > MaximumPayloadLength + sizeof(int))
        {
            throw new InvalidOperationException(
                $"The encoded TRX journal record is {stream.Length} bytes, exceeding the supported maximum.");
        }

        byte[] resultBytes = stream.ToArray();
        byte[] executionIdBytes = executionId.ToByteArray();
        byte[] encoded = new byte[checked(resultBytes.Length + executionIdBytes.Length)];
        Array.Copy(resultBytes, encoded, resultBytes.Length);
        Array.Copy(executionIdBytes, 0, encoded, resultBytes.Length, executionIdBytes.Length);
        return encoded;
    }

    private static int ReadExactly(ITrxPrototypeFile file, byte[] buffer, int offset, int count)
    {
        int total = 0;
        while (total < count)
        {
            int read = file.Read(buffer, offset + total, count - total);
            if (read == 0)
            {
                break;
            }

            total += read;
        }

        return total;
    }

    private static void WriteFragment(
        ITrxPrototypeFile destination,
        byte[] fragment,
        int currentRecordBytes,
        SnapshotMetrics metrics)
    {
        destination.Write(fragment, 0, fragment.Length);
        metrics.MaxRenderedFragmentBytes = Math.Max(metrics.MaxRenderedFragmentBytes, fragment.Length);
        metrics.PeakLogicalBufferBytes = Math.Max(
            metrics.PeakLogicalBufferBytes,
            checked(currentRecordBytes + fragment.Length));
    }

    private static void ValidateRunningTests(IReadOnlyList<TrxPrototypeRunningTest> runningTests)
    {
        var executionIds = new HashSet<Guid>();
        foreach (TrxPrototypeRunningTest running in runningTests)
        {
            if (running is null)
            {
                throw new ArgumentException("Running breadcrumbs cannot contain null.", nameof(runningTests));
            }

            ThrowIfNullOrEmpty(running.Uid, nameof(runningTests));
            if (running.DisplayName is null)
            {
                throw new ArgumentException("A running breadcrumb display name cannot be null.", nameof(runningTests));
            }

            if (!executionIds.Add(running.ExecutionId))
            {
                throw new ArgumentException(
                    $"Running execution '{running.ExecutionId}' occurs more than once.",
                    nameof(runningTests));
            }
        }
    }

    private static string SerializeElement(XElement element)
    {
        XmlWriterSettings settings = new()
        {
            OmitXmlDeclaration = true,
            Indent = false,
            NewLineHandling = NewLineHandling.Entitize,
        };
        StringBuilder builder = new();
        using (var writer = XmlWriter.Create(builder, settings))
        {
            element.WriteTo(writer);
        }

        return builder.ToString();
    }

    private static string Sanitize(string value)
    {
        StringBuilder? builder = null;
        for (int i = 0; i < value.Length; i++)
        {
            char current = value[i];
            bool valid = current is '\t' or '\n' or '\r'
                or (>= '\x20' and <= '\uD7FF')
                or (>= '\uE000' and <= '\uFFFD');
            bool surrogatePair = char.IsHighSurrogate(current)
                && i + 1 < value.Length
                && char.IsLowSurrogate(value[i + 1]);
            if (valid || surrogatePair)
            {
                if (builder is not null)
                {
                    _ = builder.Append(current);
                    if (surrogatePair)
                    {
                        _ = builder.Append(value[++i]);
                    }
                }
                else if (surrogatePair)
                {
                    i++;
                }

                continue;
            }

            builder ??= new StringBuilder(value, 0, i, value.Length + (value.Length / 2));
            _ = builder.Append(@"\u").Append(((ushort)current).ToString("x4", CultureInfo.InvariantCulture));
        }

        return builder?.ToString() ?? value;
    }

    private static void ThrowIfNullOrEmpty(string value, string parameterName)
    {
        if (RoslynString.IsNullOrEmpty(value))
        {
            throw new ArgumentException("The value cannot be null or empty.", parameterName);
        }
    }

    private sealed class SnapshotCounts
    {
        public int Passed { get; private set; }

        public int Failed { get; private set; }

        public int Skipped { get; private set; }

        public int Timeout { get; private set; }

        public int InProgress { get; init; }

        public int Total => Passed + Failed + Skipped + Timeout;

        public int Executed => Passed + Failed;

        public void Add(TrxTestOutcome outcome)
        {
            switch (outcome)
            {
                case TrxTestOutcome.Passed:
                    Passed++;
                    break;
                case TrxTestOutcome.Failed:
                    Failed++;
                    break;
                case TrxTestOutcome.Skipped:
                    Skipped++;
                    break;
                case TrxTestOutcome.Timeout:
                    Timeout++;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(outcome));
            }
        }
    }

    private sealed class SnapshotMetrics(int appendedRecordCount, int maxEncodedRecordBytes)
    {
        public int AppendedRecordCount { get; } = appendedRecordCount;

        public int PublishedRecordCount { get; set; }

        public int PublishedDefinitionCount { get; set; }

        public int MaxEncodedRecordBytes { get; set; } = maxEncodedRecordBytes;

        public int MaxRenderedFragmentBytes { get; set; }

        public int PeakLogicalBufferBytes { get; set; } = maxEncodedRecordBytes;

        public int CurrentReplayRecordCount { get; private set; }

        public int PeakReplayRecordCount { get; private set; }

        public int CurrentRecordBufferBytes { get; private set; }

        public int PeakRecordBufferBytes { get; private set; }

        public int CurrentDefinitionIdCount { get; private set; }

        public int PeakDefinitionIdCount { get; private set; }

        public void BeginReplayRecord()
        {
            CurrentReplayRecordCount++;
            PeakReplayRecordCount = Math.Max(PeakReplayRecordCount, CurrentReplayRecordCount);
        }

        public void EndReplayRecord() => CurrentReplayRecordCount--;

        public void SetCurrentRecordBufferBytes(int value)
        {
            CurrentRecordBufferBytes = value;
            PeakRecordBufferBytes = Math.Max(PeakRecordBufferBytes, value);
        }

        public void SetCurrentDefinitionIdCount(int value)
        {
            CurrentDefinitionIdCount = value;
            PeakDefinitionIdCount = Math.Max(PeakDefinitionIdCount, value);
        }

        public TrxJournalSnapshotDiagnostics ToDiagnostics()
            => new()
            {
                AppendedRecordCount = AppendedRecordCount,
                PublishedRecordCount = PublishedRecordCount,
                PublishedDefinitionCount = PublishedDefinitionCount,
                MaxEncodedRecordBytes = MaxEncodedRecordBytes,
                MaxRenderedFragmentBytes = MaxRenderedFragmentBytes,
                PeakLogicalBufferBytes = PeakLogicalBufferBytes,
                CurrentReplayRecordCount = CurrentReplayRecordCount,
                PeakReplayRecordCount = PeakReplayRecordCount,
                CurrentRecordBufferBytes = CurrentRecordBufferBytes,
                PeakRecordBufferBytes = PeakRecordBufferBytes,
                CurrentDefinitionIdCount = CurrentDefinitionIdCount,
                PeakDefinitionIdCount = PeakDefinitionIdCount,
                RetainsResultCollection = false,
                RetainsXDocument = false,
            };
    }

    private sealed class JournalRecord(TrxTestResult result, Guid executionId, int encodedByteCount)
    {
        public TrxTestResult Result { get; } = result;

        public Guid ExecutionId { get; } = executionId;

        public int EncodedByteCount { get; } = encodedByteCount;
    }
}
