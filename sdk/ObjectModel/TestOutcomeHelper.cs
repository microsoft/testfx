// ---------------------------------------------------------------------------
// <copyright file="TestOutcomeHelper.cs" company="Microsoft"> 
//     Copyright (c) Microsoft Corporation. All rights reserved. 
// </copyright> 
// <summary>
//     Helper methods for working with the TestOutcome enum.
// </summary>
// <owner>apatters</owner> 
// ---------------------------------------------------------------------------
using System;

namespace Microsoft.VisualStudio.TestPlatform.ObjectModel
{
    /// <summary>
    /// Helper methods for working with the TestOutcome enum.
    /// </summary>
    public static class TestOutcomeHelper
    {
        /// <summary>
        /// Converts the outcome into its localized string representation.
        /// </summary>
        /// <param name="outcome">The outcome to get the string for.</param>
        /// <returns>The localized outcome string.</returns>
        public static string GetOutcomeString(TestOutcome outcome)
        {
            string result = null;

            switch (outcome)
            {
                case TestOutcome.None:
                    result = Resources.TestOutcomeNone;
                    break;
                case TestOutcome.Passed:
                    result = Resources.TestOutcomePassed;
                    break;
                case TestOutcome.Failed:
                    result = Resources.TestOutcomeFailed;
                    break;
                case TestOutcome.Skipped:
                    result = Resources.TestOutcomeSkipped;
                    break;
                case TestOutcome.NotFound:
                    result = Resources.TestOutcomeNotFound;
                    break;
                default:
                    throw new ArgumentOutOfRangeException("outcome");
            }

            return result;
        }
    }
}
