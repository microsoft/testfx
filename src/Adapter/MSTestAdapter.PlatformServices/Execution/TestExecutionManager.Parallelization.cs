// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Helpers;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Deployment;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Interface;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using ExecutionScope = Microsoft.VisualStudio.TestTools.UnitTesting.ExecutionScope;

namespace Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Execution;

internal partial class TestExecutionManager
{
    /// <summary>
    /// Execute the parameter tests.
    /// </summary>
    /// <param name="tests">Tests to execute.</param>
    /// <param name="deploymentContext">Host-provided test-run directory and run settings XML.</param>
    /// <param name="messageLogger">Logger used to report test messages back to the host.</param>
    /// <param name="testResultRecorder">Recorder used to report test results back to the host.</param>
    /// <param name="filterProvider">Provider for the test filter, or <see langword="null"/> for no filter.</param>
    /// <param name="isDeploymentDone">Indicates if deployment is done.</param>
    internal virtual async Task ExecuteTestsAsync(IEnumerable<UnitTestElement> tests, DeploymentContext deploymentContext, IAdapterMessageLogger messageLogger, ITestResultRecorder testResultRecorder, ITestElementFilterProvider? filterProvider, bool isDeploymentDone)
    {
        _testResultRecorder = testResultRecorder;
        _testElementFilterProvider = filterProvider;

        InitializeRandomTestOrder(messageLogger);

        var testsBySource = (from test in tests
                             group test by test.TestMethod.AssemblyName into testGroup
                             select new { Source = testGroup.Key, Tests = (IEnumerable<UnitTestElement>)testGroup }).ToArray();

        if (_testOrderRandom is { } random)
        {
            Shuffle(random, testsBySource);
        }

        // Configured declarations are applied per source below, but their diagnostics have to be judged
        // against the whole run: a declaration naming a test in assembly A is legitimately absent from
        // assembly B, so warning per source would report every valid declaration as unmatched once for each
        // other assembly. Reported once here, against every test in the run.
        if (MSTestSettings.CurrentSettings.DeclaredDependencies is { Length: > 0 } declaredDependencies)
        {
            TestDependencyDeclaration.ReportUnmatchedDeclarations(declaredDependencies, tests, messageLogger);
        }

        foreach (var group in testsBySource)
        {
            _testRunCancellationToken?.ThrowIfCancellationRequested();
            await ExecuteTestsInSourceAsync(group.Tests, deploymentContext, messageLogger, group.Source, isDeploymentDone).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Execute the parameter tests present in parameter source.
    /// </summary>
    /// <param name="tests">Tests to execute.</param>
    /// <param name="deploymentContext">Host-provided test-run directory and run settings XML.</param>
    /// <param name="messageLogger">Logger used to report test messages back to the host.</param>
    /// <param name="source">The test container for the tests.</param>
    /// <param name="isDeploymentDone">Indicates if deployment is done.</param>
    private async Task ExecuteTestsInSourceAsync(IEnumerable<UnitTestElement> tests, DeploymentContext deploymentContext, IAdapterMessageLogger messageLogger, string source, bool isDeploymentDone)
    {
        DebugEx.Assert(!StringEx.IsNullOrEmpty(source), "Source cannot be empty");

#if !WINDOWS_UWP && !WIN_UI
        if (isDeploymentDone)
        {
            source = Path.Combine(PlatformServiceProvider.Instance.TestDeployment.GetDeploymentDirectory()!, Path.GetFileName(source));
        }
#endif

        IAdapterMessageLogger adapterMessageLogger = messageLogger;

        using ITestSourceHost isolationHost = PlatformServiceProvider.Instance.CreateTestSourceHost(source, deploymentContext.RunSettingsXml);
        bool usesAppDomains = isolationHost is TestSourceHost { UsesAppDomain: true };

        if (PlatformServiceProvider.Instance.AdapterTraceLogger.IsInfoEnabled)
        {
            PlatformServiceProvider.Instance.AdapterTraceLogger.Info("Created unit-test runner {0}", source);
        }

        // Default test set is filtered tests based on user provided filter criteria
        bool filterHasError = false;
        ITestElementFilter? filter = _testElementFilterProvider?.GetTestElementFilter(adapterMessageLogger, out filterHasError);
        if (filterHasError)
        {
            // Bail out without processing everything else below.
            return;
        }

        // this is done so that appropriate values of test context properties are set at source level
        // and are merged with session level parameters
        IDictionary<string, object> sourceLevelParameters = PlatformServiceProvider.Instance.SettingsProvider.GetProperties(source);

        if (_sessionParameters is { Count: > 0 })
        {
            sourceLevelParameters = _sessionParameters.ConcatWithOverwrites(sourceLevelParameters);
        }

        _testRunCancellationToken?.ThrowIfCancellationRequested();

        TestAssemblySettingsProvider? sourceSettingsProvider = null;

        try
        {
            sourceSettingsProvider = isolationHost.CreateInstanceForType(
                typeof(TestAssemblySettingsProvider),
                null) as TestAssemblySettingsProvider;
        }
        catch (Exception ex)
        {
            if (PlatformServiceProvider.Instance.AdapterTraceLogger.IsInfoEnabled)
            {
                PlatformServiceProvider.Instance.AdapterTraceLogger.Info("Could not create TestAssemblySettingsProvider instance in child app-domain", ex);
            }
        }

        TestAssemblySettings sourceSettings = (sourceSettingsProvider != null)
            ? sourceSettingsProvider.GetSettings(source)
            : new TestAssemblySettings();

        int parallelWorkers = sourceSettings.Workers;
        ExecutionScope parallelScope = sourceSettings.Scope;
        UnitTestElement[] testsToRun = [.. tests.Where(t => MatchTestFilter(filter, t, source))];
        if (_testOrderRandom is { } sourceRandom)
        {
            Shuffle(sourceRandom, testsToRun);
        }

        UnitTestElement[] unitTestElements = [.. testsToRun.Select(e => e.WithUpdatedSource(source))];
        // Create an instance of a type defined in adapter so that adapter gets loaded in the child app domain
        var testRunner = (UnitTestRunner)isolationHost.CreateInstanceForType(
            typeof(UnitTestRunner),
            [MSTestSettings.CurrentSettings, unitTestElements])!;

        // Ensures that the cancellation token gets through AppDomain boundary.
        _testRunCancellationToken?.Register(static state => ((UnitTestRunner)state!).Cancel(), testRunner);

        if (MSTestSettings.CurrentSettings.ParallelizationWorkers.HasValue)
        {
            // The runsettings value takes precedence over an assembly level setting. Reset the level.
            parallelWorkers = MSTestSettings.CurrentSettings.ParallelizationWorkers.Value;
        }

        if (MSTestSettings.CurrentSettings.ParallelizationScope.HasValue)
        {
            // The runsettings value takes precedence over an assembly level setting. Reset the level.
            parallelScope = MSTestSettings.CurrentSettings.ParallelizationScope.Value;
        }

        bool parallelizationEnabled = !MSTestSettings.CurrentSettings.DisableParallelization && sourceSettings.CanParallelizeAssembly && parallelWorkers > 0;

        // Sorting the graph's input by name is what makes name order the *tie-breaker* within the dependency
        // order, rather than something the graph overrides: OrderTopologically breaks ties by position in this
        // array, so tests that are simultaneously ready still run in name order. Doing it here rather than in
        // the runner is deliberate - re-sorting a chunk after the topological pass would undo the ordering the
        // graph just established. Ordering keys mirror the historical VSTest ManagedType/ManagedMethod
        // test-case properties, which are only populated when the test method carries managed method metadata.
        if (MSTestSettings.CurrentSettings.OrderTestsByNameInClass && !MSTestSettings.CurrentSettings.RandomizeTestOrder)
        {
            testsToRun =
            [
                .. testsToRun
                    .OrderBy(t => t.TestMethod.HasManagedMethodAndTypeProperties ? t.TestMethod.ManagedTypeName : null, StringComparer.Ordinal)
                    .ThenBy(t => t.TestMethod.HasManagedMethodAndTypeProperties ? t.TestMethod.ManagedMethodName : null, StringComparer.Ordinal),
            ];
        }

        // Edges declared in testconfig.json are merged onto the elements first, so that from the graph's
        // point of view a configured dependency is indistinguishable from an attribute-declared one.
        if (MSTestSettings.CurrentSettings.DeclaredDependencies is { Length: > 0 } declaredDependencies)
        {
            TestDependencyDeclaration.ApplyAll(declaredDependencies, testsToRun, adapterMessageLogger: null);
        }

        // Returns null - and so leaves every run that does not use [DependsOn] on exactly the path it uses
        // today - unless at least one test in this source declares a dependency.
        var dependencyGraph = TestDependencyGraph.Build(testsToRun, parallelScope, parallelizationEnabled);
        if (parallelizationEnabled)
        {
            // Parallelization is enabled. Let's do further classification for sets.
            adapterMessageLogger.SendMessage(
                MessageLevel.Informational,
                string.Format(CultureInfo.CurrentCulture, Resource.TestParallelizationBanner, source, parallelWorkers, parallelScope));

            if (dependencyGraph is not null)
            {
                await ExecuteTestsWithDependencyGraphAsync(dependencyGraph, adapterMessageLogger, source, sourceLevelParameters, testRunner, usesAppDomains, parallelWorkers).ConfigureAwait(false);
            }
            else
            {
                await ExecuteParallelizedTestsAsync(testsToRun, adapterMessageLogger, source, sourceLevelParameters, testRunner, usesAppDomains, parallelWorkers, parallelScope).ConfigureAwait(false);
            }
        }
        else if (dependencyGraph is not null)
        {
            await ExecuteTestsWithDependencyGraphAsync(dependencyGraph, adapterMessageLogger, source, sourceLevelParameters, testRunner, usesAppDomains, parallelWorkers: 1).ConfigureAwait(false);
        }
        else
        {
            await ExecuteTestsWithTestRunnerAsync(testsToRun, adapterMessageLogger, source, sourceLevelParameters, testRunner, usesAppDomains).ConfigureAwait(false);
        }

        if (PlatformServiceProvider.Instance.IsGracefulStopRequested)
        {
            testRunner.ForceCleanup(sourceLevelParameters!, new RemotingMessageLogger(adapterMessageLogger));
        }

        if (PlatformServiceProvider.Instance.AdapterTraceLogger.IsInfoEnabled)
        {
            PlatformServiceProvider.Instance.AdapterTraceLogger.Info("Executed tests belonging to source {0}", source);
        }
    }

    /// <summary>
    /// The ordinary parallel path: split the tests into a parallelizable and a non-parallelizable set, chunk the
    /// former according to <paramref name="parallelScope"/>, drain the chunks with a fixed pool of workers, then
    /// run the non-parallelizable set.
    /// </summary>
    private async Task ExecuteParallelizedTestsAsync(
        UnitTestElement[] testsToRun,
        IAdapterMessageLogger adapterMessageLogger,
        string source,
        IDictionary<string, object> sourceLevelParameters,
        UnitTestRunner testRunner,
        bool usesAppDomains,
        int parallelWorkers,
        ExecutionScope parallelScope)
    {
        // Create test sets for execution, we can execute them in parallel based on parallel settings
        // Parallel and not parallel sets.
        // Single-pass partition: enumerate testsToRun once via GroupBy (lazy GroupBy evaluated twice
        // — once per FirstOrDefault — caused 2 full passes over testsToRun).
        IEnumerable<UnitTestElement>? parallelizableTestSet = null;
        IEnumerable<UnitTestElement>? nonParallelizableTestSet = null;
        foreach (IGrouping<bool, UnitTestElement> group in testsToRun.GroupBy(t => t.DoNotParallelize))
        {
            if (group.Key)
            {
                nonParallelizableTestSet = group;
            }
            else
            {
                parallelizableTestSet = group;
            }
        }

        if (parallelizableTestSet != null)
        {
            ConcurrentQueue<IEnumerable<UnitTestElement>>? queue = null;

            // Chunk the sets into further groups based on parallel level
            switch (parallelScope)
            {
                case ExecutionScope.MethodLevel:
                    if (_testOrderRandom is { } methodRandom)
                    {
                        IEnumerable<UnitTestElement>[] methodChunks = [.. parallelizableTestSet.Select(t => (IEnumerable<UnitTestElement>)[t])];
                        Shuffle(methodRandom, methodChunks);
                        queue = new ConcurrentQueue<IEnumerable<UnitTestElement>>(methodChunks);
                    }
                    else
                    {
                        queue = new ConcurrentQueue<IEnumerable<UnitTestElement>>(parallelizableTestSet.Select(t => new[] { t }));
                    }

                    break;

                case ExecutionScope.ClassLevel:
                    if (_testOrderRandom is { } classRandom)
                    {
                        IEnumerable<UnitTestElement>[] classChunks =
                        [
                            .. parallelizableTestSet
                                .GroupBy(t => t.TestMethod.FullClassName)
                                .Select(g => (IEnumerable<UnitTestElement>)g.ToArray()),
                        ];
                        Shuffle(classRandom, classChunks);
                        queue = new ConcurrentQueue<IEnumerable<UnitTestElement>>(classChunks);
                    }
                    else
                    {
                        queue = new ConcurrentQueue<IEnumerable<UnitTestElement>>(parallelizableTestSet.GroupBy(t => t.TestMethod.FullClassName));
                    }

                    break;
            }

            var tasks = new List<Task>();
            var resourceLockManager = new ResourceLockManager();

            for (int i = 0; i < parallelWorkers; i++)
            {
                _testRunCancellationToken?.ThrowIfCancellationRequested();

                tasks.Add(_taskFactory(async () =>
                {
                    try
                    {
                        while (!queue!.IsEmpty)
                        {
                            _testRunCancellationToken?.ThrowIfCancellationRequested();

                            if (queue.TryDequeue(out IEnumerable<UnitTestElement>? testSet))
                            {
                                IReadOnlyList<ResourceLockInfo> chunkLocks = ResourceLockManager.GetChunkLocks(testSet);
                                if (chunkLocks.Count == 0)
                                {
                                    await ExecuteTestsWithTestRunnerAsync(testSet, adapterMessageLogger, source, sourceLevelParameters, testRunner, usesAppDomains).ConfigureAwait(false);
                                }
                                else
                                {
                                    await resourceLockManager.ExecuteWithLocksAsync(
                                        chunkLocks,
                                        () => ExecuteTestsWithTestRunnerAsync(testSet, adapterMessageLogger, source, sourceLevelParameters, testRunner, usesAppDomains),
                                        _testRunCancellationToken?.CancellationToken ?? CancellationToken.None).ConfigureAwait(false);
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
        }

        // Queue the non parallel set
        if (nonParallelizableTestSet != null)
        {
            await ExecuteTestsWithTestRunnerAsync(nonParallelizableTestSet, adapterMessageLogger, source, sourceLevelParameters, testRunner, usesAppDomains).ConfigureAwait(false);
        }
    }

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
