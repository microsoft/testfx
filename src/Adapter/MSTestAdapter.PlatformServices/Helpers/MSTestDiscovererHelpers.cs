// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Interface;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter;

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

    internal static bool InitializeDiscovery(IEnumerable<string> sources, string? settingsXml, IAdapterMessageLogger messageLogger, IConfiguration? configuration, ITestSourceHandler testSourceHandler)
    {
        if (!AreValidSources(sources, testSourceHandler))
        {
            throw new NotSupportedException(Resource.SourcesNotSupported);
        }

        // Populate the runsettings. Any settings-format error (invalid MSTest setting value or a structural
        // runsettings error) surfaces as an AdapterSettingsException; it is logged as an error and treated as a
        // graceful bail-out (discovery reports no tests / execution runs nothing) rather than escaping to the host.
        try
        {
            MSTestSettings.PopulateSettings(settingsXml, messageLogger, configuration);
            return true;
        }
        catch (AdapterSettingsException ex)
        {
            messageLogger.SendMessage(MessageLevel.Error, ex.Message);
            return false;
        }
    }
}
