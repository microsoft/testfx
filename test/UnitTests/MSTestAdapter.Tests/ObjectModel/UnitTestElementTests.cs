// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.ObjectModel
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    using Microsoft.VisualStudio.TestPlatform.UnitTestFramework;

    using MSTest.TestAdapter;
    using MSTest.TestAdapter.ObjectModel;

    [TestClass]
    public class UnitTestElementTests
    {
        private TestMethod testMethod;
        private UnitTestElement unitTestElement;

        [TestInitialize]
        public void TestInit()
        {
            this.testMethod = new TestMethod("M", "C", "A", true);
            this.unitTestElement = new UnitTestElement(testMethod);
        }

        #region Ctor tests

        [TestMethod]
        public void UnitTestElementConstructorShouldThrowIfTestMethodIsNull()
        {
            Assert.ThrowsException<ArgumentNullException>(() => new UnitTestElement(null));
        }

        #endregion

        #region ToTestCase tests

        [TestMethod]
        public void ToTestCaseShouldSetFullyQualifiedName()
        {
            var testCase = this.unitTestElement.ToTestCase();

            Assert.AreEqual("C.M", testCase.FullyQualifiedName);
        }

        [TestMethod]
        public void ToTestCaseShouldSetExecutorUri()
        {
            var testCase = this.unitTestElement.ToTestCase();

            Assert.AreEqual(Constants.ExecutorUri, testCase.ExecutorUri);
        }

        [TestMethod]
        public void ToTestCaseShouldSetAssemblyName()
        {
            var testCase = this.unitTestElement.ToTestCase();

            Assert.AreEqual("A", testCase.Source);
        }

        [TestMethod]
        public void ToTestCaseShouldSetDisplayName()
        {
            var testCase = this.unitTestElement.ToTestCase();

            Assert.AreEqual("M", testCase.DisplayName);
        }

        [TestMethod]
        public void ToTestCaseShouldSetTestEnabledProperty()
        {
            this.unitTestElement.Ignored = false;
            var testCase = this.unitTestElement.ToTestCase();

            Assert.AreEqual(true, testCase.GetPropertyValue(Constants.TestEnabledProperty));

            this.unitTestElement.Ignored = true;
            testCase = this.unitTestElement.ToTestCase();

            Assert.AreEqual(false, testCase.GetPropertyValue(Constants.TestEnabledProperty));
        }

        [TestMethod]
        public void ToTestCaseShouldSetTestClassNameProperty()
        {
            var testCase = this.unitTestElement.ToTestCase();

            Assert.AreEqual("C", testCase.GetPropertyValue(Constants.TestClassNameProperty));
        }

        [TestMethod]
        public void ToTestCaseShouldSetIsAsyncProperty()
        {
            this.unitTestElement.IsAsync = true;
            var testCase = this.unitTestElement.ToTestCase();

            Assert.AreEqual(true, testCase.GetPropertyValue(Constants.AsyncTestProperty));

            this.unitTestElement.IsAsync = false;
            testCase = this.unitTestElement.ToTestCase();

            Assert.AreEqual(false, testCase.GetPropertyValue(Constants.AsyncTestProperty));
        }

        [TestMethod]
        public void ToTestCaseShouldSetTestCategoryIfPresent()
        {
            this.unitTestElement.TestCategory = null;
            var testCase = this.unitTestElement.ToTestCase();

            Assert.IsNull(testCase.GetPropertyValue(Constants.TestCategoryProperty));

            this.unitTestElement.TestCategory = new string[] { };
            testCase = this.unitTestElement.ToTestCase();

            Assert.IsNull(testCase.GetPropertyValue(Constants.TestCategoryProperty));

            this.unitTestElement.TestCategory = new string[] { "TC" };
            testCase = this.unitTestElement.ToTestCase();

            CollectionAssert.AreEqual(new string[] { "TC" }, (testCase.GetPropertyValue(Constants.TestCategoryProperty) as string[]));
        }

        [TestMethod]
        public void ToTestCaseShouldSetPriorityIfPresent()
        {
            this.unitTestElement.Priority = null;
            var testCase = this.unitTestElement.ToTestCase();

            Assert.AreEqual(0, testCase.GetPropertyValue(Constants.PriorityProperty));

            this.unitTestElement.Priority = 1;
            testCase = this.unitTestElement.ToTestCase();

            Assert.AreEqual(1, testCase.GetPropertyValue(Constants.PriorityProperty));
        }

        [TestMethod]
        public void ToTestCaseShouldSetTraitsIfPresent()
        {
            this.unitTestElement.Traits = null;
            var testCase = this.unitTestElement.ToTestCase();

            Assert.AreEqual(0, testCase.Traits.Count());

            var trait = new TestPlatform.ObjectModel.Trait("trait", "value");
            this.unitTestElement.Traits = new TestPlatform.ObjectModel.Trait[] { trait };
            testCase = this.unitTestElement.ToTestCase();
            
            Assert.AreEqual(1, testCase.Traits.Count());
            Assert.AreEqual("trait", testCase.Traits.ToArray()[0].Name);
            Assert.AreEqual("value", testCase.Traits.ToArray()[0].Value);
        }

        [TestMethod]
        public void ToTestCaseShouldSetDeploymentItemPropertyIfPresent()
        {
            this.unitTestElement.DeploymentItems = null;
            var testCase = this.unitTestElement.ToTestCase();

            Assert.IsNull(testCase.GetPropertyValue(Constants.DeploymentItemsProperty));

            this.unitTestElement.DeploymentItems = new KeyValuePair<string, string>[] { };
            testCase = this.unitTestElement.ToTestCase();

            Assert.IsNull(testCase.GetPropertyValue(Constants.DeploymentItemsProperty));

            this.unitTestElement.DeploymentItems = new KeyValuePair<string, string>[] { new KeyValuePair<string, string>("s" ,"d") };
            testCase = this.unitTestElement.ToTestCase();

            CollectionAssert.AreEqual(this.unitTestElement.DeploymentItems, (testCase.GetPropertyValue(Constants.DeploymentItemsProperty) as KeyValuePair<string,string>[]));
        }

        #endregion
    }
}
