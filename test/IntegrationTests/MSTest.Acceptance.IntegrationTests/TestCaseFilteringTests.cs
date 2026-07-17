// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Immutable;
using System.Runtime.Loader;

using Microsoft.MSTestV2.CLIAutomation;
using Microsoft.Testing.Platform.Acceptance.IntegrationTests;
using Microsoft.VisualStudio.TestPlatform.ObjectModel;

using TestResult = Microsoft.VisualStudio.TestPlatform.ObjectModel.TestResult;

namespace MSTest.Acceptance.IntegrationTests;

/// <summary>
/// Regression guard for filtering over the neutral <c>UnitTestElement</c>. Discovery matches
/// <c>element.ToTestCase()</c>, while execution reconstructs a <c>UnitTestElement</c> from each incoming
/// <see cref="TestCase"/> before matching. These tests preserve the object-model assertions around the
/// <c>TestCase</c> to <c>UnitTestElement</c> round trip for the two identity properties most sensitive to it.
/// </summary>
[TestClass]
public sealed class TestCaseFilteringTests : AcceptanceTestBase<TestCaseFilteringTests.TestAssetFixture>
{
    private const string AssetName = "DiscoverInternalsFiltering";
    private const string TargetDisplayName = "TopLevelInternalClass_TestMethod1";
    private static readonly Lock AssetAssemblyLock = new();
    private static Assembly? s_assetAssembly;

    [TestMethod]
    public void FilterByFullyQualifiedNameSelectsExactlyTheMatchingTestDuringDiscovery()
    {
        TestCase target = GetTargetTestCase();

        ImmutableArray<TestCase> filtered = DiscoverTests($"FullyQualifiedName={target.FullyQualifiedName}");

        Assert.HasCount(1, filtered);
        Assert.AreEqual(target.FullyQualifiedName, filtered[0].FullyQualifiedName);
        Assert.AreEqual(target.Id, filtered[0].Id);
    }

    [TestMethod]
    public void FilterByIdSelectsExactlyTheMatchingTestDuringDiscovery()
    {
        TestCase target = GetTargetTestCase();

        ImmutableArray<TestCase> filtered = DiscoverTests($"Id={target.Id}");

        Assert.HasCount(1, filtered);
        Assert.AreEqual(target.FullyQualifiedName, filtered[0].FullyQualifiedName);
        Assert.AreEqual(target.Id, filtered[0].Id);
    }

    [TestMethod]
    public async Task FilterByFullyQualifiedNameSelectsExactlyTheMatchingTestDuringExecution()
    {
        ImmutableArray<TestCase> allTests = DiscoverTests();
        TestCase target = allTests.First(testCase => testCase.DisplayName == TargetDisplayName);

        SimulateCrossProcessTestCases(allTests);

        ImmutableArray<TestResult> results = await CLITestBase.RunTestsAsync(
            allTests,
            $"FullyQualifiedName={target.FullyQualifiedName}");

        Assert.HasCount(1, results);
        Assert.AreEqual(target.FullyQualifiedName, results[0].TestCase.FullyQualifiedName);
        Assert.AreEqual(TestOutcome.Passed, results[0].Outcome);
    }

    [TestMethod]
    public async Task FilterByIdSelectsExactlyTheMatchingTestDuringExecution()
    {
        ImmutableArray<TestCase> allTests = DiscoverTests();
        Guid targetId = allTests.First(testCase => testCase.DisplayName == TargetDisplayName).Id;

        SimulateCrossProcessTestCases(allTests);

        ImmutableArray<TestResult> results = await CLITestBase.RunTestsAsync(allTests, $"Id={targetId}");

        Assert.HasCount(1, results);
        Assert.AreEqual(targetId, results[0].TestCase.Id);
        Assert.AreEqual(TestOutcome.Passed, results[0].Outcome);
    }

    private static TestCase GetTargetTestCase()
        => DiscoverTests().First(testCase => testCase.DisplayName == TargetDisplayName);

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

    // Deserialized VSTest test cases carry no in-process adapter payload. Clearing LocalExtensionData
    // deliberately forces execution to reconstruct UnitTestElement and regenerate TestCase identity.
    private static void SimulateCrossProcessTestCases(ImmutableArray<TestCase> testCases)
    {
        foreach (TestCase testCase in testCases)
        {
            testCase.LocalExtensionData = null;
        }
    }

    public sealed class TestAssetFixture : TestAssetFixtureBase
    {
        public string TargetAssetPath => GetAssetPath(AssetName);

        public string AssemblyPath
            => Path.Combine(TargetAssetPath, "bin", "Release", TargetFrameworks.NetCurrent, $"{AssetName}.dll");

        public override (string ID, string Name, string Code) GetAssetsToGenerate() => (AssetName, AssetName,
                SourceCode
                .PatchTargetFrameworks(TargetFrameworks.NetCurrent)
                .PatchCodeWithReplace("$MSTestVersion$", MSTestVersion));

        private const string SourceCode = """
#file DiscoverInternalsFiltering.csproj
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <IsTestProject>true</IsTestProject>
    <TargetFrameworks>$TargetFrameworks$</TargetFrameworks>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="MSTest.TestAdapter" Version="$MSTestVersion$" />
    <PackageReference Include="MSTest.TestFramework" Version="$MSTestVersion$" />
  </ItemGroup>

</Project>

#file UnitTest1.cs
using Microsoft.VisualStudio.TestTools.UnitTesting;

[assembly: DiscoverInternals]

namespace DiscoverInternalsFiltering;

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
""";
    }
}
