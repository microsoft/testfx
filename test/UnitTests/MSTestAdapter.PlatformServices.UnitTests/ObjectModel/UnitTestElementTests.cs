// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using AwesomeAssertions;

using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Extensions;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices;
using Microsoft.VisualStudio.TestPlatform.ObjectModel;

using TestFramework.ForTestingMSTest;

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.ObjectModel;

public class UnitTestElementTests : TestContainer
{
    private readonly TestMethod _testMethod;
    private readonly UnitTestElement _unitTestElement;

    public UnitTestElementTests()
    {
        _testMethod = new TestMethod("M", "C", "A", displayName: null);
        _unitTestElement = new UnitTestElement(_testMethod);
    }

    #region Ctor tests

    public void UnitTestElementConstructorShouldThrowIfTestMethodIsNull()
    {
        Action action = () => _ = new UnitTestElement(null!);
        action.Should().Throw<ArgumentNullException>();
    }

    public void ResourceLocksShouldRoundTripThroughTestCaseProperty()
    {
        // The acceptance assets run on the native MSTest runner, so they never exercise this hidden VSTest
        // TestProperty transport. Discovery and execution are separate phases (and may be separate AppDomains),
        // so a regression here would silently drop or weaken locks without failing any other test.
        var element = new UnitTestElement(new TestMethod("M", "C", "A", displayName: null))
        {
            ResourceLocks =
            [
                new ResourceLockInfo("exclusive-key", ResourceAccessMode.ReadWrite),
                new ResourceLockInfo("shared-key", ResourceAccessMode.Read),
            ],
        };

        UnitTestElement roundTripped = element.ToTestCase().ToUnitTestElementWithUpdatedSource("A");

        roundTripped.ResourceLocks.Should().NotBeNull();
        roundTripped.ResourceLocks!.Length.Should().Be(2);
        roundTripped.ResourceLocks[0].Resource.Should().Be("exclusive-key");
        roundTripped.ResourceLocks[0].Mode.Should().Be(ResourceAccessMode.ReadWrite);
        roundTripped.ResourceLocks[1].Resource.Should().Be("shared-key");
        roundTripped.ResourceLocks[1].Mode.Should().Be(ResourceAccessMode.Read);
    }

    public void ResourceLocksShouldRoundTripKeysContainingThePrefixCharacters()
    {
        // The encoding is a one-character prefix with no delimiter, so keys that themselves start with 'R' or
        // 'W' are the case most likely to be mangled by an off-by-one in the decoder.
        var element = new UnitTestElement(new TestMethod("M", "C", "A", displayName: null))
        {
            ResourceLocks = [new ResourceLockInfo("Registry", ResourceAccessMode.Read), new ResourceLockInfo("Windows", ResourceAccessMode.ReadWrite)],
        };

        UnitTestElement roundTripped = element.ToTestCase().ToUnitTestElementWithUpdatedSource("A");

        roundTripped.ResourceLocks![0].Resource.Should().Be("Registry");
        roundTripped.ResourceLocks[0].Mode.Should().Be(ResourceAccessMode.Read);
        roundTripped.ResourceLocks[1].Resource.Should().Be("Windows");
        roundTripped.ResourceLocks[1].Mode.Should().Be(ResourceAccessMode.ReadWrite);
    }

    public void ResourceLocksShouldRemainNullWhenNoneAreDeclared()
    {
        UnitTestElement roundTripped = _unitTestElement.ToTestCase().ToUnitTestElementWithUpdatedSource("A");

        roundTripped.ResourceLocks.Should().BeNull();
    }

    public void DependenciesShouldRoundTripThroughTestCaseProperty()
    {
        // The acceptance assets run on the native MSTest runner, so nothing else exercises this hidden VSTest
        // TestProperty transport; a registration or conversion regression would silently make [DependsOn]
        // ineffective under VSTest while every other test still passed.
        //
        // LocalExtensionData is cleared deliberately. ToTestCase caches the element on the test case, and
        // ToUnitTestElementWithUpdatedSource returns that cached instance when it is present - which is what
        // happens in-process, and which would make this test pass without the encoded property being read at
        // all. Clearing it reproduces the case the encoding exists for: a test case that crossed a process or
        // AppDomain boundary, where only the serialized properties survive.
        var element = new UnitTestElement(new TestMethod("M", "C", "A", displayName: null))
        {
            Dependencies =
            [
                new TestDependencyInfo("Ns.Other", "Prereq", proceedOnFailure: false),
                new TestDependencyInfo("Ns.Whole", null, proceedOnFailure: true),
                new TestDependencyInfo(null, "SameClassPrereq", proceedOnFailure: false),
            ],
        };

        var testCase = element.ToTestCase();
        testCase.LocalExtensionData = null;
        UnitTestElement roundTripped = testCase.ToUnitTestElementWithUpdatedSource("A");

        roundTripped.Dependencies.Should().NotBeNull();
        roundTripped.Dependencies!.Length.Should().Be(3);

        roundTripped.Dependencies[0].TargetClassFullName.Should().Be("Ns.Other");
        roundTripped.Dependencies[0].TargetMethodName.Should().Be("Prereq");
        roundTripped.Dependencies[0].ProceedOnFailure.Should().BeFalse();

        // A whole-class target keeps its null method name, which is what distinguishes it from a named one.
        roundTripped.Dependencies[1].TargetClassFullName.Should().Be("Ns.Whole");
        roundTripped.Dependencies[1].TargetMethodName.Should().BeNull();
        roundTripped.Dependencies[1].ProceedOnFailure.Should().BeTrue();

        // A same-class target keeps its null class name, so it still resolves against the dependent's class.
        roundTripped.Dependencies[2].TargetClassFullName.Should().BeNull();
        roundTripped.Dependencies[2].TargetMethodName.Should().Be("SameClassPrereq");
        roundTripped.Dependencies[2].ProceedOnFailure.Should().BeFalse();
    }

    public void DependenciesShouldRemainNullWhenNoneAreDeclared()
    {
        var testCase = _unitTestElement.ToTestCase();
        testCase.LocalExtensionData = null;

        testCase.ToUnitTestElementWithUpdatedSource("A").Dependencies.Should().BeNull();
    }

    #endregion

    #region Source resolution / host test case tests

    public void WithUpdatedSourceShouldReturnSameInstanceWhenSourceUnchanged()
        => _unitTestElement.WithUpdatedSource("A").Should().BeSameAs(_unitTestElement);

    public void WithUpdatedSourceShouldReturnCloneWithNewSourceAndLeaveOriginalUnchanged()
    {
        UnitTestElement clone = _unitTestElement.WithUpdatedSource("B");

        clone.Should().NotBeSameAs(_unitTestElement);
        clone.TestMethod.AssemblyName.Should().Be("B");
        // The original element (and its test method) must not be mutated by the clone.
        _unitTestElement.TestMethod.AssemblyName.Should().Be("A");
        clone.TestMethod.Should().NotBeSameAs(_testMethod);
    }

    public void GetOrCreateHostTestCaseShouldReturnHostHandleWhenPresent()
    {
        var hostTestCase = new TestCase("C.M", EngineConstants.ExecutorUri, "A");
        _unitTestElement.HostRecordingHandle = hostTestCase;

        _unitTestElement.GetOrCreateHostTestCase().Should().BeSameAs(hostTestCase);
    }

    public void GetOrCreateHostTestCaseShouldMaterializeAndCacheWhenNoHandle()
    {
        _unitTestElement.HostRecordingHandle.Should().BeNull();

        TestCase materialized = _unitTestElement.GetOrCreateHostTestCase();

        materialized.Should().NotBeNull();
        // Subsequent calls (deployment, test-start, each reported result) reuse the same instance.
        _unitTestElement.GetOrCreateHostTestCase().Should().BeSameAs(materialized);
        _unitTestElement.HostRecordingHandle.Should().BeSameAs(materialized);
    }

    #endregion

    #region TestMethod.FullyQualifiedName tests

    public void FullyQualifiedNameShouldCombineFullClassNameAndMethodName()
        => _testMethod.FullyQualifiedName.Should().Be("C.M");

    public void FullyQualifiedNameShouldBeComputedOnceAndCached()
    {
        string first = _testMethod.FullyQualifiedName;
        first.Should().Be("C.M");

        // The string is built by interpolation, so a recomputed value would be a distinct (non-interned)
        // instance. Reference identity is what proves the hot paths reuse a single allocation per test method.
        _testMethod.FullyQualifiedName.Should().BeSameAs(first);
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
        _unitTestElement.TestMethod.DisplayName = "Display Name";
        var testCase = _unitTestElement.ToTestCase();

        testCase.DisplayName.Should().Be("Display Name");
    }

    public void ToTestCaseShouldSetTestClassNameProperty()
    {
        var testCase = _unitTestElement.ToTestCase();

        (testCase.GetPropertyValue(AdapterTestProperties.TestClassNameProperty) as string).Should().Be("C");
    }

    public void ToTestCaseShouldUseFullClassNameAsManagedTypeName()
    {
        var testMethod = new TestMethod("DummyMethod", null, "DummyMethod", "SemanticClassName", "A", displayName: null, null);
        var testCase = new UnitTestElement(testMethod).ToTestCase();

        (testCase.GetPropertyValue(TestCaseExtensions.ManagedTypeProperty) as string).Should().Be("SemanticClassName");
    }

    public void ToTestCaseShouldSetTestCategoryIfPresent()
    {
        _unitTestElement.TestCategory = null;
        var testCase = _unitTestElement.ToTestCase();

        testCase.GetPropertyValue(AdapterTestProperties.TestCategoryProperty).Should().BeNull();

        _unitTestElement.TestCategory = [];
        testCase = _unitTestElement.ToTestCase();

        testCase.GetPropertyValue(AdapterTestProperties.TestCategoryProperty).Should().BeNull();

        _unitTestElement.TestCategory = ["TC"];
        testCase = _unitTestElement.ToTestCase();

        new string[] { "TC" }.SequenceEqual((string[])testCase.GetPropertyValue(AdapterTestProperties.TestCategoryProperty)!).Should().BeTrue();
    }

    public void ToTestCaseShouldSetPriorityIfPresent()
    {
        _unitTestElement.Priority = null;
        var testCase = _unitTestElement.ToTestCase();

        ((int)testCase.GetPropertyValue(AdapterTestProperties.PriorityProperty)!).Should().Be(0);

        _unitTestElement.Priority = 1;
        testCase = _unitTestElement.ToTestCase();

        ((int)testCase.GetPropertyValue(AdapterTestProperties.PriorityProperty)!).Should().Be(1);
    }

    public void ToTestCaseShouldSetTraitsIfPresent()
    {
        _unitTestElement.Traits = null;
        var testCase = _unitTestElement.ToTestCase();

#pragma warning disable CA1827 // Do not use Count() or LongCount() when Any() can be used
        testCase.Traits.Count().Should().Be(0);
#pragma warning restore CA1827 // Do not use Count() or LongCount() when Any() can be used

        var trait = new TestTrait("trait", "value");
        _unitTestElement.Traits = [trait];
        testCase = _unitTestElement.ToTestCase();

        testCase.Traits.Count().Should().Be(1);
        testCase.Traits.ToArray()[0].Name.Should().Be("trait");
        testCase.Traits.ToArray()[0].Value.Should().Be("value");
    }

    public void ToTestCaseShouldSetPropertiesIfPresent()
    {
        _unitTestElement.WorkItemIds = ["2312", "22332"];

        var testCase = _unitTestElement.ToTestCase();

        ((string[])testCase.GetPropertyValue(AdapterTestProperties.WorkItemIdsProperty)!).Should().Equal(["2312", "22332"]);
    }

#if !WINDOWS_UWP && !WIN_UI
    public void ToTestCaseShouldSetDeploymentItemPropertyIfPresent()
    {
        _unitTestElement.DeploymentItems = null;
        var testCase = _unitTestElement.ToTestCase();

        testCase.GetPropertyValue(AdapterTestProperties.DeploymentItemsProperty).Should().BeNull();

        _unitTestElement.DeploymentItems = [];
        testCase = _unitTestElement.ToTestCase();

        testCase.GetPropertyValue(AdapterTestProperties.DeploymentItemsProperty).Should().BeNull();

        _unitTestElement.DeploymentItems = [new("s", "d")];
        testCase = _unitTestElement.ToTestCase();

        _unitTestElement.DeploymentItems.SequenceEqual(testCase.GetPropertyValue(AdapterTestProperties.DeploymentItemsProperty) as KeyValuePair<string, string>[]).Should().BeTrue();
    }
#endif

    public void ToTestCase_WhenStrategyIsData_DoesNotUseDefaultTestCaseId()
    {
#if NETCOREAPP
        foreach (DynamicDataType dataType in Enum.GetValues<DynamicDataType>())
#else
        foreach (DynamicDataType dataType in Enum.GetValues(typeof(DynamicDataType)))
#endif
        {
            var testCase = new UnitTestElement(new("MyMethod", "MyProduct.MyNamespace.MyClass", "MyAssembly", null)
            {
                DataType = dataType,
                SerializedData = dataType == DynamicDataType.None ? null : [],
            }).ToTestCase();
            var expectedTestCase = new TestCase(testCase.FullyQualifiedName, testCase.ExecutorUri, testCase.Source);
            Guid expectedId = GuidFromString("MyAssemblyMyProduct.MyNamespace.MyClass.MyMethod" + (dataType == DynamicDataType.None ? string.Empty : "[0]"));
            expectedTestCase.Id.Should().NotBe(testCase.Id);
            testCase.Id.Should().Be(expectedId);
            Guid.TryParse(dataType == DynamicDataType.None ? "157ad7ac-90d2-8e05-a240-056ef4253f19" : "1834fb10-d2d5-8106-8620-918822cdc63a", out Guid expectedId2).Should().BeTrue();
            expectedId.Should().Be(expectedId2);
        }

        static Guid GuidFromString(string data)
        {
            byte[] hash = TestFx.Hashing.XxHash128.Hash(Encoding.Unicode.GetBytes(data));
            return UnitTestElementExtensions.VersionedGuidFromHash(hash, hashVersion: 1);
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
                    DataType = DynamicDataType.ITestDataSource,
                })
            .ToTestCase(),
            new UnitTestElement(
                new("MyMethod", "MyProduct.MyNamespace.MyClass", "MyAssembly", null)
                {
                    SerializedData = ["System.Int32[], mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089", "[1]"],
                    TestCaseIndex = 1,
                    DataType = DynamicDataType.ITestDataSource,
                })
            .ToTestCase(),
            new UnitTestElement(
                new("MyMethod", "MyProduct.MyNamespace.MyClass", "MyAssembly", null)
                {
                    SerializedData = ["System.Int32[], mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089", "[1,1]"],
                    TestCaseIndex = 2,
                    DataType = DynamicDataType.ITestDataSource,
                })
            .ToTestCase()
        ];

        testCases.Select(tc => tc.Id.ToString()).Distinct().Count().Should().Be(testCases.Length);
    }

    #endregion
}
