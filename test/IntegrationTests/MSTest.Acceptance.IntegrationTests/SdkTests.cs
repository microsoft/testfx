// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Acceptance.IntegrationTests;
using Microsoft.Testing.Platform.Acceptance.IntegrationTests.Helpers;
using Microsoft.Testing.Platform.Helpers;

using SL = Microsoft.Build.Logging.StructuredLogger;

namespace MSTest.Acceptance.IntegrationTests;

[TestClass]
public sealed class SdkTests : AcceptanceTestBase<NopAssetFixture>
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

    private const string SingleTestSourceCodeVSTest = SingleTestSourceCode + """

        #file global.json
        {
          "test": {
            "runner": "VSTest"
          }
        }
        """;

    public TestContext TestContext { get; set; }

    [TestMethod]
    [DynamicData(nameof(GetBuildMatrixMultiTfmFoldedBuildConfiguration), typeof(AcceptanceTestBase<NopAssetFixture>))]
    public async Task RunTests_With_VSTest(string multiTfm, BuildConfiguration buildConfiguration)
    {
        using TestAsset testAsset = await TestAsset.GenerateAssetAsync(
            AssetName,
            SingleTestSourceCodeVSTest
            .PatchCodeWithReplace("$MSTestVersion$", MSTestVersion)
            .PatchCodeWithReplace("$TargetFramework$", multiTfm)
            .PatchCodeWithReplace("$ExtraProperties$", "<UseVSTest>true</UseVSTest>"));

        DotnetMuxerResult compilationResult = await DotnetCli.RunAsync($"test -c {buildConfiguration} {testAsset.TargetAssetPath}", workingDirectory: testAsset.TargetAssetPath, cancellationToken: TestContext.CancellationToken);
        compilationResult.AssertExitCodeIs(0);

        compilationResult.AssertOutputMatchesRegex(@"Passed!  - Failed:     0, Passed:     1, Skipped:     0, Total:     1, Duration: .* [m]?s - MSTestSdk.dll \(net10\.0\)");
#if !SKIP_INTERMEDIATE_TARGET_FRAMEWORKS
        compilationResult.AssertOutputMatchesRegex(@"Passed!  - Failed:     0, Passed:     1, Skipped:     0, Total:     1, Duration: .* [m]?s - MSTestSdk.dll \(net8\.0\)");
#endif

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            compilationResult.AssertOutputMatchesRegex(@"Passed!  - Failed:     0, Passed:     1, Skipped:     0, Total:     1, Duration: .* [m]?s - MSTestSdk.dll \(net462\)");
        }
    }

    [TestMethod]
    [DynamicData(nameof(GetBuildMatrixMultiTfmFoldedBuildConfiguration), typeof(AcceptanceTestBase<NopAssetFixture>))]
    public async Task RunTests_With_MSTestRunner_DotnetTest(string multiTfm, BuildConfiguration buildConfiguration)
    {
        using TestAsset testAsset = await TestAsset.GenerateAssetAsync(
               AssetName,
               SingleTestSourceCode
               .PatchCodeWithReplace("$MSTestVersion$", MSTestVersion)
               .PatchCodeWithReplace("$TargetFramework$", multiTfm)
               .PatchCodeWithReplace("$ExtraProperties$", string.Empty));

        DotnetMuxerResult compilationResult = await DotnetCli.RunAsync($"test -c {buildConfiguration} --project {testAsset.TargetAssetPath} --no-progress --no-ansi", workingDirectory: testAsset.TargetAssetPath, cancellationToken: TestContext.CancellationToken);
        compilationResult.AssertExitCodeIs(0);

        compilationResult.AssertOutputMatchesRegex(@"MSTestSdk.*? \(net10\.0\|x64\) passed");
#if !SKIP_INTERMEDIATE_TARGET_FRAMEWORKS
        compilationResult.AssertOutputMatchesRegex(@"MSTestSdk.*? \(net8\.0\|x64\) passed");
#endif

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            compilationResult.AssertOutputMatchesRegex(@"MSTestSdk.*? \(net48\|x64\) passed");
        }
    }

    [TestMethod]
    [DynamicData(nameof(GetBuildMatrixMultiTfmFoldedBuildConfiguration), typeof(AcceptanceTestBase<NopAssetFixture>))]
    public async Task RunTests_With_MSTestRunner_Standalone(string multiTfm, BuildConfiguration buildConfiguration)
    {
        using TestAsset testAsset = await TestAsset.GenerateAssetAsync(
               AssetName,
               SingleTestSourceCode
               .PatchCodeWithReplace("$MSTestVersion$", MSTestVersion)
               .PatchCodeWithReplace("$TargetFramework$", multiTfm)
               .PatchCodeWithReplace("$ExtraProperties$", string.Empty));

        DotnetMuxerResult compilationResult = await DotnetCli.RunAsync($"build -c {buildConfiguration} {testAsset.TargetAssetPath}", cancellationToken: TestContext.CancellationToken);
        compilationResult.AssertExitCodeIs(0);
        foreach (string tfm in multiTfm.Split(";"))
        {
            var testHost = TestHost.LocateFrom(testAsset.TargetAssetPath, AssetName, tfm, buildConfiguration: buildConfiguration);
            TestHostResult testHostResult = await testHost.ExecuteAsync(cancellationToken: TestContext.CancellationToken);
            testHostResult.AssertOutputContainsSummary(0, 1, 0);
        }
    }

    [TestMethod]
    [DynamicData(nameof(GetBuildMatrixMultiTfmFoldedBuildConfiguration), typeof(AcceptanceTestBase<NopAssetFixture>))]
    public async Task RunTests_With_CentralPackageManagement_Standalone(string multiTfm, BuildConfiguration buildConfiguration)
    {
        // Exercise CPM with CentralPackageVersionOverrideEnabled=false to ensure MSTest.Sdk
        // does not rely on the (then-forbidden) VersionOverride attribute and instead injects
        // PackageVersion items for its implicit references.
        const string CpmSourceCode = SingleTestSourceCode + """

#file Directory.Packages.props
<Project>
  <PropertyGroup>
    <ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>
    <CentralPackageVersionOverrideEnabled>false</CentralPackageVersionOverrideEnabled>
  </PropertyGroup>
</Project>
""";

        using TestAsset testAsset = await TestAsset.GenerateAssetAsync(
               AssetName,
               CpmSourceCode
               .PatchCodeWithReplace("$MSTestVersion$", MSTestVersion)
               .PatchCodeWithReplace("$TargetFramework$", multiTfm)
               .PatchCodeWithReplace("$ExtraProperties$", string.Empty));

        DotnetMuxerResult compilationResult = await DotnetCli.RunAsync($"build -c {buildConfiguration} {testAsset.TargetAssetPath}", cancellationToken: TestContext.CancellationToken);
        compilationResult.AssertExitCodeIs(0);
        foreach (string tfm in multiTfm.Split(";"))
        {
            var testHost = TestHost.LocateFrom(testAsset.TargetAssetPath, AssetName, tfm, buildConfiguration: buildConfiguration);
            TestHostResult testHostResult = await testHost.ExecuteAsync(cancellationToken: TestContext.CancellationToken);
            testHostResult.AssertOutputContainsSummary(0, 1, 0);
        }
    }

    public static IEnumerable<TestDataRow<(string MultiTfm, BuildConfiguration BuildConfiguration, string MSBuildExtensionEnableFragment, string EnableCommandLineArg, string InvalidCommandLineArg)>> RunTests_With_MSTestRunner_Standalone_Plus_Extensions_Data()
    {
        foreach ((string MultiTfm, BuildConfiguration BuildConfiguration) buildConfig in GetBuildMatrixMultiTfmFoldedBuildConfiguration())
        {
            yield return new((buildConfig.MultiTfm, buildConfig.BuildConfiguration,
                "<EnableMicrosoftTestingExtensionsCodeCoverage>true</EnableMicrosoftTestingExtensionsCodeCoverage>",
                "--coverage",
                "--crashdump"));

            yield return new((buildConfig.MultiTfm, buildConfig.BuildConfiguration,
              "<EnableMicrosoftTestingExtensionsRetry>true</EnableMicrosoftTestingExtensionsRetry>",
              "--retry-failed-tests 3",
              "--crashdump"));

            yield return new((buildConfig.MultiTfm, buildConfig.BuildConfiguration,
              "<EnableMicrosoftTestingExtensionsTrxReport>true</EnableMicrosoftTestingExtensionsTrxReport>",
              "--report-trx",
              "--crashdump"));

            yield return new((buildConfig.MultiTfm, buildConfig.BuildConfiguration,
              "<EnableMicrosoftTestingExtensionsCrashDump>true</EnableMicrosoftTestingExtensionsCrashDump>",
              "--crashdump",
              "--hangdump"));

            yield return new((buildConfig.MultiTfm, buildConfig.BuildConfiguration,
              "<EnableMicrosoftTestingExtensionsHangDump>true</EnableMicrosoftTestingExtensionsHangDump>",
              "--hangdump",
              "--crashdump"));

            yield return new((buildConfig.MultiTfm, buildConfig.BuildConfiguration,
              "<EnableMicrosoftTestingExtensionsAzureDevOpsReport>true</EnableMicrosoftTestingExtensionsAzureDevOpsReport>",
              "--report-azdo",
              "--crashdump"));
        }
    }

    [TestMethod]
    [DynamicData(nameof(RunTests_With_MSTestRunner_Standalone_Plus_Extensions_Data))]
    public async Task RunTests_With_MSTestRunner_Standalone_Selectively_Enabled_Extensions(string multiTfm, BuildConfiguration buildConfiguration,
        string msbuildExtensionEnableFragment,
        string enableCommandLineArg,
        string invalidCommandLineArg)
    {
        using TestAsset testAsset = await TestAsset.GenerateAssetAsync(
               AssetName,
               SingleTestSourceCode
               .PatchCodeWithReplace("$MSTestVersion$", MSTestVersion)
               .PatchCodeWithReplace("$TargetFramework$", multiTfm)
               .PatchCodeWithReplace("$ExtraProperties$", msbuildExtensionEnableFragment));

        DotnetMuxerResult compilationResult = await DotnetCli.RunAsync($"build -c {buildConfiguration} {testAsset.TargetAssetPath}", cancellationToken: TestContext.CancellationToken);
        compilationResult.AssertExitCodeIs(0);
        foreach (string tfm in multiTfm.Split(";"))
        {
            var testHost = TestHost.LocateFrom(testAsset.TargetAssetPath, AssetName, tfm, buildConfiguration: buildConfiguration);
            TestHostResult testHostResult = await testHost.ExecuteAsync(command: enableCommandLineArg, cancellationToken: TestContext.CancellationToken);
            testHostResult.AssertOutputContainsSummary(0, 1, 0);

            testHostResult = await testHost.ExecuteAsync(command: invalidCommandLineArg, cancellationToken: TestContext.CancellationToken);
            testHostResult.AssertExitCodeIs(ExitCode.InvalidCommandLine);
        }
    }

    [TestMethod]
    [DynamicData(nameof(GetBuildMatrixMultiTfmFoldedBuildConfiguration), typeof(AcceptanceTestBase<NopAssetFixture>))]
    public async Task RunTests_With_MSTestRunner_Standalone_EnableAll_Extensions(string multiTfm, BuildConfiguration buildConfiguration)
    {
        using TestAsset testAsset = await TestAsset.GenerateAssetAsync(
               AssetName,
               SingleTestSourceCode
               .PatchCodeWithReplace("$MSTestVersion$", MSTestVersion)
               .PatchCodeWithReplace("$TargetFramework$", multiTfm)
               .PatchCodeWithReplace("$ExtraProperties$", "<TestingExtensionsProfile>AllMicrosoft</TestingExtensionsProfile>"), addPublicFeeds: true);

        DotnetMuxerResult compilationResult = await DotnetCli.RunAsync($"build -c {buildConfiguration} {testAsset.TargetAssetPath}", cancellationToken: TestContext.CancellationToken);
        compilationResult.AssertExitCodeIs(0);
        foreach (string tfm in multiTfm.Split(";"))
        {
            var testHost = TestHost.LocateFrom(testAsset.TargetAssetPath, AssetName, tfm, buildConfiguration: buildConfiguration);
            TestHostResult testHostResult = await testHost.ExecuteAsync(command: "--coverage --retry-failed-tests 3 --report-trx --crashdump --hangdump --report-azdo", cancellationToken: TestContext.CancellationToken);
            testHostResult.AssertOutputContainsSummary(0, 1, 0);
        }
    }

    public static IEnumerable<(string MultiTfm, BuildConfiguration BuildConfiguration, bool EnableDefaultExtensions)> RunTests_With_MSTestRunner_Standalone_Default_Extensions_Data()
    {
        foreach ((string MultiTfm, BuildConfiguration BuildConfiguration) buildConfig in GetBuildMatrixMultiTfmFoldedBuildConfiguration())
        {
            yield return new(buildConfig.MultiTfm, buildConfig.BuildConfiguration, true);
            yield return new(buildConfig.MultiTfm, buildConfig.BuildConfiguration, false);
        }
    }

    [TestMethod]
    [DynamicData(nameof(RunTests_With_MSTestRunner_Standalone_Default_Extensions_Data))]
    public async Task RunTests_With_MSTestRunner_Standalone_Enable_Default_Extensions(string multiTfm, BuildConfiguration buildConfiguration, bool enableDefaultExtensions)
    {
        using TestAsset testAsset = await TestAsset.GenerateAssetAsync(
               AssetName,
               SingleTestSourceCode
               .PatchCodeWithReplace("$MSTestVersion$", MSTestVersion)
               .PatchCodeWithReplace("$TargetFramework$", multiTfm)
               .PatchCodeWithReplace("$ExtraProperties$", enableDefaultExtensions ? string.Empty : "<TestingExtensionsProfile>None</TestingExtensionsProfile>"));

        DotnetMuxerResult compilationResult = await DotnetCli.RunAsync($"build -c {buildConfiguration} {testAsset.TargetAssetPath}", cancellationToken: TestContext.CancellationToken);
        compilationResult.AssertExitCodeIs(0);
        foreach (string tfm in multiTfm.Split(";"))
        {
            var testHost = TestHost.LocateFrom(testAsset.TargetAssetPath, AssetName, tfm, buildConfiguration: buildConfiguration);
            TestHostResult testHostResult = await testHost.ExecuteAsync(command: "--coverage --report-trx", cancellationToken: TestContext.CancellationToken);
            if (enableDefaultExtensions)
            {
                testHostResult.AssertOutputContainsSummary(0, 1, 0);
            }
            else
            {
                testHostResult.AssertExitCodeIs(ExitCode.InvalidCommandLine);
            }
        }
    }

    [TestMethod]
    [DynamicData(nameof(GetBuildMatrixMultiTfmFoldedBuildConfiguration), typeof(AcceptanceTestBase<NopAssetFixture>))]
    public async Task Invalid_TestingProfile_Name_Should_Fail(string multiTfm, BuildConfiguration buildConfiguration)
    {
        using TestAsset testAsset = await TestAsset.GenerateAssetAsync(
               AssetName,
               SingleTestSourceCode
               .PatchCodeWithReplace("$MSTestVersion$", MSTestVersion)
               .PatchCodeWithReplace("$TargetFramework$", multiTfm)
               .PatchCodeWithReplace("$ExtraProperties$", "<TestingExtensionsProfile>WrongName</TestingExtensionsProfile>"));

        DotnetMuxerResult compilationResult = await DotnetCli.RunAsync($"build -c {buildConfiguration} {testAsset.TargetAssetPath}", failIfReturnValueIsNotZero: false, cancellationToken: TestContext.CancellationToken);
        compilationResult.AssertExitCodeIs(1);
        compilationResult.AssertOutputContains("Invalid value for property TestingExtensionsProfile. Valid values are 'Default', 'AllMicrosoft' and 'None'.");
    }

    [TestMethod]
    [OSCondition(ConditionMode.Exclude, OperatingSystems.OSX)]
    public async Task NativeAot_Smoke_Test()
    {
        using TestAsset testAsset = await TestAsset.GenerateAssetAsync(
            AssetName,
            SingleTestSourceCode
            .PatchCodeWithReplace("$MSTestVersion$", MSTestVersion)
            .PatchCodeWithReplace("$TargetFramework$", TargetFrameworks.NetCurrent)
            .PatchCodeWithReplace("$ExtraProperties$", """
                <PublishAot>true</PublishAot>
                <EnableMicrosoftTestingExtensionsCodeCoverage>false</EnableMicrosoftTestingExtensionsCodeCoverage>
                <!-- Show individual trim/AOT warnings instead of a single IL2104 per assembly -->
                <TrimmerSingleWarn>false</TrimmerSingleWarn>
                """),
            addPublicFeeds: true);

        DotnetMuxerResult compilationResult = await DotnetCli.RunAsync(
            $"publish -r {RID} -f {TargetFrameworks.NetCurrent} {testAsset.TargetAssetPath}",
            cancellationToken: TestContext.CancellationToken);
        compilationResult.AssertOutputContains("Generating native code");

        var testHost = TestHost.LocateFrom(testAsset.TargetAssetPath, AssetName, TargetFrameworks.NetCurrent, verb: Verb.publish);
        TestHostResult testHostResult = await testHost.ExecuteAsync(cancellationToken: TestContext.CancellationToken);

        testHostResult.AssertExitCodeIs(ExitCode.Success);
        testHostResult.AssertOutputContainsSummary(0, 1, 0);
    }

    [TestMethod]
    public async Task SettingIsTestApplicationToFalseReducesAddedExtensionsAndMakesProjectNotExecutable()
    {
        using TestAsset testAsset = await TestAsset.GenerateAssetAsync(
               AssetName,
               SingleTestSourceCodeVSTest
               .PatchCodeWithReplace("$MSTestVersion$", MSTestVersion)
               .PatchCodeWithReplace("$TargetFramework$", TargetFrameworks.NetCurrent)
               .PatchCodeWithReplace("$ExtraProperties$", "<IsTestApplication>false</IsTestApplication>"));

        DotnetMuxerResult compilationResult = await DotnetCli.RunAsync($"test {testAsset.TargetAssetPath}", workingDirectory: testAsset.TargetAssetPath, cancellationToken: TestContext.CancellationToken);

        compilationResult.AssertExitCodeIs(0);

        SL.Build binLog = SL.Serialization.Read(compilationResult.BinlogPath);
        SL.Task cscTask = binLog.FindChildrenRecursive<SL.Task>(task => task.Name == "Csc").Single();
        SL.Item[] references = [.. cscTask.FindChildrenRecursive<SL.Parameter>(p => p.Name == "References").Single().Children.OfType<SL.Item>()];

        // Ensure that MSTest.Framework is referenced
        Assert.Contains(r => r.Text.EndsWith("MSTest.TestFramework.dll", StringComparison.OrdinalIgnoreCase), references);
        Assert.Contains(r => r.Text.EndsWith("MSTest.TestFramework.Extensions.dll", StringComparison.OrdinalIgnoreCase), references);

        // No adapter, no extensions, no vstest sdk
        Assert.DoesNotContain(r => r.Text.EndsWith("MSTest.TestAdapter.dll", StringComparison.OrdinalIgnoreCase), references);
        Assert.DoesNotContain(r => r.Text.Contains("Microsoft.Testing.Extensions.", StringComparison.OrdinalIgnoreCase), references);

        // It's not an executable
        Assert.DoesNotContain(p => p.Value == "Exe", binLog.FindChildrenRecursive<SL.Property>(p => p.Name == "OutputType"));
    }
}
