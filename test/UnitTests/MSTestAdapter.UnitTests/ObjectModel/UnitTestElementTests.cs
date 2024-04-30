// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.ObjectModel;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using TestFramework.ForTestingMSTest;

using Constants = Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Constants;

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
        Exception ex = VerifyThrows(() => _ = new UnitTestElement(null));
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

        Verify((testCase.GetPropertyValue(Constants.TestClassNameProperty) as string) == "C");
    }

    public void ToTestCaseShouldSetDeclaringClassNameIfPresent()
    {
        _testMethod.DeclaringClassFullName = null;
        var testCase = _unitTestElement.ToTestCase();

        Verify(testCase.GetPropertyValue(Constants.DeclaringClassNameProperty) is null);

        _testMethod.DeclaringClassFullName = "DC";
        testCase = _unitTestElement.ToTestCase();

        Verify((testCase.GetPropertyValue(Constants.DeclaringClassNameProperty) as string) == "DC");
    }

    public void ToTestCaseShouldSetIsAsyncProperty()
    {
        _unitTestElement.IsAsync = true;
        var testCase = _unitTestElement.ToTestCase();

        Verify((bool)testCase.GetPropertyValue(Constants.AsyncTestProperty));

        _unitTestElement.IsAsync = false;
        testCase = _unitTestElement.ToTestCase();

        Verify(!(bool)testCase.GetPropertyValue(Constants.AsyncTestProperty));
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

#pragma warning disable CA1827 // Do not use Count() or LongCount() when Any() can be used
        Verify(testCase.Traits.Count() == 0);
#pragma warning restore CA1827 // Do not use Count() or LongCount() when Any() can be used

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

        Verify((testCase.GetPropertyValue(Constants.CssIterationProperty) as string) == "12");
        Verify((testCase.GetPropertyValue(Constants.CssProjectStructureProperty) as string) == "ProjectStructure");
        Verify((testCase.GetPropertyValue(Constants.DescriptionProperty) as string) == "I am a dummy test");
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

        _unitTestElement.DeploymentItems = new KeyValuePair<string, string>[] { new("s", "d") };
        testCase = _unitTestElement.ToTestCase();

        Verify(_unitTestElement.DeploymentItems.SequenceEqual(testCase.GetPropertyValue(Constants.DeploymentItemsProperty) as KeyValuePair<string, string>[]));
    }

    [Obsolete("Remove test case when enum entry is removed")]
    public void ToTestCase_WhenStrategyIsLegacy_UsesDefaultTestCaseId()
    {
        foreach (DynamicDataType dataType in Enum.GetValues(typeof(DynamicDataType)))
        {
            var testCase = new UnitTestElement(new("MyMethod", "MyProduct.MyNamespace.MyClass", "MyAssembly", false, TestIdGenerationStrategy.Legacy) { DataType = dataType }).ToTestCase();
            var expectedTestCase = new TestCase(testCase.FullyQualifiedName, testCase.ExecutorUri, testCase.Source);
            Verify(expectedTestCase.Id == testCase.Id);
            Verify(testCase.GetPropertyValue(Constants.TestIdGenerationStrategyProperty).Equals((int)TestIdGenerationStrategy.Legacy));
        }
    }

    [Obsolete("Remove test case when enum entry is removed")]
    public void ToTestCase_WhenStrategyIsDisplayName_DoesNotUseDefaultTestCaseId()
    {
        foreach (DynamicDataType dataType in Enum.GetValues(typeof(DynamicDataType)))
        {
            var testCase = new UnitTestElement(new("MyMethod", "MyProduct.MyNamespace.MyClass", "MyAssembly", false, TestIdGenerationStrategy.DisplayName) { DataType = dataType }).ToTestCase();
            var expectedTestCase = new TestCase(testCase.FullyQualifiedName, testCase.ExecutorUri, testCase.Source);
            if (dataType == DynamicDataType.None)
            {
                Verify(expectedTestCase.Id == testCase.Id);
            }
            else
            {
                Verify(expectedTestCase.Id != testCase.Id);
            }

            Verify(testCase.GetPropertyValue(Constants.TestIdGenerationStrategyProperty).Equals((int)TestIdGenerationStrategy.DisplayName));
        }
    }

    public void ToTestCase_WhenStrategyIsData_DoesNotUseDefaultTestCaseId()
    {
        foreach (DynamicDataType dataType in Enum.GetValues(typeof(DynamicDataType)))
        {
            var testCase = new UnitTestElement(new("MyMethod", "MyProduct.MyNamespace.MyClass", "MyAssembly", false, TestIdGenerationStrategy.FullyQualified) { DataType = dataType }).ToTestCase();
            var expectedTestCase = new TestCase(testCase.FullyQualifiedName, testCase.ExecutorUri, testCase.Source);
            Verify(expectedTestCase.Id != testCase.Id);
            Verify(testCase.GetPropertyValue(Constants.TestIdGenerationStrategyProperty).Equals((int)TestIdGenerationStrategy.FullyQualified));
        }
    }

    [Obsolete("Remove test case when enum entry is removed")]
    public void ToTestCase_WhenStrategyIsDisplayName_ExamplesOfTestCaseIdUniqueness()
    {
        TestIdGenerationStrategy testIdStrategy = TestIdGenerationStrategy.DisplayName;
        TestCase[] testCases = new[]
        {
            new UnitTestElement(
                new("MyMethod", "MyProduct.MyNamespace.MyClass", "MyAssembly", false, testIdStrategy))
            .ToTestCase(),
            new UnitTestElement(
                new("MyOtherMethod", "MyProduct.MyNamespace.MyClass", "MyAssembly", false, testIdStrategy))
            .ToTestCase(),
            new UnitTestElement(
                new("MyMethod", "MyOtherProduct.MyNamespace.MyClass", "MyAssembly", false, testIdStrategy))
            .ToTestCase(),
            new UnitTestElement(
                new("MyMethod", "MyProduct.MyNamespace.MyClass", "MyOtherAssembly", false, testIdStrategy))
            .ToTestCase(),
            new UnitTestElement(
                new("MyMethod", "MyProduct.MyNamespace.MyClass", "MyAssembly", false, testIdStrategy)
                {
                    DataType = DynamicDataType.DataSourceAttribute,
                })
            {
                DisplayName = "SomeDisplayName",
            }
            .ToTestCase(),
            new UnitTestElement(
                new("MyMethod", "MyProduct.MyNamespace.MyClass", "MyAssembly", false, testIdStrategy)
                {
                    DataType = DynamicDataType.ITestDataSource,
                })
            {
                DisplayName = "SomeOtherDisplayName",
            }
            .ToTestCase(),
        };

        Verify(testCases.Select(tc => tc.Id.ToString()).Distinct().Count() == testCases.Length);
    }

    [Obsolete("Remove test case when enum entry is removed")]
    public void ToTestCase_WhenStrategyIsDisplayName_ExamplesOfTestCaseIdCollision()
    {
        TestIdGenerationStrategy testIdStrategy = TestIdGenerationStrategy.DisplayName;
        TestCase[] testCases = new[]
        {
            new UnitTestElement(
                new("MyMethod", "MyProduct.MyNamespace.MyClass", "MyAssembly", false, testIdStrategy)
                {
                    DataType = DynamicDataType.DataSourceAttribute,
                })
            .ToTestCase(),
            new UnitTestElement(
                new("MyMethod", "MyProduct.MyNamespace.MyClass", "MyAssembly", false, testIdStrategy)
                {
                    DataType = DynamicDataType.DataSourceAttribute,
                    SerializedData = new[] { "1", },
                })
            .ToTestCase(),
            new UnitTestElement(
                new("MyMethod", "MyProduct.MyNamespace.MyClass", "MyAssembly", false, testIdStrategy)
                {
                    DataType = DynamicDataType.DataSourceAttribute,
                    SerializedData = new[] { "2", },
                })
            .ToTestCase(),
            new UnitTestElement(
                new("MyMethod", "MyProduct.MyNamespace.MyClass", "MyAssembly", false, testIdStrategy)
                {
                    DataType = DynamicDataType.ITestDataSource,
                    SerializedData = new[] { "1", },
                })
            .ToTestCase(),
            new UnitTestElement(
                new("MyMethod", "MyProduct.MyNamespace.MyClass", "MyAssembly", false, testIdStrategy)
                {
                    DataType = DynamicDataType.ITestDataSource,
                    SerializedData = new[] { "2", },
                })
            .ToTestCase(),
        };

        Verify(testCases.Select(tc => tc.Id.ToString()).Distinct().Count() == 1);
    }

    public void ToTestCase_WhenStrategyIsFullyQualifiedTest_ExamplesOfTestCaseIdUniqueness()
    {
        TestIdGenerationStrategy testIdStrategy = TestIdGenerationStrategy.FullyQualified;
        TestCase[] testCases = new[]
        {
            new UnitTestElement(
                new("MyMethod", "MyProduct.MyNamespace.MyClass", "MyAssembly", false, testIdStrategy))
            .ToTestCase(),
            new UnitTestElement(
                new("MyOtherMethod", "MyProduct.MyNamespace.MyClass", "MyAssembly", false, testIdStrategy))
            .ToTestCase(),
            new UnitTestElement(
                new("MyMethod", "MyOtherProduct.MyNamespace.MyClass", "MyAssembly", false, testIdStrategy))
            .ToTestCase(),
            new UnitTestElement(
                new("MyMethod", "MyProduct.MyNamespace.MyClass", "MyOtherAssembly", false, testIdStrategy))
            .ToTestCase(),
            new UnitTestElement(
                new("MyMethod", "MyProduct.MyNamespace.MyClass", "MyAssembly", false, testIdStrategy)
                {
                    SerializedData = new[] { "System.Int32[], mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089", "[]", },
                })
            .ToTestCase(),
            new UnitTestElement(
                new("MyMethod", "MyProduct.MyNamespace.MyClass", "MyAssembly", false, testIdStrategy)
                {
                    SerializedData = new[] { "System.Int32[], mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089", "[1]", },
                })
            .ToTestCase(),
            new UnitTestElement(
                new("MyMethod", "MyProduct.MyNamespace.MyClass", "MyAssembly", false, testIdStrategy)
                {
                    SerializedData = new[] { "System.Int32[], mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089", "[1,1]", },
                })
            .ToTestCase(),
        };

        Verify(testCases.Select(tc => tc.Id.ToString()).Distinct().Count() == testCases.Length);
    }

    #endregion
}
