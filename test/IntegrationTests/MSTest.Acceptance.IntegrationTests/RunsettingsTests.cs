// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Acceptance.IntegrationTests;
using Microsoft.Testing.Platform.Acceptance.IntegrationTests.Helpers;
using Microsoft.Testing.Platform.Helpers;

namespace MSTest.Acceptance.IntegrationTests;

[TestClass]
public sealed class RunSettingsTests : AcceptanceTestBase<RunSettingsTests.TestAssetFixture>
{
    internal static IEnumerable<(string? TestingPlatformUILanguage, string? DotnetCLILanguage, string? VSLang, string ExpectedLocale)> LocalizationTestCases()
    {
        // Show that TestingPlatformUILanguage is respected.
        yield return new("fr-FR", null, null, "fr-FR");

        // Show that TestingPlatformUILanguage takes precedence over DotnetCLILanguage.
        yield return new("fr-FR", "it-IT", null, "fr-FR");

        // Show that DotnetCLILanguage is respected.
        yield return new(null, "it-IT", null, "it-IT");

        // Show that DotnetCLILanguage takes precedence over VSLang.
        yield return new(null, "it-IT", "fr-FR", "it-IT");

        // Show that VSLang is respected.
        yield return new(null, null, "it-IT", "it-IT");

        // Show that TestingPlatformUILanguage takes precedence over everything.
        yield return new("fr-FR", "it-IT", "it-IT", "fr-FR");
    }

    [TestMethod]
    [DynamicData(nameof(TargetFrameworks.AllForDynamicData), typeof(TargetFrameworks))]
    public async Task UnsupportedRunSettingsEntriesAreFlagged(string tfm)
    {
        var testHost = TestHost.LocateFrom(AssetFixture.ProjectPath, TestAssetFixture.ProjectName, tfm);
        TestHostResult testHostResult = await testHost.ExecuteAsync("--settings my.runsettings", cancellationToken: TestContext.CancellationToken);

        // Assert
        testHostResult.AssertExitCodeIs(0);
        testHostResult.AssertOutputContainsSummary(failed: 0, passed: 1, skipped: 0);

        testHostResult.AssertOutputContains("Runsettings loggers are not supported by Microsoft.Testing.Platform and will be ignored");
        testHostResult.AssertOutputContains("Runsettings datacollectors are not supported by Microsoft.Testing.Platform and will be ignored");
        testHostResult.AssertOutputContains("Runsettings attribute 'MaxCpuCount' is not supported by Microsoft.Testing.Platform and will be ignored");
        testHostResult.AssertOutputContains("Runsettings attribute 'TargetFrameworkVersion' is not supported by Microsoft.Testing.Platform and will be ignored");
        testHostResult.AssertOutputContains("Runsettings attribute 'TargetPlatform' is not supported by Microsoft.Testing.Platform and will be ignored");
        testHostResult.AssertOutputContains("Runsettings attribute 'TestAdaptersPaths' is not supported by Microsoft.Testing.Platform and will be ignored");
        testHostResult.AssertOutputContains("Runsettings attribute 'TestSessionTimeout' is not supported by Microsoft.Testing.Platform and will be ignored");
        testHostResult.AssertOutputContains("Runsettings attribute 'TreatNoTestsAsError' is not supported by Microsoft.Testing.Platform and will be ignored");
        testHostResult.AssertOutputDoesNotContain("Runsettings attribute 'TestCaseFilter' is not supported by Microsoft.Testing.Platform and will be ignored");
    }

    [TestMethod]
    [DynamicData(nameof(LocalizationTestCases))]
    public async Task UnsupportedRunSettingsEntriesAreFlagged_Localization(string? testingPlatformUILanguage, string? dotnetCLILanguage, string? vsLang, string? expectedLocale)
    {
        var testHost = TestHost.LocateFrom(AssetFixture.ProjectPath, TestAssetFixture.ProjectName, TargetFrameworks.NetCurrent);
        TestHostResult testHostResult = await testHost.ExecuteAsync(
            "--settings my.runsettings",
            environmentVariables: new()
            {
                ["TESTINGPLATFORM_UI_LANGUAGE"] = testingPlatformUILanguage,
                ["DOTNET_CLI_UI_LANGUAGE"] = dotnetCLILanguage,
                ["VSLANG"] = vsLang is null ? null : new CultureInfo(vsLang).LCID.ToString(CultureInfo.CurrentCulture),
            },
            cancellationToken: TestContext.CancellationToken);

        // Assert
        testHostResult.AssertExitCodeIs(0);

        switch (expectedLocale)
        {
            case "fr-FR":
                // Using regex for the "é" of ignorés as something with encoding doesn't work properly.
                // The é shows correctly when invoking with Arcade, but not with dotnet test.
                // This is probably because Arcade infra uses Exec MSBuild task, which seems to be having extra logic around handling encoding.
                // See https://github.com/dotnet/msbuild/blob/bcc2dc6a6509ffb63f1253a9bbbaaa233bd53a50/src/Tasks/Exec.cs
                testHostResult.AssertOutputMatchesRegex(@"Les loggers Runsettings ne sont pas pris en charge par Microsoft\.Testing\.Platform et seront ignor.*?s");
                testHostResult.AssertOutputMatchesRegex(@"Les datacollecteurs Runsettings ne sont pas pris en charge par Microsoft\.Testing\.Platform et seront ignor.*?s");
                break;
            case "it-IT":
                testHostResult.AssertOutputContains("I logger Runsettings non sono supportati da Microsoft.Testing.Platform e verranno ignorati");
                testHostResult.AssertOutputContains("I datacollector Runsettings non sono supportati da Microsoft.Testing.Platform e verranno ignorati");
                break;
            default:
                throw ApplicationStateGuard.Unreachable();
        }
    }

    public sealed class TestAssetFixture() : TestAssetFixtureBase(AcceptanceFixture.NuGetGlobalPackagesFolder)
    {
        public const string ProjectName = "TestRunSettings";

        public string ProjectPath => GetAssetPath(ProjectName);

        public override IEnumerable<(string ID, string Name, string Code)> GetAssetsToGenerate()
        {
            yield return (ProjectName, ProjectName,
                SourceCode
                .PatchTargetFrameworks(TargetFrameworks.All)
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

    public TestContext TestContext { get; set; }
}
