// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.Services;

namespace Microsoft.Testing.Platform.UnitTests.Services;

[TestClass]
public class CurrentTestApplicationModuleInfoTests
{
    [TestMethod]
    public void GetCurrentExecutableInfo_WithCustomArgs_ForExecutable_ShouldUseCustomArguments()
    {
        // Arrange
        var environment = new TestEnvironment();
        environment.SetCommandLineArgs(["MyTest.exe"]);
        environment.SetProcessPath("MyTest.exe");
        
        var processHandler = new TestProcessHandler();
        var moduleInfo = new CurrentTestApplicationModuleInfo(environment, processHandler);
        
        var customArgs = new[] { "--retry-failed-tests", "1", "--results-directory", "test" };
        var argsProvider = new CommandLineArgumentsProvider(customArgs);
        moduleInfo.SetCommandLineArgumentsProvider(argsProvider);

        // Act
        var executableInfo = moduleInfo.GetCurrentExecutableInfo();

        // Assert
        Assert.IsNotNull(executableInfo);
        var arguments = executableInfo.Arguments.ToArray();
        
        // For executable scenarios, arguments should be: custom args (without executable name)
        Assert.AreEqual(4, arguments.Length);
        Assert.AreEqual("--retry-failed-tests", arguments[0]);
        Assert.AreEqual("1", arguments[1]);
        Assert.AreEqual("--results-directory", arguments[2]);
        Assert.AreEqual("test", arguments[3]);
    }

    [TestMethod]
    public void GetCurrentExecutableInfo_WithCustomArgs_ForDotnet_ShouldCombineWithAssemblyPath()
    {
        // Arrange
        var environment = new TestEnvironment();
        environment.SetCommandLineArgs(["dotnet", "MyTest.dll"]);
        environment.SetProcessPath("dotnet");
        
        var processHandler = new TestProcessHandler();
        var moduleInfo = new CurrentTestApplicationModuleInfo(environment, processHandler);
        
        var customArgs = new[] { "--retry-failed-tests", "1" };
        var argsProvider = new CommandLineArgumentsProvider(customArgs);
        moduleInfo.SetCommandLineArgumentsProvider(argsProvider);

        // Act
        var executableInfo = moduleInfo.GetCurrentExecutableInfo();

        // Assert
        Assert.IsNotNull(executableInfo);
        var arguments = executableInfo.Arguments.ToArray();
        
        // For dotnet scenarios, arguments should be: ["exec", "dotnet", "MyTest.dll", custom args...]
        Assert.AreEqual(5, arguments.Length);
        Assert.AreEqual("exec", arguments[0]);
        Assert.AreEqual("dotnet", arguments[1]);
        Assert.AreEqual("MyTest.dll", arguments[2]);
        Assert.AreEqual("--retry-failed-tests", arguments[3]);
        Assert.AreEqual("1", arguments[4]);
    }

    [TestMethod]
    public void GetCurrentExecutableInfo_WithoutCustomArgs_ShouldUseEnvironmentArguments()
    {
        // Arrange
        var environment = new TestEnvironment();
        environment.SetCommandLineArgs(["MyTest.exe", "--existing-arg"]);
        environment.SetProcessPath("MyTest.exe");
        
        var processHandler = new TestProcessHandler();
        var moduleInfo = new CurrentTestApplicationModuleInfo(environment, processHandler);

        // Act (no custom args provider set)
        var executableInfo = moduleInfo.GetCurrentExecutableInfo();

        // Assert
        Assert.IsNotNull(executableInfo);
        var arguments = executableInfo.Arguments.ToArray();
        
        // Should fall back to environment args (minus executable name)
        Assert.AreEqual(1, arguments.Length);
        Assert.AreEqual("--existing-arg", arguments[0]);
    }

    private class TestEnvironment : IEnvironment
    {
        private string[] _commandLineArgs = Array.Empty<string>();
        private string? _processPath;

        public void SetCommandLineArgs(string[] args) => _commandLineArgs = args;
        public void SetProcessPath(string path) => _processPath = path;

        public string[] GetCommandLineArgs() => _commandLineArgs;
        public int ProcessId => 1234;
        public string? ProcessPath => _processPath;
        
        public string? GetEnvironmentVariable(string name) => null;
        public void SetEnvironmentVariable(string name, string? value) { }
    }

    private class TestProcessHandler : IProcessHandler
    {
        public IProcess GetCurrentProcess() => new TestProcess();
        public IProcess Start(ProcessStartInfo startInfo) => new TestProcess();
    }

    private class TestProcess : IProcess
    {
        public int Id => 1234;
        public string Name => "test";
        public ProcessModule? MainModule => new TestProcessModule();
        public int ExitCode => 0;
        public event EventHandler? Exited;
        public void WaitForExit() { }
        public Task WaitForExitAsync() => Task.CompletedTask;
        public void Dispose() { }
    }

    private class TestProcessModule : ProcessModule
    {
        public override string? FileName => "MyTest.exe";
    }
}