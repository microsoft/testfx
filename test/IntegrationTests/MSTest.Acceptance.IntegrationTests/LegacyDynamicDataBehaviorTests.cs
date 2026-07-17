// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Acceptance.IntegrationTests;
using Microsoft.Testing.Platform.Acceptance.IntegrationTests.Helpers;
using Microsoft.Testing.Platform.Helpers;

namespace MSTest.Acceptance.IntegrationTests;

[TestClass]
public sealed class LegacyDynamicDataBehaviorTests : AcceptanceTestBase<LegacyDynamicDataBehaviorTests.TestAssetFixture>
{
    private const string AssetName = "LegacyDynamicDataBehavior";

    public TestContext TestContext { get; set; } = default!;

    [TestMethod]
    [DynamicData(nameof(TargetFrameworks.AllForDynamicData), typeof(TargetFrameworks))]
    public async Task DynamicDataSourcesInheritanceDisplayNamesAndOverloadsExecute(string tfm)
    {
        TestHostResult result = await AssetFixture.GetTestHost(tfm).ExecuteAsync(
            "--filter ClassName=LegacyDynamicDataBehavior.SourceCases --output Detailed",
            cancellationToken: TestContext.CancellationToken);

        result.AssertExitCodeIs(ExitCode.Success);
        result.AssertOutputContainsSummary(failed: 0, passed: 36, skipped: 0);
        LegacyAcceptanceAssert.Passed(
            result,
            "CurrentMethodSource (\"current-method\",1)",
            "InheritedMethodSource (\"base-method\",2)",
            "ShadowedMethodSource (\"derived-method\",3)",
            "AutoDetectedMethodSource (\"auto-method\",4)",
            "AutoDetectedInheritedMethodSource (\"base-method\",2)",
            "AutoDetectedShadowedMethodSource (\"derived-method\",3)",
            "CurrentPropertySource (\"current-property\",5)",
            "InheritedPropertySource (\"base-property\",6)",
            "ShadowedPropertySource (\"derived-property\",7)",
            "AutoDetectedPropertySource (\"auto-property\",8)",
            "AutoDetectedInheritedPropertySource (\"base-property\",6)",
            "AutoDetectedShadowedPropertySource (\"derived-property\",7)",
            "ExternalMethodSource (\"external-method\",9)",
            "ExternalPropertySource (\"external-property\",10)",
            "Custom LocalDisplayName with 2 parameters",
            "External ExternalDisplayName with 2 parameters",
            "Custom LocalPropertyDisplayName with 2 parameters",
            "External ExternalDisplayForCurrentMethod with 2 parameters",
            "External ExternalDisplayForCurrentProperty with 2 parameters",
            "Custom LocalDisplayForExternalMethod with 2 parameters",
            "Custom LocalDisplayForExternalProperty with 2 parameters",
            "External ExternalDisplayForExternalProperty with 2 parameters",
            "ReferencedAssemblyValues (\"John;Doe\",LegacyDynamicDataValues.User)",
            "ReferencedAssemblyValues (\"Jane;Doe\",LegacyDynamicDataValues.User)",
            "StackOverflowRegression (LegacyDynamicDataBehavior.SourceCases+ExampleTestCase)",
            "ExplicitFieldSource (\"field\",5)",
            "ExplicitFieldSource (\"test\",4)",
            "AutoDetectedFieldSource (\"field\",5)",
            "AutoDetectedFieldSource (\"test\",4)",
            "SimpleCollection (0)",
            "SimpleCollection (2)",
            "SimpleCollection (4)",
            "MethodWithOverload (\"one\",1)",
            "MethodWithOverload (\"two\",2)",
            "MethodWithOverload (1,\"one\")",
            "MethodWithOverload (2,\"two\")");
    }

    [TestMethod]
    [DynamicData(nameof(TargetFrameworks.AllForDynamicData), typeof(TargetFrameworks))]
    public async Task DynamicDataCategoryFilteringSelectsExactRows(string tfm)
    {
        TestHostResult result = await AssetFixture.GetTestHost(tfm).ExecuteAsync(
            "--filter TestCategory=DynamicDataWithCategory --output Detailed",
            cancellationToken: TestContext.CancellationToken);

        result.AssertExitCodeIs(ExitCode.Success);
        result.AssertOutputContainsSummary(failed: 0, passed: 2, skipped: 0);
        LegacyAcceptanceAssert.Passed(
            result,
            "CategorizedDynamicData (\"John;Doe\",1)",
            "CategorizedDynamicData (\"Jane;Doe\",2)");
        result.AssertOutputDoesNotContain("CurrentMethodSource");
    }

    [TestMethod]
    [DynamicData(nameof(TargetFrameworks.AllForDynamicData), typeof(TargetFrameworks))]
    public async Task FoldedDynamicDataDiscoversParentsAndExecutesRows(string tfm)
    {
        TestHost testHost = AssetFixture.GetTestHost(tfm);

        TestHostResult discovery = await testHost.ExecuteAsync(
            "--filter ClassName=LegacyDynamicDataBehavior.FoldedCases --list-tests",
            cancellationToken: TestContext.CancellationToken);

        discovery.AssertExitCodeIs(ExitCode.Success);
        discovery.AssertOutputContains("Test discovery summary: found 4 test(s)");
        LegacyAcceptanceAssert.OutputContains(
            discovery,
            "PropertySourceOnCurrentType",
            "MethodSourceOnCurrentType",
            "PropertySourceOnDifferentType",
            "MethodSourceOnDifferentType");
        discovery.AssertOutputDoesNotContain("PropertySourceOnCurrentType (1,\"a\")");

        TestHostResult execution = await testHost.ExecuteAsync(
            "--filter ClassName=LegacyDynamicDataBehavior.FoldedCases --output Detailed",
            cancellationToken: TestContext.CancellationToken);

        execution.AssertExitCodeIs(ExitCode.Success);
        execution.AssertOutputContainsSummary(failed: 0, passed: 8, skipped: 0);
        LegacyAcceptanceAssert.Passed(
            execution,
            "PropertySourceOnCurrentType (1,\"a\")",
            "PropertySourceOnCurrentType (2,\"b\")",
            "MethodSourceOnCurrentType (1,\"a\")",
            "MethodSourceOnCurrentType (2,\"b\")",
            "PropertySourceOnDifferentType (3,\"c\")",
            "PropertySourceOnDifferentType (4,\"d\")",
            "MethodSourceOnDifferentType (3,\"c\")",
            "MethodSourceOnDifferentType (4,\"d\")");
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
#file LegacyDynamicDataBehavior.csproj
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <EnableMSTestRunner>true</EnableMSTestRunner>
    <TargetFrameworks>$TargetFrameworks$</TargetFrameworks>
    <LangVersion>preview</LangVersion>
  </PropertyGroup>
  <ItemGroup>
    <Compile Remove="LegacyDynamicDataValues\**\*.cs" />
    <PackageReference Include="MSTest.TestAdapter" Version="$MSTestVersion$" />
    <PackageReference Include="MSTest.TestFramework" Version="$MSTestVersion$" />
    <PackageReference Include="Newtonsoft.Json" Version="13.0.3" />
    <ProjectReference Include="LegacyDynamicDataValues\LegacyDynamicDataValues.csproj" />
  </ItemGroup>
</Project>

#file LegacyDynamicDataValues/LegacyDynamicDataValues.csproj
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFrameworks>$TargetFrameworks$</TargetFrameworks>
    <LangVersion>preview</LangVersion>
  </PropertyGroup>
</Project>

#file LegacyDynamicDataValues/User.cs
namespace LegacyDynamicDataValues;

public sealed class User
{
    public string FirstName { get; set; }
    public string LastName { get; set; }
}

#file DynamicDataCases.cs
using System.Collections.Generic;
using System.Reflection;
using LegacyDynamicDataValues;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json.Linq;

namespace LegacyDynamicDataBehavior;

public abstract class BaseSources
{
    public static IEnumerable<object[]> BaseMethod() => [["base-method", 2]];
    public static IEnumerable<object[]> BaseProperty => [["base-property", 6]];
    public static IEnumerable<object[]> ShadowedMethod() => throw new System.InvalidOperationException("The derived source must be selected.");
    public static IEnumerable<object[]> ShadowedProperty => throw new System.InvalidOperationException("The derived source must be selected.");
}

[TestClass]
public class SourceCases : BaseSources
{
    private static readonly IEnumerable<object[]> FieldData = [["field", 5], ["test", 4]];

    [TestMethod]
    [DynamicData(nameof(CurrentMethod), DynamicDataSourceType.Method)]
    public void CurrentMethodSource(string text, int number) => Assert.AreEqual(1, number);

    [TestMethod]
    [DynamicData(nameof(BaseMethod), DynamicDataSourceType.Method)]
    public void InheritedMethodSource(string text, int number) => Assert.AreEqual(2, number);

    [TestMethod]
    [DynamicData(nameof(ShadowedMethod), DynamicDataSourceType.Method)]
    public void ShadowedMethodSource(string text, int number) => Assert.AreEqual(3, number);

    [TestMethod]
    [DynamicData(nameof(AutoMethod))]
    public void AutoDetectedMethodSource(string text, int number) => Assert.AreEqual(4, number);

    [TestMethod]
    [DynamicData(nameof(BaseMethod))]
    public void AutoDetectedInheritedMethodSource(string text, int number) => Assert.AreEqual(2, number);

    [TestMethod]
    [DynamicData(nameof(ShadowedMethod))]
    public void AutoDetectedShadowedMethodSource(string text, int number) => Assert.AreEqual(3, number);

    [TestMethod]
    [DynamicData(nameof(CurrentProperty), DynamicDataSourceType.Property)]
    public void CurrentPropertySource(string text, int number) => Assert.AreEqual(5, number);

    [TestMethod]
    [DynamicData(nameof(BaseProperty), DynamicDataSourceType.Property)]
    public void InheritedPropertySource(string text, int number) => Assert.AreEqual(6, number);

    [TestMethod]
    [DynamicData(nameof(ShadowedProperty), DynamicDataSourceType.Property)]
    public void ShadowedPropertySource(string text, int number) => Assert.AreEqual(7, number);

    [TestMethod]
    [DynamicData(nameof(AutoProperty))]
    public void AutoDetectedPropertySource(string text, int number) => Assert.AreEqual(8, number);

    [TestMethod]
    [DynamicData(nameof(BaseProperty))]
    public void AutoDetectedInheritedPropertySource(string text, int number) => Assert.AreEqual(6, number);

    [TestMethod]
    [DynamicData(nameof(ShadowedProperty))]
    public void AutoDetectedShadowedPropertySource(string text, int number) => Assert.AreEqual(7, number);

    [TestMethod]
    [DynamicData(nameof(ExternalSources.Method), typeof(ExternalSources), DynamicDataSourceType.Method)]
    public void ExternalMethodSource(string text, int number) => Assert.AreEqual(9, number);

    [TestMethod]
    [DynamicData(nameof(ExternalSources.Property), typeof(ExternalSources))]
    public void ExternalPropertySource(string text, int number) => Assert.AreEqual(10, number);

    [TestMethod]
    [DynamicData(nameof(CurrentMethod), DynamicDataSourceType.Method, DynamicDataDisplayName = nameof(LocalDisplay))]
    public void LocalDisplayName(string text, int number) { }

    [TestMethod]
    [DynamicData(nameof(ExternalSources.Method), typeof(ExternalSources), DynamicDataSourceType.Method,
        DynamicDataDisplayName = nameof(ExternalSources.ExternalDisplay),
        DynamicDataDisplayNameDeclaringType = typeof(ExternalSources))]
    public void ExternalDisplayName(string text, int number) { }

    [TestMethod]
    [DynamicData(nameof(CurrentProperty), DynamicDataSourceType.Property, DynamicDataDisplayName = nameof(LocalDisplay))]
    public void LocalPropertyDisplayName(string text, int number) { }

    [TestMethod]
    [DynamicData(nameof(CurrentMethod), DynamicDataSourceType.Method,
        DynamicDataDisplayName = nameof(ExternalSources.ExternalDisplay),
        DynamicDataDisplayNameDeclaringType = typeof(ExternalSources))]
    public void ExternalDisplayForCurrentMethod(string text, int number) { }

    [TestMethod]
    [DynamicData(nameof(CurrentProperty), DynamicDataSourceType.Property,
        DynamicDataDisplayName = nameof(ExternalSources.ExternalDisplay),
        DynamicDataDisplayNameDeclaringType = typeof(ExternalSources))]
    public void ExternalDisplayForCurrentProperty(string text, int number) { }

    [TestMethod]
    [DynamicData(nameof(ExternalSources.Method), typeof(ExternalSources), DynamicDataSourceType.Method,
        DynamicDataDisplayName = nameof(LocalDisplay))]
    public void LocalDisplayForExternalMethod(string text, int number) { }

    [TestMethod]
    [DynamicData(nameof(ExternalSources.Property), typeof(ExternalSources), DynamicDataSourceType.Property,
        DynamicDataDisplayName = nameof(LocalDisplay))]
    public void LocalDisplayForExternalProperty(string text, int number) { }

    [TestMethod]
    [DynamicData(nameof(ExternalSources.Property), typeof(ExternalSources), DynamicDataSourceType.Property,
        DynamicDataDisplayName = nameof(ExternalSources.ExternalDisplay),
        DynamicDataDisplayNameDeclaringType = typeof(ExternalSources))]
    public void ExternalDisplayForExternalProperty(string text, int number) { }

    [TestMethod]
    [DynamicData(nameof(ReferencedUsers), DynamicDataSourceType.Method)]
    public void ReferencedAssemblyValues(string userData, User expectedUser)
    {
        string[] names = userData.Split(';');
        Assert.AreEqual(names[0], expectedUser.FirstName);
        Assert.AreEqual(names[1], expectedUser.LastName);
    }

    [TestMethod]
    [DynamicData(nameof(ExampleTestCases), DynamicDataSourceType.Method)]
    public void StackOverflowRegression(ExampleTestCase exampleTestCase) => Assert.IsNotNull(exampleTestCase.Example);

    [TestMethod]
    [DynamicData(nameof(FieldData), DynamicDataSourceType.Field)]
    public void ExplicitFieldSource(string text, int length) => Assert.AreEqual(length, text.Length);

    [TestMethod]
    [DynamicData(nameof(FieldData))]
    public void AutoDetectedFieldSource(string text, int length) => Assert.AreEqual(length, text.Length);

    [TestMethod]
    [DynamicData(nameof(EvenNumbers))]
    public void SimpleCollection(int value) => Assert.AreEqual(0, value % 2);

    [TestMethod]
    [DynamicData(nameof(StringAndInt), DynamicDataSourceType.Method)]
    public void MethodWithOverload(string text, int number) { }

    [TestMethod]
    [DynamicData(nameof(IntAndString), DynamicDataSourceType.Method)]
    public void MethodWithOverload(int number, string text) { }

    public static IEnumerable<object[]> CurrentMethod() => [["current-method", 1]];
    public static new IEnumerable<object[]> ShadowedMethod() => [["derived-method", 3]];
    public static IEnumerable<object[]> AutoMethod() => [["auto-method", 4]];
    public static IEnumerable<object[]> CurrentProperty => [["current-property", 5]];
    public static new IEnumerable<object[]> ShadowedProperty => [["derived-property", 7]];
    public static IEnumerable<object[]> AutoProperty => [["auto-property", 8]];
    public static IEnumerable<int> EvenNumbers => [0, 2, 4];
    public static IEnumerable<object[]> StringAndInt() => [["one", 1], ["two", 2]];
    public static IEnumerable<object[]> IntAndString() => [[1, "one"], [2, "two"]];
    public static IEnumerable<object[]> ReferencedUsers() =>
    [
        ["John;Doe", new User { FirstName = "John", LastName = "Doe" }],
        ["Jane;Doe", new User { FirstName = "Jane", LastName = "Doe" }],
    ];

    public static IEnumerable<object[]> ExampleTestCases() =>
    [
        [
            new ExampleTestCase
            {
                Example = new ExampleClass
                {
                    JTokenDictionary = new Dictionary<string, JToken>
                    {
                        ["names"] = new JArray("Jane", "John"),
                    },
                },
            },
        ],
    ];

    public static string LocalDisplay(MethodInfo methodInfo, object[] data)
        => $"Custom {methodInfo.Name} with {data.Length} parameters";

    public sealed class ExampleClass
    {
        public IDictionary<string, JToken> JTokenDictionary { get; set; }
    }

    public sealed class ExampleTestCase
    {
        public ExampleClass Example { get; set; }
    }
}

public static class ExternalSources
{
    public static IEnumerable<object[]> Method() => [["external-method", 9]];
    public static IEnumerable<object[]> Property => [["external-property", 10]];

    public static string ExternalDisplay(MethodInfo methodInfo, object[] data)
        => $"External {methodInfo.Name} with {data.Length} parameters";
}

[TestClass]
public class CategoryCases
{
    [TestCategory("DynamicDataWithCategory")]
    [TestMethod]
    [DynamicData(nameof(Data))]
    public void CategorizedDynamicData(string text, int number) => Assert.IsGreaterThan(0, number);

    public static IEnumerable<object[]> Data => [["John;Doe", 1], ["Jane;Doe", 2]];
}

[TestClass]
public class FoldedCases
{
    [TestMethod(UnfoldingStrategy = TestDataSourceUnfoldingStrategy.Fold)]
    [DynamicData(nameof(CurrentProperty))]
    public void PropertySourceOnCurrentType(int number, string text) { }

    [TestMethod(UnfoldingStrategy = TestDataSourceUnfoldingStrategy.Fold)]
    [DynamicData(nameof(CurrentMethod), DynamicDataSourceType.Method)]
    public void MethodSourceOnCurrentType(int number, string text) { }

    [TestMethod(UnfoldingStrategy = TestDataSourceUnfoldingStrategy.Fold)]
    [DynamicData(nameof(FoldedExternalSources.Property), typeof(FoldedExternalSources))]
    public void PropertySourceOnDifferentType(int number, string text) { }

    [TestMethod(UnfoldingStrategy = TestDataSourceUnfoldingStrategy.Fold)]
    [DynamicData(nameof(FoldedExternalSources.Method), typeof(FoldedExternalSources), DynamicDataSourceType.Method)]
    public void MethodSourceOnDifferentType(int number, string text) { }

    private static IEnumerable<object[]> CurrentProperty => CurrentMethod();
    private static IEnumerable<object[]> CurrentMethod() => [[1, "a"], [2, "b"]];
}

public static class FoldedExternalSources
{
    public static IEnumerable<object[]> Property => Method();
    public static IEnumerable<object[]> Method() => [[3, "c"], [4, "d"]];
}
""";
    }
}
