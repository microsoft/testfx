// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Acceptance.IntegrationTests;
using Microsoft.Testing.Platform.Acceptance.IntegrationTests.Helpers;
using Microsoft.Testing.Platform.Helpers;

namespace MSTest.Acceptance.IntegrationTests;

[TestGroup]
public class HelpInfoTests : AcceptanceTestBase
{
    private const string AssetName = "HelpInfo";
    private readonly TestAssetFixture _testAssetFixture;

    // There's a bug in TAFX where we need to use it at least one time somewhere to use it inside the fixture self (AcceptanceFixture).
    public HelpInfoTests(ITestExecutionContext testExecutionContext, TestAssetFixture testAssetFixture,
        AcceptanceFixture globalFixture)
        : base(testExecutionContext) => _testAssetFixture = testAssetFixture;

    [ArgumentsProvider(nameof(TargetFrameworks.All), typeof(TargetFrameworks))]
    public async Task Help_WhenMSTestExtensionRegistered_OutputHelpContentOfRegisteredExtension(string tfm)
    {
        var testHost = TestHost.LocateFrom(_testAssetFixture.TargetAssetPath, AssetName, tfm);
        TestHostResult testHostResult = await testHost.ExecuteAsync("--help");

        testHostResult.AssertExitCodeIs(ExitCodes.Success);

        string wildcardMatchPattern = $"""
MSTest v{MSTestVersion} (UTC *) [* - *]
Usage {AssetName}* [option providers] [extension option providers]
Execute a .NET Test Application.
Options:
    --config-file
        Specifies a testconfig.json file.
    --diagnostic
        Enable the diagnostic logging. The default log level is 'Trace'.
        The file will be written in the output directory with the name log_[yyMMddHHmmssfff].diag
    --diagnostic-filelogger-synchronouswrite
        Force the built-in file logger to write the log synchronously.
        Useful for scenario where you don't want to lose any log (i.e. in case of crash).
        Note that this is slowing down the test execution.
    --diagnostic-output-directory
        Output directory of the diagnostic logging.
        If not specified the file will be generated inside the default 'TestResults' directory.
    --diagnostic-output-fileprefix
        Prefix for the log file name that will replace '[log]_.'
    --diagnostic-verbosity
        Define the level of the verbosity for the --diagnostic.
        The available values are 'Trace', 'Debug', 'Information', 'Warning', 'Error', and 'Critical'.
    --exit-on-process-exit
        Exit the test process if dependent process exits. PID must be provided.
    --help
        Show the command line help.
    --ignore-exit-code
        Do not report non successful exit value for specific exit codes
        (e.g. '--ignore-exit-code 8;9' ignore exit code 8 and 9 and will return 0 in these case)
    --info
        Display .NET test application information.
    --list-tests
        List available tests.
    --minimum-expected-tests
        Specifies the minimum number of tests that are expected to run.
    --results-directory
        The directory where the test results are going to be placed.
        If the specified directory doesn't exist, it's created.
        The default is TestResults in the directory that contains the test application.
    --timeout
        A global test execution timeout.
        Takes one argument as string in the format <value>[h|m|s] where 'value' is float.
Extension options:
    --filter
        Filters tests using the given expression. For more information, see the Filter option details section. For more information and examples on how to use selective unit test filtering, see https://learn.microsoft.com/dotnet/core/testing/selective-unit-tests.
    --maximum-failed-tests
        Specifies a maximum number of test failures that, when exceeded, will abort the test run.
    --no-ansi
        Disable outputting ANSI escape characters to screen.
    --no-progress
        Disable reporting progress to screen.
    --output
        Output verbosity when reporting tests.
        Valid values are 'Normal', 'Detailed'. Default is 'Normal'.
    --settings
        The path, relative or absolute, to the .runsettings file. For more information and examples on how to configure test run, see https://learn.microsoft.com/visualstudio/test/configure-unit-tests-by-using-a-dot-runsettings-file#the-runsettings-file
    --test-parameter
        Specify or override a key-value pair parameter. For more information and examples, see https://learn.microsoft.com/visualstudio/test/configure-unit-tests-by-using-a-dot-runsettings-file#testrunparameters
""";

        testHostResult.AssertOutputMatchesLines(wildcardMatchPattern);
    }

    [ArgumentsProvider(nameof(TargetFrameworks.All), typeof(TargetFrameworks))]
    public async Task Info_WhenMSTestExtensionRegistered_OutputInfoContentOfRegisteredExtension(string tfm)
    {
        var testHost = TestHost.LocateFrom(_testAssetFixture.TargetAssetPath, AssetName, tfm);
        TestHostResult testHostResult = await testHost.ExecuteAsync("--info");

        testHostResult.AssertExitCodeIs(ExitCodes.Success);

        string output = $"""
  MSTestExtension
    Name: MSTest
    Version: {MSTestVersion}
    Description: MSTest Framework for Microsoft Testing Platform
    Options:
      --settings
        Arity: 1
        Hidden: False
        Description: The path, relative or absolute, to the .runsettings file. For more information and examples on how to configure test run, see https://learn.microsoft.com/visualstudio/test/configure-unit-tests-by-using-a-dot-runsettings-file#the-runsettings-file
      --filter
        Arity: 1
        Hidden: False
        Description: Filters tests using the given expression. For more information, see the Filter option details section. For more information and examples on how to use selective unit test filtering, see https://learn.microsoft.com/dotnet/core/testing/selective-unit-tests.
      --test-parameter
        Arity: 1..N
        Hidden: False
        Description: Specify or override a key-value pair parameter. For more information and examples, see https://learn.microsoft.com/visualstudio/test/configure-unit-tests-by-using-a-dot-runsettings-file#testrunparameters
      --maximum-failed-tests
        Arity: 1
        Hidden: False
        Description: Specifies a maximum number of test failures that, when exceeded, will abort the test run.
""";

        testHostResult.AssertOutputContains(output);
    }

    [TestFixture(TestFixtureSharingStrategy.PerTestGroup)]
    public sealed class TestAssetFixture(AcceptanceFixture acceptanceFixture) : TestAssetFixtureBase(acceptanceFixture.NuGetGlobalPackagesFolder)
    {
        public string TargetAssetPath => GetAssetPath(AssetName);

        public override IEnumerable<(string ID, string Name, string Code)> GetAssetsToGenerate()
        {
            yield return (AssetName, AssetName,
                SourceCode
                .PatchTargetFrameworks(TargetFrameworks.All)
                .PatchCodeWithReplace("$MSTestVersion$", MSTestVersion));
        }

        private const string SourceCode = """
#file HelpInfo.csproj
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <EnableMSTestRunner>true</EnableMSTestRunner>
    <TargetFrameworks>$TargetFrameworks$</TargetFrameworks>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="MSTest.TestAdapter" Version="$MSTestVersion$" />
    <PackageReference Include="MSTest.TestFramework" Version="$MSTestVersion$" />
  </ItemGroup>

</Project>

#file UnitTest1.cs

using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;

[TestClass]
public class TestClass
{
    [TestMethod]
    public void Test() {}
}
""";
    }
}
