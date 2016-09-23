//   Copyright (c) Microsoft Corporation. All rights reserved.

namespace MSTestAdapter.PlatformServices.Desktop.UnitTests.Services
{
    extern alias FrameworkV1;
    extern alias FrameworkV2;
    extern alias FrameworkV2DesktopExtension;

    using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices;
    using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Interface.ObjectModel;
    using Moq;
    using System.IO;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;

    using Assert = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.Assert;
    using CollectionAssert = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.CollectionAssert;
    using TestClass = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.TestClassAttribute;
    using TestFrameworkV2 = FrameworkV2::Microsoft.VisualStudio.TestTools.UnitTesting;
    using TestInitialize = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.TestInitializeAttribute;
    using TestMethod = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.TestMethodAttribute;
    using DesktopTestFrameworkV2 = FrameworkV2DesktopExtension::Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class DesktopTestDataSourceTests
    {
        private Mock<TestFrameworkV2.ITestMethod> mockTestMethodInfo;
        private Mock<ITestMethod> testMethod;
        private IDictionary<string, object> properties;
        private TestContextImplementation testContextImplementation;

        [TestInitialize]
        public void TestInit()
        {
            this.testMethod = new Mock<ITestMethod>();
            this.properties = new Dictionary<string, object>();
            this.mockTestMethodInfo = new Mock<TestFrameworkV2.ITestMethod>();
        }

        [TestMethod]
        public void HasDataDrivenTestsReturnsTrueWhenTestIsDataDriven()
        {
            TestFrameworkV2.DataSourceAttribute dataSourceAttribute = new TestFrameworkV2.DataSourceAttribute(
                "Microsoft.VisualStudio.TestTools.DataSource.XML", "DataTestSourceFile.xml", "settings", TestFrameworkV2.DataAccessMethod.Sequential);
            this.mockTestMethodInfo.Setup(ds => ds.GetAttributes<TestFrameworkV2.DataSourceAttribute>(false))
                .Returns(new TestFrameworkV2.DataSourceAttribute[] { dataSourceAttribute } );

            TestDataSource testDataSource = new TestDataSource();
            bool result = testDataSource.HasDataDrivenTests(this.mockTestMethodInfo.Object);
            Assert.AreEqual(result, true);
        }

        [TestMethod]
        public void HasDataDrivenReturnsFalseWhenDataSourceAttributeIsNull()
        {
            TestFrameworkV2.DataSourceAttribute[] dataSourceAttribute = null;
            this.mockTestMethodInfo.Setup(ds => ds.GetAttributes<TestFrameworkV2.DataSourceAttribute>(false))
                .Returns(dataSourceAttribute);

            TestDataSource testDataSource = new TestDataSource();
            bool result = testDataSource.HasDataDrivenTests(this.mockTestMethodInfo.Object);
            Assert.AreEqual(result, false);
        }


        [TestMethod]
        public void RunDataDrivenTestsGivesTestResultAsPassedWhenTestMethodPasses()
        {
            this.testContextImplementation = new TestContextImplementation(this.testMethod.Object, new System.IO.StringWriter(), this.properties);

            TestFrameworkV2.DataSourceAttribute dataSourceAttribute = new TestFrameworkV2.DataSourceAttribute(
                "Microsoft.VisualStudio.TestTools.DataSource.XML", "DataTestSourceFile.xml", "settings", TestFrameworkV2.DataAccessMethod.Sequential);

            this.mockTestMethodInfo.Setup(ds => ds.GetAttributes<TestFrameworkV2.DataSourceAttribute>(false))
                .Returns(new TestFrameworkV2.DataSourceAttribute[] { dataSourceAttribute });

            TestFrameworkV2.TestResult testResult = new TestFrameworkV2.TestResult();
            DummyTestClass testClassInstance = new DummyTestClass();
            var methodInfo = typeof(DummyTestClass).GetMethod("PassingTest");
            
            this.mockTestMethodInfo.Setup(ds => ds.Invoke(null)).
                Callback( ()=>
                {
                    try
                    {
                        testClassInstance.TestContext = this.testContextImplementation;

                        var task = methodInfo.Invoke(testClassInstance, null) as Task;
                        task?.GetAwaiter().GetResult();

                        testResult.Outcome = TestFrameworkV2.UnitTestOutcome.Passed;
                    }
                    catch(Exception ex)
                    {
                        testResult.Outcome = TestFrameworkV2.UnitTestOutcome.Failed;
                        testResult.TestFailureException = ex;
                    }
                }
                ).Returns(testResult);
            this.mockTestMethodInfo.Setup(ds => ds.MethodInfo).Returns(methodInfo);

            TestFrameworkV2.TestMethodAttribute testMethodAttribute = new TestFrameworkV2.TestMethodAttribute();
            TestDataSource testDataSource = new TestDataSource();

            TestFrameworkV2.TestResult[] result = testDataSource.RunDataDrivenTest(this.testContextImplementation, this.mockTestMethodInfo.Object, null, testMethodAttribute);
            Assert.AreEqual(result[0].Outcome, TestFrameworkV2.UnitTestOutcome.Passed);
        }

        [TestMethod]
        public void RunDataDrivenTestsGivesTestResultAsFailedWhenTestMethodFails()
        {
            this.testContextImplementation = new TestContextImplementation(this.testMethod.Object, new System.IO.StringWriter(), this.properties);

            TestFrameworkV2.DataSourceAttribute dataSourceAttribute = new TestFrameworkV2.DataSourceAttribute(
                "Microsoft.VisualStudio.TestTools.DataSource.XML", "DataTestSourceFile.xml", "settings", TestFrameworkV2.DataAccessMethod.Sequential);


            this.mockTestMethodInfo.Setup(ds => ds.GetAttributes<TestFrameworkV2.DataSourceAttribute>(false))
                .Returns(new TestFrameworkV2.DataSourceAttribute[] { dataSourceAttribute });

            TestFrameworkV2.TestResult testResult = new TestFrameworkV2.TestResult();
            DummyTestClass testClassInstance = new DummyTestClass();

            var methodInfo = typeof(DummyTestClass).GetMethod("FailingTest");
            
            this.mockTestMethodInfo.Setup(ds => ds.Invoke(null)).
                Callback(() =>
                {
                    try
                    {
                        testClassInstance.TestContext = this.testContextImplementation;

                        var task = methodInfo.Invoke(testClassInstance, null) as Task;
                        task?.GetAwaiter().GetResult();

                        testResult.Outcome = TestFrameworkV2.UnitTestOutcome.Passed;
                    }
                    catch (Exception ex)
                    {
                        testResult.Outcome = TestFrameworkV2.UnitTestOutcome.Failed;
                        testResult.TestFailureException = ex;
                    }
                }
                ).Returns(testResult);
            this.mockTestMethodInfo.Setup(ds => ds.MethodInfo).Returns(methodInfo);

            TestFrameworkV2.TestMethodAttribute testMethodAttribute = new TestFrameworkV2.TestMethodAttribute();
            TestDataSource testDataSource = new TestDataSource();

            TestFrameworkV2.TestResult[] result = testDataSource.RunDataDrivenTest(this.testContextImplementation, this.mockTestMethodInfo.Object, null, testMethodAttribute);
            Assert.AreEqual(result[0].Outcome, TestFrameworkV2.UnitTestOutcome.Failed);
        }

        [TestMethod]
        public void RunDataDrivenTestsShouldSetDataRowIndex()
        {
            this.testContextImplementation = new TestContextImplementation(this.testMethod.Object, new System.IO.StringWriter(), this.properties);

            TestFrameworkV2.DataSourceAttribute dataSourceAttribute = new TestFrameworkV2.DataSourceAttribute(
                "Microsoft.VisualStudio.TestTools.DataSource.XML", "DataTestSourceFile.xml", "settings", TestFrameworkV2.DataAccessMethod.Sequential);

            this.mockTestMethodInfo.Setup(ds => ds.GetAttributes<TestFrameworkV2.DataSourceAttribute>(false))
                .Returns(new TestFrameworkV2.DataSourceAttribute[] { dataSourceAttribute });

            TestFrameworkV2.TestResult testResult = new TestFrameworkV2.TestResult();
            DummyTestClass testClassInstance = new DummyTestClass();
            var methodInfo = typeof(DummyTestClass).GetMethod("PassingTest");

            this.mockTestMethodInfo.Setup(ds => ds.Invoke(null)).
                Callback(() =>
                {
                    try
                    {
                        testClassInstance.TestContext = this.testContextImplementation;

                        var task = methodInfo.Invoke(testClassInstance, null) as Task;
                        task?.GetAwaiter().GetResult();

                        testResult.Outcome = TestFrameworkV2.UnitTestOutcome.Passed;
                    }
                    catch (Exception ex)
                    {
                        testResult.Outcome = TestFrameworkV2.UnitTestOutcome.Failed;
                        testResult.TestFailureException = ex;
                    }
                }
                ).Returns(testResult);
            this.mockTestMethodInfo.Setup(ds => ds.MethodInfo).Returns(methodInfo);

            TestFrameworkV2.TestMethodAttribute testMethodAttribute = new TestFrameworkV2.TestMethodAttribute();
            TestDataSource testDataSource = new TestDataSource();

            TestFrameworkV2.TestResult[] result = testDataSource.RunDataDrivenTest(this.testContextImplementation, this.mockTestMethodInfo.Object, null, testMethodAttribute);
            Assert.AreEqual(result[3].DatarowIndex, 3);
        }

        [TestMethod]
        public void RunDataDrivenTestsShouldAttachResultsFilesForEachTestCase()
        {
            this.testContextImplementation = new TestContextImplementation(this.testMethod.Object, new System.IO.StringWriter(), this.properties);

            TestFrameworkV2.DataSourceAttribute dataSourceAttribute = new TestFrameworkV2.DataSourceAttribute(
                "Microsoft.VisualStudio.TestTools.DataSource.XML", "DataTestSourceFile.xml", "settings", TestFrameworkV2.DataAccessMethod.Sequential);

            this.mockTestMethodInfo.Setup(ds => ds.GetAttributes<TestFrameworkV2.DataSourceAttribute>(false))
                .Returns(new TestFrameworkV2.DataSourceAttribute[] { dataSourceAttribute });

            TestFrameworkV2.TestResult testResult = new TestFrameworkV2.TestResult();
            DummyTestClass testClassInstance = new DummyTestClass();
            var methodInfo = typeof(DummyTestClass).GetMethod("PassingTest");

            this.mockTestMethodInfo.Setup(ds => ds.Invoke(null)).
                Callback(() =>
                {
                    try
                    {
                        this.testContextImplementation.AddResultFile("C:\\temp.txt");
                        testClassInstance.TestContext = this.testContextImplementation;

                        var task = methodInfo.Invoke(testClassInstance, null) as Task;
                        task?.GetAwaiter().GetResult();

                        testResult.Outcome = TestFrameworkV2.UnitTestOutcome.Passed;
                    }
                    catch (Exception ex)
                    {
                        testResult.Outcome = TestFrameworkV2.UnitTestOutcome.Failed;
                        testResult.TestFailureException = ex;
                    }
                }
                ).Returns(testResult);
            this.mockTestMethodInfo.Setup(ds => ds.MethodInfo).Returns(methodInfo);

            TestFrameworkV2.TestMethodAttribute testMethodAttribute = new TestFrameworkV2.TestMethodAttribute();
            TestDataSource testDataSource = new TestDataSource();

            TestFrameworkV2.TestResult[] result = testDataSource.RunDataDrivenTest(this.testContextImplementation, this.mockTestMethodInfo.Object, null, testMethodAttribute);
            CollectionAssert.Contains(result[0].ResultFiles.ToList(), "C:\\temp.txt");
            CollectionAssert.Contains(result[1].ResultFiles.ToList(), "C:\\temp.txt");
            CollectionAssert.Contains(result[2].ResultFiles.ToList(), "C:\\temp.txt");
            CollectionAssert.Contains(result[3].ResultFiles.ToList(), "C:\\temp.txt");
        }

        #region Dummy implementation

        public class DummyTestClass
        {
            private DesktopTestFrameworkV2.TestContext testContextInstance;

            public DesktopTestFrameworkV2.TestContext TestContext
            {
                get { return testContextInstance; }
                set { testContextInstance = value; }
            }


            [TestFrameworkV2.TestMethod]
            public void PassingTest()
            {
                Assert.AreEqual(testContextInstance.DataRow["adapter"].ToString(), "v1");
                Assert.AreEqual(testContextInstance.DataRow["targetPlatform"].ToString(), "x86");
            }

            [TestFrameworkV2.TestMethod]
            public void FailingTest()
            {
                Assert.AreEqual(testContextInstance.DataRow["configuration"].ToString(), "Release");
            }
        }

        #endregion

    }
}
