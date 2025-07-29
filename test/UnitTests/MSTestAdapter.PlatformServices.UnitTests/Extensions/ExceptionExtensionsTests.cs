// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using FluentAssertions;

using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Extensions;

using TestFramework.ForTestingMSTest;

using UTF = Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MSTestAdapter.PlatformServices.Tests.Extensions;

public class ExceptionExtensionsTests : TestContainer
{
    public void GetExceptionMessageShouldReturnExceptionMessage()
    {
        Exception ex = new("something bad happened");
        ex.GetExceptionMessage().Should().Be("something bad happened");
    }

    public void GetExceptionMessageShouldReturnInnerExceptionMessageAsWell()
    {
        Exception ex = new("something bad happened", new Exception("inner exception", new Exception("the real exception")));
        string expectedMessage = string.Concat(
            "something bad happened",
            Environment.NewLine,
            "inner exception",
            Environment.NewLine,
            "the real exception");

        Verify(expectedMessage == ex.GetExceptionMessage());
    }

    #region TryGetExceptionMessage scenarios

    public void ExceptionTryGetMessageGetsTheExceptionMessage()
    {
        var exception = new Exception("dummyMessage");

        exception.TryGetMessage().Should().Be("dummyMessage");
    }

    public void ExceptionTryGetMessageReturnsEmptyStringIfExceptionMessageIsNull()
    {
        var exception = new DummyException(() => null!);

        exception.TryGetMessage().Should().Be(string.Empty);
    }

    public void ExceptionTryGetMessageReturnsErrorMessageIfExceptionIsNull()
    {
        string errorMessage = string.Format(CultureInfo.InvariantCulture, Resource.UTF_FailedToGetExceptionMessage, "null");

        var exception = (Exception?)null;

        Verify(errorMessage == exception.TryGetMessage());
    }

    public void ExceptionTryGetMessageShouldThrowIfExceptionMessageThrows()
    {
        var exception = new DummyException(() => throw new NotImplementedException());

        VerifyThrows<NotImplementedException>(() => exception.TryGetMessage());
    }

    #endregion

    #region TryGetStackTraceInformation scenarios

    public void TryGetStackTraceInformationReturnsNullIfExceptionStackTraceIsNullOrEmpty()
    {
        var exception = new DummyExceptionForStackTrace(() => null!);

        Verify(exception.TryGetStackTraceInformation() is null);
    }

    public void TryGetStackTraceInformationReturnsStackTraceForAnException()
    {
        var exception = new DummyExceptionForStackTrace(() => "   at A()\r\n    at B()");

        StackTraceInformation? stackTraceInformation = exception.TryGetStackTraceInformation();

        stackTraceInformation!.ErrorStackTrace.Should().StartsWith("   at A()", StringComparison.Ordinal);
        stackTraceInformation.ErrorFilePath.Should().BeNull();
        stackTraceInformation.ErrorLineNumber.Should().Be(0);
    }

    public void TryGetStackTraceInformationShouldThrowIfStackTraceThrows()
    {
        var exception = new DummyExceptionForStackTrace(() => throw new NotImplementedException());

        VerifyThrows<NotImplementedException>(() => exception.TryGetStackTraceInformation());
    }

#pragma warning disable CA1710 // Identifiers should have correct suffix
    public class DummyExceptionForStackTrace : Exception
#pragma warning restore CA1710 // Identifiers should have correct suffix
    {
        private readonly Func<string> _getStackTrace;

        public DummyExceptionForStackTrace(Func<string> getStackTrace) => _getStackTrace = getStackTrace;

        public override string StackTrace => _getStackTrace();
    }

    internal class DummyException : Exception
    {
        private readonly Func<string> _getMessage;

        public DummyException(Func<string> message) => _getMessage = message;

        public override string Message => _getMessage();
    }

    #endregion

    #region IsUnitTestAssertException scenarios

    public void IsUnitTestAssertExceptionReturnsTrueIfExceptionIsAssertException()
    {
        var exception = new AssertInconclusiveException();
        Verify(exception.TryGetUnitTestAssertException(out _, out _, out _));
    }

    public void IsUnitTestAssertExceptionReturnsFalseIfExceptionIsNotAssertException()
    {
        var exception = new NotImplementedException();
        Verify(!exception.TryGetUnitTestAssertException(out _, out _, out _));
    }

    public void IsUnitTestAssertExceptionSetsOutcomeAsInconclusiveIfAssertInconclusiveException()
    {
        var exception = new AssertInconclusiveException("Dummy Message", new NotImplementedException("notImplementedException"));
        exception.TryGetUnitTestAssertException(out UTF.UnitTestOutcome outcome, out string? exceptionMessage, out _);

        outcome.Should().Be(UTF.UnitTestOutcome.Inconclusive);
        exceptionMessage.Should().Be("Dummy Message");
    }

    public void IsUnitTestAssertExceptionSetsOutcomeAsFailedIfAssertFailedException()
    {
        var exception = new AssertFailedException("Dummy Message", new NotImplementedException("notImplementedException"));
        exception.TryGetUnitTestAssertException(out UTF.UnitTestOutcome outcome, out string? exceptionMessage, out _);

        outcome.Should().Be(UTF.UnitTestOutcome.Failed);
        exceptionMessage.Should().Be("Dummy Message");
    }
    #endregion

    #region GetRealException scenarios
    public void GetRealExceptionGetsTheTopExceptionWhenThereIsJustOne()
    {
        var exception = new InvalidOperationException();
        Exception actual = exception.GetRealException();

        actual.Should().BeOfType<InvalidOperationException>();
    }

    public void GetRealExceptionGetsTheInnerExceptionWhenTheExceptionIsTargetInvocation()
    {
        var exception = new TargetInvocationException(new InvalidOperationException());
        Exception actual = exception.GetRealException();

        actual.Should().BeOfType<InvalidOperationException>();
    }

    public void GetRealExceptionGetsTheTargetInvocationExceptionWhenTargetInvocationIsProvidedWithNullInnerException()
    {
        var exception = new TargetInvocationException(null);
        Exception actual = exception.GetRealException();

        actual.Should().BeOfType<TargetInvocationException>();
    }

    public void GetRealExceptionGetsTheInnerMostRealException()
    {
        var exception = new TargetInvocationException(new TargetInvocationException(new TargetInvocationException(new InvalidOperationException())));
        Exception actual = exception.GetRealException();

        actual.Should().BeOfType<InvalidOperationException>();
    }

    public void GetRealExceptionGetsTheInnerMostTargetInvocationException()
    {
        var exception = new TargetInvocationException(new TargetInvocationException(new TargetInvocationException("inner most", null)));
        Exception actual = exception.GetRealException();

        actual is TargetInvocationException);
        Verify(actual.Message.Should().Be("inner most");
    }

    public void GetRealExceptionGetsTheInnerExceptionWhenTheExceptionIsTypeInitialization()
    {
        var exception = new TypeInitializationException("some type", new InvalidOperationException());
        Exception actual = exception.GetRealException();

        actual.Should().BeOfType<InvalidOperationException>();
    }

    public void GetRealExceptionGetsTheTypeInitializationExceptionWhenTypeInitializationIsProvidedWithNullInnerException()
    {
        var exception = new TypeInitializationException("some type", null);
        Exception actual = exception.GetRealException();

        actual.Should().BeOfType<TypeInitializationException>();
    }

    public void GetRealExceptionGetsTheInnerMostRealExceptionOfTypeInitialization()
    {
        var exception = new TypeInitializationException("some type", new TypeInitializationException("some type", new TypeInitializationException("some type", new InvalidOperationException())));
        Exception actual = exception.GetRealException();

        actual.Should().BeOfType<InvalidOperationException>();
    }

    public void GetRealExceptionGetsTheInnerMostTypeInitializationException()
    {
        var exception = new TypeInitializationException("some type", new TypeInitializationException("some type", new TypeInitializationException("inner most", null)));
        Exception actual = exception.GetRealException();

        actual is TypeInitializationException);
        Verify(actual.Message.Should().Be("The type initializer for 'inner most' threw an exception.");
    }
    #endregion
}
