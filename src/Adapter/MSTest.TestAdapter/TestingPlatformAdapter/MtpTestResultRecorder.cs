// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Messages;
using Microsoft.Testing.Platform.Telemetry;
using Microsoft.Testing.Platform.TestHost;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Helpers;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Interface;
using Microsoft.VisualStudio.TestPlatform.ObjectModel;

using FrameworkTestResult = Microsoft.VisualStudio.TestTools.UnitTesting.TestResult;

namespace Microsoft.VisualStudio.TestTools.UnitTesting;

/// <summary>
/// A Microsoft.Testing.Platform-native <see cref="ITestResultRecorder"/> that reports test lifecycle and results
/// directly as <see cref="TestNodeUpdateMessage"/>s on the platform message bus, without materializing a VSTest
/// <c>TestResult</c> or going through the VSTest bridge.
/// </summary>
[SuppressMessage("ApiDesign", "RS0030:Do not use banned APIs", Justification = "We can use MTP from this folder")]
internal sealed class MtpTestResultRecorder : ITestResultRecorder
{
    private readonly IMessageBus _messageBus;
    private readonly IDataProducer _dataProducer;
    private readonly SessionUid _sessionUid;
    private readonly bool _isTrxEnabled;
    private readonly MSTestSettings _settings;
    private readonly TestExecutionActivityBroker? _testExecutionActivityBroker;
    private readonly Dictionary<UnitTestElement, Occurrence> _occurrences = [];
#if NET9_0_OR_GREATER
    private readonly Lock _syncRoot = new();
#else
    private readonly object _syncRoot = new();
#endif

    public MtpTestResultRecorder(
        IMessageBus messageBus,
        IDataProducer dataProducer,
        SessionUid sessionUid,
        bool isTrxEnabled,
        MSTestSettings settings)
        : this(messageBus, dataProducer, sessionUid, isTrxEnabled, settings, testExecutionActivityBroker: null)
    {
    }

    internal MtpTestResultRecorder(
        IMessageBus messageBus,
        IDataProducer dataProducer,
        SessionUid sessionUid,
        bool isTrxEnabled,
        MSTestSettings settings,
        TestExecutionActivityBroker? testExecutionActivityBroker)
    {
        _messageBus = messageBus;
        _dataProducer = dataProducer;
        _sessionUid = sessionUid;
        _isTrxEnabled = isTrxEnabled;
        _settings = settings;
        _testExecutionActivityBroker = testExecutionActivityBroker;
    }

    void ITestResultRecorder.PrepareResults(UnitTestElement testElement, FrameworkTestResult[] results)
    {
        lock (_syncRoot)
        {
            if (_occurrences.TryGetValue(testElement, out Occurrence? occurrence))
            {
                occurrence.RemainingResults = results.Length;
            }
        }
    }

    public async Task RecordStartAsync(UnitTestElement testElement)
    {
        TestNode testNode = MSTestTestNodeConverter.ToInProgressTestNode(testElement, _isTrxEnabled);
        Occurrence? occurrence;
        lock (_syncRoot)
        {
            _occurrences.TryGetValue(testElement, out occurrence);
        }

        TestExecutionActivityReservation? discardedReservation = null;
        if (occurrence is null && _testExecutionActivityBroker?.Reserve(testNode) is { } reservation)
        {
            var candidate = new Occurrence(reservation);
            lock (_syncRoot)
            {
                if (_occurrences.TryGetValue(testElement, out occurrence))
                {
                    discardedReservation = reservation;
                }
                else
                {
                    _occurrences.Add(testElement, candidate);
                    occurrence = candidate;
                }
            }
        }

        if (discardedReservation is not null)
        {
            discardedReservation.RecordExecutionEnd(DateTimeOffset.UtcNow);
            discardedReservation.CompleteWithoutResult();
        }

        if (occurrence is not null)
        {
            testElement.ExecutionActivityLease = occurrence.Lease;
            testNode.Properties.Add(new TestExecutionActivityProperty(occurrence.Reservation, isFinalResult: false));
        }

        await PublishAsync(testNode).ConfigureAwait(false);
        occurrence?.Reservation.Activate();
    }

    public async Task RecordEmptyResultAsync(UnitTestElement testElement)
    {
        TestNode testNode = MSTestTestNodeConverter.ToEmptyResultTestNode(testElement, _isTrxEnabled);
        Occurrence? occurrence = TakeOccurrence(testElement, isResult: true);
        if (occurrence is not null)
        {
            occurrence.Reservation.RecordExecutionEnd(DateTimeOffset.UtcNow);
            testNode.Properties.Add(new TestExecutionActivityProperty(occurrence.Reservation, isFinalResult: true));
        }

        await PublishAsync(testNode).ConfigureAwait(false);
    }

    public async Task<bool> RecordResultAsync(UnitTestElement testElement, FrameworkTestResult unitTestResult, DateTimeOffset startTime, DateTimeOffset endTime)
    {
        var outcome = UnitTestOutcomeHelper.ToTestOutcome(unitTestResult.Outcome, _settings);
        bool isFailed = outcome == TestOutcome.Failed;
        Occurrence? occurrence = TakeOccurrence(testElement, isResult: true);

        // Mirror TestResultRecorderExtensions: a NotFound result is not reported while hot reload is enabled.
        if (outcome != TestOutcome.NotFound || !RuntimeContext.IsHotReloadEnabled)
        {
            TestNode testNode = MSTestTestNodeConverter.ToResultTestNode(testElement, unitTestResult, startTime, endTime, _isTrxEnabled, _settings);
            if (occurrence is not null)
            {
                occurrence.Reservation.RecordExecutionEnd(endTime);
                testNode.Properties.Add(new TestExecutionActivityProperty(occurrence.Reservation, occurrence.IsFinalResult));
            }

            await PublishAsync(testNode).ConfigureAwait(false);
        }
        else if (occurrence is not null)
        {
            occurrence.Reservation.RecordExecutionEnd(endTime);
            if (occurrence.IsFinalResult)
            {
                occurrence.Reservation.CompleteWithoutResult();
            }
        }

        return isFailed;
    }

    private Task PublishAsync(TestNode testNode)
        => _messageBus.PublishAsync(_dataProducer, new TestNodeUpdateMessage(_sessionUid, testNode));

    private Occurrence? TakeOccurrence(UnitTestElement testElement, bool isResult)
    {
        lock (_syncRoot)
        {
            if (!_occurrences.TryGetValue(testElement, out Occurrence? occurrence))
            {
                return null;
            }

            if (isResult)
            {
                occurrence.RemainingResults = Math.Max(0, occurrence.RemainingResults - 1);
                occurrence.IsFinalResult = occurrence.RemainingResults == 0;
            }

            if (occurrence.IsFinalResult)
            {
                _occurrences.Remove(testElement);
                testElement.ExecutionActivityLease = null;
            }

            return occurrence;
        }
    }

    private sealed class Occurrence(TestExecutionActivityReservation reservation)
    {
        public TestExecutionActivityReservation Reservation { get; } = reservation;

        public ITestExecutionActivityLease Lease { get; } = new MSTestExecutionActivityLease(reservation);

        public int RemainingResults { get; set; } = 1;

        public bool IsFinalResult { get; set; }
    }

    private sealed class MSTestExecutionActivityLease(TestExecutionActivityReservation reservation) : ITestExecutionActivityLease
    {
        public IDisposable? Enter()
            => reservation.Enter();

        public void RecordExecutionEnd(DateTimeOffset endTime)
            => reservation.RecordExecutionEnd(endTime);
    }
}
