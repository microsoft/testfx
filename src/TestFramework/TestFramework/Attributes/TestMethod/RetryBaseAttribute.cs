// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestTools.UnitTesting;

/// <summary>
/// An abstract attribute that controls retrying a test method if it failed. It's up to the derived classes to
/// define how the retry is done.
/// </summary>
[AttributeUsage(AttributeTargets.Method, Inherited = false, AllowMultiple = false)]
public abstract class RetryBaseAttribute : Attribute
{
    /// <summary>
    /// Retries the test method. The details of how retry is done is left to the derived classes.
    /// Note that a first run of the method was already executed and failed before this method is called.
    /// </summary>
    /// <param name="retryContext">An object to encapsulate the state needed for retry execution.</param>
    /// <returns>
    /// Returns a <see cref="RetryResult"/> object that contains the results of all attempts. Only
    /// the last added element is used to determine the test outcome.
    /// The other results are currently not used, but may be used in the future for tooling to show the
    /// state of the failed attempts.
    /// </returns>
    protected internal abstract Task<RetryResult> ExecuteAsync(RetryContext retryContext);

    internal static bool IsAcceptableResultForRetry(TestResult[] results)
    {
        foreach (TestResult result in results)
        {
            UnitTestOutcome outcome = result.Outcome;
            if (outcome is UnitTestOutcome.Failed or UnitTestOutcome.Timeout)
            {
                return false;
            }
        }

        return true;
    }

    internal static bool IsAcceptableResultForRetry(List<TestResult> results)
    {
        foreach (TestResult result in results)
        {
            UnitTestOutcome outcome = result.Outcome;
            if (outcome is UnitTestOutcome.Failed or UnitTestOutcome.Timeout)
            {
                return false;
            }
        }

        return true;
    }
}
