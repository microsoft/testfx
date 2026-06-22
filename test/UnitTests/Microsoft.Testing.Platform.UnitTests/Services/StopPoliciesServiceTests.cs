// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.Services;

using Moq;

namespace Microsoft.Testing.Platform.UnitTests;

[TestClass]
public sealed class StopPoliciesServiceTests : IDisposable
{
    private readonly Mock<ITestApplicationCancellationTokenSource> _cancellationTokenSource = new();
    private readonly CancellationTokenSource _cts = new();

    public TestContext TestContext { get; set; } = null!;

    public void Dispose() => _cts.Dispose();

    [TestInitialize]
    public void Initialize()
        => _cancellationTokenSource.SetupGet(x => x.CancellationToken).Returns(_cts.Token);

    [TestMethod]
    public void IsMaxFailedTestsTriggered_InitiallyFalse()
    {
        StopPoliciesService service = new(_cancellationTokenSource.Object);
        Assert.IsFalse(service.IsMaxFailedTestsTriggered);
    }

    [TestMethod]
    public void IsAbortTriggered_InitiallyFalse()
    {
        StopPoliciesService service = new(_cancellationTokenSource.Object);
        Assert.IsFalse(service.IsAbortTriggered);
    }

    [TestMethod]
    public async Task ExecuteMaxFailedTestsCallbacksAsync_SetsIsMaxFailedTestsTriggered()
    {
        StopPoliciesService service = new(_cancellationTokenSource.Object);

        await service.ExecuteMaxFailedTestsCallbacksAsync(5, CancellationToken.None);

        Assert.IsTrue(service.IsMaxFailedTestsTriggered);
    }

    [TestMethod]
    public async Task ExecuteMaxFailedTestsCallbacksAsync_InvokesRegisteredCallback()
    {
        StopPoliciesService service = new(_cancellationTokenSource.Object)
        {
            ProcessRole = TestProcessRole.TestHost,
        };

        int capturedMaxFailedTests = -1;
        await service.RegisterOnMaxFailedTestsCallbackAsync((count, _) =>
        {
            capturedMaxFailedTests = count;
            return Task.CompletedTask;
        });

        await service.ExecuteMaxFailedTestsCallbacksAsync(7, CancellationToken.None);

        Assert.AreEqual(7, capturedMaxFailedTests);
    }

    [TestMethod]
    public async Task ExecuteAbortCallbacksAsync_SetsIsAbortTriggered()
    {
        StopPoliciesService service = new(_cancellationTokenSource.Object);

        await service.ExecuteAbortCallbacksAsync();

        Assert.IsTrue(service.IsAbortTriggered);
    }

    [TestMethod]
    public async Task ExecuteAbortCallbacksAsync_InvokesRegisteredCallback()
    {
        StopPoliciesService service = new(_cancellationTokenSource.Object);

        bool callbackInvoked = false;
        await service.RegisterOnAbortCallbackAsync(() =>
        {
            callbackInvoked = true;
            return Task.CompletedTask;
        });

        await service.ExecuteAbortCallbacksAsync();

        Assert.IsTrue(callbackInvoked);
    }

    [TestMethod]
    public async Task RegisterOnMaxFailedTestsCallbackAsync_ThrowsIfNotTestHost()
    {
        foreach (TestProcessRole? processRole in new TestProcessRole?[] { null, TestProcessRole.TestHostController })
        {
            StopPoliciesService service = new(_cancellationTokenSource.Object)
            {
                ProcessRole = processRole,
            };

            // UnreachableException is an internal, per-assembly polyfill on non-NETCOREAPP TFMs, so asserting on the
            // generic type parameter would compare against this test assembly's copy and fail due to type identity
            // mismatch across assemblies. Assert on the full type name instead.
            Exception exception = await Assert.ThrowsAsync<Exception>(
                () => service.RegisterOnMaxFailedTestsCallbackAsync((_, _) => Task.CompletedTask));
            Assert.AreEqual(typeof(global::System.Diagnostics.UnreachableException).FullName, exception.GetType().FullName);
        }
    }

    [TestMethod]
    public async Task RegisterOnMaxFailedTestsCallbackAsync_ImmediatelyInvokesCallbackIfAlreadyTriggered()
    {
        StopPoliciesService service = new(_cancellationTokenSource.Object)
        {
            ProcessRole = TestProcessRole.TestHost,
        };
        await service.ExecuteMaxFailedTestsCallbacksAsync(3, CancellationToken.None);

        int invocationCount = 0;
        int capturedCount = -1;
        await service.RegisterOnMaxFailedTestsCallbackAsync((count, _) =>
        {
            invocationCount++;
            capturedCount = count;
            return Task.CompletedTask;
        });

        await service.ExecuteMaxFailedTestsCallbacksAsync(10, CancellationToken.None);

        Assert.AreEqual(2, invocationCount);
        Assert.AreEqual(10, capturedCount);
    }

    [TestMethod]
    public async Task RegisterOnAbortCallbackAsync_ImmediatelyInvokesCallbackIfAlreadyTriggered()
    {
        StopPoliciesService service = new(_cancellationTokenSource.Object);
        await service.ExecuteAbortCallbacksAsync();

        int invocationCount = 0;
        await service.RegisterOnAbortCallbackAsync(() =>
        {
            invocationCount++;
            return Task.CompletedTask;
        });

        await service.ExecuteAbortCallbacksAsync();

        Assert.AreEqual(2, invocationCount);
    }

    [TestMethod]
    public async Task CancellationToken_Cancelled_TriggersAbortCallbacks()
    {
        StopPoliciesService service = new(_cancellationTokenSource.Object);

        TaskCompletionSource<bool> callbackInvoked = new(TaskCreationOptions.RunContinuationsAsynchronously);
        await service.RegisterOnAbortCallbackAsync(() =>
        {
            callbackInvoked.TrySetResult(true);
            return Task.CompletedTask;
        });

#if NETCOREAPP
        await _cts.CancelAsync();
#else
        _cts.Cancel();
#endif

        Task completedTask = await Task.WhenAny(callbackInvoked.Task, Task.Delay(TimeSpan.FromSeconds(5), TestContext.CancellationToken));
        Assert.AreSame(callbackInvoked.Task, completedTask);

        await callbackInvoked.Task;
    }
}
