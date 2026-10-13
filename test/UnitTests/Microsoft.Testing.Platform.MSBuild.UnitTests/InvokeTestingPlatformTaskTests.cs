// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

extern alias PlatformMSBuild;

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Microsoft.Testing.Platform.Helpers;

using Moq;

using FailedTestInfoRequest = PlatformMSBuild::Microsoft.Testing.Extensions.MSBuild.Serializers.FailedTestInfoRequest;
using ModuleInfoRequest = PlatformMSBuild::Microsoft.Testing.Extensions.MSBuild.Serializers.ModuleInfoRequest;
using RunSummaryInfoRequest = PlatformMSBuild::Microsoft.Testing.Extensions.MSBuild.Serializers.RunSummaryInfoRequest;

namespace Microsoft.Testing.Platform.MSBuild.UnitTests;

[TestClass]
public sealed class InvokeTestingPlatformTaskTests
{
    [TestMethod]
    [DataRow(true, 0, true)]
    [DataRow(false, 0, false)]
    [DataRow(true, 2, false)]
    public void Execute_ReconcilesToolExitAndRequiredHandshakeWithoutLaunchingProcess(bool direct, int exitCode, bool expectedSuccess)
    {
        using AppHostTaskFixture fixture = new();
        StubFileSystem fileSystem = new();
        using TestableInvokeTestingPlatformTask task = CreateSimulatedTask(fixture, fileSystem);
        string target = task.TargetPath.ItemSpec;
        task.TestingPlatformUseDirectExecutable = direct;
        task.SimulatedExitCode = exitCode;
        List<BuildMessageEventArgs> messages = [];
        List<BuildErrorEventArgs> errors = [];
        var engine = new Mock<IBuildEngine>();
        engine.Setup(value => value.LogMessageEvent(It.IsAny<BuildMessageEventArgs>())).Callback<BuildMessageEventArgs>(messages.Add);
        engine.Setup(value => value.LogErrorEvent(It.IsAny<BuildErrorEventArgs>())).Callback<BuildErrorEventArgs>(errors.Add);
        task.BuildEngine = engine.Object;

        Assert.AreEqual(expectedSuccess, task.Execute());
        Assert.AreEqual(1, task.SimulatedLaunches);
        Assert.AreEqual(exitCode, task.ExitCode);
        Assert.Contains("=== COMMAND LINE ===", task.GetCapturedOutput());
        Assert.Contains("simulated command", task.GetCapturedOutput());
        if (expectedSuccess)
        {
            Assert.IsEmpty(errors);
            Assert.Contains(message => message.Importance == MessageImportance.High && message.Message!.Contains("apphost", StringComparison.Ordinal), messages);
        }
        else
        {
            BuildErrorEventArgs error = Assert.ContainsSingle(errors);
            if (exitCode == 0)
            {
                Assert.Contains("apphost", error.Message!);
            }
            else
            {
                Assert.AreEqual(target, error.File);
                Assert.AreEqual("run failed", error.Code);
                Assert.Contains("simulated output", error.Message!);
            }
        }
    }

    [TestMethod]
    [DataRow(0, 0, 0, 0, true, "Failed!")]
    [DataRow(0, 0, 0, 0, false, "Failed!")]
    [DataRow(3, 0, 0, 3, true, "Passed!")]
    [DataRow(3, 0, 0, 3, false, "Failed!")]
    [DataRow(4, 0, 1, 3, false, "Passed!")]
    [DataRow(4, 1, 3, 0, true, "Failed!")]
    public void Execute_RunSummaryPreservesZeroTestsPolicyAndAllCounters(
        int total, int failed, int passed, int skipped, bool allowSkipped, string verdict)
    {
        using AppHostTaskFixture fixture = new();
        StubFileSystem fileSystem = new();
        using TestableInvokeTestingPlatformTask task = CreateSimulatedTask(fixture, fileSystem);
        List<BuildMessageEventArgs> messages = [];
        var engine = new Mock<IBuildEngine>();
        engine.Setup(value => value.LogMessageEvent(It.IsAny<BuildMessageEventArgs>())).Callback<BuildMessageEventArgs>(messages.Add);
        task.BuildEngine = engine.Object;
        task.OnSimulatedLaunch = current =>
        {
            current.DeliverRequest(new ModuleInfoRequest("framework", "x64", "first-results"));
            current.DeliverRequest(new RunSummaryInfoRequest(total, failed, passed, skipped, "3.5s", allowSkipped));
        };

        Assert.IsTrue(task.Execute(), "The summary's presentation must not replace the tool's exit code.");
        BuildMessageEventArgs summary = Assert.ContainsSingle(messages.Where(message => message.Message!.StartsWith(verdict, StringComparison.Ordinal)));
        Assert.Contains($"Failed: {failed}", summary.Message!);
        Assert.Contains($"Passed: {passed}", summary.Message!);
        Assert.Contains($"Skipped: {skipped}", summary.Message!);
        Assert.Contains($"Total: {total}", summary.Message!);
        Assert.Contains("3.5s", summary.Message!);
        Assert.Contains($"{Path.GetFileName(task.TargetPath.ItemSpec)} (net9.0|x64)", summary.Message!);
        Assert.AreEqual(1, task.SimulatedLaunches);
    }

    [TestMethod]
    public void Execute_FirstModuleOwnsOutputFileAndCompleteHandshakeAllowsSuccess()
    {
        using AppHostTaskFixture fixture = new();
        StubFileSystem fileSystem = new();
        using TestableInvokeTestingPlatformTask task = CreateSimulatedTask(fixture, fileSystem);
        task.OnSimulatedLaunch = current =>
        {
            current.DeliverRequest(new ModuleInfoRequest("first-framework", "first-architecture", "original-results"));
            current.DeliverRequest(new ModuleInfoRequest("retry-framework", "retry-architecture", "retry-results"));
            current.DeliverRequest(new RunSummaryInfoRequest(1, 0, 1, 0, "1s", AllowSkipped: true));
        };

        Assert.IsTrue(task.Execute());
        string path = Assert.ContainsSingle(fileSystem.CreatedFiles.Keys);
        Assert.AreEqual(Path.Combine("original-results", "apphost_net9.0_x64.log"), path);
        string output = Encoding.Unicode.GetString(fileSystem.CreatedFiles[path].ToArray()).TrimStart('\uFEFF');
        Assert.AreEqual($"simulated output{Environment.NewLine}{Environment.NewLine}=== COMMAND LINE ==={Environment.NewLine}simulated command{Environment.NewLine}{Environment.NewLine}", output);
        Assert.IsFalse(fileSystem.CreatedFiles[path].CanWrite, "Execute must dispose the output stream.");
    }

    [TestMethod]
    public void Execute_ModuleWithoutRunSummaryRejectsSuccessfulToolExit()
    {
        using AppHostTaskFixture fixture = new();
        StubFileSystem fileSystem = new();
        using TestableInvokeTestingPlatformTask task = CreateSimulatedTask(fixture, fileSystem);
        List<BuildErrorEventArgs> errors = [];
        var engine = new Mock<IBuildEngine>();
        engine.Setup(value => value.LogErrorEvent(It.IsAny<BuildErrorEventArgs>())).Callback<BuildErrorEventArgs>(errors.Add);
        task.BuildEngine = engine.Object;
        task.OnSimulatedLaunch = current => current.DeliverRequest(new ModuleInfoRequest("framework", "x64", "results"));

        Assert.IsFalse(task.Execute());
        Assert.AreEqual(0, task.ExitCode);
        Assert.Contains("run summary", Assert.ContainsSingle(errors).Message!.ToLowerInvariant());
        Assert.IsFalse(Assert.ContainsSingle(fileSystem.CreatedFiles.Values).CanWrite);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void Execute_FailedTestRequestHonorsShowFailuresAndPreservesSourceLocation(bool showFailures)
    {
        using AppHostTaskFixture fixture = new();
        StubFileSystem fileSystem = new();
        using TestableInvokeTestingPlatformTask task = CreateSimulatedTask(fixture, fileSystem);
        task.TestingPlatformUseDirectExecutable = true;
        task.TestingPlatformShowTestsFailure = new TaskItem(showFailures.ToString(CultureInfo.InvariantCulture));
        List<BuildErrorEventArgs> errors = [];
        var engine = new Mock<IBuildEngine>();
        engine.Setup(value => value.LogErrorEvent(It.IsAny<BuildErrorEventArgs>())).Callback<BuildErrorEventArgs>(errors.Add);
        task.BuildEngine = engine.Object;
        task.OnSimulatedLaunch = current => current.DeliverRequest(
            new FailedTestInfoRequest("failure-name", false, "1s", "expected error", "source stack", null, null, "source.cs", 42));

        Assert.IsTrue(task.Execute());
        if (showFailures)
        {
            BuildErrorEventArgs error = Assert.ContainsSingle(errors);
            Assert.AreEqual("source.cs", error.File);
            Assert.AreEqual(42, error.LineNumber);
            Assert.Contains("failure-name", error.Message!);
            Assert.Contains("expected error", error.Message!);
        }
        else
        {
            Assert.IsEmpty(errors);
        }
    }

    [TestMethod]
    [DataRow(false, 5)]
    [DataRow(true, 2)]
    public void Execute_FailedToolPersistsOutputInDefaultOrModuleOwnedLog(bool hasModule, int exitCode)
    {
        using AppHostTaskFixture fixture = new();
        StubFileSystem fileSystem = new();
        using TestableInvokeTestingPlatformTask task = CreateSimulatedTask(fixture, fileSystem);
        task.SimulatedExitCode = exitCode;
        List<BuildErrorEventArgs> errors = [];
        var engine = new Mock<IBuildEngine>();
        engine.Setup(value => value.LogErrorEvent(It.IsAny<BuildErrorEventArgs>())).Callback<BuildErrorEventArgs>(errors.Add);
        task.BuildEngine = engine.Object;
        if (hasModule)
        {
            task.OnSimulatedLaunch = current => current.DeliverRequest(new ModuleInfoRequest("framework", "x64", "module-results"));
        }

        Assert.IsFalse(task.Execute());
        string directory = hasModule ? "module-results" : Path.Combine(fixture.DotnetRoot, "TestResults");
        string path = Path.Combine(directory, "apphost_net9.0_x64.log");
        Assert.AreEqual(path, Assert.ContainsSingle(fileSystem.CreatedFiles.Keys));
        Assert.Contains("simulated output", Encoding.Unicode.GetString(fileSystem.CreatedFiles[path].ToArray()));
        Assert.Contains("simulated command", Encoding.Unicode.GetString(fileSystem.CreatedFiles[path].ToArray()));
        Assert.Contains(path, Assert.ContainsSingle(errors).Message!);
        Assert.AreEqual("run failed", errors[0].Code);
        Assert.AreEqual(task.TargetPath.ItemSpec, errors[0].File);
        Assert.AreEqual(exitCode, task.ExitCode);
        Assert.IsFalse(fileSystem.CreatedFiles[path].CanWrite);
        if (hasModule)
        {
            Assert.IsEmpty(fileSystem.CreatedDirectories);
        }
        else
        {
            Assert.AreEqual(directory, Assert.ContainsSingle(fileSystem.CreatedDirectories));
        }
    }

    private static TestableInvokeTestingPlatformTask CreateSimulatedTask(AppHostTaskFixture fixture, StubFileSystem fileSystem)
    {
        string extension = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? ".exe" : string.Empty;
        string target = Path.Combine(fixture.DotnetRoot, $"apphost{extension}");
        fileSystem.ExistingPaths.Add(target);
        return new TestableInvokeTestingPlatformTask(fileSystem)
        {
            TargetPath = new TaskItem(target),
            SimulatedExitCode = 0,
            TargetDir = new TaskItem($"{fixture.DotnetRoot}{Path.DirectorySeparatorChar}"),
            AssemblyName = new TaskItem("apphost"),
            NativeExecutableExtension = new TaskItem(extension),
            UseAppHost = new TaskItem("true"),
            IsExecutable = new TaskItem("true"),
        };
    }

    [TestMethod]
    public void ToolResolution_DirectExecutableUsesTargetPathEvenWithAppHostInputs()
    {
        using AppHostTaskFixture fixture = new();
        using TestableInvokeTestingPlatformTask task = fixture.CreateTask();
        task.TestingPlatformUseDirectExecutable = true;
        string target = Path.Combine(fixture.DotnetRoot, "direct target.exe");
        task.TargetPath = new TaskItem(target);

        Assert.AreEqual("direct target.exe", task.GetToolName());
        Assert.AreEqual(target, task.GetFullToolPath());
        Assert.ThrowsExactly<NotSupportedException>(() => task.ToolExe = "override.exe");
    }

    [TestMethod]
    public void ToolResolution_AppHostUsesConstructedRunCommandInsteadOfMuxer()
    {
        using AppHostTaskFixture fixture = new();
        using TestableInvokeTestingPlatformTask task = fixture.CreateTask();
        string expected = Path.Combine(fixture.DotnetRoot, RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "apphost.exe" : "apphost");
        task.TargetPath = new TaskItem(Path.Combine(fixture.DotnetRoot, "assembly.dll"));

        Assert.AreEqual(Path.GetFileName(expected), task.GetToolName());
        Assert.AreEqual(expected, task.GetFullToolPath());
        string command = task.GetGeneratedCommandLine();
        Assert.DoesNotContain("exec", command);
        Assert.DoesNotContain("assembly.dll", command);
        Assert.Contains(task.GetPipeName(), command);
    }

    [TestMethod]
    [DataRow("false", "true")]
    [DataRow("invalid", "true")]
    [DataRow("true", "false")]
    [DataRow("true", "invalid")]
    public void ToolResolution_NonAppHostFlagsUseCompatibleExplicitMuxer(string executable, string appHost)
    {
        using AppHostTaskFixture fixture = new();
        using TestableInvokeTestingPlatformTask task = fixture.CreateTask();
        task.TargetPath = new TaskItem(Path.Combine(fixture.DotnetRoot, "assembly.DLL"));
        task.IsExecutable = new TaskItem(executable);
        task.UseAppHost = new TaskItem(appHost);

        Assert.AreEqual(RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "dotnet.exe" : "dotnet", task.GetToolName());
        Assert.AreEqual(task.DotnetHostPath!.ItemSpec, task.GetFullToolPath());
        Assert.IsTrue(task.GetGeneratedCommandLine().StartsWith("exec ", StringComparison.Ordinal));
        Assert.Contains("assembly.DLL", task.GetGeneratedCommandLine());
    }

    [TestMethod]
    public void ToolResolution_MissingConstructedAppHostFallsBackToCompatibleMuxer()
    {
        using AppHostTaskFixture fixture = new();
        using TestableInvokeTestingPlatformTask task = fixture.CreateTask();
        task.AssemblyName = new TaskItem("missing-apphost");
        task.TargetPath = new TaskItem(Path.Combine(fixture.DotnetRoot, "assembly.dll"));

        Assert.AreEqual(RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "dotnet.exe" : "dotnet", task.GetToolName());
        Assert.AreEqual(task.DotnetHostPath!.ItemSpec, task.GetFullToolPath());
    }

    [TestMethod]
    public void GenerateCommandLineCommands_QuotesTargetAndPreservesUserArgumentsAndRunSettings()
    {
        using AppHostTaskFixture fixture = new();
        using TestableInvokeTestingPlatformTask task = fixture.CreateTask();
        string target = Path.Combine(fixture.DotnetRoot, "assembly with spaces.dll");
        task.TargetPath = new TaskItem(target);
        task.UseAppHost = new TaskItem("false");
        task.TestingPlatformCommandLineArguments = new TaskItem("--filter \"name contains spaces\"");
        task.VSTestCLIRunSettings = [new TaskItem("--setting first=value"), new TaskItem("--setting second=value")];

        string actual = task.GetGeneratedCommandLine();

        Assert.AreEqual(
            $"exec \"{target}\" --internal-msbuild-node {task.GetPipeName()} --filter \"name contains spaces\"  --setting first=value --setting second=value",
            actual);
    }

    [TestMethod]
    public void ValidateParameters_ReportsExactInvalidTargetAndAcceptsExistingTarget()
    {
        StubFileSystem fileSystem = new();
        using TestableInvokeTestingPlatformTask task = new(fileSystem);
        List<BuildErrorEventArgs> errors = [];
        var engine = new Mock<IBuildEngine>();
        engine.Setup(value => value.LogErrorEvent(It.IsAny<BuildErrorEventArgs>())).Callback<BuildErrorEventArgs>(errors.Add);
        task.BuildEngine = engine.Object;
        task.TargetPath = new TaskItem("missing-target.dll");

        Assert.IsFalse(task.InvokeValidateParameters());
        Assert.Contains("missing-target.dll", Assert.ContainsSingle(errors).Message!);
        fileSystem.ExistingPaths.Add("missing-target.dll");
        Assert.IsTrue(task.InvokeValidateParameters());
        Assert.ContainsSingle(errors);
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    public void ToolResolution_FrameworkExecutableIsRunDirectlyOnWindows()
    {
        using TestableInvokeTestingPlatformTask task = new();
        task.TargetFrameworkIdentifier = new TaskItem(".NETFramework");
        task.TargetPath = new TaskItem("framework.exe");

        Assert.AreEqual("framework.exe", task.GetToolName());
        Assert.AreEqual("framework.exe", task.GetFullToolPath());
        Assert.DoesNotContain("exec", task.GetGeneratedCommandLine());
    }

    [TestMethod]
    [DataRow("##[group]Tests: MyAssembly (net9.0)", true)]
    [DataRow("##[endgroup]", true)]
    [DataRow("##[section]Section", true)]
    [DataRow("##vso[task.logissue type=error]boom", true)]
    [DataRow("##vso[task.uploadsummary]/path/to/summary.md", true)]
    [DataRow("Passed! - MyTest (1ms)", false)]
    [DataRow("Running tests: MyAssembly.dll", false)]
    [DataRow("  ##[group]indented is not a command", false)]
    [DataRow("#[group]not-a-command", false)]
    [DataRow("", false)]
    public void IsAzureDevOpsLoggingCommand_ClassifiesLinesCorrectly(string line, bool expected)
        => Assert.AreEqual(expected, InvokeTestingPlatformTask.IsAzureDevOpsLoggingCommand(line));

    [TestMethod]
    [ResourceLock(WellKnownResources.Console)] // Mutates process-wide Console.Out; must not overlap with other tests capturing/using the console.
    public void LogEventsFromTextOutput_AzureDevOpsCommands_AreWrittenToStdoutAtColumnZero_NotThroughMSBuildLog()
    {
        List<string> loggedMessages = [];
        Mock<IBuildEngine> buildEngine = new();
        buildEngine
            .Setup(x => x.LogMessageEvent(It.IsAny<BuildMessageEventArgs>()))
            .Callback<BuildMessageEventArgs>(e => loggedMessages.Add(e.Message ?? string.Empty));

        TestableInvokeTestingPlatformTask task = new() { BuildEngine = buildEngine.Object };

        TextWriter originalOut = Console.Out;
        using StringWriter capturedStdout = new();
        Console.SetOut(capturedStdout);
        try
        {
            task.InvokeLogEventsFromTextOutput("##[group]Tests: MyAssembly (net9.0)");
            task.InvokeLogEventsFromTextOutput("##[endgroup]");
            task.InvokeLogEventsFromTextOutput("normal output line");
        }
        finally
        {
            Console.SetOut(originalOut);
        }

        string[] stdoutLines = capturedStdout.ToString().Split(["\r\n", "\n"], StringSplitOptions.RemoveEmptyEntries);

        // Azure DevOps commands are written verbatim to stdout at column 0 (exact element match => no indentation).
        Assert.IsTrue(stdoutLines.Contains("##[group]Tests: MyAssembly (net9.0)"), capturedStdout.ToString());
        Assert.IsTrue(stdoutLines.Contains("##[endgroup]"), capturedStdout.ToString());

        // ...and are NOT routed through the MSBuild logger, which would indent them and break Azure DevOps parsing.
        string joinedMessages = string.Join('\n', loggedMessages);
        Assert.DoesNotContain("##[group]", joinedMessages);
        Assert.DoesNotContain("##[endgroup]", joinedMessages);

        // Regular output keeps flowing through the MSBuild logger and does not leak to stdout.
        Assert.Contains("normal output line", loggedMessages);
        Assert.DoesNotContain("normal output line", capturedStdout.ToString());
        Assert.AreEqual(
            "##[group]Tests: MyAssembly (net9.0)"
                + Environment.NewLine
                + "##[endgroup]"
                + Environment.NewLine
                + "normal output line"
                + Environment.NewLine,
            task.GetCapturedOutput());
    }

    [TestMethod]
    public void AddAppHostDotnetRootEnvironmentVariable_AddsArchitectureSpecificVariableBeforeUserVariables()
    {
        using AppHostTaskFixture fixture = new();
        TestableInvokeTestingPlatformTask task = fixture.CreateTask();
        task.EnvironmentVariables = ["CUSTOM_VARIABLE=value"];

        task.InvokeAddAppHostDotnetRootEnvironmentVariable();

        Assert.IsNotNull(task.EnvironmentVariables);
        Assert.HasCount(2, task.EnvironmentVariables);
        Assert.AreEqual($"{GetDotnetRootArchitectureVariableName()}={fixture.DotnetRoot}", task.EnvironmentVariables[0]);
        Assert.AreEqual("CUSTOM_VARIABLE=value", task.EnvironmentVariables[1]);
    }

    [TestMethod]
    public void AddAppHostDotnetRootEnvironmentVariable_WithNoUserVariables_AddsOnlyResolvedVariableAndLogsIt()
    {
        using AppHostTaskFixture fixture = new();
        List<BuildMessageEventArgs> messages = [];
        Mock<IBuildEngine> buildEngine = new();
        buildEngine
            .Setup(engine => engine.LogMessageEvent(It.IsAny<BuildMessageEventArgs>()))
            .Callback<BuildMessageEventArgs>(messages.Add);
        TestableInvokeTestingPlatformTask task = fixture.CreateTask();
        task.BuildEngine = buildEngine.Object;

        task.InvokeAddAppHostDotnetRootEnvironmentVariable();

        Assert.IsNotNull(task.EnvironmentVariables);
        Assert.AreSequenceEqual(
            [$"{GetDotnetRootArchitectureVariableName()}={fixture.DotnetRoot}"],
            task.EnvironmentVariables);
        Assert.Contains(
            message => message.Importance == MessageImportance.Low
                && message.Message == $"Setting '{GetDotnetRootArchitectureVariableName()}' to '{fixture.DotnetRoot}' for apphost runtime resolution.",
            messages);
    }

    [TestMethod]
    public void AddAppHostDotnetRootEnvironmentVariable_UsesRequestedEnvironmentVariableComparison()
    {
        using AppHostTaskFixture fixture = new();
        string variableName = GetDotnetRootArchitectureVariableName();

        TestableInvokeTestingPlatformTask windowsTask = fixture.CreateTask();
        windowsTask.EnvironmentVariables = [$"{variableName.ToLowerInvariant()}=explicit"];
        windowsTask.InvokeAddAppHostDotnetRootEnvironmentVariable(
            RuntimeInformation.ProcessArchitecture,
            isWindows: true,
            Environment.Is64BitOperatingSystem);
        Assert.AreSequenceEqual(
            [$"{variableName.ToLowerInvariant()}=explicit"],
            windowsTask.EnvironmentVariables!);

        TestableInvokeTestingPlatformTask unixTask = fixture.CreateTask();
        unixTask.EnvironmentVariables = [$"{variableName.ToLowerInvariant()}=explicit"];
        unixTask.InvokeAddAppHostDotnetRootEnvironmentVariable(
            RuntimeInformation.ProcessArchitecture,
            isWindows: false,
            Environment.Is64BitOperatingSystem);
        Assert.AreSequenceEqual(
            [$"{variableName}={fixture.DotnetRoot}", $"{variableName.ToLowerInvariant()}=explicit"],
            unixTask.EnvironmentVariables!);
    }

    [TestMethod]
    public void AddAppHostDotnetRootEnvironmentVariable_DoesNotOverrideExplicitDotnetRoot()
    {
        using AppHostTaskFixture fixture = new();
        TestableInvokeTestingPlatformTask task = fixture.CreateTask();
        task.EnvironmentVariables = ["DOTNET_ROOT=explicit"];

        task.InvokeAddAppHostDotnetRootEnvironmentVariable();

        Assert.IsNotNull(task.EnvironmentVariables);
        Assert.HasCount(2, task.EnvironmentVariables);
        Assert.AreEqual($"{GetDotnetRootArchitectureVariableName()}=", task.EnvironmentVariables[0]);
        Assert.AreEqual("DOTNET_ROOT=explicit", task.EnvironmentVariables[1]);
    }

    [TestMethod]
    public void AddAppHostDotnetRootEnvironmentVariable_DoesNotOverrideExplicitArchitectureSpecificDotnetRoot()
    {
        using AppHostTaskFixture fixture = new();
        TestableInvokeTestingPlatformTask task = fixture.CreateTask();
        string explicitVariable = $"{GetDotnetRootArchitectureVariableName()}=explicit";
        task.EnvironmentVariables = [explicitVariable];

        task.InvokeAddAppHostDotnetRootEnvironmentVariable();

        Assert.IsNotNull(task.EnvironmentVariables);
        Assert.HasCount(1, task.EnvironmentVariables);
        Assert.AreEqual(explicitVariable, task.EnvironmentVariables[0]);
    }

    [TestMethod]
    public void AddAppHostDotnetRootEnvironmentVariable_ExplicitDotnetRootInWindowsX86Process_ClearsBothArchitectureSpecificVariables()
    {
        using AppHostTaskFixture fixture = new();
        TestableInvokeTestingPlatformTask task = fixture.CreateTask();
        task.TestArchitecture = new TaskItem("X86");
        task.EnvironmentVariables = ["DOTNET_ROOT=explicit"];

        task.InvokeAddAppHostDotnetRootEnvironmentVariable(Architecture.X86, isWindows: true, is64BitOperatingSystem: true);

        Assert.IsNotNull(task.EnvironmentVariables);
        Assert.HasCount(3, task.EnvironmentVariables);
        Assert.AreEqual("DOTNET_ROOT_X86=", task.EnvironmentVariables[0]);
        Assert.AreEqual("DOTNET_ROOT(x86)=", task.EnvironmentVariables[1]);
        Assert.AreEqual("DOTNET_ROOT=explicit", task.EnvironmentVariables[2]);
    }

    [TestMethod]
    public void AddAppHostDotnetRootEnvironmentVariable_ExplicitWindowsX86DotnetRoot_ClearsOnlyArchitectureSpecificVariable()
    {
        using AppHostTaskFixture fixture = new();
        TestableInvokeTestingPlatformTask task = fixture.CreateTask();
        task.TestArchitecture = new TaskItem("X86");
        task.EnvironmentVariables = ["DOTNET_ROOT(x86)=explicit"];

        task.InvokeAddAppHostDotnetRootEnvironmentVariable(Architecture.X86, isWindows: true, is64BitOperatingSystem: true);

        Assert.IsNotNull(task.EnvironmentVariables);
        Assert.HasCount(2, task.EnvironmentVariables);
        Assert.AreEqual("DOTNET_ROOT_X86=", task.EnvironmentVariables[0]);
        Assert.AreEqual("DOTNET_ROOT(x86)=explicit", task.EnvironmentVariables[1]);
    }

    [TestMethod]
    public void AddAppHostDotnetRootEnvironmentVariable_DoesNothingWhenDisabled()
    {
        using AppHostTaskFixture fixture = new();
        TestableInvokeTestingPlatformTask task = fixture.CreateTask();
        task.TestingPlatformDisableAppHostDotnetRoot = true;

        task.InvokeAddAppHostDotnetRootEnvironmentVariable();

        Assert.IsNull(task.EnvironmentVariables);
    }

    [TestMethod]
    public void AddAppHostDotnetRootEnvironmentVariable_DoesNothingWithoutAppHost()
    {
        using AppHostTaskFixture fixture = new();
        TestableInvokeTestingPlatformTask task = fixture.CreateTask();
        task.UseAppHost = new TaskItem("false");

        task.InvokeAddAppHostDotnetRootEnvironmentVariable();

        Assert.IsNull(task.EnvironmentVariables);
    }

    [TestMethod]
    public void AddAppHostDotnetRootEnvironmentVariable_DoesNothingForIncompatibleArchitecture()
    {
        using AppHostTaskFixture fixture = new();
        TestableInvokeTestingPlatformTask task = fixture.CreateTask();
        task.TestArchitecture = new TaskItem(RuntimeInformation.ProcessArchitecture is Architecture.X64 or Architecture.X86 ? "arm64" : "x64");

        task.InvokeAddAppHostDotnetRootEnvironmentVariable();

        Assert.IsNull(task.EnvironmentVariables);
    }

    [TestMethod]
    [DataRow("X86", true, true, true)]
    [DataRow("X86", true, false, false)]
    [DataRow("X86", false, true, false)]
    [DataRow("X64", true, true, false)]
    public void IsWindowsX86ProcessOn64BitOperatingSystem_ReturnsExpectedResult(
        string architecture,
        bool isWindows,
        bool is64BitOperatingSystem,
        bool expected)
        => Assert.AreEqual(
            expected,
            InvokeTestingPlatformTask.IsWindowsX86ProcessOn64BitOperatingSystem(
                Enum.Parse<Architecture>(architecture),
                isWindows,
                is64BitOperatingSystem));

    [TestMethod]
    public void Constructor_UsesPlatformSpecificPipeName()
    {
        TestableInvokeTestingPlatformTask task = new();
        string pipeName = task.GetPipeName();
        const string Prefix = "testingplatform.pipe.";

        if (Path.DirectorySeparatorChar == '/')
        {
            Assert.IsTrue(Path.IsPathRooted(pipeName));
            Assert.IsTrue(Guid.TryParseExact(Path.GetFileName(pipeName), "N", out _));
        }
        else
        {
            Assert.StartsWith(Prefix, pipeName);
            Assert.DoesNotContain(Path.DirectorySeparatorChar.ToString(), pipeName);
            Assert.IsTrue(Guid.TryParseExact(pipeName[Prefix.Length..], "N", out _));
        }
    }

    private static string GetDotnetRootArchitectureVariableName()
        => $"DOTNET_ROOT_{RuntimeInformation.ProcessArchitecture.ToString().ToUpperInvariant()}";

    private sealed class TestableInvokeTestingPlatformTask : InvokeTestingPlatformTask
    {
        // MSBuild is the one that normally supplies the [Required] inputs; stub them out here so the test can focus
        // on the output-logging behavior.
        [SetsRequiredMembers]
        public TestableInvokeTestingPlatformTask()
            : this(new StubFileSystem())
        {
        }

        [SetsRequiredMembers]
        public TestableInvokeTestingPlatformTask(IFileSystem fileSystem)
            : base(fileSystem)
        {
            BuildEngine = Mock.Of<IBuildEngine>();
            TargetPath = new TaskItem("MyAssembly.dll");
            TargetFramework = new TaskItem("net9.0");
            TestArchitecture = new TaskItem("x64");
            TargetFrameworkIdentifier = new TaskItem(".NETCoreApp");
            TestingPlatformShowTestsFailure = new TaskItem("false");
            TestingPlatformCaptureOutput = new TaskItem("true");
            ProjectFullPath = new TaskItem("MyProject.csproj");
        }

        public void InvokeLogEventsFromTextOutput(string singleLine)
            => LogEventsFromTextOutput(singleLine, MessageImportance.High);

        public string GetToolName() => ToolName;

        public int? SimulatedExitCode { get; set; }

        public int SimulatedLaunches { get; private set; }

        public Action<TestableInvokeTestingPlatformTask>? OnSimulatedLaunch { get; set; }

        public string? GetFullToolPath() => GenerateFullPathToTool();

        public string GetGeneratedCommandLine() => GenerateCommandLineCommands();

        public bool InvokeValidateParameters() => ValidateParameters();

        protected override int ExecuteTool(string pathToTool, string responseFileCommands, string commandLineCommands)
        {
            if (SimulatedExitCode is not int exitCode)
            {
                return base.ExecuteTool(pathToTool, responseFileCommands, commandLineCommands);
            }

            SimulatedLaunches++;
            LogToolCommand("simulated command");
            LogEventsFromTextOutput("simulated output", MessageImportance.High);
            OnSimulatedLaunch?.Invoke(this);
            return exitCode;
        }

        public void DeliverRequest(object request)
            => ((System.Threading.Tasks.Task)typeof(InvokeTestingPlatformTask)
                .GetMethod("HandleRequestAsync", BindingFlags.Instance | BindingFlags.NonPublic)!
                .Invoke(this, [request])!).GetAwaiter().GetResult();

        public void InvokeAddAppHostDotnetRootEnvironmentVariable()
            => AddAppHostDotnetRootEnvironmentVariable();

        public void InvokeAddAppHostDotnetRootEnvironmentVariable(
            Architecture currentProcessArchitecture,
            bool isWindows,
            bool is64BitOperatingSystem)
            => AddAppHostDotnetRootEnvironmentVariable(currentProcessArchitecture, isWindows, is64BitOperatingSystem);

        public string GetCapturedOutput()
            => ((StringBuilder)typeof(InvokeTestingPlatformTask)
                .GetField("_output", BindingFlags.Instance | BindingFlags.NonPublic)!
                .GetValue(this)!).ToString();

        public string GetPipeName()
        {
            object description = typeof(InvokeTestingPlatformTask)
                .GetField("_pipeNameDescription", BindingFlags.Instance | BindingFlags.NonPublic)!
                .GetValue(this)!;
            return (string)description.GetType().GetProperty("Name")!.GetValue(description)!;
        }
    }

    private sealed class AppHostTaskFixture : IDisposable
    {
        private readonly string _appHostExtension = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? ".exe" : string.Empty;

        public AppHostTaskFixture()
        {
            Directory.CreateDirectory(DotnetRoot);
            File.WriteAllText(Path.Combine(DotnetRoot, $"apphost{_appHostExtension}"), string.Empty);
            File.WriteAllText(Path.Combine(DotnetRoot, RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "dotnet.exe" : "dotnet"), string.Empty);
        }

        public string DotnetRoot { get; } = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));

        public TestableInvokeTestingPlatformTask CreateTask()
            => new()
            {
                TargetPath = new TaskItem(Path.Combine(DotnetRoot, $"apphost{_appHostExtension}")),
                TargetDir = new TaskItem($"{DotnetRoot}{Path.DirectorySeparatorChar}"),
                AssemblyName = new TaskItem("apphost"),
                NativeExecutableExtension = new TaskItem(_appHostExtension),
                UseAppHost = new TaskItem("true"),
                IsExecutable = new TaskItem("true"),
                TestArchitecture = new TaskItem(RuntimeInformation.ProcessArchitecture.ToString()),
                DotnetHostPath = new TaskItem(Path.Combine(DotnetRoot, RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "dotnet.exe" : "dotnet")),
            };

        public void Dispose() => Directory.Delete(DotnetRoot, recursive: true);
    }

    private sealed class StubFileSystem : IFileSystem
    {
        public HashSet<string> ExistingPaths { get; } = [];

        public Dictionary<string, MemoryStream> CreatedFiles { get; } = [];

        public List<string> CreatedDirectories { get; } = [];

        public bool Exist(string path) => ExistingPaths.Contains(path);

        public void CreateDirectory(string directory) => CreatedDirectories.Add(directory);

        public Stream CreateNew(string path)
        {
            MemoryStream stream = new();
            CreatedFiles.Add(path, stream);
            return stream;
        }

        public void CopyFile(string source, string destination) => throw new NotSupportedException();

        public string ReadAllText(string path) => throw new NotSupportedException();

        public void WriteAllText(string path, string? contents) => throw new NotSupportedException();
    }
}
