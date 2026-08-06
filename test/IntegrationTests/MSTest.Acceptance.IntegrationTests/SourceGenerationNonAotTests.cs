// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Acceptance.IntegrationTests;
using Microsoft.Testing.Platform.Acceptance.IntegrationTests.Helpers;

namespace MSTest.Acceptance.IntegrationTests;

/// <summary>
/// MSTest.SourceGeneration is most commonly used in Native AOT and trimming scenarios
/// (see <see cref="NativeAotTests"/> and <see cref="TrimTests"/>), but the package itself
/// is a plain Roslyn source generator that should work in any SDK-style project. These
/// acceptance tests pin that behavior: referencing the package on a non-AOT, non-trimmed
/// project must still build, emit the generated metadata file, and successfully run tests
/// through the source-generated <c>ReflectionMetadataHook</c> module initializer.
/// </summary>
[TestClass]
public class SourceGenerationNonAotTests : AcceptanceTestBase<NopAssetFixture>
{
    private const string AssetName = "MSTestSourceGenNonAot";
    private const string SdkAssetName = "MSTestSourceGenSdkNonAot";

    // Source code for a non-AOT, non-trimmed test project that references MSTest.SourceGeneration.
    // EmitCompilerGeneratedFiles is enabled so we can statically assert the generator ran and
    // wrote out the expected metadata file alongside the assembly.
    private const string SourceCode = """
#file MSTestSourceGenNonAot.csproj
<Project Sdk="Microsoft.NET.Sdk">
    <PropertyGroup>
        <TargetFramework>$TargetFramework$</TargetFramework>
        <ImplicitUsings>enable</ImplicitUsings>
        <Nullable>enable</Nullable>
        <OutputType>Exe</OutputType>
        <LangVersion>preview</LangVersion>
        <EnableMSTestRunner>true</EnableMSTestRunner>
        <!-- Persist the generator output to disk so the test can assert it was emitted. -->
        <EmitCompilerGeneratedFiles>true</EmitCompilerGeneratedFiles>
    </PropertyGroup>
    <ItemGroup>
        <PackageReference Include="Microsoft.Testing.Platform" Version="$MicrosoftTestingPlatformVersion$" />
        <PackageReference Include="MSTest.SourceGeneration" Version="$MSTestSourceGenerationVersion$" />
        <PackageReference Include="MSTest.TestAdapter" Version="$MSTestVersion$" />
        <PackageReference Include="MSTest.TestFramework" Version="$MSTestVersion$" />
    </ItemGroup>
</Project>

#file TestClass1.cs
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MyTests;

[TestClass]
public class UnitTest1
{
    [TestMethod]
    public void TestMethod1()
    {
    }

    [TestMethod]
    [DataRow(0, 1)]
    public void TestMethod2(int a, int b)
    {
    }

    // Exercises the source-generated DynamicData accessor (property source) at runtime: if the generated
    // DynamicDataSourceResolver registration produced the wrong data, this test would fail or not run.
    public static IEnumerable<object[]> Data => new[] { new object[] { 1, 2 }, new object[] { 3, 4 } };

    [TestMethod]
    [DynamicData(nameof(Data))]
    public void TestMethod3(int a, int b)
    {
        Assert.AreEqual(a + 1, b);
    }
}
""";

    private const string CentralPackageManagementSourceCode = """

#file Directory.Packages.props
<Project>
    <PropertyGroup>
        <ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>
        <CentralPackageVersionOverrideEnabled>false</CentralPackageVersionOverrideEnabled>
    </PropertyGroup>
</Project>
""";

    private const string SdkSourceCode = """
#file MSTestSourceGenSdkNonAot.csproj
<Project Sdk="MSTest.Sdk/$MSTestVersion$">
    <PropertyGroup>
        <TargetFramework>$TargetFramework$</TargetFramework>
        <IsTestApplication>$IsTestApplication$</IsTestApplication>
        <PublishAot>$PublishAot$</PublishAot>
        <EnableMicrosoftTestingPlatform>$IsTestApplication$</EnableMicrosoftTestingPlatform>
        <EnableMSTestSourceGeneration>true</EnableMSTestSourceGeneration>
        <EmitCompilerGeneratedFiles>true</EmitCompilerGeneratedFiles>
    </PropertyGroup>
    $ExtraItems$
</Project>

#file TestClass1.cs
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MyTests;

[TestClass]
public class UnitTest1
{
    [TestMethod]
    public void TestMethod1()
    {
    }
}
""";

    private const string TestingPlatformPackageReference = """
<ItemGroup>
    <PackageReference Include="Microsoft.Testing.Platform" Version="$MicrosoftTestingPlatformVersion$" />
</ItemGroup>
""";

    [TestMethod]
    [DynamicData(nameof(TargetFrameworks.NetForDynamicData), typeof(TargetFrameworks))]
    public async Task SourceGenerationNonAot_BuildsAndRunsTests_WithExitCodeZero(string tfm)
    {
        using TestAsset generator = await TestAsset.GenerateAssetAsync(
            $"{AssetName}_{tfm}",
            SourceCode
            .PatchCodeWithReplace("$MicrosoftTestingPlatformVersion$", MicrosoftTestingPlatformVersion)
            .PatchCodeWithReplace("$TargetFramework$", tfm)
            .PatchCodeWithReplace("$MSTestVersion$", MSTestVersion)
            .PatchCodeWithReplace("$MSTestSourceGenerationVersion$", MSTestSourceGenerationVersion),
            addPublicFeeds: true);

        // Plain `dotnet build` — no -r RID, no publish, no PublishAot, no PublishTrimmed.
        // This is the scenario the follow-up review surfaced: the source generator must also
        // work for users who opt in to source-generated discovery without trimming/AOT.
        DotnetMuxerResult buildResult = await DotnetCli.RunAsync(
            $"build {generator.TargetAssetPath} -c {BuildConfiguration.Release} -f {tfm}",
            cancellationToken: TestContext.CancellationToken);
        buildResult.AssertExitCodeIs(0);

        // Static evidence the source generator actually ran in the build (not just that
        // the package was restored). EmitCompilerGeneratedFiles writes the generator
        // output under obj/<config>/<tfm>/generated/<generator-assembly>/<full-type-name>/<hintname>.
        // The emitted hint name depends on which generator ran, and that is selected by the
        // MSTestSourceGenMode default (ReflectionFree) supplied by MSTest.TestAdapter.targets:
        //   - Rooting        -> '<AssemblyName>.MSTestReflectionMetadata.g.cs'
        //   - ReflectionFree -> 'MSTestReflectionMetadata.Registry.g.cs' (plus SupportTypes/Registration)
        // Both contain 'MSTestReflectionMetadata' and end with '.g.cs', so match either with a glob
        // to keep this smoke test independent of the default mode.
        string objGenerated = Path.Combine(generator.TargetAssetPath, "obj", "Release", tfm, "generated");
        string[] generatedFiles = Directory.Exists(objGenerated)
            ? Directory.GetFiles(objGenerated, "*MSTestReflectionMetadata*.g.cs", SearchOption.AllDirectories)
            : [];
        Assert.IsNotEmpty(generatedFiles, $"the source generator should have emitted a '*MSTestReflectionMetadata*.g.cs' file under '{objGenerated}'");

        // Behavioral evidence: tests still discover and run when the source-generated
        // ReflectionMetadataHook is the only metadata provider wired in at module init.
        // If the hook crashed during ModuleInitializer or swapped in a broken provider, the
        // test host would fail before printing a summary. (We assert the full count to also
        // catch silent discovery regressions where tests are not picked up.)
        var testHost = TestHost.LocateFrom(generator.TargetAssetPath, AssetName, tfm, buildConfiguration: BuildConfiguration.Release);
        TestHostResult testHostResult = await testHost.ExecuteAsync(cancellationToken: TestContext.CancellationToken);
        testHostResult.AssertOutputContainsSummary(failed: 0, passed: 4, skipped: 0);
        testHostResult.AssertExitCodeIs(0);
    }

    [TestMethod]
    [DataRow(false, false)]
    [DataRow(false, true)]
    [DataRow(true, false)]
    [DataRow(true, true)]
    public async Task MSTestSdk_SourceGenerationOptIn_BuildsAndRunsTests(bool isTestApplication, bool useCentralPackageManagement)
    {
        string tfm = TargetFrameworks.NetCurrent;
        using TestAsset generator = await TestAsset.GenerateAssetAsync(
            $"{SdkAssetName}_{isTestApplication}_{useCentralPackageManagement}",
            (useCentralPackageManagement ? SdkSourceCode + CentralPackageManagementSourceCode : SdkSourceCode)
            .PatchCodeWithReplace("$TargetFramework$", tfm)
            .PatchCodeWithReplace("$IsTestApplication$", isTestApplication.ToString().ToLowerInvariant())
            .PatchCodeWithReplace("$PublishAot$", "false")
            .PatchCodeWithReplace("$ExtraItems$", string.Empty)
            .PatchCodeWithReplace("$MSTestVersion$", MSTestVersion),
            addPublicFeeds: true);

        DotnetMuxerResult buildResult = await DotnetCli.RunAsync(
            $"build {generator.TargetAssetPath} -c {BuildConfiguration.Release} -f {tfm}",
            cancellationToken: TestContext.CancellationToken);
        buildResult.AssertExitCodeIs(0);

        string objGenerated = Path.Combine(generator.TargetAssetPath, "obj", "Release", tfm, "generated");
        string[] generatedFiles = Directory.Exists(objGenerated)
            ? Directory.GetFiles(objGenerated, "*MSTestReflectionMetadata*.g.cs", SearchOption.AllDirectories)
            : [];
        Assert.IsNotEmpty(generatedFiles, $"the MSTest.Sdk source-generation opt-in should have emitted metadata under '{objGenerated}'");

        if (!isTestApplication)
        {
            return;
        }

        var testHost = TestHost.LocateFrom(generator.TargetAssetPath, SdkAssetName, tfm, buildConfiguration: BuildConfiguration.Release);
        TestHostResult testHostResult = await testHost.ExecuteAsync(cancellationToken: TestContext.CancellationToken);
        testHostResult.AssertOutputContainsSummary(failed: 0, passed: 1, skipped: 0);
        testHostResult.AssertExitCodeIs(0);
    }

    [TestMethod]
    public async Task MSTestSdk_SourceGenerationOptIn_NativeAotLibraryBuilds()
    {
        string tfm = TargetFrameworks.NetCurrent;
        using TestAsset generator = await TestAsset.GenerateAssetAsync(
            $"{SdkAssetName}_NativeAotLibrary",
            SdkSourceCode
            .PatchCodeWithReplace("$TargetFramework$", tfm)
            .PatchCodeWithReplace("$IsTestApplication$", "false")
            .PatchCodeWithReplace("$PublishAot$", "true")
            .PatchCodeWithReplace("$ExtraItems$", TestingPlatformPackageReference)
            .PatchCodeWithReplace("$MicrosoftTestingPlatformVersion$", MicrosoftTestingPlatformVersion)
            .PatchCodeWithReplace("$MSTestVersion$", MSTestVersion),
            addPublicFeeds: true);

        DotnetMuxerResult buildResult = await DotnetCli.RunAsync(
            $"build {generator.TargetAssetPath} -c {BuildConfiguration.Release}",
            cancellationToken: TestContext.CancellationToken);
        buildResult.AssertExitCodeIs(0);

        string objGenerated = Path.Combine(generator.TargetAssetPath, "obj", "Release", tfm, "generated");
        string[] generatedFiles = Directory.Exists(objGenerated)
            ? Directory.GetFiles(objGenerated, "*MSTestReflectionMetadata*.g.cs", SearchOption.AllDirectories)
            : [];
        Assert.IsNotEmpty(generatedFiles, $"the NativeAOT library source generator should have emitted metadata under '{objGenerated}'");
    }

    [TestMethod]
    public async Task MSTestSdk_SourceGenerationOptIn_RejectsNetStandardTestLibrary()
    {
        using TestAsset generator = await TestAsset.GenerateAssetAsync(
            $"{SdkAssetName}_NetStandard",
            SdkSourceCode
            .PatchCodeWithReplace("$TargetFramework$", "netstandard2.0")
            .PatchCodeWithReplace("$IsTestApplication$", "false")
            .PatchCodeWithReplace("$PublishAot$", "false")
            .PatchCodeWithReplace("$ExtraItems$", string.Empty)
            .PatchCodeWithReplace("$MSTestVersion$", MSTestVersion),
            addPublicFeeds: true);

        DotnetMuxerResult buildResult = await DotnetCli.RunAsync(
            $"build {generator.TargetAssetPath} -c {BuildConfiguration.Release}",
            failIfReturnValueIsNotZero: false,
            cancellationToken: TestContext.CancellationToken);
        buildResult.AssertExitCodeIs(1);
        buildResult.AssertOutputContains(
            "'EnableMSTestSourceGeneration' is not supported for .NET Standard target frameworks because the required MSTest.TestAdapter runtime hooks are unavailable.");
    }

    public TestContext TestContext { get; set; } = null!;

    private const string MetadataSourceCode = """
#file MSTestSourceGenNonAotMetadata.csproj
<Project Sdk="Microsoft.NET.Sdk">
    <PropertyGroup>
        <TargetFramework>$TargetFramework$</TargetFramework>
        <ImplicitUsings>enable</ImplicitUsings>
        <Nullable>enable</Nullable>
        <OutputType>Exe</OutputType>
        <LangVersion>preview</LangVersion>
        <EnableMSTestRunner>true</EnableMSTestRunner>
        <MSTestSourceGenMode>ReflectionFree</MSTestSourceGenMode>
        <EmitCompilerGeneratedFiles>true</EmitCompilerGeneratedFiles>
    </PropertyGroup>
    <ItemGroup>
        <PackageReference Include="Microsoft.Testing.Platform" Version="$MicrosoftTestingPlatformVersion$" />
        <PackageReference Include="MSTest.SourceGeneration" Version="$MSTestSourceGenerationVersion$" />
        <PackageReference Include="MSTest.TestAdapter" Version="$MSTestVersion$" />
        <PackageReference Include="MSTest.TestFramework" Version="$MSTestVersion$" />
    </ItemGroup>
</Project>

#file MetadataTests.cs
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MyTests;

[TestClass]
public sealed class MethodMetadataTests
{
    [TestMethod]
    [TestCategory("GeneratedMetadata")]
    public void CategorizedTest()
    {
    }

    [TestMethod]
    [Ignore("generated method ignore")]
    public void IgnoredMethod()
        => Assert.Fail("The generated method-level IgnoreAttribute was not honored.");
}

[TestCategory("InheritedTypeMetadata")]
public abstract class CategorizedBase
{
}

[TestClass]
public sealed class InheritedCategorizedTests : CategorizedBase
{
    [TestMethod]
    public void CategorizedThroughBaseClass()
    {
    }
}
""";

    [TestMethod]
    [DynamicData(nameof(TargetFrameworks.NetForDynamicData), typeof(TargetFrameworks))]
    public async Task SourceGenerationNonAot_HonorsGeneratedInheritedTypeAndMethodMetadata(string tfm)
    {
        using TestAsset generator = await TestAsset.GenerateAssetAsync(
            $"{AssetName}Metadata_{tfm}",
            MetadataSourceCode
            .PatchCodeWithReplace("$MicrosoftTestingPlatformVersion$", MicrosoftTestingPlatformVersion)
            .PatchCodeWithReplace("$TargetFramework$", tfm)
            .PatchCodeWithReplace("$MSTestVersion$", MSTestVersion)
            .PatchCodeWithReplace("$MSTestSourceGenerationVersion$", MSTestSourceGenerationVersion),
            addPublicFeeds: true);

        DotnetMuxerResult buildResult = await DotnetCli.RunAsync(
            $"build {generator.TargetAssetPath} -c {BuildConfiguration.Release} -f {tfm}",
            cancellationToken: TestContext.CancellationToken);
        buildResult.AssertExitCodeIs(0);

        string objGenerated = Path.Combine(generator.TargetAssetPath, "obj", "Release", tfm, "generated");
        string[] generatedFiles = Directory.Exists(objGenerated)
            ? Directory.GetFiles(objGenerated, "*MSTestReflectionMetadata*.g.cs", SearchOption.AllDirectories)
            : [];
        Assert.IsNotEmpty(generatedFiles, $"the reflection-free source generator should have emitted metadata under '{objGenerated}'");

        var testHost = TestHost.LocateFrom(generator.TargetAssetPath, "MSTestSourceGenNonAotMetadata", tfm, buildConfiguration: BuildConfiguration.Release);

        TestHostResult fullRun = await testHost.ExecuteAsync(cancellationToken: TestContext.CancellationToken);
        fullRun.AssertOutputContainsSummary(failed: 0, passed: 2, skipped: 1);
        fullRun.AssertExitCodeIs(0);

        TestHostResult categoryRun = await testHost.ExecuteAsync(
            "--filter TestCategory=GeneratedMetadata",
            cancellationToken: TestContext.CancellationToken);
        categoryRun.AssertOutputContainsSummary(failed: 0, passed: 1, skipped: 0);
        categoryRun.AssertExitCodeIs(0);

        TestHostResult inheritedCategoryRun = await testHost.ExecuteAsync(
            "--filter TestCategory=InheritedTypeMetadata",
            cancellationToken: TestContext.CancellationToken);
        inheritedCategoryRun.AssertOutputContainsSummary(failed: 0, passed: 1, skipped: 0);
        inheritedCategoryRun.AssertExitCodeIs(0);
    }
}
