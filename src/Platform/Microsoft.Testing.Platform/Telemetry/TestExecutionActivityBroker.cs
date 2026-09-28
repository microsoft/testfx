// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Extensions.Messages;

namespace Microsoft.Testing.Platform.Telemetry;

internal sealed class TestExecutionActivityBroker(
    IPlatformOpenTelemetryServiceWithTestExecutionActivities otelService,
    PlatformOpenTelemetryOptions options) : IDisposable
{
#if NET9_0_OR_GREATER
    private readonly Lock _syncRoot = new();
#else
    private readonly object _syncRoot = new();
#endif
    private readonly Dictionary<long, Entry> _entries = [];
    private long _nextToken;
    private bool _disposed;

    public TestExecutionActivityReservation? Reserve(TestNode testNode, TestNodeUid? parentUid = null)
    {
        string activityName = OpenTelemetryResultHandler.GetActivityName(testNode);
        KeyValuePair<string, object?>[] initialInfo =
        [
            .. OpenTelemetryResultHandler.GetTestInitialInfo(testNode, parentUid, options),
        ];
        string? parentId = otelService.TestFrameworkActivity?.Id;
        long token;
        lock (_syncRoot)
        {
            if (_disposed)
            {
                return null;
            }

            token = ++_nextToken;
            _entries.Add(token, new Entry(activityName, initialInfo, parentId));
        }

        return new TestExecutionActivityReservation(this, token);
    }

    internal void Activate(long token)
    {
        Entry? entry;
        DateTimeOffset startTime = DateTimeOffset.UtcNow;
        lock (_syncRoot)
        {
            if (!_entries.TryGetValue(token, out entry)
                || entry.Finalized
                || entry.Activated
                || entry.ActivationInProgress)
            {
                return;
            }

            entry.ActivationInProgress = true;
        }

        IPlatformTestExecutionActivity? activity;
        try
        {
            activity = otelService.StartTestExecutionActivity(
                entry.ActivityName,
                entry.InitialInfo,
                entry.ParentId,
                startTime);
        }
        catch
        {
            FinalizationWork? failedActivationWork = null;
            lock (_syncRoot)
            {
                if (_entries.TryGetValue(token, out Entry? currentEntry)
                    && ReferenceEquals(currentEntry, entry)
                    && !entry.Finalized)
                {
                    entry.ActivationInProgress = false;
                    entry.Sealed = true;
                    failedActivationWork = ClaimFinalization(token, entry, startTime);
                }
            }

            if (failedActivationWork is not null)
            {
                FinalizeActivity(failedActivationWork.Value);
            }

            throw;
        }

        FinalizationWork? work = null;
        bool stopActivity;
        lock (_syncRoot)
        {
            if (!_entries.TryGetValue(token, out Entry? currentEntry)
                || !ReferenceEquals(currentEntry, entry)
                || entry.Finalized)
            {
                stopActivity = true;
            }
            else
            {
                entry.Activity = activity;
                entry.StartTime = startTime;
                entry.ActivationInProgress = false;
                entry.Activated = true;
                stopActivity = false;
                if (entry.Sealed)
                {
                    work = ClaimFinalization(token, entry, entry.ExecutionEnd ?? DateTimeOffset.UtcNow);
                }
            }
        }

        if (stopActivity)
        {
            activity?.Stop(DateTimeOffset.UtcNow);
        }
        else if (work is not null)
        {
            FinalizeActivity(work.Value);
        }
    }

    public void Dispose()
    {
        List<FinalizationWork> work = [];
        lock (_syncRoot)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            foreach ((long token, Entry entry) in _entries.ToArray())
            {
                entry.Sealed = true;
                if (!entry.ActivationInProgress)
                {
                    work.Add(ClaimFinalization(token, entry, DateTimeOffset.UtcNow));
                }
            }
        }

        FinalizeActivities(work);
    }

    internal IDisposable? Enter(long token)
    {
        IPlatformTestExecutionActivity? activity;
        lock (_syncRoot)
        {
            if (!_entries.TryGetValue(token, out Entry? entry) || entry.Finalized)
            {
                return null;
            }

            activity = entry.Activity;
        }

        return activity?.Enter();
    }

    internal void RecordExecutionEnd(long token, DateTimeOffset endTime)
    {
        FinalizationWork? work = null;
        lock (_syncRoot)
        {
            if (!_entries.TryGetValue(token, out Entry? entry) || entry.Finalized)
            {
                return;
            }

            entry.ExecutionEnd ??= endTime;
            if (entry.Sealed && !entry.ActivationInProgress)
            {
                work = ClaimFinalization(token, entry, endTime);
            }
        }

        if (work is not null)
        {
            FinalizeActivity(work.Value);
        }
    }

    internal bool ProcessResult(
        long token,
        string result,
        bool contributesToAggregate,
        bool isFinalResult,
        Action<IPlatformActivity?, string?, DateTimeOffset, TimeSpan> enrich)
    {
        FinalizationWork? work = null;
        lock (_syncRoot)
        {
            if (!_entries.TryGetValue(token, out Entry? entry) || entry.Finalized)
            {
                return false;
            }

            if (contributesToAggregate)
            {
                int resultPriority = GetResultPriority(result);
                if (resultPriority >= GetResultPriority(entry.AggregateResult))
                {
                    entry.AggregateResult = result;
                    entry.Enrich = enrich;
                }
            }

            if (isFinalResult)
            {
                entry.Sealed = true;
            }

            if (entry.Sealed && entry.ExecutionEnd is not null && !entry.ActivationInProgress)
            {
                work = ClaimFinalization(token, entry, entry.ExecutionEnd.Value);
            }
        }

        if (work is not null)
        {
            FinalizeActivity(work.Value);
        }

        return true;
    }

    internal void CompleteWithoutResult(long token)
    {
        FinalizationWork? work = null;
        lock (_syncRoot)
        {
            if (!_entries.TryGetValue(token, out Entry? entry) || entry.Finalized)
            {
                return;
            }

            entry.Sealed = true;
            if (entry.ExecutionEnd is not null && !entry.ActivationInProgress)
            {
                work = ClaimFinalization(token, entry, entry.ExecutionEnd.Value);
            }
        }

        if (work is not null)
        {
            FinalizeActivity(work.Value);
        }
    }

    private FinalizationWork ClaimFinalization(long token, Entry entry, DateTimeOffset fallbackEndTime)
    {
        entry.Finalized = true;
        _entries.Remove(token);
        DateTimeOffset endTime = entry.ExecutionEnd ?? fallbackEndTime;
        DateTimeOffset startTime = entry.StartTime ?? endTime;
        return new FinalizationWork(
            entry.Activity,
            entry.AggregateResult,
            entry.Enrich,
            endTime,
            endTime - startTime);
    }

    private static void FinalizeActivities(List<FinalizationWork> work)
    {
        foreach (FinalizationWork item in work)
        {
            FinalizeActivity(item);
        }
    }

    private static void FinalizeActivity(FinalizationWork work)
    {
        try
        {
            work.Enrich?.Invoke(work.Activity, work.AggregateResult, work.EndTime, work.Duration);
        }
        finally
        {
            work.Activity?.Stop(work.EndTime);
        }
    }

    private static int GetResultPriority(string? result)
        => result switch
        {
            TestingPlatformSemanticConventions.TestResultStatus.Error => 6,
            TestingPlatformSemanticConventions.TestResultStatus.Timeout => 5,
            TestingPlatformSemanticConventions.TestResultStatus.Fail => 4,
            TestingPlatformSemanticConventions.TestResultStatus.Cancelled => 3,
            TestingPlatformSemanticConventions.TestResultStatus.Pass => 2,
            TestingPlatformSemanticConventions.TestResultStatus.Skipped => 1,
            _ => 0,
        };

    private sealed class Entry(
        string activityName,
        KeyValuePair<string, object?>[] initialInfo,
        string? parentId)
    {
        public string ActivityName { get; } = activityName;

        public KeyValuePair<string, object?>[] InitialInfo { get; } = initialInfo;

        public string? ParentId { get; } = parentId;

        public IPlatformTestExecutionActivity? Activity { get; set; }

        public DateTimeOffset? StartTime { get; set; }

        public DateTimeOffset? ExecutionEnd { get; set; }

        public string? AggregateResult { get; set; }

        public Action<IPlatformActivity?, string?, DateTimeOffset, TimeSpan>? Enrich { get; set; }

        public bool Sealed { get; set; }

        public bool Finalized { get; set; }

        public bool ActivationInProgress { get; set; }

        public bool Activated { get; set; }
    }

    private readonly record struct FinalizationWork(
        IPlatformTestExecutionActivity? Activity,
        string? AggregateResult,
        Action<IPlatformActivity?, string?, DateTimeOffset, TimeSpan>? Enrich,
        DateTimeOffset EndTime,
        TimeSpan Duration);
}

internal sealed class TestExecutionActivityReservation(TestExecutionActivityBroker broker, long token)
{
    public void Activate()
        => broker.Activate(token);

    public IDisposable? Enter()
        => broker.Enter(token);

    public void RecordExecutionEnd(DateTimeOffset endTime)
        => broker.RecordExecutionEnd(token, endTime);

    public bool ProcessResult(
        string result,
        bool contributesToAggregate,
        bool isFinalResult,
        Action<IPlatformActivity?, string?, DateTimeOffset, TimeSpan> enrich)
        => broker.ProcessResult(token, result, contributesToAggregate, isFinalResult, enrich);

    public void CompleteWithoutResult()
        => broker.CompleteWithoutResult(token);
}
