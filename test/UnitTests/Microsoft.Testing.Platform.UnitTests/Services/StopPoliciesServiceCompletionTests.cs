// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Services;

using Moq;

namespace Microsoft.Testing.Platform.UnitTests;

[TestClass]
public sealed class StopPoliciesServiceCompletionTests : IDisposable
{
    private readonly Mock<ITestApplicationCancellationTokenSource> _cancellationTokenSource = new();
    private readonly CancellationTokenSource _cts = new();

    public StopPoliciesServiceCompletionTests()
        => _cancellationTokenSource.SetupGet(x => x.CancellationToken).Returns(_cts.Token);

    public void Dispose() => _cts.Dispose();

    [TestMethod]
    public async Task ExecuteAbortCallbacksAsync_WhenCallbacksSucceed_ReturnedTaskCompletesSuccessfully()
    {
        StopPoliciesService service = new(_cancellationTokenSource.Object);
        TaskCompletionSource<bool> gate = new(TaskCreationOptions.RunContinuationsAsynchronously);
        await service.RegisterOnAbortCallbackAsync(async () => await gate.Task);

        Task task = service.ExecuteAbortCallbacksAsync();

        Assert.IsFalse(task.IsCompleted);
        gate.SetResult(true);
        await BoundedTaskWait.CompleteWithinAsync(task);
        Assert.AreEqual(TaskStatus.RanToCompletion, task.Status);
    }

    [TestMethod]
    public async Task ExecuteAbortCallbacksAsync_WhenCallbackThrows_ReturnedTaskFaultsWithThatException()
    {
        StopPoliciesService service = new(_cancellationTokenSource.Object);
        InvalidOperationException expected = new("callback failed");
        await service.RegisterOnAbortCallbackAsync(() => throw expected);

        Task task = service.ExecuteAbortCallbacksAsync();

        InvalidOperationException actual = await Assert.ThrowsExactlyAsync<InvalidOperationException>(async () => await BoundedTaskWait.CompleteWithinAsync(task));
        Assert.AreSame(expected, actual);
        Assert.AreEqual(TaskStatus.Faulted, task.Status);
    }

    [TestMethod]
    public async Task ExecuteAbortCallbacksAsync_ClearsCallbacks_SoLateRegistrationRunsOnceAndNotTwice()
    {
        StopPoliciesService service = new(_cancellationTokenSource.Object);
        int early = 0;
        int late = 0;
        await service.RegisterOnAbortCallbackAsync(() =>
        {
            early++;
            return Task.CompletedTask;
        });

        await BoundedTaskWait.CompleteWithinAsync(service.ExecuteAbortCallbacksAsync());
        await service.RegisterOnAbortCallbackAsync(() =>
        {
            late++;
            return Task.CompletedTask;
        });
        await BoundedTaskWait.CompleteWithinAsync(service.ExecuteAbortCallbacksAsync());

        Assert.AreEqual(1, early);
        Assert.AreEqual(1, late);
    }
}
