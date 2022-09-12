// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.ObjectModel;

using System;
using System.Collections.Generic;
using System.Linq;
using global::MSTestAdapter.TestUtilities;
using MSTest.TestAdapter;
using MSTest.TestAdapter.ObjectModel;

using TestFramework.ForTestingMSTest;

public class UnitTestElementTests : TestContainer
{
    private TestMethod _testMethod;
    private UnitTestElement _unitTestElement;

    [TestInitialize]
    public void TestInit()
    {
        _testMethod = new TestMethod("M", "C", "A", true);
        _unitTestElement = new UnitTestElement(_testMethod);
    }

    #region Ctor tests

    public void UnitTestElementConstructorShouldThrowIfTestMethodIsNull()
    {
        ActionUtility.ActionShouldThrowExceptionOfType(
            () => _ = new UnitTestElement(null),
            typeof(ArgumentNullException));
    }

    #endregion

    #region ToTestCase tests

    public void ToTestCaseShouldSetFullyQualifiedName()
    {
        var testCase = _unitTestElement.ToTestCase();

        Assert.AreEqual("C.M", testCase.FullyQualifiedName);
    }

    public void ToTestCaseShouldSetExecutorUri()
    {
        var testCase = _unitTestElement.ToTestCase();

        Assert.AreEqual(Constants.ExecutorUri, testCase.ExecutorUri);
    }

    public void ToTestCaseShouldSetAssemblyName()
    {
        var testCase = _unitTestElement.ToTestCase();

        Assert.AreEqual("A", testCase.Source);
    }

    public void ToTestCaseShouldSetDisplayName()
    {
        var testCase = _unitTestElement.ToTestCase();

        Assert.AreEqual("M", testCase.DisplayName);
    }

    public void ToTestCaseShouldSetDisplayNameIfPresent()
    {
        _unitTestElement.DisplayName = "Display Name";
        var testCase = _unitTestElement.ToTestCase();

        Assert.AreEqual("Display Name", testCase.DisplayName);
    }

    public void ToTestCaseShouldSetTestClassNameProperty()
    {
        var testCase = _unitTestElement.ToTestCase();

        Assert.AreEqual("C", testCase.GetPropertyValue(Constants.TestClassNameProperty));
    }

    public void ToTestCaseShouldSetDeclaringClassNameIfPresent()
    {
        _testMethod.DeclaringClassFullName = null;
        var testCase = _unitTestElement.ToTestCase();

        Verify(testCase.GetPropertyValue(Constants.DeclaringClassNameProperty) is null);

        _testMethod.DeclaringClassFullName = "DC";
        testCase = _unitTestElement.ToTestCase();

        Assert.AreEqual("DC", testCase.GetPropertyValue(Constants.DeclaringClassNameProperty));
    }

    public void ToTestCaseShouldSetIsAsyncProperty()
    {
        _unitTestElement.IsAsync = true;
        var testCase = _unitTestElement.ToTestCase();

        Assert.AreEqual(true, testCase.GetPropertyValue(Constants.AsyncTestProperty));

        _unitTestElement.IsAsync = false;
        testCase = _unitTestElement.ToTestCase();

        Assert.AreEqual(false, testCase.GetPropertyValue(Constants.AsyncTestProperty));
    }

    public void ToTestCaseShouldSetTestCategoryIfPresent()
    {
        _unitTestElement.TestCategory = null;
        var testCase = _unitTestElement.ToTestCase();

        Verify(testCase.GetPropertyValue(Constants.TestCategoryProperty) is null);

        _unitTestElement.TestCategory = Array.Empty<string>();
        testCase = _unitTestElement.ToTestCase();

        Verify(testCase.GetPropertyValue(Constants.TestCategoryProperty) is null);

        _unitTestElement.TestCategory = new string[] { "TC" };
        testCase = _unitTestElement.ToTestCase();

        CollectionAssert.AreEqual(new string[] { "TC" }, testCase.GetPropertyValue(Constants.TestCategoryProperty) as string[]);
    }

    public void ToTestCaseShouldSetPriorityIfPresent()
    {
        _unitTestElement.Priority = null;
        var testCase = _unitTestElement.ToTestCase();

        Assert.AreEqual(0, testCase.GetPropertyValue(Constants.PriorityProperty));

        _unitTestElement.Priority = 1;
        testCase = _unitTestElement.ToTestCase();

        Assert.AreEqual(1, testCase.GetPropertyValue(Constants.PriorityProperty));
    }

    public void ToTestCaseShouldSetTraitsIfPresent()
    {
        _unitTestElement.Traits = null;
        var testCase = _unitTestElement.ToTestCase();

        Assert.AreEqual(0, testCase.Traits.Count());

        var trait = new TestPlatform.ObjectModel.Trait("trait", "value");
        _unitTestElement.Traits = new TestPlatform.ObjectModel.Trait[] { trait };
        testCase = _unitTestElement.ToTestCase();

        Assert.AreEqual(1, testCase.Traits.Count());
        Assert.AreEqual("trait", testCase.Traits.ToArray()[0].Name);
        Assert.AreEqual("value", testCase.Traits.ToArray()[0].Value);
    }

    public void ToTestCaseShouldSetPropertiesIfPresent()
    {
        _unitTestElement.CssIteration = "12";
        _unitTestElement.CssProjectStructure = "ProjectStructure";
        _unitTestElement.Description = "I am a dummy test";
        _unitTestElement.WorkItemIds = new string[] { "2312", "22332" };

        var testCase = _unitTestElement.ToTestCase();

        Assert.AreEqual("12", testCase.GetPropertyValue(Constants.CssIterationProperty));
        Assert.AreEqual("ProjectStructure", testCase.GetPropertyValue(Constants.CssProjectStructureProperty));
        Assert.AreEqual("I am a dummy test", testCase.GetPropertyValue(Constants.DescriptionProperty));
        CollectionAssert.AreEqual(new string[] { "2312", "22332" }, testCase.GetPropertyValue(Constants.WorkItemIdsProperty) as string[]);
    }

    public void ToTestCaseShouldSetDeploymentItemPropertyIfPresent()
    {
        _unitTestElement.DeploymentItems = null;
        var testCase = _unitTestElement.ToTestCase();

        Verify(testCase.GetPropertyValue(Constants.DeploymentItemsProperty) is null);

        _unitTestElement.DeploymentItems = Array.Empty<KeyValuePair<string, string>>();
        testCase = _unitTestElement.ToTestCase();

        Verify(testCase.GetPropertyValue(Constants.DeploymentItemsProperty) is null);

        _unitTestElement.DeploymentItems = new KeyValuePair<string, string>[] { new KeyValuePair<string, string>("s", "d") };
        testCase = _unitTestElement.ToTestCase();

        CollectionAssert.AreEqual(_unitTestElement.DeploymentItems, testCase.GetPropertyValue(Constants.DeploymentItemsProperty) as KeyValuePair<string, string>[]);
    }

    #endregion
}
