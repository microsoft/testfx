// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Globalization;
using System.Runtime.InteropServices;

using Microsoft.Testing.Platform.Acceptance.IntegrationTests;
using Microsoft.Testing.Platform.Acceptance.IntegrationTests.Helpers;
using Microsoft.Testing.Platform.Helpers;

namespace MSTest.Acceptance.IntegrationTests;

[TestGroup]
public sealed class RunSettingsTests : AcceptanceTestBase
{
    private readonly TestAssetFixture _testAssetFixture;

    public RunSettingsTests(ITestExecutionContext testExecutionContext, TestAssetFixture testAssetFixture)
        : base(testExecutionContext) => _testAssetFixture = testAssetFixture;

    internal static IEnumerable<TestArgumentsEntry<string>> TfmList()
    {
        yield return TargetFrameworks.NetCurrent;
        yield return TargetFrameworks.NetFramework.First();
    }

    internal static IEnumerable<TestArgumentsEntry<(string? TestingPlatformUILanguage, string? DotnetCLILanguage, string? VSLang, string ExpectedLocale)>> LocalizationTestCases()
    {
        // Show that TestingPlatformUILanguage is respected.
        yield return new TestArgumentsEntry<(string?, string?, string?, string)>(("fr-FR", null, null, "fr-FR"), "TestingPlatformUILanguage: fr-FR, expected: fr-FR");

        // Show that TestingPlatformUILanguage takes precedence over DotnetCLILanguage.
        yield return new TestArgumentsEntry<(string?, string?, string?, string)>(("fr-FR", "it-IT", null, "fr-FR"), "TestingPlatformUILanguage: fr-FR, CLI: it-IT, expected: fr-FR");

        // Show that DotnetCLILanguage is respected.
        yield return new TestArgumentsEntry<(string?, string?, string?, string)>((null, "it-IT", null, "it-IT"), "CLI: it-IT, expected: it-IT");

        // Show that DotnetCLILanguage takes precedence over VSLang.
        yield return new TestArgumentsEntry<(string?, string?, string?, string)>((null, "it-IT", "fr-FR", "it-IT"), "CLI: it-IT, VSLang: fr-FR, expected: it-IT");

        // Show that VSLang is respected.
        yield return new TestArgumentsEntry<(string?, string?, string?, string)>((null, null, "it-IT", "it-IT"), "VSLang: it-IT, expected: it-IT");

        // Show that TestingPlatformUILanguage takes precedence over everything.
        yield return new TestArgumentsEntry<(string?, string?, string?, string)>(("fr-FR", "it-IT", "it-IT", "fr-FR"), "TestingPlatformUILanguage: fr-FR, CLI: it-IT, VSLang: it-IT, expected: fr-FR");
    }

    [ArgumentsProvider(nameof(TfmList))]
    public async Task UnsupportedRunSettingsEntriesAreFlagged(string tfm)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows) && tfm == TargetFrameworks.NetFramework.First().Arguments)
        {
            return;
        }

        var testHost = TestHost.LocateFrom(_testAssetFixture.ProjectPath, TestAssetFixture.ProjectName, tfm);
        TestHostResult testHostResult = await testHost.ExecuteAsync("--settings my.runsettings");

        // Assert
        testHostResult.AssertExitCodeIs(0);
        testHostResult.AssertOutputContainsSummary(failed: 0, passed: 1, skipped: 0);

        testHostResult.AssertOutputContains("Runsettings loggers are not supported by Microsoft.Testing.Platform and will be ignored");
        testHostResult.AssertOutputContains("Runsettings datacollectors are not supported by Microsoft.Testing.Platform and will be ignored");
        testHostResult.AssertOutputContains("Runsettings attribute 'MaxCpuCount' is not supported by Microsoft.Testing.Platform and will be ignored");
        testHostResult.AssertOutputContains("Runsettings attribute 'TargetFrameworkVersion' is not supported by Microsoft.Testing.Platform and will be ignored");
        testHostResult.AssertOutputContains("Runsettings attribute 'TargetPlatform' is not supported by Microsoft.Testing.Platform and will be ignored");
        testHostResult.AssertOutputContains("Runsettings attribute 'TestAdaptersPaths' is not supported by Microsoft.Testing.Platform and will be ignored");
        testHostResult.AssertOutputContains("Runsettings attribute 'TestCaseFilter' is not supported by Microsoft.Testing.Platform and will be ignored");
        testHostResult.AssertOutputContains("Runsettings attribute 'TestSessionTimeout' is not supported by Microsoft.Testing.Platform and will be ignored");
        testHostResult.AssertOutputContains("Runsettings attribute 'TreatNoTestsAsError' is not supported by Microsoft.Testing.Platform and will be ignored");
    }

    [ArgumentsProvider(nameof(LocalizationTestCases))]
    public async Task UnsupportedRunSettingsEntriesAreFlagged_Localization((string? TestingPlatformUILanguage, string? DotnetCLILanguage, string? VSLang, string? ExpectedLocale) testArgument)
    {
        var testHost = TestHost.LocateFrom(_testAssetFixture.ProjectPath, TestAssetFixture.ProjectName, TargetFrameworks.NetCurrent.Arguments);
        TestHostResult testHostResult = await testHost.ExecuteAsync("--settings my.runsettings", environmentVariables: new()
        {
            ["TESTINGPLATFORM_UI_LANGUAGE"] = testArgument.TestingPlatformUILanguage,
            ["DOTNET_CLI_UI_LANGUAGE"] = testArgument.DotnetCLILanguage,
            ["VSLANG"] = testArgument.VSLang is null ? null : new CultureInfo(testArgument.VSLang).LCID.ToString(CultureInfo.CurrentCulture),
        });

        // Assert
        testHostResult.AssertExitCodeIs(0);

        switch (testArgument.ExpectedLocale)
        {
            case "fr-FR":
                testHostResult.AssertOutputContains("Les loggers Runsettings ne sont pas pris en charge par Microsoft.Testing.Platform et seront ignorés");
                testHostResult.AssertOutputContains("Les datacollecteurs Runsettings ne sont pas pris en charge par Microsoft.Testing.Platform et seront ignorés");
                testHostResult.AssertOutputContains("Les attributs Runsettings « MaxCpuCount » ne sont pas pris en charge par Microsoft.Testing.Platform et seront ignorés");
                testHostResult.AssertOutputContains("Les attributs Runsettings « TargetFrameworkVersion » ne sont pas pris en charge par Microsoft.Testing.Platform et seront ignorés");
                testHostResult.AssertOutputContains("Les attributs Runsettings « TargetPlatform » ne sont pas pris en charge par Microsoft.Testing.Platform et seront ignorés");
                testHostResult.AssertOutputContains("Les attributs Runsettings « TestAdaptersPaths » ne sont pas pris en charge par Microsoft.Testing.Platform et seront ignorés");
                testHostResult.AssertOutputContains("Les attributs Runsettings « TestCaseFilter » ne sont pas pris en charge par Microsoft.Testing.Platform et seront ignorés");
                testHostResult.AssertOutputContains("Les attributs Runsettings « TestSessionTimeout » ne sont pas pris en charge par Microsoft.Testing.Platform et seront ignorés");
                testHostResult.AssertOutputContains("Les attributs Runsettings « TreatNoTestsAsError » ne sont pas pris en charge par Microsoft.Testing.Platform et seront ignorés");
                break;
            case "it-IT":
                testHostResult.AssertOutputContains("I logger Runsettings non sono supportati da Microsoft.Testing.Platform e verranno ignorati");
                testHostResult.AssertOutputContains("I datacollector Runsettings non sono supportati da Microsoft.Testing.Platform e verranno ignorati");

                // Unsure why this happens :/
                string notSupportedItalianMessageFormat = OperatingSystem.IsWindows()
                    ? "L'attributo Runsettings `{0}' non è supportato da Microsoft.Testing.Platform e verrà ignorato"
                    : "L’attributo Runsettings ‘0’ non è supportato da Microsoft.Testing.Platform e verrà ignorato";

                testHostResult.AssertOutputContains(string.Format(CultureInfo.InvariantCulture, notSupportedItalianMessageFormat, "MaxCpuCount"));
                testHostResult.AssertOutputContains(string.Format(CultureInfo.InvariantCulture, notSupportedItalianMessageFormat, "TargetFrameworkVersion"));
                testHostResult.AssertOutputContains(string.Format(CultureInfo.InvariantCulture, notSupportedItalianMessageFormat, "TargetPlatform"));
                testHostResult.AssertOutputContains(string.Format(CultureInfo.InvariantCulture, notSupportedItalianMessageFormat, "TestAdaptersPaths"));
                testHostResult.AssertOutputContains(string.Format(CultureInfo.InvariantCulture, notSupportedItalianMessageFormat, "TestCaseFilter"));
                testHostResult.AssertOutputContains(string.Format(CultureInfo.InvariantCulture, notSupportedItalianMessageFormat, "TestSessionTimeout"));
                testHostResult.AssertOutputContains(string.Format(CultureInfo.InvariantCulture, notSupportedItalianMessageFormat, "TreatNoTestsAsError"));
                break;
            default:
                throw ApplicationStateGuard.Unreachable();
        }
    }

    [TestFixture(TestFixtureSharingStrategy.PerTestGroup)]
    public sealed class TestAssetFixture(AcceptanceFixture acceptanceFixture) : TestAssetFixtureBase(acceptanceFixture.NuGetGlobalPackagesFolder)
    {
        public const string ProjectName = "TestRunSettings";

        public string ProjectPath => GetAssetPath(ProjectName);

        public override IEnumerable<(string ID, string Name, string Code)> GetAssetsToGenerate()
        {
            yield return (ProjectName, ProjectName,
                SourceCode
                .PatchTargetFrameworks(TargetFrameworks.NetCurrent, TargetFrameworks.NetFramework.First())
                .PatchCodeWithReplace("$MSTestVersion$", MSTestVersion));
        }

        private const string SourceCode = """
#file TestRunSettings.csproj
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <EnableMSTestRunner>true</EnableMSTestRunner>
    <TargetFrameworks>$TargetFrameworks$</TargetFrameworks>
    <LangVersion>Preview</LangVersion>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="MSTest.TestAdapter" Version="$MSTestVersion$" />
    <PackageReference Include="MSTest.TestFramework" Version="$MSTestVersion$" />
  </ItemGroup>

  <ItemGroup>
    <None Update="*.runsettings">
      <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
    </None>
  </ItemGroup>

</Project>

#file my.runsettings
<?xml version="1.0" encoding="utf-8"?>
<!-- Example runsettings from documentation https://learn.microsoft.com/visualstudio/test/configure-unit-tests-by-using-a-dot-runsettings-file#example-runsettings-file -->
<RunSettings>
  <!-- Configurations that affect the Test Framework -->
  <RunConfiguration>
    <!-- Use 0 for maximum process-level parallelization. This does not force parallelization within the test DLL (on the thread-level). You can also change it from the Test menu; choose "Run tests in parallel". Unchecked = 1 (only 1), checked = 0 (max). -->
    <MaxCpuCount>1</MaxCpuCount>
    <!-- Path relative to directory that contains .runsettings file-->
    <ResultsDirectory>.\TestResults</ResultsDirectory>

    <!-- Omit the whole tag for auto-detection. -->
    <!-- [x86] or x64, ARM, ARM64, s390x  -->
    <!-- You can also change it from the Test menu; choose "Processor Architecture for AnyCPU Projects" -->
    <TargetPlatform>x86</TargetPlatform>

    <!-- Any TargetFramework moniker or omit the whole tag for auto-detection. -->
    <!-- net48, [net40], net6.0, net5.0, netcoreapp3.1, uap10.0 etc. -->
    <TargetFrameworkVersion>net40</TargetFrameworkVersion>

    <!-- Path to Test Adapters -->
    <TestAdaptersPaths>%SystemDrive%\Temp\foo;%SystemDrive%\Temp\bar</TestAdaptersPaths>

    <!-- TestCaseFilter expression -->
    <TestCaseFilter>(TestCategory != Integration) &amp; (TestCategory != UnfinishedFeature)</TestCaseFilter>

    <!-- TestSessionTimeout was introduced in Visual Studio 2017 version 15.5 -->
    <!-- Specify timeout in milliseconds. A valid value should be greater than 0 -->
    <TestSessionTimeout>10000</TestSessionTimeout>

    <!-- true or false -->
    <!-- Value that specifies the exit code when no tests are discovered -->
    <TreatNoTestsAsError>true</TreatNoTestsAsError>

    <!-- List of environment variables we want to set-->
    <EnvironmentVariables>
        <SAMPLEKEY>SAMPLEVALUE</SAMPLEKEY>
    </EnvironmentVariables>
  </RunConfiguration>

  <!-- Configurations for data collectors -->
  <DataCollectionRunSettings>
    <DataCollectors>
      <DataCollector friendlyName="Code Coverage" uri="datacollector://Microsoft/CodeCoverage/2.0" assemblyQualifiedName="Microsoft.VisualStudio.Coverage.DynamicCoverageDataCollector, Microsoft.VisualStudio.TraceCollector, Version=11.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a">
        <Configuration>
          <CodeCoverage>
            <ModulePaths>
              <Exclude>
                <ModulePath>.*CPPUnitTestFramework.*</ModulePath>
              </Exclude>
            </ModulePaths>

            <!-- We recommend you do not change the following values: -->
            <UseVerifiableInstrumentation>True</UseVerifiableInstrumentation>
            <AllowLowIntegrityProcesses>True</AllowLowIntegrityProcesses>
            <CollectFromChildProcesses>True</CollectFromChildProcesses>
            <CollectAspDotNet>False</CollectAspDotNet>

          </CodeCoverage>
        </Configuration>
      </DataCollector>

      <DataCollector uri="datacollector://microsoft/VideoRecorder/1.0" assemblyQualifiedName="Microsoft.VisualStudio.TestTools.DataCollection.VideoRecorder.VideoRecorderDataCollector, Microsoft.VisualStudio.TestTools.DataCollection.VideoRecorder, Version=15.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a" friendlyName="Screen and Voice Recorder">
        <!--Video data collector was introduced in Visual Studio 2017 version 15.5 -->
        <Configuration>
          <!-- Set "sendRecordedMediaForPassedTestCase" to "false" to add video attachments to failed tests only -->
          <MediaRecorder sendRecordedMediaForPassedTestCase="true"  xmlns="">
            <ScreenCaptureVideo bitRate="512" frameRate="2" quality="20" />
          </MediaRecorder>
        </Configuration>
      </DataCollector>

      <!-- Configuration for blame data collector -->
      <DataCollector friendlyName="blame" enabled="True">
      </DataCollector>

    </DataCollectors>
  </DataCollectionRunSettings>

  <!-- Parameters used by tests at run time -->
  <TestRunParameters>
    <Parameter name="webAppUrl" value="http://localhost" />
    <Parameter name="webAppUserName" value="Admin" />
    <Parameter name="webAppPassword" value="Password" />
  </TestRunParameters>

  <!-- Configuration for loggers -->
  <LoggerRunSettings>
    <Loggers>
      <Logger friendlyName="console" enabled="True">
        <Configuration>
            <Verbosity>quiet</Verbosity>
        </Configuration>
      </Logger>
      <Logger friendlyName="trx" enabled="True">
        <Configuration>
          <LogFileName>foo.trx</LogFileName>
        </Configuration>
      </Logger>
      <Logger friendlyName="html" enabled="True">
        <Configuration>
          <LogFileName>foo.html</LogFileName>
        </Configuration>
      </Logger>
      <Logger friendlyName="blame" enabled="True" />
    </Loggers>
  </LoggerRunSettings>

  <!-- Adapter Specific sections -->

  <!-- MSTest adapter -->
  <MSTest>
    <MapInconclusiveToFailed>True</MapInconclusiveToFailed>
    <CaptureTraceOutput>false</CaptureTraceOutput>
    <DeleteDeploymentDirectoryAfterTestRunIsComplete>False</DeleteDeploymentDirectoryAfterTestRunIsComplete>
    <DeploymentEnabled>False</DeploymentEnabled>
    <AssemblyResolution>
      <Directory path="D:\myfolder\bin\" includeSubDirectories="false"/>
    </AssemblyResolution>
  </MSTest>

</RunSettings>

#file UnitTest1.cs
using Microsoft.VisualStudio.TestTools.UnitTesting;

[TestClass]
public class UnitTest1
{

    [TestMethod]
    public void TestMethod()
    {
        Assert.AreEqual("SAMPLEVALUE", System.Environment.GetEnvironmentVariable("SAMPLEKEY")!);
    }
}
""";
    }
}
