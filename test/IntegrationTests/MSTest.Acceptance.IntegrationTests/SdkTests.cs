// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Runtime.InteropServices;

using Microsoft.Testing.Platform.Acceptance.IntegrationTests;
using Microsoft.Testing.Platform.Acceptance.IntegrationTests.Helpers;
using Microsoft.Testing.Platform.Helpers;

using SL = Microsoft.Build.Logging.StructuredLogger;

namespace MSTest.Acceptance.IntegrationTests;

[TestGroup]
public sealed class SdkTests : AcceptanceTestBase
{
    private const string AssetName = "MSTestSdk";

    private const string SingleTestSourceCode = """
#file MSTestSdk.csproj
<Project Sdk="MSTest.Sdk/$MSTestVersion$" >

  <PropertyGroup>
    <!--
        This property is not required by users and is only set to simplify our testing infrastructure. When testing out in local or ci,
        we end up with a -dev or -ci version which will lose resolution over -preview dependency of code coverage. Because we want to
        ensure we are testing with locally built version, we force adding the platform dependency.
    -->
    <EnableMicrosoftTestingPlatform>true</EnableMicrosoftTestingPlatform>
    <TargetFrameworks>$TargetFramework$</TargetFrameworks>
    <PlatformTarget>x64</PlatformTarget>
    <NoWarn>$(NoWarn);NU1507</NoWarn>
    $ExtraProperties$
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
    private readonly TestAssetFixture _testAssetFixture;

    public SdkTests(ITestExecutionContext testExecutionContext, AcceptanceFixture acceptanceFixture, TestAssetFixture testAssetFixture)
        : base(testExecutionContext)
    {
        _acceptanceFixture = acceptanceFixture;
        _testAssetFixture = testAssetFixture;
    }

    [ArgumentsProvider(nameof(GetBuildMatrixMultiTfmFoldedBuildConfiguration))]
    public async Task RunTests_With_VSTest(string multiTfm, BuildConfiguration buildConfiguration)
    {
        using TestAsset testAsset = await TestAsset.GenerateAssetAsync(
            AssetName,
            SingleTestSourceCode
            .PatchCodeWithReplace("$MSTestVersion$", MSTestVersion)
            .PatchCodeWithReplace("$TargetFramework$", multiTfm)
            .PatchCodeWithReplace("$ExtraProperties$", "<UseVSTest>true</UseVSTest>"));

        DotnetMuxerResult compilationResult = await DotnetCli.RunAsync($"test -c {buildConfiguration} {testAsset.TargetAssetPath}", _acceptanceFixture.NuGetGlobalPackagesFolder.Path);
        Assert.AreEqual(0, compilationResult.ExitCode);

        compilationResult.AssertOutputRegEx(@"Passed!  - Failed:     0, Passed:     1, Skipped:     0, Total:     1, Duration: .* [m]?s - MSTestSdk.dll \(net9\.0\)");
#if !SKIP_INTERMEDIATE_TARGET_FRAMEWORKS
        compilationResult.AssertOutputRegEx(@"Passed!  - Failed:     0, Passed:     1, Skipped:     0, Total:     1, Duration: .* [m]?s - MSTestSdk.dll \(net8\.0\)");
        compilationResult.AssertOutputRegEx(@"Passed!  - Failed:     0, Passed:     1, Skipped:     0, Total:     1, Duration: .* [m]?s - MSTestSdk.dll \(net7\.0\)");
        compilationResult.AssertOutputRegEx(@"Passed!  - Failed:     0, Passed:     1, Skipped:     0, Total:     1, Duration: .* [m]?s - MSTestSdk.dll \(net6\.0\)");
#endif

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            compilationResult.AssertOutputRegEx(@"Passed!  - Failed:     0, Passed:     1, Skipped:     0, Total:     1, Duration: .* [m]?s - MSTestSdk.dll \(net462\)");
        }
    }

    [ArgumentsProvider(nameof(GetBuildMatrixMultiTfmFoldedBuildConfiguration))]
    public async Task RunTests_With_MSTestRunner_DotnetTest(string multiTfm, BuildConfiguration buildConfiguration)
    {
        using TestAsset testAsset = await TestAsset.GenerateAssetAsync(
               AssetName,
               SingleTestSourceCode
               .PatchCodeWithReplace("$MSTestVersion$", MSTestVersion)
               .PatchCodeWithReplace("$TargetFramework$", multiTfm)
               .PatchCodeWithReplace("$ExtraProperties$", string.Empty));

        DotnetMuxerResult compilationResult = await DotnetCli.RunAsync($"test -c {buildConfiguration} {testAsset.TargetAssetPath}", _acceptanceFixture.NuGetGlobalPackagesFolder.Path);
        Assert.AreEqual(0, compilationResult.ExitCode);

        compilationResult.AssertOutputRegEx(@"Tests succeeded: .* \[net9\.0|x64\]");
#if !SKIP_INTERMEDIATE_TARGET_FRAMEWORKS
        compilationResult.AssertOutputRegEx(@"Tests succeeded: .* \[net8\.0|x64\]");
        compilationResult.AssertOutputRegEx(@"Tests succeeded: .* \[net7\.0|x64\]");
        compilationResult.AssertOutputRegEx(@"Tests succeeded: .* \[net6\.0|x64\]");
#endif

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            compilationResult.AssertOutputRegEx(@"Tests succeeded: .* \[net462|x64\]");
        }
    }

    [ArgumentsProvider(nameof(GetBuildMatrixMultiTfmFoldedBuildConfiguration))]
    public async Task RunTests_With_MSTestRunner_Standalone(string multiTfm, BuildConfiguration buildConfiguration)
    {
        using TestAsset testAsset = await TestAsset.GenerateAssetAsync(
               AssetName,
               SingleTestSourceCode
               .PatchCodeWithReplace("$MSTestVersion$", MSTestVersion)
               .PatchCodeWithReplace("$TargetFramework$", multiTfm)
               .PatchCodeWithReplace("$ExtraProperties$", string.Empty));

        DotnetMuxerResult compilationResult = await DotnetCli.RunAsync($"build -c {buildConfiguration} {testAsset.TargetAssetPath}", _acceptanceFixture.NuGetGlobalPackagesFolder.Path);
        Assert.AreEqual(0, compilationResult.ExitCode);
        foreach (string tfm in multiTfm.Split(";"))
        {
            var testHost = TestHost.LocateFrom(testAsset.TargetAssetPath, AssetName, tfm, buildConfiguration: buildConfiguration);
            TestHostResult testHostResult = await testHost.ExecuteAsync();
            testHostResult.AssertOutputContainsSummary(0, 1, 0);
        }
    }

    [ArgumentsProvider(nameof(GetBuildMatrixMultiTfmFoldedBuildConfiguration))]
    public async Task RunTests_With_CentralPackageManagement_Standalone(string multiTfm, BuildConfiguration buildConfiguration)
    {
        using TestAsset testAsset = await TestAsset.GenerateAssetAsync(
               AssetName,
               SingleTestSourceCode
               .PatchCodeWithReplace("$MSTestVersion$", MSTestVersion)
               .PatchCodeWithReplace("$TargetFramework$", multiTfm)
               .PatchCodeWithReplace("$ExtraProperties$", string.Empty));

        DotnetMuxerResult compilationResult = await DotnetCli.RunAsync($"build -c {buildConfiguration} {testAsset.TargetAssetPath}", _acceptanceFixture.NuGetGlobalPackagesFolder.Path);
        Assert.AreEqual(0, compilationResult.ExitCode);
        foreach (string tfm in multiTfm.Split(";"))
        {
            var testHost = TestHost.LocateFrom(testAsset.TargetAssetPath, AssetName, tfm, buildConfiguration: buildConfiguration);
            TestHostResult testHostResult = await testHost.ExecuteAsync();
            testHostResult.AssertOutputContainsSummary(0, 1, 0);
        }
    }

    public static IEnumerable<TestArgumentsEntry<(string MultiTfm, BuildConfiguration BuildConfiguration, string MSBuildExtensionEnableFragment, string EnableCommandLineArg, string InvalidCommandLineArg)>> RunTests_With_MSTestRunner_Standalone_Plus_Extensions_Data()
    {
        foreach (TestArgumentsEntry<(string MultiTfm, BuildConfiguration BuildConfiguration)> buildConfig in GetBuildMatrixMultiTfmFoldedBuildConfiguration())
        {
            yield return new TestArgumentsEntry<(string, BuildConfiguration, string, string, string)>(
                (buildConfig.Arguments.MultiTfm, buildConfig.Arguments.BuildConfiguration,
                "<EnableMicrosoftTestingExtensionsCodeCoverage>true</EnableMicrosoftTestingExtensionsCodeCoverage>",
                "--coverage",
                "--crashdump"),
                $"multitfm,{buildConfig.Arguments.BuildConfiguration},CodeCoverage");

            yield return new TestArgumentsEntry<(string, BuildConfiguration, string, string, string)>(
              (buildConfig.Arguments.MultiTfm, buildConfig.Arguments.BuildConfiguration,
              "<EnableMicrosoftTestingExtensionsRetry>true</EnableMicrosoftTestingExtensionsRetry>",
              "--retry-failed-tests 3",
              "--crashdump"),
              $"multitfm,{buildConfig.Arguments.BuildConfiguration},Retry");

            yield return new TestArgumentsEntry<(string, BuildConfiguration, string, string, string)>(
              (buildConfig.Arguments.MultiTfm, buildConfig.Arguments.BuildConfiguration,
              "<EnableMicrosoftTestingExtensionsTrxReport>true</EnableMicrosoftTestingExtensionsTrxReport>",
              "--report-trx",
              "--crashdump"),
              $"multitfm,{buildConfig.Arguments.BuildConfiguration},TrxReport");

            yield return new TestArgumentsEntry<(string, BuildConfiguration, string, string, string)>(
              (buildConfig.Arguments.MultiTfm, buildConfig.Arguments.BuildConfiguration,
              "<EnableMicrosoftTestingExtensionsCrashDump>true</EnableMicrosoftTestingExtensionsCrashDump>",
              "--crashdump",
              "--hangdump"),
              $"multitfm,{buildConfig.Arguments.BuildConfiguration},CrashDump");

            yield return new TestArgumentsEntry<(string, BuildConfiguration, string, string, string)>(
              (buildConfig.Arguments.MultiTfm, buildConfig.Arguments.BuildConfiguration,
              "<EnableMicrosoftTestingExtensionsHangDump>true</EnableMicrosoftTestingExtensionsHangDump>",
              "--hangdump",
              "--crashdump"),
              $"multitfm,{buildConfig.Arguments.BuildConfiguration},HangDump");
        }
    }

    // These are failing because the `Retry` filter hasn't been updated
    // But it's not in this repo so I can't change it
    // [ArgumentsProvider(nameof(RunTests_With_MSTestRunner_Standalone_Plus_Extensions_Data))]
    // public async Task RunTests_With_MSTestRunner_Standalone_Selectively_Enabled_Extensions(string multiTfm, BuildConfiguration buildConfiguration,
    //     string msbuildExtensionEnableFragment,
    //     string enableCommandLineArg,
    //     string invalidCommandLineArg)
    // {
    //     using TestAsset testAsset = await TestAsset.GenerateAssetAsync(
    //            AssetName,
    //            SingleTestSourceCode
    //            .PatchCodeWithReplace("$MSTestVersion$", MSTestVersion)
    //            .PatchCodeWithReplace("$TargetFramework$", multiTfm)
    //            .PatchCodeWithReplace("$ExtraProperties$", msbuildExtensionEnableFragment));
    //
    //     DotnetMuxerResult compilationResult = await DotnetCli.RunAsync($"build -c {buildConfiguration} {testAsset.TargetAssetPath}", _acceptanceFixture.NuGetGlobalPackagesFolder.Path);
    //     Assert.AreEqual(0, compilationResult.ExitCode);
    //     foreach (string tfm in multiTfm.Split(";"))
    //     {
    //         var testHost = TestHost.LocateFrom(testAsset.TargetAssetPath, AssetName, tfm, buildConfiguration: buildConfiguration);
    //         TestHostResult testHostResult = await testHost.ExecuteAsync(command: enableCommandLineArg);
    //         testHostResult.AssertOutputContainsSummary(0, 1, 0);
    //
    //         testHostResult = await testHost.ExecuteAsync(command: invalidCommandLineArg);
    //         Assert.AreEqual(ExitCodes.InvalidCommandLine, testHostResult.ExitCode);
    //     }
    // }
    // These are failing because the `Retry` filter hasn't been updated
    // But it's not in this repo so I can't change it
    // [ArgumentsProvider(nameof(GetBuildMatrixMultiTfmFoldedBuildConfiguration))]
    // public async Task RunTests_With_MSTestRunner_Standalone_EnableAll_Extensions(string multiTfm, BuildConfiguration buildConfiguration)
    // {
    //     using TestAsset testAsset = await TestAsset.GenerateAssetAsync(
    //            AssetName,
    //            SingleTestSourceCode
    //            .PatchCodeWithReplace("$MSTestVersion$", MSTestVersion)
    //            .PatchCodeWithReplace("$TargetFramework$", multiTfm)
    //            .PatchCodeWithReplace("$ExtraProperties$", "<TestingExtensionsProfile>AllMicrosoft</TestingExtensionsProfile>"));
    //
    //     DotnetMuxerResult compilationResult = await DotnetCli.RunAsync($"build -c {buildConfiguration} {testAsset.TargetAssetPath}", _acceptanceFixture.NuGetGlobalPackagesFolder.Path);
    //     Assert.AreEqual(0, compilationResult.ExitCode);
    //     foreach (string tfm in multiTfm.Split(";"))
    //     {
    //         var testHost = TestHost.LocateFrom(testAsset.TargetAssetPath, AssetName, tfm, buildConfiguration: buildConfiguration);
    //         TestHostResult testHostResult = await testHost.ExecuteAsync(command: "--coverage --retry-failed-tests 3 --report-trx --crashdump --hangdump");
    //         testHostResult.AssertOutputContainsSummary(0, 1, 0);
    //     }
    // }
    public static IEnumerable<TestArgumentsEntry<(string MultiTfm, BuildConfiguration BuildConfiguration, bool EnableDefaultExtensions)>> RunTests_With_MSTestRunner_Standalone_Default_Extensions_Data()
    {
        foreach (TestArgumentsEntry<(string MultiTfm, BuildConfiguration BuildConfiguration)> buildConfig in GetBuildMatrixMultiTfmFoldedBuildConfiguration())
        {
            yield return new TestArgumentsEntry<(string, BuildConfiguration, bool)>(
                (buildConfig.Arguments.MultiTfm, buildConfig.Arguments.BuildConfiguration, true),
                $"enabled,{buildConfig.Arguments.BuildConfiguration},CodeCoverage");

            yield return new TestArgumentsEntry<(string, BuildConfiguration, bool)>(
                (buildConfig.Arguments.MultiTfm, buildConfig.Arguments.BuildConfiguration, false),
                $"disabled,{buildConfig.Arguments.BuildConfiguration},CodeCoverage");
        }
    }

    // These are failing because the `Retry` filter hasn't been updated
    // But it's not in this repo so I can't change it
    // [ArgumentsProvider(nameof(RunTests_With_MSTestRunner_Standalone_Default_Extensions_Data))]
    // public async Task RunTests_With_MSTestRunner_Standalone_Enable_Default_Extensions(string multiTfm, BuildConfiguration buildConfiguration, bool enableDefaultExtensions)
    // {
    //     using TestAsset testAsset = await TestAsset.GenerateAssetAsync(
    //            AssetName,
    //            SingleTestSourceCode
    //            .PatchCodeWithReplace("$MSTestVersion$", MSTestVersion)
    //            .PatchCodeWithReplace("$TargetFramework$", multiTfm)
    //            .PatchCodeWithReplace("$ExtraProperties$", enableDefaultExtensions ? string.Empty : "<TestingExtensionsProfile>None</TestingExtensionsProfile>"));
    //
    //     DotnetMuxerResult compilationResult = await DotnetCli.RunAsync($"build -c {buildConfiguration} {testAsset.TargetAssetPath}", _acceptanceFixture.NuGetGlobalPackagesFolder.Path);
    //     Assert.AreEqual(0, compilationResult.ExitCode);
    //     foreach (string tfm in multiTfm.Split(";"))
    //     {
    //         var testHost = TestHost.LocateFrom(testAsset.TargetAssetPath, AssetName, tfm, buildConfiguration: buildConfiguration);
    //         TestHostResult testHostResult = await testHost.ExecuteAsync(command: "--coverage --report-trx");
    //         if (enableDefaultExtensions)
    //         {
    //             testHostResult.AssertOutputContainsSummary(0, 1, 0);
    //         }
    //         else
    //         {
    //             Assert.AreEqual(ExitCodes.InvalidCommandLine, testHostResult.ExitCode);
    //         }
    //     }
    // }
    [ArgumentsProvider(nameof(GetBuildMatrixMultiTfmFoldedBuildConfiguration))]
    public async Task Invalid_TestingProfile_Name_Should_Fail(string multiTfm, BuildConfiguration buildConfiguration)
    {
        using TestAsset testAsset = await TestAsset.GenerateAssetAsync(
               AssetName,
               SingleTestSourceCode
               .PatchCodeWithReplace("$MSTestVersion$", MSTestVersion)
               .PatchCodeWithReplace("$TargetFramework$", multiTfm)
               .PatchCodeWithReplace("$ExtraProperties$", "<TestingExtensionsProfile>WrongName</TestingExtensionsProfile>"));

        DotnetMuxerResult compilationResult = await DotnetCli.RunAsync($"build -c {buildConfiguration} {testAsset.TargetAssetPath}", _acceptanceFixture.NuGetGlobalPackagesFolder.Path, failIfReturnValueIsNotZero: false);
        Assert.AreEqual(1, compilationResult.ExitCode);
        compilationResult.AssertOutputContains("Invalid value for property TestingExtensionsProfile. Valid values are 'Default', 'AllMicrosoft' and 'None'.");
    }

    public async Task NativeAot_Smoke_Test_Windows()
        // The native AOT publication is pretty flaky and is often failing on CI with "fatal error LNK1136: invalid or corrupt file",
        // or sometimes doesn't fail but the native code generation is not done.
        // Retrying the restore/publish on fresh asset seems to be more effective than retrying on the same asset.
        => await RetryHelper.RetryAsync(
            async () =>
            {
                if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    return;
                }

                using TestAsset testAsset = await TestAsset.GenerateAssetAsync(
                    AssetName,
                    SingleTestSourceCode
                    .PatchCodeWithReplace("$MSTestVersion$", MSTestVersion)
                    // temporarily set test to be on net9.0 as it's fixing one error that started to happen:  error IL3000: System.Net.Quic.MsQuicApi..cctor
                    // see https://github.com/dotnet/sdk/issues/44880.
                    .PatchCodeWithReplace("$TargetFramework$", "net9.0")
                    .PatchCodeWithReplace("$ExtraProperties$", $"""
                <PublishAot>true</PublishAot>
                <EnableMicrosoftTestingExtensionsCodeCoverage>false</EnableMicrosoftTestingExtensionsCodeCoverage>
                """),
                    addPublicFeeds: true);

                DotnetMuxerResult compilationResult = await DotnetCli.RunAsync(
                    $"publish -r {RID} -f net9.0 {testAsset.TargetAssetPath}",
                    _acceptanceFixture.NuGetGlobalPackagesFolder.Path,
                    // We prefer to use the outer retry mechanism as we need some extra checks
                    retryCount: 0,
                    timeoutInSeconds: 180);
                compilationResult.AssertOutputContains("Generating native code");
                compilationResult.AssertOutputNotContains("warning");

                var testHost = TestHost.LocateFrom(testAsset.TargetAssetPath, AssetName, "net9.0", verb: Verb.publish);
                TestHostResult testHostResult = await testHost.ExecuteAsync();

                testHostResult.AssertExitCodeIs(ExitCodes.Success);
                testHostResult.AssertOutputContainsSummary(0, 1, 0);
            }, times: 15, every: TimeSpan.FromSeconds(5));

    [ArgumentsProvider(nameof(TargetFrameworks.All), typeof(TargetFrameworks))]
    public async Task EnablePlaywrightProperty_WhenUsingRunner_AllowsToRunPlaywrightTests(string tfm)
    {
        var testHost = TestHost.LocateFrom(_testAssetFixture.PlaywrightProjectPath, TestAssetFixture.PlaywrightProjectName, tfm);
        TestHostResult testHostResult = await testHost.ExecuteAsync();

        // Depending on the machine, the test might fail due to the browser not being installed.
        // To avoid slowing down the tests, we will not run the installation so depending on machines we have different results.
        switch (testHostResult.ExitCode)
        {
            case 0:
                testHostResult.AssertOutputContainsSummary(0, 1, 0);
                break;

            case 2:
                testHostResult.AssertOutputContains("Microsoft.Playwright.PlaywrightException: Executable doesn't exist");
                break;

            default:
                Assert.Fail("Unexpected exit code");
                break;
        }
    }

    [ArgumentsProvider(nameof(TargetFrameworks.All), typeof(TargetFrameworks))]
    public async Task EnablePlaywrightProperty_WhenUsingVSTest_AllowsToRunPlaywrightTests(string tfm)
    {
        var testHost = TestHost.LocateFrom(_testAssetFixture.PlaywrightProjectPath, TestAssetFixture.PlaywrightProjectName, tfm);
        string exeOrDllName = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? testHost.FullName
            : testHost.FullName + ".dll";
        DotnetMuxerResult dotnetTestResult = await DotnetCli.RunAsync(
            $"test {exeOrDllName}",
            _acceptanceFixture.NuGetGlobalPackagesFolder.Path,
            failIfReturnValueIsNotZero: false,
            warnAsError: false,
            suppressPreviewDotNetMessage: false);

        // Ensure output contains the right platform banner
        dotnetTestResult.AssertOutputContains("VSTest version");

        // Depending on the machine, the test might fail due to the browser not being installed.
        // To avoid slowing down the tests, we will not run the installation so depending on machines we have different results.
        switch (dotnetTestResult.ExitCode)
        {
            case 0:
                dotnetTestResult.AssertOutputContains("Passed!  - Failed:     0, Passed:     1, Skipped:     0, Total:     1");
                break;

            case 1:
                dotnetTestResult.AssertOutputContains("Failed!  - Failed:     1, Passed:     0, Skipped:     0, Total:     1");
                break;

            default:
                Assert.Fail("Unexpected exit code");
                break;
        }
    }

    public async Task EnableAspireProperty_WhenUsingRunner_AllowsToRunAspireTests()
    {
        var testHost = TestHost.LocateFrom(_testAssetFixture.AspireProjectPath, TestAssetFixture.AspireProjectName, TargetFrameworks.NetCurrent.UidFragment);
        TestHostResult testHostResult = await testHost.ExecuteAsync();
        testHostResult.AssertOutputContainsSummary(0, 1, 0);
    }

    public async Task EnableAspireProperty_WhenUsingVSTest_AllowsToRunAspireTests()
    {
        var testHost = TestHost.LocateFrom(_testAssetFixture.AspireProjectPath, TestAssetFixture.AspireProjectName, TargetFrameworks.NetCurrent.UidFragment);
        string exeOrDllName = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? testHost.FullName
            : testHost.FullName + ".dll";
        DotnetMuxerResult dotnetTestResult = await DotnetCli.RunAsync(
            $"test {exeOrDllName}",
            _acceptanceFixture.NuGetGlobalPackagesFolder.Path,
            warnAsError: false,
            suppressPreviewDotNetMessage: false);
        Assert.AreEqual(0, dotnetTestResult.ExitCode);
        // Ensure output contains the right platform banner
        dotnetTestResult.AssertOutputContains("VSTest version");
        dotnetTestResult.AssertOutputContains("Passed!  - Failed:     0, Passed:     1, Skipped:     0, Total:     1");
    }

    public async Task SettingIsTestApplicationToFalseReducesAddedExtensionsAndMakesProjectNotExecutable()
    {
        using TestAsset testAsset = await TestAsset.GenerateAssetAsync(
               AssetName,
               SingleTestSourceCode
               .PatchCodeWithReplace("$MSTestVersion$", MSTestVersion)
               .PatchCodeWithReplace("$TargetFramework$", TargetFrameworks.NetCurrent.UidFragment)
               .PatchCodeWithReplace("$ExtraProperties$", "<IsTestApplication>false</IsTestApplication>"));
        string binlogFile = Path.Combine(testAsset.TargetAssetPath, Guid.NewGuid().ToString("N"), "msbuild.binlog");

        DotnetMuxerResult compilationResult = await DotnetCli.RunAsync($"test {testAsset.TargetAssetPath} -bl:{binlogFile}", _acceptanceFixture.NuGetGlobalPackagesFolder.Path);

        Assert.AreEqual(0, compilationResult.ExitCode);

        SL.Build binLog = SL.Serialization.Read(binlogFile);
        SL.Task cscTask = binLog.FindChildrenRecursive<SL.Task>(task => task.Name == "Csc").Single();
        SL.Item[] references = cscTask.FindChildrenRecursive<SL.Parameter>(p => p.Name == "References").Single().Children.OfType<SL.Item>().ToArray();

        // Ensure that MSTest.Framework is referenced
        Assert.IsTrue(references.Any(r => r.Text.EndsWith("Microsoft.VisualStudio.TestPlatform.TestFramework.dll", StringComparison.OrdinalIgnoreCase)));
        Assert.IsTrue(references.Any(r => r.Text.EndsWith("Microsoft.VisualStudio.TestPlatform.TestFramework.Extensions.dll", StringComparison.OrdinalIgnoreCase)));

        // No adapter, no extensions, no vstest sdk
        Assert.IsFalse(references.Any(r => r.Text.EndsWith("Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.dll", StringComparison.OrdinalIgnoreCase)));
        Assert.IsFalse(references.Any(r => r.Text.Contains("Microsoft.Testing.Extensions.", StringComparison.OrdinalIgnoreCase)));

        // It's not an executable
        Assert.IsFalse(binLog.FindChildrenRecursive<SL.Property>(p => p.Name == "OutputType").Any(p => p.Value == "Exe"));
    }

    [TestFixture(TestFixtureSharingStrategy.PerTestGroup)]
    public sealed class TestAssetFixture(AcceptanceFixture _acceptanceFixture)
        : TestAssetFixtureBase(_acceptanceFixture.NuGetGlobalPackagesFolder)
    {
        public const string AspireProjectName = "AspireProject";
        public const string PlaywrightProjectName = "PlaywrightProject";

        private const string AspireSourceCode = """
#file AspireProject.csproj
<Project Sdk="MSTest.Sdk/$MSTestVersion$">
  <PropertyGroup>
    <TargetFrameworks>$TargetFrameworks$</TargetFrameworks>
    <LangVersion>latest</LangVersion>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <!-- Ensures that dotnet test uses VSTest so we can run tests with the 2 platforms -->
    <TestingPlatformDotnetTestSupport>false</TestingPlatformDotnetTestSupport>
    <!-- Disable all extensions by default -->
    <TestingExtensionsProfile>None</TestingExtensionsProfile>
    <EnableAspireTesting>true</EnableAspireTesting>
  </PropertyGroup>

  <ItemGroup>
    <Using Include="System.Threading.Tasks" />
  </ItemGroup>
</Project>

#file UnitTest1.cs
namespace AspireProject;

[TestClass]
public class IntegrationTest1
{
    [TestMethod]
    public void GetWebResourceRootReturnsOkStatusCode()
    {
        // TODO: Test could be improved to run a real Aspire app, their starter is a big multi-projects app
    }
}
""";

        private const string PlaywrightSourceCode = """
#file PlaywrightProject.csproj
<Project Sdk="MSTest.Sdk/$MSTestVersion$">
  <PropertyGroup>
    <TargetFrameworks>$TargetFrameworks$</TargetFrameworks>
    <LangVersion>latest</LangVersion>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <!-- Ensures that dotnet test uses VSTest so we can run tests with the 2 platforms -->
    <TestingPlatformDotnetTestSupport>false</TestingPlatformDotnetTestSupport>
    <!-- Disable all extensions by default -->
    <TestingExtensionsProfile>None</TestingExtensionsProfile>
    <EnablePlaywright>true</EnablePlaywright>
  </PropertyGroup>

  <ItemGroup>
    <Using Include="System.Text.RegularExpressions" />
    <Using Include="System.Threading.Tasks" />
  </ItemGroup>
</Project>

#file UnitTest1.cs
namespace PlaywrightProject;

[TestClass]
public class UnitTest1 : PageTest
{
    [TestMethod]
    public async Task HomepageHasPlaywrightInTitleAndGetStartedLinkLinkingToTheIntroPage()
    {
        await Page.GotoAsync("https://playwright.dev");

        // Expect a title "to contain" a substring.
        await Expect(Page).ToHaveTitleAsync(new Regex("Playwright"));

        // create a locator
        var getStarted = Page.Locator("text=Get Started");

        // Expect an attribute "to be strictly equal" to the value.
        await Expect(getStarted).ToHaveAttributeAsync("href", "/docs/intro");

        // Click the get started link.
        await getStarted.ClickAsync();

        // Expects the URL to contain intro.
        await Expect(Page).ToHaveURLAsync(new Regex(".*intro"));
    }
}
""";

        public string AspireProjectPath => GetAssetPath(AspireProjectName);

        public string PlaywrightProjectPath => GetAssetPath(PlaywrightProjectName);

        public override IEnumerable<(string ID, string Name, string Code)> GetAssetsToGenerate()
        {
            yield return (AspireProjectName, AspireProjectName,
                AspireSourceCode
                .PatchTargetFrameworks(TargetFrameworks.NetCurrent)
                .PatchCodeWithReplace("$MSTestVersion$", MSTestVersion));

            yield return (PlaywrightProjectName, PlaywrightProjectName,
                PlaywrightSourceCode
                .PatchTargetFrameworks(TargetFrameworks.All)
                .PatchCodeWithReplace("$MSTestVersion$", MSTestVersion));
        }
    }
}
