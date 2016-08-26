// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.VisualStudio.TestTools.UnitTesting
{
    using System;
    using System.Collections.Generic;

    #region DataRow

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
        /// The <see cref="TestResult[]"/>.
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

                results.Add(result);
            }

            return results.ToArray();
        }
    }

    /// <summary>
    /// DataRowattribute for defining inline data for DataTestMethodAttribute.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
    public class DataRowAttribute : Attribute
    {
        /// <summary>
        /// Constructor without parameters to suppress error for CLS complaint.
        /// </summary>
        /// <param name="data1"> The data object. </param>
        public DataRowAttribute(object data1)
        {
            this.Data = new object[] { data1 };
        }

        /// <summary>
        /// Can provide any number of arguments.
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
        /// Gets Data for calling test method.
        /// </summary>
        public object[] Data { get; private set; }

        /// <summary>
        /// Gets or sets display name in test results for customization.
        /// </summary>
        public string DisplayName { get; set; }

    }

    #endregion
}
