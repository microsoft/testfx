// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if !WINDOWS_UWP
using Microsoft.Testing.Extensions;
using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Extensions;

using IFileSystem = Microsoft.Testing.Platform.Helpers.IFileSystem;

namespace Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.TestingPlatformAdapter;

/// <summary>
/// MSTest-native configuration source that bridges the <c>&lt;ResultsDirectory&gt;</c> from a .runsettings file into
/// the Microsoft.Testing.Platform configuration model. Mirrors the VSTest bridge's
/// <c>RunSettingsConfigurationProvider</c> without depending on the bridge.
/// </summary>
[SuppressMessage("ApiDesign", "RS0030:Do not use banned APIs", Justification = "We can use MTP from this folder")]
internal sealed class MSTestRunSettingsConfigurationProvider : RunSettingsConfigurationProviderBase
{
    private readonly IExtension _extension;
    private readonly IFileSystem _fileSystem;

    public MSTestRunSettingsConfigurationProvider(IExtension extension, IFileSystem fileSystem)
    {
        _extension = extension;
        _fileSystem = fileSystem;
    }

    public override string Uid => nameof(MSTestRunSettingsConfigurationProvider);

    public override string Version => _extension.Version;

    public override string DisplayName => "MSTest: runsettings configuration";

    protected override string? ReadRunSettings(CommandLineParseResult commandLineParseResult)
    {
        _ = commandLineParseResult.TryGetOptionArgumentList(MSTestRunSettingsCommandLineOptionsProvider.RunSettingsOptionName, out string[]? fileNames);
        return MSTestRunSettings.ReadRunSettings(fileNames, _fileSystem);
    }
}
#endif
