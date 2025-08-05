// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Acceptance.IntegrationTests;
using Microsoft.Testing.Platform.Acceptance.IntegrationTests.Helpers;

namespace MSTest.Acceptance.IntegrationTests;

[TestClass]
public class DotnetTestCliTests : AcceptanceTestBase<NopAssetFixture>
{
    private const string AssetName = "MSTestProject";

    [TestMethod]
    [DynamicData(nameof(GetBuildMatrixTfmBuildConfiguration), typeof(AcceptanceTestBase<NopAssetFixture>))]
    public async Task DotnetTest_Should_Execute_Tests(string tfm, BuildConfiguration buildConfiguration)
    {
        using TestAsset generator = await TestAsset.GenerateAssetAsync(
            AssetName,
            CurrentMSTestSourceCode
            .PatchCodeWithReplace("$TargetFramework$", $"<TargetFramework>{tfm}</TargetFramework>")
            .PatchCodeWithReplace("$MicrosoftNETTestSdkVersion$", MicrosoftNETTestSdkVersion)
            .PatchCodeWithReplace("$MSTestVersion$", MSTestVersion)
            .PatchCodeWithReplace("$EnableMSTestRunner$", string.Empty)
            .PatchCodeWithReplace("$OutputType$", string.Empty)
            .PatchCodeWithReplace("$Extra$", string.Empty));

        DotnetMuxerResult compilationResult = await DotnetCli.RunAsync($"test -m:1 -nodeReuse:false {generator.TargetAssetPath}", AcceptanceFixture.NuGetGlobalPackagesFolder.Path, workingDirectory: generator.TargetAssetPath);

        // There is whitespace difference in output in parent and public repo that depends on the version of the dotnet SDK used.
        compilationResult.AssertOutputMatchesRegex(@"Passed!\s+-\s+Failed:\s+0,\s+Passed:\s+1,\s+Skipped:\s+0,\s+Total:\s+1");
    }
}
