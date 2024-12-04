// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.ObjectModel;

using UTF = Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Extensions;

#if NET6_0_OR_GREATER
[Obsolete(Constants.PublicTypeObsoleteMessage, DiagnosticId = "MSTESTOBS")]
#else
[Obsolete(Constants.PublicTypeObsoleteMessage)]
#endif
public static class UnitTestOutcomeExtensions
{
    /// <summary>
    /// Converts the test framework's UnitTestOutcome object to adapter's UnitTestOutcome object.
    /// </summary>
    /// <param name="frameworkTestOutcome">The test framework's UnitTestOutcome object.</param>
    /// <returns>The adapter's UnitTestOutcome object.</returns>
    public static UnitTestOutcome ToUnitTestOutcome(this UTF.UnitTestOutcome frameworkTestOutcome)
        => frameworkTestOutcome switch
        {
            UTF.UnitTestOutcome.Failed => UnitTestOutcome.Failed,
            UTF.UnitTestOutcome.Inconclusive => UnitTestOutcome.Inconclusive,
            UTF.UnitTestOutcome.InProgress => UnitTestOutcome.InProgress,
            UTF.UnitTestOutcome.Passed => UnitTestOutcome.Passed,
            UTF.UnitTestOutcome.Timeout => UnitTestOutcome.Timeout,
            UTF.UnitTestOutcome.NotRunnable => UnitTestOutcome.NotRunnable,
            UTF.UnitTestOutcome.NotFound => UnitTestOutcome.NotFound,
            _ => UnitTestOutcome.Error,
        };

    /// <summary>
    /// Returns more important outcome of two.
    /// </summary>
    /// <param name="outcome1"> First outcome that needs to be compared. </param>
    /// <param name="outcome2"> Second outcome that needs to be compared. </param>
    /// <returns> Outcome which has higher importance.</returns>
    internal static UTF.UnitTestOutcome GetMoreImportantOutcome(this UTF.UnitTestOutcome outcome1, UTF.UnitTestOutcome outcome2)
    {
        var unitTestOutcome1 = outcome1.ToUnitTestOutcome();
        var unitTestOutcome2 = outcome2.ToUnitTestOutcome();
        return unitTestOutcome1 < unitTestOutcome2 ? outcome1 : outcome2;
    }
}
