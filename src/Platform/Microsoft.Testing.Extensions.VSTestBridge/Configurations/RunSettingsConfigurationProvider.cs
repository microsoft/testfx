// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Xml.Linq;

using Microsoft.Testing.Extensions.VSTestBridge.CommandLine;
using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Configurations;
using Microsoft.Testing.Platform.Extensions;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Adapter;

namespace Microsoft.Testing.Extensions.VSTestBridge.Configurations;

internal sealed class RunSettingsConfigurationProvider : IConfigurationSource, IConfigurationProvider
{
    private string? _runSettingsFilePath;
    private string? _resultsDirectory;

    public RunSettingsConfigurationProvider(IExtension extension)
    {
        Uid = extension.Uid;
        Version = extension.Version;
        DisplayName = extension.DisplayName;
        Description = extension.Description;
    }

    /// <inheritdoc />
    public string Uid { get; }

    /// <inheritdoc />
    public string Version { get; }

    /// <inheritdoc />
    public string DisplayName { get; }

    /// <inheritdoc />
    public string Description { get; }

    /// <inheritdoc />
    public Task<bool> IsEnabledAsync() => Task.FromResult(true);

    /// <inheritdoc />
    public Task LoadAsync()
    {
        var document = XDocument.Parse(_runsettings);
        value = document.Element("RunSettings")?.Element("RunConfiguration")?.Element("ResultsDirectory")?.Value;
    }

    /// <inheritdoc />
    public bool TryGet(string key, out string? value)
    {
        if (_runSettingsFilePath is null)
        {
            value = null;
            return false;
        }

        if (key == PlatformConfigurationConstants.PlatformResultDirectory)
        {
            var document = XDocument.Parse(_runsettings);
            value = document.Element("RunSettings")?.Element("RunConfiguration")?.Element("ResultsDirectory")?.Value;
            if (value is not null)
            {
                return true;
            }
        }

        value = null;
        return false;
    }

    /// <inheritdoc />
    public IConfigurationProvider Build(CommandLineParseResult commandLineParseResult)
    {
        if (commandLineParseResult.TryGetOptionArgumentList(RunSettingsCommandLineOptionsProvider.RunSettingsOptionName, out string[]? runSettingsFilePath))
        {
            _runSettingsFilePath = runSettingsFilePath[0];
        }

        return this;
    }
}
