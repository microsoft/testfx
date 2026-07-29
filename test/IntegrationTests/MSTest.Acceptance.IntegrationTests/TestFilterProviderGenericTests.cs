// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Acceptance.IntegrationTests;
using Microsoft.Testing.Platform.Acceptance.IntegrationTests.Helpers;
using Microsoft.Testing.Platform.Helpers;

namespace MSTest.Acceptance.IntegrationTests;

/// <summary>
/// End-to-end coverage for the type-safe <c>[assembly: TestFilterProvider&lt;TFilter&gt;]</c> form.
/// </summary>
/// <remarks>
/// The generic attribute derives from the non-generic one, but its <c>CustomAttributeData</c> reports the
/// <em>constructed</em> type, whose <c>FullName</c> embeds the type argument. The adapter's cheap
/// metadata-only marker probe therefore has to compare against the generic type definition; this test is
/// what proves that probe (and the reflection lookup behind it) actually finds the generic form at run time.
/// It only targets .NET because .NET Framework reflection cannot materialize generic custom attributes.
/// </remarks>
[TestClass]
public sealed class TestFilterProviderGenericTests : AcceptanceTestBase<TestFilterProviderGenericTests.TestAssetFixture>
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task GenericTestFilterProvider_IsDiscoveredAndApplied()
    {
        var testHost = TestHost.LocateFrom(AssetFixture.ProjectPath, TestAssetFixture.ProjectName, TargetFrameworks.NetCurrent);

        TestHostResult testHostResult = await testHost.ExecuteAsync(cancellationToken: TestContext.CancellationToken);

        // The filter skips SkippedTest, drops DroppedTest, and lets RunTest through.
        testHostResult.AssertExitCodeIs(ExitCode.Success);
        testHostResult.AssertOutputContainsSummary(failed: 0, passed: 1, skipped: 1);
        testHostResult.AssertOutputContains("Skipped by the generic provider");
    }

    public sealed class TestAssetFixture() : TestAssetFixtureBase()
    {
        public const string ProjectName = "TestFilterProviderGeneric";

        public string ProjectPath => GetAssetPath(ProjectName);

        public override (string ID, string Name, string Code) GetAssetsToGenerate() => (ProjectName, ProjectName,
                SourceCode
                .PatchTargetFrameworks(TargetFrameworks.NetCurrent)
                .PatchCodeWithReplace("$MSTestVersion$", MSTestVersion));

        private const string SourceCode = """
#file TestFilterProviderGeneric.csproj
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

[assembly: TestFilterProvider<MyFilter>]

public sealed class MyFilter : ITestFilter
{
    public TestFilterResult Filter(TestFilterContext context)
        => context.MethodName switch
        {
            "SkippedTest" => TestFilterResult.Skip("Skipped by the generic provider"),
            "DroppedTest" => TestFilterResult.Drop,
            _ => TestFilterResult.Run,
        };
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
