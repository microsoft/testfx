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
        DateTimeOffset startTime = DateTimeOffset.UtcNow;
        long token;
        lock (_syncRoot)
        {
            if (_disposed)
            {
                return null;
            }

            token = ++_nextToken;
        }

        IPlatformTestExecutionActivity? activity = otelService.StartTestExecutionActivity(
            OpenTelemetryResultHandler.GetActivityName(testNode),
            OpenTelemetryResultHandler.GetTestInitialInfo(testNode, parentUid, options),
            otelService.TestFrameworkActivity?.Id,
            startTime);

        bool stopActivity;
        lock (_syncRoot)
        {
            if (_disposed)
            {
                stopActivity = true;
            }
            else
            {
                _entries.Add(token, new Entry(activity, startTime));
                stopActivity = false;
            }
        }

        if (stopActivity)
        {
            activity?.Stop(DateTimeOffset.UtcNow);
            return null;
        }

        return new TestExecutionActivityReservation(this, token);
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
                work.Add(ClaimFinalization(token, entry, DateTimeOffset.UtcNow));
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
            if (entry.Sealed)
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

            if (entry.Sealed && entry.ExecutionEnd is not null)
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
            if (entry.ExecutionEnd is not null)
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
        return new FinalizationWork(
            entry.Activity,
            entry.AggregateResult,
            entry.Enrich,
            endTime,
            endTime - entry.StartTime);
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

    private sealed class Entry(IPlatformTestExecutionActivity? activity, DateTimeOffset startTime)
    {
        public IPlatformTestExecutionActivity? Activity { get; } = activity;

        public DateTimeOffset StartTime { get; } = startTime;

        public DateTimeOffset? ExecutionEnd { get; set; }

        public string? AggregateResult { get; set; }

        public Action<IPlatformActivity?, string?, DateTimeOffset, TimeSpan>? Enrich { get; set; }

        public bool Sealed { get; set; }

        public bool Finalized { get; set; }
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
