// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestTools.UnitTesting;

/// <summary>
/// An abstract attribute that controls retrying a test method if it failed. It's up to the derived classes to
/// define how the retry is done.
/// </summary>
/// <remarks>
/// When applied to a test class, the attribute is used as a default for every test method declared on the class.
/// A retry attribute placed directly on a test method takes precedence over a class-level one.
/// The attribute is not inherited: applying it to a base test class does not apply it to derived test classes.
/// <para>
/// Retry is only triggered when a test result has an outcome of <see cref="UnitTestOutcome.Failed"/> or
/// <see cref="UnitTestOutcome.Timeout"/>. Any other outcome (including <see cref="UnitTestOutcome.Inconclusive"/>,
/// for example when the test calls <c>Assert.Inconclusive()</c>) is considered an acceptable result and
/// stops retrying, so that outcome becomes the final result of the test.
/// </para>
/// </remarks>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
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
    [Experimental("MSTESTEXP", UrlFormat = "https://aka.ms/mstest/diagnostics#{0}")]
    protected internal abstract Task<RetryResult> ExecuteAsync(RetryContext retryContext);

    internal static bool IsAcceptableResultForRetry(TestResult[] results)
    {
        foreach (TestResult result in results)
        {
            UnitTestOutcome outcome = result.Outcome;

            // Only Failed and Timeout outcomes are considered retriable. Every other outcome
            // (including Inconclusive) is treated as an acceptable result that stops the retry loop.
            if (outcome is UnitTestOutcome.Failed or UnitTestOutcome.Timeout)
            {
                return false;
            }
        }

        return true;
    }
}
