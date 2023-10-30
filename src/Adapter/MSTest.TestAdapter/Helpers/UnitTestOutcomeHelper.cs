// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.ObjectModel;

namespace Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Helpers;

internal static class UnitTestOutcomeHelper
{
    /// <summary>
    /// Converts the parameter unitTestOutcome to testOutcome.
    /// </summary>
    /// <param name="unitTestOutcome"> The unit Test Outcome. </param>
    /// <param name="currentSettings">Current MSTest settings.</param>
    /// <returns>The Test platforms outcome.</returns>
    internal static TestOutcome ToTestOutcome(UnitTestOutcome unitTestOutcome, MSTestSettings currentSettings)
        => unitTestOutcome switch
        {
            UnitTestOutcome.Passed => TestOutcome.Passed,
            UnitTestOutcome.Failed or UnitTestOutcome.Error or UnitTestOutcome.Timeout => TestOutcome.Failed,
            UnitTestOutcome.NotRunnable => currentSettings.MapNotRunnableToFailed ? TestOutcome.Failed : TestOutcome.None,
            UnitTestOutcome.Ignored => TestOutcome.Skipped,
            UnitTestOutcome.Inconclusive => currentSettings.MapInconclusiveToFailed ? TestOutcome.Failed : TestOutcome.Skipped,
            UnitTestOutcome.NotFound => TestOutcome.NotFound,
            _ => TestOutcome.None,
        };
}
