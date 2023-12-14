// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Acceptance.IntegrationTests.Helpers;
using Microsoft.Testing.Platform.Helpers;

namespace Microsoft.Testing.Platform.Acceptance.IntegrationTests;

[TestGroup]
public class HelpTests : BaseAcceptanceTests
{
    private readonly HelpAssetsFixture _helpAssetsFixture;

    public HelpTests(ITestExecutionContext testExecutionContext, AcceptanceFixture acceptanceFixture, HelpAssetsFixture helpAssetsFixture)
        : base(testExecutionContext, acceptanceFixture)
    {
        _helpAssetsFixture = helpAssetsFixture;
    }

    [ArgumentsProvider(nameof(TargetFrameworks.All), typeof(TargetFrameworks))]
    public async Task Help_WhenNoExtensionRegistered_OutputDefaultHelpContent(string tfm)
    {
        TestInfrastructure.TestHost testHost = TestInfrastructure.TestHost.LocateFrom(_helpAssetsFixture.NoExtensionTargetAssetPath, HelpAssetsFixture.NoExtensionAssetName, tfm);
        TestHostResult testHostResult = await testHost.ExecuteAsync(
            "--help",
            new Dictionary<string, string> { { "TESTINGPLATFORM_TELEMETRY_OPTOUT", "1" } });

        testHostResult.AssertHasExitCode(ExitCodes.Success);

        const string RegexMatchPattern = $"""
Microsoft\(R\) Testing Platform Execution Command Line Tool
Version: \d+\.\d+\.\d+(-.*)?
RuntimeInformation: .+
Copyright\(c\) Microsoft Corporation\.  All rights reserved\.
Usage {HelpAssetsFixture.NoExtensionAssetName}.* \[option providers\] \[extension option providers\]
Execute a .NET Test Application\.
Options:
  --diagnostic                             Enable the diagnostic logging\. The default log level is 'Information'\. The file will be written in the output directory with the name log_\[MMddHHssfff\]\.diag
  --diagnostic-filelogger-synchronouswrite Force the built-in file logger to write the log synchronously\. Useful for scenario where you don't want to lose any log \(i\.e\. in case of crash\)\. Note that this is slowing down the test execution\.
  --diagnostic-output-directory            Output directory of the diagnostic logging, if not specified the file will be generated inside the default 'TestResults' directory\.
  --diagnostic-output-fileprefix           Prefix for the log file name that will replace '\[log\]_\.'
  --diagnostic-verbosity                   Define the level of the verbosity for the --diagnostic\. The available values are 'Trace', 'Debug', 'Information', 'Warning', 'Error', and 'Critical'
  --help                                   Show the command line help\.
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

        TestInfrastructure.TestHost testHost = TestInfrastructure.TestHost.LocateFrom(_helpAssetsFixture.NoExtensionTargetAssetPath, HelpAssetsFixture.NoExtensionAssetName, tfm);
        TestHostResult testHostResult = await testHost.ExecuteAsync($"-{UnknownOption}");

        testHostResult.AssertHasExitCode(ExitCodes.InvalidCommandLine);

        const string RegexMatchPattern = $"""
Unknown option '--{UnknownOption}'
Usage {HelpAssetsFixture.NoExtensionAssetName}.* \[option providers\] \[extension option providers\]
Execute a \.NET Test Application\.
Options:
  --diagnostic                             Enable the diagnostic logging\. The default log level is 'Information'\. The file will be written in the output directory with the name log_\[MMddHHssfff\]\.diag
  --diagnostic-filelogger-synchronouswrite Force the built-in file logger to write the log synchronously\. Useful for scenario where you don't want to lose any log \(i\.e\. in case of crash\)\. Note that this is slowing down the test execution\.
  --diagnostic-output-directory            Output directory of the diagnostic logging, if not specified the file will be generated inside the default 'TestResults' directory\.
  --diagnostic-output-fileprefix           Prefix for the log file name that will replace '\[log\]_\.'
  --diagnostic-verbosity                   Define the level of the verbosity for the --diagnostic\. The available values are 'Trace', 'Debug', 'Information', 'Warning', 'Error', and 'Critical'
  --help                                   Show the command line help\.
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
        TestInfrastructure.TestHost testHost = TestInfrastructure.TestHost.LocateFrom(_helpAssetsFixture.MSTestTargetAssetPath, HelpAssetsFixture.MSTestAssetName, tfm);
        TestHostResult testHostResult = await testHost.ExecuteAsync(
            "--help",
            new Dictionary<string, string> { { "TESTINGPLATFORM_TELEMETRY_OPTOUT", "1" } });

        testHostResult.AssertHasExitCode(ExitCodes.Success);

        const string RegexMatchPattern = $"""
Microsoft\(R\) Testing Platform Execution Command Line Tool
Version: \d+\.\d+\.\d+(-.*)?
RuntimeInformation: .+
Copyright\(c\) Microsoft Corporation\.  All rights reserved\.
Usage {HelpAssetsFixture.MSTestAssetName}.* \[option providers\] \[extension option providers\]
Execute a .NET Test Application\.
Options:
  --diagnostic                             Enable the diagnostic logging\. The default log level is 'Information'\. The file will be written in the output directory with the name log_\[MMddHHssfff\]\.diag
  --diagnostic-filelogger-synchronouswrite Force the built-in file logger to write the log synchronously\. Useful for scenario where you don't want to lose any log \(i\.e\. in case of crash\)\. Note that this is slowing down the test execution\.
  --diagnostic-output-directory            Output directory of the diagnostic logging, if not specified the file will be generated inside the default 'TestResults' directory\.
  --diagnostic-output-fileprefix           Prefix for the log file name that will replace '\[log\]_\.'
  --diagnostic-verbosity                   Define the level of the verbosity for the --diagnostic\. The available values are 'Trace', 'Debug', 'Information', 'Warning', 'Error', and 'Critical'
  --help                                   Show the command line help\.
  --info                                   Display \.NET test application information\.
  --list-tests                             List available tests\.
  --minimum-expected-tests                 Specifies the minimum number of tests that are expected to run\.
  --results-directory                      The directory where the test results are going to be placed\. If the specified directory doesn't exist, it's created\. The default is TestResults in the directory that contains the test application\.
Extension options:
  --vstest-filter      Filters tests using the given expression\. For more information, see the Filter option details section\. For more information and examples on how to use selective unit test filtering, see https://learn\.microsoft\.com/dotnet/core/testing/selective-unit-tests\.
  --vstest-runsettings The path, relative or absolute, to the \.runsettings file\.For more information and examples on how to configure test run, see https://learn\.microsoft\.com/visualstudio/test/configure-unit-tests-by-using-a-dot-runsettings-file#the-runsettings-file
""";

        testHostResult.AssertOutputMatchesRegex(RegexMatchPattern);
    }

    [TestFixture(TestFixtureSharingStrategy.PerTestGroup)]
    public sealed class HelpAssetsFixture : IAsyncInitializable, IDisposable
    {
        public const string NoExtensionAssetName = "NoExtensionHelpTest";
        public const string MSTestAssetName = "MSTestHelpTest";

        private readonly AcceptanceFixture _acceptanceFixture;
        private TestAsset? _noExtensionTestAsset;
        private TestAsset? _mstestTestAsset;

        public string NoExtensionTargetAssetPath => _noExtensionTestAsset!.TargetAssetPath;

        public string MSTestTargetAssetPath => _mstestTestAsset!.TargetAssetPath;

        public HelpAssetsFixture(AcceptanceFixture acceptanceFixture)
        {
            _acceptanceFixture = acceptanceFixture;
        }

        public async Task InitializeAsync(InitializationContext context)
        {
            await Task.WhenAll(
                GenerateNoExtensionTestAsset(),
                GenerateMSTestTestAsset());

            async Task GenerateNoExtensionTestAsset()
            {
                _noExtensionTestAsset = await TestAsset.GenerateAssetAsync(
                    NoExtensionAssetName,
                    NoExtensionHelpTestCode.PatchCodeWithRegularExpression("tfms", TargetFrameworks.All.ToMSBuildTargetFrameworks()));
                await DotnetCli.RunAsync($"build -nodeReuse:false {_noExtensionTestAsset.TargetAssetPath} -c Release", _acceptanceFixture.NuGetGlobalPackagesFolder);
            }

            async Task GenerateMSTestTestAsset()
            {
                _mstestTestAsset = await TestAsset.GenerateAssetAsync(
                    MSTestAssetName,
                    MSTestCode.PatchCodeWithRegularExpression("tfms", TargetFrameworks.All.ToMSBuildTargetFrameworks()));
                await DotnetCli.RunAsync($"build -nodeReuse:false {_mstestTestAsset.TargetAssetPath} -c Release", _acceptanceFixture.NuGetGlobalPackagesFolder);
            }
        }

        public void Dispose()
        {
            _noExtensionTestAsset?.Dispose();
            _mstestTestAsset?.Dispose();
        }
    }

    private const string NoExtensionHelpTestCode = """
#file NoExtensionHelpTest.csproj
<Project Sdk="Microsoft.NET.Sdk">
    <PropertyGroup>
        <TargetFrameworks>tfms</TargetFrameworks>
        <ImplicitUsings>enable</ImplicitUsings>
        <Nullable>enable</Nullable>
        <OutputType>Exe</OutputType>
        <UseAppHost>true</UseAppHost>
        <LangVersion>preview</LangVersion>
    </PropertyGroup>
    <ItemGroup>
        <PackageReference Include="Microsoft.Testing.Framework" Version="[1.0.0-*,)" />
        <PackageReference Include="Microsoft.Testing.Framework.SourceGeneration" Version="[1.0.0-*,)" />
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

[TestGroup]
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
global using Microsoft.Testing.Platform.Extensions;
""";

    private const string MSTestCode = """
#file MSTestHelpTest.csproj
<Project Sdk="Microsoft.NET.Sdk">
    <PropertyGroup>
        <TargetFrameworks>tfms</TargetFrameworks>
        <ImplicitUsings>enable</ImplicitUsings>
        <Nullable>enable</Nullable>
        <OutputType>Exe</OutputType>
        <UseAppHost>true</UseAppHost>
        <LangVersion>preview</LangVersion>
        <EnableMSTestRunner>true</EnableMSTestRunner>
    </PropertyGroup>
    <ItemGroup>
        <PackageReference Include="Microsoft.Testing.Platform" Version="[1.0.0-*,)" />
        <PackageReference Include="Microsoft.Testing.Platform.MSBuild" Version="[1.0.0-*,)" />
        <PackageReference Include="Microsoft.Testing.Platform.Extensions.VSTestBridge" Version="[1.0.0-*,)" />
        <PackageReference Include="MSTest" Version="[1.0.0-*,)" />
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
global using Microsoft.Testing.Platform.Extensions;
global using Microsoft.VisualStudio.TestTools.UnitTesting;
""";
}
