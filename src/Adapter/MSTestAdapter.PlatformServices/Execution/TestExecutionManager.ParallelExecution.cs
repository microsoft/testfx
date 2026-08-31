// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Interface;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using ExecutionScope = Microsoft.VisualStudio.TestTools.UnitTesting.ExecutionScope;

namespace Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Execution;

internal partial class TestExecutionManager
{
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
}
