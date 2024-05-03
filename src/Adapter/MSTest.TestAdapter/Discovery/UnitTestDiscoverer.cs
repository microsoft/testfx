// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Globalization;

using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Discovery;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Adapter;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter;

internal class UnitTestDiscoverer
{
    private readonly AssemblyEnumeratorWrapper _assemblyEnumeratorWrapper;

    internal UnitTestDiscoverer()
    {
        _assemblyEnumeratorWrapper = new AssemblyEnumeratorWrapper();
        TestMethodFilter = new TestMethodFilter();
    }

    /// <summary>
    /// Gets or sets method filter for filtering tests.
    /// </summary>
    private TestMethodFilter TestMethodFilter { get; set; }

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
        TimeSpan getTests;
        var sw = Stopwatch.StartNew();
        ICollection<UnitTestElement>? testElements = _assemblyEnumeratorWrapper.GetTests(source, discoveryContext?.RunSettings, out ICollection<string>? warnings);
        getTests = sw.Elapsed;
        sw.Restart();

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
        TimeSpan sendOverhead = sw.Elapsed;
        Debug.WriteLine(">>>>>>>>>>>>>>>>>>>>>>>>>>>");
        Console.WriteLine($"discovered: {testElements.Count} tests in {getTests.TotalMilliseconds} ms, sent them in {sendOverhead.TotalMilliseconds} ms, total: {sendOverhead.TotalMilliseconds + getTests.TotalMilliseconds}");
    }

    internal void SendTestCases(string source, IEnumerable<UnitTestElement> testElements, ITestCaseDiscoverySink discoverySink, IDiscoveryContext? discoveryContext, IMessageLogger logger)
    {
        bool shouldCollectSourceInformation = MSTestSettings.RunConfigurationSettings.CollectSourceInformation;

        var navigationSessions = new Dictionary<string, object?>();
        try
        {
            if (shouldCollectSourceInformation)
            {
                navigationSessions.Add(source, PlatformServiceProvider.Instance.FileOperations.CreateNavigationSession(source));
            }

            // Get filter expression and skip discovery in case filter expression has parsing error.
            ITestCaseFilterExpression? filterExpression = TestMethodFilter.GetFilterExpression(discoveryContext, logger, out bool filterHasError);
            if (filterHasError)
            {
                return;
            }

            foreach (UnitTestElement testElement in testElements)
            {
                var testCase = testElement.ToTestCase();

                // Filter tests based on test case filters
                if (filterExpression != null && !filterExpression.MatchTestCase(testCase, (p) => TestMethodFilter.PropertyValueProvider(testCase, p)))
                {
                    continue;
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
