// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

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

    public void UnitTestElementConstructorShouldThrowIfTestMethodIsNull() =>
        VerifyThrows<ArgumentNullException>(() => _ = new UnitTestElement(null!));

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

        Verify(testCase.ExecutorUri == EngineConstants.ExecutorUri);
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

        Verify((testCase.GetPropertyValue(EngineConstants.TestClassNameProperty) as string) == "C");
    }

    public void ToTestCaseShouldSetDeclaringClassNameIfPresent()
    {
        _testMethod.DeclaringClassFullName = null;
        var testCase = _unitTestElement.ToTestCase();

        Verify(testCase.GetPropertyValue(EngineConstants.DeclaringClassNameProperty) is null);

        _testMethod.DeclaringClassFullName = "DC";
        testCase = _unitTestElement.ToTestCase();

        Verify((testCase.GetPropertyValue(EngineConstants.DeclaringClassNameProperty) as string) == "DC");
    }

    public void ToTestCaseShouldSetTestCategoryIfPresent()
    {
        _unitTestElement.TestCategory = null;
        var testCase = _unitTestElement.ToTestCase();

        Verify(testCase.GetPropertyValue(EngineConstants.TestCategoryProperty) is null);

        _unitTestElement.TestCategory = [];
        testCase = _unitTestElement.ToTestCase();

        Verify(testCase.GetPropertyValue(EngineConstants.TestCategoryProperty) is null);

        _unitTestElement.TestCategory = ["TC"];
        testCase = _unitTestElement.ToTestCase();

        Verify(new string[] { "TC" }.SequenceEqual((string[])testCase.GetPropertyValue(EngineConstants.TestCategoryProperty)!));
    }

    public void ToTestCaseShouldSetPriorityIfPresent()
    {
        _unitTestElement.Priority = null;
        var testCase = _unitTestElement.ToTestCase();

        Verify((int)testCase.GetPropertyValue(EngineConstants.PriorityProperty)! == 0);

        _unitTestElement.Priority = 1;
        testCase = _unitTestElement.ToTestCase();

        Verify((int)testCase.GetPropertyValue(EngineConstants.PriorityProperty)! == 1);
    }

    public void ToTestCaseShouldSetTraitsIfPresent()
    {
        _unitTestElement.Traits = null;
        var testCase = _unitTestElement.ToTestCase();

#pragma warning disable CA1827 // Do not use Count() or LongCount() when Any() can be used
        Verify(testCase.Traits.Count() == 0);
#pragma warning restore CA1827 // Do not use Count() or LongCount() when Any() can be used

        var trait = new Trait("trait", "value");
        _unitTestElement.Traits = [trait];
        testCase = _unitTestElement.ToTestCase();

        Verify(testCase.Traits.Count() == 1);
        Verify(testCase.Traits.ToArray()[0].Name == "trait");
        Verify(testCase.Traits.ToArray()[0].Value == "value");
    }

    public void ToTestCaseShouldSetPropertiesIfPresent()
    {
        _unitTestElement.WorkItemIds = ["2312", "22332"];

        var testCase = _unitTestElement.ToTestCase();

        Verify(new string[] { "2312", "22332" }.SequenceEqual((string[])testCase.GetPropertyValue(EngineConstants.WorkItemIdsProperty)!));
    }

    public void ToTestCaseShouldSetDeploymentItemPropertyIfPresent()
    {
        _unitTestElement.DeploymentItems = null;
        var testCase = _unitTestElement.ToTestCase();

        Verify(testCase.GetPropertyValue(EngineConstants.DeploymentItemsProperty) is null);

        _unitTestElement.DeploymentItems = [];
        testCase = _unitTestElement.ToTestCase();

        Verify(testCase.GetPropertyValue(EngineConstants.DeploymentItemsProperty) is null);

        _unitTestElement.DeploymentItems = [new("s", "d")];
        testCase = _unitTestElement.ToTestCase();

        Verify(_unitTestElement.DeploymentItems.SequenceEqual(testCase.GetPropertyValue(EngineConstants.DeploymentItemsProperty) as KeyValuePair<string, string>[]));
    }

    public void ToTestCase_WhenStrategyIsData_DoesNotUseDefaultTestCaseId()
    {
#pragma warning disable CA2263 // Prefer generic overload when type is known
        foreach (DynamicDataType dataType in Enum.GetValues<DynamicDataType>())
        {
            var testCase = new UnitTestElement(new("MyMethod", "MyProduct.MyNamespace.MyClass", "MyAssembly", null)
            {
                DataType = dataType,
                SerializedData = dataType == DynamicDataType.None ? null : [],
            }).ToTestCase();
            var expectedTestCase = new TestCase(testCase.FullyQualifiedName, testCase.ExecutorUri, testCase.Source);
            Guid expectedId = GuidFromString("MyAssemblyMyProduct.MyNamespace.MyClass.MyMethod" + (dataType == DynamicDataType.None ? string.Empty : "[0]"));
            Verify(expectedTestCase.Id != testCase.Id);
            Verify(expectedId == testCase.Id);
            Verify(Guid.TryParse(dataType == DynamicDataType.None ? "acd77ae5-d290-158e-2240-056ef4253f19" : "10fb34b8-d5d2-16a1-0620-918822cdc63a", out Guid expectedId2));
            Verify(expectedId == expectedId2);
        }
#pragma warning restore CA2263 // Prefer generic overload when type is known

        unsafe static Guid GuidFromString(string data)
        {
            byte[] hash = TestFx.Hashing.XxHash128.Hash(Encoding.Unicode.GetBytes(data));
            var guid = new Guid(hash);
            short* addressOfC = (short*)((byte*)&guid + 6);
            *addressOfC = (short)((*addressOfC & 0b0000_1111_1111_1111) | 0b0001_0000_0000_0000);
            return guid;
        }
    }

    public void ToTestCase_WhenStrategyIsFullyQualifiedTest_ExamplesOfTestCaseIdUniqueness()
    {
        TestCase[] testCases =
        [
            new UnitTestElement(
                new("MyMethod", "MyProduct.MyNamespace.MyClass", "MyAssembly", null))
            .ToTestCase(),
            new UnitTestElement(
                new("MyOtherMethod", "MyProduct.MyNamespace.MyClass", "MyAssembly", null))
            .ToTestCase(),
            new UnitTestElement(
                new("MyMethod", "MyOtherProduct.MyNamespace.MyClass", "MyAssembly", null))
            .ToTestCase(),
            new UnitTestElement(
                new("MyMethod", "MyProduct.MyNamespace.MyClass", "MyOtherAssembly", null))
            .ToTestCase(),
            new UnitTestElement(
                new("MyMethod", "MyProduct.MyNamespace.MyClass", "MyAssembly", null)
                {
                    SerializedData = ["System.Int32[], mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089", "[]"],
                    TestCaseIndex = 0,
                })
            .ToTestCase(),
            new UnitTestElement(
                new("MyMethod", "MyProduct.MyNamespace.MyClass", "MyAssembly", null)
                {
                    SerializedData = ["System.Int32[], mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089", "[1]"],
                    TestCaseIndex = 1,
                })
            .ToTestCase(),
            new UnitTestElement(
                new("MyMethod", "MyProduct.MyNamespace.MyClass", "MyAssembly", null)
                {
                    SerializedData = ["System.Int32[], mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089", "[1,1]"],
                    TestCaseIndex = 2,
                })
            .ToTestCase()
        ];

        Verify(testCases.Select(tc => tc.Id.ToString()).Distinct().Count() == testCases.Length);
    }

    #endregion
}
