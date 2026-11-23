// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using AwesomeAssertions;

#if DEBUG && !NETFRAMEWORK
using Microsoft.Testing.TestInfrastructure;
#endif

using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Execution;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.TestableImplementations;

using Moq;

using TestFramework.ForTestingMSTest;

namespace MSTestAdapter.PlatformServices.UnitTests.Execution;

public sealed class ExceptionHelperMutationTests : TestContainer
{
    private const string FailedToGetStackTraceMessage = "StackTraceHelper.GetStackTraceInformation: Failed to get stack trace info.";

    private readonly IPlatformServiceProvider _previousProvider = PlatformServiceProvider.Instance;
    private readonly TestablePlatformServiceProvider _provider = new();

    public ExceptionHelperMutationTests() => PlatformServiceProvider.Instance = _provider;

    protected override void Dispose(bool disposing)
    {
        PlatformServiceProvider.Instance = _previousProvider;
        base.Dispose(disposing);
    }

    public void GetStackTraceInformation_WhenBothStackGettersThrowAndErrorTracingIsEnabled_TracesTheFailureOnce()
    {
        _provider.MockTraceLogger.SetupGet(logger => logger.IsErrorEnabled).Returns(true);

        StackTraceInformation? result = CreateExceptionWithUnreadableStackTrace().GetStackTraceInformation();

        result.Should().NotBeNull();
        result!.ErrorStackTrace.Should().Contain("inner trace");
        _provider.MockTraceLogger.Verify(logger => logger.Error(FailedToGetStackTraceMessage), Times.Once);
        _provider.MockTraceLogger.Verify(logger => logger.Error(It.IsAny<string>()), Times.Once);
    }

    public void GetStackTraceInformation_WhenBothStackGettersThrowAndErrorTracingIsDisabled_DoesNotTrace()
    {
        _provider.MockTraceLogger.SetupGet(logger => logger.IsErrorEnabled).Returns(false);

        StackTraceInformation? result = CreateExceptionWithUnreadableStackTrace().GetStackTraceInformation();

        result.Should().NotBeNull();
        result!.ErrorStackTrace.Should().Contain("inner trace");
        _provider.MockTraceLogger.Verify(logger => logger.Error(It.IsAny<string>()), Times.Never);
    }

    public void GetStackTraceInformation_WhenNoStackGetterThrows_DoesNotTrace()
    {
        _provider.MockTraceLogger.SetupGet(logger => logger.IsErrorEnabled).Returns(true);

        StackTraceInformation? result = new FixedStackTraceException("outer trace").GetStackTraceInformation();

        result.Should().NotBeNull();
        result!.ErrorStackTrace.Should().Contain("outer trace");
        _provider.MockTraceLogger.Verify(logger => logger.Error(It.IsAny<string>()), Times.Never);
    }

#if DEBUG && !NETFRAMEWORK
    public void GetStackTraceInformation_WhenExceptionIsNull_ReportsTheViolatedPreconditionBeforeReturningAnEmptyTrace()
    {
        var assertions = new List<string?>();
        using var observer = new DiagnosticAssertionObserver(assertions.Add);

        StackTraceInformation? result = ExceptionHelper.GetStackTraceInformation(null!);

        assertions.Should().Equal("exception should not be null.", "StackTrace message should not be empty");
        result.Should().NotBeNull();
        result!.ErrorStackTrace.Should().BeEmpty();
    }
#endif

    private static ThrowingStackTraceException CreateExceptionWithUnreadableStackTrace()
        => new(
            new ThrowingStackTraceException(new InvalidOperationException("unreadable getter failure")),
            new FixedStackTraceException("inner trace"));

    private sealed class FixedStackTraceException(string? stackTrace, Exception? inner = null) : Exception("failure", inner)
    {
        public override string? StackTrace => stackTrace;
    }

    private sealed class ThrowingStackTraceException(Exception getterFailure, Exception? inner = null) : Exception("failure", inner)
    {
        public override string? StackTrace => throw getterFailure;
    }
}
