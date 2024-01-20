// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Acceptance.IntegrationTests.Helpers;
using Microsoft.Testing.Platform.Helpers;

namespace Microsoft.Testing.Platform.Acceptance.IntegrationTests;

[TestGroup]
public class InfoTests : AcceptanceTestBase
{
    private readonly TestAssetFixture _testAssetFixture;

    public InfoTests(ITestExecutionContext testExecutionContext, TestAssetFixture testAssetFixture)
        : base(testExecutionContext)
    {
        _testAssetFixture = testAssetFixture;
    }

    [ArgumentsProvider(nameof(TargetFrameworks.All), typeof(TargetFrameworks))]
    public async Task Info_WhenNoExtensionRegistered_OutputDefaultInfoContent(string tfm)
    {
        TestInfrastructure.TestHost testHost = TestInfrastructure.TestHost.LocateFrom(_testAssetFixture.NoExtensionTargetAssetPath, TestAssetFixture.NoExtensionAssetName, tfm);
        TestHostResult testHostResult = await testHost.ExecuteAsync("--info");

        testHostResult.AssertExitCodeIs(ExitCodes.Success);

        string regexMatchPattern = $"""
Microsoft\(R\) Testing Platform Execution Command Line Tool
Version: \d+\.\d+\.\d+(-.*)?
RuntimeInformation: .+
Copyright\(c\) Microsoft Corporation\.  All rights reserved\.
Microsoft Testing Platform:
  Version: \d+\.\d+\.\d+(-.*)?
  Dynamic Code Supported: True
  Runtime information: .+({Environment.NewLine}  Runtime location: .+)?
  Test module: .+{TestAssetFixture.NoExtensionAssetName}.*
Built-in command line providers:
  PlatformCommandLineProvider
    Name: Platform command line provider
    Version: \d+\.\d+\.\d+
    Description: Microsoft Testing Platform command line provider
    Options:
      --client-host
        Arity: 1
        Hidden: True
        Description: Specify the hostname of the client\.
      --client-port
        Arity: 1
        Hidden: True
        Description: Specify the port of the client\.
      --diagnostic
        Arity: 0
        Hidden: False
        Description: Enable the diagnostic logging\. The default log level is 'Trace'\. The file will be written in the output directory with the name log_\[MMddHHssfff\]\.diag
      --diagnostic-filelogger-synchronouswrite
        Arity: 0
        Hidden: False
        Description: Force the built-in file logger to write the log synchronously\. Useful for scenario where you don't want to lose any log \(i\.e\. in case of crash\)\. Note that this is slowing down the test execution\.
      --diagnostic-output-directory
        Arity: 1
        Hidden: False
        Description: Output directory of the diagnostic logging, if not specified the file will be generated inside the default 'TestResults' directory\.
      --diagnostic-output-fileprefix
        Arity: 1
        Hidden: False
        Description: Prefix for the log file name that will replace '\[log\]_\.'
      --diagnostic-verbosity
        Arity: 1
        Hidden: False
        Description: Define the level of the verbosity for the --diagnostic\. The available values are 'Trace', 'Debug', 'Information', 'Warning', 'Error', and 'Critical'
      --help
        Arity: 0
        Hidden: False
        Description: Show the command line help\.
      --ignore-exit-code
        Arity: 1
        Hidden: False
        Description: Do not report non successful exit value for specific exit codes \(e\.g\. '--ignore-exit-code 8;9' ignore exit code 8 and 9 and will return 0 in these case\)
      --info
        Arity: 0
        Hidden: False
        Description: Display \.NET test application information\.
      --internal-testhostcontroller-pid
        Arity: 0\.\.1
        Hidden: True
        Description: Eventual parent test host controller PID\.
      --internal-testingplatform-skipbuildercheck
        Arity: 0
        Hidden: True
        Description: For testing purposes
      --internal-vstest-adapter
        Arity: 0
        Hidden: True
        Description: Bridge to VSTest APIs
      --list-tests
        Arity: 0
        Hidden: False
        Description: List available tests\.
      --minimum-expected-tests
        Arity: 0\.\.1
        Hidden: False
        Description: Specifies the minimum number of tests that are expected to run\.
      --no-banner
        Arity: 0\.\.1
        Hidden: True
        Description: Do not display the startup banner, the copyright message or the telemetry banner\.
      --port
        Arity: 1
        Hidden: True
        Description: Specify the port of the server\.
      --results-directory
        Arity: 1
        Hidden: False
        Description: The directory where the test results are going to be placed\. If the specified directory doesn't exist, it's created\. The default is TestResults in the directory that contains the test application\.
      --server
        Arity: 0
        Hidden: True
        Description: Enable the server mode\.
Registered command line providers:
  TestingFrameworkExtension
    Name: Microsoft Testing Framework
    Version: \d+\.\d+\.\d+
    Description: Microsoft Testing Framework\. This framework allows you to test your code anywhere in any mode \(all OSes, all platforms, all configurations\.\.\.\)\.
    Options:
      --treenode-filter
        Arity: 0\.\.1
        Hidden: False
        Description: Use a tree filter to filter down the tests to execute
Registered tools:
  There are no registered tools\.
""";

        testHostResult.AssertOutputMatchesRegex(regexMatchPattern);
    }

    [ArgumentsProvider(nameof(TargetFrameworks.All), typeof(TargetFrameworks))]
    public async Task Info_WhenMSTestExtensionRegistered_OutputInfoContentOfRegisteredExtension(string tfm)
    {
        TestInfrastructure.TestHost testHost = TestInfrastructure.TestHost.LocateFrom(_testAssetFixture.MSTestTargetAssetPath, TestAssetFixture.MSTestAssetName, tfm);
        TestHostResult testHostResult = await testHost.ExecuteAsync("--info");

        testHostResult.AssertExitCodeIs(ExitCodes.Success);

        const string RegexMatchPattern = $"""
Registered command line providers:
  MSTestExtension
    Name: MSTest
    Version: \d+\.\d+\.\d+(-.*)?
    Description: MSTest Framework for Microsoft Testing Platform
    Options:
      --settings
        Arity: 0\.\.1
        Hidden: False
        Description: The path, relative or absolute, to the \.runsettings file\.For more information and examples on how to configure test run, see https://learn\.microsoft\.com/visualstudio/test/configure-unit-tests-by-using-a-dot-runsettings-file#the-runsettings-file
      --filter
        Arity: 0\.\.1
        Hidden: False
        Description: Filters tests using the given expression\. For more information, see the Filter option details section\. For more information and examples on how to use selective unit test filtering, see https://learn\.microsoft\.com/dotnet/core/testing/selective-unit-tests\.
""";

        testHostResult.AssertOutputMatchesRegex(RegexMatchPattern);
    }

    [TestFixture(TestFixtureSharingStrategy.PerTestGroup)]
    public sealed class TestAssetFixture(AcceptanceFixture acceptanceFixture) : TestAssetFixtureBase(acceptanceFixture.NuGetGlobalPackagesFolder)
    {
        public const string NoExtensionAssetName = "NoExtensionInfoTest";
        public const string MSTestAssetName = "MSTestInfoTest";

        private const string NoExtensionTestCode = """
#file NoExtensionInfoTest.csproj
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
using NoExtensionInfoTest;
ITestApplicationBuilder builder = await TestApplication.CreateBuilderAsync(args);
builder.AddTestFramework(new SourceGeneratedTestNodesBuilder());
using ITestApplication app = await builder.BuildAsync();
return await app.RunAsync();

#file UnitTest1.cs
namespace NoExtensionInfoTest;

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
global using Microsoft.Testing.Extensions;
""";

        private const string MSTestCode = """
#file MSTestInfoTest.csproj
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
using MSTestInfoTest;

ITestApplicationBuilder builder = await TestApplication.CreateBuilderAsync(args);
builder.AddMSTest(() => new[] { typeof(Program).Assembly });
using ITestApplication app = await builder.BuildAsync();
return await app.RunAsync();

#file UnitTest1.cs
namespace MSTestInfoTest;

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
global using Microsoft.VisualStudio.TestTools.UnitTesting;
""";

        public string NoExtensionTargetAssetPath => GetAssetPath(NoExtensionAssetName);

        public string MSTestTargetAssetPath => GetAssetPath(MSTestAssetName);

        public override IEnumerable<(string ID, string Name, string Code)> GetAssetsToGenerate()
        {
            yield return (NoExtensionAssetName, NoExtensionAssetName,
                NoExtensionTestCode
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
