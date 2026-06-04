// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Services;

namespace Microsoft.Testing.Platform.UnitTests;

[TestClass]
public sealed class CTRLPlusCCancellationTokenSourceTests
{
    [TestMethod]
    public void Initial_State_NeitherTokenIsCancelled()
    {
        using var source = new CTRLPlusCCancellationTokenSource(
            console: null,
            logger: null,
            gracePeriod: Timeout.InfiniteTimeSpan,
            abortTimeout: Timeout.InfiniteTimeSpan);

        Assert.IsFalse(source.CancellationToken.IsCancellationRequested);
        Assert.IsFalse(source.DrainingToken.IsCancellationRequested);
        Assert.IsFalse(source.AbortingToken.IsCancellationRequested);
    }

    [TestMethod]
    public void Cancel_OnlySignalsDrainingToken()
    {
        using var source = new CTRLPlusCCancellationTokenSource(
            console: null,
            logger: null,
            gracePeriod: Timeout.InfiniteTimeSpan,
            abortTimeout: Timeout.InfiniteTimeSpan);

        source.Cancel();

        Assert.IsTrue(source.DrainingToken.IsCancellationRequested);
        Assert.IsTrue(source.CancellationToken.IsCancellationRequested, "Legacy alias must follow DrainingToken.");
        Assert.IsFalse(source.AbortingToken.IsCancellationRequested);
    }

    [TestMethod]
    public void Abort_SignalsBothTokens()
    {
        using var source = new CTRLPlusCCancellationTokenSource(
            console: null,
            logger: null,
            gracePeriod: Timeout.InfiniteTimeSpan,
            abortTimeout: Timeout.InfiniteTimeSpan);

        source.Abort();

        Assert.IsTrue(source.DrainingToken.IsCancellationRequested);
        Assert.IsTrue(source.AbortingToken.IsCancellationRequested);
    }

    [TestMethod]
    public void Cancel_IsIdempotent()
    {
        using var source = new CTRLPlusCCancellationTokenSource(
            console: null,
            logger: null,
            gracePeriod: Timeout.InfiniteTimeSpan,
            abortTimeout: Timeout.InfiniteTimeSpan);

        source.Cancel();
        source.Cancel();
        source.Cancel();

        Assert.IsTrue(source.DrainingToken.IsCancellationRequested);
        Assert.IsFalse(source.AbortingToken.IsCancellationRequested);
    }

    [TestMethod]
    public async Task GracePeriodElapse_EscalatesToAborting()
    {
        using var source = new CTRLPlusCCancellationTokenSource(
            console: null,
            logger: null,
            gracePeriod: TimeSpan.FromMilliseconds(50),
            abortTimeout: Timeout.InfiniteTimeSpan);

        source.Cancel();
        Assert.IsTrue(source.DrainingToken.IsCancellationRequested);
        Assert.IsFalse(source.AbortingToken.IsCancellationRequested);

        // Event-driven wait: complete a TaskCompletionSource as soon as the token
        // is canceled rather than polling, so the test finishes immediately after
        // the grace period elapses (avoids Task.Delay flakiness under load).
        var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        using CancellationTokenRegistration registration = source.AbortingToken.Register(() => tcs.TrySetResult(true));

        Task timeoutTask = Task.Delay(TimeSpan.FromSeconds(5), TestContext.CancellationToken);
        Task completed = await Task.WhenAny(tcs.Task, timeoutTask).ConfigureAwait(false);

        Assert.AreSame(tcs.Task, completed, "Aborting must trip before the timeout.");
        Assert.IsTrue(source.AbortingToken.IsCancellationRequested, "Aborting must trip after the grace period.");
    }

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public void ZeroGracePeriod_ImmediatelyEscalatesToAborting()
    {
        using var source = new CTRLPlusCCancellationTokenSource(
            console: null,
            logger: null,
            gracePeriod: TimeSpan.Zero,
            abortTimeout: Timeout.InfiniteTimeSpan);

        source.Cancel();

        Assert.IsTrue(source.DrainingToken.IsCancellationRequested);
        Assert.IsTrue(source.AbortingToken.IsCancellationRequested);
    }
}
