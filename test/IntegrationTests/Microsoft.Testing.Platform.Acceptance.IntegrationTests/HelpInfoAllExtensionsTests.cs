// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.Acceptance.IntegrationTests;

[TestClass]
public sealed class HelpInfoAllExtensionsTests : AcceptanceTestBase<HelpInfoAllExtensionsTests.TestAssetFixture>
{
    [DynamicData(nameof(TargetFrameworks.AllForDynamicData), typeof(TargetFrameworks))]
    [TestMethod]
    public async Task Help_WithAllExtensionsRegistered_OutputFullHelpContent(string tfm)
    {
        var testHost = TestInfrastructure.TestHost.LocateFrom(AssetFixture.AllExtensionsTargetAssetPath, TestAssetFixture.AllExtensionsAssetName, tfm);
        TestHostResult testHostResult = await testHost.ExecuteAsync("--help", cancellationToken: TestContext.CancellationToken);

        testHostResult.AssertExitCodeIs(ExitCode.Success);

        string wildcardPattern = $$"""
Microsoft.Testing.Platform v*
Usage {{TestAssetFixture.AllExtensionsAssetName}}* [option providers] [extension option providers]
Execute a .NET Test Application.
Options:
    --ansi
        Control whether ANSI escape characters are emitted.
        Valid values are 'auto' (default), 'on' (also accepts 'true', 'enable', '1') or 'off' (also accepts 'false', 'disable', '0').
        'on' forces ANSI escape codes (including cursor movement) even when stdout is redirected; pair it with --no-progress if you only want colors.
        When both --ansi and --no-ansi are provided, --ansi wins.
    --config-file
        Specifies a testconfig.json file.
    --debug
        Allows to pause execution in order to attach to the process for debug purposes.
    --diagnostic
        Enable the diagnostic logging. The default log level is 'Trace'.
        The file will be written in the output directory with the name log_[yyMMddHHmmssfff].diag
    --diagnostic-file-prefix
        Prefix for the log file name that will replace '[log]_.'
    --diagnostic-output-directory
        Output directory of the diagnostic logging.
        If not specified the file will be generated inside the default 'TestResults' directory.
    --diagnostic-synchronous-write
        Force the built-in file logger to write the log synchronously.
        Useful for scenario where you don't want to lose any log (i.e. in case of crash).
        Note that this is slowing down the test execution.
    --diagnostic-verbosity
        Define the level of the verbosity for the --diagnostic.
        The available values are 'Trace', 'Debug', 'Information', 'Warning', 'Error', and 'Critical'.
    --exit-on-process-exit
        Exit the test process if dependent process exits. PID must be provided.
    --filter-uid
        Provides a list of test node UIDs to filter by.
    --help
        Show the command line help.
    --ignore-exit-code
        Do not report non successful exit value for specific exit codes
        (e.g. '--ignore-exit-code 8;9' ignore exit code 8 and 9 and will return 0 in these case)
    --info
        Display .NET test application information.
    --list-tests
        List available tests.
        Optionally accepts 'text' (the default human-readable output) or 'json' to print the discovered tests as a JSON document on standard output.
    --minimum-expected-tests
        Specifies the minimum number of tests that are expected to run.
    --no-ansi
        Disable outputting ANSI escape characters to screen.
    --no-progress
        Disable reporting progress to screen.
    --output
        Output verbosity when reporting tests.
        Valid values are 'Normal', 'Detailed'. Default is 'Normal'.
    --results-directory
        The directory where the test results are going to be placed.
        If the specified directory doesn't exist, it's created.
        The default is TestResults in the directory that contains the test application.
    --show-stderr
        Determines when to show captured error output of a test.
        Valid values are 'All', 'Failed', 'None'. Default is 'All' (or 'Failed' when an LLM/AI agent environment is detected).
    --show-stdout
        Determines when to show captured standard output of a test.
        Valid values are 'All', 'Failed', 'None'. Default is 'All' (or 'Failed' when an LLM/AI agent environment is detected).
    --timeout
        A global test execution timeout.
        Takes one argument as a time value with an explicit unit suffix. Accepted suffixes are 'ms'/'mil(s)'/'millisecond(s)', 's'/'sec(s)'/'second(s)', 'm'/'min(s)'/'minute(s)', 'h'/'hour(s)', and 'd'/'day(s)', e.g. '500ms', '5400s', '90m', '1.5h', '1d'.
Extension options:
    --crash-report
        [Linux/macOS only] Generate a JSON crash report when the test process crashes. Combine with '--crashdump' to also generate a dump file. Requires .NET 7+ when used alone; .NET 6+ when combined with '--crashdump'. This runtime requirement is not enforced by the tool: on unsupported runtimes no crash report will be emitted. Not supported on Windows due to a .NET runtime limitation (dotnet/runtime#80191); use '--crash-report-if-supported' to silently skip the option there.
    --crash-report-if-supported
        Same as '--crash-report' but silently ignored (with an informational message) on platforms where crash report generation is not supported. Use this option to keep the same command line across CI matrices that include Windows. Mutually exclusive with '--crash-report'.
    --crash-sequence
        Control whether a sequence file listing the tests started and ended during the test session is generated alongside the crash dump or crash report.
        The file makes it possible to identify the tests that were running at the time of the crash without having to inspect the dump.
        Valid values are 'on' (default; also accepts 'true', 'enable', '1') or 'off' (also accepts 'false', 'disable', '0').
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
        Specify the name of the dump file.
        Supports the following placeholders: {pname} (test application name), {pid} (process ID), {asm} (entry assembly name), {tfm} (target framework moniker), {time} (timestamp). The legacy %p token (process ID) is also supported for backward compatibility.
    --hangdump-timeout
        Specify the timeout after which the dump will be generated.
        The timeout value is specified in one of the following formats:
            500ms, 500mil, 500millisecond, 500milliseconds,
            5400s, 5400sec, 5400second, 5400seconds,
            90m, 90min, 90minute, 90minutes,
            1.5h, 1.5hour, 1.5hours,
            1d, 1day, 1days.
            A bare number (with no suffix) is interpreted as milliseconds.
            Default is 30m.
    --hangdump-type
        Specify the type of the dump.
        Valid values are {{GetExpectedHangDumpDescriptionOptions(tfm)}}.
        Default type is 'Full'
    --hangdump-type-if-supported
        Same as '--hangdump-type' but silently falls back (with an informational message) to the closest supported dump type when the requested type is not available on the current runtime (e.g. 'Triage' is only supported on .NET Core and falls back to 'Mini' on .NET Framework). Use this option to keep the same command line across CI matrices that mix .NET Framework and .NET. Valid values are 'Mini', 'Heap', 'Full', 'Triage', 'None'. Mutually exclusive with '--hangdump-type'.
    --publish-azdo-run-name
        Custom Azure DevOps test run name for live test-result publishing.
    --publish-azdo-test-results
        Publish test results live to the Azure DevOps Tests tab.
    --report-azdo
        Enable Azure DevOps report generator to write errors to the output in a way that Azure DevOps understands.
    --report-azdo-demote-known-flaky
        Demote failures with an Azure DevOps flaky history of at least 25% in the selected window to warnings.
    --report-azdo-flaky-history
        Query Azure DevOps test result history for the past N days (1-90) and annotate reported failures with flakiness context.
    --report-azdo-quarantine-file
        Path to a text file that lists quarantined test fully qualified names or glob patterns. Matching failures are reported as warnings.
    --report-azdo-severity
        Severity to use for the reported event. Options are: error (default) and warning.
    --report-azdo-upload-artifact-exclude
        Exclude files from Azure DevOps artifact upload using glob patterns relative to the test results directory.
    --report-azdo-upload-artifact-include
        Include files in Azure DevOps artifact upload using glob patterns relative to the test results directory. Defaults to '**/*'.
    --report-azdo-upload-artifact-name
        Override the Azure DevOps artifact container name. Defaults to 'TestResults_{assemblyName}_{tfm}'.
    --report-azdo-upload-artifacts
        Upload test result files and/or add build tags to Azure DevOps. Options are: off (default), tags-only, files, and all.
    --report-html
        Enable generating an HTML report
    --report-html-filename
        The name of the generated HTML report. May include a relative or absolute path; relative paths are resolved against the test results directory and missing directories are created.
        Supports the following placeholders: {pname} (test application name), {pid} (process ID), {asm} (entry assembly name), {tfm} (target framework moniker), {time} (timestamp).
        Example: MyReport_{tfm}.html
    --report-trx
        Enable generating TRX report
    --report-trx-filename
        The name of the generated TRX report. May include a relative or absolute path; relative paths are resolved against the test results directory and missing directories are created.
        Supports the following placeholders: {pname} (test application name), {pid} (process ID), {asm} (entry assembly name), {tfm} (target framework moniker), {time} (timestamp).
        Example: MyReport_{tfm}.trx
    --retry-failed-tests
        Retry failed tests the given number of times
    --retry-failed-tests-delay
        Add a delay between retries. The delay is expressed as a time value, e.g. 200, 500ms, 1s, 2.5m, 1h, 1d. Default unit is milliseconds.
    --retry-failed-tests-max-percentage
        Disable retry mechanism if the percentage of failed tests is greater than the specified value
    --retry-failed-tests-max-tests
        Disable retry mechanism if the number of failed tests is greater than the specified value
""";

        testHostResult.AssertOutputMatchesLines(wildcardPattern);
    }

    [DynamicData(nameof(TargetFrameworks.AllForDynamicData), typeof(TargetFrameworks))]
    [TestMethod]
    public async Task HelpShortName_WithAllExtensionsRegistered_OutputFullHelpContent(string tfm)
    {
        var testHost = TestInfrastructure.TestHost.LocateFrom(AssetFixture.AllExtensionsTargetAssetPath, TestAssetFixture.AllExtensionsAssetName, tfm);
        TestHostResult testHostResult = await testHost.ExecuteAsync("-?", cancellationToken: TestContext.CancellationToken);

        testHostResult.AssertExitCodeIs(ExitCode.Success);

        string wildcardPattern = $$"""
Microsoft.Testing.Platform v*
Usage {{TestAssetFixture.AllExtensionsAssetName}}* [option providers] [extension option providers]
Execute a .NET Test Application.
Options:
""";

        testHostResult.AssertOutputMatchesLines(wildcardPattern);
    }

    [DynamicData(nameof(TargetFrameworks.AllForDynamicData), typeof(TargetFrameworks))]
    [TestMethod]
    public async Task Info_WithAllExtensionsRegistered_OutputFullInfoContent(string tfm)
    {
        var testHost = TestInfrastructure.TestHost.LocateFrom(AssetFixture.AllExtensionsTargetAssetPath, TestAssetFixture.AllExtensionsAssetName, tfm);
        TestHostResult testHostResult = await testHost.ExecuteAsync("--info", cancellationToken: TestContext.CancellationToken);

        testHostResult.AssertExitCodeIs(ExitCode.Success);

        string wildcardPattern = $$"""
Microsoft.Testing.Platform v* [*]
Microsoft Testing Platform:
  Version: *
  Dynamic Code Supported: True
  Runtime information: *
  {{(tfm != TargetFrameworks.NetFramework[0] ? "###SKIP###" : "Runtime location: *")}}
  Test module: *{{TestAssetFixture.AllExtensionsAssetName}}*
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
      --debug
        Arity: 0
        Hidden: False
        Description: Allows to pause execution in order to attach to the process for debug purposes.
      --diagnostic
        Arity: 0
        Hidden: False
        Description: Enable the diagnostic logging. The default log level is 'Trace'.
        The file will be written in the output directory with the name log_[yyMMddHHmmssfff].diag
      --diagnostic-file-prefix
        Arity: 1
        Hidden: False
        Description: Prefix for the log file name that will replace '[log]_.'
      --diagnostic-output-directory
        Arity: 1
        Hidden: False
        Description: Output directory of the diagnostic logging.
        If not specified the file will be generated inside the default 'TestResults' directory.
      --diagnostic-synchronous-write
        Arity: 0
        Hidden: False
        Description: Force the built-in file logger to write the log synchronously.
        Useful for scenario where you don't want to lose any log (i.e. in case of crash).
        Note that this is slowing down the test execution.
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
      --filter-uid
        Arity: 1..N
        Hidden: False
        Description: Provides a list of test node UIDs to filter by.
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
      --list-tests
        Arity: 0..1
        Hidden: False
        Description: List available tests.
        Optionally accepts 'text' (the default human-readable output) or 'json' to print the discovered tests as a JSON document on standard output.
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
        Takes one argument as a time value with an explicit unit suffix. Accepted suffixes are 'ms'/'mil(s)'/'millisecond(s)', 's'/'sec(s)'/'second(s)', 'm'/'min(s)'/'minute(s)', 'h'/'hour(s)', and 'd'/'day(s)', e.g. '500ms', '5400s', '90m', '1.5h', '1d'.
  TerminalTestReporterCommandLineOptionsProvider
    Name: Terminal test reporter
    Version: *
    Description: Writes test results to terminal.
    Options:
      --ansi
        Arity: 1
        Hidden: False
        Description: Control whether ANSI escape characters are emitted.
        Valid values are 'auto' (default), 'on' (also accepts 'true', 'enable', '1') or 'off' (also accepts 'false', 'disable', '0').
        'on' forces ANSI escape codes (including cursor movement) even when stdout is redirected; pair it with --no-progress if you only want colors.
        When both --ansi and --no-ansi are provided, --ansi wins.
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
      --show-stderr
        Arity: 1
        Hidden: False
        Description: Determines when to show captured error output of a test.
        Valid values are 'All', 'Failed', 'None'. Default is 'All' (or 'Failed' when an LLM/AI agent environment is detected).
      --show-stdout
        Arity: 1
        Hidden: False
        Description: Determines when to show captured standard output of a test.
        Valid values are 'All', 'Failed', 'None'. Default is 'All' (or 'Failed' when an LLM/AI agent environment is detected).
Registered command line providers:
  AzureDevOpsCommandLineProvider
    Name: Azure DevOps report generator
    Version: *
    Description: Azure DevOps report generator to write errors to the output in a way that Azure DevOps understands.
    Options:
      --publish-azdo-run-name
        Arity: 1
        Hidden: False
        Description: Custom Azure DevOps test run name for live test-result publishing.
      --publish-azdo-test-results
        Arity: 0
        Hidden: False
        Description: Publish test results live to the Azure DevOps Tests tab.
      --report-azdo
        Arity: 0
        Hidden: False
        Description: Enable Azure DevOps report generator to write errors to the output in a way that Azure DevOps understands.
      --report-azdo-demote-known-flaky
        Arity: 0
        Hidden: False
        Description: Demote failures with an Azure DevOps flaky history of at least 25% in the selected window to warnings.
      --report-azdo-flaky-history
        Arity: 1
        Hidden: False
        Description: Query Azure DevOps test result history for the past N days (1-90) and annotate reported failures with flakiness context.
      --report-azdo-quarantine-file
        Arity: 1
        Hidden: False
        Description: Path to a text file that lists quarantined test fully qualified names or glob patterns. Matching failures are reported as warnings.
      --report-azdo-severity
        Arity: 1
        Hidden: False
        Description: Severity to use for the reported event. Options are: error (default) and warning.
      --report-azdo-upload-artifact-exclude
        Arity: 0..N
        Hidden: False
        Description: Exclude files from Azure DevOps artifact upload using glob patterns relative to the test results directory.
      --report-azdo-upload-artifact-include
        Arity: 0..N
        Hidden: False
        Description: Include files in Azure DevOps artifact upload using glob patterns relative to the test results directory. Defaults to '**/*'.
      --report-azdo-upload-artifact-name
        Arity: 1
        Hidden: False
        Description: Override the Azure DevOps artifact container name. Defaults to 'TestResults_{assemblyName}_{tfm}'.
      --report-azdo-upload-artifacts
        Arity: 1
        Hidden: False
        Description: Upload test result files and/or add build tags to Azure DevOps. Options are: off (default), tags-only, files, and all.
  CrashDumpCommandLineProvider
    Name: Crash dump
    Version: *
    Description: [net6.0+ only] Produce crash dump files when the test execution process crashes unexpectedly
    Options:
      --crash-report
        Arity: 0
        Hidden: False
        Description: [Linux/macOS only] Generate a JSON crash report when the test process crashes. Combine with '--crashdump' to also generate a dump file. Requires .NET 7+ when used alone; .NET 6+ when combined with '--crashdump'. This runtime requirement is not enforced by the tool: on unsupported runtimes no crash report will be emitted. Not supported on Windows due to a .NET runtime limitation (dotnet/runtime#80191); use '--crash-report-if-supported' to silently skip the option there.
      --crash-report-if-supported
        Arity: 0
        Hidden: False
        Description: Same as '--crash-report' but silently ignored (with an informational message) on platforms where crash report generation is not supported. Use this option to keep the same command line across CI matrices that include Windows. Mutually exclusive with '--crash-report'.
      --crash-sequence
        Arity: 1
        Hidden: False
        Description: Control whether a sequence file listing the tests started and ended during the test session is generated alongside the crash dump or crash report.
        The file makes it possible to identify the tests that were running at the time of the crash without having to inspect the dump.
        Valid values are 'on' (default; also accepts 'true', 'enable', '1') or 'off' (also accepts 'false', 'disable', '0').
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
        Description: Specify the name of the dump file.
        Supports the following placeholders: {pname} (test application name), {pid} (process ID), {asm} (entry assembly name), {tfm} (target framework moniker), {time} (timestamp). The legacy %p token (process ID) is also supported for backward compatibility.
      --hangdump-timeout
        Arity: 1
        Hidden: False
        Description: Specify the timeout after which the dump will be generated.
        The timeout value is specified in one of the following formats:
            500ms, 500mil, 500millisecond, 500milliseconds,
            5400s, 5400sec, 5400second, 5400seconds,
            90m, 90min, 90minute, 90minutes,
            1.5h, 1.5hour, 1.5hours,
            1d, 1day, 1days.
            A bare number (with no suffix) is interpreted as milliseconds.
            Default is 30m.
      --hangdump-type
        Arity: 1
        Hidden: False
        Description: Specify the type of the dump.
        Valid values are {{GetExpectedHangDumpDescriptionOptions(tfm)}}.
        Default type is 'Full'
      --hangdump-type-if-supported
        Arity: 1
        Hidden: False
        Description: Same as '--hangdump-type' but silently falls back (with an informational message) to the closest supported dump type when the requested type is not available on the current runtime (e.g. 'Triage' is only supported on .NET Core and falls back to 'Mini' on .NET Framework). Use this option to keep the same command line across CI matrices that mix .NET Framework and .NET. Valid values are 'Mini', 'Heap', 'Full', 'Triage', 'None'. Mutually exclusive with '--hangdump-type'.
  HtmlReportGeneratorCommandLine
    Name: HTML report generator
    Version: *
    Description: Produce a self-contained HTML report for the current test session
    Options:
      --report-html
        Arity: 0
        Hidden: False
        Description: Enable generating an HTML report
      --report-html-filename
        Arity: 1
        Hidden: False
        Description: The name of the generated HTML report. May include a relative or absolute path; relative paths are resolved against the test results directory and missing directories are created.
        Supports the following placeholders: {pname} (test application name), {pid} (process ID), {asm} (entry assembly name), {tfm} (target framework moniker), {time} (timestamp).
        Example: MyReport_{tfm}.html
  MSBuildCommandLineProvider
    Name: MSBuildCommandLineProvider
    Version: *
    Description: Extension used to pass parameters from MSBuild node and the hosts
    Options:
      --internal-msbuild-node
        Arity: 1
        Hidden: True
        Description: Used to pass the MSBuild node handle
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
        Description: Retry failed tests the given number of times
      --retry-failed-tests-delay
        Arity: 1
        Hidden: False
        Description: Add a delay between retries. The delay is expressed as a time value, e.g. 200, 500ms, 1s, 2.5m, 1h, 1d. Default unit is milliseconds.
      --retry-failed-tests-max-percentage
        Arity: 1
        Hidden: False
        Description: Disable retry mechanism if the percentage of failed tests is greater than the specified value
      --retry-failed-tests-max-tests
        Arity: 1
        Hidden: False
        Description: Disable retry mechanism if the number of failed tests is greater than the specified value
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
        Description: The name of the generated TRX report. May include a relative or absolute path; relative paths are resolved against the test results directory and missing directories are created.
        Supports the following placeholders: {pname} (test application name), {pid} (process ID), {asm} (entry assembly name), {tfm} (target framework moniker), {time} (timestamp).
        Example: MyReport_{tfm}.trx
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

    private static string GetExpectedHangDumpDescriptionOptions(string tfm)
        => TargetFrameworks.NetFramework.Contains(tfm)
            ? "'Mini', 'Heap', 'Full', 'None'"
            : "'Mini', 'Heap', 'Full', 'Triage', 'None'";

    public sealed class TestAssetFixture() : TestAssetFixtureBase()
    {
        public const string AllExtensionsAssetName = "AllExtensionsInfoTest";

        private const string AllExtensionsTestCode = """
#file AllExtensionsInfoTest.csproj
<Project Sdk="Microsoft.NET.Sdk">
    <PropertyGroup>
        <TargetFrameworks>$TargetFrameworks$</TargetFrameworks>
        <ImplicitUsings>enable</ImplicitUsings>
        <Nullable>enable</Nullable>
        <OutputType>Exe</OutputType>
        <LangVersion>preview</LangVersion>
        <GenerateTestingPlatformEntryPoint>false</GenerateTestingPlatformEntryPoint>
    </PropertyGroup>
    <ItemGroup>
        <PackageReference Include="Microsoft.Testing.Platform.MSBuild" Version="$MicrosoftTestingPlatformVersion$" />
        <PackageReference Include="Microsoft.Testing.Extensions.AzureDevOpsReport" Version="$MicrosoftTestingPlatformVersion$" />
        <PackageReference Include="Microsoft.Testing.Extensions.CrashDump" Version="$MicrosoftTestingPlatformVersion$" />
        <PackageReference Include="Microsoft.Testing.Extensions.HangDump" Version="$MicrosoftTestingPlatformVersion$" />
        <PackageReference Include="Microsoft.Testing.Extensions.HotReload" Version="$MicrosoftTestingPlatformVersion$" />
        <PackageReference Include="Microsoft.Testing.Extensions.HtmlReport" Version="$MicrosoftTestingPlatformVersion$" />
        <PackageReference Include="Microsoft.Testing.Extensions.Retry" Version="$MicrosoftTestingPlatformVersion$" />
        <PackageReference Include="Microsoft.Testing.Extensions.TrxReport" Version="$MicrosoftTestingPlatformVersion$" />
    </ItemGroup>
</Project>

#file Program.cs
using AllExtensionsInfoTest;
using Microsoft.Testing.Platform.Builder;
using Microsoft.Testing.Platform.Capabilities.TestFramework;
using Microsoft.Testing.Platform.Extensions.TestFramework;
using Microsoft.Testing.Platform.Services;

public class Program
{
    public static async Task<int> Main(string[] args)
    {
        ITestApplicationBuilder builder = await TestApplication.CreateBuilderAsync(args);
        builder.RegisterTestFramework(
            sp => new TestFrameworkCapabilities(),
            (_,__) => new DummyTestFramework());
        builder.AddSelfRegisteredExtensions(args);
        using ITestApplication app = await builder.BuildAsync();
        return await app.RunAsync();
    }
}

public class DummyTestFramework : ITestFramework
{
    public string Uid => nameof(DummyTestFramework);

    public string Version => "2.0.0";

    public string DisplayName => nameof(DummyTestFramework);

    public string Description => nameof(DummyTestFramework);

    public Task<bool> IsEnabledAsync() => Task.FromResult(true);

    public Task<CreateTestSessionResult> CreateTestSessionAsync(CreateTestSessionContext context)
        => Task.FromResult(new CreateTestSessionResult() { IsSuccess = true });
    public Task<CloseTestSessionResult> CloseTestSessionAsync(CloseTestSessionContext context)
        => Task.FromResult(new CloseTestSessionResult() { IsSuccess = true });
    public Task ExecuteRequestAsync(ExecuteRequestContext context)
    {
       context.Complete();
       return Task.CompletedTask;
    }
}
""";

        public string AllExtensionsTargetAssetPath => GetAssetPath(AllExtensionsAssetName);

        public override (string ID, string Name, string Code) GetAssetsToGenerate() => (AllExtensionsAssetName, AllExtensionsAssetName,
                AllExtensionsTestCode
                .PatchTargetFrameworks(TargetFrameworks.All)
                .PatchCodeWithReplace("$MicrosoftTestingPlatformVersion$", MicrosoftTestingPlatformVersion));
    }

    public TestContext TestContext { get; set; }
}
