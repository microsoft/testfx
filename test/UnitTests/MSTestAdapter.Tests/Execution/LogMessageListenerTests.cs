//   Copyright (c) Microsoft Corporation. All rights reserved.

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.Execution
{
    extern alias FrameworkV1;
    extern alias FrameworkV2;

    using Assert = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.Assert;
    using TestClass = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.TestClassAttribute;
    using TestMethod = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.TestMethodAttribute;
    using TestInitialize = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.TestInitializeAttribute;
    using TestCleanup = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.TestCleanupAttribute;

    using System;
    using System.IO;
    using System.Text;

    using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter;
    using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Execution;
    using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.TestableImplementations;
    using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Interface;

    using Moq;

    using UTF = FrameworkV2::Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class LogMessageListenerTests
    {
        private TestablePlatformServiceProvider testablePlatformServiceProvider;

        [TestInitialize]
        public void TestInit()
        {
            this.testablePlatformServiceProvider = new TestablePlatformServiceProvider();
            PlatformServiceProvider.Instance = this.testablePlatformServiceProvider;
        }

        [TestCleanup]
        public void Testcleanup()
        {
            PlatformServiceProvider.Instance = null;
        }

        [TestMethod]
        public void LogMessageListenerShouldCaptureTestFrameworkLogMessages()
        {
            using (var logMessageListener = new LogMessageListener(false))
            {
                UTF.Logger.LogMessage("sample log {0}", 123);

                Assert.AreEqual("sample log 123" + Environment.NewLine, logMessageListener.StandardOutput);
            }
        }

        [TestMethod]
        public void LogMessageListenerShouldCaptureErrorMessages()
        {
            using (var logMessageListener = new LogMessageListener(false))
            {
                UTF.Logger.LogMessage("Error Message");

                Assert.AreEqual("Error Message" + Environment.NewLine, logMessageListener.StandardError);
            }
        }

        [TestMethod]
        public void NoTraceListenerOperationShouldBePerformedIfDebugTraceIsNotEnabled()
        {
            var logMessageListener = new LogMessageListener(false);
            this.testablePlatformServiceProvider.MockTraceListenerManager.Verify(mtlm => mtlm.Add(It.IsAny<ITraceListener>()), Times.Never);
        }

        [TestMethod]
        public void AddTraceListenerOperationShouldBePerformedIfDebugTraceIsEnabled()
        {
            var logMessageListener = new LogMessageListener(true);
            this.testablePlatformServiceProvider.MockTraceListenerManager.Verify(mtlm => mtlm.Add(this.testablePlatformServiceProvider.MockTraceListener.Object), Times.Once);
        }

        [TestMethod]
        public void DebugTraceShouldReturnTraceOutput()
        {
            var logMessageListener = new LogMessageListener(true);
            StringWriter writer = new StringWriter(new StringBuilder("DummyTrace"));
            this.testablePlatformServiceProvider.MockTraceListener.Setup(tl => tl.GetWriter()).Returns(writer);
            Assert.AreEqual(logMessageListener.DebugTrace, "DummyTrace");
        }

        #region Dispose Tests
        [TestMethod]
        public void DisposeShouldNotRemoveTraceListenerIfDebugTracesIsNotEnabled()
        {
            var logMessageListener = new LogMessageListener(false);
            logMessageListener.Dispose();
            this.testablePlatformServiceProvider.MockTraceListenerManager.Verify(mtlm => mtlm.Remove(It.IsAny<ITraceListener>()), Times.Never);        
        }

        [TestMethod]
        public void DisposeShouldRemoveTraceListenerIfDebugTracesIsEnabled()
        {
            using (var logMessageListener = new LogMessageListener(true))
            {
                logMessageListener.Dispose();
            }
            // Once when Dispose() is called and second time when destructor is called
            this.testablePlatformServiceProvider.MockTraceListenerManager.Verify(mtlm => mtlm.Remove(It.IsAny<ITraceListener>()), Times.Exactly(2));
        }

        [TestMethod]
        public void DisposeShouldCatcheExceptionIfTraceListenerManagerThrows()
        {
            var logMessageListener = new LogMessageListener(true);
            this.testablePlatformServiceProvider.MockTraceListenerManager.Setup(mtlm => mtlm.Add(It.IsAny<ITraceListener>())).Throws(new Exception("DummyException"));
            logMessageListener.Dispose();
        }

        [TestMethod]
        public void DisposeShouldCloseTraceListener()
        {
            var logMessageListener = new LogMessageListener(true);
            logMessageListener.Dispose();
            this.testablePlatformServiceProvider.MockTraceListenerManager.Verify(mtlm => mtlm.Close(this.testablePlatformServiceProvider.MockTraceListener.Object), Times.Once);
        }

        [TestMethod]
        public void DisposeShouldDisposeTraceListener()
        {
            var logMessageListener = new LogMessageListener(true);
            logMessageListener.Dispose();
            this.testablePlatformServiceProvider.MockTraceListenerManager.Verify(mtlm => mtlm.Dispose(this.testablePlatformServiceProvider.MockTraceListener.Object), Times.Once);
        }

        #endregion

        [TestMethod]
        public void LogMessageListenerShouldCaptureLogMessagesInAllListeningScopes()
        {
            using (var logMessageListener1 = new LogMessageListener(false))
            {
                using (var logMessageListener2 = new LogMessageListener(false))
                {
                    UTF.Logger.LogMessage("sample log {0}", 123);

                    Assert.AreEqual("sample log 123" + Environment.NewLine, logMessageListener2.StandardOutput);
                }

                UTF.Logger.LogMessage("sample log {0}", 124);

                var expectedMessage = string.Format("sample log 123{0}sample log 124{0}", Environment.NewLine);
                Assert.AreEqual(expectedMessage, logMessageListener1.StandardOutput);
            }
        }
    }
}
