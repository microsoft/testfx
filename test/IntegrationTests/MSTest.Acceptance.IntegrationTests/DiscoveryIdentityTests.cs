// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Immutable;
using System.Runtime.Loader;

using Microsoft.MSTestV2.CLIAutomation;
using Microsoft.Testing.Platform.Acceptance.IntegrationTests;
using Microsoft.Testing.Platform.Acceptance.IntegrationTests.Helpers;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Extensions;
using Microsoft.VisualStudio.TestPlatform.ObjectModel;

namespace MSTest.Acceptance.IntegrationTests;

[TestClass]
public sealed class DiscoveryIdentityTests : AcceptanceTestBase<DiscoveryIdentityTests.TestAssetFixture>
{
    private const string AssetName = "DiscoveryIdentityAsset";
    private static readonly Lock AssetAssemblyLock = new();
    private static Assembly? s_assetAssembly;

    public TestContext TestContext { get; set; } = default!;

    [TestMethod]
    public void TestsWithoutNamespaceHaveExactAdapterHierarchy()
    {
        ImmutableArray<TestCase> testCases = DiscoverTests();

        TestCase noNamespace = testCases.Single(testCase => testCase.FullyQualifiedName.Contains("ClassWithNoNamespace", StringComparison.Ordinal));
        TestCase namespaced = testCases.Single(testCase => testCase.FullyQualifiedName.Contains("SomeNamespace.WithMultipleLevels.ClassWithNamespace", StringComparison.Ordinal));

        AssertHierarchy(noNamespace, expectedNamespace: null, "ClassWithNoNamespace", "MyMethodUnderTest");
        AssertHierarchy(namespaced, "SomeNamespace.WithMultipleLevels", "ClassWithNamespace", "MyMethodUnderTest");
    }

    [TestMethod]
    public void ParameterizedTestsHaveUniqueAdapterTestCaseIdsForEveryDataShape()
    {
        ImmutableArray<TestCase> testCases = DiscoverTests();

        AssertUniqueIds(testCases, "DataRowArraysTests", 3);
        AssertUniqueIds(testCases, "DataRowStringTests", 5);
        AssertUniqueIds(testCases, "DynamicDataArraysTests", 3);
        AssertUniqueIds(testCases, "DynamicDataTuplesTests", 2);
        AssertUniqueIds(testCases, "DynamicDataGenericCollectionsTests", 4);
        AssertUniqueIds(testCases, "TestDataSourceArraysTests", 3);
        AssertUniqueIds(testCases, "TestDataSourceTuplesTests", 2);
        AssertUniqueIds(testCases, "TestDataSourceGenericCollectionsTests", 4);

        TestCase[] allIdentityCases = [.. testCases.Where(testCase => testCase.FullyQualifiedName.Contains("IdentityCases.", StringComparison.Ordinal))];
        Assert.HasCount(26, allIdentityCases);
        Assert.AreEqual(
            allIdentityCases.Length,
            allIdentityCases.Select(testCase => testCase.Id).Distinct().Count(),
            "Every discovered parameterized test case should have a unique adapter TestCase.Id.");
    }

    [TestMethod]
    public void TestDataRowsExposeExactMergedCategoriesAndAreFilterable()
    {
        ImmutableArray<TestCase> testCases = DiscoverTests();

        AssertCategories(testCases, "Test with Integration and Slow categories", "Integration", "Slow");
        AssertCategories(testCases, "Test with Unit and Fast categories", "Fast", "Unit");
        AssertCategories(testCases, "Test with no additional categories");
        AssertCategories(testCases, "Test with method and data categories", "DataLevel", "MethodLevel");

        ImmutableArray<TestCase> integrationTests = DiscoverTests("TestCategory=Integration");
        Assert.HasCount(1, integrationTests);
        Assert.AreEqual("Test with Integration and Slow categories", integrationTests[0].DisplayName);

        ImmutableArray<TestCase> methodLevelTests = DiscoverTests("TestCategory=MethodLevel");
        ImmutableArray<TestCase> dataLevelTests = DiscoverTests("TestCategory=DataLevel");
        Assert.HasCount(1, methodLevelTests);
        Assert.HasCount(1, dataLevelTests);
        Assert.AreEqual(methodLevelTests[0].Id, dataLevelTests[0].Id);
        Assert.AreEqual("Test with method and data categories", methodLevelTests[0].DisplayName);
    }

    [TestMethod]
    public async Task TestDataRowCategoryFilteringWorksOutOfProcess()
    {
        TestHostResult result = await AssetFixture.GetTestHost().ExecuteAsync(
            "--filter TestCategory=Integration --output Detailed",
            cancellationToken: TestContext.CancellationToken);

        result.AssertExitCodeIs(ExitCode.Success);
        result.AssertOutputContainsSummary(failed: 0, passed: 1, skipped: 0);
        result.AssertOutputContains("Test with Integration and Slow categories");
        result.AssertOutputDoesNotContain("Test with Unit and Fast categories");
        result.AssertOutputDoesNotContain("Test with method and data categories");
    }

    [TestMethod]
    public void DiscoverInternalsFindsTopLevelNestedGenericAndDynamicDataTests()
    {
        ImmutableArray<TestCase> testCases = DiscoverTests();

        Assert.HasCount(1, testCases.Where(testCase => testCase.DisplayName == "TopLevelInternalClass_TestMethod1"));
        Assert.HasCount(1, testCases.Where(testCase => testCase.DisplayName == "NestedInternalClass_TestMethod1"));
        Assert.HasCount(1, testCases.Where(testCase => testCase.DisplayName == "EqualityIsCaseInsensitive"));
        Assert.HasCount(1, testCases.Where(testCase => testCase.DisplayName == "DynamicDataTestMethod (DiscoveryIdentityAsset.SerializableInternalType)"));
    }

    [TestMethod]
    public async Task InternalTypeDynamicDataTestRunsOutOfProcess()
    {
        TestHostResult result = await AssetFixture.GetTestHost().ExecuteAsync(
            "--filter FullyQualifiedName~DynamicDataTestMethod --output Detailed",
            cancellationToken: TestContext.CancellationToken);

        result.AssertExitCodeIs(ExitCode.Success);
        result.AssertOutputContainsSummary(failed: 0, passed: 1, skipped: 0);
        result.AssertOutputContains("DynamicDataTestMethod (DiscoveryIdentityAsset.SerializableInternalType)");
    }

    [TestMethod]
    public async Task ClsCompliantDataRowsAreDiscoveredAndRun()
    {
        ImmutableArray<TestCase> clsTests = [.. DiscoverTests().Where(testCase => testCase.FullyQualifiedName.Contains("ClsTests.", StringComparison.Ordinal))];

        Assert.HasCount(5, clsTests);
        CollectionAssert.AreEquivalent(
            new[]
            {
                "TestMethod",
                "IntDataRow (10)",
                "StringDataRow (\"some string\")",
                "StringDataRow2 (\"some string\")",
                "StringDataRow2 (\"some other string\")",
            },
            clsTests.Select(testCase => testCase.DisplayName).ToArray());

        TestHostResult result = await AssetFixture.GetTestHost().ExecuteAsync(
            "--filter FullyQualifiedName~ClsTests --output Detailed",
            cancellationToken: TestContext.CancellationToken);

        result.AssertExitCodeIs(ExitCode.Success);
        result.AssertOutputContainsSummary(failed: 0, passed: 5, skipped: 0);
    }

    private static ImmutableArray<TestCase> DiscoverTests(string? testCaseFilter = null)
    {
        EnsureAssetAssemblyIsLoaded();
        return CLITestBase.DiscoverTests(AssetFixture.AssemblyPath, testCaseFilter);
    }

    private static void EnsureAssetAssemblyIsLoaded()
    {
        lock (AssetAssemblyLock)
        {
            s_assetAssembly ??= AssemblyLoadContext.Default.LoadFromAssemblyPath(AssetFixture.AssemblyPath);
        }
    }

    private static void AssertHierarchy(
        TestCase testCase,
        string? expectedNamespace,
        string expectedClassName,
        string expectedMethodName)
    {
        string[]? hierarchy = testCase.GetPropertyValue(TestCaseExtensions.HierarchyProperty) as string[];

        Assert.IsNotNull(hierarchy);
        string[] nonNullHierarchy = hierarchy!;
        Assert.HasCount(4, nonNullHierarchy);
        Assert.IsNull(nonNullHierarchy[0]);
        Assert.AreEqual(expectedNamespace, nonNullHierarchy[1]);
        Assert.AreEqual(expectedClassName, nonNullHierarchy[2]);
        Assert.AreEqual(expectedMethodName, nonNullHierarchy[3]);
    }

    private static void AssertUniqueIds(
        ImmutableArray<TestCase> testCases,
        string methodName,
        int expectedCount)
    {
        TestCase[] matchingTests = [.. testCases.Where(testCase => testCase.FullyQualifiedName.Contains($".{methodName}", StringComparison.Ordinal))];

        Assert.HasCount(expectedCount, matchingTests, $"Unexpected discovery count for {methodName}.");
        Assert.IsFalse(matchingTests.Any(testCase => testCase.Id == Guid.Empty), $"{methodName} should not produce an empty TestCase.Id.");
        Assert.AreEqual(
            expectedCount,
            matchingTests.Select(testCase => testCase.Id).Distinct().Count(),
            $"{methodName} should produce a unique adapter TestCase.Id for every data row.");
    }

    private static void AssertCategories(
        ImmutableArray<TestCase> testCases,
        string displayName,
        params string[] expectedCategories)
    {
        TestCase testCase = testCases.Single(testCase => testCase.DisplayName == displayName);
        TestProperty? categoryProperty = testCase.Properties.SingleOrDefault(property => property.Id == "MSTestDiscoverer.TestCategory");
        string[] categories = categoryProperty is null
            ? []
            : (testCase.GetPropertyValue(categoryProperty) as string[] ?? []);

        categories = [.. categories.OrderBy(category => category, StringComparer.Ordinal)];
        CollectionAssert.AreEqual(expectedCategories, categories, $"Unexpected categories for '{displayName}'.");
    }

    public sealed class TestAssetFixture : TestAssetFixtureBase
    {
        protected override IReadOnlyList<MetadataMode> SourceGenMetadataModes => [];

        public string TargetAssetPath => GetAssetPath(AssetName);

        public string AssemblyPath
            => Path.Combine(TargetAssetPath, "bin", "Release", TargetFrameworks.NetCurrent, $"{AssetName}.dll");

        public TestHost GetTestHost()
            => TestHost.LocateFrom(TargetAssetPath, AssetName, TargetFrameworks.NetCurrent);

        public override (string ID, string Name, string Code) GetAssetsToGenerate() => (AssetName, AssetName,
                SourceCode
                .PatchTargetFrameworks(TargetFrameworks.NetCurrent)
                .PatchCodeWithReplace("$MSTestVersion$", MSTestVersion));

        private const string SourceCode = """
#file DiscoveryIdentityAsset.csproj
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <EnableMSTestRunner>true</EnableMSTestRunner>
    <TargetFrameworks>$TargetFrameworks$</TargetFrameworks>
    <Nullable>enable</Nullable>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="MSTest.TestAdapter" Version="$MSTestVersion$" />
    <PackageReference Include="MSTest.TestFramework" Version="$MSTestVersion$" />
  </ItemGroup>

</Project>

#file DiscoveryCases.cs
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.Serialization;
using Microsoft.VisualStudio.TestTools.UnitTesting;

[assembly: CLSCompliant(true)]
[assembly: DiscoverInternals]

[TestClass]
[SuppressMessage("Design", "CA1050:Declare types in namespaces", Justification = "This test preserves discovery hierarchy for a class with no namespace.")]
public class ClassWithNoNamespace
{
    [TestMethod]
    public void MyMethodUnderTest()
    {
    }
}

namespace SomeNamespace.WithMultipleLevels
{
    [TestClass]
    public class ClassWithNamespace
    {
        [TestMethod]
        public void MyMethodUnderTest()
        {
        }
    }
}

namespace DiscoveryIdentityAsset
{
    [TestClass]
    internal class TopLevelInternalClass
    {
        [TestMethod]
        public void TopLevelInternalClass_TestMethod1()
        {
        }

        [TestClass]
        internal class NestedInternalClass
        {
            [TestMethod]
            public void NestedInternalClass_TestMethod1()
            {
            }
        }
    }

    internal class FancyString
    {
    }

    public abstract class CaseInsensitivityTests<T>
    {
        protected abstract Tuple<T, T> EquivalentInstancesDistinctInCase { get; }

        [TestMethod]
        public void EqualityIsCaseInsensitive()
        {
            Tuple<T, T> tuple = EquivalentInstancesDistinctInCase;
            Assert.AreEqual(tuple.Item1, tuple.Item2);
        }
    }

    [TestClass]
    internal class FancyStringsAreCaseInsensitive : CaseInsensitivityTests<FancyString>
    {
        protected override Tuple<FancyString, FancyString> EquivalentInstancesDistinctInCase =>
            new(new FancyString(), new FancyString());
    }

    [DataContract]
    internal sealed class SerializableInternalType
    {
    }

    [TestClass]
    internal class DynamicDataTest
    {
        [TestMethod]
        [DynamicData(nameof(Data))]
        internal void DynamicDataTestMethod(SerializableInternalType serializableInternalType)
        {
        }

        public static IEnumerable<object[]> Data =>
        [
            [new SerializableInternalType()],
        ];
    }

    [TestClass]
    public class ClsTests
    {
        [TestMethod]
        public void TestMethod()
        {
        }

        [TestMethod]
        [DataRow(10)]
        public void IntDataRow(int value) => Assert.AreEqual(10, value);

        [TestMethod]
        [DataRow("some string")]
        public void StringDataRow(string value) => Assert.IsNotNull(value);

        [TestMethod]
        [DataRow("some string")]
        [DataRow("some other string")]
        public void StringDataRow2(string value) => Assert.IsNotNull(value);
    }
}

#file IdentityCases.cs
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DiscoveryIdentityAsset;

[TestClass]
[CLSCompliant(false)]
public class IdentityCases
{
    [TestMethod]
    [DataRow(0, new int[] { })]
    [DataRow(0, new int[] { 0 })]
    [DataRow(0, new int[] { 0, 0, 0 })]
    public void DataRowArraysTests(int expectedSum, int[] array) => Assert.AreEqual(expectedSum, array.Sum());

    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow(" ")]
    [DataRow("  ")]
    [DataRow("\t")]
    public void DataRowStringTests(string? value)
    {
    }

    [TestMethod]
    [DynamicData(nameof(ArraysData))]
    public void DynamicDataArraysTests(int expectedSum, int[] array) => Assert.AreEqual(expectedSum, array.Sum());

    public static IEnumerable<object[]> ArraysData
    {
        get
        {
            yield return [0, Array.Empty<int>()];
            yield return [0, new int[] { 0 }];
            yield return [0, new int[] { 0, 0, 0 }];
        }
    }

    [TestMethod]
    [DynamicData(nameof(TuplesData))]
    public void DynamicDataTuplesTests((int I, string S, bool B) tuple)
    {
    }

    public static IEnumerable<object[]> TuplesData
    {
        get
        {
            yield return [(1, "text", true)];
            yield return [(1, "text", false)];
        }
    }

    [TestMethod]
    [DynamicData(nameof(GenericCollectionsData))]
    public void DynamicDataGenericCollectionsTests(List<int> integers, List<string> strings, List<bool> bools)
    {
    }

    public static IEnumerable<object[]> GenericCollectionsData
    {
        get
        {
            yield return [new List<int> { 1, 2, 3 }, new List<string> { "a", "b", "c" }, new List<bool> { true, false, true }];
            yield return [new List<int> { 1, 2 }, new List<string> { "a", "b", "c" }, new List<bool> { true, false, true }];
            yield return [new List<int> { 1, 2, 3 }, new List<string> { "a", "b" }, new List<bool> { true, false, true }];
            yield return [new List<int> { 1, 2, 3 }, new List<string> { "a", "b", "c" }, new List<bool> { true, false }];
        }
    }

    [TestMethod]
    [ArraysDataSource]
    public void TestDataSourceArraysTests(int expectedSum, int[] array) => Assert.AreEqual(expectedSum, array.Sum());

    [TestMethod]
    [TuplesDataSource]
    public void TestDataSourceTuplesTests((int I, string S, bool B) tuple)
    {
    }

    [TestMethod]
    [GenericCollectionsDataSource]
    public void TestDataSourceGenericCollectionsTests(List<int> integers, List<string> strings, List<bool> bools)
    {
    }

    internal sealed class ArraysDataSourceAttribute : Attribute, ITestDataSource
    {
        public IEnumerable<object[]> GetData(MethodInfo methodInfo) => ArraysData;

        public string GetDisplayName(MethodInfo methodInfo, object?[]? data) => "Custom name";
    }

    internal sealed class TuplesDataSourceAttribute : Attribute, ITestDataSource
    {
        public IEnumerable<object[]> GetData(MethodInfo methodInfo) => TuplesData;

        public string GetDisplayName(MethodInfo methodInfo, object?[]? data) => "Custom name";
    }

    internal sealed class GenericCollectionsDataSourceAttribute : Attribute, ITestDataSource
    {
        public IEnumerable<object[]> GetData(MethodInfo methodInfo) => GenericCollectionsData;

        public string GetDisplayName(MethodInfo methodInfo, object?[]? data) => "Custom name";
    }
}

#file CategoryCases.cs
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DiscoveryIdentityAsset;

[TestClass]
public class CategoryCases
{
    [TestMethod]
    [DynamicData(nameof(GetTestDataWithCategories))]
    public void TestMethodWithDynamicDataCategories(string value, int number)
    {
        Assert.IsFalse(string.IsNullOrEmpty(value));
        Assert.IsGreaterThan(0, number);
    }

    public static IEnumerable<TestDataRow<(string Value, int Number)>> GetTestDataWithCategories()
    {
        yield return new TestDataRow<(string, int)>(("value1", 1))
        {
            TestCategories = ["Integration", "Slow"],
            DisplayName = "Test with Integration and Slow categories",
        };

        yield return new TestDataRow<(string, int)>(("value2", 2))
        {
            TestCategories = ["Unit", "Fast"],
            DisplayName = "Test with Unit and Fast categories",
        };

        yield return new TestDataRow<(string, int)>(("value3", 3))
        {
            DisplayName = "Test with no additional categories",
        };
    }

    [TestCategory("MethodLevel")]
    [TestMethod]
    [DynamicData(nameof(GetTestDataWithCategoriesForMethodWithCategory))]
    public void TestMethodWithMethodLevelCategoriesAndDataCategories(string value)
        => Assert.IsFalse(string.IsNullOrEmpty(value));

    public static IEnumerable<TestDataRow<string>> GetTestDataWithCategoriesForMethodWithCategory()
    {
        yield return new TestDataRow<string>("test")
        {
            TestCategories = ["DataLevel"],
            DisplayName = "Test with method and data categories",
        };
    }
}
""";
    }
}
