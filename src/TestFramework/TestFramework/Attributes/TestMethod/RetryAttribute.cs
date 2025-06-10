// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestTools.UnitTesting;

/// <summary>
/// This attribute is used to set a retry count on a test method in case of failure.
/// </summary>
[AttributeUsage(AttributeTargets.Method, Inherited = false, AllowMultiple = false)]
public sealed class RetryAttribute : RetryBaseAttribute
{
    /// <summary>
    /// Initializes a new instance of the <see cref="RetryAttribute"/> class with the given number of max retries.
    /// </summary>
    /// <param name="maxRetryAttempts">The maximum number of retry attempts. This must be greater than or equal to 1.</param>
    public RetryAttribute(int maxRetryAttempts)
    {
#pragma warning disable CA1512 // Use ArgumentOutOfRangeException throw helper
        if (maxRetryAttempts < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(maxRetryAttempts));
        }
#pragma warning disable CA1512 // Use ArgumentOutOfRangeException throw helper

        MaxRetryAttempts = maxRetryAttempts;
    }

    /// <summary>
    /// Gets the number of retries that the test should make in case of failures.
    /// Note that before RetryAttribute is considered, the test was already executed once.
    /// This property determines the max number of retries after the first normal run.
    /// </summary>
    public int MaxRetryAttempts { get; }

    /// <summary>
    /// Gets or sets the delay, in milliseconds, between retries.
    /// This delay is also applied after the first run and before the first retry attempt.
    /// </summary>
    public int MillisecondsDelayBetweenRetries { get; set; }

    /// <summary>
    /// Gets or sets the delay backoff type.
    /// </summary>
    public DelayBackoffType BackoffType
    {
        get => field;
        set
        {
            if (value is < DelayBackoffType.Constant or > DelayBackoffType.Exponential)
            {
                throw new ArgumentOutOfRangeException(nameof(value));
            }

            field = value;
        }
    }

    /// <summary>
    /// Retries the test method <see cref="MaxRetryAttempts"/> times in case of failure.
    /// Note that a first run of the method was already executed and failed before this method is called.
    /// </summary>
    /// <param name="retryContext">An object to encapsulate the state needed for retry execution.</param>
    /// <returns>
    /// Returns a <see cref="RetryResult"/> object that contains the results of all attempts. Only
    /// the last added element is used to determine the test outcome.
    /// The other results are currently not used, but may be used in the future for tooling to show the
    /// state of the failed attempts.
    /// </returns>
    protected internal override async Task<RetryResult> ExecuteAsync(RetryContext retryContext)
    {
        var result = new RetryResult();
        int currentDelay = MillisecondsDelayBetweenRetries;
        for (int i = 0; i < MaxRetryAttempts; i++)
        {
            // The caller already executed the test once. So we need to do the delay here.
            await Task.Delay(currentDelay);
            if (BackoffType == DelayBackoffType.Exponential)
            {
                currentDelay *= 2;
            }

            TestResult[] testResults = await retryContext.ExecuteTaskGetter();
            result.AddResult(testResults);
            if (IsAcceptableResultForRetry(testResults))
            {
                break;
            }
        }

        return result;
    }
}
