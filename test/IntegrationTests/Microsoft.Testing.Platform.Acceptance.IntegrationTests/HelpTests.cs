// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Acceptance.IntegrationTests.Helpers;
using Microsoft.Testing.Platform.Helpers;

namespace Microsoft.Testing.Platform.Acceptance.IntegrationTests;

// [TestGroup]
public class HelpTests : AcceptanceTestBase
{
    private readonly TestAssetFixture _testAssetFixture;

    public HelpTests(ITestExecutionContext testExecutionContext, TestAssetFixture testAssetFixture)
        : base(testExecutionContext)
    {
        _testAssetFixture = testAssetFixture;
    }

    [ArgumentsProvider(nameof(TargetFrameworks.All), typeof(TargetFrameworks))]
    public async Task Help_WhenNoExtensionRegistered_OutputDefaultHelpContent(string tfm)
    {
        TestInfrastructure.TestHost testHost = TestInfrastructure.TestHost.LocateFrom(_testAssetFixture.NoExtensionTargetAssetPath, TestAssetFixture.NoExtensionAssetName, tfm);
        TestHostResult testHostResult = await testHost.ExecuteAsync("--help");

        testHostResult.AssertExitCodeIs(ExitCodes.Success);

        const string RegexMatchPattern = $"""
Microsoft\(R\) Testing Platform Execution Command Line Tool
Version: \d+\.\d+\.\d+(-.*)?
RuntimeInformation: .+
Copyright\(c\) Microsoft Corporation\.  All rights reserved\.
Usage {TestAssetFixture.NoExtensionAssetName}.* \[option providers\] \[extension option providers\]
Execute a .NET Test Application\.
Options:
  --diagnostic                             Enable the diagnostic logging\. The default log level is 'Information'\. The file will be written in the output directory with the name log_\[MMddHHssfff\]\.diag
  --diagnostic-filelogger-synchronouswrite Force the built-in file logger to write the log synchronously\. Useful for scenario where you don't want to lose any log \(i\.e\. in case of crash\)\. Note that this is slowing down the test execution\.
  --diagnostic-output-directory            Output directory of the diagnostic logging, if not specified the file will be generated inside the default 'TestResults' directory\.
  --diagnostic-output-fileprefix           Prefix for the log file name that will replace '\[log\]_\.'
  --diagnostic-verbosity                   Define the level of the verbosity for the --diagnostic\. The available values are 'Trace', 'Debug', 'Information', 'Warning', 'Error', and 'Critical'
  --help                                   Show the command line help\.
  --ignore-exit-code                       Do not report non successful exit value for specific exit codes \(e\.g\. '--ignore-exit-code 8;9' ignore exit code 8 and 9 and will return 0 in these case\)
  --info                                   Display \.NET test application information\.
  --list-tests                             List available tests\.
  --minimum-expected-tests                 Specifies the minimum number of tests that are expected to run\.
  --results-directory                      The directory where the test results are going to be placed\. If the specified directory doesn't exist, it's created\. The default is TestResults in the directory that contains the test application\.
Extension options:
  --treenode-filter Use a tree filter to filter down the tests to execute
""";

        testHostResult.AssertOutputMatchesRegex(RegexMatchPattern);
    }

    [ArgumentsProvider(nameof(TargetFrameworks.All), typeof(TargetFrameworks))]
    public async Task Help_WhenNoExtensionRegisteredAndUnknownOptionIsSpecified_OutputDefaultHelpContentAndUnknownOption(string tfm)
    {
        const string UnknownOption = "aaa";

        TestInfrastructure.TestHost testHost = TestInfrastructure.TestHost.LocateFrom(_testAssetFixture.NoExtensionTargetAssetPath, TestAssetFixture.NoExtensionAssetName, tfm);
        TestHostResult testHostResult = await testHost.ExecuteAsync($"-{UnknownOption}");

        testHostResult.AssertExitCodeIs(ExitCodes.InvalidCommandLine);

        const string RegexMatchPattern = $"""
Unknown option '--{UnknownOption}'
Usage {TestAssetFixture.NoExtensionAssetName}.* \[option providers\] \[extension option providers\]
Execute a \.NET Test Application\.
Options:
  --diagnostic                             Enable the diagnostic logging\. The default log level is 'Information'\. The file will be written in the output directory with the name log_\[MMddHHssfff\]\.diag
  --diagnostic-filelogger-synchronouswrite Force the built-in file logger to write the log synchronously\. Useful for scenario where you don't want to lose any log \(i\.e\. in case of crash\)\. Note that this is slowing down the test execution\.
  --diagnostic-output-directory            Output directory of the diagnostic logging, if not specified the file will be generated inside the default 'TestResults' directory\.
  --diagnostic-output-fileprefix           Prefix for the log file name that will replace '\[log\]_\.'
  --diagnostic-verbosity                   Define the level of the verbosity for the --diagnostic\. The available values are 'Trace', 'Debug', 'Information', 'Warning', 'Error', and 'Critical'
  --help                                   Show the command line help\.
  --ignore-exit-code                       Do not report non successful exit value for specific exit codes \(e\.g\. '--ignore-exit-code 8;9' ignore exit code 8 and 9 and will return 0 in these case\)
  --info                                   Display \.NET test application information\.
  --list-tests                             List available tests\.
  --minimum-expected-tests                 Specifies the minimum number of tests that are expected to run\.
  --results-directory                      The directory where the test results are going to be placed\. If the specified directory doesn't exist, it's created\. The default is TestResults in the directory that contains the test application\.
Extension options:
  --treenode-filter Use a tree filter to filter down the tests to execute
""";

        testHostResult.AssertOutputMatchesRegex(RegexMatchPattern);
    }

    [ArgumentsProvider(nameof(TargetFrameworks.All), typeof(TargetFrameworks))]
    public async Task Help_WhenMSTestExtensionRegistered_OutputHelpContentOfRegisteredExtension(string tfm)
    {
        TestInfrastructure.TestHost testHost = TestInfrastructure.TestHost.LocateFrom(_testAssetFixture.MSTestTargetAssetPath, TestAssetFixture.MSTestAssetName, tfm);
        TestHostResult testHostResult = await testHost.ExecuteAsync("--help");

        testHostResult.AssertExitCodeIs(ExitCodes.Success);

        const string RegexMatchPattern = $"""
Microsoft\(R\) Testing Platform Execution Command Line Tool
Version: \d+\.\d+\.\d+(-.*)?
RuntimeInformation: .+
Copyright\(c\) Microsoft Corporation\.  All rights reserved\.
Usage {TestAssetFixture.MSTestAssetName}.* \[option providers\] \[extension option providers\]
Execute a .NET Test Application\.
Options:
  --diagnostic                             Enable the diagnostic logging\. The default log level is 'Information'\. The file will be written in the output directory with the name log_\[MMddHHssfff\]\.diag
  --diagnostic-filelogger-synchronouswrite Force the built-in file logger to write the log synchronously\. Useful for scenario where you don't want to lose any log \(i\.e\. in case of crash\)\. Note that this is slowing down the test execution\.
  --diagnostic-output-directory            Output directory of the diagnostic logging, if not specified the file will be generated inside the default 'TestResults' directory\.
  --diagnostic-output-fileprefix           Prefix for the log file name that will replace '\[log\]_\.'
  --diagnostic-verbosity                   Define the level of the verbosity for the --diagnostic\. The available values are 'Trace', 'Debug', 'Information', 'Warning', 'Error', and 'Critical'
  --help                                   Show the command line help\.
  --ignore-exit-code                       Do not report non successful exit value for specific exit codes \(e\.g\. '--ignore-exit-code 8;9' ignore exit code 8 and 9 and will return 0 in these case\)
  --info                                   Display \.NET test application information\.
  --list-tests                             List available tests\.
  --minimum-expected-tests                 Specifies the minimum number of tests that are expected to run\.
  --results-directory                      The directory where the test results are going to be placed\. If the specified directory doesn't exist, it's created\. The default is TestResults in the directory that contains the test application\.
Extension options:
  --filter   Filters tests using the given expression\. For more information, see the Filter option details section\. For more information and examples on how to use selective unit test filtering, see https://learn\.microsoft\.com/dotnet/core/testing/selective-unit-tests\.
  --settings The path, relative or absolute, to the \.runsettings file\.For more information and examples on how to configure test run, see https://learn\.microsoft\.com/visualstudio/test/configure-unit-tests-by-using-a-dot-runsettings-file#the-runsettings-file
""";

        testHostResult.AssertOutputMatchesRegex(RegexMatchPattern);
    }

    [TestFixture(TestFixtureSharingStrategy.PerTestGroup)]
    public sealed class TestAssetFixture(AcceptanceFixture acceptanceFixture) : TestAssetFixtureBase(acceptanceFixture.NuGetGlobalPackagesFolder)
    {
        public const string NoExtensionAssetName = "NoExtensionHelpTest";
        public const string MSTestAssetName = "MSTestHelpTest";

        private const string NoExtensionHelpTestCode = """
#file NoExtensionHelpTest.csproj
<Project Sdk="Microsoft.NET.Sdk">
    <PropertyGroup>
        <TargetFrameworks>$TargetFrameworks$</TargetFrameworks>
        <ImplicitUsings>enable</ImplicitUsings>
        <Nullable>enable</Nullable>
        <OutputType>Exe</OutputType>
        <UseAppHost>true</UseAppHost>
        <LangVersion>preview</LangVersion>
    </PropertyGroup>
    <ItemGroup>
        <PackageReference Include="Microsoft.Testing.Platform" Version="$MicrosoftTestingPlatformVersion$" />
        <PackageReference Include="Microsoft.Testing.Framework" Version="$MicrosoftTestingPlatformExtensionsVersion$" />
        <PackageReference Include="Microsoft.Testing.Framework.SourceGeneration" Version="$MicrosoftTestingPlatformExtensionsVersion$" />
    </ItemGroup>
</Project>

#file Program.cs
using NoExtensionHelpTest;
ITestApplicationBuilder builder = await TestApplication.CreateBuilderAsync(args);
builder.AddTestFramework(new SourceGeneratedTestNodesBuilder());
using ITestApplication app = await builder.BuildAsync();
return await app.RunAsync();

#file UnitTest1.cs
namespace NoExtensionHelpTest;

// [TestGroup]
public class UnitTest1
{
    public void TestMethod1()
    {
        Assert.IsTrue(true);
    }
}

#file Usings.cs
global using Microsoft.Testing.Platform.Builder;
global using Microsoft.Testing.Framework;
global using Microsoft.Testing.Extensions;
""";

        private const string MSTestCode = """
#file MSTestHelpTest.csproj
<Project Sdk="Microsoft.NET.Sdk">
    <PropertyGroup>
        <TargetFrameworks>$TargetFrameworks$</TargetFrameworks>
        <ImplicitUsings>enable</ImplicitUsings>
        <Nullable>enable</Nullable>
        <OutputType>Exe</OutputType>
        <UseAppHost>true</UseAppHost>
        <LangVersion>preview</LangVersion>
        <EnableMSTestRunner>true</EnableMSTestRunner>
    </PropertyGroup>
    <ItemGroup>
        <PackageReference Include="Microsoft.Testing.Platform" Version="$MicrosoftTestingPlatformVersion$" />
        <PackageReference Include="MSTest" Version="$MSTestVersion$" />
        <!-- Required for internal build -->
        <PackageReference Include="Microsoft.Testing.Extensions.VSTestBridge" Version="$MicrosoftTestingPlatformExtensionsVersion$" />
        <PackageReference Include="Microsoft.Testing.Platform.MSBuild" Version="$MicrosoftTestingPlatformExtensionsVersion$" />
    </ItemGroup>
</Project>

#file Program.cs
using MSTestHelpTest;

ITestApplicationBuilder builder = await TestApplication.CreateBuilderAsync(args);
builder.AddMSTest(() => new[] { typeof(Program).Assembly });
using ITestApplication app = await builder.BuildAsync();
return await app.RunAsync();

#file UnitTest1.cs
namespace MSTestHelpTest;

[TestClass]
public class UnitTest1
{
    [TestMethod]
    public void TestMethod1()
    {
    }
}

#file Usings.cs
global using Microsoft.Testing.Platform.Builder;
global using Microsoft.Testing.Extensions;
global using Microsoft.VisualStudio.TestTools.UnitTesting;
""";

        public string NoExtensionTargetAssetPath => GetAssetPath(NoExtensionAssetName);

        public string MSTestTargetAssetPath => GetAssetPath(MSTestAssetName);

        public override IEnumerable<(string ID, string Name, string Code)> GetAssetsToGenerate()
        {
            yield return (NoExtensionAssetName, NoExtensionAssetName,
                NoExtensionHelpTestCode
                .PatchTargetFrameworks(TargetFrameworks.All)
                .PatchCodeWithReplace("$MicrosoftTestingPlatformVersion$", MicrosoftTestingPlatformVersion)
                .PatchCodeWithReplace("$MicrosoftTestingPlatformExtensionsVersion$", MicrosoftTestingPlatformExtensionsVersion));
            yield return (MSTestAssetName, MSTestAssetName,
                MSTestCode
                .PatchTargetFrameworks(TargetFrameworks.All)
                .PatchCodeWithReplace("$MicrosoftTestingPlatformVersion$", MicrosoftTestingPlatformVersion)
                .PatchCodeWithReplace("$MicrosoftTestingPlatformExtensionsVersion$", MicrosoftTestingPlatformExtensionsVersion)
                .PatchCodeWithReplace("$MSTestVersion$", MSTestVersion));
        }
    }
}
