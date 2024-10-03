﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Xml.Linq;

using Microsoft.Testing.Extensions.VSTestBridge.CommandLine;
using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Configurations;
using Microsoft.Testing.Platform.Helpers;

namespace Microsoft.Testing.Extensions.VSTestBridge.Configurations;

internal sealed class RunSettingsConfigurationProvider(IFileSystem fileSystem) : IConfigurationSource, IConfigurationProvider
{
    private readonly IFileSystem _fileSystem = fileSystem;

    private string? _runSettingsFileContent;

    /// <inheritdoc />
    public string Uid { get; } = nameof(RunSettingsConfigurationProvider);

    /// <inheritdoc />
    public string Version { get; } = AppVersion.DefaultSemVer;

    /// <inheritdoc />
    public string DisplayName { get; } = "VSTest Helpers: runsettings configuration";

    /// <inheritdoc />
    public string Description { get; } = "Configuration source to bridge VSTest xml runsettings configuration into Microsoft Testing Platform configuration model.";

    public int Order => 2;

    /// <inheritdoc />
    public Task<bool> IsEnabledAsync() => Task.FromResult(true);

    /// <inheritdoc />
    public Task LoadAsync() => Task.CompletedTask;

    /// <inheritdoc />
    public bool TryGet(string key, out string? value)
    {
        if (_runSettingsFileContent is null)
        {
            value = null;
            return false;
        }

        if (key == PlatformConfigurationConstants.PlatformResultDirectory)
        {
            var document = XDocument.Parse(_runSettingsFileContent);
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
    public async Task<IConfigurationProvider> BuildAsync(CommandLineParseResult commandLineParseResult)
    {
        if (commandLineParseResult.TryGetOptionArgumentList(RunSettingsCommandLineOptionsProvider.RunSettingsOptionName, out string[]? runSettingsFilePath))
        {
            if (_fileSystem.Exists(runSettingsFilePath[0]))
            {
                _runSettingsFileContent = await _fileSystem.ReadAllTextAsync(runSettingsFilePath[0]);
            }
        }

        return this;
    }
}
