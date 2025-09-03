// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using AwesomeAssertions;

using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices;
using Microsoft.VisualStudio.TestPlatform.ObjectModel;

using Polyfills;

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
        Action action = () => _ = new UnitTestElement(null!);
        action.Should().Throw<ArgumentNullException>();
    }

    #endregion

    #region ToTestCase tests

    public void ToTestCaseShouldSetFullyQualifiedName()
    {
        var testCase = _unitTestElement.ToTestCase();

        testCase.FullyQualifiedName.Should().Be("C.M");
    }

    public void ToTestCaseShouldSetExecutorUri()
    {
        var testCase = _unitTestElement.ToTestCase();

        testCase.ExecutorUri.Should().Be(EngineConstants.ExecutorUri);
    }

    public void ToTestCaseShouldSetAssemblyName()
    {
        var testCase = _unitTestElement.ToTestCase();

        testCase.Source.Should().Be("A");
    }

    public void ToTestCaseShouldSetDisplayName()
    {
        var testCase = _unitTestElement.ToTestCase();

        testCase.DisplayName.Should().Be("M");
    }

    public void ToTestCaseShouldSetDisplayNameIfPresent()
    {
        _unitTestElement.DisplayName = "Display Name";
        var testCase = _unitTestElement.ToTestCase();

        testCase.DisplayName.Should().Be("Display Name");
    }

    public void ToTestCaseShouldSetTestClassNameProperty()
    {
        var testCase = _unitTestElement.ToTestCase();

        (testCase.GetPropertyValue(EngineConstants.TestClassNameProperty) as string).Should().Be("C");
    }

    public void ToTestCaseShouldSetDeclaringClassNameIfPresent()
    {
        _testMethod.DeclaringClassFullName = null;
        var testCase = _unitTestElement.ToTestCase();

        testCase.GetPropertyValue(EngineConstants.DeclaringClassNameProperty).Should().BeNull();

        _testMethod.DeclaringClassFullName = "DC";
        testCase = _unitTestElement.ToTestCase();

        (testCase.GetPropertyValue(EngineConstants.DeclaringClassNameProperty) as string).Should().Be("DC");
    }

    public void ToTestCaseShouldSetTestCategoryIfPresent()
    {
        _unitTestElement.TestCategory = null;
        var testCase = _unitTestElement.ToTestCase();

        testCase.GetPropertyValue(EngineConstants.TestCategoryProperty).Should().BeNull();

        _unitTestElement.TestCategory = [];
        testCase = _unitTestElement.ToTestCase();

        testCase.GetPropertyValue(EngineConstants.TestCategoryProperty).Should().BeNull();

        _unitTestElement.TestCategory = ["TC"];
        testCase = _unitTestElement.ToTestCase();

        new string[] { "TC" }.SequenceEqual((string[])testCase.GetPropertyValue(EngineConstants.TestCategoryProperty)!).Should().BeTrue();
    }

    public void ToTestCaseShouldSetPriorityIfPresent()
    {
        _unitTestElement.Priority = null;
        var testCase = _unitTestElement.ToTestCase();

        ((int)testCase.GetPropertyValue(EngineConstants.PriorityProperty)!).Should().Be(0);

        _unitTestElement.Priority = 1;
        testCase = _unitTestElement.ToTestCase();

        ((int)testCase.GetPropertyValue(EngineConstants.PriorityProperty)!).Should().Be(1);
    }

    public void ToTestCaseShouldSetTraitsIfPresent()
    {
        _unitTestElement.Traits = null;
        var testCase = _unitTestElement.ToTestCase();

#pragma warning disable CA1827 // Do not use Count() or LongCount() when Any() can be used
        testCase.Traits.Count().Should().Be(0);
#pragma warning restore CA1827 // Do not use Count() or LongCount() when Any() can be used

        var trait = new Trait("trait", "value");
        _unitTestElement.Traits = [trait];
        testCase = _unitTestElement.ToTestCase();

        testCase.Traits.Count().Should().Be(1);
        testCase.Traits.ToArray()[0].Name.Should().Be("trait");
        testCase.Traits.ToArray()[0].Value.Should().Be("value");
    }

    public void ToTestCaseShouldSetPropertiesIfPresent()
    {
        _unitTestElement.CssIteration = "12";
        _unitTestElement.CssProjectStructure = "ProjectStructure";
        _unitTestElement.WorkItemIds = ["2312", "22332"];

        var testCase = _unitTestElement.ToTestCase();

        (testCase.GetPropertyValue(EngineConstants.CssIterationProperty) as string).Should().Be("12");
        (testCase.GetPropertyValue(EngineConstants.CssProjectStructureProperty) as string).Should().Be("ProjectStructure");
        new string[] { "2312", "22332" }.SequenceEqual((string[])testCase.GetPropertyValue(EngineConstants.WorkItemIdsProperty)!).Should().BeTrue();
    }

    public void ToTestCaseShouldSetDeploymentItemPropertyIfPresent()
    {
        _unitTestElement.DeploymentItems = null;
        var testCase = _unitTestElement.ToTestCase();

        testCase.GetPropertyValue(EngineConstants.DeploymentItemsProperty).Should().BeNull();

        _unitTestElement.DeploymentItems = [];
        testCase = _unitTestElement.ToTestCase();

        testCase.GetPropertyValue(EngineConstants.DeploymentItemsProperty).Should().BeNull();

        _unitTestElement.DeploymentItems = [new("s", "d")];
        testCase = _unitTestElement.ToTestCase();

        _unitTestElement.DeploymentItems.SequenceEqual(testCase.GetPropertyValue(EngineConstants.DeploymentItemsProperty) as KeyValuePair<string, string>[]).Should().BeTrue();
    }

    [Obsolete("Remove test case when enum entry is removed")]
    public void ToTestCase_WhenStrategyIsLegacy_UsesDefaultTestCaseId()
    {
#pragma warning disable CA2263 // Prefer generic overload when type is known
        foreach (DynamicDataType dataType in Enum.GetValues<DynamicDataType>())
        {
            var testCase = new UnitTestElement(new("MyMethod", "MyProduct.MyNamespace.MyClass", "MyAssembly", null, TestIdGenerationStrategy.Legacy) { DataType = dataType }).ToTestCase();
            var expectedTestCase = new TestCase(testCase.FullyQualifiedName, testCase.ExecutorUri, testCase.Source);
            testCase.Id.Should().Be(expectedTestCase.Id);
            testCase.GetPropertyValue(EngineConstants.TestIdGenerationStrategyProperty)!.Equals((int)TestIdGenerationStrategy.Legacy).Should().BeTrue();
        }
#pragma warning restore CA2263 // Prefer generic overload when type is known
    }

    [Obsolete("Remove test case when enum entry is removed")]
    public void ToTestCase_WhenStrategyIsDisplayName_DoesNotUseDefaultTestCaseId()
    {
#pragma warning disable CA2263 // Prefer generic overload when type is known
        foreach (DynamicDataType dataType in Enum.GetValues<DynamicDataType>())
        {
            var testCase = new UnitTestElement(new("MyMethod", "MyProduct.MyNamespace.MyClass", "MyAssembly", null, TestIdGenerationStrategy.DisplayName) { DataType = dataType }).ToTestCase();
            var expectedTestCase = new TestCase(testCase.FullyQualifiedName, testCase.ExecutorUri, testCase.Source);
            if (dataType == DynamicDataType.None)
            {
                testCase.Id.Should().Be(expectedTestCase.Id);
            }
            else
            {
                testCase.Id.Should().NotBe(expectedTestCase.Id);
            }

            testCase.GetPropertyValue(EngineConstants.TestIdGenerationStrategyProperty)!.Equals((int)TestIdGenerationStrategy.DisplayName).Should().BeTrue();
        }
#pragma warning restore CA2263 // Prefer generic overload when type is known
    }

    public void ToTestCase_WhenStrategyIsData_DoesNotUseDefaultTestCaseId()
    {
#pragma warning disable CA2263 // Prefer generic overload when type is known
        foreach (DynamicDataType dataType in Enum.GetValues<DynamicDataType>())
        {
            var testCase = new UnitTestElement(new("MyMethod", "MyProduct.MyNamespace.MyClass", "MyAssembly", null, TestIdGenerationStrategy.FullyQualified) { DataType = dataType }).ToTestCase();
            var expectedTestCase = new TestCase(testCase.FullyQualifiedName, testCase.ExecutorUri, testCase.Source);
            testCase.Id.Should().NotBe(expectedTestCase.Id);
            testCase.GetPropertyValue(EngineConstants.TestIdGenerationStrategyProperty)!.Equals((int)TestIdGenerationStrategy.FullyQualified).Should().BeTrue();
        }
#pragma warning restore CA2263 // Prefer generic overload when type is known
    }

    [Obsolete("Remove test case when enum entry is removed")]
    public void ToTestCase_WhenStrategyIsDisplayName_ExamplesOfTestCaseIdUniqueness()
    {
        TestIdGenerationStrategy testIdStrategy = TestIdGenerationStrategy.DisplayName;
        TestCase[] testCases =
        [
            new UnitTestElement(
                new("MyMethod", "MyProduct.MyNamespace.MyClass", "MyAssembly", null, testIdStrategy))
            .ToTestCase(),
            new UnitTestElement(
                new("MyOtherMethod", "MyProduct.MyNamespace.MyClass", "MyAssembly", null, testIdStrategy))
            .ToTestCase(),
            new UnitTestElement(
                new("MyMethod", "MyOtherProduct.MyNamespace.MyClass", "MyAssembly", null, testIdStrategy))
            .ToTestCase(),
            new UnitTestElement(
                new("MyMethod", "MyProduct.MyNamespace.MyClass", "MyOtherAssembly", null, testIdStrategy))
            .ToTestCase(),
            new UnitTestElement(
                new("MyMethod", "MyProduct.MyNamespace.MyClass", "MyAssembly", null, testIdStrategy)
                {
                    DataType = DynamicDataType.ITestDataSource,
                })
            {
                DisplayName = "SomeDisplayName",
            }
            .ToTestCase(),
            new UnitTestElement(
                new("MyMethod", "MyProduct.MyNamespace.MyClass", "MyAssembly", null, testIdStrategy)
                {
                    DataType = DynamicDataType.ITestDataSource,
                })
            {
                DisplayName = "SomeOtherDisplayName",
            }
            .ToTestCase()
        ];

        testCases.Select(tc => tc.Id.ToString()).Distinct().Count().Should().Be(testCases.Length);
    }

    [Obsolete("Remove test case when enum entry is removed")]
    public void ToTestCase_WhenStrategyIsDisplayName_ExamplesOfTestCaseIdCollision()
    {
        TestIdGenerationStrategy testIdStrategy = TestIdGenerationStrategy.DisplayName;
        TestCase[] testCases =
        [
            new UnitTestElement(
                new("MyMethod", "MyProduct.MyNamespace.MyClass", "MyAssembly", null, testIdStrategy)
                {
                    DataType = DynamicDataType.None,
                })
            .ToTestCase(),
            new UnitTestElement(
                new("MyMethod", "MyProduct.MyNamespace.MyClass", "MyAssembly", null, testIdStrategy)
                {
                    DataType = DynamicDataType.None,
                    SerializedData = ["1"],
                })
            .ToTestCase(),
            new UnitTestElement(
                new("MyMethod", "MyProduct.MyNamespace.MyClass", "MyAssembly", null, testIdStrategy)
                {
                    DataType = DynamicDataType.None,
                    SerializedData = ["2"],
                })
            .ToTestCase(),
            new UnitTestElement(
                new("MyMethod", "MyProduct.MyNamespace.MyClass", "MyAssembly", null, testIdStrategy)
                {
                    DataType = DynamicDataType.ITestDataSource,
                    SerializedData = ["1"],
                })
            .ToTestCase(),
            new UnitTestElement(
                new("MyMethod", "MyProduct.MyNamespace.MyClass", "MyAssembly", null, testIdStrategy)
                {
                    DataType = DynamicDataType.ITestDataSource,
                    SerializedData = ["2"],
                })
            .ToTestCase()
        ];

        // All the test cases with DynamicDataType.None will have the same Id (showing collisions).
        // All the test cases with DynamicDataType.ITestDataSource will have the same Id, but different one (showing collisions).
        // So for the 5 test cases, we have 2 distinct Ids.
        testCases.Select(tc => tc.Id.ToString()).Distinct().Count().Should().Be(2);
    }

    public void ToTestCase_WhenStrategyIsFullyQualifiedTest_ExamplesOfTestCaseIdUniqueness()
    {
        TestIdGenerationStrategy testIdStrategy = TestIdGenerationStrategy.FullyQualified;
        TestCase[] testCases =
        [
            new UnitTestElement(
                new("MyMethod", "MyProduct.MyNamespace.MyClass", "MyAssembly", null, testIdStrategy))
            .ToTestCase(),
            new UnitTestElement(
                new("MyOtherMethod", "MyProduct.MyNamespace.MyClass", "MyAssembly", null, testIdStrategy))
            .ToTestCase(),
            new UnitTestElement(
                new("MyMethod", "MyOtherProduct.MyNamespace.MyClass", "MyAssembly", null, testIdStrategy))
            .ToTestCase(),
            new UnitTestElement(
                new("MyMethod", "MyProduct.MyNamespace.MyClass", "MyOtherAssembly", null, testIdStrategy))
            .ToTestCase(),
            new UnitTestElement(
                new("MyMethod", "MyProduct.MyNamespace.MyClass", "MyAssembly", null, testIdStrategy)
                {
                    SerializedData = ["System.Int32[], mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089", "[]"],
                })
            .ToTestCase(),
            new UnitTestElement(
                new("MyMethod", "MyProduct.MyNamespace.MyClass", "MyAssembly", null, testIdStrategy)
                {
                    SerializedData = ["System.Int32[], mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089", "[1]"],
                })
            .ToTestCase(),
            new UnitTestElement(
                new("MyMethod", "MyProduct.MyNamespace.MyClass", "MyAssembly", null, testIdStrategy)
                {
                    SerializedData = ["System.Int32[], mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089", "[1,1]"],
                })
            .ToTestCase()
        ];

        testCases.Select(tc => tc.Id.ToString()).Distinct().Count().Should().Be(testCases.Length);
    }

    #endregion
}
