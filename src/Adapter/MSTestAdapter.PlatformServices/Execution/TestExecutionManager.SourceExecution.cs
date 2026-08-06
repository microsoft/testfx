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
}
