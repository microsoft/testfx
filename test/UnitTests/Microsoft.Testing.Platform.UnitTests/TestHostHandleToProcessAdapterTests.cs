// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Extensions.TestHostControllers;
using Microsoft.Testing.Platform.Helpers;

using Moq;

namespace Microsoft.Testing.Platform.UnitTests;

[TestClass]
[UnsupportedOSPlatform("browser")]
public sealed class TestHostHandleToProcessAdapterTests
{
    [TestMethod]
    public void TrustedProcessId_ForLocalProcessHandle_ReturnsLauncherProcessId()
    {
        using var adapter = new TestHostHandleToProcessAdapter(new LocalTestHostHandle(42));

        Assert.AreEqual(42, adapter.TrustedProcessId);
    }

    [TestMethod]
    public void TrustedProcessId_ForMechanismAgnosticHandle_IsNull()
    {
        using var adapter = new TestHostHandleToProcessAdapter(new RemoteTestHostHandle());

        Assert.IsNull(adapter.TrustedProcessId);
    }

    private class RemoteTestHostHandle : ITestHostHandle
    {
        public string? Identifier => "remote:42";

        public int ExitCode => 0;

        public bool HasExited => false;

        public Task WaitForExitAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public void Terminate()
        {
        }

        public void Dispose()
        {
        }
    }

    private sealed class LocalTestHostHandle(int processId) : RemoteTestHostHandle, ILocalTestHostHandle
    {
        public int ProcessId { get; } = processId;
    }

    [TestMethod]
    public async Task Adapter_NonlocalCapabilitiesWaitShapesAndTerminationPreserveHandleContract()
    {
        using CancellationTokenSource caller = new();
        Mock<ITestHostHandle> handle = new();
        handle.SetupGet(h => h.ExitCode).Returns(17);
        handle.SetupGet(h => h.HasExited).Returns(false);
        TaskCompletionSource<bool> monitor = new(TaskCreationOptions.RunContinuationsAsynchronously);
        CancellationToken monitorToken = default;
        handle.Setup(h => h.WaitForExitAsync(It.IsAny<CancellationToken>())).Returns<CancellationToken>(token =>
        {
            if (token.CanBeCanceled && token != caller.Token)
            {
                monitorToken = token;
                return monitor.Task.WaitAsync(token);
            }

            return Task.CompletedTask;
        });
        TestHostHandleToProcessAdapter adapter = new(handle.Object);
        Assert.AreEqual(
            "The test host launcher does not expose a numeric process id; use 'ITestHostHandle.Identifier' for diagnostics.",
            Assert.ThrowsExactly<InvalidOperationException>(() => _ = adapter.Id).Message);
        Assert.AreEqual(string.Empty, adapter.Name);
        Assert.IsNull(adapter.MainModule);
        Assert.AreEqual(DateTime.MinValue, adapter.StartTime);
        Assert.AreEqual(17, adapter.ExitCode);
        Assert.IsFalse(adapter.HasExited);
        Func<Task> waitWithoutToken = adapter.WaitForExitAsync;
        await waitWithoutToken();
        await adapter.WaitForExitAsync(caller.Token);
        adapter.WaitForExit();
        adapter.Kill();
        adapter.Dispose();
        adapter.Dispose();

        Assert.IsTrue(monitorToken.IsCancellationRequested);
        handle.Verify(h => h.WaitForExitAsync(CancellationToken.None), Times.Exactly(2));
        handle.Verify(h => h.WaitForExitAsync(caller.Token), Times.Once);
        handle.Verify(h => h.Terminate(), Times.Once);
        handle.Verify(h => h.Dispose(), Times.Once);
    }

    [TestMethod]
    public async Task Adapter_ExitEventUsesAdapterSenderAndRemovalPreservesRemainingSubscriber()
    {
        Mock<ITestHostHandle> handle = new();
        TaskCompletionSource<bool> exit = new(TaskCreationOptions.RunContinuationsAsynchronously);
        handle.Setup(h => h.WaitForExitAsync(It.IsAny<CancellationToken>())).Returns<CancellationToken>(token => exit.Task.WaitAsync(token));
        using TestHostHandleToProcessAdapter adapter = new(handle.Object);
        int removed = 0;
        EventHandler first = (_, _) => removed++;
        TaskCompletionSource<object?> observed = new(TaskCreationOptions.RunContinuationsAsynchronously);
        adapter.Exited += first;
        adapter.Exited += (sender, args) =>
        {
            Assert.AreSame(EventArgs.Empty, args);
            observed.TrySetResult(sender);
        };
        adapter.Exited -= first;

        exit.SetResult(true);
        await observed.Task.TimeoutAfterAsync(TimeoutHelper.DefaultHangTimeSpanTimeout);

        Assert.AreSame(adapter, await observed.Task);
        Assert.AreEqual(0, removed);
    }
}
