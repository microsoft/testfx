// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace MSTestAdapter.PlatformServices.Desktop.UnitTests.Services
{
    extern alias FrameworkV1;
    extern alias FrameworkV2;
    extern alias FrameworkV2Extension;

    using System.Collections.Generic;
    using System.Data;
    using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices;
    using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Interface;
    using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Interface.ObjectModel;
    using Moq;
    using Assert = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.Assert;
    using DesktopTestFrameworkV2 = FrameworkV2Extension::Microsoft.VisualStudio.TestTools.UnitTesting;
    using TestClass = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.TestClassAttribute;
    using TestFrameworkV2 = FrameworkV2::Microsoft.VisualStudio.TestTools.UnitTesting;
    using TestInitialize = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.TestInitializeAttribute;
    using TestMethod = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.TestMethodAttribute;

    [TestClass]
    public class DesktopTestDataSourceTests
    {
        private Mock<TestFrameworkV2.ITestMethod> mockTestMethodInfo;
        private Mock<ITestMethod> testMethod;
        private IDictionary<string, object> properties;
        private Mock<ITestContext> mockTestContext;

        [TestInitialize]
        public void TestInit()
        {
            this.testMethod = new Mock<ITestMethod>();
            this.properties = new Dictionary<string, object>();
            this.mockTestMethodInfo = new Mock<TestFrameworkV2.ITestMethod>();
            this.mockTestContext = new Mock<ITestContext>();
        }

        [TestMethod]
        public void GetDataShouldReadDataFromGivenDataSource()
        {
            var methodInfo = typeof(DummyTestClass).GetMethod("PassingTest");
            TestFrameworkV2.DataSourceAttribute dataSourceAttribute = new TestFrameworkV2.DataSourceAttribute(
                "Microsoft.VisualStudio.TestTools.DataSource.XML", "DataTestSourceFile.xml", "settings", TestFrameworkV2.DataAccessMethod.Sequential);

            this.mockTestMethodInfo.Setup(ds => ds.GetAttributes<TestFrameworkV2.DataSourceAttribute>(false))
                .Returns(new TestFrameworkV2.DataSourceAttribute[] { dataSourceAttribute });
            this.mockTestMethodInfo.Setup(ds => ds.MethodInfo).Returns(methodInfo);

            TestDataSource testDataSource = new TestDataSource();
            IEnumerable<object> dataRows = testDataSource.GetData(this.mockTestMethodInfo.Object, this.mockTestContext.Object);

            foreach (DataRow dataRow in dataRows)
            {
                Assert.AreEqual("v1", dataRow[3]);
            }
        }

        [TestMethod]
        public void GetDataShouldSetDataConnectionInTestContextObject()
        {
            var methodInfo = typeof(DummyTestClass).GetMethod("PassingTest");
            TestFrameworkV2.DataSourceAttribute dataSourceAttribute = new TestFrameworkV2.DataSourceAttribute(
                "Microsoft.VisualStudio.TestTools.DataSource.XML", "DataTestSourceFile.xml", "settings", TestFrameworkV2.DataAccessMethod.Sequential);

            this.mockTestMethodInfo.Setup(ds => ds.GetAttributes<TestFrameworkV2.DataSourceAttribute>(false))
                .Returns(new TestFrameworkV2.DataSourceAttribute[] { dataSourceAttribute });
            this.mockTestMethodInfo.Setup(ds => ds.MethodInfo).Returns(methodInfo);

            TestDataSource testDataSource = new TestDataSource();
            IEnumerable<object> dataRows = testDataSource.GetData(this.mockTestMethodInfo.Object, this.mockTestContext.Object);

            this.mockTestContext.Verify(tc => tc.SetDataConnection(It.IsAny<object>()), Times.Once);
        }

        #region Dummy implementation

        public class DummyTestClass
        {
            private DesktopTestFrameworkV2.TestContext testContextInstance;

            public DesktopTestFrameworkV2.TestContext TestContext
            {
                get { return this.testContextInstance; }
                set { this.testContextInstance = value; }
            }

            [TestFrameworkV2.TestMethod]
            public void PassingTest()
            {
                Assert.AreEqual(this.testContextInstance.DataRow["adapter"].ToString(), "v1");
                Assert.AreEqual(this.testContextInstance.DataRow["targetPlatform"].ToString(), "x86");
                this.TestContext.AddResultFile("C:\\temp.txt");
            }

            [TestFrameworkV2.TestMethod]
            public void FailingTest()
            {
                Assert.AreEqual(this.testContextInstance.DataRow["configuration"].ToString(), "Release");
            }
        }

        #endregion
    }
}
