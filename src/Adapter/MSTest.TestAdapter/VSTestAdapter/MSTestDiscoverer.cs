// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.VisualStudio.TestPlatform.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Adapter;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Logging;

namespace Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter;

/// <summary>
/// Contains the discovery logic for this adapter.
/// </summary>
[DefaultExecutorUri(Constants.ExecutorUriString)]
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
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1062:Validate arguments of public methods", MessageId = "0", Justification = "Discovery context can be null.")]
    public void DiscoverTests(IEnumerable<string> sources, IDiscoveryContext discoveryContext, IMessageLogger logger, ITestCaseDiscoverySink discoverySink)
    {
        ValidateArg.NotNull(sources, "sources");
        ValidateArg.NotNull(logger, "logger");
        ValidateArg.NotNull(discoverySink, "discoverySink");

        if (MSTestDiscovererHelpers.InitializeDiscovery(sources, discoveryContext, logger))
        {
            new UnitTestDiscoverer().DiscoverTests(sources, logger, discoverySink, discoveryContext);
        }
    }
}
