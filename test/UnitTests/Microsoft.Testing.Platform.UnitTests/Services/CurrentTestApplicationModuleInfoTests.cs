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

        Assert.AreSequenceEqual(new[] { "--filter", "MyTest" }, executable.Arguments.ToArray());
    }

    [TestMethod]
    public void GetCurrentExecutableInfo_AppHost_WithPassedArgs_UsesPassedArgsInsteadOfEnvironmentArgs()
    {
        // Simulate a custom Main that mutates args (e.g. inserting default options) before
        // forwarding them to TestApplication.CreateBuilderAsync.
        Mock<IEnvironment> environment = CreateAppHostEnvironment(["myapp.exe"]);

        CurrentTestApplicationModuleInfo info = new(environment.Object, new SystemProcessHandler(), ["--retry-failed-tests", "1"]);

        ExecutableInfo executable = info.GetCurrentExecutableInfo();

        Assert.AreSequenceEqual(new[] { "--retry-failed-tests", "1" }, executable.Arguments.ToArray());
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

        Assert.AreSequenceEqual(new[] { "exec", "myapp.dll", "--filter", "MyTest" }, executable.Arguments.ToArray());
    }

    [TestMethod]
    public void GetCurrentExecutableInfo_DotnetMuxer_WithPassedArgs_KeepsDllPathAndUsesPassedArgs()
    {
        // For the dotnet muxer case we still need to know which dll to launch, so we recover that
        // from Environment.GetCommandLineArgs()[0]. The args that follow come from the user.
        Mock<IEnvironment> environment = CreateDotnetMuxerEnvironment(["myapp.dll"]);

        CurrentTestApplicationModuleInfo info = new(environment.Object, new SystemProcessHandler(), ["--retry-failed-tests", "1"]);

        ExecutableInfo executable = info.GetCurrentExecutableInfo();

        Assert.AreSequenceEqual(new[] { "exec", "myapp.dll", "--retry-failed-tests", "1" }, executable.Arguments.ToArray());
    }

    [TestMethod]
    public void GetCurrentExecutableInfo_DotnetMuxer_WithPassedArgsAndEmptyEnvironmentArgs_FallsBackToExecPlusPassedArgs()
    {
        Mock<IEnvironment> environment = CreateDotnetMuxerEnvironment([]);

        CurrentTestApplicationModuleInfo info = new(environment.Object, new SystemProcessHandler(), ["--retry-failed-tests", "1"]);

        ExecutableInfo executable = info.GetCurrentExecutableInfo();

        Assert.AreSequenceEqual(new[] { "exec", "--retry-failed-tests", "1" }, executable.Arguments.ToArray());
    }

    [TestMethod]
    public void GetCurrentExecutableInfo_MonoMuxer_NoPassedArgs_UsesEnvironmentArgsAsIs()
    {
        Mock<IEnvironment> environment = CreateMonoMuxerEnvironment(["myapp.dll", "--filter", "MyTest"]);

        CurrentTestApplicationModuleInfo info = new(environment.Object, new SystemProcessHandler());

        ExecutableInfo executable = info.GetCurrentExecutableInfo();

        Assert.AreSequenceEqual(new[] { "myapp.dll", "--filter", "MyTest" }, executable.Arguments.ToArray());
    }

    [TestMethod]
    public void GetCurrentExecutableInfo_MonoMuxer_WithPassedArgs_KeepsDllPathAndUsesPassedArgs()
    {
        // Same as the dotnet muxer case: we need to keep the dll path (envArgs[0]) so mono knows
        // which assembly to run, but the rest of the arguments must come from the user-supplied args.
        Mock<IEnvironment> environment = CreateMonoMuxerEnvironment(["myapp.dll", "--filter", "MyTest"]);

        CurrentTestApplicationModuleInfo info = new(environment.Object, new SystemProcessHandler(), ["--retry-failed-tests", "1"]);

        ExecutableInfo executable = info.GetCurrentExecutableInfo();

        Assert.AreSequenceEqual(new[] { "myapp.dll", "--retry-failed-tests", "1" }, executable.Arguments.ToArray());
    }

    [TestMethod]
    public void GetCurrentExecutableInfo_MonoMuxer_WithPassedArgsAndEmptyEnvironmentArgs_FallsBackToPassedArgs()
    {
        Mock<IEnvironment> environment = CreateMonoMuxerEnvironment([]);

        CurrentTestApplicationModuleInfo info = new(environment.Object, new SystemProcessHandler(), ["--retry-failed-tests", "1"]);

        ExecutableInfo executable = info.GetCurrentExecutableInfo();

        Assert.AreSequenceEqual(new[] { "--retry-failed-tests", "1" }, executable.Arguments.ToArray());
    }

    [TestMethod]
    public void GetCurrentExecutableInfo_PassedArgsAreSnapshotted_CallerMutationDoesNotAffectResult()
    {
        // The constructor should take a defensive copy of the passed args so that later
        // mutations of the caller's array don't change what GetCurrentExecutableInfo returns.
        Mock<IEnvironment> environment = CreateAppHostEnvironment(["myapp.exe"]);
        string[] passedArgs = ["--retry-failed-tests", "1"];

        CurrentTestApplicationModuleInfo info = new(environment.Object, new SystemProcessHandler(), passedArgs);

        passedArgs[0] = "--mutated";
        passedArgs[1] = "999";

        ExecutableInfo executable = info.GetCurrentExecutableInfo();

        Assert.AreSequenceEqual(new[] { "--retry-failed-tests", "1" }, executable.Arguments.ToArray());
    }

    private static Mock<IEnvironment> CreateDotnetMuxerEnvironment(string[] commandLineArgs)
    {
        Mock<IEnvironment> environment = new();
        environment.SetupGet(e => e.ProcessPath).Returns(Path.Combine("some", "directory", "dotnet.exe"));
        environment.Setup(e => e.GetCommandLineArgs()).Returns(commandLineArgs);
        return environment;
    }

    private static Mock<IEnvironment> CreateMonoMuxerEnvironment(string[] commandLineArgs)
    {
        Mock<IEnvironment> environment = new();
        environment.SetupGet(e => e.ProcessPath).Returns(Path.Combine("some", "directory", "mono.exe"));
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
