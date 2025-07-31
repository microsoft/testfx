// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.VisualStudio.TestPlatform.ObjectModel.Adapter;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Logging;

using MSTest.PlatformServices;
using MSTest.PlatformServices.Interface;
using MSTest.PlatformServices.ObjectModel;

namespace MSTest.TestAdapter;

internal static class MSTestDiscovererHelpers
{
    /// <summary>
    /// Verifies if the sources are valid for the target platform.
    /// </summary>
    /// <param name="sources">The test sources.</param>
    /// <param name="testSourceHandler">The test source.</param>
    /// <remarks>Sources cannot be null.</remarks>
    /// <returns>True if the source has a valid extension for the current platform.</returns>
    internal static bool AreValidSources(IEnumerable<string> sources, ITestSourceHandler testSourceHandler)
        => sources.Any(source => testSourceHandler.ValidSourceExtensions.Any(extension => string.Equals(Path.GetExtension(source), extension, StringComparison.OrdinalIgnoreCase)));

    internal static bool InitializeDiscovery(IEnumerable<string> sources, IDiscoveryContext? discoveryContext, IMessageLogger messageLogger, IConfiguration? configuration, ITestSourceHandler testSourceHandler)
    {
        if (!AreValidSources(sources, testSourceHandler))
        {
            throw new NotSupportedException(Resource.SourcesNotSupported);
        }

        // Populate the runsettings.
        try
        {
            MSTestSettings.PopulateSettings(discoveryContext, messageLogger, configuration);
        }
        catch (AdapterSettingsException ex)
        {
            messageLogger.SendMessage(TestMessageLevel.Error, ex.Message);
            return false;
        }

        // Scenarios that include testsettings or forcing a run via the legacy adapter are currently not supported in MSTestAdapter.
        return !MSTestSettings.IsLegacyScenario(messageLogger);
    }
}
