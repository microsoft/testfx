// Copyright (c) Microsoft. All rights reserved.

using System.Collections.Generic;

namespace UnitTestFramework.Tests
{
    extern alias FrameworkV1;
    extern alias FrameworkV2;

    using Moq;
    using System;

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

        [TestFrameworkV1.TestMethod]
        public void RunDataDrivenTestShouldFillInDisplayNameWithDataRowDisplayNameIfProvided()
        {
            int dummyIntData = 2;
            string dummyStringData = "DummyString";
            TestFrameworkV2.DataRowAttribute dataRowAttribute = new TestFrameworkV2.DataRowAttribute(dummyIntData,
                dummyStringData);
            dataRowAttribute.DisplayName = "DataRowTestDisplayName";

            TestFrameworkV2.TestResult testResult = new TestFrameworkV2.TestResult();

            //Setup mocks.
            this.testMethod.Setup(tm => tm.TestMethodName).Returns("DummyTestMethod");
            this.testMethod.Setup(tm => tm.Invoke(It.IsAny<object[]>())).Returns(testResult);

            //Act.
            TestFrameworkV2.TestResult[] results =
                TestFrameworkV2.DataTestMethodAttribute.RunDataDrivenTest(this.testMethod.Object,
                    new TestFrameworkV2.DataRowAttribute[] {dataRowAttribute});

            TestFrameworkV2.Assert.AreEqual(results[0].DisplayName, "DataRowTestDisplayName");
        }

        [TestFrameworkV1.TestMethod]
        public void RunDataDrivenTestShouldFillInDisplayNameWithDataRowArgumentsIfNoDisplayNameIsProvided()
        {
            int dummyIntData = 2;
            string dummyStringData = "DummyString";
            TestFrameworkV2.DataRowAttribute dataRowAttribute = new TestFrameworkV2.DataRowAttribute(dummyIntData,
                dummyStringData);

            TestFrameworkV2.TestResult testResult = new TestFrameworkV2.TestResult();

            //Setup mocks.
            this.testMethod.Setup(tm => tm.TestMethodName).Returns("DummyTestMethod");
            this.testMethod.Setup(tm => tm.Invoke(It.IsAny<object[]>())).Returns(testResult);

            //Act.
            TestFrameworkV2.TestResult[] results =
                TestFrameworkV2.DataTestMethodAttribute.RunDataDrivenTest(this.testMethod.Object,
                    new TestFrameworkV2.DataRowAttribute[] {dataRowAttribute});

            TestFrameworkV2.Assert.AreEqual(results[0].DisplayName, "DummyTestMethod (2,DummyString)");
        }
    }
}
