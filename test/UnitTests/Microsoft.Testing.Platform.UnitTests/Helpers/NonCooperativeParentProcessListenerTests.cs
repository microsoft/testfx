// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Helpers;

using Microsoft.Win32.SafeHandles;

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
    public async Task ParentProcessExited_CallsEnvironmentExit()
    {
        using Process parentProcess = StartBlockingProcess();
        try
        {
            Mock<ICommandLineOptions> commandLineOptions = CreateCommandLineOptions(parentProcess.Id);
            Mock<IEnvironment> environment = new();
            TaskCompletionSource<bool> exitCalled = new(TaskCreationOptions.RunContinuationsAsynchronously);
            environment
                .Setup(e => e.Exit((int)ExitCode.DependentProcessExited))
                .Callback(() => exitCalled.TrySetResult(true));

            using var listener = new NonCooperativeParentProcessListener(commandLineOptions.Object, environment.Object);

            environment.Verify(e => e.Exit(It.IsAny<int>()), Times.Never);

            parentProcess.StandardInput.Close();
            await exitCalled.Task.TimeoutAfterAsync(TimeoutHelper.DefaultHangTimeSpanTimeout);
            environment.Verify(e => e.Exit((int)ExitCode.DependentProcessExited), Times.Once);
        }
        finally
        {
            if (!parentProcess.HasExited)
            {
                parentProcess.Kill();
            }
        }
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
        Process parentProcess = GetParentProcess(listener);
        SafeProcessHandle processHandle = parentProcess.SafeHandle;

        listener.Dispose();
        Assert.IsTrue(processHandle.IsClosed);
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
        ProcessStartInfo startInfo = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? new ProcessStartInfo("cmd.exe", "/c exit") { UseShellExecute = false }
            : new ProcessStartInfo("/bin/sh", "-c exit") { UseShellExecute = false };

        using Process process = Process.Start(startInfo)!;
        process.WaitForExit();
        return process.Id;
    }

    private static Process StartBlockingProcess()
    {
        ProcessStartInfo startInfo = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? new ProcessStartInfo("cmd.exe", "/d /c more")
            : new ProcessStartInfo("/bin/sh", "-c cat");
        startInfo.UseShellExecute = false;
        startInfo.RedirectStandardInput = true;
        startInfo.CreateNoWindow = true;

        Process process = Process.Start(startInfo)!;
        Assert.IsFalse(process.HasExited);
        return process;
    }

    private static Process GetParentProcess(NonCooperativeParentProcessListener listener)
    {
        FieldInfo parentProcessField = typeof(NonCooperativeParentProcessListener).GetField(
            "_parentProcess", BindingFlags.NonPublic | BindingFlags.Instance)!;
        return (Process)parentProcessField.GetValue(listener)!;
    }
}
