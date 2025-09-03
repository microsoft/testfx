// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using AwesomeAssertions;

using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Extensions;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices;
using Microsoft.VisualStudio.TestPlatform.ObjectModel;

using TestFramework.ForTestingMSTest;

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.Extensions;

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

        resultUnitTestElement.Priority.Should().Be(2);
        resultUnitTestElement.TestCategory.Should().Equal(testCategories);
        resultUnitTestElement.DisplayName.Should().Be("DummyDisplayName");
        resultUnitTestElement.TestMethod.Name.Should().Be("DummyMethod");
        resultUnitTestElement.TestMethod.FullClassName.Should().Be("DummyClassName");
        resultUnitTestElement.TestMethod.DeclaringClassFullName.Should().BeNull();
    }

    public void ToUnitTestElementForTestCaseWithNoPropertiesShouldReturnUnitTestElementWithDefaultFields()
    {
        TestCase testCase = new("DummyClass.DummyMethod", new("DummyUri", UriKind.Relative), Assembly.GetCallingAssembly().FullName!);
        testCase.SetPropertyValue(EngineConstants.TestClassNameProperty, "DummyClassName");

        var resultUnitTestElement = testCase.ToUnitTestElement(testCase.Source);

        // These are set for testCase by default by ObjectModel.
        resultUnitTestElement.Priority.Should().Be(0);
        resultUnitTestElement.TestCategory.Should().BeNull();
    }

    public void ToUnitTestElementShouldAddDeclaringClassNameToTestElementWhenAvailable()
    {
        TestCase testCase = new("DummyClass.DummyMethod", new("DummyUri", UriKind.Relative), Assembly.GetCallingAssembly().FullName!);
        testCase.SetPropertyValue(EngineConstants.TestClassNameProperty, "DummyClassName");
        testCase.SetPropertyValue(EngineConstants.DeclaringClassNameProperty, "DummyDeclaringClassName");

        var resultUnitTestElement = testCase.ToUnitTestElement(testCase.Source);

        resultUnitTestElement.TestMethod.FullClassName.Should().Be("DummyClassName");
        resultUnitTestElement.TestMethod.DeclaringClassFullName.Should().Be("DummyDeclaringClassName");
    }
}
