// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Acceptance.IntegrationTests;
using Microsoft.Testing.Platform.Acceptance.IntegrationTests.Helpers;
using Microsoft.Testing.Platform.Helpers;

namespace MSTest.Acceptance.IntegrationTests;

[TestClass]
public sealed class LegacyExtensibilityTests : AcceptanceTestBase<LegacyExtensibilityTests.TestAssetFixture>
{
    private const string AssetName = "LegacyExtensibility";

    public TestContext TestContext { get; set; } = default!;

    [TestMethod]
    [DynamicData(nameof(TargetFrameworks.AllForDynamicData), typeof(TargetFrameworks))]
    public async Task CustomAssertExtensionsReportExactPassesAndFailures(string tfm)
    {
        TestHostResult result = await AssetFixture.GetTestHost(tfm).ExecuteAsync(
            "--filter ClassName=LegacyExtensibility.AssertExtensionCases --output Detailed",
            cancellationToken: TestContext.CancellationToken);

        result.AssertExitCodeIs(ExitCode.AtLeastOneTestFailed);
        result.AssertOutputContainsSummary(failed: 2, passed: 2, skipped: 0);
        LegacyAcceptanceAssert.Passed(
            result,
            "BasicAssertExtension",
            "ChainedAssertExtension");
        LegacyAcceptanceAssert.Failed(
            result,
            "BasicFailingAssertExtension",
            "ChainedFailingAssertExtension");
        LegacyAcceptanceAssert.OutputContains(
            result,
            "Expected object of type System.FormatException but found object of type System.ArgumentNullException",
            "-10 is not positive");
    }

    [TestMethod]
    [DynamicData(nameof(TargetFrameworks.AllForDynamicData), typeof(TargetFrameworks))]
    public async Task CustomTestMethodAndClassAttributesReturnEveryExecutionResult(string tfm)
    {
        TestHostResult result = await AssetFixture.GetTestHost(tfm).ExecuteAsync(
            "--filter \"(Name=CustomTestMethod)|(Name=CustomTestClass)\" --output Detailed",
            cancellationToken: TestContext.CancellationToken);

        result.AssertExitCodeIs(ExitCode.AtLeastOneTestFailed);
        result.AssertOutputContainsSummary(failed: 2, passed: 8, skipped: 0);
        LegacyAcceptanceAssert.Passed(
            result,
            "CustomTestMethod - Execution number 1",
            "CustomTestMethod - Execution number 2",
            "CustomTestMethod - Execution number 4",
            "CustomTestMethod - Execution number 5",
            "CustomTestClass - Execution number 1",
            "CustomTestClass - Execution number 2",
            "CustomTestClass - Execution number 4",
            "CustomTestClass - Execution number 5");
        LegacyAcceptanceAssert.Failed(
            result,
            "CustomTestMethod - Execution number 3",
            "CustomTestClass - Execution number 3");
    }

    [TestMethod]
    [DynamicData(nameof(TargetFrameworks.AllForDynamicData), typeof(TargetFrameworks))]
    public async Task CustomTestMethodAttributeComposesWithDataRows(string tfm)
    {
        TestHostResult result = await AssetFixture.GetTestHost(tfm).ExecuteAsync(
            "--filter FullyQualifiedName~IterativeCases.CustomTestMethodWithData --output Detailed",
            cancellationToken: TestContext.CancellationToken);

        result.AssertExitCodeIs(ExitCode.AtLeastOneTestFailed);
        result.AssertOutputContainsSummary(failed: 6, passed: 3, skipped: 0);
        LegacyAcceptanceAssert.OutcomeCount(result, "passed", "CustomTestMethodWithData (\"B\")", 3);
        LegacyAcceptanceAssert.OutcomeCount(result, "failed", "CustomTestMethodWithData (\"A\")", 3);
        LegacyAcceptanceAssert.OutcomeCount(result, "failed", "CustomTestMethodWithData (\"C\")", 3);
    }

    [TestMethod]
    [DynamicData(nameof(TargetFrameworks.AllForDynamicData), typeof(TargetFrameworks))]
    public async Task FoldedCustomTestDataSourceDiscoversOneParentAndExecutesTwoRows(string tfm)
    {
        TestHost testHost = AssetFixture.GetTestHost(tfm);

        TestHostResult discovery = await testHost.ExecuteAsync(
            "--filter FullyQualifiedName~FoldedCustomDataSource --list-tests",
            cancellationToken: TestContext.CancellationToken);

        discovery.AssertExitCodeIs(ExitCode.Success);
        discovery.AssertOutputContains("Test discovery summary: found 1 test(s)");
        discovery.AssertOutputContains("FoldedCustomDataSource");
        discovery.AssertOutputDoesNotContain("FoldedCustomDataSource (1,2,3)");

        TestHostResult execution = await testHost.ExecuteAsync(
            "--filter FullyQualifiedName~FoldedCustomDataSource --output Detailed",
            cancellationToken: TestContext.CancellationToken);

        execution.AssertExitCodeIs(ExitCode.Success);
        execution.AssertOutputContainsSummary(failed: 0, passed: 2, skipped: 0);
        LegacyAcceptanceAssert.Passed(
            execution,
            "FoldedCustomDataSource (1,2,3)",
            "FoldedCustomDataSource (4,5,6)");
    }

    [TestMethod]
    [DynamicData(nameof(TargetFrameworks.AllForDynamicData), typeof(TargetFrameworks))]
    public async Task DynamicDataPropertyMethodAndExternalSourcesExecute(string tfm)
    {
        TestHostResult result = await AssetFixture.GetTestHost(tfm).ExecuteAsync(
            "--filter \"ClassName=LegacyExtensibility.DynamicDataCases|ClassName=LegacyExtensibility.ExternalDynamicDataCases\" --output Detailed",
            cancellationToken: TestContext.CancellationToken);

        result.AssertExitCodeIs(ExitCode.AtLeastOneTestFailed);
        result.AssertOutputContainsSummary(failed: 6, passed: 8, skipped: 0);
        LegacyAcceptanceAssert.Passed(
            result,
            "PropertySource (\"string\",2,True)",
            "MethodSource (\"string\",4,True)",
            "CombinedSources (\"string\",2,True)",
            "CombinedSources (\"string\",4,True)",
            "ExternalPropertySource (\"string\",2,True)",
            "ExternalMethodSource (\"string\",4,True)",
            "ExternalCombinedSources (\"string\",2,True)",
            "ExternalCombinedSources (\"string\",4,True)");
        LegacyAcceptanceAssert.Failed(
            result,
            "EmptyPropertySource",
            "EmptyMethodSource",
            "EmptyCombinedSources",
            "ExternalEmptyPropertySource",
            "ExternalEmptyMethodSource",
            "ExternalEmptyCombinedSources");
    }

    public sealed class TestAssetFixture : TestAssetFixtureBase
    {
        protected override IReadOnlyList<MetadataMode> SourceGenMetadataModes => [];

        public TestHost GetTestHost(string tfm)
            => TestHost.LocateFrom(GetAssetPath(AssetName), AssetName, tfm);

        public override (string ID, string Name, string Code) GetAssetsToGenerate() => (AssetName, AssetName,
                SourceCode
                .PatchTargetFrameworks(TargetFrameworks.All)
                .PatchCodeWithReplace("$MSTestVersion$", MSTestVersion));

        private const string SourceCode = """
#file LegacyExtensibility.csproj
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <EnableMSTestRunner>true</EnableMSTestRunner>
    <TargetFrameworks>$TargetFrameworks$</TargetFrameworks>
    <LangVersion>preview</LangVersion>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="MSTest.TestAdapter" Version="$MSTestVersion$" />
    <PackageReference Include="MSTest.TestFramework" Version="$MSTestVersion$" />
  </ItemGroup>
</Project>

#file ExtensibilityCases.cs
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace LegacyExtensibility;

[TestClass]
public class AssertExtensionCases
{
    [TestMethod]
    public void BasicAssertExtension() => Assert.That.IsOfType<ArgumentException>(new ArgumentOutOfRangeException());

    [TestMethod]
    public void BasicFailingAssertExtension() => Assert.That.IsOfType<FormatException>(new ArgumentNullException());

    [TestMethod]
    public void ChainedAssertExtension() => Assert.That.Is().Divisor(120, 5);

    [TestMethod]
    public void ChainedFailingAssertExtension() => Assert.That.Is().Positive(-10);
}

public static class AssertExtensions
{
    private static readonly AssertIs AssertIs = new();

    public static bool IsOfType<T>(this Assert assert, object value)
        => value is T
            ? true
            : throw new AssertFailedException(
                $"Expected object of type {typeof(T)} but found object of type {value?.GetType()}");

    public static AssertIs Is(this Assert assert) => AssertIs;
}

public sealed class AssertIs
{
    public bool Divisor(int number, int divisor)
        => number % divisor == 0
            ? true
            : throw new AssertFailedException($"{divisor} is not a divisor of {number}");

    public bool Positive(int number)
        => number > 0
            ? true
            : throw new AssertFailedException($"{number} is not positive");
}

[IterativeTestClass(5)]
public class IterativeCases
{
    private static int s_methodExecutionCount;
    private static int s_classExecutionCount;

    [IterativeTestMethod(5)]
    public void CustomTestMethod()
    {
        s_methodExecutionCount++;
        Assert.AreNotEqual(3, s_methodExecutionCount);
    }

    [TestMethod]
    public void CustomTestClass()
    {
        s_classExecutionCount++;
        Assert.AreNotEqual(3, s_classExecutionCount);
    }

    [IterativeTestMethod(3)]
    [DataRow("A")]
    [DataRow("B")]
    [DataRow("C")]
    public void CustomTestMethodWithData(string value) => Assert.AreEqual("B", value);
}

public sealed class IterativeTestMethodAttribute : TestMethodAttribute
{
    private readonly int _iterations;

    public IterativeTestMethodAttribute(int iterations) => _iterations = iterations;

    public override async Task<TestResult[]> ExecuteAsync(ITestMethod testMethod)
    {
        var results = new List<TestResult>();
        for (int index = 0; index < _iterations; index++)
        {
            TestResult[] iterationResults = await base.ExecuteAsync(testMethod);
            foreach (TestResult result in iterationResults)
            {
                result.DisplayName = $"{testMethod.TestMethodName} - Execution number {index + 1}";
            }

            results.AddRange(iterationResults);
        }

        return [.. results];
    }
}

public sealed class IterativeTestClassAttribute : TestClassAttribute
{
    private readonly int _iterations;

    public IterativeTestClassAttribute(int iterations) => _iterations = iterations;

    public override TestMethodAttribute GetTestMethodAttribute(TestMethodAttribute testMethodAttribute)
        => testMethodAttribute is IterativeTestMethodAttribute
            ? testMethodAttribute
            : new IterativeTestMethodAttribute(_iterations);
}

[TestClass]
public class CustomDataSourceCases
{
    [TestMethod(UnfoldingStrategy = TestDataSourceUnfoldingStrategy.Fold)]
    [CustomTestDataSource]
    public void FoldedCustomDataSource(int first, int second, int third)
        => Assert.AreEqual(0, third % 3);
}

[AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
public sealed class CustomTestDataSourceAttribute : Attribute, ITestDataSource
{
    public IEnumerable<object[]> GetData(MethodInfo methodInfo) => [[1, 2, 3], [4, 5, 6]];

    public string GetDisplayName(MethodInfo methodInfo, object[] data)
        => $"{methodInfo.Name} ({string.Join(",", data)})";
}

[TestClass]
public class DynamicDataCases
{
    private static IEnumerable<object[]> PropertyData => [["string", 2, true]];
    private static IEnumerable<object[]> MethodData() => [["string", 4, true]];
    private static IEnumerable<object[]> EmptyPropertyData => [];
    private static IEnumerable<object[]> EmptyMethodData() => [];

    [TestMethod] [DynamicData(nameof(PropertyData))] public void PropertySource(string text, int number, bool flag) { }
    [TestMethod] [DynamicData(nameof(MethodData), DynamicDataSourceType.Method)] public void MethodSource(string text, int number, bool flag) { }
    [TestMethod] [DynamicData(nameof(PropertyData))] [DynamicData(nameof(MethodData), DynamicDataSourceType.Method)] public void CombinedSources(string text, int number, bool flag) { }
    [TestMethod] [DynamicData(nameof(EmptyPropertyData))] public void EmptyPropertySource(string text, int number, bool flag) { }
    [TestMethod] [DynamicData(nameof(EmptyMethodData), DynamicDataSourceType.Method)] public void EmptyMethodSource(string text, int number, bool flag) { }
    [TestMethod] [DynamicData(nameof(EmptyPropertyData))] [DynamicData(nameof(EmptyMethodData), DynamicDataSourceType.Method)] public void EmptyCombinedSources(string text, int number, bool flag) { }
}

[TestClass]
public class ExternalDynamicDataCases
{
    [TestMethod] [DynamicData("PropertyData", typeof(DynamicDataCases))] public void ExternalPropertySource(string text, int number, bool flag) { }
    [TestMethod] [DynamicData("MethodData", typeof(DynamicDataCases), DynamicDataSourceType.Method)] public void ExternalMethodSource(string text, int number, bool flag) { }
    [TestMethod] [DynamicData("PropertyData", typeof(DynamicDataCases))] [DynamicData("MethodData", typeof(DynamicDataCases), DynamicDataSourceType.Method)] public void ExternalCombinedSources(string text, int number, bool flag) { }
    [TestMethod] [DynamicData("EmptyPropertyData", typeof(DynamicDataCases))] public void ExternalEmptyPropertySource(string text, int number, bool flag) { }
    [TestMethod] [DynamicData("EmptyMethodData", typeof(DynamicDataCases), DynamicDataSourceType.Method)] public void ExternalEmptyMethodSource(string text, int number, bool flag) { }
    [TestMethod] [DynamicData("EmptyPropertyData", typeof(DynamicDataCases))] [DynamicData("EmptyMethodData", typeof(DynamicDataCases), DynamicDataSourceType.Method)] public void ExternalEmptyCombinedSources(string text, int number, bool flag) { }
}
""";
    }
}
