// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.VisualStudio.TestPlatform.ObjectModel;

using UTF = Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Helpers;

internal static class UnitTestOutcomeHelper
{
    /// <summary>
    /// Converts the parameter unitTestOutcome to testOutcome.
    /// </summary>
    /// <param name="unitTestOutcome"> The unit Test Outcome. </param>
    /// <param name="currentSettings">Current MSTest settings.</param>
    /// <returns>The Test platforms outcome.</returns>
    internal static TestOutcome ToTestOutcome(UTF.UnitTestOutcome unitTestOutcome, MSTestSettings currentSettings)
        => unitTestOutcome switch
        {
            UTF.UnitTestOutcome.Passed => TestOutcome.Passed,
            UTF.UnitTestOutcome.Failed or UTF.UnitTestOutcome.Error or UTF.UnitTestOutcome.Timeout or UTF.UnitTestOutcome.Aborted or UTF.UnitTestOutcome.Unknown => TestOutcome.Failed,
            UTF.UnitTestOutcome.NotRunnable => currentSettings.MapNotRunnableToFailed ? TestOutcome.Failed : TestOutcome.None,
            UTF.UnitTestOutcome.Ignored => TestOutcome.Skipped,
            UTF.UnitTestOutcome.Inconclusive => currentSettings.MapInconclusiveToFailed ? TestOutcome.Failed : TestOutcome.Skipped,
            UTF.UnitTestOutcome.NotFound => TestOutcome.NotFound,
            _ => TestOutcome.None,
        };
}
