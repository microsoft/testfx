// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if NET462
namespace MSTestAdapter.PlatformServices.UnitTests.Services;

using System.Collections.Generic;
using System.Data;

using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Interface;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Interface.ObjectModel;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using Moq;

using TestFramework.ForTestingMSTest;

using ITestMethod = Microsoft.VisualStudio.TestTools.UnitTesting.ITestMethod;

public class DesktopTestDataSourceTests : TestContainer
{
    private readonly Mock<ITestMethod> _mockTestMethodInfo;
    private readonly Mock<ITestMethod> _testMethod;
    private readonly IDictionary<string, object> _properties;
    private readonly Mock<ITestContext> _mockTestContext;

    public DesktopTestDataSourceTests()
    {
        _testMethod = new Mock<ITestMethod>();
        _properties = new Dictionary<string, object>();
        _mockTestMethodInfo = new Mock<ITestMethod>();
        _mockTestContext = new Mock<ITestContext>();
    }

    public void GetDataShouldReadDataFromGivenDataSource()
    {
        var methodInfo = typeof(DummyTestClass).GetMethod("PassingTest");
        DataSourceAttribute dataSourceAttribute = new(
            "Microsoft.VisualStudio.TestTools.DataSource.XML", "DataTestSourceFile.xml", "settings", DataAccessMethod.Sequential);

        _mockTestMethodInfo.Setup(ds => ds.GetAttributes<DataSourceAttribute>(false))
            .Returns(new DataSourceAttribute[] { dataSourceAttribute });
        _mockTestMethodInfo.Setup(ds => ds.MethodInfo).Returns(methodInfo);

        TestDataSource testDataSource = new();
        IEnumerable<object> dataRows = testDataSource.GetData(_mockTestMethodInfo.Object, _mockTestContext.Object);

        foreach (DataRow dataRow in dataRows)
        {
            Verify("v1".Equals(dataRow[3]));
        }
    }

    public void GetDataShouldSetDataConnectionInTestContextObject()
    {
        var methodInfo = typeof(DummyTestClass).GetMethod("PassingTest");
        DataSourceAttribute dataSourceAttribute = new(
            "Microsoft.VisualStudio.TestTools.DataSource.XML", "DataTestSourceFile.xml", "settings", DataAccessMethod.Sequential);

        _mockTestMethodInfo.Setup(ds => ds.GetAttributes<DataSourceAttribute>(false))
            .Returns(new DataSourceAttribute[] { dataSourceAttribute });
        _mockTestMethodInfo.Setup(ds => ds.MethodInfo).Returns(methodInfo);

        TestDataSource testDataSource = new();
        IEnumerable<object> dataRows = testDataSource.GetData(_mockTestMethodInfo.Object, _mockTestContext.Object);

        _mockTestContext.Verify(tc => tc.SetDataConnection(It.IsAny<object>()), Times.Once);
    }

    #region Dummy implementation

    public class DummyTestClass
    {
        private TestContext _testContextInstance;

        public TestContext TestContext
        {
            get { return _testContextInstance; }
            set { _testContextInstance = value; }
        }

        [TestMethod]
        public void PassingTest()
        {
            Verify("v1" == _testContextInstance.DataRow["adapter"].ToString());
            Verify("x86" == _testContextInstance.DataRow["targetPlatform"].ToString());
            TestContext.AddResultFile("C:\\temp.txt");
        }

        [TestMethod]
        public void FailingTest()
        {
            Verify("Release" == _testContextInstance.DataRow["configuration"].ToString());
        }
    }

#endregion
}

#endif
