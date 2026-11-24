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
    private readonly TestPartition? _partition;
    private readonly Action<UnitTestElement>? _unpartitionedTestObserver;

    internal UnitTestDiscoverer(ITestSourceHandler testSourceHandler)
        : this(testSourceHandler, partition: null, unpartitionedTestObserver: null)
    {
    }

    internal UnitTestDiscoverer(ITestSourceHandler testSourceHandler, TestPartition? partition, Action<UnitTestElement>? unpartitionedTestObserver = null)
    {
        _testSource = testSourceHandler;
        _partition = partition;
        _unpartitionedTestObserver = unpartitionedTestObserver;
    }

    /// <summary>
    /// Discovers the tests available from the provided sources.
    /// </summary>
    /// <param name="sources"> The sources. </param>
    /// <param name="logger"> The logger. </param>
    /// <param name="discoverySink"> The discovery Sink. </param>
    /// <param name="settingsXml"> The run settings XML, or <see langword="null"/> when none was provided. </param>
    /// <param name="filterProvider">Provider for the test filter, or <see langword="null"/> for no filter.</param>
    /// <param name="isMTP">Flag set to true when the platform running discovery is MTP.</param>
    internal async Task DiscoverTestsAsync(
        IEnumerable<string> sources,
        IAdapterMessageLogger logger,
        IUnitTestElementSink discoverySink,
        string? settingsXml,
        ITestElementFilterProvider? filterProvider,
        bool isMTP)
    {
        foreach (string source in sources)
        {
            await DiscoverTestsInSourceAsync(source, logger, discoverySink, settingsXml, filterProvider, isMTP).ConfigureAwait(false);
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
    internal virtual async Task DiscoverTestsInSourceAsync(
        string source,
        IAdapterMessageLogger logger,
        IUnitTestElementSink discoverySink,
        string? settingsXml,
        ITestElementFilterProvider? filterProvider,
        bool isMTP)
    {
        ICollection<UnitTestElement>? testElements = GetTestElements(source, settingsXml, isMTP, out List<string> warnings, out int discoveredTestCount);

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
        if (testElements is null || discoveredTestCount == 0)
        {
            return;
        }

        if (PlatformServiceProvider.Instance.AdapterTraceLogger.IsInfoEnabled)
        {
            PlatformServiceProvider.Instance.AdapterTraceLogger.Info(
                "MSTestDiscoverer: Found {0} tests from source {1}",
                discoveredTestCount,
                source);
        }

        await SendTestCasesAsync(testElements, discoverySink, filterProvider, logger, _partition, _unpartitionedTestObserver).ConfigureAwait(false);
    }

    private readonly ITestSourceHandler _testSource;

    internal virtual ICollection<UnitTestElement>? GetTestElements(
        string source,
        string? settingsXml,
        bool isMTP,
        out List<string> warnings,
        out int discoveredTestCount)
    {
        ICollection<UnitTestElement>? elements = AssemblyEnumeratorWrapper.GetTests(source, settingsXml, _testSource, isMTP, out warnings);
        discoveredTestCount = elements?.Count ?? 0;
        return elements;
    }

    internal static Task SendTestCasesAsync(IEnumerable<UnitTestElement> testElements, IUnitTestElementSink discoverySink, ITestElementFilterProvider? filterProvider, IAdapterMessageLogger logger)
        => SendTestCasesAsync(testElements, discoverySink, filterProvider, logger, partition: null, unpartitionedTestObserver: null);

    private static async Task SendTestCasesAsync(
        IEnumerable<UnitTestElement> testElements,
        IUnitTestElementSink discoverySink,
        ITestElementFilterProvider? filterProvider,
        IAdapterMessageLogger logger,
        TestPartition? partition,
        Action<UnitTestElement>? unpartitionedTestObserver)
    {
        // Get filter and skip discovery in case filter expression has parsing error.
        bool filterHasError = false;
        ITestElementFilter? filter = filterProvider?.GetTestElementFilter(logger, out filterHasError);
        if (filterHasError)
        {
            return;
        }

        HashSet<UnitTestElement>? partitionTests = null;
        if (partition is not null)
        {
            ICollection<UnitTestElement> allTests = testElements as ICollection<UnitTestElement> ?? [.. testElements];
            partitionTests = [.. partition.SelectTests(allTests, MSTestSettings.CurrentSettings.DeclaredDependencies)];
            testElements = allTests;
        }

        foreach (UnitTestElement testElement in testElements)
        {
            // Filter tests based on test case filters.
            if (filter is not null && !filter.Matches(testElement))
            {
                continue;
            }

            unpartitionedTestObserver?.Invoke(testElement);
            if (partitionTests is not null && !partitionTests.Contains(testElement))
            {
                continue;
            }

            await discoverySink.SendTestElementAsync(testElement).ConfigureAwait(false);
        }
    }
}
