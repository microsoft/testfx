// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if !WINDOWS_UWP
using Microsoft.Testing.Platform.Extensions.Messages;
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
    private readonly IDataProducer _dataProducer;
    private readonly SessionUid _sessionUid;
    private readonly bool _isTrxEnabled;
    private readonly MSTestSettings _settings;

    public MtpTestResultRecorder(IMessageBus messageBus, IDataProducer dataProducer, SessionUid sessionUid, bool isTrxEnabled, MSTestSettings settings)
    {
        _messageBus = messageBus;
        _dataProducer = dataProducer;
        _sessionUid = sessionUid;
        _isTrxEnabled = isTrxEnabled;
        _settings = settings;
    }

    public void RecordStart(UnitTestElement testElement)
    {
        TestNode testNode = MSTestTestNodeConverter.ToInProgressTestNode(testElement, _isTrxEnabled);
        Publish(testNode);
    }

    // Mirrors the VSTest recorder: an empty result maps to RecordEnd(TestOutcome.None), which does not publish any
    // test node update to Microsoft.Testing.Platform. So there is nothing to report here.
    public void RecordEmptyResult(UnitTestElement testElement)
    {
    }

    public bool RecordResult(UnitTestElement testElement, FrameworkTestResult unitTestResult, DateTimeOffset startTime, DateTimeOffset endTime)
    {
        var outcome = UnitTestOutcomeHelper.ToTestOutcome(unitTestResult.Outcome, _settings);
        bool isFailed = outcome == TestOutcome.Failed;

        // Mirror TestResultRecorderExtensions: a NotFound result is not reported while hot reload is enabled.
        if (outcome != TestOutcome.NotFound || !RuntimeContext.IsHotReloadEnabled)
        {
            TestNode testNode = MSTestTestNodeConverter.ToResultTestNode(testElement, unitTestResult, startTime, endTime, _isTrxEnabled, _settings);
            Publish(testNode);
        }

        return isFailed;
    }

    private void Publish(TestNode testNode)
        => _messageBus.PublishAsync(_dataProducer, new TestNodeUpdateMessage(_sessionUid, testNode)).GetAwaiter().GetResult();
}
#endif
