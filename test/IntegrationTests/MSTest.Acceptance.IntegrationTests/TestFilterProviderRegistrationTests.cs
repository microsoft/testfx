// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Combinatorial.MSTest;

using Microsoft.Testing.Platform.Acceptance.IntegrationTests;
using Microsoft.Testing.Platform.Acceptance.IntegrationTests.Helpers;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.TestInfrastructure;

namespace MSTest.Acceptance.IntegrationTests;

/// <summary>
/// End-to-end coverage for both shapes that register an <c>ITestFilter</c>: the original
/// <c>[assembly: TestFilterProvider(typeof(TFilter))]</c> and the type-safe
/// <c>[assembly: TestFilterProvider&lt;TFilter&gt;]</c>.
/// </summary>
/// <remarks>
/// <para>
/// The adapter guards discovery with a metadata-only marker probe, preserves the original concrete
/// <c>TestFilterProviderAttribute</c> lookup for the shipped non-generic shape, and uses the internal
/// <c>ITestFilterProviderAttribute</c> contract for the generic shape. This test proves those paths resolve
/// each shape at run time — in particular that a constructed generic attribute, whose
/// <c>CustomAttributeData</c> reports a <c>FullName</c> embedding the type argument, is still recognized.
/// </para>
/// <para>
/// The asset multi-targets and selects the shape with <c>#if NET</c>, so a single run covers the generic
/// form on .NET and the non-generic form on .NET Framework (where generic custom attributes cannot be
/// materialized at all, which is why the generic attribute is not shipped there). That also makes the
/// asset the worked example of how to use the type-safe form from multi-targeted test projects.
/// </para>
/// </remarks>
[TestClass]
public sealed class TestFilterProviderRegistrationTests : AcceptanceTestBase<TestFilterProviderRegistrationTests.TestAssetFixture>
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    [DynamicData(nameof(TargetFrameworks.AllForDynamicData), typeof(TargetFrameworks))]
    public async Task TestFilterProvider_IsDiscoveredAndApplied(string currentTfm)
    {
        var testHost = TestHost.LocateFrom(AssetFixture.ProjectPath, TestAssetFixture.ProjectName, currentTfm);

        TestHostResult testHostResult = await testHost.ExecuteAsync(cancellationToken: TestContext.CancellationToken);

        // The filter skips SkippedTest, drops DroppedTest, and lets RunTest through.
        testHostResult.AssertExitCodeIs(ExitCode.Success);
        testHostResult.AssertOutputContainsSummary(failed: 0, passed: 1, skipped: 1);
        testHostResult.AssertOutputContains("Skipped by the provider");
    }

    // Reflection is only one of the metadata paths. Under AotSourceGeneration the adapter serves
    // assembly attributes from a materialized set through SourceGeneratedReflectionOperations, which
    // filters with Type.IsInstanceOfType rather than reflecting -- a genuinely different route for both
    // the concrete-type and the interface lookup this PR introduced. Pin the same behaviour on every
    // mode the harness runs, so the reflection-free path is exercised and not merely compiled.
    [TestMethod]
    [CombinatorialData]
    public async Task TestFilterProvider_IsDiscoveredAndApplied_UnderEveryMetadataMode([MetadataModeValues] MetadataMode metadataMode)
    {
        var testHost = TestHost.LocateFrom(AssetFixture.ProjectPath, TestAssetFixture.ProjectName, TargetFrameworks.NetCurrent, metadataMode: metadataMode);

        TestHostResult testHostResult = await testHost.ExecuteAsync(cancellationToken: TestContext.CancellationToken);

        testHostResult.AssertExitCodeIs(ExitCode.Success);
        testHostResult.AssertOutputContainsSummary(failed: 0, passed: 1, skipped: 1);
        testHostResult.AssertOutputContains("Skipped by the provider");
    }

    public sealed class TestAssetFixture() : TestAssetFixtureBase()
    {
        public const string ProjectName = "TestFilterProviderRegistration";

        // AotSourceGeneration is opt-in because it has not been validated across the whole corpus, but it
        // is the only mode that publishes a materialized assembly-attribute set -- exactly what the
        // provider lookup reads -- so this asset builds and runs against it.
        protected override IReadOnlyList<MetadataMode> SourceGenMetadataModes { get; }
            = [MetadataMode.SourceGeneration, MetadataMode.AotSourceGeneration];

        public string ProjectPath => GetAssetPath(ProjectName);

        public override (string ID, string Name, string Code) GetAssetsToGenerate() => (ProjectName, ProjectName,
                SourceCode
                .PatchTargetFrameworks(TargetFrameworks.All)
                .PatchCodeWithReplace("$MSTestVersion$", MSTestVersion));

        private const string SourceCode = """
#file TestFilterProviderRegistration.csproj
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <EnableMSTestRunner>true</EnableMSTestRunner>
    <TargetFrameworks>$TargetFrameworks$</TargetFrameworks>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="MSTest.TestAdapter" Version="$MSTestVersion$" />
    <PackageReference Include="MSTest.TestFramework" Version="$MSTestVersion$" />
  </ItemGroup>

</Project>

#file MyFilter.cs
using Microsoft.VisualStudio.TestTools.UnitTesting;

#pragma warning disable MSTESTEXP

// TestFilterProviderAttribute<TFilter> is only shipped in the .NET assets of MSTest.TestFramework, so a
// multi-targeted test project selects the shape it can use. This is the documented pattern.
#if NET
[assembly: TestFilterProvider<MyFilter>]
#else
[assembly: TestFilterProvider(typeof(MyFilter))]
#endif

public sealed class MyFilter : ITestFilter
{
    public TestFilterResult Filter(TestFilterContext context)
    {
        switch (context.MethodName)
        {
            case "SkippedTest":
                return TestFilterResult.Skip("Skipped by the provider");

            case "DroppedTest":
                return TestFilterResult.Drop;

            default:
                return TestFilterResult.Run;
        }
    }
}

#file UnitTest1.cs
using Microsoft.VisualStudio.TestTools.UnitTesting;

[TestClass]
public class MyTests
{
    [TestMethod]
    public void RunTest() { }

    [TestMethod]
    public void SkippedTest() { }

    [TestMethod]
    public void DroppedTest() { }
}
""";
    }
}
