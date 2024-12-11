// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Diagnostics.CodeAnalysis;
using System.Globalization;

using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Discovery;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Adapter;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter;

[SuppressMessage("Performance", "CA1852: Seal internal types", Justification = "Overrides required for testability")]
internal class UnitTestDiscoverer
{
    private readonly AssemblyEnumeratorWrapper _assemblyEnumeratorWrapper;

    internal UnitTestDiscoverer()
    {
        _assemblyEnumeratorWrapper = new AssemblyEnumeratorWrapper();
        _testMethodFilter = new TestMethodFilter();
    }

    private readonly TestMethodFilter _testMethodFilter;

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
        ICollection<UnitTestElement>? testElements = _assemblyEnumeratorWrapper.GetTests(source, discoveryContext?.RunSettings, out List<string> warnings);

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

        SendTestCases(source, testElements, discoverySink, discoveryContext, logger);
    }

    internal void SendTestCases(string source, IEnumerable<UnitTestElement> testElements, ITestCaseDiscoverySink discoverySink, IDiscoveryContext? discoveryContext, IMessageLogger logger)
    {
        bool shouldCollectSourceInformation = MSTestSettings.RunConfigurationSettings.CollectSourceInformation;
        bool hasAnyRunnableTests = false;
        var fixtureTests = new List<TestCase>();

        var navigationSessions = new Dictionary<string, object?>();
        try
        {
            if (shouldCollectSourceInformation)
            {
                navigationSessions.Add(source, PlatformServiceProvider.Instance.FileOperations.CreateNavigationSession(source));
            }

            // Get filter expression and skip discovery in case filter expression has parsing error.
            ITestCaseFilterExpression? filterExpression = _testMethodFilter.GetFilterExpression(discoveryContext, logger, out bool filterHasError);
            if (filterHasError)
            {
                return;
            }

            foreach (UnitTestElement testElement in testElements)
            {
                var testCase = testElement.ToTestCase();
                bool hasFixtureTraits = testCase.Traits.Any(t => t.Name == Constants.FixturesTestTrait);

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

                if (!hasAnyRunnableTests)
                {
                    hasAnyRunnableTests = !hasFixtureTraits;
                }

                if (!shouldCollectSourceInformation)
                {
                    discoverySink.SendTestCase(testCase);
                    continue;
                }

                string testSource = testElement.TestMethod.DeclaringAssemblyName ?? source;

                if (!navigationSessions.TryGetValue(testSource, out object? testNavigationSession))
                {
                    testNavigationSession = PlatformServiceProvider.Instance.FileOperations.CreateNavigationSession(testSource);
                    navigationSessions.Add(testSource, testNavigationSession);
                }

                if (testNavigationSession == null)
                {
                    discoverySink.SendTestCase(testCase);
                    continue;
                }

                string className = testElement.TestMethod.DeclaringClassFullName
                                ?? testElement.TestMethod.FullClassName;

                string methodName = testElement.TestMethod.Name;

                // If it is async test method use compiler generated type and method name for navigation data.
                if (!StringEx.IsNullOrEmpty(testElement.AsyncTypeName))
                {
                    className = testElement.AsyncTypeName;

                    // compiler generated method name is "MoveNext".
                    methodName = "MoveNext";
                }

                PlatformServiceProvider.Instance.FileOperations.GetNavigationData(
                    testNavigationSession,
                    className,
                    methodName,
                    out int minLineNumber,
                    out string? fileName);

                if (!StringEx.IsNullOrEmpty(fileName))
                {
                    testCase.LineNumber = minLineNumber;
                    testCase.CodeFilePath = fileName;
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
        finally
        {
            foreach (object? navigationSession in navigationSessions.Values)
            {
                PlatformServiceProvider.Instance.FileOperations.DisposeNavigationSession(navigationSession);
            }
        }
    }
}
