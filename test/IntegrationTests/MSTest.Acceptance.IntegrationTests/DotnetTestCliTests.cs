// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Combinatorial.MSTest;

using Microsoft.Testing.Platform.Acceptance.IntegrationTests;
using Microsoft.Testing.Platform.Acceptance.IntegrationTests.Helpers;

namespace MSTest.Acceptance.IntegrationTests;

[TestClass]
public class DotnetTestCliTests : AcceptanceTestBase<NopAssetFixture>
{
    private const string AssetName = "MSTestProject";

    [TestMethod]
    [CombinatorialData]
    public async Task DotnetTest_Should_Execute_Tests([AllTargetFrameworks] string tfm, BuildConfiguration buildConfiguration)
    {
        using TestAsset generator = await TestAsset.GenerateAssetAsync(
            AssetName,
            CurrentMSTestSourceCode
            .PatchCodeWithReplace("$TargetFramework$", $"<TargetFramework>{tfm}</TargetFramework>")
            .PatchCodeWithReplace("$MicrosoftNETTestSdkVersion$", MicrosoftNETTestSdkVersion)
            .PatchCodeWithReplace("$MSTestVersion$", MSTestVersion)
            .PatchCodeWithReplace("$EnableMSTestRunner$", string.Empty)
            .PatchCodeWithReplace("$OutputType$", "<OutputType>Exe</OutputType>")
            .PatchCodeWithReplace("$Extra$", string.Empty));

        DotnetMuxerResult compilationResult = await DotnetCli.RunAsync($"test {generator.TargetAssetPath}", workingDirectory: generator.TargetAssetPath, cancellationToken: TestContext.CancellationToken);

        // MSTest v5 always runs on Microsoft.Testing.Platform, so the generated project opts into the new
        // dotnet test experience (global.json runner = Microsoft.Testing.Platform) and the output is the MTP
        // summary rather than the classic VSTest one.
        compilationResult.AssertOutputContains("Test run summary: Passed!");
        compilationResult.AssertOutputMatchesRegex(@"total: 1\b");
        compilationResult.AssertOutputMatchesRegex(@"failed: 0\b");
        compilationResult.AssertOutputMatchesRegex(@"succeeded: 1\b");
        compilationResult.AssertOutputMatchesRegex(@"skipped: 0\b");
    }

    public TestContext TestContext { get; set; }
}
