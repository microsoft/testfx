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
        StopPoliciesService service = new(_cancellationTokenSource.Object)
        {
            ProcessRole = TestProcessRole.TestHostController,
        };

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(
            () => service.RegisterOnMaxFailedTestsCallbackAsync((_, _) => Task.CompletedTask));
    }

    [TestMethod]
    public async Task RegisterOnMaxFailedTestsCallbackAsync_ImmediatelyInvokesCallbackIfAlreadyTriggered()
    {
        StopPoliciesService service = new(_cancellationTokenSource.Object)
        {
            ProcessRole = TestProcessRole.TestHost,
        };
        await service.ExecuteMaxFailedTestsCallbacksAsync(3, CancellationToken.None);

        int capturedCount = -1;
        await service.RegisterOnMaxFailedTestsCallbackAsync((count, _) =>
        {
            capturedCount = count;
            return Task.CompletedTask;
        });

        Assert.AreEqual(3, capturedCount);
    }

    [TestMethod]
    public async Task RegisterOnAbortCallbackAsync_ImmediatelyInvokesCallbackIfAlreadyTriggered()
    {
        StopPoliciesService service = new(_cancellationTokenSource.Object);
        await service.ExecuteAbortCallbacksAsync();

        bool callbackInvoked = false;
        await service.RegisterOnAbortCallbackAsync(() =>
        {
            callbackInvoked = true;
            return Task.CompletedTask;
        });

        Assert.IsTrue(callbackInvoked);
    }

    [TestMethod]
    public async Task CancellationToken_Cancelled_TriggersAbortCallbacks()
    {
        StopPoliciesService service = new(_cancellationTokenSource.Object);

        bool callbackInvoked = false;
        await service.RegisterOnAbortCallbackAsync(() =>
        {
            callbackInvoked = true;
            return Task.CompletedTask;
        });

        await _cts.CancelAsync();

        // Allow async callback time to complete
        await Task.Delay(200, TestContext.CancellationToken);

        Assert.IsTrue(callbackInvoked);
    }
}
