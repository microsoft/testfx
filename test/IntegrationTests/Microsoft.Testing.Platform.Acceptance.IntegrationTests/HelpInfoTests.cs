// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Acceptance.IntegrationTests.Helpers;
using Microsoft.Testing.Platform.Helpers;

namespace Microsoft.Testing.Platform.Acceptance.IntegrationTests;

[TestGroup]
public class HelpInfoTests : AcceptanceTestBase
{
    private readonly TestAssetFixture _testAssetFixture;

    public HelpInfoTests(ITestExecutionContext testExecutionContext, TestAssetFixture testAssetFixture)
        : base(testExecutionContext) => _testAssetFixture = testAssetFixture;

    [ArgumentsProvider(nameof(TargetFrameworks.All), typeof(TargetFrameworks))]
    public async Task Help_WhenNoExtensionRegistered_OutputDefaultHelpContent(string tfm)
    {
        var testHost = TestInfrastructure.TestHost.LocateFrom(_testAssetFixture.NoExtensionTargetAssetPath, TestAssetFixture.NoExtensionAssetName, tfm);
        TestHostResult testHostResult = await testHost.ExecuteAsync("--help");

        testHostResult.AssertExitCodeIs(ExitCodes.Success);

        const string wildcardMatchPattern = $"""
.NET Testing Platform v*
Usage {TestAssetFixture.NoExtensionAssetName}* [option providers] [extension option providers]
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
    --no-ansi
        Disable outputting ANSI escape characters to screen.
    --no-progress
        Disable reporting progress to screen.
    --output
        Output verbosity when reporting tests.
        Valid values are 'Normal', 'Detailed'. Default is 'Normal'.
    --treenode-filter
        Use a tree filter to filter down the tests to execute
""";

        testHostResult.AssertOutputMatchesLines(wildcardMatchPattern);
    }

    [ArgumentsProvider(nameof(TargetFrameworks.All), typeof(TargetFrameworks))]
    public async Task HelpShortName_WhenNoExtensionRegistered_OutputDefaultHelpContent(string tfm)
    {
        var testHost = TestInfrastructure.TestHost.LocateFrom(_testAssetFixture.NoExtensionTargetAssetPath, TestAssetFixture.NoExtensionAssetName, tfm);
        TestHostResult testHostResult = await testHost.ExecuteAsync("--?");

        testHostResult.AssertExitCodeIs(ExitCodes.Success);

        const string wildcardMatchPattern = $"""
.NET Testing Platform v*
Usage {TestAssetFixture.NoExtensionAssetName}* [option providers] [extension option providers]
Execute a .NET Test Application.
Options:
""";

        testHostResult.AssertOutputMatchesLines(wildcardMatchPattern);
    }

    [ArgumentsProvider(nameof(TargetFrameworks.All), typeof(TargetFrameworks))]
    public async Task Help_WhenNoExtensionRegisteredAndUnknownOptionIsSpecified_OutputDefaultHelpContentAndUnknownOption(string tfm)
    {
        const string UnknownOption = "aaa";

        var testHost = TestInfrastructure.TestHost.LocateFrom(_testAssetFixture.NoExtensionTargetAssetPath, TestAssetFixture.NoExtensionAssetName, tfm);
        TestHostResult testHostResult = await testHost.ExecuteAsync($"-{UnknownOption}");

        testHostResult.AssertExitCodeIs(ExitCodes.InvalidCommandLine);

        const string wildcardMatchPattern = $"""
.NET Testing Platform v*
Unknown option '--{UnknownOption}'
Usage {TestAssetFixture.NoExtensionAssetName}* [option providers] [extension option providers]
Execute a .NET Test Application.
Options:
""";

        testHostResult.AssertOutputMatchesLines(wildcardMatchPattern);
    }

    [ArgumentsProvider(nameof(TargetFrameworks.All), typeof(TargetFrameworks))]
    public async Task Info_WhenNoExtensionRegistered_OutputDefaultInfoContent(string tfm)
    {
        var testHost = TestInfrastructure.TestHost.LocateFrom(_testAssetFixture.NoExtensionTargetAssetPath, TestAssetFixture.NoExtensionAssetName, tfm);
        TestHostResult testHostResult = await testHost.ExecuteAsync("--info");

        testHostResult.AssertExitCodeIs(ExitCodes.Success);

        string regexMatchPattern = $"""
.NET Testing Platform v.+ \[.+\]
Microsoft Testing Platform:
  Version: .+
  Dynamic Code Supported: True
  Runtime information: .+
  {(tfm != TargetFrameworks.NetFramework[0].Arguments ? "###SKIP###" : $"Runtime location: .+")}
  Test module: .+{TestAssetFixture.NoExtensionAssetName}.*
Built-in command line providers:
  PlatformCommandLineProvider
    Name: Platform command line provider
    Version: .+
    Description: Microsoft Testing Platform command line provider
    Options:
      --\?
        Arity: 0
        Hidden: True
        Description: Show the command line help\.
      --client-host
        Arity: 1
        Hidden: True
        Description: Specify the hostname of the client\.
      --client-port
        Arity: 1
        Hidden: True
        Description: Specify the port of the client\.
    --config-file
        Arity: 1
        Hidden: False
        Description: Specifies a testconfig\.json file\.
      --diagnostic
        Arity: 0
        Hidden: False
        Description: Enable the diagnostic logging\. The default log level is 'Trace'\.
        The file will be written in the output directory with the name log_\[yyMMddHHmmssfff\]\.diag
      --diagnostic-filelogger-synchronouswrite
        Arity: 0
        Hidden: False
        Description: Force the built-in file logger to write the log synchronously\.
        Useful for scenario where you don't want to lose any log \(i\.e\. in case of crash\)\.
        Note that this is slowing down the test execution\.
      --diagnostic-output-directory
        Arity: 1
        Hidden: False
        Description: Output directory of the diagnostic logging.
        If not specified the file will be generated inside the default 'TestResults' directory\.
      --diagnostic-output-fileprefix
        Arity: 1
        Hidden: False
        Description: Prefix for the log file name that will replace '\[log\]_\.'
      --diagnostic-verbosity
        Arity: 1
        Hidden: False
        Description: Define the level of the verbosity for the --diagnostic\.
        The available values are 'Trace', 'Debug', 'Information', 'Warning', 'Error', and 'Critical'
      --dotnet-test-pipe
        Arity: 1
        Hidden: True
        Description: dotnet test pipe\.
      --exit-on-process-exit
        Arity: 1
        Hidden: False
        Description: Exit the test process if dependent process exits\. PID must be provided\.
      --help
        Arity: 0
        Hidden: False
        Description: Show the command line help\.
      --ignore-exit-code
        Arity: 1
        Hidden: False
        Description: Do not report non successful exit value for specific exit codes
        \(e\.g\. '--ignore-exit-code 8;9' ignore exit code 8 and 9 and will return 0 in these case\)
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
      --results-directory
        Arity: 1
        Hidden: False
        Description: The directory where the test results are going to be placed\.
        If the specified directory doesn't exist, it's created\.
        The default is TestResults in the directory that contains the test application\.
      --server
        Arity: 0\.\.1
        Hidden: True
        Description: Enable the server mode\.
      --timeout
        Arity: 1
        Hidden: False
        Description: A global test execution timeout.
        Takes one argument as string in the format <value>\[h\|m\|s\] where 'value' is float\.
Registered command line providers:
  TerminalTestReporterCommandLineOptionsProvider
    Name: Terminal test reporter
    Version: .+
    Description: Writes test results to terminal.
    Options:
      --no-ansi
        Arity: 0
        Hidden: False
        Description: Disable outputting ANSI escape characters to screen.
      --no-progress
        Arity: 0
        Hidden: False
        Description: Disable reporting progress to screen.
      --output
        Arity: 1
        Hidden: False
        Description: Output verbosity when reporting tests.
        Valid values are 'Normal', 'Detailed'. Default is 'Normal'.
  TestingFrameworkExtension
    Name: Microsoft Testing Framework
    Version: .+
    Description: Microsoft Testing Framework\. This framework allows you to test your code anywhere in any mode \(all OSes, all platforms, all configurations\.\.\.\)\.
    Options:
      --treenode-filter
        Arity: 1
        Hidden: False
        Description: Use a tree filter to filter down the tests to execute
Registered tools:
  There are no registered tools\.
""";

        testHostResult.AssertOutputMatchesRegexLines(regexMatchPattern);
    }

    [ArgumentsProvider(nameof(TargetFrameworks.All), typeof(TargetFrameworks))]
    public async Task Help_WithAllExtensionsRegistered_OutputFullHelpContent(string tfm)
    {
        var testHost = TestInfrastructure.TestHost.LocateFrom(_testAssetFixture.AllExtensionsTargetAssetPath, TestAssetFixture.AllExtensionsAssetName, tfm);
        TestHostResult testHostResult = await testHost.ExecuteAsync("--help");

        testHostResult.AssertExitCodeIs(ExitCodes.Success);

        string wildcardPattern = $"""
.NET Testing Platform v*
Usage {TestAssetFixture.AllExtensionsAssetName}* [option providers] [extension option providers]
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
    --retry-failed-tests
        Enable retry failed tests
    --retry-failed-tests-max-percentage
        Disable retry mechanism if the percentage of failed tests is greater than the specified value
    --retry-failed-tests-max-tests
        Disable retry mechanism if the number of failed tests is greater than the specified value
    --timeout
        A global test execution timeout.
        Takes one argument as string in the format <value>[h|m|s] where 'value' is float.
Extension options:
    --crashdump
        [net6.0+ only] Generate a dump file if the test process crashes
    --crashdump-filename
        Specify the name of the dump file
    --crashdump-type
        Specify the type of the dump.
        Valid values are 'Mini', 'Heap', 'Triage' or 'Full'. Default type is 'Full'.
        For more information visit https://learn.microsoft.com/dotnet/core/diagnostics/collect-dumps-crash#types-of-mini-dumps
    --hangdump
        Generate a dump file if the test process hangs
    --hangdump-filename
        Specify the name of the dump file
    --hangdump-timeout
        Specify the timeout after which the dump will be generated.
        The timeout value is specified in one of the following formats:
            1.5h, 1.5hour, 1.5hours,
            90m, 90min, 90minute, 90minutes,
            5400s, 5400sec, 5400second, 5400seconds.
            Default is 30m.
    --hangdump-type
        Specify the type of the dump.
        Valid values are 'Mini', 'Heap', 'Triage' (only available in .NET 6+) or 'Full'.
        Default type is 'Full'
    --no-ansi
        Disable outputting ANSI escape characters to screen.
    --no-progress
        Disable reporting progress to screen.
    --output
        Output verbosity when reporting tests.
        Valid values are 'Normal', 'Detailed'. Default is 'Normal'.
    --report-trx
        Enable generating TRX report
    --report-trx-filename
        The name of the generated TRX report
    --treenode-filter
        Use a tree filter to filter down the tests to execute
""";

        testHostResult.AssertOutputMatchesLines(wildcardPattern);
    }

    [ArgumentsProvider(nameof(TargetFrameworks.All), typeof(TargetFrameworks))]
    public async Task HelpShortName_WithAllExtensionsRegistered_OutputFullHelpContent(string tfm)
    {
        var testHost = TestInfrastructure.TestHost.LocateFrom(_testAssetFixture.AllExtensionsTargetAssetPath, TestAssetFixture.AllExtensionsAssetName, tfm);
        TestHostResult testHostResult = await testHost.ExecuteAsync("-?");

        testHostResult.AssertExitCodeIs(ExitCodes.Success);

        string wildcardPattern = $"""
.NET Testing Platform v*
Usage {TestAssetFixture.AllExtensionsAssetName}* [option providers] [extension option providers]
Execute a .NET Test Application.
Options:
""";

        testHostResult.AssertOutputMatchesLines(wildcardPattern);
    }

    [ArgumentsProvider(nameof(TargetFrameworks.All), typeof(TargetFrameworks))]
    public async Task Info_WithAllExtensionsRegistered_OutputFullInfoContent(string tfm)
    {
        var testHost = TestInfrastructure.TestHost.LocateFrom(_testAssetFixture.AllExtensionsTargetAssetPath, TestAssetFixture.AllExtensionsAssetName, tfm);
        TestHostResult testHostResult = await testHost.ExecuteAsync("--info");

        testHostResult.AssertExitCodeIs(ExitCodes.Success);

        string wildcardPattern = $"""
.NET Testing Platform v* [*]
Microsoft Testing Platform:
  Version: *
  Dynamic Code Supported: True
  Runtime information: *
  {(tfm != TargetFrameworks.NetFramework[0].Arguments ? "###SKIP###" : $"Runtime location: *")}
  Test module: *{TestAssetFixture.AllExtensionsAssetName}*
Built-in command line providers:
  PlatformCommandLineProvider
    Name: Platform command line provider
    Version: *
    Description: Microsoft Testing Platform command line provider
    Options:
      --?
        Arity: 0
        Hidden: True
        Description: Show the command line help.
      --client-host
        Arity: 1
        Hidden: True
        Description: Specify the hostname of the client.
      --client-port
        Arity: 1
        Hidden: True
        Description: Specify the port of the client.
      --config-file
        Arity: 1
        Hidden: False
        Description: Specifies a testconfig.json file.
      --diagnostic
        Arity: 0
        Hidden: False
        Description: Enable the diagnostic logging. The default log level is 'Trace'.
        The file will be written in the output directory with the name log_[yyMMddHHmmssfff].diag
      --diagnostic-filelogger-synchronouswrite
        Arity: 0
        Hidden: False
        Description: Force the built-in file logger to write the log synchronously.
        Useful for scenario where you don't want to lose any log (i.e. in case of crash).
        Note that this is slowing down the test execution.
      --diagnostic-output-directory
        Arity: 1
        Hidden: False
        Description: Output directory of the diagnostic logging.
        If not specified the file will be generated inside the default 'TestResults' directory.
      --diagnostic-output-fileprefix
        Arity: 1
        Hidden: False
        Description: Prefix for the log file name that will replace '[log]_.'
      --diagnostic-verbosity
        Arity: 1
        Hidden: False
        Description: Define the level of the verbosity for the --diagnostic.
        The available values are 'Trace', 'Debug', 'Information', 'Warning', 'Error', and 'Critical'.
      --dotnet-test-pipe
        Arity: 1
        Hidden: True
        Description: dotnet test pipe.
      --exit-on-process-exit
        Arity: 1
        Hidden: False
        Description: Exit the test process if dependent process exits. PID must be provided.
      --help
        Arity: 0
        Hidden: False
        Description: Show the command line help.
      --ignore-exit-code
        Arity: 1
        Hidden: False
        Description: Do not report non successful exit value for specific exit codes
        (e.g. '--ignore-exit-code 8;9' ignore exit code 8 and 9 and will return 0 in these case)
      --info
        Arity: 0
        Hidden: False
        Description: Display .NET test application information.
      --internal-testhostcontroller-pid
        Arity: 0..1
        Hidden: True
        Description: Eventual parent test host controller PID.
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
        Description: List available tests.
      --minimum-expected-tests
        Arity: 0..1
        Hidden: False
        Description: Specifies the minimum number of tests that are expected to run.
      --no-banner
        Arity: 0..1
        Hidden: True
        Description: Do not display the startup banner, the copyright message or the telemetry banner.
      --results-directory
        Arity: 1
        Hidden: False
        Description: The directory where the test results are going to be placed.
        If the specified directory doesn't exist, it's created.
        The default is TestResults in the directory that contains the test application.
      --server
        Arity: 0..1
        Hidden: True
        Description: Enable the server mode.
      --timeout
        Arity: 1
        Hidden: False
        Description: A global test execution timeout.
        Takes one argument as string in the format <value>[h|m|s] where 'value' is float.
Registered command line providers:
  CrashDumpCommandLineProvider
    Name: Crash dump
    Version: *
    Description: [net6.0+ only] Produce crash dump files when the test execution process crashes unexpectedly
    Options:
      --crashdump
        Arity: 0
        Hidden: False
        Description: [net6.0+ only] Generate a dump file if the test process crashes
      --crashdump-filename
        Arity: 1
        Hidden: False
        Description: Specify the name of the dump file
      --crashdump-type
        Arity: 1
        Hidden: False
        Description: Specify the type of the dump.
        Valid values are 'Mini', 'Heap', 'Triage' or 'Full'. Default type is 'Full'.
        For more information visit https://learn.microsoft.com/dotnet/core/diagnostics/collect-dumps-crash#types-of-mini-dumps
  HangDumpCommandLineProvider
    Name: Hang dump
    Version: *
    Description: Produce hang dump files when a test execution exceed a given time.
    Options:
      --hangdump
        Arity: 0
        Hidden: False
        Description: Generate a dump file if the test process hangs
      --hangdump-filename
        Arity: 1
        Hidden: False
        Description: Specify the name of the dump file
      --hangdump-timeout
        Arity: 1
        Hidden: False
        Description: Specify the timeout after which the dump will be generated.
        The timeout value is specified in one of the following formats:
            1.5h, 1.5hour, 1.5hours,
            90m, 90min, 90minute, 90minutes,
            5400s, 5400sec, 5400second, 5400seconds.
            Default is 30m.
      --hangdump-type
        Arity: 1
        Hidden: False
        Description: Specify the type of the dump.
        Valid values are 'Mini', 'Heap', 'Triage' (only available in .NET 6+) or 'Full'.
        Default type is 'Full'
  RetryCommandLineOptionsProvider
    Name: Retry failed tests
    Version: *
    Description: Retry failed tests feature allows to restart test execution upon failure.
    Options:
      --internal-retry-pipename
        Arity: 1
        Hidden: True
        Description: Communication between the test host and the retry infra.
      --retry-failed-tests
        Arity: 1
        Hidden: False
        Description: Enable retry failed tests
      --retry-failed-tests-max-percentage
        Arity: 1
        Hidden: False
        Description: Disable retry mechanism if the percentage of failed tests is greater than the specified value
      --retry-failed-tests-max-tests
        Arity: 1
        Hidden: False
        Description: Disable retry mechanism if the number of failed tests is greater than the specified value
  TerminalTestReporterCommandLineOptionsProvider
    Name: Terminal test reporter
    Version: *
    Description: Writes test results to terminal.
    Options:
      --no-ansi
        Arity: 0
        Hidden: False
        Description: Disable outputting ANSI escape characters to screen.
      --no-progress
        Arity: 0
        Hidden: False
        Description: Disable reporting progress to screen.
      --output
        Arity: 1
        Hidden: False
        Description: Output verbosity when reporting tests.
        Valid values are 'Normal', 'Detailed'. Default is 'Normal'.
  TestingFrameworkExtension
    Name: Microsoft Testing Framework
    Version: *
    Description: Microsoft Testing Framework. This framework allows you to test your code anywhere in any mode (all OSes, all platforms, all configurations...).
    Options:
      --treenode-filter
        Arity: 1
        Hidden: False
        Description: Use a tree filter to filter down the tests to execute
  TrxReportGeneratorCommandLine
    Name: TRX report generator
    Version: *
    Description: Produce a TRX report for the current test session
    Options:
      --report-trx
        Arity: 0
        Hidden: False
        Description: Enable generating TRX report
      --report-trx-filename
        Arity: 1
        Hidden: False
        Description: The name of the generated TRX report
Registered tools:
  TrxCompareTool
    Command: ms-trxcompare
    Name: TRX comparer tool
    Version: *
    Description: This tool allows to compare and highlights differences between 2 TRX reports
    Tool command line providers:
      TrxCompareTool
        Name: TRX comparer tool
        Version: *
        Description: This tool allows to compare and highlights differences between 2 TRX reports
        Options:
          --baseline-trx
            Arity: 1
            Hidden: False
            Description: The baseline TRX file
          --trx-to-compare
            Arity: 1
            Hidden: False
            Description: The TRX file to compare with the baseline
""";

        testHostResult.AssertOutputMatchesLines(wildcardPattern);
    }

    [TestFixture(TestFixtureSharingStrategy.PerTestGroup)]
    public sealed class TestAssetFixture(AcceptanceFixture acceptanceFixture) : TestAssetFixtureBase(acceptanceFixture.NuGetGlobalPackagesFolder)
    {
        public const string AllExtensionsAssetName = "AllExtensionsInfoTest";
        public const string NoExtensionAssetName = "NoExtensionInfoTest";

        private const string AllExtensionsTestCode = """
#file AllExtensionsInfoTest.csproj
<Project Sdk="Microsoft.NET.Sdk">
    <PropertyGroup>
        <TargetFrameworks>$TargetFrameworks$</TargetFrameworks>
        <ImplicitUsings>enable</ImplicitUsings>
        <Nullable>enable</Nullable>
        <OutputType>Exe</OutputType>
        <LangVersion>preview</LangVersion>
    </PropertyGroup>
    <ItemGroup>
        <!-- Platform and TrxReport.Abstractions are only needed because Internal.Framework relies on a preview version that we want to override with currently built one -->
        <PackageReference Include="Microsoft.Testing.Platform" Version="$MicrosoftTestingPlatformVersion$" />
        <PackageReference Include="Microsoft.Testing.Extensions.TrxReport.Abstractions" Version="$MicrosoftTestingPlatformVersion$" />
        <PackageReference Include="Microsoft.Testing.Internal.Framework" Version="$MicrosoftTestingInternalFrameworkVersion$" />
        <PackageReference Include="Microsoft.Testing.Internal.Framework.SourceGeneration" Version="$MicrosoftTestingInternalFrameworkVersion$" />
        <PackageReference Include="Microsoft.Testing.Extensions.CrashDump" Version="$MicrosoftTestingPlatformVersion$" />
        <PackageReference Include="Microsoft.Testing.Extensions.HangDump" Version="$MicrosoftTestingPlatformVersion$" />
        <PackageReference Include="Microsoft.Testing.Extensions.HotReload" Version="$MicrosoftTestingEnterpriseExtensionsVersion$" />
        <PackageReference Include="Microsoft.Testing.Extensions.Retry" Version="$MicrosoftTestingEnterpriseExtensionsVersion$" />
        <PackageReference Include="Microsoft.Testing.Extensions.TrxReport" Version="$MicrosoftTestingPlatformVersion$" />
    </ItemGroup>
</Project>

#file Program.cs
using AllExtensionsInfoTest;
ITestApplicationBuilder builder = await TestApplication.CreateBuilderAsync(args);
builder.AddTestFramework(new SourceGeneratedTestNodesBuilder());
builder.AddCrashDumpProvider();
builder.AddHangDumpProvider();
builder.AddHotReloadProvider();
builder.AddRetryProvider();
builder.AddTrxReportProvider();
using ITestApplication app = await builder.BuildAsync();
return await app.RunAsync();

#file UnitTest1.cs
namespace AllExtensionsInfoTest;

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
global using Microsoft.Testing.Internal.Framework;
global using Microsoft.Testing.Extensions;
""";

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
        <!-- Platform and TrxReport.Abstractions are only needed because Internal.Framework relies on a preview version that we want to override with currently built one -->
        <PackageReference Include="Microsoft.Testing.Platform" Version="$MicrosoftTestingPlatformVersion$" />
        <PackageReference Include="Microsoft.Testing.Extensions.TrxReport.Abstractions" Version="$MicrosoftTestingPlatformVersion$" />
        <PackageReference Include="Microsoft.Testing.Internal.Framework" Version="$MicrosoftTestingInternalFrameworkVersion$" />
        <PackageReference Include="Microsoft.Testing.Internal.Framework.SourceGeneration" Version="$MicrosoftTestingInternalFrameworkVersion$" />
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
global using Microsoft.Testing.Internal.Framework;
global using Microsoft.Testing.Extensions;
""";

        public string NoExtensionTargetAssetPath => GetAssetPath(NoExtensionAssetName);

        public string AllExtensionsTargetAssetPath => GetAssetPath(AllExtensionsAssetName);

        public override IEnumerable<(string ID, string Name, string Code)> GetAssetsToGenerate()
        {
            yield return (NoExtensionAssetName, NoExtensionAssetName,
                NoExtensionTestCode
                .PatchTargetFrameworks(TargetFrameworks.All)
                .PatchCodeWithReplace("$MicrosoftTestingPlatformVersion$", MicrosoftTestingPlatformVersion)
                .PatchCodeWithReplace("$MicrosoftTestingEnterpriseExtensionsVersion$", MicrosoftTestingEnterpriseExtensionsVersion)
                .PatchCodeWithReplace("$MicrosoftTestingInternalFrameworkVersion$", MicrosoftTestingInternalFrameworkVersion));
            yield return (AllExtensionsAssetName, AllExtensionsAssetName,
                AllExtensionsTestCode
                .PatchTargetFrameworks(TargetFrameworks.All)
                .PatchCodeWithReplace("$MicrosoftTestingPlatformVersion$", MicrosoftTestingPlatformVersion)
                .PatchCodeWithReplace("$MicrosoftTestingEnterpriseExtensionsVersion$", MicrosoftTestingEnterpriseExtensionsVersion)
                .PatchCodeWithReplace("$MicrosoftTestingInternalFrameworkVersion$", MicrosoftTestingInternalFrameworkVersion));
        }
    }
}
