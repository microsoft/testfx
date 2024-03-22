// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Internal.Framework;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.TestInfrastructure;

namespace Microsoft.Testing.Platform.UnitTests;

[TestGroup]
public sealed class TaskExtensionsTests : TestBase
{
    public TaskExtensionsTests(ITestExecutionContext testExecutionContext)
        : base(testExecutionContext)
    {
    }

    public async Task TimeoutAfterAsync_Succeeds()
        => await Assert.ThrowsAsync<TimeoutException>(async ()
            => await Task.Delay(TimeSpan.FromSeconds(60)).TimeoutAfterAsync(TimeSpan.FromSeconds(2)));

    public async Task TimeoutAfterAsync_CancellationToken_Succeeds()
        => await Assert.ThrowsAsync<TaskCanceledException>(async () =>
            await Task.Delay(TimeSpan.FromSeconds(60)).TimeoutAfterAsync(
                TimeSpan.FromSeconds(30),
                new CancellationTokenSource(TimeSpan.FromSeconds(2)).Token));

    public async Task TimeoutAfterAsync_CancellationTokenNone_Succeeds()
        => await Assert.ThrowsAsync<TimeoutException>(async () =>
            await Task.Delay(TimeSpan.FromSeconds(60)).TimeoutAfterAsync(
                TimeSpan.FromSeconds(2),
                CancellationToken.None));

    public async Task CancellationAsync_Cancellation_Succeeds()
    {
        CancellationTokenSource cancellationTokenSource = new();
        var task = Task.Run(async () => await Task.Delay(-1).WithCancellationAsync(cancellationTokenSource.Token));
#pragma warning disable VSTHRD103 // Call async methods when in an async method
        cancellationTokenSource.Cancel();
#pragma warning restore VSTHRD103 // Call async methods when in an async method
        OperationCanceledException exception = await Assert.ThrowsAsync<OperationCanceledException>(async () => await task);
        Assert.AreEqual(cancellationTokenSource.Token, exception.CancellationToken);
    }

    public async Task CancellationAsync_CancellationWithArgument_Succeeds()
    {
        CancellationTokenSource cancellationTokenSource = new();
        var task = Task.Run(async () => await DoSomething().WithCancellationAsync(cancellationTokenSource.Token));
#pragma warning disable VSTHRD103 // Call async methods when in an async method
        cancellationTokenSource.Cancel();
#pragma warning restore VSTHRD103 // Call async methods when in an async method
        OperationCanceledException exception = await Assert.ThrowsAsync<OperationCanceledException>(async () => await task);
        Assert.AreEqual(cancellationTokenSource.Token, exception.CancellationToken);
    }

    public async Task CancellationAsync_NonCancelled_Succeeds()
    {
        CancellationTokenSource cancellationTokenSource = new();
        await Task.Delay(TimeSpan.FromSeconds(1)).WithCancellationAsync(cancellationTokenSource.Token);
    }

    public async Task CancellationAsync_NonCancelledWithArgument_Succeeds()
    {
        CancellationTokenSource cancellationTokenSource = new();
        Assert.AreEqual("Hello", await DoSomething().WithCancellationAsync(cancellationTokenSource.Token));
    }

    public async Task CancellationAsync_ObserveException_Succeeds()
        => await RetryHelper.RetryAsync(
            async () =>
            {
                ManualResetEvent waitException = new(false);
                OperationCanceledException exception = await Assert.ThrowsAsync<OperationCanceledException>(async ()
                    => await Task.Run(async () =>
                    {
                        await Task.Delay(TimeSpan.FromSeconds(10));
                        waitException.Set();
                        throw new InvalidOperationException();
                    }).WithCancellationAsync(new CancellationTokenSource(TimeSpan.FromSeconds(1)).Token));

                waitException.WaitOne();
                await Task.Delay(TimeSpan.FromSeconds(4));
            }, 3, TimeSpan.FromSeconds(3), _ => true);

    public async Task CancellationAsyncWithReturnValue_ObserveException_Succeeds()
        => await RetryHelper.RetryAsync(
            async () =>
            {
                ManualResetEvent waitException = new(false);
                OperationCanceledException exception = await Assert.ThrowsAsync<OperationCanceledException>(async ()
                    => await Task.Run(async () =>
                    {
                        await Task.Delay(TimeSpan.FromSeconds(10));
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
                    }).WithCancellationAsync(new CancellationTokenSource(TimeSpan.FromSeconds(1)).Token));

                waitException.WaitOne();
                await Task.Delay(TimeSpan.FromSeconds(4));
            }, 3, TimeSpan.FromSeconds(3), _ => true);

    private async Task<string> DoSomething()
    {
        await Task.Delay(TimeSpan.FromSeconds(1));
        return "Hello";
    }
}
