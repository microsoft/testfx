// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Acceptance.IntegrationTests;
using Microsoft.Testing.Platform.Acceptance.IntegrationTests.Helpers;

namespace MSTest.Acceptance.IntegrationTests;

[TestClass]
public class TrimTests : AcceptanceTestBase<NopAssetFixture>
{
    // Source code for a minimal project that deeply validates trim/AOT compatibility of
    // MSTest assemblies by using TrimmerRootAssembly to force the trimmer to analyze
    // all code paths in each assembly, not just those reachable from the test entry point.
    // See https://learn.microsoft.com/dotnet/core/deploying/trimming/prepare-libraries-for-trimming
    private const string TrimAnalysisSourceCode = """
#file MSTestTrimAnalysisTest.csproj
<Project Sdk="Microsoft.NET.Sdk">
    <PropertyGroup>
        <TargetFramework>$TargetFramework$</TargetFramework>
        <OutputType>Exe</OutputType>
        <PublishTrimmed>true</PublishTrimmed>
        <!-- Show individual trim warnings instead of a single IL2104 per assembly -->
        <TrimmerSingleWarn>false</TrimmerSingleWarn>
    </PropertyGroup>
    <ItemGroup>
        <PackageReference Include="Microsoft.Testing.Platform" Version="$MicrosoftTestingPlatformVersion$" />
        <PackageReference Include="MSTest.Engine" Version="$MSTestEngineVersion$" />
        <PackageReference Include="MSTest.SourceGeneration" Version="$MSTestEngineVersion$" />
        <PackageReference Include="MSTest.TestFramework" Version="$MSTestVersion$" />
    </ItemGroup>
    <!-- Force the trimmer to analyze the full assembly surface, not just reachable code paths.
         MSTest.SourceGeneration is a source generator with no runtime assembly so it cannot be a trimmer root.
         MSTest.TestFramework has known reflection-heavy code paths (DynamicData, etc.) that are not yet trim-safe. -->
    <ItemGroup>
        <TrimmerRootAssembly Include="MSTest.Engine" />
    </ItemGroup>
</Project>

#file Program.cs
System.Console.WriteLine("This project validates trim/AOT compatibility via dotnet publish.");
""";

    [TestMethod]
    [DynamicData(nameof(TargetFrameworks.NetForDynamicData), typeof(TargetFrameworks))]
    public async Task Publish_ShouldNotProduceTrimWarnings(string tfm)
    {
        // See https://github.com/microsoft/testfx/issues/7153
        // This test forces deep trim analysis of MSTest assemblies using TrimmerRootAssembly
        // to catch trim warnings that would not be caught by only testing reachable code paths.
        using TestAsset generator = await TestAsset.GenerateAssetAsync(
            $"MSTestTrimAnalysisTest_{tfm}",
            TrimAnalysisSourceCode
            .PatchCodeWithReplace("$MicrosoftTestingPlatformVersion$", MicrosoftTestingPlatformVersion)
            .PatchCodeWithReplace("$MSTestVersion$", MSTestVersion)
            .PatchCodeWithReplace("$MSTestEngineVersion$", MSTestEngineVersion)
            .PatchCodeWithReplace("$TargetFramework$", tfm),
            addPublicFeeds: true);

        await DotnetCli.RunAsync(
            $"publish {generator.TargetAssetPath} -r {RID} -f {tfm}",
            cancellationToken: TestContext.CancellationToken);
    }

    // Source code for a project that references MSTest.TestAdapter (the reflection-mode adapter)
    // and runs trim analysis with TrimmerRootAssembly to scan the full surface of the assemblies
    // we own. Used by Publish_WithTestAdapter_DoesNotSurfaceWarningsFromSuppressedSources.
    //
    // Note: We do not enable TreatWarningsAsErrors here because the reflection-mode adapter still
    // depends on the vstest Microsoft.TestPlatform.ObjectModel submodule and on
    // System.Private.DataContractSerialization internals, both of which emit trim warnings that
    // are outside this repo's control. The test instead asserts that specific source files we
    // suppressed in this repo no longer appear in publish output.
    private const string TrimAnalysisWithTestAdapterSourceCode = """
#file MSTestTrimAnalysisWithTestAdapter.csproj
<Project Sdk="Microsoft.NET.Sdk">
    <PropertyGroup>
        <TargetFramework>$TargetFramework$</TargetFramework>
        <OutputType>Exe</OutputType>
        <EnableMSTestRunner>true</EnableMSTestRunner>
        <PublishTrimmed>true</PublishTrimmed>
        <!-- Show individual trim warnings instead of a single IL2104 per assembly -->
        <TrimmerSingleWarn>false</TrimmerSingleWarn>
    </PropertyGroup>
    <ItemGroup>
        <PackageReference Include="Microsoft.Testing.Platform" Version="$MicrosoftTestingPlatformVersion$" />
        <PackageReference Include="MSTest.TestAdapter" Version="$MSTestVersion$" />
        <PackageReference Include="MSTest.TestFramework" Version="$MSTestVersion$" />
    </ItemGroup>
    <!-- Force the trimmer to analyze the full surface of the assemblies modified in this PR
         so we catch trim warnings that would not be caught by only testing reachable code paths. -->
    <ItemGroup>
        <TrimmerRootAssembly Include="MSTestAdapter.PlatformServices" />
        <TrimmerRootAssembly Include="Microsoft.Testing.Extensions.VSTestBridge" />
        <TrimmerRootAssembly Include="MSTest.TestFramework" />
    </ItemGroup>
</Project>

#file UnitTest1.cs
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

    [TestMethod]
    [DynamicData(nameof(TargetFrameworks.NetForDynamicData), typeof(TargetFrameworks))]
    public async Task Publish_WithTestAdapter_DoesNotSurfaceWarningsFromSuppressedSources(string tfm)
    {
        // Regression test for the suppressions added in https://github.com/microsoft/testfx/pull/8686.
        //
        // Before that PR, the listed source files emitted trim warnings (IL20xx/IL30xx) when a
        // downstream consumer published a trimmed project that referenced MSTest.TestAdapter.
        // The original C# `#pragma warning disable ILxxxx` directives in those files only
        // silenced the compile-time warning during MSTest's own build; they did NOT propagate to
        // the IL, so the linker still emitted the warnings at the consumer's publish time.
        //
        // After converting those pragmas to [UnconditionalSuppressMessage] / [RequiresUnreferencedCode]
        // / [RequiresDynamicCode] attributes, the suppressions are honored by the IL trimmer and
        // these source-file references should no longer appear in publish output.
        using TestAsset generator = await TestAsset.GenerateAssetAsync(
            $"MSTestTrimAnalysisWithTestAdapter_{tfm}",
            TrimAnalysisWithTestAdapterSourceCode
            .PatchCodeWithReplace("$MicrosoftTestingPlatformVersion$", MicrosoftTestingPlatformVersion)
            .PatchCodeWithReplace("$MSTestVersion$", MSTestVersion)
            .PatchCodeWithReplace("$TargetFramework$", tfm),
            addPublicFeeds: true);

        DotnetMuxerResult result = await DotnetCli.RunAsync(
            $"publish {generator.TargetAssetPath} -r {RID} -f {tfm}",
            cancellationToken: TestContext.CancellationToken);

        // Files in MSTest's own source whose trim warnings are suppressed by this PR.
        // The trimmer includes source file paths in its IL2xxx/IL3xxx warnings, so the absence
        // of these file names in publish output is evidence that the suppression attributes work.
        string[] suppressedSourceFiles =
        [
            "TestSourceHost.cs",
            "DeploymentUtilityBase.cs",
            "ReflectionOperations.cs",
            "AssemblyResolver.cs",
            "DataSerializationHelper.cs",
            "ManagedNameHelper.cs",
            "MethodInfoExtensions.cs",
            "TestMethodFilter.cs",
            "SynchronizedSingleSessionVSTestAndTestAnywhereAdapter.cs",
            "ReflectionTestMethodInfo.cs",
        ];

        foreach (string fileName in suppressedSourceFiles)
        {
            result.AssertOutputDoesNotContain(fileName);
        }
    }

    public TestContext TestContext { get; set; }
}
