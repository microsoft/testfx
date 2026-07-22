// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.Acceptance.IntegrationTests;

[TestClass]
public class HelpInfoTests : AcceptanceTestBase<HelpInfoTests.TestAssetFixture>
{
    [DynamicData(nameof(TargetFrameworks.AllForDynamicData), typeof(TargetFrameworks))]
    [TestMethod]
    public async Task Help_WhenNoExtensionRegistered_OutputDefaultHelpContent(string tfm)
    {
        var testHost = TestInfrastructure.TestHost.LocateFrom(AssetFixture.NoExtensionTargetAssetPath, TestAssetFixture.NoExtensionAssetName, tfm);
        TestHostResult testHostResult = await testHost.ExecuteAsync("--help", cancellationToken: TestContext.CancellationToken);

        testHostResult.AssertExitCodeIs(ExitCode.Success);

        const string wildcardMatchPattern = $"""
Microsoft.Testing.Platform v*
Usage {TestAssetFixture.NoExtensionAssetName}* [option providers] [extension option providers]
Execute a .NET Test Application.
Options:
    --ansi
        Control whether ANSI escape characters are emitted.
        Valid values are 'auto' (default), 'on' (also accepts 'true', 'enable', '1') or 'off' (also accepts 'false', 'disable', '0').
        'on' forces ANSI escape codes (including cursor movement) even when stdout is redirected; pair it with --progress off if you only want colors.
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
        [Deprecated, use '--progress off' instead] Disable reporting progress to screen.
    --output
        Output verbosity when reporting tests.
        Valid values are 'Normal', 'Detailed'. Default is 'Normal'.
    --progress
        Control whether progress is reported to screen.
        Valid values are 'auto' (default), 'on' (also accepts 'true', 'enable', '1') or 'off' (also accepts 'false', 'disable', '0').
        'auto' shows progress unless the terminal cannot update in place (for example with --no-ansi or in CI).
        This option takes precedence over the deprecated --no-progress flag.
    --results-directory
        The directory where the test results are going to be placed.
        If the specified directory doesn't exist, it's created.
        The default is TestResults in the directory that contains the test application.
    --show-slowest-tests
        Show the specified number of slowest tests (by reported execution duration) in the run summary. Expects a positive integer.
    --show-stderr
        Determines when to show captured error output of a test.
        Valid values are 'All', 'Failed', 'None'. Default is 'All' (or 'Failed' when an LLM/AI agent environment is detected).
    --show-stdout
        Determines when to show captured standard output of a test.
        Valid values are 'All', 'Failed', 'None'. Default is 'All' (or 'Failed' when an LLM/AI agent environment is detected).
    --timeout
        A global test execution timeout.
        Takes one argument as a time value with an explicit unit suffix. Accepted suffixes are 'ms'/'mil(s)'/'millisecond(s)', 's'/'sec(s)'/'second(s)', 'm'/'min(s)'/'minute(s)', 'h'/'hour(s)', and 'd'/'day(s)', e.g. '500ms', '5400s', '90m', '1.5h', '1d'.
    --zero-tests-policy
        Specifies how a run that executed no tests is treated.
        Valid values are 'allow-skipped' (the default) which counts skipped tests as run, so only a run where no test was found at all fails with exit code 8, and 'strict' which treats skipped tests as not run, so a run where every test was skipped (or no test was found) fails with exit code 8.
Extension options:
    No extension registered.
""";

        testHostResult.AssertOutputMatchesLines(wildcardMatchPattern);
    }

    [DynamicData(nameof(TargetFrameworks.AllForDynamicData), typeof(TargetFrameworks))]
    [TestMethod]
    public async Task HelpShortName_WhenNoExtensionRegistered_OutputDefaultHelpContent(string tfm)
    {
        var testHost = TestInfrastructure.TestHost.LocateFrom(AssetFixture.NoExtensionTargetAssetPath, TestAssetFixture.NoExtensionAssetName, tfm);
        TestHostResult testHostResult = await testHost.ExecuteAsync("--?", cancellationToken: TestContext.CancellationToken);

        testHostResult.AssertExitCodeIs(ExitCode.Success);

        const string wildcardMatchPattern = $"""
Microsoft.Testing.Platform v*
Usage {TestAssetFixture.NoExtensionAssetName}* [option providers] [extension option providers]
Execute a .NET Test Application.
Options:
""";

        testHostResult.AssertOutputMatchesLines(wildcardMatchPattern);
    }

    [DynamicData(nameof(TargetFrameworks.AllForDynamicData), typeof(TargetFrameworks))]
    [TestMethod]
    public async Task Help_WhenNoExtensionRegisteredAndUnknownOptionIsSpecified_OutputDefaultHelpContentAndUnknownOption(string tfm)
    {
        const string UnknownOption = "aaa";

        var testHost = TestInfrastructure.TestHost.LocateFrom(AssetFixture.NoExtensionTargetAssetPath, TestAssetFixture.NoExtensionAssetName, tfm);
        TestHostResult testHostResult = await testHost.ExecuteAsync($"-{UnknownOption}", cancellationToken: TestContext.CancellationToken);

        testHostResult.AssertExitCodeIs(ExitCode.InvalidCommandLine);

        const string wildcardMatchPattern = $"""
Microsoft.Testing.Platform v*
Unknown option '--{UnknownOption}'
Command line: --no-ansi --progress off -{UnknownOption}
Usage {TestAssetFixture.NoExtensionAssetName}* [option providers] [extension option providers]
Execute a .NET Test Application.
Options:
""";

        testHostResult.AssertOutputMatchesLines(wildcardMatchPattern);
    }

    [DynamicData(nameof(TargetFrameworks.AllForDynamicData), typeof(TargetFrameworks))]
    [TestMethod]
    public async Task Info_WhenNoExtensionRegistered_OutputDefaultInfoContent(string tfm)
    {
        var testHost = TestInfrastructure.TestHost.LocateFrom(AssetFixture.NoExtensionTargetAssetPath, TestAssetFixture.NoExtensionAssetName, tfm);
        TestHostResult testHostResult = await testHost.ExecuteAsync("--info", cancellationToken: TestContext.CancellationToken);

        testHostResult.AssertExitCodeIs(ExitCode.Success);

        string regexMatchPattern = $"""
Microsoft.Testing.Platform v.+ \[.+\]
Microsoft Testing Platform:
  Version: .+
  Dynamic Code Supported: True
  Runtime information: .+
  {(tfm != TargetFrameworks.NetFramework[0] ? "###SKIP###" : "Runtime location: .+")}
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
      --debug
        Arity: 0
        Hidden: False
        Description: Allows to pause execution in order to attach to the process for debug purposes.
      --diagnostic
        Arity: 0
        Hidden: False
        Description: Enable the diagnostic logging\. The default log level is 'Trace'\.
        The file will be written in the output directory with the name log_\[yyMMddHHmmssfff\]\.diag
      --diagnostic-file-prefix
        Arity: 1
        Hidden: False
        Description: Prefix for the log file name that will replace '\[log\]_\.'
      --diagnostic-output-directory
        Arity: 1
        Hidden: False
        Description: Output directory of the diagnostic logging.
        If not specified the file will be generated inside the default 'TestResults' directory\.
      --diagnostic-synchronous-write
        Arity: 0
        Hidden: False
        Description: Force the built-in file logger to write the log synchronously\.
        Useful for scenario where you don't want to lose any log \(i\.e\. in case of crash\)\.
        Note that this is slowing down the test execution\.
      --diagnostic-verbosity
        Arity: 1
        Hidden: False
        Description: Define the level of the verbosity for the --diagnostic\.
        The available values are 'Trace', 'Debug', 'Information', 'Warning', 'Error', and 'Critical'
      --dotnet-test-pipe
        Arity: 1
        Hidden: True
        Description: dotnet test pipe\.
      --dotnet-test-transport
        Arity: 1
        Hidden: True
        Description: Pre-launch transport used to carry the 'dotnet test' pipe protocol\. The available values are 'pipe' and 'websocket'\. Defaults to 'pipe' \(implied whenever only '--dotnet-test-pipe' is specified\)\.
      --dotnet-test-websocket-endpoint
        Arity: 1
        Hidden: True
        Description: The WebSocket endpoint URI the test host connects to when '--dotnet-test-transport websocket' is selected\.
      --dotnet-test-websocket-token
        Arity: 1
        Hidden: True
        Description: The per-run authentication token the test host presents when connecting via '--dotnet-test-transport websocket'\.
      --exit-on-process-exit
        Arity: 1
        Hidden: False
        Description: Exit the test process if dependent process exits\. PID must be provided\.
      --filter-uid
        Arity: 1\.\.N
        Hidden: False
        Description: Provides a list of test node UIDs to filter by\.
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
      --list-tests
        Arity: 0..1
        Hidden: False
        Description: List available tests\.
        Optionally accepts 'text' \(the default human-readable output\) or 'json' to print the discovered tests as a JSON document on standard output\.
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
        Takes one argument as a time value with an explicit unit suffix\. Accepted suffixes are 'ms'/'mil\(s\)'/'millisecond\(s\)', 's'/'sec\(s\)'/'second\(s\)', 'm'/'min\(s\)'/'minute\(s\)', 'h'/'hour\(s\)', and 'd'/'day\(s\)', e\.g\. '500ms', '5400s', '90m', '1\.5h', '1d'\.
      --zero-tests-policy
        Arity: 1
        Hidden: False
        Description: Specifies how a run that executed no tests is treated.
        Valid values are 'allow-skipped' \(the default\) which counts skipped tests as run, so only a run where no test was found at all fails with exit code 8, and 'strict' which treats skipped tests as not run, so a run where every test was skipped \(or no test was found\) fails with exit code 8\.
  TerminalTestReporterCommandLineOptionsProvider
    Name: Terminal test reporter
    Version: .+
    Description: Writes test results to terminal.
    Options:
      --ansi
        Arity: 1
        Hidden: False
        Description: Control whether ANSI escape characters are emitted\.
        Valid values are 'auto' \(default\), 'on' \(also accepts 'true', 'enable', '1'\) or 'off' \(also accepts 'false', 'disable', '0'\)\.
        'on' forces ANSI escape codes \(including cursor movement\) even when stdout is redirected; pair it with --progress off if you only want colors\.
        When both --ansi and --no-ansi are provided, --ansi wins\.
      --no-ansi
        Arity: 0
        Hidden: False
        Description: Disable outputting ANSI escape characters to screen.
      --no-progress
        Arity: 0
        Hidden: False
        Description: \[Deprecated, use '--progress off' instead\] Disable reporting progress to screen.
      --output
        Arity: 1
        Hidden: False
        Description: Output verbosity when reporting tests.
        Valid values are 'Normal', 'Detailed'. Default is 'Normal'.
      --progress
        Arity: 1
        Hidden: False
        Description: Control whether progress is reported to screen.
        Valid values are 'auto' \(default\), 'on' \(also accepts 'true', 'enable', '1'\) or 'off' \(also accepts 'false', 'disable', '0'\).
        'auto' shows progress unless the terminal cannot update in place \(for example with --no-ansi or in CI\).
        This option takes precedence over the deprecated --no-progress flag.
      --show-slowest-tests
        Arity: 1
        Hidden: False
        Description: Show the specified number of slowest tests \(by reported execution duration\) in the run summary. Expects a positive integer.
      --show-stderr
        Arity: 1
        Hidden: False
        Description: Determines when to show captured error output of a test.
        Valid values are 'All', 'Failed', 'None'. Default is 'All' \(or 'Failed' when an LLM/AI agent environment is detected\).
      --show-stdout
        Arity: 1
        Hidden: False
        Description: Determines when to show captured standard output of a test.
        Valid values are 'All', 'Failed', 'None'. Default is 'All' \(or 'Failed' when an LLM/AI agent environment is detected\).
Registered command line providers:
  There are no registered command line providers.
Registered tools:
  There are no registered tools\.
""";

        testHostResult.AssertOutputMatchesRegexLines(regexMatchPattern);
    }

    [DynamicData(nameof(TargetFrameworks.AllForDynamicData), typeof(TargetFrameworks))]
    [TestMethod]
    public async Task Help_DoesNotCreateTestResultsFolder(string tfm)
    {
        var testHost = TestInfrastructure.TestHost.LocateFrom(AssetFixture.NoExtensionTargetAssetPath, TestAssetFixture.NoExtensionAssetName, tfm);
        string testHostDirectory = testHost.DirectoryName;
        string testResultsPath = Path.Combine(testHostDirectory, "TestResults");

        // Ensure TestResults folder doesn't exist before running the test
        if (Directory.Exists(testResultsPath))
        {
            Directory.Delete(testResultsPath, recursive: true);
        }

        TestHostResult testHostResult = await testHost.ExecuteAsync("--help", cancellationToken: TestContext.CancellationToken);

        testHostResult.AssertExitCodeIs(ExitCode.Success);

        // Verify that TestResults folder was not created
        Assert.IsFalse(Directory.Exists(testResultsPath), "TestResults folder should not be created for help command");
    }

    [DynamicData(nameof(TargetFrameworks.AllForDynamicData), typeof(TargetFrameworks))]
    [TestMethod]
    public async Task HelpShortName_DoesNotCreateTestResultsFolder(string tfm)
    {
        var testHost = TestInfrastructure.TestHost.LocateFrom(AssetFixture.NoExtensionTargetAssetPath, TestAssetFixture.NoExtensionAssetName, tfm);
        string testHostDirectory = testHost.DirectoryName;
        string testResultsPath = Path.Combine(testHostDirectory, "TestResults");

        // Ensure TestResults folder doesn't exist before running the test
        if (Directory.Exists(testResultsPath))
        {
            Directory.Delete(testResultsPath, recursive: true);
        }

        TestHostResult testHostResult = await testHost.ExecuteAsync("--?", cancellationToken: TestContext.CancellationToken);

        testHostResult.AssertExitCodeIs(ExitCode.Success);

        // Verify that TestResults folder was not created
        Assert.IsFalse(Directory.Exists(testResultsPath), "TestResults folder should not be created for help short name command");
    }

    [DynamicData(nameof(TargetFrameworks.AllForDynamicData), typeof(TargetFrameworks))]
    [TestMethod]
    public async Task Info_DoesNotCreateTestResultsFolder(string tfm)
    {
        var testHost = TestInfrastructure.TestHost.LocateFrom(AssetFixture.NoExtensionTargetAssetPath, TestAssetFixture.NoExtensionAssetName, tfm);
        string testHostDirectory = testHost.DirectoryName;
        string testResultsPath = Path.Combine(testHostDirectory, "TestResults");

        // Ensure TestResults folder doesn't exist before running the test
        if (Directory.Exists(testResultsPath))
        {
            Directory.Delete(testResultsPath, recursive: true);
        }

        TestHostResult testHostResult = await testHost.ExecuteAsync("--info", cancellationToken: TestContext.CancellationToken);

        testHostResult.AssertExitCodeIs(ExitCode.Success);

        // Verify that TestResults folder was not created
        Assert.IsFalse(Directory.Exists(testResultsPath), "TestResults folder should not be created for info command");
    }

    public sealed class TestAssetFixture() : TestAssetFixtureBase()
    {
        public const string NoExtensionAssetName = "NoExtensionInfoTest";

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
    </ItemGroup>
</Project>

#file Program.cs
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

        public string NoExtensionTargetAssetPath => GetAssetPath(NoExtensionAssetName);

        public override (string ID, string Name, string Code) GetAssetsToGenerate() => (NoExtensionAssetName, NoExtensionAssetName,
                NoExtensionTestCode
                .PatchTargetFrameworks(TargetFrameworks.All)
                .PatchCodeWithReplace("$MicrosoftTestingPlatformVersion$", MicrosoftTestingPlatformVersion));
    }

    public TestContext TestContext { get; set; }
}
