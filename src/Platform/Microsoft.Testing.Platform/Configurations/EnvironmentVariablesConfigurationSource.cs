// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Helpers;

namespace Microsoft.Testing.Platform.Configurations;

internal sealed class EnvironmentVariablesConfigurationSource(IEnvironment environmentVariables) : IConfigurationSource
{
    private readonly IEnvironment _environmentVariables = environmentVariables;

    public string Uid => nameof(EnvironmentVariablesConfigurationSource);

    public string Version => AppVersion.DefaultSemVer;

    // Can be empty string because it's not used in the UI
    public string DisplayName => string.Empty;

    // Can be empty string because it's not used in the UI
    public string Description => string.Empty;

    public int Order => 1;

    public Task<bool> IsEnabledAsync() => Task.FromResult(true);

    public Task<IConfigurationProvider> BuildAsync(CommandLineParseResult commandLineParseResult)
        => Task.FromResult<IConfigurationProvider>(new EnvironmentVariablesConfigurationProvider(_environmentVariables));
}
