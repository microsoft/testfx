// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Xml.Linq;

using Microsoft.Testing.Platform.Configurations;
using Microsoft.Testing.Platform.Helpers;

namespace Microsoft.Testing.Extensions.VSTestBridge.Configurations;

internal sealed class RunSettingsConfigurationProvider : IConfigurationSource, IConfigurationProvider
{
    private readonly string _runsettings;

    public RunSettingsConfigurationProvider(string runSettings)
    {
        _runsettings = runSettings;
    }

    /// <inheritdoc />
    public string Uid { get; } = nameof(RunSettingsConfigurationProvider);

    /// <inheritdoc />
    public string Version { get; } = AppVersion.DefaultSemVer;

    /// <inheritdoc />
    public string DisplayName { get; } = "VSTest Helpers: runsettings configuration";

    /// <inheritdoc />
    public string Description { get; } = "Configuration source to bridge VSTest xml runsettings configuration into Microsoft Testing Platform configuration model.";

    /// <inheritdoc />
    public Task<bool> IsEnabledAsync() => Task.FromResult(true);

    /// <inheritdoc />
    public Task LoadAsync() => Task.CompletedTask;

    /// <inheritdoc />
    public bool TryGet(string key, out string? value)
    {
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
    public IConfigurationProvider Build()
        => new RunSettingsConfigurationProvider(_runsettings);
}
