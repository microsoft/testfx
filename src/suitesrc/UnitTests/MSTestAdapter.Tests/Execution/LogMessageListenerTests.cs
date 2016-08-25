// --------------------------------------------------------------------------------------------------------------------
// <copyright file="LogMessageListenerTests.cs" company="">
//   
// </copyright>
// --------------------------------------------------------------------------------------------------------------------

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.Execution
{
    using System;

    using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Execution;
    using Microsoft.VisualStudio.TestPlatform.UnitTestFramework;

    using UTF = Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class LogMessageListenerTests
    {
        [TestMethod]
        public void LogMessageListenerCapturesTestFrameworkLogMessages()
        {
            using (var logMessageListener = new LogMessageListener(false))
            {
                UTF.Logger.LogMessage("sample log {0}", 123);

                Assert.AreEqual("sample log 123" + Environment.NewLine, logMessageListener.LoggerOut);
            }
        }

        [TestMethod]
        public void LogMessageListenerCapturesTraceLogsIfDebugTraceIsEnabled()
        {
        }

        [TestMethod]
        public void LogMessageListenerCapturesLogMessagesInAllListeningScopes()
        {
            using (var logMessageListener1 = new LogMessageListener(false))
            {
                using (var logMessageListener2 = new LogMessageListener(false))
                {
                    UTF.Logger.LogMessage("sample log {0}", 123);

                    Assert.AreEqual("sample log 123" + Environment.NewLine, logMessageListener2.LoggerOut);
                }

                UTF.Logger.LogMessage("sample log {0}", 124);

                var expectedMessage = string.Format("sample log 123{0}sample log 124{0}", Environment.NewLine);
                Assert.AreEqual(expectedMessage, logMessageListener1.LoggerOut);
            }
        }
    }
}
