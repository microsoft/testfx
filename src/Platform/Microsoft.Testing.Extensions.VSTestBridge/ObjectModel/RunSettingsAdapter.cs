// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#pragma warning disable TPEXP // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.

using System.Globalization;
using System.Xml.Linq;

using Microsoft.Testing.Extensions.VSTestBridge.CommandLine;
using Microsoft.Testing.Extensions.VSTestBridge.Resources;
using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Configurations;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.Logging;
using Microsoft.Testing.Platform.Services;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Adapter;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Logging;

namespace Microsoft.Testing.Extensions.VSTestBridge.ObjectModel;

/// <summary>
/// A Microsoft Testing Platform oriented implementation of the VSTest <see cref="IRunSettings"/>.
/// </summary>
internal sealed class RunSettingsAdapter : IRunSettings
{
    public RunSettingsAdapter(
        ICommandLineOptions commandLineOptions,
        IFileSystem fileSystem,
        IConfiguration configuration,
        IClientInfo client,
        ILoggerFactory loggerFactory,
        IMessageLogger messageLogger)
    {
        string? runSettingsXml =
            commandLineOptions.TryGetOptionArgumentList(RunSettingsCommandLineOptionsProvider.RunSettingsOptionName, out string[]? fileNames)
            && fileNames is not null
            && fileNames.Length == 1
            && fileSystem.Exists(fileNames[0])
                ? fileSystem.ReadAllText(fileNames[0])
                : Environment.GetEnvironmentVariable("TESTINGPLATFORM_EXPERIMENTAL_VSTEST_RUNSETTINGS");

        XDocument runSettingsDocument = RunSettingsPatcher.Patch(runSettingsXml, configuration, client, commandLineOptions);
        WarnOnUnsupportedEntries(runSettingsDocument, messageLogger);

        SettingsXml = runSettingsDocument.ToString();
        ILogger<RunSettingsAdapter> logger = loggerFactory.CreateLogger<RunSettingsAdapter>();
        logger.LogDebug($"Execution will use the following runsettings:{Environment.NewLine}{SettingsXml}");
    }

    /// <inheritdoc />
    public string? SettingsXml { get; }

    /// <inheritdoc />
    // TODO: Needs to be implemented if used by adapters. It is not used by MSTest.
    public ISettingsProvider? GetSettings(string? settingsName) => throw new NotImplementedException();

    private static void WarnOnUnsupportedEntries(XDocument document, IMessageLogger messageLogger)
    {
        XElement runSettingsElement = document.Element("RunSettings")!;

        if (runSettingsElement.Element("LoggerRunSettings") is not null)
        {
            messageLogger.SendMessage(TestMessageLevel.Warning, ExtensionResources.UnsupportedRunsettingsLoggers);
        }

        if (runSettingsElement.Element("DataCollectionRunSettings") is not null)
        {
            messageLogger.SendMessage(TestMessageLevel.Warning, ExtensionResources.UnsupportedRunsettingsDatacollectors);
        }

        if (runSettingsElement.Element("RunConfiguration") is not { } runConfigurationElement)
        {
            return;
        }

        if (runConfigurationElement.Element("MaxCpuCount") is not null)
        {
            messageLogger.SendMessage(TestMessageLevel.Warning, string.Format(CultureInfo.InvariantCulture, ExtensionResources.UnsupportedRunconfigurationSetting, "MaxCpuCount"));
        }

        if (runConfigurationElement.Element("TargetFrameworkVersion") is not null)
        {
            messageLogger.SendMessage(TestMessageLevel.Warning, string.Format(CultureInfo.InvariantCulture, ExtensionResources.UnsupportedRunconfigurationSetting, "TargetFrameworkVersion"));
        }

        if (runConfigurationElement.Element("TargetPlatform") is not null)
        {
            messageLogger.SendMessage(TestMessageLevel.Warning, string.Format(CultureInfo.InvariantCulture, ExtensionResources.UnsupportedRunconfigurationSetting, "TargetPlatform"));
        }

        if (runConfigurationElement.Element("TreatTestAdapterErrorsAsWarnings") is not null)
        {
            messageLogger.SendMessage(TestMessageLevel.Warning, string.Format(CultureInfo.InvariantCulture, ExtensionResources.UnsupportedRunconfigurationSetting, "TreatTestAdapterErrorsAsWarnings"));
        }

        if (runConfigurationElement.Element("TestAdaptersPaths") is not null)
        {
            messageLogger.SendMessage(TestMessageLevel.Warning, string.Format(CultureInfo.InvariantCulture, ExtensionResources.UnsupportedRunconfigurationSetting, "TestAdaptersPaths"));
        }

        if (runConfigurationElement.Element("TestCaseFilter") is not null)
        {
            messageLogger.SendMessage(TestMessageLevel.Warning, string.Format(CultureInfo.InvariantCulture, ExtensionResources.UnsupportedRunconfigurationSetting, "TestCaseFilter"));
        }

        if (runConfigurationElement.Element("TestSessionTimeout") is not null)
        {
            messageLogger.SendMessage(TestMessageLevel.Warning, string.Format(CultureInfo.InvariantCulture, ExtensionResources.UnsupportedRunconfigurationSetting, "TestSessionTimeout"));
        }

        if (runConfigurationElement.Element("DotnetHostPath") is not null)
        {
            messageLogger.SendMessage(TestMessageLevel.Warning, string.Format(CultureInfo.InvariantCulture, ExtensionResources.UnsupportedRunconfigurationSetting, "DotnetHostPath"));
        }

        if (runConfigurationElement.Element("TreatNoTestsAsError") is not null)
        {
            messageLogger.SendMessage(TestMessageLevel.Warning, string.Format(CultureInfo.InvariantCulture, ExtensionResources.UnsupportedRunconfigurationSetting, "TreatNoTestsAsError"));
        }
    }
}
