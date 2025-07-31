// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.VisualStudio.TestPlatform.ObjectModel;

using TestFramework.ForTestingMSTest;

namespace MSTest.PlatformServices.Extensions.UnitTests;

public class TestCaseExtensionsTests : TestContainer
{
    public void ToUnitTestElementShouldReturnUnitTestElementWithFieldsSet()
    {
        TestCase testCase = new("DummyClassName.DummyMethod", new("DummyUri", UriKind.Relative), Assembly.GetCallingAssembly().FullName!)
        {
            DisplayName = "DummyDisplayName",
        };
        string[] testCategories = ["DummyCategory"];

        testCase.SetPropertyValue(EngineConstants.PriorityProperty, 2);
        testCase.SetPropertyValue(EngineConstants.TestCategoryProperty, testCategories);
        testCase.SetPropertyValue(EngineConstants.TestClassNameProperty, "DummyClassName");

        var resultUnitTestElement = testCase.ToUnitTestElement(testCase.Source);

        Verify(resultUnitTestElement.Priority == 2);
        Verify(testCategories == resultUnitTestElement.TestCategory);
        Verify(resultUnitTestElement.DisplayName == "DummyDisplayName");
        Verify(resultUnitTestElement.TestMethod.Name == "DummyMethod");
        Verify(resultUnitTestElement.TestMethod.FullClassName == "DummyClassName");
        Verify(resultUnitTestElement.TestMethod.DeclaringClassFullName is null);
    }

    public void ToUnitTestElementForTestCaseWithNoPropertiesShouldReturnUnitTestElementWithDefaultFields()
    {
        TestCase testCase = new("DummyClass.DummyMethod", new("DummyUri", UriKind.Relative), Assembly.GetCallingAssembly().FullName!);
        testCase.SetPropertyValue(EngineConstants.TestClassNameProperty, "DummyClassName");

        var resultUnitTestElement = testCase.ToUnitTestElement(testCase.Source);

        // These are set for testCase by default by ObjectModel.
        Verify(resultUnitTestElement.Priority == 0);
        Verify(resultUnitTestElement.TestCategory is null);
    }

    public void ToUnitTestElementShouldAddDeclaringClassNameToTestElementWhenAvailable()
    {
        TestCase testCase = new("DummyClass.DummyMethod", new("DummyUri", UriKind.Relative), Assembly.GetCallingAssembly().FullName!);
        testCase.SetPropertyValue(EngineConstants.TestClassNameProperty, "DummyClassName");
        testCase.SetPropertyValue(EngineConstants.DeclaringClassNameProperty, "DummyDeclaringClassName");

        var resultUnitTestElement = testCase.ToUnitTestElement(testCase.Source);

        Verify(resultUnitTestElement.TestMethod.FullClassName == "DummyClassName");
        Verify(resultUnitTestElement.TestMethod.DeclaringClassFullName == "DummyDeclaringClassName");
    }
}
