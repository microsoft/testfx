// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.Execution;

using System;
using System.IO;
using System.Text;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Execution;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Interface;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.TestableImplementations;
using Moq;

using TestFramework.ForTestingMSTest;

using UTF = Microsoft.VisualStudio.TestTools.UnitTesting;

public class LogMessageListenerTests : TestContainer
{
    private TestablePlatformServiceProvider _testablePlatformServiceProvider;

    [TestInitialize]
    public void TestInit()
    {
        _testablePlatformServiceProvider = new TestablePlatformServiceProvider();
        PlatformServiceProvider.Instance = _testablePlatformServiceProvider;
    }

    [TestCleanup]
    public void Testcleanup()
    {
        PlatformServiceProvider.Instance = null;
    }

    public void LogMessageListenerShouldCaptureTestFrameworkLogMessages()
    {
        using var logMessageListener = new LogMessageListener(false);
        UTF.Logging.Logger.LogMessage("sample log {0}", 123);

        Assert.AreEqual("sample log 123" + Environment.NewLine, logMessageListener.StandardOutput);
    }

    public void NoTraceListenerOperationShouldBePerformedIfDebugTraceIsNotEnabled()
    {
        using var logMessageListener = new LogMessageListener(false);
        _testablePlatformServiceProvider.MockTraceListenerManager.Verify(mtlm => mtlm.Add(It.IsAny<ITraceListener>()), Times.Never);
    }

    public void AddTraceListenerOperationShouldBePerformedIfDebugTraceIsEnabled()
    {
        using var logMessageListener = new LogMessageListener(true);
        _testablePlatformServiceProvider.MockTraceListenerManager.Verify(mtlm => mtlm.Add(_testablePlatformServiceProvider.MockTraceListener.Object), Times.Once);
    }

    #region Dispose Tests

    public void DisposeShouldNotRemoveTraceListenerIfDebugTracesIsNotEnabled()
    {
        using var logMessageListener = new LogMessageListener(false);
        logMessageListener.Dispose();
        _testablePlatformServiceProvider.MockTraceListenerManager.Verify(mtlm => mtlm.Remove(It.IsAny<ITraceListener>()), Times.Never);
    }

    public void DisposeShouldRemoveTraceListenerIfDebugTracesIsEnabled()
    {
        using (var logMessageListener = new LogMessageListener(true))
        {
            logMessageListener.Dispose();
        }

        // Once when Dispose() is called and second time when destructor is called
        _testablePlatformServiceProvider.MockTraceListenerManager.Verify(mtlm => mtlm.Remove(It.IsAny<ITraceListener>()), Times.Exactly(1));
    }

    public void DisposeShouldDisposeTraceListener()
    {
        using var logMessageListener = new LogMessageListener(true);
        logMessageListener.Dispose();
        _testablePlatformServiceProvider.MockTraceListenerManager.Verify(mtlm => mtlm.Dispose(_testablePlatformServiceProvider.MockTraceListener.Object), Times.Once);
    }

    #endregion

    public void LogMessageListenerShouldCaptureLogMessagesInAllListeningScopes()
    {
        using var logMessageListener1 = new LogMessageListener(false);
        using (var logMessageListener2 = new LogMessageListener(false))
        {
            UTF.Logging.Logger.LogMessage("sample log {0}", 123);

            Assert.AreEqual("sample log 123" + Environment.NewLine, logMessageListener2.StandardOutput);
        }

        UTF.Logging.Logger.LogMessage("sample log {0}", 124);

        var expectedMessage = string.Format("sample log 123{0}sample log 124{0}", Environment.NewLine);
        Assert.AreEqual(expectedMessage, logMessageListener1.StandardOutput);
    }
}
