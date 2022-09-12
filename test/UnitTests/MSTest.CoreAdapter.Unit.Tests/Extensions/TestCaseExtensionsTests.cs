// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.Extensions;

using System;
using System.Reflection;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Extensions;
using Microsoft.VisualStudio.TestPlatform.ObjectModel;

using TestFramework.ForTestingMSTest;

using Constants = Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Constants;

public class TestCaseExtensionsTests : TestContainer
{
    public void ToUnitTestElementShouldReturnUnitTestElementWithFieldsSet()
    {
        TestCase testCase = new("DummyClassName.DummyMethod", new("DummyUri", UriKind.Relative), Assembly.GetCallingAssembly().FullName)
        {
            DisplayName = "DummyDisplayName"
        };
        var testCategories = new[] { "DummyCategory" };

        testCase.SetPropertyValue(Constants.AsyncTestProperty, true);
        testCase.SetPropertyValue(Constants.PriorityProperty, 2);
        testCase.SetPropertyValue(Constants.TestCategoryProperty, testCategories);
        testCase.SetPropertyValue(Constants.TestClassNameProperty, "DummyClassName");

        var resultUnitTestElement = testCase.ToUnitTestElement(testCase.Source);

        Verify(resultUnitTestElement.IsAsync);
        Assert.AreEqual(2, resultUnitTestElement.Priority);
        Assert.AreEqual(testCategories, resultUnitTestElement.TestCategory);
        Assert.AreEqual("DummyDisplayName", resultUnitTestElement.DisplayName);
        Assert.AreEqual("DummyMethod", resultUnitTestElement.TestMethod.Name);
        Assert.AreEqual("DummyClassName", resultUnitTestElement.TestMethod.FullClassName);
        Verify(resultUnitTestElement.TestMethod.IsAsync);
        Verify(resultUnitTestElement.TestMethod.DeclaringClassFullName is null);
    }

    public void ToUnitTestElementForTestCaseWithNoPropertiesShouldReturnUnitTestElementWithDefaultFields()
    {
        TestCase testCase = new("DummyClass.DummyMethod", new("DummyUri", UriKind.Relative), Assembly.GetCallingAssembly().FullName);
        testCase.SetPropertyValue(Constants.TestClassNameProperty, "DummyClassName");

        var resultUnitTestElement = testCase.ToUnitTestElement(testCase.Source);

        // These are set for testCase by default by ObjectModel.
        Verify(!resultUnitTestElement.IsAsync);
        Assert.AreEqual(0, resultUnitTestElement.Priority);
        Verify(resultUnitTestElement.TestCategory is null);
    }

    public void ToUnitTestElementShouldAddDeclaringClassNameToTestElementWhenAvailable()
    {
        TestCase testCase = new("DummyClass.DummyMethod", new("DummyUri", UriKind.Relative), Assembly.GetCallingAssembly().FullName);
        testCase.SetPropertyValue(Constants.TestClassNameProperty, "DummyClassName");
        testCase.SetPropertyValue(Constants.DeclaringClassNameProperty, "DummyDeclaringClassName");

        var resultUnitTestElement = testCase.ToUnitTestElement(testCase.Source);

        Assert.AreEqual("DummyClassName", resultUnitTestElement.TestMethod.FullClassName);
        Assert.AreEqual("DummyDeclaringClassName", resultUnitTestElement.TestMethod.DeclaringClassFullName);
    }
}
