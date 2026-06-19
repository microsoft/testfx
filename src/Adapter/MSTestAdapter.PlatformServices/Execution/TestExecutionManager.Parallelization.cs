// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Extensions;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Helpers;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Interface;
using Microsoft.VisualStudio.TestPlatform.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Adapter;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Logging;
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
    /// <param name="isDeploymentDone">Indicates if deployment is done.</param>
    internal virtual async Task ExecuteTestsAsync(IEnumerable<TestCase> tests, IRunContext? runContext, IFrameworkHandle frameworkHandle, bool isDeploymentDone)
    {
        InitializeRandomTestOrder(frameworkHandle);

        var testsBySource = (from test in tests
                             group test by test.Source into testGroup
                             select new { Source = testGroup.Key, Tests = (IEnumerable<TestCase>)testGroup }).ToArray();

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
    private async Task ExecuteTestsInSourceAsync(IEnumerable<TestCase> tests, IRunContext? runContext, IFrameworkHandle frameworkHandle, string source, bool isDeploymentDone)
    {
        DebugEx.Assert(!StringEx.IsNullOrEmpty(source), "Source cannot be empty");

#if !WINDOWS_UWP && !WIN_UI
        if (isDeploymentDone)
        {
            source = Path.Combine(PlatformServiceProvider.Instance.TestDeployment.GetDeploymentDirectory()!, Path.GetFileName(source));
        }
#endif

        using ITestSourceHost isolationHost = PlatformServiceProvider.Instance.CreateTestSourceHost(source, runContext?.RunSettings);
        bool usesAppDomains = isolationHost is TestSourceHost { UsesAppDomain: true };

        if (PlatformServiceProvider.Instance.AdapterTraceLogger.IsInfoEnabled)
        {
            PlatformServiceProvider.Instance.AdapterTraceLogger.Info("Created unit-test runner {0}", source);
        }

        // Default test set is filtered tests based on user provided filter criteria
        ITestCaseFilterExpression? filterExpression = _testMethodFilter.GetFilterExpression(runContext, frameworkHandle, out bool filterHasError);
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
        TestCase[] testsToRun = [.. tests.Where(t => MatchTestFilter(filterExpression, t, _testMethodFilter))];
        if (_testOrderRandom is { } sourceRandom)
        {
            Shuffle(sourceRandom, testsToRun);
        }

        UnitTestElement[] unitTestElements = [.. testsToRun.Select(e => e.ToUnitTestElementWithUpdatedSource(source))];
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
            frameworkHandle.SendMessage(
                TestMessageLevel.Informational,
                string.Format(CultureInfo.CurrentCulture, Resource.TestParallelizationBanner, source, parallelWorkers, parallelScope));

            // Create test sets for execution, we can execute them in parallel based on parallel settings
            // Parallel and not parallel sets.
            IEnumerable<IGrouping<bool, TestCase>> testSets = testsToRun.GroupBy(t => t.GetPropertyValue(EngineConstants.DoNotParallelizeProperty, false));

            IGrouping<bool, TestCase>? parallelizableTestSet = testSets.FirstOrDefault(g => !g.Key);
            IGrouping<bool, TestCase>? nonParallelizableTestSet = testSets.FirstOrDefault(g => g.Key);

            if (parallelizableTestSet != null)
            {
                ConcurrentQueue<IEnumerable<TestCase>>? queue = null;

                // Chunk the sets into further groups based on parallel level
                switch (parallelScope)
                {
                    case ExecutionScope.MethodLevel:
                        if (_testOrderRandom is { } methodRandom)
                        {
                            IEnumerable<TestCase>[] methodChunks = [.. parallelizableTestSet.Select(t => (IEnumerable<TestCase>)[t])];
                            Shuffle(methodRandom, methodChunks);
                            queue = new ConcurrentQueue<IEnumerable<TestCase>>(methodChunks);
                        }
                        else
                        {
                            queue = new ConcurrentQueue<IEnumerable<TestCase>>(parallelizableTestSet.Select(t => new[] { t }));
                        }

                        break;

                    case ExecutionScope.ClassLevel:
                        if (_testOrderRandom is { } classRandom)
                        {
                            IEnumerable<TestCase>[] classChunks =
                            [
                                .. parallelizableTestSet
                                    .GroupBy(t => t.GetPropertyValue(EngineConstants.TestClassNameProperty) as string)
                                    .Select(g => (IEnumerable<TestCase>)g.ToArray()),
                            ];
                            Shuffle(classRandom, classChunks);
                            queue = new ConcurrentQueue<IEnumerable<TestCase>>(classChunks);
                        }
                        else
                        {
                            queue = new ConcurrentQueue<IEnumerable<TestCase>>(parallelizableTestSet.GroupBy(t => t.GetPropertyValue(EngineConstants.TestClassNameProperty) as string));
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

                                if (queue.TryDequeue(out IEnumerable<TestCase>? testSet))
                                {
                                    await ExecuteTestsWithTestRunnerAsync(testSet, frameworkHandle, source, sourceLevelParameters, testRunner, usesAppDomains).ConfigureAwait(false);
                                }
                            }
                        }
                        catch (OperationCanceledException) when (_testRunCancellationToken?.Canceled == true)
                        {
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

                    frameworkHandle.SendMessage(TestMessageLevel.Error, exceptionToString);
                    throw;
                }
            }

            // Queue the non parallel set
            if (nonParallelizableTestSet != null)
            {
                await ExecuteTestsWithTestRunnerAsync(nonParallelizableTestSet, frameworkHandle, source, sourceLevelParameters, testRunner, usesAppDomains).ConfigureAwait(false);
            }
        }
        else
        {
            await ExecuteTestsWithTestRunnerAsync(testsToRun, frameworkHandle, source, sourceLevelParameters, testRunner, usesAppDomains).ConfigureAwait(false);
        }

        if (PlatformServiceProvider.Instance.IsGracefulStopRequested)
        {
            testRunner.ForceCleanup(sourceLevelParameters!, new RemotingMessageLogger(frameworkHandle));
        }

        if (PlatformServiceProvider.Instance.AdapterTraceLogger.IsInfoEnabled)
        {
            PlatformServiceProvider.Instance.AdapterTraceLogger.Info("Executed tests belonging to source {0}", source);
        }
    }
}
