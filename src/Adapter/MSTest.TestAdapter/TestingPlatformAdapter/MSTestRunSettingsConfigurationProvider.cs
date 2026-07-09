// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if !WINDOWS_UWP
using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Configurations;
using Microsoft.Testing.Platform.Extensions;

using IFileSystem = Microsoft.Testing.Platform.Helpers.IFileSystem;

namespace Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.TestingPlatformAdapter;

/// <summary>
/// MSTest-native configuration source that bridges the <c>&lt;ResultsDirectory&gt;</c> from a .runsettings file into
/// the Microsoft.Testing.Platform configuration model. Mirrors the VSTest bridge's
/// <c>RunSettingsConfigurationProvider</c> without depending on the bridge.
/// </summary>
[SuppressMessage("ApiDesign", "RS0030:Do not use banned APIs", Justification = "We can use MTP from this folder")]
internal sealed class MSTestRunSettingsConfigurationProvider : IConfigurationSource, IConfigurationProvider
{
    private readonly IExtension _extension;
    private readonly IFileSystem _fileSystem;
    private string? _runSettingsFileContent;

    public MSTestRunSettingsConfigurationProvider(IExtension extension, IFileSystem fileSystem)
    {
        _extension = extension;
        _fileSystem = fileSystem;
    }

    public string Uid => nameof(MSTestRunSettingsConfigurationProvider);

    public string Version => _extension.Version;

    public string DisplayName => "MSTest: runsettings configuration";

    public string Description => "Configuration source to bridge VSTest xml runsettings configuration into Microsoft Testing Platform configuration model.";

    public int Order => 2;

    public Task<bool> IsEnabledAsync() => Task.FromResult(true);

    public Task LoadAsync() => Task.CompletedTask;

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

    public Task<IConfigurationProvider> BuildAsync(CommandLineParseResult commandLineParseResult)
    {
        _ = commandLineParseResult.TryGetOptionArgumentList(MSTestRunSettingsCommandLineOptionsProvider.RunSettingsOptionName, out string[]? fileNames);
        _runSettingsFileContent = MSTestRunSettings.ReadRunSettings(fileNames, _fileSystem);
        return Task.FromResult<IConfigurationProvider>(this);
    }
}
#endif
