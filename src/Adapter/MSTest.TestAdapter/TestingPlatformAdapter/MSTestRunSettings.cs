// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if !WINDOWS_UWP
using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Configurations;
using Microsoft.Testing.Platform.Services;
using Microsoft.Testing.Platform.TestHost;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Resources;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Adapter;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Logging;

using IFileSystem = Microsoft.Testing.Platform.Helpers.IFileSystem;

namespace Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.TestingPlatformAdapter;

/// <summary>
/// A native Microsoft.Testing.Platform (MTP) implementation of the VSTest <see cref="IRunSettings"/> for the MSTest
/// native path. It reads the runsettings XML (from the <c>--settings</c> option or the runsettings environment
/// variables), patches it with MTP defaults (design mode, results directory, <c>--test-parameter</c> overrides) and
/// warns on entries MTP does not support — mirroring the VSTest bridge's <c>RunSettingsAdapter</c> /
/// <c>RunSettingsPatcher</c> / <c>RunSettingsHelpers</c> without depending on the bridge.
/// </summary>
[SuppressMessage("ApiDesign", "RS0030:Do not use banned APIs", Justification = "We can use MTP from this folder")]
internal sealed class MSTestRunSettings : IRunSettings
{
    private static readonly char[] TestRunParameterSeparator = ['='];

    private static readonly string[] UnsupportedRunConfigurationSettings =
    [
        "DotnetHostPath",
        "MaxCpuCount",
        "TargetFrameworkVersion",
        "TargetPlatform",
        "TestAdaptersPaths",
        "TestSessionTimeout",
        "TreatNoTestsAsError",
        "TreatTestAdapterErrorsAsWarnings",
    ];

    public MSTestRunSettings(
        ICommandLineOptions commandLineOptions,
        IFileSystem fileSystem,
        IConfiguration configuration,
        IClientInfo client,
        IMessageLogger messageLogger)
    {
        _ = commandLineOptions.TryGetOptionArgumentList(MSTestRunSettingsCommandLineOptionsProvider.RunSettingsOptionName, out string[]? fileNames);
        string runSettingsXml = ReadRunSettings(fileNames, fileSystem);

        XDocument runSettingsDocument = Patch(runSettingsXml, configuration, client, commandLineOptions);
        WarnOnUnsupportedEntries(runSettingsDocument, messageLogger);

        SettingsXml = runSettingsDocument.ToString();
    }

    public string? SettingsXml { get; }

    // Not used by MSTest (matches the bridge's RunSettingsAdapter).
    public ISettingsProvider? GetSettings(string? settingsName) => throw new NotImplementedException();

    internal static string ReadRunSettings(string[]? fileNames, IFileSystem fileSystem)
    {
        // Use the first provided --settings path rather than requiring exactly one, matching the runsettings
        // environment-variable provider that reads the same option, so a valid value is not ignored when more
        // than one argument is present.
        if (fileNames is { Length: > 0 } && fileSystem.ExistFile(fileNames[0]))
        {
            return fileSystem.ReadAllText(fileNames[0]);
        }

        string? envVariableRunSettings = Environment.GetEnvironmentVariable("TESTINGPLATFORM_EXPERIMENTAL_VSTEST_RUNSETTINGS");
        if (!string.IsNullOrEmpty(envVariableRunSettings))
        {
            return envVariableRunSettings;
        }

        string? runSettingsFilePath = Environment.GetEnvironmentVariable("TESTINGPLATFORM_VSTESTBRIDGE_RUNSETTINGS_FILE");
        return !string.IsNullOrEmpty(runSettingsFilePath) && fileSystem.ExistFile(runSettingsFilePath)
            ? fileSystem.ReadAllText(runSettingsFilePath)
            : string.Empty;
    }

    private static XDocument Patch(string? runSettingsXml, IConfiguration configuration, IClientInfo client, ICommandLineOptions commandLineOptions)
    {
        XDocument runSettingsDocument = PatchSettingsWithDefaults(runSettingsXml, isDesignMode: client.Id == WellKnownClients.VisualStudio, configuration);
        PatchTestRunParameters(runSettingsDocument, commandLineOptions);
        return runSettingsDocument;
    }

    private static XDocument PatchSettingsWithDefaults(string? runSettingsXml, bool isDesignMode, IConfiguration configuration)
    {
        if (string.IsNullOrWhiteSpace(runSettingsXml))
        {
            runSettingsXml = """
                <RunSettings>
                </RunSettings>
                """;
        }

        var document = XDocument.Parse(runSettingsXml);
        if (document.Element("RunSettings") is not { } runSettingsElement)
        {
            throw new InvalidOperationException(PlatformAdapterResources.MissingRunSettingsAttribute);
        }

        bool isPatchingCommentAdded = false;
        if (runSettingsElement.Element("RunConfiguration") is not { } runConfigurationElement)
        {
            runConfigurationElement = new XElement("RunConfiguration");
            AddPatchingCommentIfNeeded(runConfigurationElement, ref isPatchingCommentAdded);
            runSettingsElement.AddFirst(runConfigurationElement);
        }

        if (runConfigurationElement.Element("DesignMode") is null)
        {
            AddPatchingCommentIfNeeded(runConfigurationElement, ref isPatchingCommentAdded);
            runConfigurationElement.Add(new XElement("DesignMode", isDesignMode));
        }

        if (runConfigurationElement.Element("CollectSourceInformation") is null)
        {
            AddPatchingCommentIfNeeded(runConfigurationElement, ref isPatchingCommentAdded);
            runConfigurationElement.Add(new XElement("CollectSourceInformation", false));
        }

        if (runConfigurationElement.Element("ResultsDirectory") is null)
        {
            AddPatchingCommentIfNeeded(runConfigurationElement, ref isPatchingCommentAdded);
            runConfigurationElement.Add(new XElement("ResultsDirectory", configuration.GetTestResultDirectory()));
        }

        if (isPatchingCommentAdded)
        {
            runConfigurationElement.Add(new XComment("End"));
        }

        return document;
    }

    private static void AddPatchingCommentIfNeeded(XElement element, ref bool isPatchingCommentAdded)
    {
        if (isPatchingCommentAdded)
        {
            return;
        }

        element.Add(new XComment("Default configuration added by Microsoft Testing Platform"));
        isPatchingCommentAdded = true;
    }

    private static void PatchTestRunParameters(XDocument runSettingsDocument, ICommandLineOptions commandLineOptions)
    {
        if (!commandLineOptions.TryGetOptionArgumentList(MSTestTestRunParametersCommandLineOptionsProvider.TestRunParameterOptionName, out string[]? testRunParameters))
        {
            return;
        }

        XElement runSettingsElement = runSettingsDocument.Element("RunSettings")!;
        XElement? testRunParametersElement = runSettingsElement.Element("TestRunParameters");
        if (testRunParametersElement is null)
        {
            testRunParametersElement = new XElement("TestRunParameters");
            runSettingsElement.Add(testRunParametersElement);
        }

        XElement[] testRunParametersNodes = [.. testRunParametersElement.Nodes().OfType<XElement>()];
        foreach (string testRunParameter in testRunParameters)
        {
            string[] parts = testRunParameter.Split(TestRunParameterSeparator, 2);
            string name = parts[0];
            string value = parts[1];
            XElement? existingElement = testRunParametersNodes.FirstOrDefault(x => x.Attribute("name")?.Value == name);
            if (existingElement is not null)
            {
                existingElement.Attribute("value")!.Value = value;
            }
            else
            {
                var parameterElement = new XElement("Parameter");
                parameterElement.Add(new XAttribute("name", name));
                parameterElement.Add(new XAttribute("value", value));
                testRunParametersElement.Add(parameterElement);
            }
        }
    }

    private static void WarnOnUnsupportedEntries(XDocument document, IMessageLogger messageLogger)
    {
        XElement runSettingsElement = document.Element("RunSettings")!;

        if (runSettingsElement.Element("LoggerRunSettings") is not null)
        {
            messageLogger.SendMessage(TestMessageLevel.Warning, PlatformAdapterResources.UnsupportedRunsettingsLoggers);
        }

        if (runSettingsElement.Element("DataCollectionRunSettings") is not null)
        {
            messageLogger.SendMessage(TestMessageLevel.Warning, PlatformAdapterResources.UnsupportedRunsettingsDatacollectors);
        }

        if (runSettingsElement.Element("RunConfiguration") is not { } runConfigurationElement)
        {
            return;
        }

        foreach (string unsupportedRunConfigurationSetting in UnsupportedRunConfigurationSettings.Where(setting => runConfigurationElement.Element(setting) is not null))
        {
            messageLogger.SendMessage(TestMessageLevel.Warning, string.Format(CultureInfo.InvariantCulture, PlatformAdapterResources.UnsupportedRunconfigurationSetting, unsupportedRunConfigurationSetting));
        }
    }
}
#endif
