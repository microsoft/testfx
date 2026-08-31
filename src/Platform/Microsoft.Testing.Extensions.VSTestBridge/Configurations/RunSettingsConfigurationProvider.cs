// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Extensions.VSTestBridge.Helpers;
using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Helpers;

namespace Microsoft.Testing.Extensions.VSTestBridge.Configurations;

internal sealed class RunSettingsConfigurationProvider(IFileSystem fileSystem) : RunSettingsConfigurationProviderBase
{
    private readonly IFileSystem _fileSystem = fileSystem;

    /// <inheritdoc />
    public override string Uid => nameof(RunSettingsConfigurationProvider);

    /// <inheritdoc />
    public override string Version => ExtensionVersion.DefaultSemVer;

    /// <inheritdoc />
    public override string DisplayName => "VSTest Helpers: runsettings configuration";

    /// <inheritdoc />
    protected override string? ReadRunSettings(CommandLineParseResult commandLineParseResult)
        => RunSettingsHelpers.ReadRunSettings(commandLineParseResult, _fileSystem);
}
