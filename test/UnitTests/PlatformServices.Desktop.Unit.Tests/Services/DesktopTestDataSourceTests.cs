// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if NET462
namespace MSTestAdapter.PlatformServices.Desktop.UnitTests.Services;

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
    private Mock<TestFrameworkV2.ITestMethod> _mockTestMethodInfo;
    private Mock<ITestMethod> _testMethod;
    private IDictionary<string, object> _properties;
    private Mock<ITestContext> _mockTestContext;

    [TestInitialize]
    public void TestInit()
    {
        _testMethod = new Mock<ITestMethod>();
        _properties = new Dictionary<string, object>();
        _mockTestMethodInfo = new Mock<TestFrameworkV2.ITestMethod>();
        _mockTestContext = new Mock<ITestContext>();
    }

    [TestMethod]
    public void GetDataShouldReadDataFromGivenDataSource()
    {
        var methodInfo = typeof(DummyTestClass).GetMethod("PassingTest");
        TestFrameworkV2.DataSourceAttribute dataSourceAttribute = new(
            "Microsoft.VisualStudio.TestTools.DataSource.XML", "DataTestSourceFile.xml", "settings", TestFrameworkV2.DataAccessMethod.Sequential);

        _mockTestMethodInfo.Setup(ds => ds.GetAttributes<TestFrameworkV2.DataSourceAttribute>(false))
            .Returns(new TestFrameworkV2.DataSourceAttribute[] { dataSourceAttribute });
        _mockTestMethodInfo.Setup(ds => ds.MethodInfo).Returns(methodInfo);

        TestDataSource testDataSource = new();
        IEnumerable<object> dataRows = testDataSource.GetData(_mockTestMethodInfo.Object, _mockTestContext.Object);

        foreach (DataRow dataRow in dataRows)
        {
            Assert.AreEqual("v1", dataRow[3]);
        }
    }

    [TestMethod]
    public void GetDataShouldSetDataConnectionInTestContextObject()
    {
        var methodInfo = typeof(DummyTestClass).GetMethod("PassingTest");
        TestFrameworkV2.DataSourceAttribute dataSourceAttribute = new(
            "Microsoft.VisualStudio.TestTools.DataSource.XML", "DataTestSourceFile.xml", "settings", TestFrameworkV2.DataAccessMethod.Sequential);

        _mockTestMethodInfo.Setup(ds => ds.GetAttributes<TestFrameworkV2.DataSourceAttribute>(false))
            .Returns(new TestFrameworkV2.DataSourceAttribute[] { dataSourceAttribute });
        _mockTestMethodInfo.Setup(ds => ds.MethodInfo).Returns(methodInfo);

        TestDataSource testDataSource = new();
        IEnumerable<object> dataRows = testDataSource.GetData(_mockTestMethodInfo.Object, _mockTestContext.Object);

        _mockTestContext.Verify(tc => tc.SetDataConnection(It.IsAny<object>()), Times.Once);
    }

    #region Dummy implementation

    public class DummyTestClass
    {
        private DesktopTestFrameworkV2.TestContext _testContextInstance;

        public DesktopTestFrameworkV2.TestContext TestContext
        {
            get { return _testContextInstance; }
            set { _testContextInstance = value; }
        }

        [TestFrameworkV2.TestMethod]
        public void PassingTest()
        {
            Assert.AreEqual("v1", _testContextInstance.DataRow["adapter"].ToString());
            Assert.AreEqual("x86", _testContextInstance.DataRow["targetPlatform"].ToString());
            TestContext.AddResultFile("C:\\temp.txt");
        }

        [TestFrameworkV2.TestMethod]
        public void FailingTest()
        {
            Assert.AreEqual("Release", _testContextInstance.DataRow["configuration"].ToString());
        }
    }

#endregion
}

#endif
