// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Execution;
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
            messageLogger.SendMessage(MessageLevel.Error, GetSettingsExceptionMessage(ex));
            return false;
        }
    }

    /// <summary>
    /// Builds the diagnostic message reported for an <see cref="AdapterSettingsException"/>. When the
    /// exception carries an inner exception (e.g. a lower-level parsing/config failure that was wrapped
    /// while validating the runsettings), the full exception chain (type and message for every level) is
    /// included so the underlying cause is not silently lost. Otherwise, the plain message is reported
    /// unchanged to keep existing, well-understood diagnostics stable.
    /// </summary>
    /// <param name="ex">The settings exception to format.</param>
    /// <returns>The message to report to the test host.</returns>
    internal static string GetSettingsExceptionMessage(AdapterSettingsException ex)
        => ex.InnerException is null
            ? ex.Message
            : ex.GetFormattedExceptionMessage();
}
