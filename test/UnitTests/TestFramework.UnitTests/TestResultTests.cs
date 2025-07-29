// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using TestFramework.ForTestingMSTest;

using FluentAssertions;

namespace Microsoft.VisualStudio.TestPlatform.TestFramework.UnitTests;

public sealed class TestResultTests : TestContainer
{
    public void SettingTestFailureExceptionShouldAggregatePreviouslySetExceptions()
    {
        // In a case like https://github.com/microsoft/testfx/issues/5165, we will set TestFailureException twice.
        // First from initialize, then from cleanup.
        // We want to aggregate them correctly.
        var testResult = new TestResult
        {
            TestFailureException = new InvalidOperationException("Failure1"),
        };

        // We use GetType() == typeof(...) to do a strict type match.
        Verify(testResult.TestFailureException.GetType() == typeof(InvalidOperationException));
        testResult.TestFailureException.Message.Should().Be("Failure1");

        testResult.TestFailureException = new ArgumentException("Failure2");
        var aggregateException = (AggregateException)testResult.TestFailureException;
        aggregateException.InnerExceptions.Count.Should().Be(2);
        aggregateException.InnerExceptions[0].Message.Should().Be("Failure1");
        aggregateException.InnerExceptions[1].Message.Should().Be("Failure2");

        testResult.TestFailureException = new ArgumentException("Failure3");
        aggregateException = (AggregateException)testResult.TestFailureException;
        aggregateException.InnerExceptions.Count.Should().Be(3);
        aggregateException.InnerExceptions[0].Message.Should().Be("Failure1");
        aggregateException.InnerExceptions[1].Message.Should().Be("Failure2");
        aggregateException.InnerExceptions[2].Message.Should().Be("Failure3");
    }
}
