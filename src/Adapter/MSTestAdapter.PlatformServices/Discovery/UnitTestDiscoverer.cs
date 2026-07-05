// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Discovery;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Interface;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter;

[SuppressMessage("Performance", "CA1852: Seal internal types", Justification = "Overrides required for testability")]
internal class UnitTestDiscoverer
{
    internal UnitTestDiscoverer(ITestSourceHandler testSourceHandler) => _testSource = testSourceHandler;

    /// <summary>
    /// Discovers the tests available from the provided sources.
    /// </summary>
    /// <param name="sources"> The sources. </param>
    /// <param name="logger"> The logger. </param>
    /// <param name="discoverySink"> The discovery Sink. </param>
    /// <param name="settingsXml"> The run settings XML, or <see langword="null"/> when none was provided. </param>
    /// <param name="filterProvider">Provider for the test filter, or <see langword="null"/> for no filter.</param>
    /// <param name="isMTP">Flag set to true when the platform running discovery is MTP.</param>
    internal void DiscoverTests(
        IEnumerable<string> sources,
        IAdapterMessageLogger logger,
        IUnitTestElementSink discoverySink,
        string? settingsXml,
        ITestElementFilterProvider? filterProvider,
        bool isMTP)
    {
        foreach (string source in sources)
        {
            DiscoverTestsInSource(source, logger, discoverySink, settingsXml, filterProvider, isMTP);
        }
    }

    /// <summary>
    /// Get the tests from the parameter source.
    /// </summary>
    /// <param name="source"> The source. </param>
    /// <param name="logger"> The logger. </param>
    /// <param name="discoverySink"> The discovery Sink. </param>
    /// <param name="settingsXml"> The run settings XML, or <see langword="null"/> when none was provided. </param>
    /// <param name="filterProvider">Provider for the test filter, or <see langword="null"/> for no filter.</param>
    /// <param name="isMTP">Flag set to true when the platform running discovery is MTP.</param>
    internal virtual void DiscoverTestsInSource(
        string source,
        IAdapterMessageLogger logger,
        IUnitTestElementSink discoverySink,
        string? settingsXml,
        ITestElementFilterProvider? filterProvider,
        bool isMTP)
    {
        ICollection<UnitTestElement>? testElements = AssemblyEnumeratorWrapper.GetTests(source, settingsXml, _testSource, isMTP, out List<string> warnings);

        if (MSTestSettings.CurrentSettings.TreatDiscoveryWarningsAsErrors)
        {
            if (warnings.Count > 0)
            {
                throw new MSTestException(
                        string.Format(
                            CultureInfo.CurrentCulture,
                            Resource.DiscoveryErrors,
                            source,
                            warnings.Count,
                            string.Join(Environment.NewLine, warnings)));
            }
        }
        else
        {
            // log the warnings
            foreach (string warning in warnings)
            {
                if (PlatformServiceProvider.Instance.AdapterTraceLogger.IsInfoEnabled)
                {
                    PlatformServiceProvider.Instance.AdapterTraceLogger.Info(
                        "MSTestDiscoverer: Warning during discovery from {0}. {1} ",
                        source,
                        warning);
                }

                string message = string.Format(CultureInfo.CurrentCulture, Resource.DiscoveryWarning, source, warning);
                logger.SendMessage(MessageLevel.Warning, message);
            }
        }

        // No tests found => nothing to do
        if (testElements == null || testElements.Count == 0)
        {
            return;
        }

        if (PlatformServiceProvider.Instance.AdapterTraceLogger.IsInfoEnabled)
        {
            PlatformServiceProvider.Instance.AdapterTraceLogger.Info(
                "MSTestDiscoverer: Found {0} tests from source {1}",
                testElements.Count,
                source);
        }

        SendTestCases(testElements, discoverySink, filterProvider, logger);
    }

    private readonly ITestSourceHandler _testSource;

    internal static void SendTestCases(IEnumerable<UnitTestElement> testElements, IUnitTestElementSink discoverySink, ITestElementFilterProvider? filterProvider, IAdapterMessageLogger logger)
    {
        // Get filter and skip discovery in case filter expression has parsing error.
        bool filterHasError = false;
        ITestElementFilter? filter = filterProvider?.GetTestElementFilter(logger, out filterHasError);
        if (filterHasError)
        {
            return;
        }

        foreach (UnitTestElement testElement in testElements)
        {
            // Filter tests based on test case filters.
            if (filter is not null && !filter.Matches(testElement))
            {
                continue;
            }

            discoverySink.SendTestElement(testElement);
        }
    }
}
