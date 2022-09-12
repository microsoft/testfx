// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.Extensions;

using System;
using System.Text;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Extensions;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.ObjectModel;

using TestFramework.ForTestingMSTest;

using UTF = Microsoft.VisualStudio.TestTools.UnitTesting;

/// <summary>
/// Tests for <see cref="ExceptionExtensions"/> class.
/// </summary>
public class ExceptionExtensionsTests : TestContainer
{
    #region GetInnerExceptionOrDefault scenarios

    public void ExceptionGetInnerExceptionOrDefaultReturnsInnerExceptionIfAvailable()
    {
        var innerException = new NotImplementedException("notImplementedException");
        var exceptionWithInnerException = new InvalidOperationException("invalidOperationException", innerException);

        var exception = exceptionWithInnerException.GetInnerExceptionOrDefault();

        Assert.AreSame(innerException, exception);
    }

    public void ExceptionGetInnerExceptionOrDefaultShouldNotThrowForNullException()
    {
        static void action() => ((Exception)null).GetInnerExceptionOrDefault();

        action();
    }

    public void ExceptionGetInnerExceptionOrDefaultShouldReturnNullForNullException()
    {
        var exception = ((Exception)null).GetInnerExceptionOrDefault();

        Verify(exception is null);
    }

    public void ExceptionGetInnerExceptionOrDefaultShouldReturnExceptionIfInnerExceptionIsNull()
    {
        var exceptionWithNoInnerException = new InvalidOperationException("invalidOperationException", innerException: null);

        var exception = exceptionWithNoInnerException.GetInnerExceptionOrDefault();

        Assert.AreSame(exceptionWithNoInnerException, exception);
    }

    #endregion

    #region TryGetExceptionMessage scenarios

    public void ExceptionTryGetMessageGetsTheExceptionMessage()
    {
        var exception = new Exception("dummyMessage");

        Assert.AreEqual<string>("dummyMessage", exception.TryGetMessage());
    }

    public void ExceptionTryGetMessageReturnsEmptyStringIfExceptionMessageIsNull()
    {
        var exception = new DummyException(() => null);

        Assert.AreEqual(string.Empty, exception.TryGetMessage());
    }

    public void ExceptionTryGetMessageReturnsErrorMessageIfExceptionIsNull()
    {
        var errorMessage = string.Format(Resource.UTF_FailedToGetExceptionMessage, "null");

        var exception = (Exception)null;

        Assert.AreEqual(errorMessage, exception.TryGetMessage());
    }

    [ExpectedException(typeof(NotImplementedException))]
    public void ExceptionTryGetMessageShouldThrowIfExceptionMessageThrows()
    {
        var errorMessage = string.Format(Resource.UTF_FailedToGetExceptionMessage, "System.NotImplementedException");
        var exception = new DummyException(() => { throw new NotImplementedException(); });

        exception.TryGetMessage();
    }

    #endregion

    #region TryGetStackTraceInformation scenarios

    public void TryGetStackTraceInformationReturnsNullIfExceptionIsNull()
    {
        var exception = (Exception)null;

        Verify(exception.TryGetStackTraceInformation() is null);
    }

    public void TryGetStackTraceInformationReturnsNullIfExceptionStackTraceIsNullOrEmpty()
    {
        var exception = new DummyExceptionForStackTrace(() => null);

        Verify(exception.TryGetStackTraceInformation() is null);
    }

    public void TryGetStackTraceInformationReturnsStackTraceForAnException()
    {
        var exception = new DummyExceptionForStackTrace(() => "    at A()\r\n    at B()");

        var stackTraceInformation = exception.TryGetStackTraceInformation();

        StringAssert.StartsWith(stackTraceInformation.ErrorStackTrace, "    at A()");
        Verify(stackTraceInformation.ErrorFilePath is null);
        Assert.AreEqual(0, stackTraceInformation.ErrorLineNumber);
    }

    [ExpectedException(typeof(NotImplementedException))]
    public void TryGetStackTraceInformationShouldThrowIfStackTraceThrows()
    {
        var exception = new DummyExceptionForStackTrace(() => { throw new NotImplementedException(); });

        exception.TryGetStackTraceInformation();
    }

    public class DummyExceptionForStackTrace : Exception
    {
        private readonly Func<string> _getStackTrace;

        public DummyExceptionForStackTrace(Func<string> getStackTrace)
        {
            _getStackTrace = getStackTrace;
        }

        public override string StackTrace => _getStackTrace();
    }

    internal class DummyException : Exception
    {
        private readonly Func<string> _getMessage;

        public DummyException(Func<string> message)
        {
            _getMessage = message;
        }

        public override string Message => _getMessage();
    }

    #endregion

    #region IsUnitTestAssertException scenarios

    public void IsUnitTestAssertExceptionReturnsTrueIfExceptionIsAssertException()
    {
        var exception = new UTF.AssertInconclusiveException();
        UTF.UnitTestOutcome outcome = UTF.UnitTestOutcome.Unknown;

        Verify(exception.TryGetUnitTestAssertException(out outcome, out var exceptionMessage, out var stackTraceInfo));
    }

    public void IsUnitTestAssertExceptionReturnsFalseIfExceptionIsNotAssertException()
    {
        var exception = new NotImplementedException();
        UTF.UnitTestOutcome outcome = UTF.UnitTestOutcome.Unknown;

        Verify(!exception.TryGetUnitTestAssertException(out outcome, out var exceptionMessage, out var stackTraceInfo));
    }

    public void IsUnitTestAssertExceptionSetsOutcomeAsInconclusiveIfAssertInconclusiveException()
    {
        var exception = new UTF.AssertInconclusiveException("Dummy Message", new NotImplementedException("notImplementedException"));
        UTF.UnitTestOutcome outcome = UTF.UnitTestOutcome.Unknown;

        exception.TryGetUnitTestAssertException(out outcome, out var exceptionMessage, out var stackTraceInfo);

        Assert.AreEqual(UTF.UnitTestOutcome.Inconclusive, outcome);
        Assert.AreEqual("Dummy Message", exceptionMessage);
    }

    public void IsUnitTestAssertExceptionSetsOutcomeAsFailedIfAssertFailedException()
    {
        var exception = new UTF.AssertFailedException("Dummy Message", new NotImplementedException("notImplementedException"));
        UTF.UnitTestOutcome outcome = UTF.UnitTestOutcome.Unknown;

        exception.TryGetUnitTestAssertException(out outcome, out var exceptionMessage, out var stackTraceInfo);

        Assert.AreEqual(UTF.UnitTestOutcome.Failed, outcome);
        Assert.AreEqual("Dummy Message", exceptionMessage);
    }
    #endregion
}
