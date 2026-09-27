// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.ServerMode.Client.Sources.UnitTests;

/// <summary>
/// Tests for <see cref="MtpServerConnector.WaitBoundedAsync(Task, TimeSpan)"/>, the bounded-wait helper shared
/// by every teardown path that must never block indefinitely on a task it does not own.
/// </summary>
[TestClass]
public sealed class MtpServerConnectorTests
{
    [TestMethod]
    public async Task WaitBoundedAsync_TaskAlreadyCompleted_ReturnsTrueWithoutWaiting()
    {
        Task completedTask = Task.CompletedTask;

        // A non-positive timeout would normally short-circuit to false, so passing one here specifically
        // proves the already-completed check runs (and wins) before the timeout is ever inspected.
        bool result = await MtpServerConnector.WaitBoundedAsync(completedTask, TimeSpan.Zero).ConfigureAwait(false);

        Assert.IsTrue(result, "An already-completed task must report completion regardless of the timeout.");
    }

    [TestMethod]
    public async Task WaitBoundedAsync_NonPositiveTimeoutAndIncompleteTask_ReturnsFalseWithoutWaiting()
    {
        var neverCompletes = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        bool result = await MtpServerConnector.WaitBoundedAsync(neverCompletes.Task, TimeSpan.Zero).ConfigureAwait(false);

        Assert.IsFalse(result, "A non-positive timeout must degrade to 'check, do not wait' rather than waiting forever.");
    }

    [TestMethod]
    public async Task WaitBoundedAsync_TaskCompletesBeforeTimeout_ReturnsTrue()
    {
        var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        Task<bool> waitTask = MtpServerConnector.WaitBoundedAsync(tcs.Task, TimeSpan.FromSeconds(30));

        tcs.TrySetResult(true);

        bool result = await waitTask.ConfigureAwait(false);

        Assert.IsTrue(result, "The task completing before the timeout elapses must report completion.");
    }

    [TestMethod]
    public async Task WaitBoundedAsync_TimeoutElapsesBeforeTaskCompletes_ReturnsFalse()
    {
        var neverCompletes = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        bool result = await MtpServerConnector.WaitBoundedAsync(neverCompletes.Task, TimeSpan.FromMilliseconds(50)).ConfigureAwait(false);

        Assert.IsFalse(result, "A short timeout against a task that never completes must report a timeout, not completion.");
    }
}
