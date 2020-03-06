// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestPlatform.TestFramework.UnitTests.Attributes
{
    extern alias FrameworkV1;
    extern alias FrameworkV2;

    using System;
    using System.Linq;
    using System.Reflection;

    using FrameworkV2::Microsoft.VisualStudio.TestTools.UnitTesting;

    using MSTestAdapter.TestUtilities;

    using TestFrameworkV1 = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestFrameworkV1.TestClass]
    public class EnumDataSourceAttributeTests
    {
        private MethodInfo methodInfo;

        [TestFrameworkV1.TestInitialize]
        public void Initialize()
        {
            this.methodInfo = this.GetType().GetTypeInfo().GetDeclaredMethod(nameof(this.TestMethod1));
        }

        [TestFrameworkV1.TestMethod]
        public void ConstructorShouldThrowArgumentNullIfEnumDataSourceIsNull()
        {
            EnumDataSourceAttribute attribute;

            Action action = () =>
            {
                attribute = new EnumDataSourceAttribute(null);
            };

            ActionUtility.ActionShouldThrowExceptionOfType(action, typeof(ArgumentNullException));
        }

        [TestFrameworkV1.TestMethod]
        public void ConstructorShouldNotThrowIfEnumDataSourceIsNotNull()
        {
            EnumDataSourceAttribute attribute;

            Action action = () =>
            {
                attribute = new EnumDataSourceAttribute(typeof(Value));
            };
            action();
        }

        [TestFrameworkV1.TestMethod]
        public void ConstructorShouldThrowArgumentNullIfEnumDataSourceIsNullAndExclusionsPassed()
        {
            EnumDataSourceAttribute attribute;

            Action action = () =>
            {
                attribute = new EnumDataSourceAttribute(null, new object());
            };

            ActionUtility.ActionShouldThrowExceptionOfType(action, typeof(ArgumentNullException));
        }

        [TestFrameworkV1.TestMethod]
        public void ConstructorShouldNotThrowIfEnumDataSourceIsNotNullAndExclusionsPassed()
        {
            EnumDataSourceAttribute attribute;

            Action action = () =>
            {
                attribute = new EnumDataSourceAttribute(typeof(Value), new object());
            };
            action();
        }

        [TestFrameworkV1.TestMethod]
        public void GetDataShouldThrowInvalidCastIfEnumDataSourceIsNotAnEnum()
        {
            Action action = () =>
            {
                var attribute = new EnumDataSourceAttribute(typeof(int));
                attribute.GetData(this.methodInfo).ToList();
            };

            ActionUtility.ActionShouldThrowExceptionOfType(action, typeof(InvalidCastException));
        }

        [TestFrameworkV1.TestMethod]
        public void GetDataShouldReturnsAllValuesOfEnumIfNoExclusionsIsSpecified()
        {
            var attribute = new EnumDataSourceAttribute(typeof(Value));

            var results = attribute.GetData(this.methodInfo).ToList();

            var values = results.Select(r => r.First()).ToList();
            Assert.AreEqual(4, values.Count);
            Assert.IsTrue(values.Contains(Value.First));
            Assert.IsTrue(values.Contains(Value.Second));
            Assert.IsTrue(values.Contains(Value.Third));
            Assert.IsTrue(values.Contains(Value.Fourth));
        }

        [TestFrameworkV1.TestMethod]
        public void GetDataShouldReturnsAllValuesOfEnumExceptExclusionIfOneExclusionSpecified()
        {
            var attribute = new EnumDataSourceAttribute(typeof(Value), Value.Second);

            var results = attribute.GetData(this.methodInfo).ToList();

            var values = results.Select(r => r.First()).ToList();
            Assert.AreEqual(3, values.Count);
            Assert.IsTrue(values.Contains(Value.First));
            Assert.IsTrue(values.Contains(Value.Third));
            Assert.IsTrue(values.Contains(Value.Fourth));
        }

        [TestFrameworkV1.TestMethod]
        public void GetDataShouldReturnsAllValuesOfEnumExceptExclusionsIfTwoExclusionSpecified()
        {
            var attribute = new EnumDataSourceAttribute(typeof(Value), Value.Second, Value.Third);

            var results = attribute.GetData(this.methodInfo).ToList();

            var values = results.Select(r => r.First()).ToList();
            Assert.AreEqual(2, values.Count);
            Assert.IsTrue(values.Contains(Value.First));
            Assert.IsTrue(values.Contains(Value.Fourth));
        }

        [TestFrameworkV1.TestMethod]
        public void GetDisplayNameShouldReturnDisplayNameWithEnumValue()
        {
            var attribute = new EnumDataSourceAttribute(typeof(Value));

            var result = attribute.GetDisplayName(this.methodInfo, new object[] { Value.First });

            Assert.AreEqual($"{nameof(this.TestMethod1)} ({Value.First.ToString()})", result);
        }

        /// <summary>
        /// The test method 1.
        /// </summary>
        [FrameworkV2::Microsoft.VisualStudio.TestTools.UnitTesting.TestMethodAttribute]
        [EnumDataSource(typeof(Value))]
        public void TestMethod1()
        {
        }

        private enum Value
        {
            First,
            Second,
            Third,
            Fourth
        }
    }
}
