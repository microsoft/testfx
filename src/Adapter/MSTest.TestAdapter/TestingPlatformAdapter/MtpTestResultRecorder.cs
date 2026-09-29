// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Extensions.TestFramework;
using Microsoft.Testing.Platform.Messages;
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
    private readonly ExecuteRequestContext? _executeRequestContext;
    private readonly IDataProducer _dataProducer;
    private readonly SessionUid _sessionUid;
    private readonly bool _isTrxEnabled;
    private readonly MSTestSettings _settings;
    private readonly Action<FrameworkTestResult> _stageResultFiles;
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
        : this(messageBus, executeRequestContext: null, dataProducer, sessionUid, isTrxEnabled, settings, static _ => { })
    {
    }

    public MtpTestResultRecorder(
        IMessageBus messageBus,
        IDataProducer dataProducer,
        SessionUid sessionUid,
        bool isTrxEnabled,
        MSTestSettings settings,
        Action<FrameworkTestResult> stageResultFiles)
        : this(messageBus, executeRequestContext: null, dataProducer, sessionUid, isTrxEnabled, settings, stageResultFiles)
    {
    }

    internal MtpTestResultRecorder(
        ExecuteRequestContext executeRequestContext,
        IDataProducer dataProducer,
        SessionUid sessionUid,
        bool isTrxEnabled,
        MSTestSettings settings)
        : this(executeRequestContext.MessageBus, executeRequestContext, dataProducer, sessionUid, isTrxEnabled, settings, static _ => { })
    {
    }

    internal MtpTestResultRecorder(
        ExecuteRequestContext executeRequestContext,
        IDataProducer dataProducer,
        SessionUid sessionUid,
        bool isTrxEnabled,
        MSTestSettings settings,
        Action<FrameworkTestResult> stageResultFiles)
        : this(executeRequestContext.MessageBus, executeRequestContext, dataProducer, sessionUid, isTrxEnabled, settings, stageResultFiles)
    {
    }

    private MtpTestResultRecorder(
        IMessageBus messageBus,
        ExecuteRequestContext? executeRequestContext,
        IDataProducer dataProducer,
        SessionUid sessionUid,
        bool isTrxEnabled,
        MSTestSettings settings,
        Action<FrameworkTestResult> stageResultFiles)
    {
        _messageBus = messageBus;
        _executeRequestContext = executeRequestContext;
        _dataProducer = dataProducer;
        _sessionUid = sessionUid;
        _isTrxEnabled = isTrxEnabled;
        _settings = settings;
        _stageResultFiles = stageResultFiles;
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
        if (testElement.SupportsExecutionActivityLease && _executeRequestContext is not null)
        {
            Occurrence occurrence;
            lock (_syncRoot)
            {
                if (!_occurrences.TryGetValue(testElement, out occurrence!))
                {
                    occurrence = new Occurrence(_executeRequestContext.StartTestExecutionAsync(
                        _dataProducer,
                        new TestNodeUpdateMessage(_sessionUid, testNode)));
                    _occurrences.Add(testElement, occurrence);
                }
            }

            try
            {
                TestExecution execution = await occurrence.ExecutionTask.ConfigureAwait(false);
                testElement.ExecutionActivityLease = occurrence.GetOrCreateLease(execution);
                return;
            }
            catch
            {
                lock (_syncRoot)
                {
                    if (_occurrences.TryGetValue(testElement, out Occurrence? current)
                        && ReferenceEquals(current, occurrence))
                    {
                        _occurrences.Remove(testElement);
                    }
                }

                throw;
            }
        }

        await PublishAsync(testNode).ConfigureAwait(false);
    }

    public async Task RecordEmptyResultAsync(UnitTestElement testElement)
    {
        TestNode testNode = MSTestTestNodeConverter.ToEmptyResultTestNode(testElement, _isTrxEnabled);
        Occurrence? occurrence = TakeOccurrence(testElement, isResult: true);
        if (occurrence is not null)
        {
            TestExecution execution = await occurrence.ExecutionTask.ConfigureAwait(false);
            await execution.CompleteAsync(
                [new TestNodeUpdateMessage(_sessionUid, testNode)],
                DateTimeOffset.UtcNow).ConfigureAwait(false);
            return;
        }

        await PublishAsync(testNode).ConfigureAwait(false);
    }

    public async Task<bool> RecordResultAsync(UnitTestElement testElement, FrameworkTestResult unitTestResult, DateTimeOffset startTime, DateTimeOffset endTime)
    {
        var outcome = UnitTestOutcomeHelper.ToTestOutcome(unitTestResult.Outcome, _settings);
        bool isFailed = outcome == TestOutcome.Failed;
        Occurrence? occurrence = TakeOccurrence(testElement, isResult: true);
        TestNodeUpdateMessage? resultMessage = null;

        // Mirror TestResultRecorderExtensions: a NotFound result is not reported while hot reload is enabled.
        if (outcome != TestOutcome.NotFound || !RuntimeContext.IsHotReloadEnabled)
        {
            _stageResultFiles(unitTestResult);
            TestNode testNode = MSTestTestNodeConverter.ToResultTestNode(testElement, unitTestResult, startTime, endTime, _isTrxEnabled, _settings);
            resultMessage = new TestNodeUpdateMessage(_sessionUid, testNode);
        }

        if (occurrence is null)
        {
            if (resultMessage is not null)
            {
                await _messageBus.PublishAsync(_dataProducer, resultMessage).ConfigureAwait(false);
            }

            return isFailed;
        }

        TestNodeUpdateMessage[]? messagesToComplete = null;
        lock (_syncRoot)
        {
            if (resultMessage is not null)
            {
                occurrence.ResultMessages.Add(resultMessage);
            }

            if (occurrence.IsFinalResult)
            {
                messagesToComplete = [.. occurrence.ResultMessages];
            }
        }

        if (messagesToComplete is not null)
        {
            TestExecution execution = await occurrence.ExecutionTask.ConfigureAwait(false);
            await execution.CompleteAsync(messagesToComplete, endTime).ConfigureAwait(false);
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

    private sealed class Occurrence(Task<TestExecution> executionTask)
    {
        private ITestExecutionActivityLease? _lease;

        public Task<TestExecution> ExecutionTask { get; } = executionTask;

        public List<TestNodeUpdateMessage> ResultMessages { get; } = [];

        public int RemainingResults { get; set; } = 1;

        public bool IsFinalResult { get; set; }

        public ITestExecutionActivityLease GetOrCreateLease(TestExecution execution)
            => _lease ??= new MSTestExecutionActivityLease(execution);
    }

    private sealed class MSTestExecutionActivityLease(TestExecution execution) : ITestExecutionActivityLease
    {
        public Task<T> RunAsync<T>(Func<Task<T>> callback)
            => execution.RunAsync(callback);
    }
}
