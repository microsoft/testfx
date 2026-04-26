// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#pragma warning disable MSTESTEXP // Experimental API

using AwesomeAssertions;

using TestFramework.ForTestingMSTest;

namespace UnitTestFramework.Tests;

/// <summary>
/// Tests for <see cref="RetryAttribute"/> constructor validation and retry execution logic.
/// </summary>
public class RetryAttributeTests : TestContainer
{
    public void Constructor_WhenMaxRetryAttemptsIsZero_ThrowsArgumentOutOfRangeException()
    {
        Action act = static () => _ = new RetryAttribute(0);
        act.Should().Throw<ArgumentOutOfRangeException>()
            .WithParameterName("maxRetryAttempts");
    }

    public void Constructor_WhenMaxRetryAttemptsIsNegative_ThrowsArgumentOutOfRangeException()
    {
        Action act = static () => _ = new RetryAttribute(-1);
        act.Should().Throw<ArgumentOutOfRangeException>()
            .WithParameterName("maxRetryAttempts");
    }

    public void Constructor_WhenMaxRetryAttemptsIsOne_SetsMaxRetryAttempts()
    {
        var attribute = new RetryAttribute(1);
        attribute.MaxRetryAttempts.Should().Be(1);
    }

    public void Constructor_WhenMaxRetryAttemptsIsPositive_SetsMaxRetryAttempts()
    {
        var attribute = new RetryAttribute(5);
        attribute.MaxRetryAttempts.Should().Be(5);
    }

    public void BackoffType_DefaultsToConstant()
    {
        var attribute = new RetryAttribute(2);
        attribute.BackoffType.Should().Be(DelayBackoffType.Constant);
    }

    public void BackoffType_WhenSetToInvalidValue_ThrowsArgumentOutOfRangeException()
    {
        var attribute = new RetryAttribute(2);
        Action act = () => attribute.BackoffType = (DelayBackoffType)99;
        act.Should().Throw<ArgumentOutOfRangeException>()
            .WithParameterName("value");
    }

    public void BackoffType_WhenSetToExponential_Succeeds()
    {
        RetryAttribute attribute = new(2) { BackoffType = DelayBackoffType.Exponential };
        attribute.BackoffType.Should().Be(DelayBackoffType.Exponential);
    }

    public void MillisecondsDelayBetweenRetries_DefaultsToZero()
    {
        var attribute = new RetryAttribute(2);
        attribute.MillisecondsDelayBetweenRetries.Should().Be(0);
    }

    public async Task ExecuteAsync_WhenTestPassesOnFirstRetry_StopsRetryingEarly()
    {
        var attribute = new RetryAttribute(maxRetryAttempts: 5);
        int callCount = 0;

        var passResult = new TestResult { Outcome = UnitTestOutcome.Passed };
        var failResult = new TestResult { Outcome = UnitTestOutcome.Failed };

        TestResult[] firstRunResults = [failResult];
        var context = new RetryContext(
            () =>
            {
                callCount++;
                return Task.FromResult(new[] { passResult });
            },
            firstRunResults);

        RetryResult result = await attribute.ExecuteAsync(context);

        callCount.Should().Be(1);
        result.TryGetLast().Should().ContainSingle()
            .Which.Outcome.Should().Be(UnitTestOutcome.Passed);
    }

    public async Task ExecuteAsync_WhenAllRetriesFail_ExecutesExactlyMaxRetryAttemptsTimes()
    {
        var attribute = new RetryAttribute(maxRetryAttempts: 3);
        int callCount = 0;

        var failResult = new TestResult { Outcome = UnitTestOutcome.Failed };
        TestResult[] firstRunResults = [failResult];

        var context = new RetryContext(
            () =>
            {
                callCount++;
                return Task.FromResult(new[] { new TestResult { Outcome = UnitTestOutcome.Failed } });
            },
            firstRunResults);

        RetryResult result = await attribute.ExecuteAsync(context);

        callCount.Should().Be(3);
    }

    public async Task ExecuteAsync_WhenAllRetriesFail_ResultContainsLastAttemptOutcome()
    {
        var attribute = new RetryAttribute(maxRetryAttempts: 2);
        int callCount = 0;

        TestResult[] firstRunResults = [new TestResult { Outcome = UnitTestOutcome.Failed }];
        var context = new RetryContext(
            () =>
            {
                callCount++;
                UnitTestOutcome outcome = callCount == 2 ? UnitTestOutcome.Timeout : UnitTestOutcome.Failed;
                return Task.FromResult(new[] { new TestResult { Outcome = outcome } });
            },
            firstRunResults);

        RetryResult result = await attribute.ExecuteAsync(context);

        result.TryGetLast().Should().ContainSingle()
            .Which.Outcome.Should().Be(UnitTestOutcome.Timeout);
    }

    public async Task ExecuteAsync_WhenTestPassesAfterSeveralFailures_StopsAtFirstSuccess()
    {
        var attribute = new RetryAttribute(maxRetryAttempts: 5);
        int callCount = 0;

        TestResult[] firstRunResults = [new TestResult { Outcome = UnitTestOutcome.Failed }];
        var context = new RetryContext(
            () =>
            {
                callCount++;
                UnitTestOutcome outcome = callCount < 3 ? UnitTestOutcome.Failed : UnitTestOutcome.Passed;
                return Task.FromResult(new[] { new TestResult { Outcome = outcome } });
            },
            firstRunResults);

        RetryResult result = await attribute.ExecuteAsync(context);

        callCount.Should().Be(3);
        result.TryGetLast().Should().ContainSingle()
            .Which.Outcome.Should().Be(UnitTestOutcome.Passed);
    }

    public async Task ExecuteAsync_WhenResultIsInconclusive_StopsRetrying()
    {
        var attribute = new RetryAttribute(maxRetryAttempts: 5);
        int callCount = 0;

        TestResult[] firstRunResults = [new TestResult { Outcome = UnitTestOutcome.Failed }];
        var context = new RetryContext(
            () =>
            {
                callCount++;
                return Task.FromResult(new[] { new TestResult { Outcome = UnitTestOutcome.Inconclusive } });
            },
            firstRunResults);

        await attribute.ExecuteAsync(context);

        callCount.Should().Be(1);
    }
}
