// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using AwesomeAssertions;

using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Execution;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Resources;

using TestFramework.ForTestingMSTest;

namespace MSTestAdapter.PlatformServices.UnitTests.Execution;

public class ExceptionHelperTests : TestContainer
{
    public void GetFormattedExceptionMessage_PreservesInnerExceptionOrder()
    {
        var exception = new InvalidOperationException("outer failure", new ArgumentException("inner failure"));

        exception.GetFormattedExceptionMessage().Should().Be(
            "System.InvalidOperationException: outer failure ---> System.ArgumentException: inner failure");
    }

    public void GetFormattedExceptionMessage_WhenMessageGetterThrows_StillIncludesInnerException()
    {
        var exception = new ThrowingMessageException(new ArgumentException("inner failure"));
        string fallback = string.Format(CultureInfo.CurrentCulture, Resource.UTF_FailedToGetExceptionMessage, typeof(ThrowingMessageException));

        exception.GetFormattedExceptionMessage().Should().Be(
            $"{typeof(ThrowingMessageException)}: {fallback} ---> System.ArgumentException: inner failure");
    }

    public void GetStackTraceInformation_OrdersInnermostTraceFirst_AndIgnoresMissingTraces()
    {
        var exception = new FixedStackTraceException(
            "outer trace",
            new FixedStackTraceException(null, new FixedStackTraceException("inner trace")));
        string expected = $"""
            inner trace
            {Resource.UTA_EndOfInnerExceptionTrace}
            outer trace

            """;

        StackTraceInformation? result = exception.GetStackTraceInformation();

        result.Should().NotBeNull();
        result!.ErrorStackTrace.Replace("\r\n", "\n")
            .Should().Be(expected.Replace("\r\n", "\n"));
    }

    public void GetStackTraceInformation_WhenStackGetterThrows_UsesGetterFailureTrace()
    {
        var exception = new ThrowingStackTraceException(new FixedStackTraceException("getter failure trace"));
        string expected = """
            getter failure trace

            """;

        StackTraceInformation? result = exception.GetStackTraceInformation();

        result.Should().NotBeNull();
        result!.ErrorStackTrace.Replace("\r\n", "\n").Should().Be(expected.Replace("\r\n", "\n"));
    }

    public void GetStackTraceInformation_WhenGetterFailureHasNoTrace_PreservesInnerTrace()
    {
        var exception = new ThrowingStackTraceException(
            new FixedStackTraceException(null),
            new FixedStackTraceException("inner trace"));
        string expected = """
            inner trace

            """;

        StackTraceInformation? result = exception.GetStackTraceInformation();

        result.Should().NotBeNull();
        result!.ErrorStackTrace.Replace("\r\n", "\n").Should().Be(expected.Replace("\r\n", "\n"));
    }

    public void GetStackTraceInformation_WhenBothStackGettersThrow_PreservesInnerTrace()
    {
        var exception = new ThrowingStackTraceException(
            new ThrowingStackTraceException(new InvalidOperationException("unreadable getter failure")),
            new FixedStackTraceException("inner trace"));
        string expected = """
            inner trace

            """;

        StackTraceInformation? result = exception.GetStackTraceInformation();

        result.Should().NotBeNull();
        result!.ErrorStackTrace.Replace("\r\n", "\n").Should().Be(expected.Replace("\r\n", "\n"));
    }

    private sealed class ThrowingMessageException(Exception innerException) : Exception("unreadable", innerException)
    {
        public override string Message => throw new InvalidOperationException("Message getter failed.");
    }

    private sealed class FixedStackTraceException(string? stackTrace, Exception? innerException = null) : Exception("failure", innerException)
    {
        public override string? StackTrace => stackTrace;
    }

    private sealed class ThrowingStackTraceException(Exception getterFailure, Exception? innerException = null) : Exception("failure", innerException)
    {
        public override string? StackTrace => throw getterFailure;
    }
}
