// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;

using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.ObjectModel;

using TestFramework.ForTestingMSTest;

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.ObjectModel;
public class UnitTestElementTests : TestContainer
{
    private readonly TestMethod _testMethod;
    private readonly UnitTestElement _unitTestElement;

    public UnitTestElementTests()
    {
        _testMethod = new TestMethod("M", "C", "A", true);
        _unitTestElement = new UnitTestElement(_testMethod);
    }

    #region Ctor tests

    public void UnitTestElementConstructorShouldThrowIfTestMethodIsNull()
    {
        var ex = VerifyThrows(() => _ = new UnitTestElement(null));
        Verify(ex.GetType() == typeof(ArgumentNullException));
    }

    #endregion

    #region ToTestCase tests

    public void ToTestCaseShouldSetFullyQualifiedName()
    {
        var testCase = _unitTestElement.ToTestCase();

        Verify(testCase.FullyQualifiedName == "C.M");
    }

    public void ToTestCaseShouldSetExecutorUri()
    {
        var testCase = _unitTestElement.ToTestCase();

        Verify(testCase.ExecutorUri == Constants.ExecutorUri);
    }

    public void ToTestCaseShouldSetAssemblyName()
    {
        var testCase = _unitTestElement.ToTestCase();

        Verify(testCase.Source == "A");
    }

    public void ToTestCaseShouldSetDisplayName()
    {
        var testCase = _unitTestElement.ToTestCase();

        Verify(testCase.DisplayName == "M");
    }

    public void ToTestCaseShouldSetDisplayNameIfPresent()
    {
        _unitTestElement.DisplayName = "Display Name";
        var testCase = _unitTestElement.ToTestCase();

        Verify(testCase.DisplayName == "Display Name");
    }

    public void ToTestCaseShouldSetTestClassNameProperty()
    {
        var testCase = _unitTestElement.ToTestCase();

        Verify(testCase.GetPropertyValue(Constants.TestClassNameProperty) as string == "C");
    }

    public void ToTestCaseShouldSetDeclaringClassNameIfPresent()
    {
        _testMethod.DeclaringClassFullName = null;
        var testCase = _unitTestElement.ToTestCase();

        Verify(testCase.GetPropertyValue(Constants.DeclaringClassNameProperty) is null);

        _testMethod.DeclaringClassFullName = "DC";
        testCase = _unitTestElement.ToTestCase();

        Verify(testCase.GetPropertyValue(Constants.DeclaringClassNameProperty) as string == "DC");
    }

    public void ToTestCaseShouldSetIsAsyncProperty()
    {
        _unitTestElement.IsAsync = true;
        var testCase = _unitTestElement.ToTestCase();

        Verify((bool)testCase.GetPropertyValue(Constants.AsyncTestProperty) == true);

        _unitTestElement.IsAsync = false;
        testCase = _unitTestElement.ToTestCase();

        Verify((bool)testCase.GetPropertyValue(Constants.AsyncTestProperty) == false);
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

        Verify(new string[] { "TC" }.SequenceEqual((string[])testCase.GetPropertyValue(Constants.TestCategoryProperty)));
    }

    public void ToTestCaseShouldSetPriorityIfPresent()
    {
        _unitTestElement.Priority = null;
        var testCase = _unitTestElement.ToTestCase();

        Verify((int)testCase.GetPropertyValue(Constants.PriorityProperty) == 0);

        _unitTestElement.Priority = 1;
        testCase = _unitTestElement.ToTestCase();

        Verify((int)testCase.GetPropertyValue(Constants.PriorityProperty) == 1);
    }

    public void ToTestCaseShouldSetTraitsIfPresent()
    {
        _unitTestElement.Traits = null;
        var testCase = _unitTestElement.ToTestCase();

        Verify(testCase.Traits.Count() == 0);

        var trait = new TestPlatform.ObjectModel.Trait("trait", "value");
        _unitTestElement.Traits = new TestPlatform.ObjectModel.Trait[] { trait };
        testCase = _unitTestElement.ToTestCase();

        Verify(testCase.Traits.Count() == 1);
        Verify(testCase.Traits.ToArray()[0].Name == "trait");
        Verify(testCase.Traits.ToArray()[0].Value == "value");
    }

    public void ToTestCaseShouldSetPropertiesIfPresent()
    {
        _unitTestElement.CssIteration = "12";
        _unitTestElement.CssProjectStructure = "ProjectStructure";
        _unitTestElement.Description = "I am a dummy test";
        _unitTestElement.WorkItemIds = new string[] { "2312", "22332" };

        var testCase = _unitTestElement.ToTestCase();

        Verify(testCase.GetPropertyValue(Constants.CssIterationProperty) as string == "12");
        Verify(testCase.GetPropertyValue(Constants.CssProjectStructureProperty) as string == "ProjectStructure");
        Verify(testCase.GetPropertyValue(Constants.DescriptionProperty) as string == "I am a dummy test");
        Verify(new string[] { "2312", "22332" }.SequenceEqual((string[])testCase.GetPropertyValue(Constants.WorkItemIdsProperty)));
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

        Verify(_unitTestElement.DeploymentItems.SequenceEqual(testCase.GetPropertyValue(Constants.DeploymentItemsProperty) as KeyValuePair<string, string>[]));
    }

    #endregion
}
