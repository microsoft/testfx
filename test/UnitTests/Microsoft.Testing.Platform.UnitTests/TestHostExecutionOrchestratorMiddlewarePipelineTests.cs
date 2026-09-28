// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Extensions.TestHostOrchestrator;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.Logging;
using Microsoft.Testing.Platform.TestHostOrchestrator;

namespace Microsoft.Testing.Platform.UnitTests;

/// <summary>
/// Covers the v1 single-invocation middleware pipeline's hard-guard semantics: composition order,
/// preserving downstream failures/cancellation, rejecting fabricated success and verdict rewrites, and
/// draining downstream executions a middleware started but did not itself observe.
/// </summary>
[TestClass]
public sealed class TestHostExecutionOrchestratorMiddlewarePipelineTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task RunAsync_NoMiddleware_ReturnsInnermostResultUnchanged()
    {
        int exitCode = await TestHostExecutionOrchestratorMiddlewarePipeline.RunAsync(
            [],
            _ => Task.FromResult(7),
            new NopLogger(),
            CancellationToken.None);

        Assert.AreEqual(7, exitCode);
    }

    [TestMethod]
    public async Task RunAsync_TwoMiddleware_ComposesOutermostFirstAndUnwindsInReverse()
    {
        var log = new List<string>();
        var outer = FakeMiddleware.PassThrough("outer", log);
        var inner = FakeMiddleware.PassThrough("inner", log);

        int exitCode = await TestHostExecutionOrchestratorMiddlewarePipeline.RunAsync(
            [outer, inner],
            _ =>
            {
                log.Add("innermost");
                return Task.FromResult(0);
            },
            new NopLogger(),
            CancellationToken.None);

        Assert.AreEqual(0, exitCode);
        Assert.AreSequenceEqual(
            new[] { "outer-before", "inner-before", "innermost", "inner-after", "outer-after" },
            log);
    }

    [TestMethod]
    public async Task RunAsync_DownstreamFails_PreservesTheFailureExitCode()
    {
        var passThrough = FakeMiddleware.PassThrough("m", []);

        int exitCode = await TestHostExecutionOrchestratorMiddlewarePipeline.RunAsync(
            [passThrough],
            _ => Task.FromResult((int)ExitCode.AtLeastOneTestFailed),
            new NopLogger(),
            CancellationToken.None);

        Assert.AreEqual((int)ExitCode.AtLeastOneTestFailed, exitCode);
    }

    [TestMethod]
    public async Task RunAsync_MiddlewareShortCircuitsWithExplicitFailureCode_NeverInvokesDownstream()
    {
        bool downstreamInvoked = false;
        FakeMiddleware admissionReject = new("gatekeeper", (_, _) => Task.FromResult((int)ExitCode.GenericFailure));

        int exitCode = await TestHostExecutionOrchestratorMiddlewarePipeline.RunAsync(
            [admissionReject],
            _ =>
            {
                downstreamInvoked = true;
                return Task.FromResult(0);
            },
            new NopLogger(),
            CancellationToken.None);

        Assert.AreEqual((int)ExitCode.GenericFailure, exitCode);
        Assert.IsFalse(downstreamInvoked);
    }

    [TestMethod]
    public async Task RunAsync_MiddlewareShortCircuitsWithSuccessCode_RejectsFabricatedSuccess()
    {
        FakeMiddleware fabricatesSuccess = new("liar", (_, _) => Task.FromResult((int)ExitCode.Success));

        InvalidOperationException exception = await Assert.ThrowsExactlyAsync<InvalidOperationException>(
            () => TestHostExecutionOrchestratorMiddlewarePipeline.RunAsync(
                [fabricatesSuccess],
                _ => Task.FromResult((int)ExitCode.Success),
                new NopLogger(),
                CancellationToken.None));

        Assert.Contains("liar", exception.Message, StringComparison.Ordinal);
    }

    [TestMethod]
    public async Task RunAsync_MiddlewareRewritesDownstreamResult_Throws()
    {
        FakeMiddleware rewriter = new("rewriter", async (next, ct) =>
        {
            int downstream = await next(ct);
            Assert.AreEqual((int)ExitCode.AtLeastOneTestFailed, downstream);

            // Attempts to turn a real failure into a fabricated success: must be rejected.
            return (int)ExitCode.Success;
        });

        InvalidOperationException exception = await Assert.ThrowsExactlyAsync<InvalidOperationException>(
            () => TestHostExecutionOrchestratorMiddlewarePipeline.RunAsync(
                [rewriter],
                _ => Task.FromResult((int)ExitCode.AtLeastOneTestFailed),
                new NopLogger(),
                CancellationToken.None));

        Assert.Contains("rewriter", exception.Message, StringComparison.Ordinal);
    }

    [TestMethod]
    public async Task RunAsync_MiddlewareInvokesNextTwiceConcurrently_SecondInvocationThrowsButActualResultStillWins()
    {
        FakeMiddleware doubleInvoker = new("double-invoker", async (next, ct) =>
        {
            Task<int> first = next(ct);
            InvalidOperationException secondCallException = Assert.ThrowsExactly<InvalidOperationException>(() => next(ct));
            Assert.Contains("next", secondCallException.Message, StringComparison.Ordinal);

            // Correlating the return value with the one legitimate invocation's actual result is what makes
            // a swallowed double-call violation surface instead of silently looking like success.
            return await first;
        });

        int exitCode = await TestHostExecutionOrchestratorMiddlewarePipeline.RunAsync(
            [doubleInvoker],
            _ => Task.FromResult((int)ExitCode.AtLeastOneTestFailed),
            new NopLogger(),
            CancellationToken.None);

        Assert.AreEqual((int)ExitCode.AtLeastOneTestFailed, exitCode);
    }

    [TestMethod]
    public async Task RunAsync_MiddlewareSwallowsDoubleCallViolationAndFabricatesAResult_ThrowsInsteadOfSucceeding()
    {
        // Same shape as above, but the middleware discards the real downstream result and returns an
        // unrelated value instead: this must not silently succeed just because the double-call violation was
        // caught internally.
        FakeMiddleware doubleInvoker = new("careless-double-invoker", (next, ct) =>
        {
            _ = next(ct);
            try
            {
                _ = next(ct);
            }
            catch (InvalidOperationException)
            {
                // Swallowed on purpose - this is the scenario under test.
            }

            return Task.FromResult((int)ExitCode.Success);
        });

        InvalidOperationException exception = await Assert.ThrowsExactlyAsync<InvalidOperationException>(
            () => TestHostExecutionOrchestratorMiddlewarePipeline.RunAsync(
                [doubleInvoker],
                _ => Task.FromResult((int)ExitCode.AtLeastOneTestFailed),
                new NopLogger(),
                CancellationToken.None));

        Assert.Contains("careless-double-invoker", exception.Message, StringComparison.Ordinal);
    }

    [TestMethod]
    public async Task RunAsync_MiddlewareStoresNextAndInvokesItAfterReturning_LateInvocationThrows()
    {
        Func<CancellationToken, Task<int>>? stashed = null;
        FakeMiddleware stasher = new("stasher", (next, _) =>
        {
            stashed = next;

            // Explicit non-success short-circuit: it never actually invokes next during this call.
            return Task.FromResult((int)ExitCode.GenericFailure);
        });

        int exitCode = await TestHostExecutionOrchestratorMiddlewarePipeline.RunAsync(
            [stasher],
            _ => Task.FromResult(0),
            new NopLogger(),
            CancellationToken.None);

        Assert.AreEqual((int)ExitCode.GenericFailure, exitCode);
        Assert.IsNotNull(stashed);
        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => stashed(CancellationToken.None));

        // A rejected late invocation must have no side effects: calling it again keeps throwing the same
        // way, rather than, say, succeeding the second time or corrupting shared pipeline state (the
        // in-flight downstream list) that a rejected call never touches in the first place.
        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => stashed(CancellationToken.None));
    }

    [TestMethod]
    public async Task RunAsync_MiddlewareNeverInvokesNext_UninvokedPlaceholderIsNeverAwaitedAndDoesNotHangThePipeline()
    {
        // The guard's downstream placeholder is preallocated for every middleware frame, whether or not
        // that middleware ever calls next(). This must not turn an uninvoked middleware into a hang: since
        // nothing ever completes an uninvoked guard's placeholder, the pipeline must complete promptly by
        // simply never awaiting it - proven here by a completed WaitAsync race against a bounded timeout
        // that would only be reached if something were incorrectly awaiting the never-completed placeholder.
        FakeMiddleware neverInvokes = new("never-invokes", (_, _) => Task.FromResult((int)ExitCode.GenericFailure));

        Task<int> pipelineTask = TestHostExecutionOrchestratorMiddlewarePipeline.RunAsync(
            [neverInvokes],
            _ => Task.FromResult(0),
            new NopLogger(),
            CancellationToken.None);

        Task completed = await Task.WhenAny(pipelineTask, Task.Delay(TimeSpan.FromSeconds(10), TestContext.CancellationToken));

        Assert.AreSame(pipelineTask, completed);
        Assert.AreEqual((int)ExitCode.GenericFailure, await pipelineTask);
    }

    [TestMethod]
    public async Task RunAsync_MiddlewareReturnsBeforeAwaitingStartedDownstream_DownstreamIsDrainedBeforePipelineCompletes()
    {
        TaskCompletionSource<int> downstreamSource = new(TaskCreationOptions.RunContinuationsAsynchronously);

        // Starts downstream but returns the un-awaited task directly: the platform (not this method) is
        // the one that awaits/drains and validates it before the pipeline completes.
        FakeMiddleware returnsBeforeAwaiting = new("fire-and-forget", (next, ct) => next(ct));

        Task<int> pipelineTask = TestHostExecutionOrchestratorMiddlewarePipeline.RunAsync(
            [returnsBeforeAwaiting],
            _ => downstreamSource.Task,
            new NopLogger(),
            CancellationToken.None);

        Assert.IsFalse(pipelineTask.IsCompleted);
        downstreamSource.SetResult((int)ExitCode.AtLeastOneTestFailed);

        int exitCode = await pipelineTask;

        Assert.AreEqual((int)ExitCode.AtLeastOneTestFailed, exitCode);
    }

    [TestMethod]
    public async Task RunAsync_MiddlewareThrowsWithoutAwaitingStartedDownstream_PrimaryExceptionWinsAndDownstreamIsStillDrained()
    {
        TaskCompletionSource<int> downstreamSource = new(TaskCreationOptions.RunContinuationsAsynchronously);

        FakeMiddleware throwsWithoutAwaiting = new("throws-without-awaiting", (next, ct) =>
        {
            _ = next(ct);
            throw new InvalidOperationException("middleware failed for reasons unrelated to next()");
        });

        Task<int> pipelineTask = TestHostExecutionOrchestratorMiddlewarePipeline.RunAsync(
            [throwsWithoutAwaiting],
            _ => downstreamSource.Task,
            new NopLogger(),
            CancellationToken.None);

        // The pipeline cannot finish until the orphaned downstream task is drained, even though the
        // middleware already threw. No real asynchronous gap was crossed yet (the middleware threw
        // synchronously and the drain loop is suspended on the still-pending TCS), so this is a
        // deterministic check, not a race.
        Assert.IsFalse(pipelineTask.IsCompleted);

        downstreamSource.SetResult((int)ExitCode.Success);

        InvalidOperationException exception = await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => pipelineTask);
        Assert.AreEqual("middleware failed for reasons unrelated to next()", exception.Message);
    }

    [TestMethod]
    public async Task RunAsync_MiddlewareSwallowsDownstreamFailure_ThrowsInsteadOfReportingSuccess()
    {
        FakeMiddleware swallower = new("swallower", (next, ct) =>
        {
            // Starts downstream but never observes its outcome.
            _ = next(ct);
            return Task.FromResult((int)ExitCode.Success);
        });

        InvalidOperationException exception = await Assert.ThrowsExactlyAsync<InvalidOperationException>(
            () => TestHostExecutionOrchestratorMiddlewarePipeline.RunAsync(
                [swallower],
                _ => Task.FromException<int>(new InvalidOperationException("downstream exploded")),
                new NopLogger(),
                CancellationToken.None));

        Assert.Contains("swallower", exception.Message, StringComparison.Ordinal);
        Assert.IsInstanceOfType<InvalidOperationException>(exception.InnerException);
        Assert.AreEqual("downstream exploded", exception.InnerException!.Message);
    }

    [TestMethod]
    public async Task RunAsync_LeafThrowsSynchronously_MiddlewareAwaitsAndCatchesReturningFailureCode_SurfacesOriginalInsteadOfNullReferenceException()
    {
        // Regression test: the leaf here throws synchronously - it never even produces a Task<int>, faulted
        // or otherwise - which this middleware observes by awaiting next(ct) inside its own try/catch (an
        // ordinary, realistic middleware shape: try/catch does not care whether the exception it observes
        // came from a direct synchronous throw or from unwrapping a faulted task). Reaching this point must
        // not leave the pipeline's tracked "downstream task" null: that would previously surface as an
        // unrelated NullReferenceException when the pipeline went to await it afterwards, masking the real
        // failure instead of reporting it through the established "swallowed downstream failure" contract.
        FakeMiddleware catchesAndReturnsFailure = new("catches-sync-throw-returns-failure", async (next, ct) =>
        {
            try
            {
                return await next(ct);
            }
            catch (InvalidOperationException)
            {
                return (int)ExitCode.GenericFailure;
            }
        });

        InvalidOperationException exception = await Assert.ThrowsExactlyAsync<InvalidOperationException>(
            () => TestHostExecutionOrchestratorMiddlewarePipeline.RunAsync(
                [catchesAndReturnsFailure],
                _ => throw new InvalidOperationException("leaf exploded synchronously"),
                new NopLogger(),
                CancellationToken.None));

        Assert.Contains("catches-sync-throw-returns-failure", exception.Message, StringComparison.Ordinal);
        Assert.IsInstanceOfType<InvalidOperationException>(exception.InnerException);
        Assert.AreEqual("leaf exploded synchronously", exception.InnerException!.Message);
    }

    [TestMethod]
    public async Task RunAsync_LeafThrowsSynchronously_MiddlewareAwaitsAndCatchesReturningSuccessCode_SurfacesOriginalInsteadOfNullReferenceException()
    {
        // Companion to the failure-code case above: whether the middleware's own catch block reports
        // success or failure after swallowing a synchronously-thrown leaf failure, the platform must still
        // surface the original exception through the same contract violation - not a
        // NullReferenceException, and not a fabricated success.
        FakeMiddleware catchesAndReturnsSuccess = new("catches-sync-throw-returns-success", async (next, ct) =>
        {
            try
            {
                return await next(ct);
            }
            catch (InvalidOperationException)
            {
                return (int)ExitCode.Success;
            }
        });

        InvalidOperationException exception = await Assert.ThrowsExactlyAsync<InvalidOperationException>(
            () => TestHostExecutionOrchestratorMiddlewarePipeline.RunAsync(
                [catchesAndReturnsSuccess],
                _ => throw new InvalidOperationException("leaf exploded synchronously"),
                new NopLogger(),
                CancellationToken.None));

        Assert.Contains("catches-sync-throw-returns-success", exception.Message, StringComparison.Ordinal);
        Assert.IsInstanceOfType<InvalidOperationException>(exception.InnerException);
        Assert.AreEqual("leaf exploded synchronously", exception.InnerException!.Message);
    }

    [TestMethod]
    public async Task RunAsync_LeafThrowsSynchronously_MiddlewareDoesNotCatch_PropagatesOriginalExceptionDirectly()
    {
        // When the middleware does not catch the synchronous throw at all (the common case - most
        // middleware just returns "next(ct)" without a try/catch), the original exception must propagate
        // out of RunAsync unchanged: no NullReferenceException, no wrapping.
        FakeMiddleware passThrough = new("pass-through", (next, ct) => next(ct));

        InvalidOperationException exception = await Assert.ThrowsExactlyAsync<InvalidOperationException>(
            () => TestHostExecutionOrchestratorMiddlewarePipeline.RunAsync(
                [passThrough],
                _ => throw new InvalidOperationException("leaf exploded synchronously"),
                new NopLogger(),
                CancellationToken.None));

        Assert.AreEqual("leaf exploded synchronously", exception.Message);
    }

    [TestMethod]
    public async Task RunAsync_RootCancellation_PropagatesEvenWhenMiddlewarePassesNoneToNext()
    {
        using CancellationTokenSource rootCts = new();
        CancellationToken observedByDownstream = default;

        FakeMiddleware passesNone = new("passes-none", (next, _) => next(CancellationToken.None));

        Task<int> pipelineTask = TestHostExecutionOrchestratorMiddlewarePipeline.RunAsync(
            [passesNone],
            ct =>
            {
                observedByDownstream = ct;
                TaskCompletionSource<int> tcs = new();

                // Root cancellation, even though the middleware passed CancellationToken.None to next(), must
                // still be linked into whatever token downstream receives.
                ct.Register(() => tcs.TrySetCanceled(ct));
                return tcs.Task;
            },
            new NopLogger(),
            rootCts.Token);

        rootCts.Cancel();

        await Assert.ThrowsExactlyAsync<TaskCanceledException>(() => pipelineTask);
        Assert.IsTrue(observedByDownstream.IsCancellationRequested);
    }

    [TestMethod]
    public async Task RunAsync_OuterMiddlewareChildToken_PropagatesThroughInnerMiddlewareToLeaf()
    {
        // Regression test: a pipeline frame used to always receive the constant rootToken as its own
        // ambient cancellation token, discarding whatever token an outer middleware actually passed to
        // next() (for example a child token derived from a timeout, unrelated to root). That token must
        // still reach every inner middleware and the leaf, not just root.
        using CancellationTokenSource outerChildCts = new();
        CancellationToken observedByInner = default;
        CancellationToken observedByLeaf = default;
        TaskCompletionSource<int> leafSource = new(TaskCreationOptions.RunContinuationsAsynchronously);

        FakeMiddleware outer = new("outer", (next, _) => next(outerChildCts.Token));
        FakeMiddleware inner = new("inner", (next, ct) =>
        {
            observedByInner = ct;
            return next(ct);
        });

        Task<int> pipelineTask = TestHostExecutionOrchestratorMiddlewarePipeline.RunAsync(
            [outer, inner],
            ct =>
            {
                observedByLeaf = ct;
                ct.Register(() => leafSource.TrySetCanceled(ct));
                return leafSource.Task;
            },
            new NopLogger(),
            CancellationToken.None);

        Assert.IsFalse(pipelineTask.IsCompleted);
        Assert.IsFalse(observedByInner.IsCancellationRequested);
        Assert.IsFalse(observedByLeaf.IsCancellationRequested);

        outerChildCts.Cancel();

        Assert.IsTrue(observedByInner.IsCancellationRequested, "The outer middleware's own child token must reach the inner middleware.");
        Assert.IsTrue(observedByLeaf.IsCancellationRequested, "The outer middleware's own child token must reach the leaf.");

        await Assert.ThrowsExactlyAsync<TaskCanceledException>(() => pipelineTask);
    }

    [TestMethod]
    public async Task RunAsync_InnerMiddlewarePassesNoneToNext_DoesNotDetachFromOuterMiddlewaresChildToken()
    {
        // Companion regression test: even after RunAsync_OuterMiddlewareChildToken_PropagatesThroughInnerMiddlewareToLeaf
        // is satisfied, an inner middleware could still re-detach the chain by passing
        // CancellationToken.None of its own to next(). Linking must happen at every frame, not just once
        // at the top, so the outer ancestor's own child token still reaches the leaf.
        using CancellationTokenSource outerChildCts = new();
        CancellationToken observedByLeaf = default;
        TaskCompletionSource<int> leafSource = new(TaskCreationOptions.RunContinuationsAsynchronously);

        FakeMiddleware outer = new("outer", (next, _) => next(outerChildCts.Token));
        FakeMiddleware inner = new("inner", (next, _) => next(CancellationToken.None));

        Task<int> pipelineTask = TestHostExecutionOrchestratorMiddlewarePipeline.RunAsync(
            [outer, inner],
            ct =>
            {
                observedByLeaf = ct;
                ct.Register(() => leafSource.TrySetCanceled(ct));
                return leafSource.Task;
            },
            new NopLogger(),
            CancellationToken.None);

        Assert.IsFalse(pipelineTask.IsCompleted);
        Assert.IsFalse(observedByLeaf.IsCancellationRequested);

        outerChildCts.Cancel();

        Assert.IsTrue(
            observedByLeaf.IsCancellationRequested,
            "Inner middleware passing CancellationToken.None to next() must not detach the leaf from an outer ancestor's own child token.");

        await Assert.ThrowsExactlyAsync<TaskCanceledException>(() => pipelineTask);
    }

    [TestMethod]
    public async Task RunAsync_RootCancellationObservedOnlyAfterMiddlewareReturned_PropagatesWithoutBeingWrappedAsSwallowedFailure()
    {
        using CancellationTokenSource rootCts = new();
        TaskCompletionSource<int> downstreamSource = new(TaskCreationOptions.RunContinuationsAsynchronously);

        FakeMiddleware firesAndForgets = new("fires-and-forgets", (next, ct) =>
        {
            // Starts downstream but returns immediately without awaiting it: the drain path (not this
            // method) is the one that will observe the eventual cancellation.
            _ = next(ct);
            return Task.FromResult((int)ExitCode.GenericFailure);
        });

        Task<int> pipelineTask = TestHostExecutionOrchestratorMiddlewarePipeline.RunAsync(
            [firesAndForgets],
            _ => downstreamSource.Task,
            new NopLogger(),
            rootCts.Token);

        rootCts.Cancel();
        downstreamSource.TrySetCanceled(rootCts.Token);

        // Must surface as cancellation, not as a wrapped "swallowed downstream failure" InvalidOperationException.
        await Assert.ThrowsExactlyAsync<TaskCanceledException>(() => pipelineTask);
    }

    private sealed class NopLogger : ILogger
    {
        public bool IsEnabled(LogLevel logLevel) => false;

        public void Log<TState>(LogLevel logLevel, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
        }

        public Task LogAsync<TState>(LogLevel logLevel, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            => Task.CompletedTask;
    }

    private sealed class FakeMiddleware(string uid, Func<Func<CancellationToken, Task<int>>, CancellationToken, Task<int>> behavior) : ITestHostExecutionOrchestratorMiddleware
    {
        public static FakeMiddleware PassThrough(string uid, List<string> log) => new(
            uid,
            async (next, ct) =>
            {
                log.Add($"{uid}-before");
                int result = await next(ct);
                log.Add($"{uid}-after");
                return result;
            });

        public string Uid => uid;

        public string Version => "1.0.0";

        public string DisplayName => uid;

        public string Description => uid;

        public Task<bool> IsEnabledAsync() => Task.FromResult(true);

        public Task<int> OrchestrateTestHostExecutionAsync(Func<CancellationToken, Task<int>> next, CancellationToken cancellationToken)
            => behavior(next, cancellationToken);
    }
}
