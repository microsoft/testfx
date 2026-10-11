// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if !NETFRAMEWORK

using Microsoft.Testing.Extensions.PackagedApp;
using Microsoft.Testing.Platform.Extensions.TestHostControllers;

using Moq;

namespace Microsoft.Testing.Extensions.UnitTests;

[TestClass]
public sealed class PackagedAppNativeInformationalLaunchTests
{
    [TestMethod]
    [DataRow(true, false)]
    [DataRow(false, true)]
    public async Task RunAsync_DisabledLauncherCannotFallback(bool extensionEnabled, bool launcherEnabled)
    {
        var launcher = new Mock<ITestHostLauncher>();
        launcher.Setup(value => value.IsEnabledAsync()).ReturnsAsync(launcherEnabled);
        using StringWriter error = new();
        int exitCode = await PackagedAppNativeInformationalLaunch.RunAsync(
            launcher.Object, CreateContext(), extensionEnabled, error, CancellationToken.None);

        Assert.AreEqual(4, exitCode);
        Assert.Contains("No test host was activated.", error.ToString());
        launcher.Verify(value => value.LaunchTestHostAsync(It.IsAny<TestHostLaunchContext>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [TestMethod]
    public async Task RunAsync_LaunchFailureIsReportedAndCannotSucceed()
    {
        var launcher = new Mock<ITestHostLauncher>();
        launcher.Setup(value => value.IsEnabledAsync()).ReturnsAsync(true);
        launcher.Setup(value => value.LaunchTestHostAsync(It.IsAny<TestHostLaunchContext>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("registration failed"));
        using StringWriter error = new();
        int exitCode = await PackagedAppNativeInformationalLaunch.RunAsync(
            launcher.Object, CreateContext(), true, error, CancellationToken.None);

        Assert.AreEqual(4, exitCode);
        Assert.AreEqual("registration failed", error.ToString().Trim());
    }

    [TestMethod]
    [DataRow(0)]
    [DataRow(5)]
    public async Task RunAsync_ReturnsActualExitCodeAndDisposesHandle(int expectedExitCode)
    {
        TestHostLaunchContext context = CreateContext();
        var handle = new Mock<ITestHostHandle>();
        handle.Setup(value => value.WaitForExitAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        handle.SetupGet(value => value.ExitCode).Returns(expectedExitCode);
        var launcher = new Mock<ITestHostLauncher>();
        launcher.Setup(value => value.IsEnabledAsync()).ReturnsAsync(true);
        launcher.Setup(value => value.LaunchTestHostAsync(context, It.IsAny<CancellationToken>())).ReturnsAsync(handle.Object);
        using StringWriter error = new();
        int exitCode = await PackagedAppNativeInformationalLaunch.RunAsync(
            launcher.Object, context, true, error, CancellationToken.None);

        Assert.AreEqual(expectedExitCode, exitCode);
        Assert.AreEqual(string.Empty, error.ToString());
        Assert.AreSequenceEqual(["--list-tests"], context.Arguments);
        launcher.Verify(value => value.LaunchTestHostAsync(context, CancellationToken.None), Times.Once);
        handle.Verify(value => value.Dispose(), Times.Once);
        handle.Verify(value => value.Terminate(), Times.Never);
    }

    [TestMethod]
    public async Task RunAsync_CancellationTerminatesOwnedHostOnceAndDisposesHandle()
    {
        using CancellationTokenSource cancellation = new();
        TaskCompletionSource exited = new(TaskCreationOptions.RunContinuationsAsynchronously);
        var handle = new Mock<ITestHostHandle>();
        handle.Setup(value => value.WaitForExitAsync(It.IsAny<CancellationToken>())).Returns((CancellationToken token) =>
        {
            if (token.CanBeCanceled)
            {
                cancellation.Cancel();
            }

            return exited.Task.WaitAsync(token);
        });
        handle.Setup(value => value.Terminate()).Callback(() => exited.SetResult());
        var launcher = new Mock<ITestHostLauncher>();
        launcher.Setup(value => value.IsEnabledAsync()).ReturnsAsync(true);
        launcher.Setup(value => value.LaunchTestHostAsync(It.IsAny<TestHostLaunchContext>(), It.IsAny<CancellationToken>())).ReturnsAsync(handle.Object);
        using StringWriter error = new();
        int exitCode = await PackagedAppNativeInformationalLaunch.RunAsync(
            launcher.Object, CreateContext(), true, error, cancellation.Token);

        Assert.AreEqual(3, exitCode);
        handle.Verify(value => value.Terminate(), Times.Once);
        handle.Verify(value => value.Dispose(), Times.Once);
        Assert.AreEqual(string.Empty, error.ToString());
    }

    private static TestHostLaunchContext CreateContext()
        => new(Path.Combine(Path.GetTempPath(), "Tests.exe"), ["--list-tests"], new Dictionary<string, string?>(), Path.GetTempPath());

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task RunAsync_CancellationHandlesCooperativeExitAndTerminationFailure(bool terminationFails)
    {
        using CancellationTokenSource cancellation = new();
        TaskCompletionSource exited = new(TaskCreationOptions.RunContinuationsAsynchronously);
        var handle = new Mock<ITestHostHandle>();
        handle.Setup(value => value.WaitForExitAsync(It.IsAny<CancellationToken>())).Returns((CancellationToken token) =>
        {
            if (token.CanBeCanceled)
            {
                cancellation.Cancel();
                return Task.FromCanceled(token);
            }

            return terminationFails ? exited.Task : Task.CompletedTask;
        });
        handle.Setup(value => value.Terminate()).Throws(new InvalidOperationException("termination failed"));
        var launcher = new Mock<ITestHostLauncher>();
        launcher.Setup(value => value.IsEnabledAsync()).ReturnsAsync(true);
        launcher.Setup(value => value.LaunchTestHostAsync(It.IsAny<TestHostLaunchContext>(), It.IsAny<CancellationToken>())).ReturnsAsync(handle.Object);
        using StringWriter error = new();
        int exitCode = await PackagedAppNativeInformationalLaunch.RunAsync(
            launcher.Object, CreateContext(), true, error, cancellation.Token);

        Assert.AreEqual(3, exitCode);
        handle.Verify(value => value.Terminate(), terminationFails ? Times.Once() : Times.Never());
        handle.Verify(value => value.Dispose(), Times.Once);
        Assert.AreEqual(terminationFails ? "termination failed" : string.Empty, error.ToString().Trim());
    }
}

#endif
