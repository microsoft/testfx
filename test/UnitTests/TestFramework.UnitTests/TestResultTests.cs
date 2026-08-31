// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using AwesomeAssertions;

using TestFramework.ForTestingMSTest;

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
        testResult.TestFailureException.Should().BeOfType<InvalidOperationException>()
            .Which.Message.Should().Be("Failure1");

        testResult.TestFailureException = new ArgumentException("Failure2");
        var aggregateException = (AggregateException)testResult.TestFailureException;
        aggregateException.InnerExceptions.Should().HaveCount(2);
        aggregateException.InnerExceptions[0].Message.Should().Be("Failure1");
        aggregateException.InnerExceptions[1].Message.Should().Be("Failure2");

        testResult.TestFailureException = new ArgumentException("Failure3");
        aggregateException = (AggregateException)testResult.TestFailureException;
        aggregateException.InnerExceptions.Should().HaveCount(3);
        aggregateException.InnerExceptions[0].Message.Should().Be("Failure1");
        aggregateException.InnerExceptions[1].Message.Should().Be("Failure2");
        aggregateException.InnerExceptions[2].Message.Should().Be("Failure3");
    }

    public void SettingTestFailureExceptionShouldCaptureAssertionTextsFromDirectAssertFailedException()
    {
        AssertFailedException exception = CreateAssertFailedException("5", "2");

        var testResult = new TestResult { TestFailureException = exception };

        testResult.ExceptionExpectedText.Should().Be("5");
        testResult.ExceptionActualText.Should().Be("2");
    }

    public void SettingTestFailureExceptionShouldCaptureAssertionTextsFromWrappedAssertFailedException()
    {
        // The adapter always wraps the original assertion exception (in TestFailedException), so the
        // assertion values must be found by walking the InnerException chain rather than only looking at
        // the outermost exception.
        var exception = new InvalidOperationException("outer", new InvalidOperationException("middle", CreateAssertFailedException("5", "2")));

        var testResult = new TestResult { TestFailureException = exception };

        testResult.ExceptionExpectedText.Should().Be("5");
        testResult.ExceptionActualText.Should().Be("2");
    }

    public void SettingTestFailureExceptionShouldCaptureAssertionTextsFromAggregateException()
    {
        var exception = new AggregateException(new InvalidOperationException("unrelated"), CreateAssertFailedException("5", "2"));

        var testResult = new TestResult { TestFailureException = exception };

        testResult.ExceptionExpectedText.Should().Be("5");
        testResult.ExceptionActualText.Should().Be("2");
    }

    public void SettingTestFailureExceptionShouldNotCaptureAssertionTextsWhenScopeReportsMultipleFailures()
    {
        // This is the exact shape Assert.Scope throws for two or more collected failures: an outer
        // AssertFailedException (carrying no values of its own) wrapping an AggregateException of the
        // individual assertion failures. Reporting only the first pair next to a message describing N
        // failures would misrepresent the result.
        var exception = new AssertFailedException(
            "2 assertions failed.",
            new AggregateException(CreateAssertFailedException("5", "2"), CreateAssertFailedException("7", "3")));

        var testResult = new TestResult { TestFailureException = exception };

        testResult.ExceptionExpectedText.Should().BeNull();
        testResult.ExceptionActualText.Should().BeNull();
    }

    public void SettingTestFailureExceptionShouldCaptureAssertionTextsWhenASecondAssertionCarriesNoValues()
    {
        // Only one comparison exists, so it is unambiguous and is reported. A failure that carries no
        // comparison (Assert.Fail here) adds nothing that could be confused with it.
        var exception = new AssertFailedException(
            "2 assertions failed.",
            new AggregateException(CreateAssertFailedException("5", "2"), CreateAssertFailedException(null, null)));

        var testResult = new TestResult { TestFailureException = exception };

        testResult.ExceptionExpectedText.Should().Be("5");
        testResult.ExceptionActualText.Should().Be("2");
    }

    public void SettingTestFailureExceptionShouldCaptureAssertionTextsWhenScopeReportsASingleFailure()
    {
        // Assert.Scope re-throws the original exception as-is when it collected exactly one failure.
        var testResult = new TestResult { TestFailureException = CreateAssertFailedException("5", "2") };

        testResult.ExceptionExpectedText.Should().Be("5");
        testResult.ExceptionActualText.Should().Be("2");
    }

    public void SettingTestFailureExceptionTwiceWithTwoAssertionsShouldDropAmbiguousAssertionTexts()
    {
        var testResult = new TestResult { TestFailureException = CreateAssertFailedException("first-expected", "first-actual") };

        // A second assertion failure (e.g. from TestCleanup) makes any single pair ambiguous.
        testResult.TestFailureException = CreateAssertFailedException("second-expected", "second-actual");

        testResult.ExceptionExpectedText.Should().BeNull();
        testResult.ExceptionActualText.Should().BeNull();
    }

    public void SettingTestFailureExceptionAfterAMultiFailureScopeShouldNotCaptureAssertionTexts()
    {
        // The scope already contributed two assertion failures, so the later single assertion from cleanup
        // must not get to claim the diff for the whole result.
        var testResult = new TestResult
        {
            TestFailureException = new AssertFailedException(
                "2 assertions failed.",
                new AggregateException(CreateAssertFailedException("1", "2"), CreateAssertFailedException("3", "4"))),
        };

        testResult.TestFailureException = CreateAssertFailedException("5", "6");

        testResult.ExceptionExpectedText.Should().BeNull();
        testResult.ExceptionActualText.Should().BeNull();
    }

    public void SettingTestFailureExceptionAfterAValueLessAssertionShouldCaptureAssertionTexts()
    {
        // Assert.Fail produces an assertion failure with no comparison to report. It must not suppress a later
        // one, otherwise a cleanup Assert.Fail would hide the body's diff while a cleanup throw would not.
        var testResult = new TestResult { TestFailureException = CreateAssertFailedException(null, null) };

        testResult.TestFailureException = CreateAssertFailedException("5", "6");

        testResult.ExceptionExpectedText.Should().Be("5");
        testResult.ExceptionActualText.Should().Be("6");
    }

    public void SettingTestFailureExceptionTwiceShouldKeepAssertionTextsWhenSecondFailureIsNotAnAssertion()
    {
        var testResult = new TestResult { TestFailureException = CreateAssertFailedException("5", "2") };

        testResult.TestFailureException = new InvalidOperationException("cleanup blew up");

        testResult.ExceptionExpectedText.Should().Be("5");
        testResult.ExceptionActualText.Should().Be("2");
    }

    public void SettingTestFailureExceptionShouldNotCaptureAssertionTextsForNonAssertionFailure()
    {
        var testResult = new TestResult { TestFailureException = new InvalidOperationException("boom") };

        testResult.ExceptionExpectedText.Should().BeNull();
        testResult.ExceptionActualText.Should().BeNull();
    }

    public void SettingTestFailureExceptionShouldStopWalkingBeyondBoundedChainDepth()
    {
        // The walk is bounded so a pathological exception chain cannot stall the test host. An assertion
        // buried deeper than the bound is intentionally not reported.
        Exception exception = CreateAssertFailedException("5", "2");
        for (int i = 0; i < 15; i++)
        {
            exception = new InvalidOperationException("wrapper", exception);
        }

        var testResult = new TestResult { TestFailureException = exception };

        testResult.ExceptionExpectedText.Should().BeNull();
        testResult.ExceptionActualText.Should().BeNull();
    }

    private static AssertFailedException CreateAssertFailedException(string? expectedText, string? actualText)
        => new("Assert.AreEqual failed.")
        {
            ExpectedText = expectedText,
            ActualText = actualText,
        };
}
