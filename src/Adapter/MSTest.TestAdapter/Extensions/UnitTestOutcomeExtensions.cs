﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Extensions
{
    using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.ObjectModel;

    using UTF = Microsoft.VisualStudio.TestTools.UnitTesting;

    public static class UnitTestOutcomeExtensions
    {
        /// <summary>
        /// Converts the test framework's UnitTestOutcome object to adapter's UnitTestOutcome object.
        /// </summary>
        /// <param name="frameworkTestOutcome">The test framework's UnitTestOutcome object.</param>
        /// <returns>The adapter's UnitTestOutcome object.</returns>
        public static UnitTestOutcome ToUnitTestOutcome(this UTF.UnitTestOutcome frameworkTestOutcome)
        {
            UnitTestOutcome outcome = UnitTestOutcome.Passed;

            switch (frameworkTestOutcome)
            {
                case UTF.UnitTestOutcome.Failed:
                    outcome = UnitTestOutcome.Failed;
                    break;

                case UTF.UnitTestOutcome.Inconclusive:
                    outcome = UnitTestOutcome.Inconclusive;
                    break;

                case UTF.UnitTestOutcome.InProgress:
                    outcome = UnitTestOutcome.InProgress;
                    break;

                case UTF.UnitTestOutcome.Passed:
                    outcome = UnitTestOutcome.Passed;
                    break;

                case UTF.UnitTestOutcome.Timeout:
                    outcome = UnitTestOutcome.Timeout;
                    break;

                case UTF.UnitTestOutcome.NotRunnable:
                    outcome = UnitTestOutcome.NotRunnable;
                    break;

                case UTF.UnitTestOutcome.Unknown:
                default:
                    outcome = UnitTestOutcome.Error;
                    break;
            }

            return outcome;
        }

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
}
