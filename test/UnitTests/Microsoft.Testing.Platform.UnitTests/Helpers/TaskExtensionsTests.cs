// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Helpers;

namespace Microsoft.Testing.Platform.UnitTests;

[TestClass]
public sealed class TaskExtensionsTests
{
    public TestContext TestContext { get; set; }

    [TestMethod]
    public async Task TimeoutAfterAsync_Succeeds()
        => await Assert.ThrowsAsync<TimeoutException>(async () =>
            await Task.Delay(TimeSpan.FromSeconds(60), TestContext.CancellationToken).TimeoutAfterAsync(TimeSpan.FromSeconds(2)));

    [TestMethod]
    public async Task TimeoutAfterAsync_CancellationToken_Succeeds()
        => await Assert.ThrowsAsync<OperationCanceledException>(async () =>
            await Task.Delay(TimeSpan.FromSeconds(60), TestContext.CancellationToken).TimeoutAfterAsync(
                TimeSpan.FromSeconds(30),
                new CancellationTokenSource(TimeSpan.FromSeconds(2)).Token));

    [TestMethod]
    public async Task TimeoutAfterAsync_CancellationTokenNone_Succeeds()
        => await Assert.ThrowsAsync<TimeoutException>(async () =>
            await Task.Delay(TimeSpan.FromSeconds(60), TestContext.CancellationToken).TimeoutAfterAsync(
                TimeSpan.FromSeconds(2),
                CancellationToken.None));

    [TestMethod]
    public async Task CancellationAsync_Cancellation_Succeeds()
    {
        CancellationTokenSource cancellationTokenSource = new();
        CancellationToken cancelToken = cancellationTokenSource.Token;
        var task = Task.Run(async () => await Task.Delay(-1, cancelToken).WithCancellationAsync(cancelToken), cancelToken);
#pragma warning disable VSTHRD103 // Call async methods when in an async method
        cancellationTokenSource.Cancel();
#pragma warning restore VSTHRD103 // Call async methods when in an async method
        OperationCanceledException exception = await Assert.ThrowsAsync<OperationCanceledException>(async () => await task);
        Assert.AreEqual(cancelToken, exception.CancellationToken);
    }

    [TestMethod]
    public async Task CancellationAsync_CancellationWithArgument_Succeeds()
    {
        CancellationTokenSource cancellationTokenSource = new();
        CancellationToken cancelToken = cancellationTokenSource.Token;
        Task<string> task = Task.Run(async () => await DoSomething().WithCancellationAsync(cancelToken), cancelToken);
#pragma warning disable VSTHRD103 // Call async methods when in an async method
        cancellationTokenSource.Cancel();
#pragma warning restore VSTHRD103 // Call async methods when in an async method
        OperationCanceledException exception = await Assert.ThrowsAsync<OperationCanceledException>(async () => await task);
        Assert.AreEqual(cancelToken, exception.CancellationToken);
    }

    [TestMethod]
    public async Task CancellationAsync_NonCanceled_Succeeds()
    {
        CancellationTokenSource cancellationTokenSource = new();
        CancellationToken cancelToken = cancellationTokenSource.Token;
        await Task.Delay(TimeSpan.FromSeconds(1), cancelToken).WithCancellationAsync(cancelToken);
    }

    [TestMethod]
    public async Task CancellationAsync_NonCanceledWithArgument_Succeeds()
    {
        CancellationTokenSource cancellationTokenSource = new();
        Assert.AreEqual("Hello", await DoSomething().WithCancellationAsync(cancellationTokenSource.Token));
    }

    [TestMethod]
    public async Task CancellationAsync_ObserveException_Succeeds()
    {
        ManualResetEvent waitException = new(false);
        CancellationToken token = new CancellationTokenSource(TimeSpan.FromSeconds(1)).Token;
        OperationCanceledException ex = await Assert.ThrowsAsync<OperationCanceledException>(async ()
            => await Task.Run(
                async () =>
                {
                    await Task.Delay(TimeSpan.FromSeconds(5), TestContext.CancellationToken);
                    waitException.Set();
                    throw new InvalidOperationException();
                }, TestContext.CancellationToken).WithCancellationAsync(token));
#if !NETFRAMEWORK // Polyfill bug in Task.WaitAsync implementation :/
        Assert.AreEqual(token, ex.CancellationToken);
#endif
        waitException.WaitOne();
    }

    [TestMethod]
    public async Task CancellationAsyncWithReturnValue_ObserveException_Succeeds()
    {
        ManualResetEvent waitException = new(false);
        CancellationToken token = new CancellationTokenSource(TimeSpan.FromSeconds(1)).Token;
        OperationCanceledException ex = await Assert.ThrowsAsync<OperationCanceledException>(async ()
            => await Task.Run(async () =>
            {
                await Task.Delay(TimeSpan.FromSeconds(5), TestContext.CancellationToken);
                try
                {
                    return 2;
                }
                finally
                {
                    waitException.Set();
#pragma warning disable CA2219 // Do not raise exceptions in finally clauses
                    throw new InvalidOperationException();
#pragma warning restore CA2219 // Do not raise exceptions in finally clauses
                }
            }).WithCancellationAsync(token));
        Assert.AreEqual(token, ex.CancellationToken);
        waitException.WaitOne();
    }

    private static async Task<string> DoSomething()
    {
        await Task.Delay(TimeSpan.FromSeconds(1));
        return "Hello";
    }
}
