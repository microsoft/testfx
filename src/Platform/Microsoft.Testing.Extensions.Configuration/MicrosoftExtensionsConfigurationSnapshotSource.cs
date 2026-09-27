// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.CommandLine;

using MelIConfiguration = Microsoft.Extensions.Configuration.IConfiguration;
using MtpIConfigurationProvider = Microsoft.Testing.Platform.Configurations.IConfigurationProvider;
using MtpIConfigurationSource = Microsoft.Testing.Platform.Configurations.IConfigurationSource;

namespace Microsoft.Testing.Extensions.Configuration;

internal sealed class MicrosoftExtensionsConfigurationSnapshotSource(
    MelIConfiguration configuration,
    int order) : MtpIConfigurationSource
{
    private readonly MelIConfiguration _configuration = configuration;

    public string Uid => "MicrosoftExtensionsConfigurationSnapshot";

    public string Version => ExtensionVersion.DefaultSemVer;

    public string DisplayName => "Microsoft.Extensions.Configuration snapshot";

    public string Description => "Imports a snapshot of an externally owned Microsoft.Extensions.Configuration configuration.";

    public int Order { get; } = order;

    public Task<bool> IsEnabledAsync() => Task.FromResult(true);

    public Task<MtpIConfigurationProvider> BuildAsync(CommandLineParseResult commandLineParseResult)
        => Task.FromResult<MtpIConfigurationProvider>(new MicrosoftExtensionsConfigurationSnapshotProvider(_configuration));
}
