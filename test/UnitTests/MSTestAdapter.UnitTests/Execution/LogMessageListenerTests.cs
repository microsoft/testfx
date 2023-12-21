// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Execution;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Interface;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.TestableImplementations;

using Moq;

using TestFramework.ForTestingMSTest;

using UTF = Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.Execution;

public class LogMessageListenerTests : TestContainer
{
    private readonly TestablePlatformServiceProvider _testablePlatformServiceProvider;

    public LogMessageListenerTests()
    {
        _testablePlatformServiceProvider = new TestablePlatformServiceProvider();
        PlatformServiceProvider.Instance = _testablePlatformServiceProvider;
    }

    protected override void Dispose(bool disposing)
    {
        if (!IsDisposed)
        {
            base.Dispose(disposing);
            PlatformServiceProvider.Instance = null;
        }
    }

    public void LogMessageListenerShouldCaptureTestFrameworkLogMessages()
    {
        using var logMessageListener = new LogMessageListener(false);
        UTF.Logging.Logger.LogMessage("sample log {0}", 123);

        Verify("sample log 123" + Environment.NewLine == logMessageListener.StandardOutput);
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
}
