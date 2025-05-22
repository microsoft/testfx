// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Execution;

internal static class TestResultHelpers
{
    internal static TestResult CreateIgnoredResult(string? ignoreReason)
        => new()
        {
            Outcome = UnitTestOutcome.Ignored,
            IgnoreReason = ignoreReason,
        };
}
