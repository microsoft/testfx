// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace UnitTestFramework.Tests
{
    extern alias FrameworkV1;
    extern alias FrameworkV2;

    using System.Collections.Generic;
    using System.Linq;

    using Moq;

    using TestFrameworkV1 = FrameworkV1.Microsoft.VisualStudio.TestTools.UnitTesting;
    using TestFrameworkV2 = FrameworkV2.Microsoft.VisualStudio.TestTools.UnitTesting;

    /// <summary>
    /// Tests for class DataTestMethodAttribute
    /// </summary>
    [TestFrameworkV1.TestClass]
    public class DataTestMethodAttributeTests
    {
        private Mock<TestFrameworkV2.ITestMethod> testMethod;

        [TestFrameworkV1.TestInitialize]
        public void TestInit()
        {
            this.testMethod = new Mock<TestFrameworkV2.ITestMethod>();
        }

        [TestFrameworkV1.IgnoreAttribute]
        [TestFrameworkV1.TestMethod]
        public void RunDataDrivenTestShouldFillInDisplayNameWithDataRowDisplayNameIfProvided()
        {
            int dummyIntData = 2;
            string dummyStringData = "DummyString";
            TestFrameworkV2.DataRowAttribute dataRowAttribute = new TestFrameworkV2.DataRowAttribute(
                dummyIntData,
                dummyStringData);
            dataRowAttribute.DisplayName = "DataRowTestDisplayName";

            TestFrameworkV2.TestResult testResult = new TestFrameworkV2.TestResult();

            // Setup mocks.
            this.testMethod.Setup(tm => tm.TestMethodName).Returns("DummyTestMethod");
            this.testMethod.Setup(tm => tm.Invoke(It.IsAny<object[]>())).Returns(testResult);

            // Act.
            TestFrameworkV2.TestResult[] results =
                TestFrameworkV2.DataTestMethodAttribute.RunDataDrivenTest(
                    this.testMethod.Object,
                    new TestFrameworkV2.DataRowAttribute[] { dataRowAttribute });

            TestFrameworkV2.Assert.AreEqual(results[0].DisplayName, "DataRowTestDisplayName");
        }

        [TestFrameworkV1.TestMethod]
        public void RunDataDrivenTestShouldFillInDisplayNameWithDataRowArgumentsIfNoDisplayNameIsProvided()
        {
            int dummyIntData = 2;
            string dummyStringData = "DummyString";
            TestFrameworkV2.DataRowAttribute dataRowAttribute = new TestFrameworkV2.DataRowAttribute(
                dummyIntData,
                dummyStringData);

            TestFrameworkV2.TestResult testResult = new TestFrameworkV2.TestResult();

            // Setup mocks.
            this.testMethod.Setup(tm => tm.TestMethodName).Returns("DummyTestMethod");
            this.testMethod.Setup(tm => tm.Invoke(It.IsAny<object[]>())).Returns(testResult);

            // Act.
            TestFrameworkV2.TestResult[] results =
                TestFrameworkV2.DataTestMethodAttribute.RunDataDrivenTest(
                    this.testMethod.Object,
                    new TestFrameworkV2.DataRowAttribute[] { dataRowAttribute });

            TestFrameworkV2.Assert.AreEqual(results[0].DisplayName, "DummyTestMethod (2,DummyString)");
        }

        [TestFrameworkV1.TestMethod]
        public void RunDataDrivenTestShouldSetResultFilesIfPresent()
        {
            int dummyIntData1 = 1;
            int dummyIntData2 = 2;
            TestFrameworkV2.DataRowAttribute dataRowAttribute1 = new TestFrameworkV2.DataRowAttribute(dummyIntData1);
            TestFrameworkV2.DataRowAttribute dataRowAttribute2 = new TestFrameworkV2.DataRowAttribute(dummyIntData2);

            TestFrameworkV2.TestResult testResult = new TestFrameworkV2.TestResult();
            testResult.ResultFiles = new List<string>() { "C:\\temp.txt" };

            // Setup mocks.
            this.testMethod.Setup(tm => tm.Invoke(It.IsAny<object[]>())).Returns(testResult);

            // Act.
            TestFrameworkV2.TestResult[] results =
                TestFrameworkV2.DataTestMethodAttribute.RunDataDrivenTest(
                    this.testMethod.Object,
                    new TestFrameworkV2.DataRowAttribute[] { dataRowAttribute1, dataRowAttribute2 });

            TestFrameworkV1.CollectionAssert.Contains(results[0].ResultFiles.ToList(), "C:\\temp.txt");
            TestFrameworkV1.CollectionAssert.Contains(results[1].ResultFiles.ToList(), "C:\\temp.txt");
        }
    }
}
