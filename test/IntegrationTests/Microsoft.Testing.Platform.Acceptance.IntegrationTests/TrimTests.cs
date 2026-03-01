// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.Acceptance.IntegrationTests;

[TestClass]
public class TrimTests : AcceptanceTestBase<NopAssetFixture>
{
    // Source code for a minimal project that deeply validates trim/AOT compatibility of
    // Microsoft.Testing.Platform by using TrimmerRootAssembly to force the trimmer to analyze
    // all code paths in the assembly, not just those reachable from the test entry point.
    // See https://learn.microsoft.com/dotnet/core/deploying/trimming/prepare-libraries-for-trimming
    private const string TrimAnalysisSourceCode = """
#file TrimAnalysisTest.csproj
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
    </ItemGroup>
    <!-- Force the trimmer to analyze the full assembly surface, not just reachable code paths -->
    <ItemGroup>
        <TrimmerRootAssembly Include="Microsoft.Testing.Platform" />
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
        // This test forces deep trim analysis of Microsoft.Testing.Platform using TrimmerRootAssembly
        // to catch trim warnings that would not be caught by only testing reachable code paths.
        using TestAsset generator = await TestAsset.GenerateAssetAsync(
            $"TrimAnalysisTest_{tfm}",
            TrimAnalysisSourceCode
            .PatchCodeWithReplace("$MicrosoftTestingPlatformVersion$", MicrosoftTestingPlatformVersion)
            .PatchCodeWithReplace("$TargetFramework$", tfm),
            addPublicFeeds: true);

        await DotnetCli.RunAsync(
            $"restore {generator.TargetAssetPath} -r {RID}",
            AcceptanceFixture.NuGetGlobalPackagesFolder.Path,
            retryCount: 0,
            cancellationToken: TestContext.CancellationToken);
        await DotnetCli.RunAsync(
            $"publish {generator.TargetAssetPath} -r {RID} -f {tfm}",
            AcceptanceFixture.NuGetGlobalPackagesFolder.Path,
            retryCount: 0,
            cancellationToken: TestContext.CancellationToken);
    }

    public TestContext TestContext { get; set; }
}
