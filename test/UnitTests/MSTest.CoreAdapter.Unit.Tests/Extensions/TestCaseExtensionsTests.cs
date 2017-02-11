// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.Extensions
{
    extern alias FrameworkV1;

    using Assert = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.Assert;
    using TestClass = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.TestClassAttribute;
    using TestMethod = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.TestMethodAttribute;

    using System;
    using System.Reflection;
    using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Extensions;
    using Microsoft.VisualStudio.TestPlatform.ObjectModel;
    using Constants = Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Constants;

    [TestClass]
    public class TestCaseExtensionsTests
    {
        [TestMethod]
        public void ToUnitTestElementShouldReturnUnitTestElementWithFieldsSet()
        {
            TestCase testCase = new TestCase("DummyClass.DummyMethod", new Uri("DummyUri", UriKind.Relative), Assembly.GetCallingAssembly().FullName);
            testCase.DisplayName = "DummyDisplayName";
            var testCategories = new[] { "DummyCategory" };

            testCase.SetPropertyValue(Constants.AsyncTestProperty, true);
            testCase.SetPropertyValue(Constants.TestEnabledProperty, false);
            testCase.SetPropertyValue(Constants.PriorityProperty, 2);
            testCase.SetPropertyValue(Constants.TestCategoryProperty, testCategories);
            testCase.SetPropertyValue(Constants.TestClassNameProperty, "DummyClassName");

            var resultUnitTestElement = testCase.ToUnitTestElement(testCase.Source);

            Assert.AreEqual(true, resultUnitTestElement.IsAsync);
            Assert.AreEqual(true, resultUnitTestElement.Ignored);
            Assert.AreEqual(2, resultUnitTestElement.Priority);
            Assert.AreEqual(testCategories, resultUnitTestElement.TestCategory);
            Assert.AreEqual("DummyDisplayName", resultUnitTestElement.TestMethod.Name);
            Assert.AreEqual("DummyClassName", resultUnitTestElement.TestMethod.FullClassName);
            Assert.AreEqual(true, resultUnitTestElement.TestMethod.IsAsync);
        }

        [TestMethod]
        public void ToUnitTestElementForTestCaseWithNoPropertiesShouldReturnUnitTestElementWithDefaultFields()
        {
            TestCase testCase = new TestCase("DummyClass.DummyMethod", new Uri("DummyUri", UriKind.Relative), Assembly.GetCallingAssembly().FullName);
            testCase.SetPropertyValue(Constants.TestClassNameProperty, "DummyClassName");

            var resultUnitTestElement = testCase.ToUnitTestElement(testCase.Source);
            
            // These are set for testCase by default by ObjectModel.
            Assert.AreEqual(false, resultUnitTestElement.IsAsync);
            Assert.AreEqual(true, resultUnitTestElement.Ignored);
            Assert.AreEqual(0, resultUnitTestElement.Priority);
            Assert.AreEqual(null, resultUnitTestElement.TestCategory);
        }
    }
}
