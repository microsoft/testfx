﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#pragma warning disable TPEXP // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.

using Microsoft.Testing.Extensions.VSTestBridge.CommandLine;
using Microsoft.Testing.Extensions.VSTestBridge.Resources;
using Microsoft.Testing.Platform;
using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Configurations;
using Microsoft.Testing.Platform.Services;
using Microsoft.Testing.Platform.TestHost;

namespace Microsoft.Testing.Extensions.VSTestBridge.ObjectModel;

internal static class RunSettingsPatcher
{
    private static readonly char[] TestRunParameterSeparator = ['='];

    public static XDocument Patch(string? runSettingsXml, IConfiguration configuration, IClientInfo client, ICommandLineOptions commandLineOptions)
    {
        XDocument runSettingsDocument = PatchSettingsWithDefaults(runSettingsXml, isDesignMode: client.Id == WellKnownClients.VisualStudio, configuration);
        PatchTestRunParameters(runSettingsDocument, commandLineOptions);

        return runSettingsDocument;
    }

    private static XDocument PatchSettingsWithDefaults(string? runSettingsXml, bool isDesignMode, IConfiguration configuration)
    {
        if (RoslynString.IsNullOrWhiteSpace(runSettingsXml))
        {
            runSettingsXml = """
                <RunSettings>
                </RunSettings>
                """;
        }

        var document = XDocument.Parse(runSettingsXml);
        if (document.Element("RunSettings") is not { } runSettingsElement)
        {
            throw new InvalidOperationException(ExtensionResources.MissingRunSettingsAttribute);
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
        if (!commandLineOptions.TryGetOptionArgumentList(TestRunParametersCommandLineOptionsProvider.TestRunParameterOptionName, out string[]? testRunParameters))
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
}
