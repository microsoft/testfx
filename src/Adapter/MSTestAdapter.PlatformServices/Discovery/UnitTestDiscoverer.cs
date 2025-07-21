// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Discovery;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices;
using Microsoft.VisualStudio.TestPlatform.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Adapter;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Logging;

namespace Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter;

[SuppressMessage("Performance", "CA1852: Seal internal types", Justification = "Overrides required for testability")]
internal class UnitTestDiscoverer
{
    private readonly TestMethodFilter _testMethodFilter;

    internal UnitTestDiscoverer()
        => _testMethodFilter = new TestMethodFilter();

    /// <summary>
    /// Discovers the tests available from the provided sources.
    /// </summary>
    /// <param name="sources"> The sources. </param>
    /// <param name="logger"> The logger. </param>
    /// <param name="discoverySink"> The discovery Sink. </param>
    /// <param name="discoveryContext"> The discovery context. </param>
    internal void DiscoverTests(
        IEnumerable<string> sources,
        IMessageLogger logger,
        ITestCaseDiscoverySink discoverySink,
        IDiscoveryContext discoveryContext)
    {
        foreach (string source in sources)
        {
            DiscoverTestsInSource(source, logger, discoverySink, discoveryContext);
        }
    }

    /// <summary>
    /// Get the tests from the parameter source.
    /// </summary>
    /// <param name="source"> The source. </param>
    /// <param name="logger"> The logger. </param>
    /// <param name="discoverySink"> The discovery Sink. </param>
    /// <param name="discoveryContext"> The discovery context. </param>
    internal virtual void DiscoverTestsInSource(
        string source,
        IMessageLogger logger,
        ITestCaseDiscoverySink discoverySink,
        IDiscoveryContext? discoveryContext)
    {
        ICollection<UnitTestElement>? testElements = AssemblyEnumeratorWrapper.GetTests(source, discoveryContext?.RunSettings, out List<string> warnings);

        bool treatDiscoveryWarningsAsErrors = MSTestSettings.CurrentSettings.TreatDiscoveryWarningsAsErrors;

        // log the warnings
        foreach (string warning in warnings)
        {
            PlatformServiceProvider.Instance.AdapterTraceLogger.LogInfo(
                "MSTestDiscoverer: Warning during discovery from {0}. {1} ",
                source,
                warning);
            string message = string.Format(CultureInfo.CurrentCulture, Resource.DiscoveryWarning, source, warning);
            logger.SendMessage(treatDiscoveryWarningsAsErrors ? TestMessageLevel.Error : TestMessageLevel.Warning, message);
        }

        // No tests found => nothing to do
        if (testElements == null || testElements.Count == 0)
        {
            return;
        }

        PlatformServiceProvider.Instance.AdapterTraceLogger.LogInfo(
            "MSTestDiscoverer: Found {0} tests from source {1}",
            testElements.Count,
            source);

        SendTestCases(testElements, discoverySink, discoveryContext, logger);
    }

#pragma warning disable IDE0028 // Collection initialization can be simplified - cannot be done for all TFMs. So suppressing.
    private static readonly ConditionalWeakTable<TestCase, object?[]> TestCaseToDataDictionary = new();
#pragma warning restore IDE0028 // Collection initialization can be simplified

    internal static bool TryGetActualData(TestCase testCase, [NotNullWhen(true)] out object?[]? actualData)
        => TestCaseToDataDictionary.TryGetValue(testCase, out actualData);

    internal void SendTestCases(IEnumerable<UnitTestElement> testElements, ITestCaseDiscoverySink discoverySink, IDiscoveryContext? discoveryContext, IMessageLogger logger)
    {
        bool hasAnyRunnableTests = false;
        var fixtureTests = new List<TestCase>();

        // Get filter expression and skip discovery in case filter expression has parsing error.
        ITestCaseFilterExpression? filterExpression = _testMethodFilter.GetFilterExpression(discoveryContext, logger, out bool filterHasError);
        if (filterHasError)
        {
            return;
        }

        foreach (UnitTestElement testElement in testElements)
        {
            var testCase = testElement.ToTestCase();
            bool hasFixtureTraits = testCase.Traits.Any(t => t.Name == EngineConstants.FixturesTestTrait);

            // Filter tests based on test case filters
            if (filterExpression != null && !filterExpression.MatchTestCase(testCase, p => _testMethodFilter.PropertyValueProvider(testCase, p)))
            {
                // If test is a fixture test, add it to the list of fixture tests.
                if (hasFixtureTraits)
                {
                    fixtureTests.Add(testCase);
                }

                continue;
            }

            if (testElement.TestMethod.ActualData is { } actualData)
            {
                TestCaseToDataDictionary.Add(testCase, actualData);
            }

            if (!hasAnyRunnableTests)
            {
                hasAnyRunnableTests = !hasFixtureTraits;
            }

            discoverySink.SendTestCase(testCase);
        }

        // If there are runnable tests, then add all fixture tests to the discovery sink.
        // Scenarios:
        // 1. Execute only a fixture test => In this case, we do not need to track any other fixture tests. Selected fixture test will be tracked as will be marked as skipped.
        // 2. Execute a runnable test => In this case, case add all fixture tests. We will update status of only those fixtures which are triggered by the selected test.
        if (hasAnyRunnableTests)
        {
            foreach (TestCase testCase in fixtureTests)
            {
                discoverySink.SendTestCase(testCase);
            }
        }
    }
}
