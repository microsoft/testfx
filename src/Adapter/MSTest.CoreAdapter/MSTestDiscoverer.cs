// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
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
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1062:Validate arguments of public methods", MessageId = "0", Justification ="Discovery context can be null.")]
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
            try
            {
                MSTestSettings.PopulateSettings(discoveryContext);
            }
            catch (AdapterSettingsException ex)
            {
                logger.SendMessage(TestMessageLevel.Error, ex.Message);
                return;
            }

            // Scenarios that include testsettings or forcing a run via the legacy adapter are currently not supported in MSTestAdapter.
            if (MSTestSettings.IsLegacyScenario(logger))
            {
                return;
            }

            new UnitTestDiscoverer().DiscoverTests(sources, logger, discoverySink, discoveryContext);
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
                    PlatformServiceProvider.Instance.TestSource.ValidSourceExtensions.Any(
                        extension =>
                        string.Compare(Path.GetExtension(source), extension, StringComparison.OrdinalIgnoreCase) == 0));
        }
    }
}
