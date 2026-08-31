// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Interface;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Execution;

internal partial class TestExecutionManager
{
    /// <summary>
    /// The dependency-graph path: report the graph's diagnostics, fail the tests caught in a cycle, then run
    /// the parallel chunks in topological order - releasing each chunk as soon as the chunks it waits for have
    /// finished, so independent branches still run concurrently - and finally the sequential phase.
    /// </summary>
    private async Task ExecuteTestsWithDependencyGraphAsync(
        TestDependencyGraph graph,
        IAdapterMessageLogger adapterMessageLogger,
        string source,
        IDictionary<string, object> sourceLevelParameters,
        UnitTestRunner testRunner,
        bool usesAppDomains,
        int parallelWorkers)
    {
        var coordinator = new TestDependencyCoordinator(graph);

        // If testRunner is in a different AppDomain, we cannot pass the message logger directly.
        IAdapterMessageLogger remotingMessageLogger = usesAppDomains
            ? new RemotingMessageLogger(adapterMessageLogger)
            : adapterMessageLogger;

        Dictionary<string, object?> lifecycleContextProperties = [with(sourceLevelParameters!)];

        foreach (string warning in graph.Warnings)
        {
            adapterMessageLogger.SendMessage(MessageLevel.Warning, warning);
        }

        foreach (string error in graph.Errors)
        {
            adapterMessageLogger.SendMessage(MessageLevel.Error, error);
        }

        // A test in a cycle is reported as failed - the declaration, not the test, is what is broken, and a
        // silent skip would hide it. Each failure carries the description of the cycle *that test* is in, so
        // an assembly with two unrelated cycles does not stamp both paths onto every failure. Recording it as
        // "did not pass" also makes everything downstream skip.
        foreach (TestDependencyGraph.BrokenTest brokenTest in graph.BrokenTests)
        {
            _testRunCancellationToken?.ThrowIfCancellationRequested();
            coordinator.RecordNotRun(brokenTest.Element);

            DateTimeOffset now = DateTimeOffset.Now;
            var cycleResult = new TestTools.UnitTesting.TestResult
            {
                Outcome = TestTools.UnitTesting.UnitTestOutcome.Failed,
                TestFailureException = new InvalidOperationException(brokenTest.CycleMessage),
            };

            await _testResultRecorder.RecordStartAsync(brokenTest.Element).ConfigureAwait(false);

            // Selected but never run, so the class-cleanup countdown still owes this test its decrement.
            // See UnitTestRunner.NotifyTestNotRunAsync.
            UnitTestElement brokenElement = brokenTest.Element.WithUpdatedSource(source);
            Dictionary<string, object?> testContextProperties = GetTestContextProperties(brokenTest.Element.ExecutionContextProperties, sourceLevelParameters, brokenElement);
            TestTools.UnitTesting.TestResult[] cleanupResults = usesAppDomains || Thread.CurrentThread.GetApartmentState() == ApartmentState.STA
#pragma warning disable VSTHRD103 // Call async methods when in an async method - mirrors RunSingleTest: Task cannot cross app domains, and an await would leave the STA thread.
                ? testRunner.NotifyTestNotRun(brokenElement, testContextProperties, lifecycleContextProperties, remotingMessageLogger)
#pragma warning restore VSTHRD103
                : await testRunner.NotifyTestNotRunAsync(brokenElement, testContextProperties, lifecycleContextProperties, remotingMessageLogger).ConfigureAwait(false);

            await SendTestResultsAsync(
                brokenTest.Element,
                [cycleResult, .. cleanupResults],
                now,
                now,
                _testResultRecorder).ConfigureAwait(false);
        }

        if (graph.ParallelChunks.Length > 0)
        {
            await ExecuteChunksInTopologicalOrderAsync(graph, coordinator, adapterMessageLogger, source, sourceLevelParameters, testRunner, usesAppDomains, parallelWorkers).ConfigureAwait(false);
        }

        if (graph.SequentialTests.Length > 0)
        {
            await ExecuteTestsWithTestRunnerAsync(graph.SequentialTests, adapterMessageLogger, source, sourceLevelParameters, testRunner, usesAppDomains, coordinator).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Drains the parallel chunks with a fixed pool of workers, honoring the chunk-level dependency edges: a
    /// chunk becomes available the moment the last chunk it waits for finishes, which is what lets the branches
    /// of a fan-out run at the same time. Completion - not success - releases a dependent chunk; whether its
    /// individual tests actually run is decided per test by <paramref name="coordinator"/>.
    /// </summary>
    private async Task ExecuteChunksInTopologicalOrderAsync(
        TestDependencyGraph graph,
        TestDependencyCoordinator coordinator,
        IAdapterMessageLogger adapterMessageLogger,
        string source,
        IDictionary<string, object> sourceLevelParameters,
        UnitTestRunner testRunner,
        bool usesAppDomains,
        int parallelWorkers)
    {
        int chunkCount = graph.ParallelChunks.Length;
        int[] pendingPrerequisites = new int[chunkCount];
        var dependents = new List<int>[chunkCount];
        for (int i = 0; i < chunkCount; i++)
        {
            pendingPrerequisites[i] = graph.ParallelChunkPrerequisites[i].Length;
            foreach (int prerequisite in graph.ParallelChunkPrerequisites[i])
            {
                (dependents[prerequisite] ??= []).Add(i);
            }
        }

        var ready = new ConcurrentQueue<int>();

        // One permit per queued chunk, plus - once everything is done - one per worker so that every worker
        // wakes up and exits instead of blocking on a queue that will never be fed again.
        var available = new SemaphoreSlim(0);
        int queued = 0;
        for (int i = 0; i < chunkCount; i++)
        {
            if (pendingPrerequisites[i] == 0)
            {
                ready.Enqueue(i);
                queued++;
            }
        }

        // Release(0) throws, and a zero count would mean every chunk had an unmet prerequisite - that is,
        // a cycle in the chunk graph. Build guarantees that cannot happen (chunks caught in a projection
        // cycle are demoted to the sequential phase), but the guard keeps a future regression in that
        // invariant from surfacing here as an argument exception instead of where it belongs.
        if (queued > 0)
        {
            available.Release(queued);
        }

        int completedChunks = 0;
        int effectiveWorkers = Math.Max(1, Math.Min(parallelWorkers, chunkCount));
        var resourceLockManager = new ResourceLockManager();
        var tasks = new List<Task>(effectiveWorkers);

        for (int worker = 0; worker < effectiveWorkers; worker++)
        {
            _testRunCancellationToken?.ThrowIfCancellationRequested();

            tasks.Add(_taskFactory(async () =>
            {
                try
                {
                    while (true)
                    {
                        _testRunCancellationToken?.ThrowIfCancellationRequested();
                        await available.WaitAsync(_testRunCancellationToken?.CancellationToken ?? CancellationToken.None).ConfigureAwait(false);

                        // A permit with nothing behind it is the shutdown signal released below.
                        if (!ready.TryDequeue(out int chunkIndex))
                        {
                            return;
                        }

                        UnitTestElement[] chunk = graph.ParallelChunks[chunkIndex];
                        try
                        {
                            IReadOnlyList<ResourceLockInfo> chunkLocks = ResourceLockManager.GetChunkLocks(chunk);
                            if (chunkLocks.Count == 0)
                            {
                                await ExecuteTestsWithTestRunnerAsync(chunk, adapterMessageLogger, source, sourceLevelParameters, testRunner, usesAppDomains, coordinator).ConfigureAwait(false);
                            }
                            else
                            {
                                await resourceLockManager.ExecuteWithLocksAsync(
                                    chunkLocks,
                                    () => ExecuteTestsWithTestRunnerAsync(chunk, adapterMessageLogger, source, sourceLevelParameters, testRunner, usesAppDomains, coordinator),
                                    _testRunCancellationToken?.CancellationToken ?? CancellationToken.None).ConfigureAwait(false);
                            }
                        }
                        finally
                        {
                            // The bookkeeping below must happen even when the chunk failed, otherwise the
                            // chunks waiting on it would never be released and the workers waiting for them
                            // would block forever - turning one faulty chunk into a hung run. Releasing on
                            // completion rather than success is also what the dependency semantics ask for:
                            // whether the dependent tests actually run is decided per test by the
                            // coordinator, which sees the (absent) outcomes and skips them.
                            if (dependents[chunkIndex] is { } chunkDependents)
                            {
                                foreach (int dependent in chunkDependents)
                                {
                                    if (Interlocked.Decrement(ref pendingPrerequisites[dependent]) == 0)
                                    {
                                        ready.Enqueue(dependent);
                                        available.Release();
                                    }
                                }
                            }

                            // Once every chunk has been accounted for, wake every worker so they observe the
                            // empty queue and exit instead of waiting for a permit that will never come.
                            if (Interlocked.Increment(ref completedChunks) == chunkCount)
                            {
                                available.Release(effectiveWorkers);
                            }
                        }
                    }
                }
                catch (OperationCanceledException) when (_testRunCancellationToken?.Canceled == true)
                {
                    // Expected when the test run is canceled. Swallow it so the worker exits gracefully.
                    if (PlatformServiceProvider.Instance.AdapterTraceLogger.IsInfoEnabled)
                    {
                        PlatformServiceProvider.Instance.AdapterTraceLogger.Info("Parallel test worker canceled for source {0}", source);
                    }
                }
            }));
        }

        try
        {
            await Task.WhenAll(tasks).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            string exceptionToString = ex.ToString();
            if (PlatformServiceProvider.Instance.AdapterTraceLogger.IsErrorEnabled)
            {
                PlatformServiceProvider.Instance.AdapterTraceLogger.Error("Error occurred while executing tests in parallel{0}{1}", Environment.NewLine, exceptionToString);
            }

            adapterMessageLogger.SendMessage(MessageLevel.Error, exceptionToString);
            throw;
        }
        finally
        {
            available.Dispose();
        }
    }
}
