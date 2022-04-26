// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.Extensions
{
    extern alias FrameworkV1;
    extern alias FrameworkV2;

    using System;
    using System.Text;
    using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter;
    using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Extensions;
    using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.ObjectModel;
    using Assert = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.Assert;
    using ExpectedException = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.ExpectedExceptionAttribute;
    using StringAssert = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.StringAssert;
    using TestClass = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.TestClassAttribute;
    using TestMethod = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.TestMethodAttribute;
    using UTF = FrameworkV2::Microsoft.VisualStudio.TestTools.UnitTesting;

    /// <summary>
    /// Tests for <see cref="ExceptionExtensions"/> class.
    /// </summary>
    [TestClass]
    public class ExceptionExtensionsTests
    {
        #region GetInnerExceptionOrDefault scenarios

        [TestMethod]
        public void ExceptionGetInnerExceptionOrDefaultReturnsInnerExceptionIfAvailable()
        {
            var innerException = new NotImplementedException("notImplementedException");
            var exceptionWithInnerException = new InvalidOperationException("invalidOperationException", innerException);

            var exception = exceptionWithInnerException.GetInnerExceptionOrDefault();

            Assert.AreSame(innerException, exception);
        }

        [TestMethod]
        public void ExceptionGetInnerExceptionOrDefaultShouldNotThrowForNullException()
        {
            Action action = () => ((Exception)null).GetInnerExceptionOrDefault();

            action();
        }

        [TestMethod]
        public void ExceptionGetInnerExceptionOrDefaultShouldReturnNullForNullException()
        {
            var exception = ((Exception)null).GetInnerExceptionOrDefault();

            Assert.IsNull(exception);
        }

        [TestMethod]
        public void ExceptionGetInnerExceptionOrDefaultShouldReturnExceptionIfInnerExceptionIsNull()
        {
            var exceptionWithNoInnerException = new InvalidOperationException("invalidOperationException", innerException: null);

            var exception = exceptionWithNoInnerException.GetInnerExceptionOrDefault();

            Assert.AreSame(exceptionWithNoInnerException, exception);
        }

        #endregion

        #region TryGetExceptionMessage scenarios

        [TestMethod]
        public void ExceptionTryGetMessageGetsTheExceptionMessage()
        {
            var exception = new Exception("dummyMessage");

            Assert.AreEqual<string>("dummyMessage", exception.TryGetMessage());
        }

        [TestMethod]
        public void ExceptionTryGetMessageReturnsEmptyStringIfExceptionMessageIsNull()
        {
            var exception = new DummyException(() => null);

            Assert.AreEqual(string.Empty, exception.TryGetMessage());
        }

        [TestMethod]
        public void ExceptionTryGetMessageReturnsErrorMessageIfExceptionIsNull()
        {
            var errorMessage = string.Format(Resource.UTF_FailedToGetExceptionMessage, "null");

            var exception = (Exception)null;

            Assert.AreEqual(errorMessage, exception.TryGetMessage());
        }

        [TestMethod]
        [ExpectedException(typeof(NotImplementedException))]
        public void ExceptionTryGetMessageShouldThrowIfExceptionMessageThrows()
        {
            var errorMessage = string.Format(Resource.UTF_FailedToGetExceptionMessage, "System.NotImplementedException");
            var exception = new DummyException(() => { throw new NotImplementedException(); });

            exception.TryGetMessage();
        }

        #endregion

        #region TryGetStackTraceInformation scenarios

        [TestMethod]
        public void TryGetStackTraceInformationReturnsNullIfExceptionIsNull()
        {
            var exception = (Exception)null;

            Assert.IsNull(exception.TryGetStackTraceInformation());
        }

        [TestMethod]
        public void TryGetStackTraceInformationReturnsNullIfExceptionStackTraceIsNullOrEmpty()
        {
            var exception = new DummyExceptionForStackTrace(() => null);

            Assert.IsNull(exception.TryGetStackTraceInformation());
        }

        [TestMethod]
        public void TryGetStackTraceInformationReturnsStackTraceForAnException()
        {
            var exception = new DummyExceptionForStackTrace(() => "    at A()\r\n    at B()");

            var stackTraceInformation = exception.TryGetStackTraceInformation();

            StringAssert.StartsWith(stackTraceInformation.ErrorStackTrace, "    at A()");
            Assert.IsNull(stackTraceInformation.ErrorFilePath);
            Assert.AreEqual(0, stackTraceInformation.ErrorLineNumber);
        }

        [TestMethod]
        [ExpectedException(typeof(NotImplementedException))]
        public void TryGetStackTraceInformationShouldThrowIfStackTraceThrows()
        {
            var exception = new DummyExceptionForStackTrace(() => { throw new NotImplementedException(); });

            exception.TryGetStackTraceInformation();
        }

        public class DummyExceptionForStackTrace : Exception
        {
            private readonly Func<string> getStackTrace;

            public DummyExceptionForStackTrace(Func<string> getStackTrace)
            {
                this.getStackTrace = getStackTrace;
            }

            public override string StackTrace => this.getStackTrace();
        }

        internal class DummyException : Exception
        {
            private readonly Func<string> getMessage;

            public DummyException(Func<string> message)
            {
                this.getMessage = message;
            }

            public override string Message => this.getMessage();
        }

        #endregion

        #region IsUnitTestAssertException scenarios

        [TestMethod]
        public void IsUnitTestAssertExceptionReturnsTrueIfExceptionIsAssertException()
        {
            var exception = new UTF.AssertInconclusiveException();
            UTF.UnitTestOutcome outcome = UTF.UnitTestOutcome.Unknown;

            Assert.IsTrue(exception.TryGetUnitTestAssertException(out outcome, out var exceptionMessage, out var stackTraceInfo));
        }

        [TestMethod]
        public void IsUnitTestAssertExceptionReturnsFalseIfExceptionIsNotAssertException()
        {
            var exception = new NotImplementedException();
            UTF.UnitTestOutcome outcome = UTF.UnitTestOutcome.Unknown;

            Assert.IsFalse(exception.TryGetUnitTestAssertException(out outcome, out var exceptionMessage, out var stackTraceInfo));
        }

        [TestMethod]
        public void IsUnitTestAssertExceptionSetsOutcomeAsInconclusiveIfAssertInconclusiveException()
        {
            var exception = new UTF.AssertInconclusiveException("Dummy Message", new NotImplementedException("notImplementedException"));
            UTF.UnitTestOutcome outcome = UTF.UnitTestOutcome.Unknown;

            exception.TryGetUnitTestAssertException(out outcome, out var exceptionMessage, out var stackTraceInfo);

            Assert.AreEqual(UTF.UnitTestOutcome.Inconclusive, outcome);
            Assert.AreEqual("Dummy Message", exceptionMessage);
        }

        [TestMethod]
        public void IsUnitTestAssertExceptionSetsOutcomeAsFailedIfAssertFailedException()
        {
            var exception = new UTF.AssertFailedException("Dummy Message", new NotImplementedException("notImplementedException"));
            UTF.UnitTestOutcome outcome = UTF.UnitTestOutcome.Unknown;

            exception.TryGetUnitTestAssertException(out outcome, out var exceptionMessage, out var stackTraceInfo);

            Assert.AreEqual(UTF.UnitTestOutcome.Failed, outcome);
            Assert.AreEqual("Dummy Message", exceptionMessage);
        }
        #endregion

        #region TryGetTestFailureExceptionMessageAndStackTrace scenarios

        [TestMethod]
        public void TryGetTestFailureExceptionMessageAndStackTraceFillsInMessageAndStackTrace()
        {
            StringBuilder message = new StringBuilder();
            StringBuilder stackTrace = new StringBuilder();
            var testFailureException = new TestFailedException(UnitTestOutcome.NotFound, "DummyMessage", new StackTraceInformation("DummyStack"));

            testFailureException.TryGetTestFailureExceptionMessageAndStackTrace(message, stackTrace);

            StringAssert.StartsWith(message.ToString(), "DummyMessage");
            StringAssert.StartsWith(stackTrace.ToString(), "DummyStack");
        }

        #endregion

    }
}
