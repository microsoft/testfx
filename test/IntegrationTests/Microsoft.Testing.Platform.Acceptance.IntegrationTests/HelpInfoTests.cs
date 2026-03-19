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

        testHostResult.AssertExitCodeIs(ExitCodes.Success);

        const string wildcardMatchPattern = $"""
Microsoft.Testing.Platform v*
Usage {TestAssetFixture.NoExtensionAssetName}* [option providers] [extension option providers]
Execute a .NET Test Application.
Options:
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
""";

        testHostResult.AssertOutputMatchesLines(wildcardMatchPattern);
    }

    [DynamicData(nameof(TargetFrameworks.AllForDynamicData), typeof(TargetFrameworks))]
    [TestMethod]
    public async Task HelpShortName_WhenNoExtensionRegistered_OutputDefaultHelpContent(string tfm)
    {
        var testHost = TestInfrastructure.TestHost.LocateFrom(AssetFixture.NoExtensionTargetAssetPath, TestAssetFixture.NoExtensionAssetName, tfm);
        TestHostResult testHostResult = await testHost.ExecuteAsync("--?", cancellationToken: TestContext.CancellationToken);

        testHostResult.AssertExitCodeIs(ExitCodes.Success);

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

        testHostResult.AssertExitCodeIs(ExitCodes.InvalidCommandLine);

        const string wildcardMatchPattern = $"""
Microsoft.Testing.Platform v*
Unknown option '--{UnknownOption}'
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

        testHostResult.AssertExitCodeIs(ExitCodes.Success);

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

        testHostResult.AssertExitCodeIs(ExitCodes.Success);

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

        testHostResult.AssertExitCodeIs(ExitCodes.Success);

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

        testHostResult.AssertExitCodeIs(ExitCodes.Success);

        // Verify that TestResults folder was not created
        Assert.IsFalse(Directory.Exists(testResultsPath), "TestResults folder should not be created for info command");
    }

    public sealed class TestAssetFixture() : TestAssetFixtureBase(AcceptanceFixture.NuGetGlobalPackagesFolder)
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

        public override (string ID, string Name, string Code) GetAssetsToGenerate()
        {
            return (NoExtensionAssetName, NoExtensionAssetName,
                NoExtensionTestCode
                .PatchTargetFrameworks(TargetFrameworks.All)
                .PatchCodeWithReplace("$MicrosoftTestingPlatformVersion$", MicrosoftTestingPlatformVersion));
        }
    }

    public TestContext TestContext { get; set; }
}
