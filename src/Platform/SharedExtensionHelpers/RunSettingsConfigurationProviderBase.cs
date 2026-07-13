// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if !WINDOWS_UWP
using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Configurations;

namespace Microsoft.Testing.Extensions;

/// <summary>
/// Resource-independent base that bridges the <c>&lt;ResultsDirectory&gt;</c> from a .runsettings file into the
/// Microsoft.Testing.Platform configuration model. It owns the XML lookup and the configuration lifecycle; the concrete
/// providers only supply their identity (<see cref="Uid"/>, <see cref="Version"/>, <see cref="DisplayName"/>) and how
/// the raw runsettings content is read. Shared by the VSTest bridge and the MSTest adapter's native
/// Microsoft.Testing.Platform integration.
/// </summary>
[SuppressMessage("ApiDesign", "RS0030:Do not use banned APIs", Justification = "The shared helper is linked into projects that are allowed to use MTP APIs")]
internal abstract class RunSettingsConfigurationProviderBase : IConfigurationSource, IConfigurationProvider
{
    private string? _runSettingsFileContent;

    /// <inheritdoc />
    public abstract string Uid { get; }

    /// <inheritdoc />
    public abstract string Version { get; }

    /// <inheritdoc />
    public abstract string DisplayName { get; }

    /// <inheritdoc />
    public string Description => "Configuration source to bridge VSTest xml runsettings configuration into Microsoft Testing Platform configuration model.";

    public int Order => 2;

    /// <inheritdoc />
    public Task<bool> IsEnabledAsync() => Task.FromResult(true);

    /// <inheritdoc />
    public Task LoadAsync() => Task.CompletedTask;

    /// <inheritdoc />
    public bool TryGet(string key, out string? value)
    {
        if (string.IsNullOrEmpty(_runSettingsFileContent))
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
    public Task<IConfigurationProvider> BuildAsync(CommandLineParseResult commandLineParseResult)
    {
        _runSettingsFileContent = ReadRunSettings(commandLineParseResult);
        return Task.FromResult<IConfigurationProvider>(this);
    }

    /// <summary>
    /// Reads the raw .runsettings content for the given command-line parse result.
    /// </summary>
    protected abstract string? ReadRunSettings(CommandLineParseResult commandLineParseResult);
}
#endif
