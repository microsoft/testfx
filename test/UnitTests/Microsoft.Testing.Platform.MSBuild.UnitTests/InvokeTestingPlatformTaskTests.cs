// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Microsoft.Testing.Platform.Helpers;

using Moq;

namespace Microsoft.Testing.Platform.MSBuild.UnitTests;

[TestClass]
public sealed class InvokeTestingPlatformTaskTests
{
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
    [DoNotParallelize] // Mutates process-wide Console.Out; must not overlap with parallel tests capturing/using the console.
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

    private static string GetDotnetRootArchitectureVariableName()
        => $"DOTNET_ROOT_{RuntimeInformation.ProcessArchitecture.ToString().ToUpperInvariant()}";

    private sealed class TestableInvokeTestingPlatformTask : InvokeTestingPlatformTask
    {
        // MSBuild is the one that normally supplies the [Required] inputs; stub them out here so the test can focus
        // on the output-logging behavior.
        [SetsRequiredMembers]
        public TestableInvokeTestingPlatformTask()
            : base(new StubFileSystem())
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

        public void InvokeAddAppHostDotnetRootEnvironmentVariable()
            => AddAppHostDotnetRootEnvironmentVariable();

        public void InvokeAddAppHostDotnetRootEnvironmentVariable(
            Architecture currentProcessArchitecture,
            bool isWindows,
            bool is64BitOperatingSystem)
            => AddAppHostDotnetRootEnvironmentVariable(currentProcessArchitecture, isWindows, is64BitOperatingSystem);
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
        public bool Exist(string path) => false;

        public void CreateDirectory(string directory) => throw new NotSupportedException();

        public Stream CreateNew(string path) => throw new NotSupportedException();

        public void CopyFile(string source, string destination) => throw new NotSupportedException();

        public void WriteAllText(string path, string? contents) => throw new NotSupportedException();
    }
}
