// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using AwesomeAssertions;

using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Extensions;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.ObjectModel;
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

        testCase.SetPropertyValue(AdapterTestProperties.PriorityProperty, 2);
        testCase.SetPropertyValue(AdapterTestProperties.TestCategoryProperty, testCategories);
        testCase.SetPropertyValue(AdapterTestProperties.TestClassNameProperty, "DummyClassName");

        UnitTestElement resultUnitTestElement = testCase.ToUnitTestElementWithUpdatedSource(testCase.Source);

        resultUnitTestElement.Priority.Should().Be(2);
        resultUnitTestElement.TestCategory.Should().Equal(testCategories);
        resultUnitTestElement.TestMethod.Name.Should().Be("DummyMethod");
        resultUnitTestElement.TestMethod.FullClassName.Should().Be("DummyClassName");
    }

    public void ToUnitTestElementForTestCaseWithNoPropertiesShouldReturnUnitTestElementWithDefaultFields()
    {
        TestCase testCase = new("DummyClass.DummyMethod", new("DummyUri", UriKind.Relative), Assembly.GetCallingAssembly().FullName!);
        testCase.SetPropertyValue(AdapterTestProperties.TestClassNameProperty, "DummyClassName");

        UnitTestElement resultUnitTestElement = testCase.ToUnitTestElementWithUpdatedSource(testCase.Source);

        // These are set for testCase by default by ObjectModel.
        resultUnitTestElement.Priority.Should().Be(0);
        resultUnitTestElement.TestCategory.Should().BeNull();
    }

    public void ToUnitTestElementShouldAddDeclaringClassNameToTestElementWhenAvailable()
    {
        TestCase testCase = new("DummyClass.DummyMethod", new("DummyUri", UriKind.Relative), Assembly.GetCallingAssembly().FullName!);
        testCase.SetPropertyValue(AdapterTestProperties.TestClassNameProperty, "DummyClassName");

        UnitTestElement resultUnitTestElement = testCase.ToUnitTestElementWithUpdatedSource(testCase.Source);

        resultUnitTestElement.TestMethod.FullClassName.Should().Be("DummyClassName");
    }

    public void ToUnitTestElementShouldPreferManagedTypeOverTestClassNameWhenAvailable()
    {
        TestCase testCase = new("SemanticClassName.DummyMethod", new("DummyUri", UriKind.Relative), Assembly.GetCallingAssembly().FullName!);
        testCase.SetPropertyValue(AdapterTestProperties.TestClassNameProperty, "SyntacticClassName");
        testCase.SetPropertyValue(TestCaseExtensions.ManagedTypeProperty, "SemanticClassName");
        testCase.SetPropertyValue(TestCaseExtensions.ManagedMethodProperty, "DummyMethod");

        UnitTestElement resultUnitTestElement = testCase.ToUnitTestElementWithUpdatedSource(testCase.Source);

        resultUnitTestElement.TestMethod.FullClassName.Should().Be("SemanticClassName");
        resultUnitTestElement.TestMethod.ManagedTypeName.Should().Be("SemanticClassName");
    }

    public void ToUnitTestElementShouldParseLegacyClosedGenericFullyQualifiedNameWhenManagedTypeIsOpenGeneric()
    {
        Type closedType = typeof(DummyGenericTestClass<int>);
        string methodName = nameof(DummyGenericTestClass<>.GenericTestMethod);
        TestCase testCase = new($"{closedType.FullName}.{methodName}", new("DummyUri", UriKind.Relative), Assembly.GetCallingAssembly().FullName!);
        testCase.SetPropertyValue(AdapterTestProperties.TestClassNameProperty, closedType.FullName);
        testCase.SetPropertyValue(TestCaseExtensions.ManagedTypeProperty, typeof(DummyGenericTestClass<>).FullName);
        testCase.SetPropertyValue(TestCaseExtensions.ManagedMethodProperty, methodName);

        UnitTestElement resultUnitTestElement = testCase.ToUnitTestElementWithUpdatedSource(testCase.Source);

        resultUnitTestElement.TestMethod.Name.Should().Be(methodName);
        resultUnitTestElement.TestMethod.FullClassName.Should().Be(closedType.FullName);
        resultUnitTestElement.TestMethod.ManagedTypeName.Should().Be(typeof(DummyGenericTestClass<>).FullName);
    }

    private class DummyGenericTestClass<T>
    {
        public void GenericTestMethod()
        {
        }
    }
}
