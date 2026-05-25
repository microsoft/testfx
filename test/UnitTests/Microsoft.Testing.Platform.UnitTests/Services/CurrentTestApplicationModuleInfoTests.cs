// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.Services;

using Moq;

namespace Microsoft.Testing.Platform.UnitTests;

[TestClass]
[UnsupportedOSPlatform("browser")]
public sealed class CurrentTestApplicationModuleInfoTests
{
    [TestMethod]
    public void GetCurrentExecutableInfo_AppHost_NoPassedArgs_UsesEnvironmentArgsSkippingProcessPath()
    {
        Mock<IEnvironment> environment = CreateAppHostEnvironment(["myapp.exe", "--filter", "MyTest"]);

        CurrentTestApplicationModuleInfo info = new(environment.Object, new SystemProcessHandler());

        ExecutableInfo executable = info.GetCurrentExecutableInfo();

        CollectionAssert.AreEqual(new[] { "--filter", "MyTest" }, executable.Arguments.ToArray());
    }

    [TestMethod]
    public void GetCurrentExecutableInfo_AppHost_WithPassedArgs_UsesPassedArgsInsteadOfEnvironmentArgs()
    {
        // Simulate a custom Main that mutates args (e.g. inserting default options) before
        // forwarding them to TestApplication.CreateBuilderAsync.
        Mock<IEnvironment> environment = CreateAppHostEnvironment(["myapp.exe"]);

        CurrentTestApplicationModuleInfo info = new(environment.Object, new SystemProcessHandler(), ["--retry-failed-tests", "1"]);

        ExecutableInfo executable = info.GetCurrentExecutableInfo();

        CollectionAssert.AreEqual(new[] { "--retry-failed-tests", "1" }, executable.Arguments.ToArray());
    }

    [TestMethod]
    public void GetCurrentExecutableInfo_AppHost_WithEmptyPassedArgs_ReturnsEmpty()
    {
        Mock<IEnvironment> environment = CreateAppHostEnvironment(["myapp.exe", "--filter", "MyTest"]);

        CurrentTestApplicationModuleInfo info = new(environment.Object, new SystemProcessHandler(), []);

        ExecutableInfo executable = info.GetCurrentExecutableInfo();

        Assert.IsEmpty(executable.Arguments);
    }

#if NETCOREAPP
    [TestMethod]
    public void GetCurrentExecutableInfo_DotnetMuxer_NoPassedArgs_PrependsExecToEnvironmentArgs()
    {
        Mock<IEnvironment> environment = CreateDotnetMuxerEnvironment(["myapp.dll", "--filter", "MyTest"]);

        CurrentTestApplicationModuleInfo info = new(environment.Object, new SystemProcessHandler());

        ExecutableInfo executable = info.GetCurrentExecutableInfo();

        CollectionAssert.AreEqual(new[] { "exec", "myapp.dll", "--filter", "MyTest" }, executable.Arguments.ToArray());
    }

    [TestMethod]
    public void GetCurrentExecutableInfo_DotnetMuxer_WithPassedArgs_KeepsDllPathAndUsesPassedArgs()
    {
        // For the dotnet muxer case we still need to know which dll to launch, so we recover that
        // from Environment.GetCommandLineArgs()[0]. The args that follow come from the user.
        Mock<IEnvironment> environment = CreateDotnetMuxerEnvironment(["myapp.dll"]);

        CurrentTestApplicationModuleInfo info = new(environment.Object, new SystemProcessHandler(), ["--retry-failed-tests", "1"]);

        ExecutableInfo executable = info.GetCurrentExecutableInfo();

        CollectionAssert.AreEqual(new[] { "exec", "myapp.dll", "--retry-failed-tests", "1" }, executable.Arguments.ToArray());
    }

    private static Mock<IEnvironment> CreateDotnetMuxerEnvironment(string[] commandLineArgs)
    {
        Mock<IEnvironment> environment = new();
        environment.SetupGet(e => e.ProcessPath).Returns(Path.Combine("some", "directory", "dotnet.exe"));
        environment.Setup(e => e.GetCommandLineArgs()).Returns(commandLineArgs);
        return environment;
    }
#endif

    private static Mock<IEnvironment> CreateAppHostEnvironment(string[] commandLineArgs)
    {
        Mock<IEnvironment> environment = new();
#if NETCOREAPP
        environment.SetupGet(e => e.ProcessPath).Returns(Path.Combine("some", "directory", "myapp.exe"));
#endif
        environment.Setup(e => e.GetCommandLineArgs()).Returns(commandLineArgs);
        return environment;
    }
}
