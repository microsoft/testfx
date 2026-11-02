// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.ObjectModel;

namespace Microsoft.VisualStudio.TestTools.UnitTesting;

/// <summary>
/// The result of a test retry.
/// </summary>
/// <remarks>
/// This API is experimental. It may change, break, or be removed at any time without notice.
/// </remarks>
[Experimental("MSTESTEXP", UrlFormat = "https://aka.ms/mstest/diagnostics#{0}")]
public sealed class RetryResult
{
    private readonly List<TestResult[]> _testResults = [];
    private ReadOnlyCollection<TestResult[]>? _testResultsView;

    /// <summary>
    /// Gets the test results of all retry attempts, in the order they were added.
    /// Each element corresponds to a single attempt and holds the test results produced by that attempt.
    /// </summary>
    /// <remarks>
    /// All attempts are reported to Microsoft.Testing.Platform: the last one as the test's outcome, the earlier
    /// ones tagged as superseded so tooling can surface the retry (see the platform's <c>RetryAttemptProperty</c>).
    /// The VSTest host has no notion of attempts, so it receives only the final result.
    /// </remarks>
    public IReadOnlyList<TestResult[]> AllResults
        => _testResultsView ??= new ReadOnlyCollection<TestResult[]>(_testResults);

    /// <summary>
    /// Adds a set of test results to the retry result.
    /// </summary>
    /// <param name="testResults">The test results for the current attempt.</param>
    public void AddResult(TestResult[] testResults)
        => _testResults.Add(testResults);

    internal TestResult[]? TryGetLast()
        => _testResults.Count > 0 ? _testResults[_testResults.Count - 1] : null;
}
