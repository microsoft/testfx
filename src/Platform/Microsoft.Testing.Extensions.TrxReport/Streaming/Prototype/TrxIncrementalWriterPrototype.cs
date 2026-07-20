// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform;

namespace Microsoft.Testing.Extensions.TrxReport.Abstractions.Streaming;

internal sealed class TrxIncrementalWriterPrototype
{
    private const string ResultsClosers = "</Results></TestRun>";
    private const int OutcomeCellWidth = 10;

    private static readonly UTF8Encoding Utf8 = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);
    private static readonly string[] MutableCounterNames =
        ["total", "executed", "passed", "failed", "timeout", "notExecuted", "inProgress"];

    private readonly ITrxPrototypeFileOperations _operations;
    private readonly string _path;
    private readonly Guid _runId;
    private readonly string _runName;
    private readonly DateTimeOffset _startTime;
    private readonly int _summaryPadBytes;
    private readonly int _counterWidth;
    private readonly int _runningSlotCount;
    private readonly int _runningSlotByteCapacity;
    private readonly TrxPrototypeXmlRenderer _renderer;
    private readonly TrxSnapshotPublisherPrototype? _snapshotPublisher;
#pragma warning disable IDE0028 // Collection expressions cannot preserve the ordinal test-id comparer.
    private readonly Dictionary<string, string> _definitionNames = new(StringComparer.OrdinalIgnoreCase);
#pragma warning restore IDE0028
    private readonly List<TrxTestResult> _completedResults = [];
    private readonly List<Guid> _completedExecutionIds = [];
    private readonly RunningClaim?[] _runningClaims;
    private readonly Dictionary<Guid, int> _runningSlotsByExecutionId = [];
#pragma warning disable IDE0028 // Collection expressions cannot preserve the ordinal counter-name comparer.
    private readonly Dictionary<string, long> _counterOffsets = new(StringComparer.Ordinal);
#pragma warning restore IDE0028

    private int _definitionPadBytes;
    private int _entryPadBytes;
    private long _definitionWriteOffset;
    private long _entryWriteOffset;
    private long _summaryWriteOffset;
    private long _resultTailOffset;
    private long _finishTimeOffset;
    private long _outcomeCellOffset;
    private int _definitionBytesRemaining;
    private int _entryBytesRemaining;
    private int _summaryBytesRemaining;
    private long _runningSlotsOffset;
    private int _passed;
    private int _failed;
    private int _skipped;
    private int _timeout;
    private DateTimeOffset _finishTime;
    private bool _initialized;
    private bool _completed;

    public TrxIncrementalWriterPrototype(
        ITrxPrototypeFileOperations operations,
        string path,
        Guid runId,
        string runName,
        string machineName,
        string testModule,
        string frameworkUid,
        string frameworkVersion,
        DateTimeOffset startTime,
        int definitionPadBytes = 65_536,
        int entryPadBytes = 32_768,
        int summaryPadBytes = 32_768,
        int counterWidth = 10,
        int runningSlotCount = 4,
        int runningSlotByteCapacity = 512,
        TrxSnapshotPublisherPrototype? snapshotPublisher = null)
    {
        _operations = operations ?? throw new ArgumentNullException(nameof(operations));
        ThrowIfNullOrEmpty(path, nameof(path));
        ThrowIfNullOrEmpty(runName, nameof(runName));
        ThrowIfNullOrEmpty(machineName, nameof(machineName));
        ThrowIfNullOrEmpty(testModule, nameof(testModule));
        ThrowIfNullOrEmpty(frameworkUid, nameof(frameworkUid));
        ThrowIfNullOrEmpty(frameworkVersion, nameof(frameworkVersion));
        if (definitionPadBytes < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(definitionPadBytes));
        }

        if (entryPadBytes < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(entryPadBytes));
        }

        if (summaryPadBytes < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(summaryPadBytes));
        }

        if (counterWidth is < 1 or > 10)
        {
            throw new ArgumentOutOfRangeException(nameof(counterWidth));
        }

        if (runningSlotCount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(runningSlotCount));
        }

        if (runningSlotByteCapacity < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(runningSlotByteCapacity));
        }

        _path = path;
        _runId = runId;
        _runName = runName;
        _startTime = startTime;
        _finishTime = startTime;
        _definitionPadBytes = definitionPadBytes;
        _entryPadBytes = entryPadBytes;
        _summaryPadBytes = summaryPadBytes;
        _counterWidth = counterWidth;
        _runningSlotCount = runningSlotCount;
        _runningSlotByteCapacity = runningSlotByteCapacity;
        _renderer = new TrxPrototypeXmlRenderer(machineName, testModule, frameworkUid, frameworkVersion);
        _snapshotPublisher = snapshotPublisher;
        _runningClaims = new RunningClaim?[runningSlotCount];
    }

    public void Initialize()
    {
        if (_initialized)
        {
            throw new InvalidOperationException("The TRX prototype is already initialized.");
        }

        byte[] bytes = TrxPrototypeXmlRenderer.RenderInitial(
            _runId,
            _runName,
            _startTime,
            _definitionPadBytes,
            _entryPadBytes,
            _summaryPadBytes,
            _counterWidth,
            _runningSlotCount,
            _runningSlotByteCapacity);
        if (_snapshotPublisher is null)
        {
            using ITrxPrototypeFile file = _operations.Open(
                _path,
                FileMode.Create,
                FileAccess.ReadWrite,
                FileShare.Read | FileShare.Delete);
            file.Write(bytes, 0, bytes.Length);
            file.Flush();
        }
        else
        {
            _snapshotPublisher.Publish(
                _path,
                file => file.Write(bytes, 0, bytes.Length));
        }

        CalculateLayout(bytes);
        _initialized = true;
    }

    public int ClaimRunning(string uid, string displayName, Guid executionId, DateTimeOffset startTime)
    {
        EnsureMutable();
        ThrowIfNullOrEmpty(uid, nameof(uid));
        if (displayName is null)
        {
            throw new ArgumentNullException(nameof(displayName));
        }

        if (_runningSlotsByExecutionId.ContainsKey(executionId))
        {
            throw new InvalidOperationException($"Execution '{executionId}' is already running.");
        }

        int slot = Array.FindIndex(_runningClaims, claim => claim is null);
        if (slot < 0)
        {
            throw new InvalidOperationException(
                $"All {_runningSlotCount} running-test slots are occupied; execution '{executionId}' was not recorded.");
        }

        byte[] slotBytes = _renderer.RenderRunningSlot(
            uid,
            displayName,
            executionId,
            startTime,
            _runningSlotByteCapacity);
        WriteFixedRegion(GetRunningSlotOffset(slot), slotBytes);

        _runningClaims[slot] = new RunningClaim(uid, displayName, executionId, startTime);
        _runningSlotsByExecutionId.Add(executionId, slot);
        WriteCounter("inProgress", _runningSlotsByExecutionId.Count);
        return slot;
    }

    public void AppendCompleted(TrxTestResult result, Guid executionId, int? runningSlot = null)
    {
        EnsureMutable();
        if (result is null)
        {
            throw new ArgumentNullException(nameof(result));
        }

        RunningClaim? claim = null;
        if (runningSlot is int slot)
        {
            if ((uint)slot >= (uint)_runningClaims.Length || _runningClaims[slot] is not { } existing)
            {
                throw new InvalidOperationException($"Running slot {slot} is not claimed.");
            }

            if (existing.ExecutionId != executionId)
            {
                throw new InvalidOperationException(
                    $"Running slot {slot} belongs to execution '{existing.ExecutionId}', not '{executionId}'.");
            }

            if (!string.Equals(
                    TrxPrototypeXmlRenderer.GetTestId(existing.Uid),
                    TrxPrototypeXmlRenderer.GetTestId(result.Uid),
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    $"Running slot {slot} belongs to test '{existing.Uid}', not '{result.Uid}'.");
            }

            claim = existing;
        }
        else if (_runningSlotsByExecutionId.ContainsKey(executionId))
        {
            throw new InvalidOperationException(
                $"Execution '{executionId}' has a running claim; its slot must be supplied when completing it.");
        }

        string testId = TrxPrototypeXmlRenderer.GetTestId(result.Uid);
        string definitionName = result.TrxTestDefinitionName ?? result.DisplayName;
        bool addDefinition = !_definitionNames.TryGetValue(testId, out string? existingDefinitionName);
        if (!addDefinition
            && result.TrxTestDefinitionName is not null
            && !string.Equals(existingDefinitionName, definitionName, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Received two different test definition names ('{existingDefinitionName}' and '{definitionName}') for test id '{testId}'.");
        }

        byte[] resultBytes = _renderer.RenderCompletedResult(result, executionId, _startTime);
        byte[] entryBytes = TrxPrototypeXmlRenderer.RenderEntry(result.Uid, executionId);
        byte[]? definitionBytes = addDefinition ? _renderer.RenderDefinition(result, executionId) : null;
        EnsureCounterCapacity();
        if (_snapshotPublisher is not null
            && ((definitionBytes?.Length ?? 0) > _definitionBytesRemaining
                || entryBytes.Length > _entryBytesRemaining))
        {
            ReflowAndAppend(
                result,
                executionId,
                runningSlot,
                definitionName,
                addDefinition,
                definitionBytes?.Length ?? 0,
                entryBytes.Length);
            return;
        }

        EnsureCapacity("definition", definitionBytes?.Length ?? 0, _definitionBytesRemaining);
        EnsureCapacity("entry", entryBytes.Length, _entryBytesRemaining);

        if (definitionBytes is not null)
        {
            WriteFixedRegion(_definitionWriteOffset, definitionBytes);
            _definitionWriteOffset += definitionBytes.Length;
            _definitionBytesRemaining -= definitionBytes.Length;
        }

        WriteFixedRegion(_entryWriteOffset, entryBytes);
        _entryWriteOffset += entryBytes.Length;
        _entryBytesRemaining -= entryBytes.Length;
        AppendResultAtTail(resultBytes);

        if (runningSlot is int claimedSlot)
        {
            WriteFixedRegion(GetRunningSlotOffset(claimedSlot), CreateWhitespace(_runningSlotByteCapacity));
            _runningClaims[claimedSlot] = null;
            _ = _runningSlotsByExecutionId.Remove(executionId);
        }

        if (addDefinition)
        {
            _definitionNames.Add(testId, definitionName);
        }

        _completedResults.Add(result);
        _completedExecutionIds.Add(executionId);
        AddOutcome(result.Outcome);
        WriteMutableCounters();
        _ = claim;
    }

    public void UpdateFinishTime(DateTimeOffset finishTime)
    {
        EnsureMutable();
        byte[] bytes = Utf8.GetBytes(finishTime.ToString("O", CultureInfo.InvariantCulture));
        if (bytes.Length != Utf8.GetByteCount(_startTime.ToString("O", CultureInfo.InvariantCulture)))
        {
            throw new InvalidOperationException("The finish timestamp does not fit the fixed timestamp cell.");
        }

        WriteFixedRegion(_finishTimeOffset, bytes);
        _finishTime = finishTime;
    }

    public void Complete(TrxPrototypeCompletion completion)
    {
        EnsureMutable();
        if (completion is null)
        {
            throw new ArgumentNullException(nameof(completion));
        }

        if (_runningSlotsByExecutionId.Count != 0)
        {
            throw new InvalidOperationException(
                $"Cannot complete while {_runningSlotsByExecutionId.Count} running-test slots remain claimed.");
        }

        byte[] summaryAdditions = _renderer.RenderSummaryAdditions(completion);
        EnsureCapacity("summary", summaryAdditions.Length, _summaryBytesRemaining);
        string outcome = IsFailedCompletion(completion) ? "Failed" : "Completed";
        byte[] outcomeCell = CreateOutcomeCell(outcome);
        byte[] finishTime = Utf8.GetBytes(completion.FinishTime.ToString("O", CultureInfo.InvariantCulture));
        if (finishTime.Length != Utf8.GetByteCount(_startTime.ToString("O", CultureInfo.InvariantCulture)))
        {
            throw new InvalidOperationException("The completion timestamp does not fit the fixed timestamp cell.");
        }

        WriteFixedRegion(_finishTimeOffset, finishTime);
        if (summaryAdditions.Length > 0)
        {
            WriteFixedRegion(_summaryWriteOffset, summaryAdditions);
            _summaryWriteOffset += summaryAdditions.Length;
            _summaryBytesRemaining -= summaryAdditions.Length;
        }

        WriteFixedRegion(_outcomeCellOffset, outcomeCell);
        _finishTime = completion.FinishTime;
        _completed = true;
    }

    public void Compact(TrxPrototypeCompletion completion)
    {
        if (!_initialized)
        {
            throw new InvalidOperationException("The TRX prototype is not initialized.");
        }

        if (completion is null)
        {
            throw new ArgumentNullException(nameof(completion));
        }

        byte[] bytes = _renderer.RenderCompact(
            _runId,
            _runName,
            _startTime,
            _completedResults,
            _completedExecutionIds,
            completion);
        if (_snapshotPublisher is null)
        {
            using ITrxPrototypeFile file = _operations.Open(
                _path,
                FileMode.Create,
                FileAccess.ReadWrite,
                FileShare.Read | FileShare.Delete);
            file.Write(bytes, 0, bytes.Length);
            file.Flush();
        }
        else
        {
            _snapshotPublisher.Publish(
                _path,
                file => file.Write(bytes, 0, bytes.Length));
        }
    }

    private void CalculateLayout(byte[] document)
    {
        _counterOffsets.Clear();
        _definitionWriteOffset = FindAfter(document, "<TestDefinitions>");
        _definitionBytesRemaining = _definitionPadBytes;
        _entryWriteOffset = FindAfter(document, "<TestEntries>");
        _entryBytesRemaining = _entryPadBytes;
        _runningSlotsOffset = FindAfter(document, "<Results>");
        _resultTailOffset = Find(document, "</Results>");
        _finishTimeOffset = FindAfter(document, "finish=\"");
        _outcomeCellOffset = FindAfter(document, "<ResultSummary outcome=\"");

        long countersStart = Find(document, "<Counters ");
        long countersEnd = Find(document, "/>", countersStart) + 2;
        _summaryWriteOffset = countersEnd;
        _summaryBytesRemaining = _summaryPadBytes;
        foreach (string counterName in MutableCounterNames)
        {
            _counterOffsets.Add(counterName, FindAfter(document, $"{counterName}=\"", countersStart));
        }
    }

    private void ReflowAndAppend(
        TrxTestResult result,
        Guid executionId,
        int? runningSlot,
        string definitionName,
        bool addDefinition,
        int definitionByteCount,
        int entryByteCount)
    {
        int usedDefinitionBytes = _definitionPadBytes - _definitionBytesRemaining;
        int usedEntryBytes = _entryPadBytes - _entryBytesRemaining;
        int definitionCapacity = GrowCapacity(
            _definitionPadBytes,
            checked(usedDefinitionBytes + definitionByteCount));
        int entryCapacity = GrowCapacity(
            _entryPadBytes,
            checked(usedEntryBytes + entryByteCount));

        List<TrxTestResult> results = [.. _completedResults, result];
        List<Guid> executionIds = [.. _completedExecutionIds, executionId];
        byte[] replacement = BuildPaddedDocument(
            results,
            executionIds,
            definitionCapacity,
            entryCapacity,
            runningSlot,
            out int replacementDefinitionBytes,
            out int replacementEntryBytes);

        _snapshotPublisher!.Publish(
            _path,
            file => file.Write(replacement, 0, replacement.Length));

        _definitionPadBytes = definitionCapacity;
        _entryPadBytes = entryCapacity;
        CalculateLayout(replacement);
        _definitionWriteOffset += replacementDefinitionBytes;
        _definitionBytesRemaining -= replacementDefinitionBytes;
        _entryWriteOffset += replacementEntryBytes;
        _entryBytesRemaining -= replacementEntryBytes;

        if (runningSlot is int claimedSlot)
        {
            _runningClaims[claimedSlot] = null;
            _ = _runningSlotsByExecutionId.Remove(executionId);
        }

        if (addDefinition)
        {
            _definitionNames.Add(TrxPrototypeXmlRenderer.GetTestId(result.Uid), definitionName);
        }

        _completedResults.Add(result);
        _completedExecutionIds.Add(executionId);
        AddOutcome(result.Outcome);
    }

    private byte[] BuildPaddedDocument(
        IReadOnlyList<TrxTestResult> results,
        IReadOnlyList<Guid> executionIds,
        int definitionCapacity,
        int entryCapacity,
        int? releasedRunningSlot,
        out int definitionBytesUsed,
        out int entryBytesUsed)
    {
        byte[] initial = TrxPrototypeXmlRenderer.RenderInitial(
            _runId,
            _runName,
            _startTime,
            definitionCapacity,
            entryCapacity,
            _summaryPadBytes,
            _counterWidth,
            _runningSlotCount,
            _runningSlotByteCapacity);
        long definitionOffset = FindAfter(initial, "<TestDefinitions>");
        long entryOffset = FindAfter(initial, "<TestEntries>");
        long runningOffset = FindAfter(initial, "<Results>");
        long resultTailOffset = Find(initial, "</Results>");

        definitionBytesUsed = 0;
        entryBytesUsed = 0;
        var definitionIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        List<byte[]> resultFragments = [];
        int passed = 0;
        int failed = 0;
        int skipped = 0;
        int timeout = 0;
        for (int i = 0; i < results.Count; i++)
        {
            TrxTestResult completedResult = results[i];
            Guid completedExecutionId = executionIds[i];
            string testId = TrxPrototypeXmlRenderer.GetTestId(completedResult.Uid);
            if (definitionIds.Add(testId))
            {
                byte[] definition = _renderer.RenderDefinition(completedResult, completedExecutionId);
                CopyToFixedRegion(initial, definitionOffset + definitionBytesUsed, definition);
                definitionBytesUsed += definition.Length;
            }

            byte[] entry = TrxPrototypeXmlRenderer.RenderEntry(completedResult.Uid, completedExecutionId);
            CopyToFixedRegion(initial, entryOffset + entryBytesUsed, entry);
            entryBytesUsed += entry.Length;
            resultFragments.Add(_renderer.RenderCompletedResult(completedResult, completedExecutionId, _startTime));

            switch (completedResult.Outcome)
            {
                case TrxTestOutcome.Passed:
                    passed++;
                    break;
                case TrxTestOutcome.Failed:
                    failed++;
                    break;
                case TrxTestOutcome.Skipped:
                    skipped++;
                    break;
                case TrxTestOutcome.Timeout:
                    timeout++;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(results));
            }
        }

        for (int slot = 0; slot < _runningClaims.Length; slot++)
        {
            if (slot == releasedRunningSlot || _runningClaims[slot] is not { } claim)
            {
                continue;
            }

            byte[] running = _renderer.RenderRunningSlot(
                claim.Uid,
                claim.DisplayName,
                claim.ExecutionId,
                claim.StartTime,
                _runningSlotByteCapacity);
            CopyToFixedRegion(initial, runningOffset + ((long)slot * _runningSlotByteCapacity), running);
        }

        WritePaddedCounter(initial, "total", passed + failed + skipped + timeout);
        WritePaddedCounter(initial, "executed", passed + failed);
        WritePaddedCounter(initial, "passed", passed);
        WritePaddedCounter(initial, "failed", failed);
        WritePaddedCounter(initial, "timeout", timeout);
        WritePaddedCounter(initial, "notExecuted", skipped);
        WritePaddedCounter(
            initial,
            "inProgress",
            _runningSlotsByExecutionId.Count - (releasedRunningSlot.HasValue ? 1 : 0));
        CopyToFixedRegion(
            initial,
            FindAfter(initial, "finish=\""),
            Utf8.GetBytes(_finishTime.ToString("O", CultureInfo.InvariantCulture)));

        int resultBytes = resultFragments.Sum(fragment => fragment.Length);
        byte[] document = new byte[checked(initial.Length + resultBytes)];
        Array.Copy(initial, 0, document, 0, resultTailOffset);
        int destinationOffset = checked((int)resultTailOffset);
        foreach (byte[] fragment in resultFragments)
        {
            Array.Copy(fragment, 0, document, destinationOffset, fragment.Length);
            destinationOffset += fragment.Length;
        }

        Array.Copy(
            initial,
            resultTailOffset,
            document,
            destinationOffset,
            initial.LongLength - resultTailOffset);
        return document;
    }

    private void WritePaddedCounter(byte[] document, string name, int value)
        => CopyToFixedRegion(
            document,
            FindAfter(document, $"{name}=\""),
            Utf8.GetBytes(value.ToString($"D{_counterWidth}", CultureInfo.InvariantCulture)));

    private static void CopyToFixedRegion(byte[] destination, long offset, byte[] source)
    {
        if (offset < 0 || offset > destination.LongLength - source.LongLength)
        {
            throw new InvalidOperationException("The rendered prototype fragment does not fit its fixed region.");
        }

        Array.Copy(source, 0, destination, offset, source.LongLength);
    }

    private static int GrowCapacity(int currentCapacity, int requiredCapacity)
    {
        if (requiredCapacity <= currentCapacity)
        {
            return currentCapacity;
        }

        int capacity = Math.Max(currentCapacity, 256);
        while (capacity < requiredCapacity)
        {
            capacity = checked(capacity * 2);
        }

        return capacity;
    }

    private void AppendResultAtTail(byte[] resultBytes)
    {
        byte[] closers = Utf8.GetBytes(ResultsClosers);
        byte[] bytes = new byte[resultBytes.Length + closers.Length];
        Array.Copy(resultBytes, bytes, resultBytes.Length);
        Array.Copy(closers, 0, bytes, resultBytes.Length, closers.Length);

        using ITrxPrototypeFile file = _operations.Open(
            _path,
            FileMode.Open,
            FileAccess.ReadWrite,
            FileShare.Read | FileShare.Delete);
        file.Seek(_resultTailOffset, SeekOrigin.Begin);
        file.Write(bytes, 0, bytes.Length);
        file.SetLength(_resultTailOffset + bytes.Length);
        file.Flush();
        _resultTailOffset += resultBytes.Length;
    }

    private void WriteMutableCounters()
    {
        WriteCounter("total", _passed + _failed + _skipped + _timeout);
        WriteCounter("executed", _passed + _failed);
        WriteCounter("passed", _passed);
        WriteCounter("failed", _failed);
        WriteCounter("timeout", _timeout);
        WriteCounter("notExecuted", _skipped);
        WriteCounter("inProgress", _runningSlotsByExecutionId.Count);
    }

    private void WriteCounter(string name, int value)
    {
        byte[] bytes = Utf8.GetBytes(value.ToString($"D{_counterWidth}", CultureInfo.InvariantCulture));
        if (bytes.Length != _counterWidth)
        {
            throw new InvalidOperationException(
                $"Counter '{name}' value {value} does not fit the fixed width {_counterWidth}.");
        }

        WriteFixedRegion(_counterOffsets[name], bytes);
    }

    private void WriteFixedRegion(long offset, byte[] bytes)
    {
        using ITrxPrototypeFile file = _operations.Open(
            _path,
            FileMode.Open,
            FileAccess.ReadWrite,
            FileShare.Read | FileShare.Delete);
        file.Seek(offset, SeekOrigin.Begin);
        file.Write(bytes, 0, bytes.Length);
        file.Flush();
    }

    private long GetRunningSlotOffset(int slot)
        => _runningSlotsOffset + ((long)slot * _runningSlotByteCapacity);

    private void AddOutcome(TrxTestOutcome outcome)
    {
        switch (outcome)
        {
            case TrxTestOutcome.Passed:
                _passed++;
                break;
            case TrxTestOutcome.Failed:
                _failed++;
                break;
            case TrxTestOutcome.Skipped:
                _skipped++;
                break;
            case TrxTestOutcome.Timeout:
                _timeout++;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(outcome));
        }
    }

    private void EnsureCounterCapacity()
    {
        int maximumValue = _counterWidth == 10
            ? int.MaxValue
            : (int)Math.Pow(10, _counterWidth) - 1;
        if (_completedResults.Count == maximumValue)
        {
            throw new InvalidOperationException(
                $"Another completed result would exceed the fixed counter width {_counterWidth}.");
        }
    }

    private bool IsFailedCompletion(TrxPrototypeCompletion completion)
        => completion.IsTestHostCrashed
            || completion.ExitCode != 0
            || _failed > 0
            || _timeout > 0;

    private void EnsureMutable()
    {
        if (!_initialized)
        {
            throw new InvalidOperationException("The TRX prototype is not initialized.");
        }

        if (_completed)
        {
            throw new InvalidOperationException("The TRX prototype is already complete.");
        }
    }

    private static void EnsureCapacity(string region, int requestedBytes, int remainingBytes)
    {
        if (requestedBytes > remainingBytes)
        {
            throw new InvalidOperationException(
                $"The {region} fragment requires {requestedBytes} UTF-8 bytes, but only {remainingBytes} bytes remain in its pad.");
        }
    }

    private static byte[] CreateOutcomeCell(string outcome)
    {
        string cell = outcome == "Completed" ? "Completed\"" : "Failed\"   ";
        byte[] bytes = Utf8.GetBytes(cell);
        return bytes.Length == OutcomeCellWidth
            ? bytes
            : throw new InvalidOperationException("The result outcome does not fit the fixed outcome cell.");
    }

    private static byte[] CreateWhitespace(int count)
    {
        byte[] bytes = new byte[count];
        for (int i = 0; i < bytes.Length; i++)
        {
            bytes[i] = (byte)' ';
        }

        return bytes;
    }

    private static void ThrowIfNullOrEmpty(string value, string parameterName)
    {
        if (RoslynString.IsNullOrEmpty(value))
        {
            throw new ArgumentException("The value cannot be null or empty.", parameterName);
        }
    }

    private static long FindAfter(byte[] bytes, string marker, long start = 0)
        => Find(bytes, marker, start) + Utf8.GetByteCount(marker);

    private static long Find(byte[] bytes, string marker, long start = 0)
    {
        byte[] markerBytes = Utf8.GetBytes(marker);
        for (long i = start; i <= bytes.LongLength - markerBytes.Length; i++)
        {
            bool matches = true;
            for (int j = 0; j < markerBytes.Length; j++)
            {
                if (bytes[i + j] != markerBytes[j])
                {
                    matches = false;
                    break;
                }
            }

            if (matches)
            {
                return i;
            }
        }

        throw new InvalidOperationException($"The rendered prototype document does not contain marker '{marker}'.");
    }

    private sealed class RunningClaim(
        string uid,
        string displayName,
        Guid executionId,
        DateTimeOffset startTime)
    {
        public string Uid { get; } = uid;

        public string DisplayName { get; } = displayName;

        public Guid ExecutionId { get; } = executionId;

        public DateTimeOffset StartTime { get; } = startTime;
    }
}
