// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter
{
    using System.Collections.Generic;
    using System.Globalization;
    using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Discovery;
    using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.ObjectModel;
    using Microsoft.VisualStudio.TestPlatform.ObjectModel;
    using Microsoft.VisualStudio.TestPlatform.ObjectModel.Adapter;
    using Microsoft.VisualStudio.TestPlatform.ObjectModel.Logging;

    internal class UnitTestDiscoverer
    {
        private readonly AssemblyEnumeratorWrapper assemblyEnumeratorWrapper;

        internal UnitTestDiscoverer()
        {
            this.assemblyEnumeratorWrapper = new AssemblyEnumeratorWrapper();
            this.TestMethodFilter = new TestMethodFilter();
        }

        /// <summary>
        /// Gets or sets method filter for filtering tests
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
            foreach (var source in sources)
            {
                this.DiscoverTestsInSource(source, logger, discoverySink, discoveryContext);
            }
        }

        /// <summary>
        /// Get the tests from the parameter source
        /// </summary>
        /// <param name="source"> The source. </param>
        /// <param name="logger"> The logger. </param>
        /// <param name="discoverySink"> The discovery Sink. </param>
        /// <param name="discoveryContext"> The discovery context. </param>
        internal virtual void DiscoverTestsInSource(
            string source,
            IMessageLogger logger,
            ITestCaseDiscoverySink discoverySink,
            IDiscoveryContext discoveryContext)
        {
            ICollection<string> warnings;

            var testElements = this.assemblyEnumeratorWrapper.GetTests(source, discoveryContext?.RunSettings, out warnings);

            // log the warnings
            foreach (var warning in warnings)
            {
                PlatformServiceProvider.Instance.AdapterTraceLogger.LogInfo(
                    "MSTestDiscoverer: Warning during discovery from {0}. {1} ",
                    source,
                    warning);
                var message = string.Format(CultureInfo.CurrentCulture, Resource.DiscoveryWarning, source, warning);
                logger.SendMessage(TestMessageLevel.Warning, message);
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

            this.SendTestCases(source, testElements, discoverySink, discoveryContext, logger);
        }

        internal void SendTestCases(string source, IEnumerable<UnitTestElement> testElements, ITestCaseDiscoverySink discoverySink, IDiscoveryContext discoveryContext, IMessageLogger logger)
        {
            var shouldCollectSourceInformation = MSTestSettings.RunConfigurationSettings.CollectSourceInformation;

            var navigationSessions = new Dictionary<string, object>();
            try
            {
                if (shouldCollectSourceInformation)
                {
                    navigationSessions.Add(source, PlatformServiceProvider.Instance.FileOperations.CreateNavigationSession(source));
                }

                // Get filter expression and skip discovery in case filter expression has parsing error.
                bool filterHasError = false;
                ITestCaseFilterExpression filterExpression = this.TestMethodFilter.GetFilterExpression(discoveryContext, logger, out filterHasError);
                if (filterHasError)
                {
                    return;
                }

                foreach (var testElement in testElements)
                {
                    var testCase = testElement.ToTestCase();

                    // Filter tests based on test case filters
                    if (filterExpression != null && filterExpression.MatchTestCase(testCase, (p) => this.TestMethodFilter.PropertyValueProvider(testCase, p)) == false)
                    {
                        continue;
                    }

                    object testNavigationSession;
                    if (shouldCollectSourceInformation)
                    {
                        string testSource = testElement.TestMethod.DeclaringAssemblyName ?? source;

                        if (!navigationSessions.TryGetValue(testSource, out testNavigationSession))
                        {
                            testNavigationSession = PlatformServiceProvider.Instance.FileOperations.CreateNavigationSession(testSource);
                            navigationSessions.Add(testSource, testNavigationSession);
                        }

                        if (testNavigationSession != null)
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
                                testNavigationSession,
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
                    }

                    discoverySink.SendTestCase(testCase);
                }
            }
            finally
            {
                foreach (object navigationSession in navigationSessions.Values)
                {
                    PlatformServiceProvider.Instance.FileOperations.DisposeNavigationSession(navigationSession);
                }
            }
        }
    }
}
