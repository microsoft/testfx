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

        // 'dotnet test' runs the target frameworks in parallel and writes each framework's summary as two separate
        // writes: the 'Passed!  - Failed: ...' counts, then the ' - MSTestSdk.dll (<tfm>)' suffix. Those writes
        // interleave, so the two halves of one framework's summary are not guaranteed to share an output line. Assert
        // the two independent facts instead: every expected framework ran, and every framework reported a clean result.
        List<string> expectedTargetFrameworks = ["net10.0"];
#if !SKIP_INTERMEDIATE_TARGET_FRAMEWORKS
        expectedTargetFrameworks.Add("net8.0");
#endif

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            expectedTargetFrameworks.Add("net462");
        }

        foreach (string targetFramework in expectedTargetFrameworks)
        {
            compilationResult.AssertOutputContains($" - MSTestSdk.dll ({targetFramework})");
        }

        compilationResult.AssertOutputMatchesRegexTimes("Passed!  - Failed:     0, Passed:     1, Skipped:     0, Total:     1", expectedTargetFrameworks.Count);
        compilationResult.AssertOutputDoesNotContain("Failed!");
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

        DotnetMuxerResult compilationResult = await DotnetCli.RunAsync($"test -c {buildConfiguration} --project {testAsset.TargetAssetPath} --progress off --no-ansi", workingDirectory: testAsset.TargetAssetPath, cancellationToken: TestContext.CancellationToken);
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
    [DataRow("MTP", "Unset", "", "cs;de;es;fr;it;ja;ko;pl;pt-BR;ru;tr;zh-Hans;zh-Hant")]
    [DataRow("MTP", "Empty", "<SatelliteResourceLanguages />", "cs;de;es;fr;it;ja;ko;pl;pt-BR;ru;tr;zh-Hans;zh-Hant")]
    [DataRow("MTP", "SingleLanguage", "<SatelliteResourceLanguages>fr</SatelliteResourceLanguages>", "fr")]
    [DataRow("MTP", "CaseInsensitiveLanguage", "<SatelliteResourceLanguages>FR</SatelliteResourceLanguages>", "fr")]
    [DataRow("MTP", "MultipleLanguages", "<SatelliteResourceLanguages>fr;ja</SatelliteResourceLanguages>", "fr;ja")]
    [DataRow("MTP", "SpecificCultureDoesNotMatchParent", "<SatelliteResourceLanguages>fr-FR</SatelliteResourceLanguages>", "")]
    [DataRow("MTP", "ParentCultureDoesNotMatchSpecific", "<SatelliteResourceLanguages>pt</SatelliteResourceLanguages>", "")]
    [DataRow("MTP", "NeutralLanguageOnly", "<NeutralLanguage>en-US</NeutralLanguage>", "cs;de;es;fr;it;ja;ko;pl;pt-BR;ru;tr;zh-Hans;zh-Hant")]
    [DataRow("MTP", "NeutralLanguageFilter", "<NeutralLanguage>en-US</NeutralLanguage><SatelliteResourceLanguages>en-US</SatelliteResourceLanguages>", "")]
    [DataRow("VSTest", "Unset", "", "fr")]
    [DataRow("VSTest", "Empty", "<SatelliteResourceLanguages />", "fr")]
    [DataRow("VSTest", "SingleLanguage", "<SatelliteResourceLanguages>fr</SatelliteResourceLanguages>", "fr")]
    [DataRow("VSTest", "CaseInsensitiveLanguage", "<SatelliteResourceLanguages>FR</SatelliteResourceLanguages>", "fr")]
    [DataRow("VSTest", "MultipleLanguages", "<SatelliteResourceLanguages>fr;ja</SatelliteResourceLanguages>", "fr")]
    [DataRow("VSTest", "DifferentLanguage", "<SatelliteResourceLanguages>ja</SatelliteResourceLanguages>", "")]
    [DataRow("VSTest", "SpecificCultureDoesNotMatchParent", "<SatelliteResourceLanguages>fr-FR</SatelliteResourceLanguages>", "")]
    [DataRow("VSTest", "NeutralLanguageOnly", "<NeutralLanguage>en-US</NeutralLanguage>", "fr")]
    [DataRow("VSTest", "NeutralLanguageFilter", "<NeutralLanguage>en-US</NeutralLanguage><SatelliteResourceLanguages>en-US</SatelliteResourceLanguages>", "")]
    public async Task SatelliteResourceLanguages_FiltersAdapterResources(string runner, string scenario, string properties, string expectedCultures)
    {
        string assetName = $"{AssetName}{runner}Satellites{scenario}";
        string runnerProperties = runner == "VSTest" ? "<UseVSTest>true</UseVSTest>" : string.Empty;
        using TestAsset testAsset = await TestAsset.GenerateAssetAsync(
            assetName,
            SingleTestSourceCode
                .PatchCodeWithReplace("MSTestSdk.csproj", $"{assetName}.csproj")
                .PatchCodeWithReplace("$MSTestVersion$", MSTestVersion)
                .PatchCodeWithReplace("$TargetFramework$", TargetFrameworks.NetCurrent)
                .PatchCodeWithReplace("$ExtraProperties$", runnerProperties + properties));

        DotnetMuxerResult compilationResult = await DotnetCli.RunAsync(
            $"build -c {BuildConfiguration.Release} {testAsset.TargetAssetPath}",
            environmentVariables: new() { ["DOTNET_CLI_UI_LANGUAGE"] = "fr" },
            cancellationToken: TestContext.CancellationToken);
        compilationResult.AssertExitCodeIs(0);

        string outputDirectory = Path.Combine(testAsset.TargetAssetPath, "bin", BuildConfiguration.Release.ToString(), TargetFrameworks.NetCurrent);
        string[] expected = expectedCultures.Split(';', StringSplitOptions.RemoveEmptyEntries);

        AssertSatelliteCultures(outputDirectory, "MSTest.TestAdapter.resources.dll", expected);
        AssertSatelliteCultures(outputDirectory, "MSTestAdapter.PlatformServices.resources.dll", expected);
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Windows, IgnoreMessage = "Classic UWP package assets are produced only on Windows.")]
    [DataRow("Unset", "", "fr;fr")]
    [DataRow("Empty", "<SatelliteResourceLanguages />", "fr;fr")]
    [DataRow("MatchingLanguage", "<SatelliteResourceLanguages>fr</SatelliteResourceLanguages>", "fr;fr")]
    [DataRow("CaseInsensitiveLanguage", "<SatelliteResourceLanguages>FR</SatelliteResourceLanguages>", "fr;fr")]
    [DataRow("DifferentLanguage", "<SatelliteResourceLanguages>ja</SatelliteResourceLanguages>", "")]
    [DataRow("SpecificCultureDoesNotMatchParent", "<SatelliteResourceLanguages>fr-FR</SatelliteResourceLanguages>", "")]
    public async Task SatelliteResourceLanguages_FiltersClassicUwpAdapterResources(string scenario, string properties, string expectedCultures)
    {
        const string Source = """
            #file ClassicUwpSatelliteResources.csproj
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>$TargetFramework$</TargetFramework>
                $Properties$
              </PropertyGroup>

              <ItemGroup>
                <PackageReference Include="MSTest.TestAdapter" Version="$MSTestVersion$" GeneratePathProperty="true" ExcludeAssets="all" />
              </ItemGroup>

              <Import Project="$(PkgMSTest_TestAdapter)\buildTransitive\uap10.0\MSTest.TestAdapter.targets"
                      Condition=" '$(PkgMSTest_TestAdapter)' != '' " />

              <Target Name="PrintSatelliteCultures" DependsOnTargets="GetMSTestV2CultureHierarchy">
                <Message Text="SatelliteCultures=[@(MSTestV2Files->'%(UICulture)')]" Importance="High" />
              </Target>
            </Project>
            """;

        using TestAsset testAsset = await TestAsset.GenerateAssetAsync(
            $"{AssetName}ClassicUwpSatellites{scenario}",
            Source
                .PatchCodeWithReplace("$MSTestVersion$", MSTestVersion)
                .PatchCodeWithReplace("$TargetFramework$", TargetFrameworks.NetCurrent)
                .PatchCodeWithReplace("$Properties$", properties));

        DotnetMuxerResult result = await DotnetCli.RunAsync(
            $"msbuild {testAsset.TargetAssetPath} -restore -t:PrintSatelliteCultures",
            environmentVariables: new() { ["DOTNET_CLI_UI_LANGUAGE"] = "fr" },
            cancellationToken: TestContext.CancellationToken);

        result.AssertExitCodeIs(0);
        result.AssertOutputContains($"SatelliteCultures=[{expectedCultures}]");
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Windows, IgnoreMessage = "Classic UWP package assets are produced only on Windows.")]
    public async Task SatelliteResourceLanguages_FiltersClassicUwpMtpAdapterResources()
    {
        const string Source = """
            #file ClassicUwpMtpSatelliteResources.csproj
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>$TargetFramework$</TargetFramework>
                <EnableMSTestRunner>true</EnableMSTestRunner>
                <SatelliteResourceLanguages>fr;ja</SatelliteResourceLanguages>
              </PropertyGroup>

              <ItemGroup>
                <PackageReference Include="MSTest.TestAdapter" Version="$MSTestVersion$" GeneratePathProperty="true" ExcludeAssets="all" />
              </ItemGroup>

              <Import Project="$(PkgMSTest_TestAdapter)\buildTransitive\uap10.0\MSTest.TestAdapter.targets"
                      Condition=" '$(PkgMSTest_TestAdapter)' != '' " />

              <Target Name="PrintSatelliteCultures" DependsOnTargets="GetMSTestV2CultureHierarchy">
                <Message Text="SatelliteCultures=[@(MSTestV2Files->'%(Culture)')]" Importance="High" />
              </Target>
            </Project>
            """;

        using TestAsset testAsset = await TestAsset.GenerateAssetAsync(
            $"{AssetName}ClassicUwpMtpSatellites",
            Source
                .PatchCodeWithReplace("$MSTestVersion$", MSTestVersion)
                .PatchCodeWithReplace("$TargetFramework$", TargetFrameworks.NetCurrent));

        DotnetMuxerResult result = await DotnetCli.RunAsync(
            $"msbuild {testAsset.TargetAssetPath} -restore -t:PrintSatelliteCultures",
            environmentVariables: new() { ["DOTNET_CLI_UI_LANGUAGE"] = "de" },
            cancellationToken: TestContext.CancellationToken);

        result.AssertExitCodeIs(0);
        result.AssertOutputContains("SatelliteCultures=[fr;fr;ja;ja]");
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

            yield return new((buildConfig.MultiTfm, buildConfig.BuildConfiguration,
              "<EnableMicrosoftTestingExtensionsGitHubActionsReport>true</EnableMicrosoftTestingExtensionsGitHubActionsReport>",
              "--report-gh",
              "--crashdump"));

            yield return new((buildConfig.MultiTfm, buildConfig.BuildConfiguration,
              "<EnableMicrosoftTestingExtensionsCtrfReport>true</EnableMicrosoftTestingExtensionsCtrfReport>",
              "--report-ctrf",
              "--crashdump"));

            yield return new((buildConfig.MultiTfm, buildConfig.BuildConfiguration,
              "<EnableMicrosoftTestingExtensionsHtmlReport>true</EnableMicrosoftTestingExtensionsHtmlReport>",
              "--report-html",
              "--crashdump"));

            yield return new((buildConfig.MultiTfm, buildConfig.BuildConfiguration,
              "<EnableMicrosoftTestingExtensionsJUnitReport>true</EnableMicrosoftTestingExtensionsJUnitReport>",
              "--report-junit",
              "--crashdump"));

            // OpenTelemetry is API-only (no CLI flag); the enable property only opts the package into the build.
            // We pass an empty enable arg to validate the package restores and the test host runs cleanly, and we
            // assert that an unrelated extension's CLI arg ('--crashdump') is rejected to prove no other extensions leaked in.
            yield return new((buildConfig.MultiTfm, buildConfig.BuildConfiguration,
              "<EnableMicrosoftTestingExtensionsOpenTelemetry>true</EnableMicrosoftTestingExtensionsOpenTelemetry>",
              string.Empty,
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
            TestHostResult testHostResult = await testHost.ExecuteAsync(command: "--coverage --retry-failed-tests 3 --report-trx --crashdump --hangdump --report-azdo --report-gh --report-html", cancellationToken: TestContext.CancellationToken);
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
            warnAsError: true,
            cancellationToken: TestContext.CancellationToken);
        compilationResult.AssertOutputContains("Generating native code");

        foreach (string fileName in TrimAndAotAssertions.MSTestOwnedSourceFiles)
        {
            compilationResult.AssertOutputDoesNotContain(fileName);
        }

        var testHost = TestHost.LocateFrom(testAsset.TargetAssetPath, AssetName, TargetFrameworks.NetCurrent, verb: Verb.publish);
        TestHostResult testHostResult = await testHost.ExecuteAsync(cancellationToken: TestContext.CancellationToken);

        testHostResult.AssertExitCodeIs(ExitCode.Success);
        testHostResult.AssertOutputContainsSummary(0, 1, 0);
    }

    [TestMethod]
    public async Task PublishReadyToRun_Smoke_Test()
    {
        using TestAsset testAsset = await TestAsset.GenerateAssetAsync(
            AssetName,
            SingleTestSourceCode
            .PatchCodeWithReplace("$MSTestVersion$", MSTestVersion)
            .PatchCodeWithReplace("$TargetFramework$", TargetFrameworks.NetCurrent)
            .PatchCodeWithReplace("$ExtraProperties$", """
                <PublishReadyToRun>true</PublishReadyToRun>
                <SelfContained>false</SelfContained>
                <EnableMicrosoftTestingExtensionsCodeCoverage>false</EnableMicrosoftTestingExtensionsCodeCoverage>
                """),
            addPublicFeeds: true);

        DotnetMuxerResult compilationResult = await DotnetCli.RunAsync(
            $"publish -r {RID} -f {TargetFrameworks.NetCurrent} {testAsset.TargetAssetPath}",
            warnAsError: true,
            cancellationToken: TestContext.CancellationToken);
        compilationResult.AssertExitCodeIs(0);

        var testHost = TestHost.LocateFrom(testAsset.TargetAssetPath, AssetName, TargetFrameworks.NetCurrent, RID, Verb.publish);

        string publishedAssemblyPath = Path.Combine(testHost.DirectoryName, $"{AssetName}.dll");
        ReadyToRunAssertions.AssertIsReadyToRunImage(publishedAssemblyPath);

        TestHostResult testHostResult = await testHost.ExecuteAsync(cancellationToken: TestContext.CancellationToken);
        testHostResult.AssertExitCodeIs(ExitCode.Success);
        testHostResult.AssertOutputContainsSummary(failed: 0, passed: 1, skipped: 0);
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

        SL.Build binLog = BinlogReader.Read(compilationResult.BinlogPath!);
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

    [TestMethod]
    public async Task TestApplicationRunsTestsFromReferencedMSTestSdkTestLibrary()
    {
        const string TestApplicationWithReferencedTestLibrary = """
            #file MSTestSdkTestApplication.csproj
            <Project Sdk="MSTest.Sdk/$MSTestVersion$">

              <PropertyGroup>
                <EnableMicrosoftTestingPlatform>true</EnableMicrosoftTestingPlatform>
                <TargetFramework>$TargetFramework$</TargetFramework>
                <PlatformTarget>x64</PlatformTarget>
              </PropertyGroup>

              <ItemGroup>
                <ProjectReference Include="TestLibrary/TestLibrary.csproj" />
                <Compile Remove="TestLibrary/**" />
              </ItemGroup>

            </Project>

            #file ReferencedLibraryTests.cs
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            using TestLibrary;

            namespace MSTestSdkTestApplication;

            [TestClass]
            public sealed class ReferencedLibraryTests : LibraryTests;

            #file TestLibrary/TestLibrary.csproj
            <Project Sdk="MSTest.Sdk/$MSTestVersion$">

              <PropertyGroup>
                <IsTestApplication>false</IsTestApplication>
                <TargetFramework>$TargetFramework$</TargetFramework>
              </PropertyGroup>

            </Project>

            #file TestLibrary/LibraryTests.cs
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            namespace TestLibrary;

            [TestClass]
            public abstract class LibraryTests
            {
                [TestMethod]
                public void TestFromReferencedLibrary()
                {
                    Assert.AreEqual("TestLibrary", typeof(LibraryTests).Assembly.GetName().Name);
                }
            }
            """;

        using TestAsset testAsset = await TestAsset.GenerateAssetAsync(
            "MSTestSdkTestApplicationWithReferencedTestLibrary",
            TestApplicationWithReferencedTestLibrary
                .PatchCodeWithReplace("$MSTestVersion$", MSTestVersion)
                .PatchCodeWithReplace("$TargetFramework$", TargetFrameworks.NetCurrent));

        DotnetMuxerResult buildResult = await DotnetCli.RunAsync(
            $"build -c {BuildConfiguration.Release} {testAsset.TargetAssetPath}",
            cancellationToken: TestContext.CancellationToken);
        buildResult.AssertExitCodeIs(0);

        var testHost = TestHost.LocateFrom(
            testAsset.TargetAssetPath,
            "MSTestSdkTestApplication",
            TargetFrameworks.NetCurrent,
            buildConfiguration: BuildConfiguration.Release);
        TestHostResult testHostResult = await testHost.ExecuteAsync(cancellationToken: TestContext.CancellationToken);
        testHostResult.AssertOutputContainsSummary(0, 1, 0);

        string libraryAssets = await File.ReadAllTextAsync(
            Path.Combine(testAsset.TargetAssetPath, "TestLibrary", "obj", "project.assets.json"),
            TestContext.CancellationToken);
        Assert.DoesNotContain("\"MSTest.TestAdapter/", libraryAssets);
        Assert.DoesNotContain("\"Microsoft.NET.Test.Sdk/", libraryAssets);
        Assert.DoesNotContain("\"Microsoft.Testing.Platform/", libraryAssets);
        Assert.DoesNotContain("\"Microsoft.Testing.Platform.MSBuild/", libraryAssets);
        Assert.DoesNotContain("\"Microsoft.Testing.Extensions.", libraryAssets);
    }

    [TestMethod]
    public async Task MSTestParallelizeScope_ClassLevel_EmitsParallelizeAttribute()
    {
        using TestAsset testAsset = await TestAsset.GenerateAssetAsync(
            AssetName,
            SingleTestSourceCode
            .PatchCodeWithReplace("$MSTestVersion$", MSTestVersion)
            .PatchCodeWithReplace("$TargetFramework$", TargetFrameworks.NetCurrent)
            .PatchCodeWithReplace("$ExtraProperties$", "<MSTestParallelizeScope>ClassLevel</MSTestParallelizeScope>"));

        DotnetMuxerResult compilationResult = await DotnetCli.RunAsync($"build -c {BuildConfiguration.Release} {testAsset.TargetAssetPath}", cancellationToken: TestContext.CancellationToken);
        compilationResult.AssertExitCodeIs(0);

        var testHost = TestHost.LocateFrom(testAsset.TargetAssetPath, AssetName, TargetFrameworks.NetCurrent, buildConfiguration: BuildConfiguration.Release);
        TestHostResult testHostResult = await testHost.ExecuteAsync(cancellationToken: TestContext.CancellationToken);
        testHostResult.AssertOutputContainsSummary(0, 1, 0);
        testHostResult.AssertOutputMatchesRegex(@"Test Parallelization enabled .* \(Workers: \d+, Scope: ClassLevel\)");
    }

    [TestMethod]
    public async Task MSTestParallelizeScope_MethodLevelWithWorkers_EmitsParallelizeAttribute()
    {
        using TestAsset testAsset = await TestAsset.GenerateAssetAsync(
            AssetName,
            SingleTestSourceCode
            .PatchCodeWithReplace("$MSTestVersion$", MSTestVersion)
            .PatchCodeWithReplace("$TargetFramework$", TargetFrameworks.NetCurrent)
            .PatchCodeWithReplace("$ExtraProperties$", """
                <MSTestParallelizeScope>MethodLevel</MSTestParallelizeScope>
                <MSTestParallelizeWorkers>3</MSTestParallelizeWorkers>
                """));

        DotnetMuxerResult compilationResult = await DotnetCli.RunAsync($"build -c {BuildConfiguration.Release} {testAsset.TargetAssetPath}", cancellationToken: TestContext.CancellationToken);
        compilationResult.AssertExitCodeIs(0);

        var testHost = TestHost.LocateFrom(testAsset.TargetAssetPath, AssetName, TargetFrameworks.NetCurrent, buildConfiguration: BuildConfiguration.Release);
        TestHostResult testHostResult = await testHost.ExecuteAsync(cancellationToken: TestContext.CancellationToken);
        testHostResult.AssertOutputContainsSummary(0, 1, 0);
        testHostResult.AssertOutputContains("Test Parallelization enabled");
        testHostResult.AssertOutputContains("(Workers: 3, Scope: MethodLevel)");
    }

    [TestMethod]
    public async Task MSTestParallelizeWorkers_Only_EmitsParallelizeAttributeWithDefaultScope()
    {
        using TestAsset testAsset = await TestAsset.GenerateAssetAsync(
            AssetName,
            SingleTestSourceCode
            .PatchCodeWithReplace("$MSTestVersion$", MSTestVersion)
            .PatchCodeWithReplace("$TargetFramework$", TargetFrameworks.NetCurrent)
            .PatchCodeWithReplace("$ExtraProperties$", "<MSTestParallelizeWorkers>2</MSTestParallelizeWorkers>"));

        DotnetMuxerResult compilationResult = await DotnetCli.RunAsync($"build -c {BuildConfiguration.Release} {testAsset.TargetAssetPath}", cancellationToken: TestContext.CancellationToken);
        compilationResult.AssertExitCodeIs(0);

        var testHost = TestHost.LocateFrom(testAsset.TargetAssetPath, AssetName, TargetFrameworks.NetCurrent, buildConfiguration: BuildConfiguration.Release);
        TestHostResult testHostResult = await testHost.ExecuteAsync(cancellationToken: TestContext.CancellationToken);
        testHostResult.AssertOutputContainsSummary(0, 1, 0);

        // ParallelizeAttribute defaults Scope to ClassLevel when only Workers is provided.
        testHostResult.AssertOutputContains("Test Parallelization enabled");
        testHostResult.AssertOutputContains("(Workers: 2, Scope: ClassLevel)");
    }

    [TestMethod]
    public async Task MSTestParallelizeWorkers_Zero_IsAcceptedAndEmitsParallelizeAttribute()
    {
        // Workers=0 is a boundary: the regex ^\d+$ accepts it and ParallelizeAttribute
        // interprets it as "use the number of available processors". Make sure the build
        // succeeds and the runtime picks it up.
        using TestAsset testAsset = await TestAsset.GenerateAssetAsync(
            AssetName,
            SingleTestSourceCode
            .PatchCodeWithReplace("$MSTestVersion$", MSTestVersion)
            .PatchCodeWithReplace("$TargetFramework$", TargetFrameworks.NetCurrent)
            .PatchCodeWithReplace("$ExtraProperties$", "<MSTestParallelizeWorkers>0</MSTestParallelizeWorkers>"));

        DotnetMuxerResult compilationResult = await DotnetCli.RunAsync($"build -c {BuildConfiguration.Release} {testAsset.TargetAssetPath}", cancellationToken: TestContext.CancellationToken);
        compilationResult.AssertExitCodeIs(0);

        var testHost = TestHost.LocateFrom(testAsset.TargetAssetPath, AssetName, TargetFrameworks.NetCurrent, buildConfiguration: BuildConfiguration.Release);
        TestHostResult testHostResult = await testHost.ExecuteAsync(cancellationToken: TestContext.CancellationToken);
        testHostResult.AssertOutputContainsSummary(0, 1, 0);
        testHostResult.AssertOutputContains("Test Parallelization enabled");

        // 0 workers means "use Environment.ProcessorCount" so the actual reported value is the host CPU count, not 0.
        testHostResult.AssertOutputMatchesRegex(@"\(Workers: \d+, Scope: ClassLevel\)");
    }

    [TestMethod]
    public async Task MSTestParallelizeScope_None_EmitsDoNotParallelizeAttribute()
    {
        using TestAsset testAsset = await TestAsset.GenerateAssetAsync(
            AssetName,
            SingleTestSourceCode
            .PatchCodeWithReplace("$MSTestVersion$", MSTestVersion)
            .PatchCodeWithReplace("$TargetFramework$", TargetFrameworks.NetCurrent)
            .PatchCodeWithReplace("$ExtraProperties$", "<MSTestParallelizeScope>None</MSTestParallelizeScope>"));

        DotnetMuxerResult compilationResult = await DotnetCli.RunAsync($"build -c {BuildConfiguration.Release} {testAsset.TargetAssetPath}", cancellationToken: TestContext.CancellationToken);
        compilationResult.AssertExitCodeIs(0);

        var testHost = TestHost.LocateFrom(testAsset.TargetAssetPath, AssetName, TargetFrameworks.NetCurrent, buildConfiguration: BuildConfiguration.Release);
        TestHostResult testHostResult = await testHost.ExecuteAsync(cancellationToken: TestContext.CancellationToken);
        testHostResult.AssertOutputContainsSummary(0, 1, 0);

        // DoNotParallelize disables parallelization entirely.
        testHostResult.AssertOutputDoesNotContain("Test Parallelization enabled");
    }

    [TestMethod]
    public async Task MSTestParallelizeScope_GenerateAssemblyInfoFalse_EmitsWarning()
    {
        // Without GenerateAssemblyInfo, the AssemblyAttribute items are not emitted, so the
        // properties would silently do nothing. Verify that the build succeeds but a warning
        // is reported to make this case discoverable.
        using TestAsset testAsset = await TestAsset.GenerateAssetAsync(
            AssetName,
            SingleTestSourceCode
            .PatchCodeWithReplace("$MSTestVersion$", MSTestVersion)
            .PatchCodeWithReplace("$TargetFramework$", TargetFrameworks.NetCurrent)
            .PatchCodeWithReplace("$ExtraProperties$", """
                <GenerateAssemblyInfo>false</GenerateAssemblyInfo>
                <MSTestParallelizeScope>ClassLevel</MSTestParallelizeScope>
                """));

        DotnetMuxerResult compilationResult = await DotnetCli.RunAsync($"build -c {BuildConfiguration.Release} {testAsset.TargetAssetPath}", warnAsError: false, cancellationToken: TestContext.CancellationToken);
        compilationResult.AssertExitCodeIs(0);
        compilationResult.AssertOutputContains("MSTestParallelizeScope and MSTestParallelizeWorkers require GenerateAssemblyInfo to be true to take effect.");
    }

    [TestMethod]
    public async Task MSTestParallelizeScope_InvalidValue_FailsBuild()
    {
        // An invalid scope value is emitted into the generated assembly attribute and rejected by the C# compiler (CS0117).
        using TestAsset testAsset = await TestAsset.GenerateAssetAsync(
            AssetName,
            SingleTestSourceCode
            .PatchCodeWithReplace("$MSTestVersion$", MSTestVersion)
            .PatchCodeWithReplace("$TargetFramework$", TargetFrameworks.NetCurrent)
            .PatchCodeWithReplace("$ExtraProperties$", "<MSTestParallelizeScope>NotAValidValue</MSTestParallelizeScope>"));

        DotnetMuxerResult compilationResult = await DotnetCli.RunAsync(
            $"build {testAsset.TargetAssetPath}",
            failIfReturnValueIsNotZero: false,
            cancellationToken: TestContext.CancellationToken);
        compilationResult.AssertExitCodeIs(1);
        compilationResult.AssertOutputContains("CS0117");
        compilationResult.AssertOutputContains("NotAValidValue");
    }

    [TestMethod]
    public async Task MSTestParallelizeWorkers_NonInteger_FailsBuild()
    {
        // A non-integer Workers value is emitted into the generated assembly attribute and rejected by the C# compiler (CS0103).
        using TestAsset testAsset = await TestAsset.GenerateAssetAsync(
            AssetName,
            SingleTestSourceCode
            .PatchCodeWithReplace("$MSTestVersion$", MSTestVersion)
            .PatchCodeWithReplace("$TargetFramework$", TargetFrameworks.NetCurrent)
            .PatchCodeWithReplace("$ExtraProperties$", "<MSTestParallelizeWorkers>abc</MSTestParallelizeWorkers>"));

        DotnetMuxerResult compilationResult = await DotnetCli.RunAsync(
            $"build {testAsset.TargetAssetPath}",
            failIfReturnValueIsNotZero: false,
            cancellationToken: TestContext.CancellationToken);
        compilationResult.AssertExitCodeIs(1);
        compilationResult.AssertOutputContains("CS0103");
        compilationResult.AssertOutputContains("abc");
    }

    [TestMethod]
    public async Task MSTestParallelizeScope_None_With_Workers_FailsBuild()
    {
        using TestAsset testAsset = await TestAsset.GenerateAssetAsync(
            AssetName,
            SingleTestSourceCode
            .PatchCodeWithReplace("$MSTestVersion$", MSTestVersion)
            .PatchCodeWithReplace("$TargetFramework$", TargetFrameworks.NetCurrent)
            .PatchCodeWithReplace("$ExtraProperties$", """
                <MSTestParallelizeScope>None</MSTestParallelizeScope>
                <MSTestParallelizeWorkers>2</MSTestParallelizeWorkers>
                """));

        DotnetMuxerResult compilationResult = await DotnetCli.RunAsync(
            $"build {testAsset.TargetAssetPath}",
            failIfReturnValueIsNotZero: false,
            cancellationToken: TestContext.CancellationToken);
        compilationResult.AssertExitCodeIs(1);
        compilationResult.AssertOutputContains("Property MSTestParallelizeWorkers cannot be set when MSTestParallelizeScope is 'None'.");
    }

    [TestMethod]
    public async Task MSTestParallelizeScope_VSTestRunner_BuildSucceeds()
    {
        // The targets emit assembly attributes via WriteCodeFragment, which is runner-agnostic.
        // Make sure the targets don't interfere with VSTest projects (build only — observing the
        // "Test Parallelization enabled" diagnostic is MTP-specific).
        using TestAsset testAsset = await TestAsset.GenerateAssetAsync(
            AssetName,
            SingleTestSourceCodeVSTest
            .PatchCodeWithReplace("$MSTestVersion$", MSTestVersion)
            .PatchCodeWithReplace("$TargetFramework$", TargetFrameworks.NetCurrent)
            .PatchCodeWithReplace("$ExtraProperties$", """
                <UseVSTest>true</UseVSTest>
                <MSTestParallelizeScope>MethodLevel</MSTestParallelizeScope>
                <MSTestParallelizeWorkers>2</MSTestParallelizeWorkers>
                """));

        DotnetMuxerResult compilationResult = await DotnetCli.RunAsync($"build -c {BuildConfiguration.Release} {testAsset.TargetAssetPath}", cancellationToken: TestContext.CancellationToken);
        compilationResult.AssertExitCodeIs(0);
    }

    // Verifies that the MSTestParallelizeScope/MSTestParallelizeWorkers properties also work for
    // projects that don't use MSTest.Sdk (i.e. they reference MSTest.TestAdapter directly via the
    // MSTest meta-package), since the targets logic ships in MSTest.TestAdapter.
    [TestMethod]
    public async Task MSTestParallelizeScope_NonSdkProject_EmitsParallelizeAttribute()
    {
        const string NonSdkSource = """
#file MSTestPlain.csproj
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>$TargetFramework$</TargetFramework>
    <OutputType>Exe</OutputType>
    <EnableMSTestRunner>true</EnableMSTestRunner>
    <PlatformTarget>x64</PlatformTarget>
    <NoWarn>$(NoWarn);NU1507</NoWarn>
    <MSTestParallelizeScope>MethodLevel</MSTestParallelizeScope>
    <MSTestParallelizeWorkers>3</MSTestParallelizeWorkers>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.Testing.Platform" Version="$MicrosoftTestingPlatformVersion$" />
    <PackageReference Include="MSTest" Version="$MSTestVersion$" />
  </ItemGroup>
</Project>

#file UnitTest1.cs
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MSTestPlainTest
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

        const string PlainAssetName = "MSTestPlain";

        using TestAsset testAsset = await TestAsset.GenerateAssetAsync(
            PlainAssetName,
            NonSdkSource
            .PatchCodeWithReplace("$MSTestVersion$", MSTestVersion)
            .PatchCodeWithReplace("$MicrosoftTestingPlatformVersion$", MicrosoftTestingPlatformVersion)
            .PatchCodeWithReplace("$TargetFramework$", TargetFrameworks.NetCurrent));

        DotnetMuxerResult compilationResult = await DotnetCli.RunAsync($"build -c {BuildConfiguration.Release} {testAsset.TargetAssetPath}", cancellationToken: TestContext.CancellationToken);
        compilationResult.AssertExitCodeIs(0);

        var testHost = TestHost.LocateFrom(testAsset.TargetAssetPath, PlainAssetName, TargetFrameworks.NetCurrent, buildConfiguration: BuildConfiguration.Release);
        TestHostResult testHostResult = await testHost.ExecuteAsync(cancellationToken: TestContext.CancellationToken);
        testHostResult.AssertOutputContainsSummary(0, 1, 0);
        testHostResult.AssertOutputContains("Test Parallelization enabled");
        testHostResult.AssertOutputContains("(Workers: 3, Scope: MethodLevel)");
    }

    // Verifies that MSTest.Sdk can be layered on top of a different base SDK (here Microsoft.NET.Sdk.Web)
    // using manual SDK imports without emitting MSB4011 duplicate-import warnings. See https://github.com/microsoft/testfx/issues/9562.
    [TestMethod]
    public async Task MSTestSdk_LayeredOnTopOfWebSdk_BuildsWithoutDuplicateImportWarnings()
    {
        const string MixedSdkSource = """
#file MSTestWeb.csproj
<Project>

  <Import Project="Sdk.props" Sdk="Microsoft.NET.Sdk.Web" />
  <Import Project="Sdk.props" Sdk="MSTest.Sdk" />

  <PropertyGroup>
    <EnableMicrosoftTestingPlatform>true</EnableMicrosoftTestingPlatform>
    <TargetFramework>$TargetFramework$</TargetFramework>
    <PlatformTarget>x64</PlatformTarget>
    <NoWarn>$(NoWarn);NU1507</NoWarn>
  </PropertyGroup>

  <Import Project="Sdk.targets" Sdk="MSTest.Sdk" />
  <Import Project="Sdk.targets" Sdk="Microsoft.NET.Sdk.Web" />

</Project>

#file global.json
{
  "msbuild-sdks": {
    "MSTest.Sdk": "$MSTestVersion$"
  }
}

#file UnitTest1.cs
using Microsoft.AspNetCore.Builder;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MSTestWebTest
{
    [TestClass]
    public class UnitTest1
    {
        [TestMethod]
        public void TestMethod1()
        {
            WebApplicationBuilder builder = WebApplication.CreateBuilder();
            Assert.IsNotNull(builder);
        }
    }
}
""";

        const string MixedAssetName = "MSTestWeb";

        using TestAsset testAsset = await TestAsset.GenerateAssetAsync(
            MixedAssetName,
            MixedSdkSource
            .PatchCodeWithReplace("$MSTestVersion$", MSTestVersion)
            .PatchCodeWithReplace("$TargetFramework$", TargetFrameworks.NetCurrent));

        DotnetMuxerResult compilationResult = await DotnetCli.RunAsync($"build -c {BuildConfiguration.Release} {testAsset.TargetAssetPath}", cancellationToken: TestContext.CancellationToken);
        compilationResult.AssertExitCodeIs(0);
        compilationResult.AssertOutputDoesNotContain("MSB4011");

        var testHost = TestHost.LocateFrom(testAsset.TargetAssetPath, MixedAssetName, TargetFrameworks.NetCurrent, buildConfiguration: BuildConfiguration.Release);
        TestHostResult testHostResult = await testHost.ExecuteAsync(cancellationToken: TestContext.CancellationToken);
        testHostResult.AssertOutputContainsSummary(0, 1, 0);
    }

    [TestMethod]
    [TestCategory("WindowsApplicationModel")]
    [OSCondition(OperatingSystems.Windows, IgnoreMessage = "UWP is Windows-only.")]
    public async Task MSTestSdk_ModernUwp_DefaultsToMtpWithAppModelController()
    {
        DotnetMuxerResult result = await EvaluateWindowsApplicationModelAsync(
            "ModernUwpSdk",
            """
            <UseUwp>true</UseUwp>
            <_IncludeApplicationDefinition>true</_IncludeApplicationDefinition>
            """);

        result.AssertOutputContains("WindowsTestContract:UseVSTest=false;GenerateEntryPoint=false;GenerateHelper=true;PackagedApp=true");
        result.AssertOutputContains("Controller=mstest-appmodel-controller.exe");
        result.AssertOutputContains("ControllerTfm=net9.0");
        result.AssertOutputContains("ControllerExtensions=msbuild;packagedapp;codecoverage;trx");
        result.AssertOutputContains("MSTest.TestAdapter");
        result.AssertOutputContains("MSTest.TestFramework");
        result.AssertOutputContains("Microsoft.Testing.Extensions.PackagedApp");
        result.AssertOutputContains("TestContainer");
        result.AssertOutputDoesNotContain("Microsoft.NET.Test.Sdk");
    }

    [TestMethod]
    [TestCategory("WindowsApplicationModel")]
    [OSCondition(OperatingSystems.Windows, IgnoreMessage = "UWP is Windows-only.")]
    public async Task MSTestSdk_ModernUwp_AllowsExplicitMtpSelection()
    {
        DotnetMuxerResult result = await EvaluateWindowsApplicationModelAsync(
            "ModernUwpMtpSdk",
            """
            <UseUwp>true</UseUwp>
            <UseUwpTools>true</UseUwpTools>
            <UseVSTest>false</UseVSTest>
            <_IncludeApplicationDefinition>true</_IncludeApplicationDefinition>
            """);

        result.AssertOutputContains("WindowsTestContract:UseVSTest=false;GenerateEntryPoint=false;GenerateHelper=true;PackagedApp=true");
        result.AssertOutputContains("Controller=mstest-appmodel-controller.exe");
        result.AssertOutputContains("Microsoft.Testing.Extensions.PackagedApp");
        result.AssertOutputDoesNotContain("Microsoft.NET.Test.Sdk");
    }

    [TestMethod]
    [TestCategory("WindowsApplicationModel")]
    [OSCondition(OperatingSystems.Windows, IgnoreMessage = "UWP XAML references are supported only on Windows.")]
    public async Task MSTestSdk_UwpXamlReferencesWithoutUwpTools_UsesDirectMtpRunner()
    {
        DotnetMuxerResult result = await EvaluateWindowsApplicationModelAsync(
            "UwpXamlReferencesSdk",
            """
            <UseUwp>true</UseUwp>
            <UseUwpTools>false</UseUwpTools>
            """);

        result.AssertOutputContains("WindowsTestContract:UseVSTest=false;GenerateEntryPoint=true;GenerateHelper=true;PackagedApp=false;OutputType=Exe");
        result.AssertOutputContains(";Controller=;ControllerTfm=;ControllerExtensions=");
        result.AssertOutputContains("Capabilities=TestingPlatformServer");
        result.AssertOutputContains("MSTest.TestAdapter");
        result.AssertOutputDoesNotContain("Microsoft.Testing.Extensions.PackagedApp");
        result.AssertOutputDoesNotContain("Microsoft.NET.Test.Sdk");
    }

    [TestMethod]
    [TestCategory("WindowsApplicationModel")]
    [OSCondition(OperatingSystems.Windows, IgnoreMessage = "UWP XAML references are supported only on Windows.")]
    public async Task MSTestSdk_UwpXamlReferencesWithoutUwpTools_BuildsAndRunsDirectMtpRunner()
    {
        const string DirectMtpAssetName = "UwpXamlReferencesDirectMtp";
        const string Source = """
            #file $AssetName$.csproj
            <Project Sdk="MSTest.Sdk/$MSTestVersion$">
              <PropertyGroup>
                <TargetFramework>$TargetFramework$</TargetFramework>
                <PlatformTarget>x64</PlatformTarget>
                <EnableMicrosoftTestingPlatform>true</EnableMicrosoftTestingPlatform>
                <EnableMicrosoftTestingExtensionsCodeCoverage>false</EnableMicrosoftTestingExtensionsCodeCoverage>
                <UseUwp>true</UseUwp>
                <UseUwpTools>false</UseUwpTools>
                <NoWarn>$(NoWarn);NU1507</NoWarn>
              </PropertyGroup>

              <Target Name="WriteResolvedMSTestAssets" AfterTargets="ResolveReferences">
                <WriteLinesToFile
                  File="$(MSBuildProjectDirectory)\resolved-mstest-assets.txt"
                  Lines="UseUwp=$(UseUwp);UseUwpTools=$(UseUwpTools);PackagedApp=$(EnableMicrosoftTestingExtensionsPackagedApp);TestingPlatformExecutablePath=$(TestingPlatformExecutablePath)"
                  Overwrite="true" />
                <WriteLinesToFile
                  File="$(MSBuildProjectDirectory)\resolved-mstest-assets.txt"
                  Lines="@(ReferencePath->'%(FullPath)')"
                  Overwrite="false" />
              </Target>
            </Project>

            #file UnitTest1.cs
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            namespace UwpXamlReferencesDirectMtp;

            [TestClass]
            public sealed class UnitTest1
            {
                [TestMethod]
                public void UseUwpToolsFalse_RunsThroughDirectMtp()
                    => Assert.AreEqual(4, 2 + 2);
            }
            """;

        using TestAsset testAsset = await TestAsset.GenerateAssetAsync(
            DirectMtpAssetName,
            Source
                .PatchCodeWithReplace("$AssetName$", DirectMtpAssetName)
                .PatchCodeWithReplace("$MSTestVersion$", MSTestVersion)
                .PatchCodeWithReplace("$TargetFramework$", $"{TargetFrameworks.NetCurrent}-windows10.0.26100.0"));
        string binlogPath = Path.Combine(testAsset.TargetAssetPath, $"{DirectMtpAssetName}.binlog");

        DotnetMuxerResult buildResult = await DotnetCli.RunAsync(
            $"build -c {BuildConfiguration.Release} {testAsset.TargetAssetPath} -bl:\"{binlogPath}\"",
            workingDirectory: testAsset.TargetAssetPath,
            cancellationToken: TestContext.CancellationToken);
        buildResult.AssertExitCodeIs(0);

        string resolvedAssetsReport = Path.Combine(testAsset.TargetAssetPath, "resolved-mstest-assets.txt");
        Assert.IsTrue(
            File.Exists(resolvedAssetsReport),
            $"The resolved MSTest asset report '{resolvedAssetsReport}' was not created. Binlog: '{binlogPath}'.");
        string[] resolvedAssets = await File.ReadAllLinesAsync(resolvedAssetsReport, TestContext.CancellationToken);
        Assert.Contains("UseUwp=true", resolvedAssets);
        Assert.Contains("UseUwpTools=false", resolvedAssets);
        Assert.Contains("PackagedApp=false", resolvedAssets);
        Assert.Contains("TestingPlatformExecutablePath=", resolvedAssets);

        string adapterPath = resolvedAssets.Single(
            path => path.EndsWith("MSTest.TestAdapter.dll", StringComparison.OrdinalIgnoreCase));
        string frameworkExtensionsPath = resolvedAssets.Single(
            path => path.EndsWith("MSTest.TestFramework.Extensions.dll", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain($"{Path.DirectorySeparatorChar}uwp{Path.DirectorySeparatorChar}", adapterPath, adapterPath);
        Assert.DoesNotContain($"{Path.DirectorySeparatorChar}uwp{Path.DirectorySeparatorChar}", frameworkExtensionsPath, frameworkExtensionsPath);
        Assert.DoesNotContain(
            path => path.Contains("Microsoft.Testing.Extensions.PackagedApp", StringComparison.OrdinalIgnoreCase),
            resolvedAssets);
        Assert.DoesNotContain(
            path => path.Contains("mstest-appmodel-controller", StringComparison.OrdinalIgnoreCase),
            resolvedAssets);

        var testHost = TestHost.LocateFrom(
            testAsset.TargetAssetPath,
            DirectMtpAssetName,
            $"{TargetFrameworks.NetCurrent}-windows10.0.26100.0",
            buildConfiguration: BuildConfiguration.Release);
        Assert.AreEqual($"{DirectMtpAssetName}.exe", Path.GetFileName(testHost.FullName), ignoreCase: true);

        TestHostResult testHostResult = await testHost.ExecuteAsync(cancellationToken: TestContext.CancellationToken);
        testHostResult.AssertExitCodeIs(0);
        testHostResult.AssertOutputContainsSummary(failed: 0, passed: 1, skipped: 0);
    }

    [TestMethod]
    [TestCategory("WindowsApplicationModel")]
    [OSCondition(OperatingSystems.Windows, IgnoreMessage = "UWP XAML references are supported only on Windows.")]
    public async Task MSTestSdk_UwpXamlReferencesWithoutUwpTools_WithApplicationDefinition_UsesSelfHostedDirectMtp()
    {
        DotnetMuxerResult result = await EvaluateWindowsApplicationModelAsync(
            "UwpXamlReferencesSelfHostedSdk",
            """
            <UseUwp>true</UseUwp>
            <UseUwpTools>false</UseUwpTools>
            <_IncludeApplicationDefinition>true</_IncludeApplicationDefinition>
            """);

        result.AssertOutputContains("WindowsTestContract:UseVSTest=false;GenerateEntryPoint=false;GenerateHelper=true;PackagedApp=false;OutputType=Exe");
        result.AssertOutputContains(";Controller=;ControllerTfm=;ControllerExtensions=");
        result.AssertOutputDoesNotContain("Microsoft.Testing.Extensions.PackagedApp");
    }

    [TestMethod]
    [TestCategory("WindowsApplicationModel")]
    [OSCondition(OperatingSystems.Windows, IgnoreMessage = "UWP XAML references are supported only on Windows.")]
    public async Task MSTestSdk_UnpackagedWinUIWithUwpXamlReferences_DoesNotConflictWithUwpTools()
    {
        DotnetMuxerResult result = await EvaluateWindowsApplicationModelAsync(
            "UnpackagedWinUIUwpXamlSdk",
            """
            <UseWinUI>true</UseWinUI>
            <WindowsPackageType>None</WindowsPackageType>
            <UseUwp>true</UseUwp>
            <UseUwpTools>false</UseUwpTools>
            <_IncludeApplicationDefinition>true</_IncludeApplicationDefinition>
            """);

        result.AssertOutputContains("WindowsTestContract:UseVSTest=false;GenerateEntryPoint=false;GenerateHelper=true;PackagedApp=false;OutputType=Exe");
        result.AssertOutputContains(";Controller=;ControllerTfm=;ControllerExtensions=");
        result.AssertOutputDoesNotContain("Microsoft.Testing.Extensions.PackagedApp");
    }

    [TestMethod]
    public async Task MSTestSdk_UnpackagedWinUI_DefaultsToMtpWithoutPackagedAppLauncher()
    {
        DotnetMuxerResult result = await EvaluateWindowsApplicationModelAsync(
            "UnpackagedWinUISdk",
            """
            <UseWinUI>true</UseWinUI>
            <WindowsPackageType>None</WindowsPackageType>
            <_IncludeApplicationDefinition>true</_IncludeApplicationDefinition>
            """);

        result.AssertOutputContains("WindowsTestContract:UseVSTest=false;GenerateEntryPoint=false;GenerateHelper=true;PackagedApp=false");
        result.AssertOutputContains("OutputType=Exe");
        result.AssertOutputContains("Capabilities=TestingPlatformServer");
        result.AssertOutputContains("TestContainer");
        result.AssertOutputDoesNotContain("Microsoft.Testing.Extensions.PackagedApp");
    }

    [TestMethod]
    public async Task MSTestSdk_UnpackagedWinUIWithExplicitPackagedAppExtension_KeepsDirectLaunch()
    {
        DotnetMuxerResult result = await EvaluateWindowsApplicationModelAsync(
            "UnpackagedWinUIWithPackagedAppSdk",
            """
            <UseWinUI>true</UseWinUI>
            <WindowsPackageType>None</WindowsPackageType>
            <EnableMicrosoftTestingExtensionsPackagedApp>true</EnableMicrosoftTestingExtensionsPackagedApp>
            <_IncludeApplicationDefinition>true</_IncludeApplicationDefinition>
            """);

        result.AssertOutputContains("WindowsTestContract:UseVSTest=false;GenerateEntryPoint=false;GenerateHelper=true;PackagedApp=true");
        result.AssertOutputContains(";Controller=;ControllerTfm=;ControllerExtensions=");
        result.AssertOutputContains("Microsoft.Testing.Extensions.PackagedApp");
    }

    [TestMethod]
    public async Task MSTestSdk_ConsoleWithExplicitPackagedAppExtension_KeepsDirectLaunch()
    {
        DotnetMuxerResult result = await EvaluateWindowsApplicationModelAsync(
            "ConsoleWithPackagedAppSdk",
            """
            <EnableMicrosoftTestingExtensionsPackagedApp>true</EnableMicrosoftTestingExtensionsPackagedApp>
            """);

        result.AssertOutputContains("WindowsTestContract:UseVSTest=false;GenerateEntryPoint=true;GenerateHelper=true;PackagedApp=true");
        result.AssertOutputContains(";Controller=;ControllerTfm=;ControllerExtensions=");
        result.AssertOutputContains("Microsoft.Testing.Extensions.PackagedApp");
    }

    [TestMethod]
    public async Task MSTestSdk_UnpackagedWinUI_RejectsVSTest()
    {
        DotnetMuxerResult result = await EvaluateWindowsApplicationModelAsync(
            "UnpackagedWinUIVSTestSdk",
            """
            <UseWinUI>true</UseWinUI>
            <WindowsPackageType>None</WindowsPackageType>
            <UseVSTest>true</UseVSTest>
            """,
            failIfReturnValueIsNotZero: false,
            target: "Build");

        Assert.AreNotEqual(0, result.ExitCode);
        result.AssertOutputContains("VSTest does not support unpackaged WinUI test applications.");
    }

    [TestMethod]
    public async Task MSTestSdk_PackagedWinUI_DefaultsToMtpWithPackagedAppLauncher()
    {
        DotnetMuxerResult result = await EvaluateWindowsApplicationModelAsync(
            "PackagedWinUISdk",
            """
            <UseWinUI>true</UseWinUI>
            <_IncludeApplicationDefinition>true</_IncludeApplicationDefinition>
            """);

        result.AssertOutputContains("WindowsTestContract:UseVSTest=false;GenerateEntryPoint=false;GenerateHelper=true;PackagedApp=true");
        result.AssertOutputContains("Controller=mstest-appmodel-controller.exe");
        result.AssertOutputContains("OutputType=Exe");
        result.AssertOutputContains("Microsoft.Testing.Extensions.PackagedApp");
    }

    [TestMethod]
    public async Task MSTestSdk_WinUISeparateHost_KeepsGeneratedEntryPoint()
    {
        DotnetMuxerResult result = await EvaluateWindowsApplicationModelAsync(
            "SeparateHostWinUISdk",
            """
            <UseWinUI>true</UseWinUI>
            <WindowsPackageType>None</WindowsPackageType>
            """);

        result.AssertOutputContains("WindowsTestContract:UseVSTest=false;GenerateEntryPoint=true;GenerateHelper=true;PackagedApp=false");
        result.AssertOutputContains("OutputType=Exe");
    }

    private static void AssertSatelliteCultures(string outputDirectory, string resourceAssemblyName, string[] expectedCultures)
    {
        string[] actualCultures = Directory.GetFiles(outputDirectory, resourceAssemblyName, SearchOption.AllDirectories)
            .Select(path => Path.GetRelativePath(outputDirectory, Path.GetDirectoryName(path)!))
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        string[] expected = expectedCultures.Order(StringComparer.OrdinalIgnoreCase).ToArray();

        Assert.AreSequenceEqual(
            expected,
            actualCultures,
            $"Unexpected cultures for {resourceAssemblyName}. Expected: '{string.Join(";", expected)}'. Actual: '{string.Join(";", actualCultures)}'.");
    }

    private async Task<DotnetMuxerResult> EvaluateWindowsApplicationModelAsync(
        string assetName,
        string applicationModelProperties,
        bool failIfReturnValueIsNotZero = true,
        string target = "PrintWindowsTestContract")
    {
        const string Source = """
            #file $AssetName$.csproj
            <Project Sdk="MSTest.Sdk/$MSTestVersion$">
              <PropertyGroup>
                <TargetFramework>$TargetFramework$</TargetFramework>
                <OutputType>Exe</OutputType>
                <EnableMicrosoftTestingPlatform>true</EnableMicrosoftTestingPlatform>
                <NoWarn>$(NoWarn);NETSDK1201;NU1507</NoWarn>
                $ApplicationModelProperties$
              </PropertyGroup>

              <ItemGroup Condition=" '$(_IncludeApplicationDefinition)' == 'true' ">
                <ApplicationDefinition Include="App.xaml" />
              </ItemGroup>

              <Target Name="PrintWindowsTestContract"
                      DependsOnTargets="_MSTestSDKValidateWindowsApplicationModel;_CalculateGenerateTestingPlatformEntryPoint">
                <Message Importance="high"
                         Text="WindowsTestContract:UseVSTest=$(UseVSTest);GenerateEntryPoint=$(GenerateTestingPlatformEntryPoint);GenerateHelper=$(GenerateTestingPlatformApplicationHelper);PackagedApp=$(EnableMicrosoftTestingExtensionsPackagedApp);OutputType=$(OutputType);IsTestProject=$(IsTestProject);Controller=$([System.IO.Path]::GetFileName($(TestingPlatformExecutablePath)));ControllerTfm=$(_TestingPlatformPackagedAppControllerTfm);ControllerExtensions=$(_TestingPlatformPackagedAppControllerExtensions)" />
                <Message Importance="high"
                         Text="PackageReferences=@(PackageReference->'%(Identity)')" />
                <Message Importance="high"
                         Text="Capabilities=@(ProjectCapability->'%(Identity)')" />
              </Target>
            </Project>
            """;

        using TestAsset testAsset = await TestAsset.GenerateAssetAsync(
            assetName,
            Source
                .PatchCodeWithReplace("$AssetName$", assetName)
                .PatchCodeWithReplace("$MSTestVersion$", MSTestVersion)
                .PatchCodeWithReplace("$TargetFramework$", TargetFrameworks.NetCurrent)
                .PatchCodeWithReplace("$ApplicationModelProperties$", applicationModelProperties));
        string binlogPath = Path.Combine(testAsset.TargetAssetPath, $"{assetName}.binlog");

        return await DotnetCli.RunAsync(
            $"build {testAsset.TargetAssetPath} -restore -t:{target} -bl:\"{binlogPath}\"",
            failIfReturnValueIsNotZero: failIfReturnValueIsNotZero,
            cancellationToken: TestContext.CancellationToken);
    }
}
