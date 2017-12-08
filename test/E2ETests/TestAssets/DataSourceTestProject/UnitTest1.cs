// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DataSourceTestProject
{
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class UnitTest1
    {
        public TestContext TestContext
        {
            get;
            set;
        }

        [TestMethod, DataSource("Microsoft.VisualStudio.TestTools.DataSource.CSV", "a.csv", "a#csv", DataAccessMethod.Sequential)]
        public void CsvTestMethod()
        {
            Assert.AreEqual(1, TestContext.DataRow["Item1"]);
        }
    }
}