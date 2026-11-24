// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Extensions;

/// <summary>
/// Extension methods for <see cref="UnitTestOutcome"/>.
/// </summary>
internal static class UnitTestOutcomeExtensions
{
    /// <summary>
    /// Converts the test framework's UnitTestOutcome object to adapter's UnitTestOutcome object.
    /// </summary>
    /// <param name="frameworkTestOutcome">The test framework's UnitTestOutcome object.</param>
    /// <returns>The adapter's UnitTestOutcome object.</returns>
    private static int GetImportance(this UnitTestOutcome frameworkTestOutcome)
        => frameworkTestOutcome switch
        {
            UnitTestOutcome.Failed => 1,
            UnitTestOutcome.Timeout => 2,
            UnitTestOutcome.Inconclusive => 3,
            UnitTestOutcome.Ignored => 4,
            UnitTestOutcome.NotRunnable => 5,
            UnitTestOutcome.Passed => 6,
            UnitTestOutcome.NotFound => 7,
            UnitTestOutcome.InProgress => 8,
            _ => 0,
        };

    /// <summary>
    /// Returns more important outcome of two.
    /// </summary>
    /// <param name="outcome1"> First outcome that needs to be compared. </param>
    /// <param name="outcome2"> Second outcome that needs to be compared. </param>
    /// <returns> Outcome which has higher importance.</returns>
    internal static UnitTestOutcome GetMoreImportantOutcome(this UnitTestOutcome outcome1, UnitTestOutcome outcome2)
    {
        int unitTestOutcome1 = outcome1.GetImportance();
        int unitTestOutcome2 = outcome2.GetImportance();
        return unitTestOutcome1 < unitTestOutcome2 ? outcome1 : outcome2;
    }
}
