// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;

using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Extensions;

using TestFramework.ForTestingMSTest;

using UTF = Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.Extensions;

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

        Verify(ReferenceEquals(innerException, exception));
    }

    public void ExceptionGetInnerExceptionOrDefaultShouldNotThrowForNullException()
    {
        static void Action() => ((Exception)null).GetInnerExceptionOrDefault();

        Action();
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

        Verify(ReferenceEquals(exceptionWithNoInnerException, exception));
    }

    #endregion

    #region TryGetExceptionMessage scenarios

    public void ExceptionTryGetMessageGetsTheExceptionMessage()
    {
        var exception = new Exception("dummyMessage");

        Verify(exception.TryGetMessage() == "dummyMessage");
    }

    public void ExceptionTryGetMessageReturnsEmptyStringIfExceptionMessageIsNull()
    {
        var exception = new DummyException(() => null);

        Verify(exception.TryGetMessage() == string.Empty);
    }

    public void ExceptionTryGetMessageReturnsErrorMessageIfExceptionIsNull()
    {
        var errorMessage = string.Format(Resource.UTF_FailedToGetExceptionMessage, "null");

        var exception = (Exception)null;

        Verify(errorMessage == exception.TryGetMessage());
    }

    public void ExceptionTryGetMessageShouldThrowIfExceptionMessageThrows()
    {
        var errorMessage = string.Format(Resource.UTF_FailedToGetExceptionMessage, "System.NotImplementedException");
        var exception = new DummyException(() => { throw new NotImplementedException(); });

        var ex = VerifyThrows(() => exception.TryGetMessage());
        Verify(ex is NotImplementedException);
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

        Verify(stackTraceInformation.ErrorStackTrace.StartsWith("    at A()"));
        Verify(stackTraceInformation.ErrorFilePath is null);
        Verify(stackTraceInformation.ErrorLineNumber == 0);
    }

    public void TryGetStackTraceInformationShouldThrowIfStackTraceThrows()
    {
        var exception = new DummyExceptionForStackTrace(() => { throw new NotImplementedException(); });

        var ex = VerifyThrows(() => exception.TryGetStackTraceInformation());
        Verify(ex is NotImplementedException);
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

        Verify(outcome == UTF.UnitTestOutcome.Inconclusive);
        Verify(exceptionMessage == "Dummy Message");
    }

    public void IsUnitTestAssertExceptionSetsOutcomeAsFailedIfAssertFailedException()
    {
        var exception = new UTF.AssertFailedException("Dummy Message", new NotImplementedException("notImplementedException"));
        UTF.UnitTestOutcome outcome = UTF.UnitTestOutcome.Unknown;

        exception.TryGetUnitTestAssertException(out outcome, out var exceptionMessage, out var stackTraceInfo);

        Verify(outcome == UTF.UnitTestOutcome.Failed);
        Verify(exceptionMessage == "Dummy Message");
    }
    #endregion
}
