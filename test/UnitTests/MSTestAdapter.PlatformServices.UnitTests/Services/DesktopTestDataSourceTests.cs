// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if NET462
using System.Data;

using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Interface;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using Moq;

using TestFramework.ForTestingMSTest;

using ITestMethod = Microsoft.VisualStudio.TestTools.UnitTesting.ITestMethod;

namespace MSTestAdapter.PlatformServices.UnitTests.Services;

public class DesktopTestDataSourceTests : TestContainer
{
    private readonly Mock<ITestMethod> _mockTestMethodInfo;
    private readonly Mock<ITestContext> _mockTestContext;

    public DesktopTestDataSourceTests()
    {
        _mockTestMethodInfo = new Mock<ITestMethod>();
        _mockTestContext = new Mock<ITestContext>();
    }

    public void GetDataShouldReadDataFromGivenDataSource()
    {
        System.Reflection.MethodInfo methodInfo = typeof(DummyTestClass).GetMethod("PassingTest");
        DataSourceAttribute dataSourceAttribute = new(
            "Microsoft.VisualStudio.TestTools.DataSource.XML", "DataTestSourceFile.xml", "settings", DataAccessMethod.Sequential);

        _mockTestMethodInfo.Setup(ds => ds.GetAttributes<DataSourceAttribute>(false))
            .Returns([dataSourceAttribute]);
        _mockTestMethodInfo.Setup(ds => ds.MethodInfo).Returns(methodInfo);

        TestDataSource testDataSource = new();
        IEnumerable<object> dataRows = testDataSource.GetData(_mockTestMethodInfo.Object, _mockTestContext.Object);

        foreach (DataRow dataRow in dataRows.Cast<DataRow>())
        {
            Verify("v1".Equals(dataRow[3]));
        }
    }

    public void GetDataShouldSetDataConnectionInTestContextObject()
    {
        System.Reflection.MethodInfo methodInfo = typeof(DummyTestClass).GetMethod("PassingTest");
        DataSourceAttribute dataSourceAttribute = new(
            "Microsoft.VisualStudio.TestTools.DataSource.XML", "DataTestSourceFile.xml", "settings", DataAccessMethod.Sequential);

        _mockTestMethodInfo.Setup(ds => ds.GetAttributes<DataSourceAttribute>(false))
            .Returns([dataSourceAttribute]);
        _mockTestMethodInfo.Setup(ds => ds.MethodInfo).Returns(methodInfo);

        TestDataSource testDataSource = new();
        IEnumerable<object> dataRows = testDataSource.GetData(_mockTestMethodInfo.Object, _mockTestContext.Object);

        _mockTestContext.Verify(tc => tc.SetDataConnection(It.IsAny<object>()), Times.Once);
    }

    #region Dummy implementation

    public class DummyTestClass
    {
        public TestContext TestContext { get; set; }

        [TestMethod]
        public void PassingTest()
        {
            Verify(TestContext.DataRow["adapter"].ToString() == "v1");
            Verify(TestContext.DataRow["targetPlatform"].ToString() == "x86");
            TestContext.AddResultFile("C:\\temp.txt");
        }

        [TestMethod]
        public void FailingTest() => Verify(TestContext.DataRow["configuration"].ToString() == "Release");
    }

    #endregion
}

#endif
