// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using AwesomeAssertions;

using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Execution;

using TestFramework.ForTestingMSTest;

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.Execution;

public sealed class ExceptionHelperTests : TestContainer
{
    public void GetFormattedExceptionMessageShouldHandleAggregateExceptionWithMultipleInnerExceptions()
    {
        var exception1 = new InvalidOperationException("First exception");
        var exception2 = new ArgumentException("Second exception");
        var aggregateException = new AggregateException(exception1, exception2);

        string result = aggregateException.GetFormattedExceptionMessage();

        result.Should().Contain("System.InvalidOperationException: First exception");
        result.Should().Contain("System.ArgumentException: Second exception");
    }

    public void GetFormattedExceptionMessageShouldHandleNestedAggregateExceptions()
    {
        var innerException = new InvalidOperationException("Inner exception");
        var middleException = new AggregateException(innerException);
        var outerException = new ArgumentException("Outer exception");
        var aggregateException = new AggregateException(middleException, outerException);

        string result = aggregateException.GetFormattedExceptionMessage();

        result.Should().Contain("System.InvalidOperationException: Inner exception");
        result.Should().Contain("System.ArgumentException: Outer exception");
    }

    public void GetFormattedExceptionMessageShouldHandleRegularExceptionWithInnerException()
    {
        var innerException = new InvalidOperationException("Inner exception");
        var outerException = new ArgumentException("Outer exception", innerException);

        string result = outerException.GetFormattedExceptionMessage();

        result.Should().Contain("System.ArgumentException: Outer exception");
        result.Should().Contain("System.InvalidOperationException: Inner exception");
    }

    public void GetStackTraceInformationShouldHandleAggregateExceptionWithMultipleInnerExceptions()
    {
        Exception exception1;
        Exception exception2;

        try
        {
            throw new InvalidOperationException("First exception");
        }
        catch (Exception ex)
        {
            exception1 = ex;
        }

        try
        {
            throw new ArgumentException("Second exception");
        }
        catch (Exception ex)
        {
            exception2 = ex;
        }

        var aggregateException = new AggregateException(exception1, exception2);

        var result = aggregateException.GetStackTraceInformation();

        result.Should().NotBeNull();
        result!.ErrorStackTrace.Should().Contain("InvalidOperationException");
        result.ErrorStackTrace.Should().Contain("ArgumentException");
    }
}
