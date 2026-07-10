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
}
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
            .PatchCodeWithReplace("$MSTestSourceGenerationVersion$", MSTestSourceGenerationVersion));

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
        testHostResult.AssertOutputContainsSummary(failed: 0, passed: 2, skipped: 0);
        testHostResult.AssertExitCodeIs(0);
    }

    public TestContext TestContext { get; set; } = null!;
}
