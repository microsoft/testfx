// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Helpers
{
    using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.ObjectModel;
    using Microsoft.VisualStudio.TestPlatform.ObjectModel;

    using UTF = Microsoft.VisualStudio.TestTools.UnitTesting;

    internal static class UnitTestOutcomeHelper
    {
        /// <summary>
        /// Converts the parameter unitTestOutcome to testOutcome
        /// </summary>
        /// <param name="unitTestOutcome"> The unit Test Outcome. </param>
        /// <param name="mapInconclusiveToFailed">Should map inconclusive to failed.</param>
        /// <param name="mapNotRunnableToFailed">Should map not runnable to failed</param>
        /// <returns>The Test platforms outcome.</returns>
        internal static TestOutcome ToTestOutcome(UnitTestOutcome unitTestOutcome, bool mapInconclusiveToFailed, bool mapNotRunnableToFailed)
        {
            switch (unitTestOutcome)
            {
                case UnitTestOutcome.Passed:
                    return TestOutcome.Passed;

                case UnitTestOutcome.Failed:
                case UnitTestOutcome.Error:
                case UnitTestOutcome.Timeout:
                    return TestOutcome.Failed;

                case UnitTestOutcome.NotRunnable:
                    {
                        if (mapNotRunnableToFailed)
                        {
                            return TestOutcome.Failed;
                        }

                        return TestOutcome.None;
                    }

                case UnitTestOutcome.Ignored:
                    return TestOutcome.Skipped;

                case UnitTestOutcome.Inconclusive:
                    {
                        if (mapInconclusiveToFailed)
                        {
                            return TestOutcome.Failed;
                        }

                        return TestOutcome.Skipped;
                    }

                case UnitTestOutcome.NotFound:
                    return TestOutcome.NotFound;

                default:
                    return TestOutcome.None;
            }
        }
    }
}
