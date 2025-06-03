// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestTools.UnitTesting;

/// <summary>
/// The result of a test retry.
/// </summary>
[Experimental("MSTESTEXP", UrlFormat = "https://aka.ms/mstest/diagnostics#{0}")]
public sealed class RetryResult
{
    private readonly List<TestResult[]> _testResults = [];

    /// <summary>
    /// Adds a set of test results to the retry result.
    /// </summary>
    /// <param name="testResults">The test results for the current attempt.</param>
    public void AddResult(TestResult[] testResults)
        => _testResults.Add(testResults);

    internal TestResult[]? TryGetLast()
        => _testResults.Count > 0 ? _testResults[_testResults.Count - 1] : null;
}
