// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Helpers;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Interface;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Adapter;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using ExecutionScope = Microsoft.VisualStudio.TestTools.UnitTesting.ExecutionScope;

namespace Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Execution;

internal partial class TestExecutionManager
{
    /// <summary>
    /// Execute the parameter tests.
    /// </summary>
    /// <param name="tests">Tests to execute.</param>
    /// <param name="runContext">The run context.</param>
    /// <param name="frameworkHandle">Handle to record test start/end/results.</param>
    /// <param name="testResultRecorder">Recorder used to report test results back to the host.</param>
    /// <param name="filterProvider">Provider for the test filter, or <see langword="null"/> for no filter.</param>
    /// <param name="isDeploymentDone">Indicates if deployment is done.</param>
    internal virtual async Task ExecuteTestsAsync(IEnumerable<UnitTestElement> tests, IRunContext? runContext, IFrameworkHandle frameworkHandle, ITestResultRecorder testResultRecorder, ITestElementFilterProvider? filterProvider, bool isDeploymentDone)
    {
        _testResultRecorder = testResultRecorder;
        _testElementFilterProvider = filterProvider;

        InitializeRandomTestOrder(frameworkHandle.ToAdapterMessageLogger());

        var testsBySource = (from test in tests
                             group test by test.TestMethod.AssemblyName into testGroup
                             select new { Source = testGroup.Key, Tests = (IEnumerable<UnitTestElement>)testGroup }).ToArray();

        if (_testOrderRandom is { } random)
        {
            Shuffle(random, testsBySource);
        }

        foreach (var group in testsBySource)
        {
            _testRunCancellationToken?.ThrowIfCancellationRequested();
            await ExecuteTestsInSourceAsync(group.Tests, runContext, frameworkHandle, group.Source, isDeploymentDone).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Execute the parameter tests present in parameter source.
    /// </summary>
    /// <param name="tests">Tests to execute.</param>
    /// <param name="runContext">The run context.</param>
    /// <param name="frameworkHandle">Handle to record test start/end/results.</param>
    /// <param name="source">The test container for the tests.</param>
    /// <param name="isDeploymentDone">Indicates if deployment is done.</param>
    private async Task ExecuteTestsInSourceAsync(IEnumerable<UnitTestElement> tests, IRunContext? runContext, IFrameworkHandle frameworkHandle, string source, bool isDeploymentDone)
    {
        DebugEx.Assert(!StringEx.IsNullOrEmpty(source), "Source cannot be empty");

#if !WINDOWS_UWP && !WIN_UI
        if (isDeploymentDone)
        {
            source = Path.Combine(PlatformServiceProvider.Instance.TestDeployment.GetDeploymentDirectory()!, Path.GetFileName(source));
        }
#endif

        IAdapterMessageLogger adapterMessageLogger = frameworkHandle.ToAdapterMessageLogger();

        using ITestSourceHost isolationHost = PlatformServiceProvider.Instance.CreateTestSourceHost(source, runContext?.RunSettings?.SettingsXml);
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

        if (!MSTestSettings.CurrentSettings.DisableParallelization && sourceSettings.CanParallelizeAssembly && parallelWorkers > 0)
        {
            // Parallelization is enabled. Let's do further classification for sets.
            adapterMessageLogger.SendMessage(
                MessageLevel.Informational,
                string.Format(CultureInfo.CurrentCulture, Resource.TestParallelizationBanner, source, parallelWorkers, parallelScope));

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
                                    await ExecuteTestsWithTestRunnerAsync(testSet, adapterMessageLogger, source, sourceLevelParameters, testRunner, usesAppDomains).ConfigureAwait(false);
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
}
