// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Discovery;
    using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.ObjectModel;
    using Microsoft.VisualStudio.TestPlatform.ObjectModel;
    using Microsoft.VisualStudio.TestPlatform.ObjectModel.Adapter;
    using Microsoft.VisualStudio.TestPlatform.ObjectModel.Logging;

    /// <summary>
    /// Contains the discovery logic for this adapter.
    /// </summary>
    [DefaultExecutorUri(TestAdapter.Constants.ExecutorUriString)]
    [FileExtension(".xap")]
    [FileExtension(".appx")]
    [FileExtension(".dll")]
    [FileExtension(".exe")]
    public class MSTestDiscoverer : ITestDiscoverer
    {
        /// <summary>
        /// Discovers the tests available from the provided source. Not supported for .xap source.
        /// </summary>
        /// <param name="sources">Collection of test containers.</param>
        /// <param name="discoveryContext">Context in which discovery is being performed.</param>
        /// <param name="logger">Logger used to log messages.</param>
        /// <param name="discoverySink">Used to send testcases and discovery related events back to Discoverer manager.</param>
        [System.Security.SecurityCritical]
        [System.Diagnostics.CodeAnalysis.SuppressMessage(
            "Microsoft.Design",
            "CA1062:Validate arguments of public methods", MessageId = "0")]
        public void DiscoverTests(
            IEnumerable<string> sources,
            IDiscoveryContext discoveryContext,
            IMessageLogger logger,
            ITestCaseDiscoverySink discoverySink)
        {
            ValidateArg.NotNull(sources, "sources");
            ValidateArg.NotNull(logger, "logger");
            ValidateArg.NotNull(discoverySink, "discoverySink");

            if (!this.AreValidSources(sources))
            {
                throw new NotSupportedException(Resource.SourcesNotSupported);
            }

            // Populate the runsettings.
            MSTestSettings.PopulateSettings(discoveryContext);

            // Scenarios that include testsettings or forcing a run via the legacy adapter are currently not supported in MSTestAdapter.
            if (MSTestSettings.IsLegacyScenario(logger))
            {
                return;
            }

            new UnitTestDiscoverer().DiscoverTests(sources, logger, discoverySink, discoveryContext?.RunSettings);
        }

        /// <summary>
        /// Verifies if the sources are valid for the target platform.
        /// </summary>
        /// <param name="sources">The test sources</param>
        /// <remarks>Sources cannot be null.</remarks>
        /// <returns>True if the source has a valid extension for the current platform.</returns>
        internal bool AreValidSources(IEnumerable<string> sources)
        {
            // ValidSourceExtensions is always expected to return a non-null list.
            return
                sources.Any(
                    source =>
                    (PlatformServiceProvider.Instance.TestSource.ValidSourceExtensions.Any(
                        extension =>
                        string.Compare(Path.GetExtension(source), extension, StringComparison.OrdinalIgnoreCase) == 0)));
        }
    }

    internal class UnitTestDiscoverer
    {
        private readonly AssemblyEnumeratorWrapper assemblyEnumeratorWrapper;

        internal UnitTestDiscoverer()
        {
            this.assemblyEnumeratorWrapper = new AssemblyEnumeratorWrapper();
        }

        /// <summary>
        /// Discovers the tests available from the provided sources.
        /// </summary>
        /// <param name="sources"> The sources. </param>
        /// <param name="logger"> The logger. </param>
        /// <param name="discoverySink"> The discovery Sink. </param>
        /// <param name="runSettings"> The run settings. </param>
        internal void DiscoverTests(
            IEnumerable<string> sources,
            IMessageLogger logger,
            ITestCaseDiscoverySink discoverySink,
            IRunSettings runSettings)
        {
            foreach (var source in sources)
            {
                this.DiscoverTestsInSource(source, logger, discoverySink, runSettings);
            }
        }

        /// <summary>
        /// Get the tests from the parameter source
        /// </summary>
        /// <param name="source"> The source. </param>
        /// <param name="logger"> The logger. </param>
        /// <param name="discoverySink"> The discovery Sink. </param>
        /// <param name="runSettings"> The run settings. </param>
        internal virtual void DiscoverTestsInSource(
            string source,
            IMessageLogger logger,
            ITestCaseDiscoverySink discoverySink,
            IRunSettings runSettings)
        {
            ICollection<string> warnings;

            var testElements = this.assemblyEnumeratorWrapper.GetTests(source, runSettings, out warnings);

            // log the warnings
            foreach (var warning in warnings)
            {
                PlatformServiceProvider.Instance.AdapterTraceLogger.LogInfo(
                    "MSTestDiscoverer: Warning during discovery from {0}. {1} ",
                    source,
                    warning);
                logger.SendMessage(TestMessageLevel.Warning, warning);
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

            this.SendTestCases(source, testElements, discoverySink);
        }

        internal void SendTestCases(string source, IEnumerable<UnitTestElement> testElements, ITestCaseDiscoverySink discoverySink)
        {
            object navigationSession = null;
            try
            {
                navigationSession = PlatformServiceProvider.Instance.FileOperations.CreateNavigationSession(source);
                foreach (var testElement in testElements)
                {
                    var testCase = testElement.ToTestCase();

                    if (navigationSession != null)
                    {
                        var className = testElement.TestMethod.DeclaringClassFullName
                                        ?? testElement.TestMethod.FullClassName;

                        var methodName = testElement.TestMethod.Name;

                        // If it is async test method use compiler generated type and method name for navigation data.
                        if (!string.IsNullOrEmpty(testElement.AsyncTypeName))
                        {
                            className = testElement.AsyncTypeName;

                            // compiler generated method name is "MoveNext".
                            methodName = "MoveNext";
                        }

                        int minLineNumber;
                        string fileName;

                        PlatformServiceProvider.Instance.FileOperations.GetNavigationData(
                            navigationSession,
                            className,
                            methodName,
                            out minLineNumber,
                            out fileName);

                        if (!string.IsNullOrEmpty(fileName))
                        {
                            testCase.LineNumber = minLineNumber;
                            testCase.CodeFilePath = fileName;
                        }
                    }

                    discoverySink.SendTestCase(testCase);
                }
            }
            finally
            {
                PlatformServiceProvider.Instance.FileOperations.DisposeNavigationSession(navigationSession);
            }
        }
    }
}
