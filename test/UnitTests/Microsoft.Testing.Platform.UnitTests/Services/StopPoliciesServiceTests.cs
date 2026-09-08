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
    public void IsTestExecutionCompleted_InitiallyFalse()
    {
        StopPoliciesService service = new(_cancellationTokenSource.Object);
        Assert.IsFalse(service.IsTestExecutionCompleted);
    }

    [TestMethod]
    public void NotifyTestExecutionCompleted_SetsIsTestExecutionCompleted()
    {
        StopPoliciesService service = new(_cancellationTokenSource.Object);

        service.NotifyTestExecutionStarting();
        service.NotifyTestExecutionCompleted();

        Assert.IsTrue(service.IsTestExecutionCompleted);
    }

    [TestMethod]
    public void NotifyTestExecutionStarting_AfterPreviousRequestCompleted_ClearsIsTestExecutionCompleted()
    {
        StopPoliciesService service = new(_cancellationTokenSource.Object);
        service.NotifyTestExecutionCompleted();

        Assert.IsTrue(service.IsTestExecutionCompleted);

        service.NotifyTestExecutionStarting();

        Assert.IsFalse(service.IsTestExecutionCompleted);
    }

    [TestMethod]
    public void NotifyTestExecutionCompleted_WithAnotherExecutionActive_DoesNotSetIsTestExecutionCompleted()
    {
        StopPoliciesService service = new(_cancellationTokenSource.Object);
        service.NotifyTestExecutionStarting();
        service.NotifyTestExecutionStarting();

        service.NotifyTestExecutionCompleted();

        Assert.IsFalse(service.IsTestExecutionCompleted);

        service.NotifyTestExecutionCompleted();

        Assert.IsTrue(service.IsTestExecutionCompleted);
    }

    [TestMethod]
    public void NotifyTestExecutionCompleted_DoesNotAffectDeadlineTriggered()
    {
        StopPoliciesService service = new(_cancellationTokenSource.Object);

        service.NotifyTestExecutionCompleted();

        // Completing execution gates future deadlines; it must not itself look like a deadline truncation.
        Assert.IsFalse(service.IsDeadlineTriggered);
    }

    [TestMethod]
    public void IsDeadlineTriggered_InitiallyFalse()
    {
        StopPoliciesService service = new(_cancellationTokenSource.Object);
        Assert.IsFalse(service.IsDeadlineTriggered);
    }

    [TestMethod]
    public async Task ExecuteDeadlineCallbacksAsync_SetsIsDeadlineTriggered()
    {
        StopPoliciesService service = new(_cancellationTokenSource.Object);

        await service.ExecuteDeadlineCallbacksAsync();

        Assert.IsTrue(service.IsDeadlineTriggered);
    }

    [TestMethod]
    public async Task RequestScopedServices_IsolateDeadlineVerdictAndExecutionCompletion()
    {
        StopPoliciesService firstRequest = new(_cancellationTokenSource.Object);
        StopPoliciesService secondRequest = new(_cancellationTokenSource.Object);
        firstRequest.NotifyTestExecutionStarting();
        secondRequest.NotifyTestExecutionStarting();

        await firstRequest.ExecuteDeadlineCallbacksAsync();
        secondRequest.NotifyTestExecutionCompleted();

        Assert.IsTrue(firstRequest.IsDeadlineTriggered);
        Assert.IsFalse(secondRequest.IsDeadlineTriggered);
        Assert.IsTrue(secondRequest.IsTestExecutionCompleted);
        Assert.IsFalse(firstRequest.IsTestExecutionCompleted);
    }

    [TestMethod]
    public async Task ExecuteDeadlineCallbacksAsync_InvokesRegisteredCallback()
    {
        StopPoliciesService service = new(_cancellationTokenSource.Object);

        int invocationCount = 0;
        await service.RegisterOnDeadlineCallbackAsync(() =>
        {
            invocationCount++;
            return Task.CompletedTask;
        });

        await service.ExecuteDeadlineCallbacksAsync();

        Assert.AreEqual(1, invocationCount);
        Assert.IsTrue(service.IsDeadlineTriggered);
    }

    [TestMethod]
    public async Task ExecuteDeadlineCallbacksAsync_IsOneShot()
    {
        StopPoliciesService service = new(_cancellationTokenSource.Object);

        int invocationCount = 0;
        await service.RegisterOnDeadlineCallbackAsync(() =>
        {
            invocationCount++;
            return Task.CompletedTask;
        });

        await service.ExecuteDeadlineCallbacksAsync();
        await service.ExecuteDeadlineCallbacksAsync();

        Assert.AreEqual(1, invocationCount);
    }

    [TestMethod]
    public async Task RegisterOnDeadlineCallbackAsync_InvokesCallbackExactlyOnceIfAlreadyTriggered()
    {
        StopPoliciesService service = new(_cancellationTokenSource.Object);
        await service.ExecuteDeadlineCallbacksAsync();

        int invocationCount = 0;
        await service.RegisterOnDeadlineCallbackAsync(() =>
        {
            invocationCount++;
            return Task.CompletedTask;
        });

        // The deadline is one-shot, so registering after it fired must invoke the callback right away and
        // must not leave it queued for a second, never-arriving trigger.
        await service.ExecuteDeadlineCallbacksAsync();

        Assert.AreEqual(1, invocationCount);
    }

    [TestMethod]
    public async Task RegisterOnDeadlineCallbackAsync_RacingTheTrigger_InvokesEveryCallbackExactlyOnce()
    {
        // Registration used to be able to lose a callback: the registering thread could read the trigger flag
        // as false, the trigger could then commit and snapshot a still-empty queue, and only afterwards would
        // the callback be enqueued -- where a one-shot deadline never reaches it. Hammer registration against
        // the trigger and assert every callback ran exactly once.
        for (int attempt = 0; attempt < 50; attempt++)
        {
            StopPoliciesService service = new(_cancellationTokenSource.Object);
            int[] invocationCounts = new int[8];

            // Async start gate so every task is released at the same moment. Barrier would be the obvious
            // choice but it is unsupported on browser, which this project targets.
            TaskCompletionSource<bool> start = new(TaskCreationOptions.RunContinuationsAsynchronously);

            var tasks = new List<Task>();
            for (int i = 0; i < invocationCounts.Length; i++)
            {
                int index = i;
                tasks.Add(Task.Run(
                    async () =>
                    {
                        await start.Task;
                        await service.RegisterOnDeadlineCallbackAsync(() =>
                        {
                            Interlocked.Increment(ref invocationCounts[index]);
                            return Task.CompletedTask;
                        });
                    },
                    TestContext.CancellationToken));
            }

            tasks.Add(Task.Run(
                async () =>
                {
                    await start.Task;
                    await service.ExecuteDeadlineCallbacksAsync();
                },
                TestContext.CancellationToken));

            start.SetResult(true);
            await Task.WhenAll(tasks);

            for (int i = 0; i < invocationCounts.Length; i++)
            {
                Assert.AreEqual(1, invocationCounts[i], $"Callback {i} was invoked {invocationCounts[i]} times on attempt {attempt}.");
            }
        }
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
    public async Task ExecuteAbortCallbacksAsync_WhenCalledTwice_InvokesRegisteredCallbackOnce()
    {
        StopPoliciesService service = new(_cancellationTokenSource.Object);
        int invocationCount = 0;
        await service.RegisterOnAbortCallbackAsync(() =>
        {
            invocationCount++;
            return Task.CompletedTask;
        });

        await service.ExecuteAbortCallbacksAsync();
        await service.ExecuteAbortCallbacksAsync();

        Assert.AreEqual(1, invocationCount);
    }

    [TestMethod]
    public async Task ExecuteAbortCallbacksAsync_WhenCalledConcurrently_ReturnsSharedTask()
    {
        StopPoliciesService service = new(_cancellationTokenSource.Object);
        TaskCompletionSource<bool> callbackStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource<bool> releaseCallback = new(TaskCreationOptions.RunContinuationsAsynchronously);
        await service.RegisterOnAbortCallbackAsync(async () =>
        {
            callbackStarted.SetResult(true);
            await releaseCallback.Task;
        });

        Task firstExecution = service.ExecuteAbortCallbacksAsync();
        await callbackStarted.Task;
        Task secondExecution = service.ExecuteAbortCallbacksAsync();

        Assert.AreSame(firstExecution, secondExecution);
        Assert.IsFalse(secondExecution.IsCompleted);

        releaseCallback.SetResult(true);
        await Task.WhenAll(firstExecution, secondExecution);
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

        Assert.AreEqual(1, invocationCount);
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
