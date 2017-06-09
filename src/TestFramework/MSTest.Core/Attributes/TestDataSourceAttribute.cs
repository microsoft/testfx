// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestTools.UnitTesting
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Reflection;

    /// <summary>
    /// Test data source attribute for data driven tests.
    /// </summary>
    public abstract class TestDataSourceAttribute : Attribute
    {
        /// <summary>
        /// Gets the test data from custom test data source.
        /// </summary>
        /// <param name="methodInfo">
        /// The method info of test method.
        /// </param>
        /// <returns>
        /// Test data for calling test method.
        /// </returns>
        public abstract IEnumerable<object[]> GetData(MethodInfo methodInfo);

        /// <summary>
        /// Gets the display name corresponding to test data row for displaying in TestResults.
        /// </summary>
        /// <param name="methodInfo">
        /// The method Info of test method.
        /// </param>
        /// <param name="data">
        /// The test data which is passed to test method.
        /// </param>
        /// <returns>
        /// The <see cref="string"/>.
        /// </returns>
        public virtual string GetDisplayName(MethodInfo methodInfo, object[] data)
        {
            if (data != null)
            {
                return string.Format(
                    CultureInfo.CurrentCulture,
                    FrameworkMessages.DataDrivenResultDisplayName,
                    methodInfo.Name,
                    string.Join(",", data));
            }

            return null;
        }
    }
}
