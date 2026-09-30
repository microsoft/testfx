// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Extensions.TestHostOrchestrator;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.Logging;
using Microsoft.Testing.Platform.Resources;

namespace Microsoft.Testing.Platform.TestHostOrchestrator;

/// <summary>
/// Builds and runs the guarded, single-invocation <see cref="ITestHostExecutionOrchestratorMiddleware"/>
/// chain wrapped around one <see cref="ITestHostExecutionOrchestrator"/> invocation. See
/// <see cref="ITestHostExecutionOrchestratorMiddleware"/> for the contract middleware must follow and for
/// the v1 scope boundary (single-invocation composition around exactly one orchestrator; no repeat,
/// sharding, phases, or protocol metadata).
/// </summary>
/// <remarks>
/// All pipeline state (in-flight downstream tasks, per-frame single-invocation guards) is local to one
/// <see cref="RunAsync"/> call: nothing is shared or reused across separate runs.
/// </remarks>
internal static class TestHostExecutionOrchestratorMiddlewarePipeline
{
    public static async Task<int> RunAsync(
        IReadOnlyList<ITestHostExecutionOrchestratorMiddleware> middlewareOutermostFirst,
        Func<CancellationToken, Task<int>> innermost,
        ILogger logger,
        CancellationToken rootToken)
    {
        // Fresh per-run state. Every downstream Task<int> started by a next() call is tracked here so it
        // can be drained (awaited to completion, exceptions logged rather than thrown) before this method
        // returns, even when the middleware that started it returned or threw without awaiting it itself.
        List<Task> inFlightDownstreamTasks = [];
        // Linked token sources are disposed only after all accepted downstream tasks have drained. A frame's
        // own task can fault before a child it started has completed, so disposal cannot be tied to that frame.
        List<CancellationTokenSource> linkedTokenSources = [];
        object inFlightLock = new();

        Func<CancellationToken, Task<int>> chain = innermost;
        for (int i = middlewareOutermostFirst.Count - 1; i >= 0; i--)
        {
            ITestHostExecutionOrchestratorMiddleware middleware = middlewareOutermostFirst[i];
            Func<CancellationToken, Task<int>> next = chain;

            // Each frame must be invoked with the ambient token it actually receives from its caller (an
            // outer middleware's own next()-supplied token, already linked with everything above it - or
            // rootToken for the outermost frame) rather than a fixed rootToken. Substituting rootToken here
            // would silently drop an outer middleware's own child token (for example, one derived from a
            // timeout) before it ever reaches this frame or anything below it.
            chain = ambientToken => InvokeFrameAsync(middleware, next, ambientToken, rootToken, inFlightDownstreamTasks, linkedTokenSources, inFlightLock);
        }

        try
        {
            return await chain(rootToken).ConfigureAwait(false);
        }
        finally
        {
            try
            {
                await DrainAsync(inFlightDownstreamTasks, inFlightLock, logger).ConfigureAwait(false);
            }
            finally
            {
                foreach (CancellationTokenSource linkedTokenSource in linkedTokenSources)
                {
                    linkedTokenSource.Dispose();
                }
            }
        }
    }

    private static async Task<int> InvokeFrameAsync(
        ITestHostExecutionOrchestratorMiddleware middleware,
        Func<CancellationToken, Task<int>> next,
        CancellationToken ambientToken,
        CancellationToken rootToken,
        List<Task> inFlightDownstreamTasks,
        List<CancellationTokenSource> linkedTokenSources,
        object inFlightLock)
    {
        SingleInvocationNext guard = new(next, ambientToken, rootToken, inFlightDownstreamTasks, linkedTokenSources, inFlightLock);
        Task<int> middlewareTask;
        try
        {
            middlewareTask = middleware.OrchestrateTestHostExecutionAsync(guard.InvokeAsync, ambientToken);
        }
        catch
        {
            guard.Close();
            if (guard.WasInvoked)
            {
                // A middleware failure cannot replace a downstream failure or cancellation. Await the
                // accepted child even when this frame faulted, then preserve the middleware failure only
                // when the child completed successfully.
                await guard.DownstreamTask.ConfigureAwait(false);
            }

            throw;
        }

        // Close immediately after the middleware method returns its Task, not after that Task completes.
        // An async middleware can remain suspended before its first next() call; that call is deferred and
        // must be rejected even though the returned Task has not completed yet.
        guard.Close();

        int result;
        try
        {
            result = await middlewareTask.ConfigureAwait(false);
        }
        catch
        {
            if (guard.WasInvoked)
            {
                // A middleware failure cannot replace a downstream failure or cancellation. Await the
                // accepted child even when this frame faulted, then preserve the middleware failure only
                // when the child completed successfully.
                await guard.DownstreamTask.ConfigureAwait(false);
            }

            throw;
        }

        if (!guard.WasInvoked)
        {
            // Short-circuit: the middleware never called next(). Reaching this point means it did not
            // throw either, so it is reporting a definitive result without running anything downstream.
            // That must never be a success code - that would fabricate a passing/empty run. Nothing ever
            // awaits guard.DownstreamTask in this branch, so an uninvoked guard's placeholder is simply
            // left pending forever and garbage-collected unobserved - it is never a resource leak or a
            // hang, because nothing (this method, the drain phase, or anyone else) ever awaits it.
            return result == (int)ExitCode.Success
                ? throw new InvalidOperationException(
                    string.Format(CultureInfo.InvariantCulture, PlatformResources.TestHostExecutionOrchestratorMiddlewareFabricatedSuccessErrorMessage, middleware.Uid))
                : result;
        }

        // The middleware invoked next(). Whatever it started must be drained here — whether or not the
        // middleware itself awaited it — so nothing from this frame is still running once the frame is
        // considered complete, and so the downstream exit code can be validated against what the
        // middleware returned. guard.DownstreamTask is always a real, non-null Task<int> the moment
        // WasInvoked is observed true (see SingleInvocationNext), even if the invocation that set
        // WasInvoked is, at this very instant, still blocked inside inner()'s own synchronous prefix on a
        // different thread and has not yet produced (or even attempted to produce) a downstream result.
        int downstreamResult;
        try
        {
            downstreamResult = await guard.DownstreamTask.ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (guard.EffectiveToken.IsCancellationRequested)
        {
            // Cancellation via any ancestor in the chain - root, an outer middleware's own child token
            // (for example a timeout), or this frame's own middlewareToken - lets it propagate like the
            // non-middleware path does, instead of wrapping it as a "swallowed failure". The middleware's
            // own call already returned successfully without observing this, but that is expected during
            // cancellation teardown, not a middleware defect. guard.EffectiveToken is only ever read here,
            // after guard.DownstreamTask has already completed (faulted, in this case), so it is guaranteed
            // to already reflect the value InvokeAsync assigned to it: Task completion establishes a
            // happens-before relationship between whatever a thread did before completing/publishing a
            // task and whatever a later awaiter of that same task observes afterward.
            throw;
        }
        catch (Exception ex)
        {
            // The middleware's own call completed without throwing, even though the downstream execution it
            // started actually faulted: the middleware did not observe/propagate that failure. Letting that
            // look like success would be exactly the "swallowed failure" this pipeline must not allow.
            throw new InvalidOperationException(
                string.Format(CultureInfo.InvariantCulture, PlatformResources.TestHostExecutionOrchestratorMiddlewareSwallowedDownstreamFailureErrorMessage, middleware.Uid),
                ex);
        }

        return result == downstreamResult
            ? result
            : throw new InvalidOperationException(
                string.Format(CultureInfo.InvariantCulture, PlatformResources.TestHostExecutionOrchestratorMiddlewareRewroteDownstreamResultErrorMessage, middleware.Uid, downstreamResult, result));
    }

    private static async Task DrainAsync(List<Task> inFlightDownstreamTasks, object inFlightLock, ILogger logger)
    {
        for (int taskIndex = 0; ; taskIndex++)
        {
            Task task;
            lock (inFlightLock)
            {
                if (taskIndex == inFlightDownstreamTasks.Count)
                {
                    return;
                }

                task = inFlightDownstreamTasks[taskIndex];
            }

            try
            {
                await task.ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                // Best-effort diagnostics only: this is draining a downstream execution whose outcome, if
                // it matters to the caller, was already surfaced (as the pipeline result or as a wrapped
                // "swallowed failure" exception) by InvokeFrameAsync. Re-throwing here would let a drained
                // task silently override that already-decided outcome.
                await TryLogDebugAsync(logger, $"Test host execution orchestrator middleware downstream execution observed a failure while being drained: {ex}").ConfigureAwait(false);
            }
        }
    }

    private static async Task TryLogDebugAsync(ILogger logger, string message)
    {
        try
        {
            await logger.LogDebugAsync(message).ConfigureAwait(false);
        }
        catch (Exception)
        {
            // Diagnostic logging here is best-effort and must not affect draining of the remaining tasks.
        }
    }

    /// <summary>
    /// Guards a single <c>next()</c> continuation and, once accepted, owns the lifecycle of whatever
    /// downstream <see cref="Task{TResult}"/> that invocation produces.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <see cref="DownstreamTask"/> is preallocated in the constructor - strictly before this guard is ever
    /// handed to a middleware - so it is always a real, non-null, awaitable <see cref="Task{TResult}"/> from
    /// the very first instant any other thread could observe <see cref="WasInvoked"/> as
    /// <see langword="true"/>. This also makes the accepted downstream task safe to await while the
    /// synchronous prefix of <c>inner()</c> is still running. The middleware contract requires the call
    /// to <c>next()</c> itself to begin before the middleware returns or throws; only the returned task
    /// may be observed asynchronously after that point.
    /// </para>
    /// <para>
    /// <b>Lock invariant.</b> Acceptance (the "single invocation" state transition from open to invoked),
    /// registration of <see cref="DownstreamTask"/> into the shared in-flight drain list, and
    /// <see cref="Close"/> (the transition from open to closed once a frame stops accepting new invocations)
    /// are all performed while holding the exact same <c>inFlightLock</c> object that
    /// <see cref="TestHostExecutionOrchestratorMiddlewarePipeline.DrainAsync(List{Task}, object, ILogger)"/>
    /// holds when it reads that list. This closes an otherwise-real gap: accepting an invocation
    /// (deciding it should run) and registering it for draining used to be two separate steps - a lock-free
    /// <see cref="Interlocked"/> state transition immediately followed by a separately-locked list append -
    /// so an invocation that had already begun while its middleware was returning or throwing could race the
    /// drain: the state transition could have already succeeded (so the invocation is "accepted"
    /// and cannot be rejected later) while the corresponding list append had not yet happened, letting a
    /// concurrently-running top-level drain miss it entirely and let the whole pipeline report
    /// completion while that still-accepted invocation (and whatever it eventually starts, up to and
    /// including the wrapped orchestrator) keeps running unmanaged underneath. Folding accept-and-register
    /// into one critical section - guarded by the very same lock the drain uses - makes that outcome
    /// impossible: by construction, any accepted invocation is present before the lock is released, and
    /// the drain keeps consuming newly appended descendants until the list is exhausted. Symmetrically, once
    /// <see cref="Close"/> has run (always synchronously, in the owning frame's own <c>finally</c>, before
    /// that frame's own exception or return value can propagate any further), any <em>later</em> call to
    /// <see cref="InvokeAsync"/> observes the frame already closed under that same lock and is rejected
    /// outright - it can never proceed to call <c>inner()</c> at all, so there is nothing from a rejected
    /// call that could ever need draining.
    /// </para>
    /// <para>
    /// Only the state check/transition and the list mutation happen under the lock; everything else -
    /// linked-token-source creation, the actual call into <c>inner()</c> (arbitrary extension or leaf code,
    /// which may itself block synchronously for an arbitrary duration), and publishing the outcome - runs
    /// outside it, so the lock is held only briefly and user code never runs while any other frame is
    /// blocked waiting for it (including the drain snapshot).
    /// </para>
    /// </remarks>
    private sealed class SingleInvocationNext
    {
        private const int StateOpen = 0;
        private const int StateInvoked = 1;
        private const int StateClosed = 2;

        private readonly Func<CancellationToken, Task<int>> _inner;
        private readonly CancellationToken _ambientToken;
        private readonly CancellationToken _rootToken;
        private readonly List<Task> _inFlightDownstreamTasks;
        private readonly List<CancellationTokenSource> _linkedTokenSources;
        private readonly object _inFlightLock;

        // See the type-level remarks: preallocated before this guard is ever exposed to a middleware, so
        // DownstreamTask (below) is never null, regardless of when - or on what thread - InvokeAsync
        // eventually runs.
        private readonly TaskCompletionSource<Task<int>> _downstreamTaskSource =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        // Guarded exclusively by _inFlightLock - see the type-level "Lock invariant" remarks. Never read
        // or written outside that lock, so no Interlocked/Volatile access is needed anywhere in this type.
        private int _state;

        public SingleInvocationNext(
            Func<CancellationToken, Task<int>> inner,
            CancellationToken ambientToken,
            CancellationToken rootToken,
            List<Task> inFlightDownstreamTasks,
            List<CancellationTokenSource> linkedTokenSources,
            object inFlightLock)
        {
            _inner = inner;
            _ambientToken = ambientToken;
            _rootToken = rootToken;
            _inFlightDownstreamTasks = inFlightDownstreamTasks;
            _linkedTokenSources = linkedTokenSources;
            _inFlightLock = inFlightLock;

            // Unwrap() produces a Task<int> that mirrors whatever Task<int> is eventually placed into
            // _downstreamTaskSource by InvokeAsync (a synchronous throw from inner() is normalized there
            // into an already-faulted Task<int> instead of being rethrown, so this always ends up
            // reflecting a real downstream outcome - success, fault, or cancellation - never a raw,
            // unrelated exception from Unwrap's own plumbing).
            DownstreamTask = _downstreamTaskSource.Task.Unwrap();
        }

        // Reads _state under the same lock every other member uses to touch it - see the type-level
        // "Lock invariant" remarks. Only ever called after Close() has already run for this guard (see
        // InvokeFrameAsync), by which point _state is guaranteed to hold its final value (Invoked or
        // Closed) - never Open - so this always reports a settled, final answer, never a transient one.
        public bool WasInvoked
        {
            get
            {
                lock (_inFlightLock)
                {
                    return _state == StateInvoked;
                }
            }
        }

        /// <summary>
        /// Gets the downstream task. Always non-null (see the type-level remarks). Only ever awaited once
        /// <see cref="WasInvoked"/> is observed <see langword="true"/> - an uninvoked guard's placeholder is
        /// simply left pending forever and garbage-collected along with this guard, never awaited by
        /// anything, so it can never hang the pipeline or leak.
        /// </summary>
        public Task<int> DownstreamTask { get; }

        /// <summary>
        /// Gets the actual composite token passed to the wrapped continuation once <see cref="InvokeAsync"/>
        /// has run. Linked from every ancestor in the chain (the ambient token, which already
        /// carries root plus every outer middleware's own child token by induction), the absolute root token
        /// (linked again explicitly as a hard, chain-independent guarantee), and whatever token this
        /// frame's middleware itself passed to <c>next</c>. Only ever read after <see cref="DownstreamTask"/>
        /// has already completed (see <see cref="InvokeFrameAsync"/>), by which point it is guaranteed to
        /// already hold its final value - see the type-level remarks on publication ordering.
        /// </summary>
        public CancellationToken EffectiveToken { get; private set; }

        public void Close()
        {
            // Guarded by the same lock InvokeAsync uses to accept-and-register: see the type-level "Lock
            // invariant" remarks. If an invocation is concurrently winning that same lock right now, this
            // call simply waits its turn - it can never observe (or race with) a half-finished acceptance,
            // only either "not yet accepted" (and this call then forecloses it) or "already fully accepted
            // and registered" (and this call is then correctly a no-op).
            lock (_inFlightLock)
            {
                if (_state == StateOpen)
                {
                    _state = StateClosed;
                }
            }
        }

        public Task<int> InvokeAsync(CancellationToken middlewareToken)
        {
            // Accept-and-register is one atomic critical section: see the type-level "Lock invariant"
            // remarks. Nothing here can observably "run inner()" without DownstreamTask having already
            // been added to the shared drain list, and nothing can add to that list without also having
            // won this frame's single-invocation guard - the two facts can never be split apart, in either
            // direction, by any interleaving with a concurrent Close() or with
            // TestHostExecutionOrchestratorMiddlewarePipeline.DrainAsync's own reads of the same list
            // under the same lock. Only the state check/transition and the list mutation happen here,
            // under the lock; everything below - linked-token-source creation, the call into inner()
            // (arbitrary extension or leaf code, which may itself block synchronously for an arbitrary
            // duration), and publishing the outcome - runs outside it.
            lock (_inFlightLock)
            {
                if (_state != StateOpen)
                {
                    throw new InvalidOperationException(PlatformResources.TestHostExecutionOrchestratorMiddlewareNextInvokedMoreThanOnceErrorMessage);
                }

                _state = StateInvoked;
                _inFlightDownstreamTasks.Add(DownstreamTask);
            }

            // Always link all three, even when middlewareToken is already CancellationToken.None: linking
            // ambientToken is what carries an outer middleware's own child token (for example a timeout)
            // down to this frame and beyond, and linking rootToken explicitly - on top of it already being
            // part of ambientToken by induction - keeps root cancellation mandatory even if that chain were
            // ever broken. Either way, passing CancellationToken.None here can never detach the downstream
            // execution from its ancestors or from root.
            // inner(...) is documented to return a Task<int>, but a misbehaving implementation - or the
            // leaf orchestrator itself - can still throw synchronously before ever producing one. Once
            // this method has set the state to Invoked, DownstreamTask must never be left unresolved
            // forever: a synchronous throw is normalized into an already-faulted task instead of being
            // rethrown, so the failure is still observable through _downstreamTaskSource - preserving the
            // original failure as its inner exception through the same "swallowed downstream failure"
            // contract as an asynchronous fault - rather than leaving the placeholder pending forever or
            // surfacing an unrelated exception that masks the real cause.
            Task<int> downstream;
            try
            {
                var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(_ambientToken, _rootToken, middlewareToken);
                lock (_inFlightLock)
                {
                    _linkedTokenSources.Add(linkedCts);
                }

                EffectiveToken = linkedCts.Token;
                downstream = _inner(EffectiveToken);
                downstream ??= Task.FromException<int>(
                    new InvalidOperationException(PlatformResources.TestHostExecutionOrchestratorMiddlewareNextReturnedNullErrorMessage));
            }
            catch (Exception ex)
            {
                downstream = Task.FromException<int>(ex);
            }

            // Publishes the real downstream outcome into the placeholder that any other thread may already
            // be holding a reference to (via DownstreamTask) and awaiting. TrySetResult (rather than
            // SetResult) is defensive only: the single-invocation guard above already ensures this method
            // body runs at most once per guard instance.
            _downstreamTaskSource.TrySetResult(downstream);

            return DownstreamTask;
        }
    }
}
