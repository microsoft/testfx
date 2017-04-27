// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter
{
    using System.Collections.Generic;
    using System.IO;
    using System.Xml;
    using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Discovery;
    using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.ObjectModel;
    using Microsoft.VisualStudio.TestPlatform.ObjectModel.Adapter;
    using Microsoft.VisualStudio.TestPlatform.ObjectModel.Logging;
    using Microsoft.VisualStudio.TestPlatform.ObjectModel.Utilities;

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

            this.SendTestCases(source, testElements, discoverySink, collectSourceInformation: this.CollectSourceInformation(runSettings));
        }

        internal void SendTestCases(string source, IEnumerable<UnitTestElement> testElements, ITestCaseDiscoverySink discoverySink, bool collectSourceInformation = true)
        {
            object navigationSession = null;
            try
            {
                if (collectSourceInformation == true)
                {
                    navigationSession = PlatformServiceProvider.Instance.FileOperations.CreateNavigationSession(source);
                }

                foreach (var testElement in testElements)
                {
                    var testCase = testElement.ToTestCase();

                    if (navigationSession != null && collectSourceInformation)
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

        private bool CollectSourceInformation(IRunSettings runSettings)
        {
            bool collectSourceInformation = true;

            if (runSettings != null && !string.IsNullOrEmpty(runSettings.SettingsXml))
            {
                StringReader stringReader = new StringReader(runSettings.SettingsXml);
                XmlReader reader = XmlReader.Create(stringReader, XmlRunSettingsUtilities.ReaderSettings);

                if (reader.ReadToFollowing("CollectSourceInformation"))
                {
                    if (!bool.TryParse(reader.ReadInnerXml(), out collectSourceInformation))
                    {
                        collectSourceInformation = true;
                    }
                }
            }

            return collectSourceInformation;
        }
    }
}
