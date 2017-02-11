// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestTools.UnitTesting
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    
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
            DataRowAttribute[] dataRows = testMethod.GetAttributes<DataRowAttribute>(false);

            if (dataRows == null || dataRows.Length == 0)
            {
                return new TestResult[] { new TestResult() { Outcome = UnitTestOutcome.Failed, TestFailureException = new Exception(FrameworkMessages.NoDataRow) } };
            }

            return RunDataDrivenTest(testMethod, dataRows);
        }

        /// <summary>
        /// Run data driven test method.
        /// </summary>
        /// <param name="testMethod"> Test method to execute. </param>
        /// <param name="dataRows"> Data Row. </param>
        /// <returns> Results of execution. </returns>
        internal static TestResult[] RunDataDrivenTest(ITestMethod testMethod, DataRowAttribute[] dataRows)
        {
            List<TestResult> results = new List<TestResult>();

            foreach (var dataRow in dataRows)
            {
                TestResult result = testMethod.Invoke(dataRow.Data);

                if (!string.IsNullOrEmpty(dataRow.DisplayName))
                {
                    result.DisplayName = dataRow.DisplayName;
                }
                else
                {
                    result.DisplayName = string.Format(CultureInfo.CurrentCulture, FrameworkMessages.DataDrivenResultDisplayName, testMethod.TestMethodName, string.Join(",", dataRow.Data));
                }

                results.Add(result);
            }

            return results.ToArray();
        }
    }

    /// <summary>
    /// Attribute to define inline data for a test method.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
    public class DataRowAttribute : Attribute
    {
        /// <summary>
        /// The constructor.
        /// </summary>
        /// <param name="data1"> The data object. </param>
        public DataRowAttribute(object data1)
        {
            // Need to have this constructor explicitly to fix a CLS compliance error.
            this.Data = new object[] { data1 };
        }

        /// <summary>
        /// The constructor which takes in an array of arguments.
        /// </summary>
        /// <param name="data1"> A data object. </param>
        /// <param name="moreData"> More data. </param>
        public DataRowAttribute(object data1, params object[] moreData)
        {
            this.Data = new object[moreData.Length + 1];
            this.Data[0] = data1;
            Array.Copy(moreData, 0, this.Data, 1, moreData.Length);
        }

        /// <summary>
        /// Gets data for calling test method.
        /// </summary>
        public object[] Data { get; private set; }

        /// <summary>
        /// Gets or sets display name in test results for customization.
        /// </summary>
        public string DisplayName { get; set; }
    }
}
