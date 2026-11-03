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
    public void GetTestNameShouldRemoveTestClassNamePrefix()
    {
        TestCase testCase = new("DummyClass.DummyMethod", new("DummyUri", UriKind.Relative), Assembly.GetCallingAssembly().FullName!);

        string testName = testCase.GetTestName("DummyClass");

        testName.Should().Be("DummyMethod");
    }

    public void GetTestNameShouldNotRemovePartialTestClassNamePrefix()
    {
        TestCase testCase = new("DummyClass.DummyMethod", new("DummyUri", UriKind.Relative), Assembly.GetCallingAssembly().FullName!);

        string testName = testCase.GetTestName("Dummy");

        testName.Should().Be("DummyClass.DummyMethod");
    }

    public void GetTestNameShouldHandleFullyQualifiedNameEqualToTestClassName()
    {
        TestCase testCase = new("DummyClass", new("DummyUri", UriKind.Relative), Assembly.GetCallingAssembly().FullName!);

        string testName = testCase.GetTestName("DummyClass");

        testName.Should().Be("DummyClass");
    }

    public void GetTestNameShouldPreserveNullTestClassNameBehavior()
    {
        TestCase testCase = new(".DummyMethod", new("DummyUri", UriKind.Relative), Assembly.GetCallingAssembly().FullName!);

        string testName = testCase.GetTestName(null);

        testName.Should().Be("DummyMethod");
    }

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

    public void HostCaseRichMetadataIsDecodedWithoutDroppingBehavioralFlags()
    {
        var testCase = new TestCase("Namespace.Type.Method", EngineConstants.ExecutorUri, "original.dll")
        {
            DisplayName = "row label",
        };
        testCase.SetPropertyValue(AdapterTestProperties.TestClassNameProperty, "Namespace.Type");
        testCase.SetPropertyValue(TestCaseExtensions.ManagedTypeProperty, "Namespace.Type");
        testCase.SetPropertyValue(TestCaseExtensions.ManagedMethodProperty, "Method");
        testCase.SetPropertyValue(AdapterTestProperties.TestDynamicDataTypeProperty, (int)DynamicDataType.ITestDataSource);
        testCase.SetPropertyValue(AdapterTestProperties.TestDynamicDataProperty, new[] { "type", "payload" });
        testCase.SetPropertyValue(AdapterTestProperties.TestCaseIndexProperty, 3);
        testCase.SetPropertyValue(AdapterTestProperties.TestDataSourceIgnoreMessageProperty, "row ignored");
        testCase.SetPropertyValue(AdapterTestProperties.WorkItemIdsProperty, new[] { "7", "11" });
        testCase.SetPropertyValue(AdapterTestProperties.DoNotParallelizeProperty, true);
        testCase.SetPropertyValue(AdapterTestProperties.ResourceLocksProperty, new[] { "Rshared", "Wexclusive" });
        testCase.SetPropertyValue(AdapterTestProperties.DependenciesProperty, new[] { "PNamespace.Other\nBefore", "S\nLocal" });
#if !WIN_UI && !WINDOWS_UWP
        testCase.SetPropertyValue(AdapterTestProperties.DeploymentItemsProperty, new[] { new KeyValuePair<string, string>("input.txt", "data") });
#endif
        testCase.Traits.Add(new Trait("Topic", "api"));
        testCase.Traits.Add(new Trait("Topic", "boundary"));

        UnitTestElement result = testCase.ToUnitTestElementWithUpdatedSource("deployed.dll");

        result.TestMethod.AssemblyName.Should().Be("deployed.dll");
        result.TestMethod.Name.Should().Be("Method");
        result.TestMethod.DataType.Should().Be(DynamicDataType.ITestDataSource);
        result.TestMethod.SerializedData.Should().Equal("type", "payload");
        result.TestMethod.TestCaseIndex.Should().Be(3);
        result.TestMethod.TestDataSourceIgnoreMessage.Should().Be("row ignored");
        result.WorkItemIds.Should().Equal("7", "11");
        result.DoNotParallelize.Should().BeTrue();
        result.Traits!.Select(trait => (trait.Name, trait.Value)).Should().Equal(("Topic", "api"), ("Topic", "boundary"));
        result.ResourceLocks!.Select(info => (info.Resource, info.Mode)).Should().Equal(
            ("shared", Microsoft.VisualStudio.TestTools.UnitTesting.ResourceAccessMode.Read),
            ("exclusive", Microsoft.VisualStudio.TestTools.UnitTesting.ResourceAccessMode.ReadWrite));
        result.Dependencies!.Select(info => (info.TargetClassFullName, info.TargetMethodName, info.ProceedOnFailure))
            .Should().Equal(("Namespace.Other", "Before", true), (null, "Local", false));
#if !WIN_UI && !WINDOWS_UWP
        result.DeploymentItems.Should().Equal(new[] { new KeyValuePair<string, string>("input.txt", "data") });
#endif
    }
}
