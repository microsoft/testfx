// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestPlatform.TestFramework.UnitTests.Attributes
{
    extern alias FrameworkV1;
    extern alias FrameworkV2;

    using System.Collections.Generic;
    using System.Reflection;

    using FrameworkV2::Microsoft.VisualStudio.TestTools.UnitTesting;
    using FrameworkV2::Microsoft.VisualStudio.TestTools.UnitTesting.Attributes;

    using TestFrameworkV1 = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestFrameworkV1.TestClass]
    public class TestDataSourceAttributeTests
    {
        private DummyTestClass dummyTestClass;

        private MethodInfo testMethodInfo;

        private TestableTestDataSourceAttribute testDataSourceAttribute;

        [TestFrameworkV1.TestInitialize]
        public void Init()
        {
            this.dummyTestClass = new DummyTestClass();
            this.testMethodInfo = this.dummyTestClass.GetType().GetTypeInfo().GetDeclaredMethod("TestMethod1");
            this.testDataSourceAttribute = new TestableTestDataSourceAttribute();
        }

        [TestFrameworkV1.TestMethod]
        public void GetDisplayNameShouldReturnDisplayName()
        {
            var data = new object[] { 1, 2, 3 };

            var displayName = this.testDataSourceAttribute.GetDisplayName(this.testMethodInfo, data);
            Assert.AreEqual("TestMethod1 (1,2,3)", displayName);
        }

        [TestFrameworkV1.TestMethod]
        public void GetDisplayNameShouldReturnEmptyStringIfDataIsNull()
        {
            var displayName = this.testDataSourceAttribute.GetDisplayName(this.testMethodInfo, null);
            Assert.IsNull(displayName);
        }

        [TestFrameworkV1.TestMethod]
        public void GetDisplayNameShouldThrowIfDataHasNullValues()
        {
            var data = new string[] { "value1", "value2", null };
            var data1 = new string[] { null, "value1", "value2" };
            var data2 = new string[] { "value1", null, "value2" };

            var displayName = this.testDataSourceAttribute.GetDisplayName(this.testMethodInfo, data);
            Assert.AreEqual("TestMethod1 (value1,value2,)", displayName);

            displayName = this.testDataSourceAttribute.GetDisplayName(this.testMethodInfo, data1);
            Assert.AreEqual("TestMethod1 ()", displayName);

            displayName = this.testDataSourceAttribute.GetDisplayName(this.testMethodInfo, data2);
            Assert.AreEqual("TestMethod1 (value1,,value2)", displayName);
        }
    }

    public class TestableTestDataSourceAttribute : TestDataSourceAttribute
    {
        public override IEnumerable<object[]> GetData(MethodInfo methodInfo)
        {
            throw new System.NotImplementedException();
        }
    }
}
