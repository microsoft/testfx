// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Helpers;

using Moq;

namespace Microsoft.Testing.Platform.UnitTests;

[TestClass]
[UnsupportedOSPlatform("browser")]
public sealed class NonCooperativeParentProcessListenerTests
{
    [TestMethod]
    public void Constructor_ParentProcessAlreadyExited_ExitsImmediately()
    {
        int exitedPid = GetGuaranteedExitedProcessId();
        Mock<ICommandLineOptions> commandLineOptions = CreateCommandLineOptions(exitedPid);
        Mock<IEnvironment> environment = new();

        using var listener = new NonCooperativeParentProcessListener(commandLineOptions.Object, environment.Object);

        environment.Verify(e => e.Exit((int)ExitCode.DependentProcessExited), Times.Once);
    }

    [TestMethod]
    public void Constructor_ParentProcessStillRunning_DoesNotExit()
    {
        int currentPid = Process.GetCurrentProcess().Id;
        Mock<ICommandLineOptions> commandLineOptions = CreateCommandLineOptions(currentPid);
        Mock<IEnvironment> environment = new();

        using var listener = new NonCooperativeParentProcessListener(commandLineOptions.Object, environment.Object);

        environment.Verify(e => e.Exit(It.IsAny<int>()), Times.Never);
    }

    [TestMethod]
    public void ParentProcessExitedHandler_WhenInvoked_CallsEnvironmentExit()
    {
        int currentPid = Process.GetCurrentProcess().Id;
        Mock<ICommandLineOptions> commandLineOptions = CreateCommandLineOptions(currentPid);
        Mock<IEnvironment> environment = new();

        using var listener = new NonCooperativeParentProcessListener(commandLineOptions.Object, environment.Object);

        MethodInfo handler = typeof(NonCooperativeParentProcessListener).GetMethod(
            "ParentProcess_Exited", BindingFlags.NonPublic | BindingFlags.Instance)!;
        handler.Invoke(listener, [null, EventArgs.Empty]);

        environment.Verify(e => e.Exit((int)ExitCode.DependentProcessExited), Times.Once);
    }

    [TestMethod]
    public void Dispose_ParentProcessAlreadyExited_DoesNotThrow()
    {
        int exitedPid = GetGuaranteedExitedProcessId();
        Mock<ICommandLineOptions> commandLineOptions = CreateCommandLineOptions(exitedPid);
        Mock<IEnvironment> environment = new();

        var listener = new NonCooperativeParentProcessListener(commandLineOptions.Object, environment.Object);

        listener.Dispose();
        listener.Dispose();
    }

    [TestMethod]
    public void Dispose_ParentProcessStillRunning_DisposesUnderlyingProcessAndDoesNotThrow()
    {
        int currentPid = Process.GetCurrentProcess().Id;
        Mock<ICommandLineOptions> commandLineOptions = CreateCommandLineOptions(currentPid);
        Mock<IEnvironment> environment = new();

        var listener = new NonCooperativeParentProcessListener(commandLineOptions.Object, environment.Object);

        listener.Dispose();
        listener.Dispose();
    }

    [TestMethod]
    public void Constructor_PidOptionNotSet_ThrowsGuardException()
    {
        Mock<ICommandLineOptions> commandLineOptions = new();
        string[]? pid = null;
        commandLineOptions
            .Setup(o => o.TryGetOptionArgumentList(PlatformCommandLineProvider.ExitOnProcessExitOptionKey, out pid))
            .Returns(false);
        Mock<IEnvironment> environment = new();

        Assert.ThrowsExactly<InvalidOperationException>(
            () => new NonCooperativeParentProcessListener(commandLineOptions.Object, environment.Object));
    }

    private static Mock<ICommandLineOptions> CreateCommandLineOptions(int pid)
    {
        Mock<ICommandLineOptions> commandLineOptions = new();
        string[]? pidArguments = [pid.ToString(CultureInfo.InvariantCulture)];
        commandLineOptions
            .Setup(o => o.TryGetOptionArgumentList(PlatformCommandLineProvider.ExitOnProcessExitOptionKey, out pidArguments))
            .Returns(true);
        return commandLineOptions;
    }

    private static int GetGuaranteedExitedProcessId()
    {
        // Spawn a short-lived process and wait for it to exit, so that we have a process id that is
        // guaranteed to no longer correspond to a running process.
        ProcessStartInfo startInfo = OperatingSystem.IsWindows()
            ? new ProcessStartInfo("cmd.exe", "/c exit") { UseShellExecute = false }
            : new ProcessStartInfo("/bin/sh", "-c exit") { UseShellExecute = false };

        using Process process = Process.Start(startInfo)!;
        process.WaitForExit();
        return process.Id;
    }
}
