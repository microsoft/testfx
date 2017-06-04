// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestTools.UnitTesting
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;

    using Microsoft.VisualStudio.TestTools.UnitTesting.Interfaces;

    /// <summary>
    /// Attribute for data driven test where data can be specified inline.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    public class DataTestMethodAttribute : TestMethodAttribute
    {
        /// <summary>
        /// Find all data rows and execute.
        /// </summary>
        /// <param name="testMethod">
        /// The test Method.
        /// </param>
        /// <returns>
        /// An array of <see cref="TestResult"/>.
        /// </returns>
        public override TestResult[] Execute(ITestMethod testMethod)
        {
            DataSource[] dataSources = testMethod.GetAttributes<DataSource>(true);

            if (dataSources == null || dataSources.Length == 0)
            {
                return new TestResult[]
                           {
                                   new TestResult()
                                       {
                                           Outcome = UnitTestOutcome.Failed,
                                           TestFailureException =
                                               new Exception(FrameworkMessages.NoDataRow)
                                       }
                           };
            }

            return RunDataDrivenTest(testMethod, dataSources);
        }

        /// <summary>
        /// Run data driven test method.
        /// </summary>
        /// <param name="testMethod"> Test method to execute. </param>
        /// <param name="dataSources"> Data Sources. </param>
        /// <returns> Results of execution. </returns>
        internal static TestResult[] RunDataDrivenTest(ITestMethod testMethod, DataSource[] dataSources)
        {
            List<TestResult> results = new List<TestResult>();

            foreach (var dataSource in dataSources)
            {
                foreach (var data in dataSource.GetData(testMethod.MethodInfo))
                {
                    TestResult result = testMethod.Invoke(data);

                    result.DisplayName = string.Format(
                        CultureInfo.CurrentCulture,
                        FrameworkMessages.DataDrivenResultDisplayName,
                        testMethod.TestMethodName,
                        string.Join(",", data));

                    results.Add(result);
                }
            }

            return results.ToArray();
        }
    }
}
