// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestTools.UnitTesting;

public sealed class RetryResult
{
    private readonly List<TestResult[]> _testResults = new();

    public void AddResult(TestResult[] testResults)
        => _testResults.Add(testResults);

    internal TestResult[]? TryGetLast()
        => _testResults.Count > 0 ? _testResults[_testResults.Count - 1] : null;
}
