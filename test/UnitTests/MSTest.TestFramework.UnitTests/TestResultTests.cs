// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using TestFramework.ForTestingMSTest;

namespace MSTest.TestFramework.UnitTests;

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
        Verify(testResult.TestFailureException.Message == "Failure1");

        testResult.TestFailureException = new ArgumentException("Failure2");
        var aggregateException = (AggregateException)testResult.TestFailureException;
        Verify(aggregateException.InnerExceptions.Count == 2);
        Verify(aggregateException.InnerExceptions[0].Message == "Failure1");
        Verify(aggregateException.InnerExceptions[1].Message == "Failure2");

        testResult.TestFailureException = new ArgumentException("Failure3");
        aggregateException = (AggregateException)testResult.TestFailureException;
        Verify(aggregateException.InnerExceptions.Count == 3);
        Verify(aggregateException.InnerExceptions[0].Message == "Failure1");
        Verify(aggregateException.InnerExceptions[1].Message == "Failure2");
        Verify(aggregateException.InnerExceptions[2].Message == "Failure3");
    }
}
