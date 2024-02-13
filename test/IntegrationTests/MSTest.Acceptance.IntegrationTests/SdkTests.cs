// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Runtime.InteropServices;

using Microsoft.Testing.Platform.Acceptance.IntegrationTests;
using Microsoft.Testing.Platform.Acceptance.IntegrationTests.Helpers;
using Microsoft.Testing.Platform.Helpers;

namespace MSTest.Acceptance.IntegrationTests;

[TestGroup]
public sealed class SdkTests : AcceptanceTestBase
{
    private const string AssetName = "MSTestSdk";

    public SdkTests(ITestExecutionContext testExecutionContext, AcceptanceFixture acceptanceFixture)
        : base(testExecutionContext)
    {
        _acceptanceFixture = acceptanceFixture;
    }

    [ArgumentsProvider(nameof(GetBuildMatrixMultiTfmBuildConfiguration))]
    public async Task RunTests_With_VSTest(string multiTfm, BuildConfiguration buildConfiguration)
    {
        using TestAsset generator = await TestAsset.GenerateAssetAsync(
               AssetName,
               SourceCode
               .PatchCodeWithReplace("$MSTestVersion$", MSTestVersion)
               .PatchCodeWithReplace("$OutputType$", string.Empty)
               .PatchCodeWithReplace("$TargetFramework$", $"<TargetFrameworks>{multiTfm}</TargetFrameworks>")
               .PatchCodeWithReplace("$EnableMSTestRunner$", string.Empty)
               .PatchCodeWithReplace("$TestingPlatformDotnetTestSupport$", string.Empty)
               .PatchCodeWithReplace("$Extensions$", string.Empty),
               addPublicFeeds: true);

        var compilationResult = await DotnetCli.RunAsync($"test -c {buildConfiguration} {generator.TargetAssetPath}", _acceptanceFixture.NuGetGlobalPackagesFolder.Path);
        Assert.AreEqual(0, compilationResult.ExitCode);

        compilationResult.AssertOutputRegEx(@"Passed!  - Failed:     0, Passed:     1, Skipped:     0, Total:     1, Duration: .* ms - MSTestSdk.dll \(net8\.0\)");
        compilationResult.AssertOutputRegEx(@"Passed!  - Failed:     0, Passed:     1, Skipped:     0, Total:     1, Duration: .* ms - MSTestSdk.dll \(net7\.0\)");
        compilationResult.AssertOutputRegEx(@"Passed!  - Failed:     0, Passed:     1, Skipped:     0, Total:     1, Duration: .* ms - MSTestSdk.dll \(net6\.0\)");

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            compilationResult.AssertOutputRegEx(@"Passed!  - Failed:     0, Passed:     1, Skipped:     0, Total:     1, Duration: .* ms - MSTestSdk.dll \(net462\)");
        }
    }

    [ArgumentsProvider(nameof(GetBuildMatrixMultiTfmBuildConfiguration))]
    public async Task RunTests_With_MSTestRunner_DotnetTest(string multiTfm, BuildConfiguration buildConfiguration)
    {
        using TestAsset generator = await TestAsset.GenerateAssetAsync(
               AssetName,
               SourceCode
               .PatchCodeWithReplace("$MSTestVersion$", MSTestVersion)
               .PatchCodeWithReplace("$OutputType$", "<OutputType>Exe</OutputType>")
               .PatchCodeWithReplace("$TargetFramework$", $"<TargetFrameworks>{multiTfm}</TargetFrameworks>")
               .PatchCodeWithReplace("$EnableMSTestRunner$", "<EnableMSTestRunner>true</EnableMSTestRunner>")
               .PatchCodeWithReplace("$TestingPlatformDotnetTestSupport$", "<TestingPlatformDotnetTestSupport>true</TestingPlatformDotnetTestSupport>")
               .PatchCodeWithReplace("$Extensions$", string.Empty),
               addPublicFeeds: true);

        var compilationResult = await DotnetCli.RunAsync($"test -c {buildConfiguration} {generator.TargetAssetPath}", _acceptanceFixture.NuGetGlobalPackagesFolder.Path);
        Assert.AreEqual(0, compilationResult.ExitCode);

        compilationResult.AssertOutputRegEx(@"Tests succeeded: .* \[net8\.0|x64\]");
        compilationResult.AssertOutputRegEx(@"Tests succeeded: .* \[net7\.0|x64\]");
        compilationResult.AssertOutputRegEx(@"Tests succeeded: .* \[net6\.0|x64\]");

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            compilationResult.AssertOutputRegEx(@"Tests succeeded: .* \[net462|x64\]");
        }
    }

    [ArgumentsProvider(nameof(GetBuildMatrixMultiTfmBuildConfiguration))]
    public async Task RunTests_With_MSTestRunner_Standalone(string multiTfm, BuildConfiguration buildConfiguration)
    {
        using TestAsset generator = await TestAsset.GenerateAssetAsync(
               AssetName,
               SourceCode
               .PatchCodeWithReplace("$MSTestVersion$", MSTestVersion)
               .PatchCodeWithReplace("$OutputType$", "<OutputType>Exe</OutputType>")
               .PatchCodeWithReplace("$TargetFramework$", $"<TargetFrameworks>{multiTfm}</TargetFrameworks>")
               .PatchCodeWithReplace("$EnableMSTestRunner$", "<EnableMSTestRunner>true</EnableMSTestRunner>")
               .PatchCodeWithReplace("$TestingPlatformDotnetTestSupport$", string.Empty)
               .PatchCodeWithReplace("$Extensions$", string.Empty),
               addPublicFeeds: true);

        var compilationResult = await DotnetCli.RunAsync($"build -c {buildConfiguration} {generator.TargetAssetPath}", _acceptanceFixture.NuGetGlobalPackagesFolder.Path);
        Assert.AreEqual(0, compilationResult.ExitCode);
        foreach (var tfm in multiTfm.Split(";"))
        {
            var testHost = TestHost.LocateFrom(generator.TargetAssetPath, AssetName, tfm, buildConfiguration: buildConfiguration);
            var testHostResult = await testHost.ExecuteAsync();
            testHostResult.AssertOutputContains("Passed! - Failed: 0, Passed: 1, Skipped: 0, Total: 1");
        }
    }

    public static IEnumerable<TestArgumentsEntry<(string MultiTfm, BuildConfiguration BuildConfiguration, string MSBuildExtensionEnableFragment, string EnableCommandLineArg, string InvalidCommandLineArg)>> RunTests_With_MSTestRunner_Standalone_Plus_Extensions_Data()
    {
        foreach (TestArgumentsEntry<(string MultiTfm, BuildConfiguration BuildConfiguration)> buildConfig in GetBuildMatrixMultiTfmBuildConfiguration())
        {
            yield return new TestArgumentsEntry<(string, BuildConfiguration, string, string, string)>(
                (buildConfig.Arguments.MultiTfm, buildConfig.Arguments.BuildConfiguration,
                "<EnableMicrosoftTestingExtensionsCodeCoverage>true</EnableMicrosoftTestingExtensionsCodeCoverage>",
                "--coverage",
                "--crashdump"),
                $"(multitfm,{buildConfig.Arguments.BuildConfiguration},CodeCoverage)");

            yield return new TestArgumentsEntry<(string, BuildConfiguration, string, string, string)>(
              (buildConfig.Arguments.MultiTfm, buildConfig.Arguments.BuildConfiguration,
              "<EnableMicrosoftTestingExtensionsRetry>true</EnableMicrosoftTestingExtensionsRetry>",
              "--retry-failed-tests 3",
              "--crashdump"),
              $"(multitfm,{buildConfig.Arguments.BuildConfiguration},Retry)");

            yield return new TestArgumentsEntry<(string, BuildConfiguration, string, string, string)>(
              (buildConfig.Arguments.MultiTfm, buildConfig.Arguments.BuildConfiguration,
              "<EnableMicrosoftTestingExtensionsTrxReport>true</EnableMicrosoftTestingExtensionsTrxReport>",
              "--report-trx",
              "--crashdump"),
              $"(multitfm,{buildConfig.Arguments.BuildConfiguration},TrxReport)");

            yield return new TestArgumentsEntry<(string, BuildConfiguration, string, string, string)>(
              (buildConfig.Arguments.MultiTfm, buildConfig.Arguments.BuildConfiguration,
              "<EnableMicrosoftTestingExtensionsCrashDump>true</EnableMicrosoftTestingExtensionsCrashDump>",
              "--crashdump",
              "--report-trx"),
              $"(multitfm,{buildConfig.Arguments.BuildConfiguration},CrashDump)");

            yield return new TestArgumentsEntry<(string, BuildConfiguration, string, string, string)>(
              (buildConfig.Arguments.MultiTfm, buildConfig.Arguments.BuildConfiguration,
              "<EnableMicrosoftTestingExtensionsHangDump>true</EnableMicrosoftTestingExtensionsHangDump>",
              "--hangdump",
              "--report-trx"),
              $"(multitfm,{buildConfig.Arguments.BuildConfiguration},HangDump)");
        }
    }

    [ArgumentsProvider(nameof(RunTests_With_MSTestRunner_Standalone_Plus_Extensions_Data))]
    public async Task RunTests_With_MSTestRunner_Standalone_Selectively_Enabled_Extensions(string multiTfm, BuildConfiguration buildConfiguration,
        string msbuildExtensionEnableFragment,
        string enableCommandLineArg,
        string invalidCommandLineArg)
    {
        using TestAsset generator = await TestAsset.GenerateAssetAsync(
               AssetName,
               SourceCode
               .PatchCodeWithReplace("$MSTestVersion$", MSTestVersion)
               .PatchCodeWithReplace("$OutputType$", "<OutputType>Exe</OutputType>")
               .PatchCodeWithReplace("$TargetFramework$", $"<TargetFrameworks>{multiTfm}</TargetFrameworks>")
               .PatchCodeWithReplace("$EnableMSTestRunner$", "<EnableMSTestRunner>true</EnableMSTestRunner>")
               .PatchCodeWithReplace("$TestingPlatformDotnetTestSupport$", string.Empty)
               .PatchCodeWithReplace("$Extensions$", msbuildExtensionEnableFragment),
               addPublicFeeds: true);

        var compilationResult = await DotnetCli.RunAsync($"build -c {buildConfiguration} {generator.TargetAssetPath}", _acceptanceFixture.NuGetGlobalPackagesFolder.Path);
        Assert.AreEqual(0, compilationResult.ExitCode);
        foreach (var tfm in multiTfm.Split(";"))
        {
            var testHost = TestHost.LocateFrom(generator.TargetAssetPath, AssetName, tfm, buildConfiguration: buildConfiguration);
            var testHostResult = await testHost.ExecuteAsync(command: enableCommandLineArg);
            testHostResult.AssertOutputContains("Passed! - Failed: 0, Passed: 1, Skipped: 0, Total: 1");

            testHostResult = await testHost.ExecuteAsync(command: invalidCommandLineArg);
            Assert.AreEqual(ExitCodes.InvalidCommandLine, testHostResult.ExitCode);
        }
    }

    [ArgumentsProvider(nameof(GetBuildMatrixMultiTfmBuildConfiguration))]
    public async Task RunTests_With_MSTestRunner_Standalone_EnableAll_Extensions(string multiTfm, BuildConfiguration buildConfiguration)
    {
        using TestAsset generator = await TestAsset.GenerateAssetAsync(
               AssetName,
               SourceCode
               .PatchCodeWithReplace("$MSTestVersion$", MSTestVersion)
               .PatchCodeWithReplace("$OutputType$", "<OutputType>Exe</OutputType>")
               .PatchCodeWithReplace("$TargetFramework$", $"<TargetFrameworks>{multiTfm}</TargetFrameworks>")
               .PatchCodeWithReplace("$EnableMSTestRunner$", "<EnableMSTestRunner>true</EnableMSTestRunner>")
               .PatchCodeWithReplace("$TestingPlatformDotnetTestSupport$", string.Empty)
               .PatchCodeWithReplace("$Extensions$", "<EnableAllTestingExtensions>true</EnableAllTestingExtensions>"),
               addPublicFeeds: true);

        var compilationResult = await DotnetCli.RunAsync($"build -c {buildConfiguration} {generator.TargetAssetPath}", _acceptanceFixture.NuGetGlobalPackagesFolder.Path);
        Assert.AreEqual(0, compilationResult.ExitCode);
        foreach (var tfm in multiTfm.Split(";"))
        {
            var testHost = TestHost.LocateFrom(generator.TargetAssetPath, AssetName, tfm, buildConfiguration: buildConfiguration);
            var testHostResult = await testHost.ExecuteAsync(command: "--coverage --retry-failed-tests 3 --report-trx --crashdump --hangdump");
            testHostResult.AssertOutputContains("Passed! - Failed: 0, Passed: 1, Skipped: 0, Total: 1");
        }
    }

    private const string SourceCode = """
#file MSTestSdk.csproj
<Project Sdk="MSTest.Sdk/$MSTestVersion$" >
  <PropertyGroup>
    $OutputType$
    $TargetFramework$
    $EnableMSTestRunner$
    $TestingPlatformDotnetTestSupport$
    <PlatformTarget>x64</PlatformTarget>
  </PropertyGroup>

  <!-- Extensions -->
  <PropertyGroup>
    $Extensions$
  </PropertyGroup>
</Project>

#file UnitTest1.cs

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MSTestSdkTest
{
    [TestClass]
    public class UnitTest1
    {
        [TestMethod]
        public void TestMethod1()
        {
        }
    }
}
""";

    private readonly AcceptanceFixture _acceptanceFixture;
}
