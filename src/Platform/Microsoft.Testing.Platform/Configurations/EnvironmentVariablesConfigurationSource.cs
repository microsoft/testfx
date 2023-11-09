// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Helpers;

namespace Microsoft.Testing.Platform.Configurations;

internal class EnvironmentVariablesConfigurationSource : IConfigurationSource
{
    private readonly IEnvironment _environmentVariables;

    public EnvironmentVariablesConfigurationSource(IEnvironment environmentVariables)
    {
        _environmentVariables = environmentVariables;
    }

    public string Uid => nameof(EnvironmentVariablesConfigurationSource);

    public string Version => AppVersion.DefaultSemVer;

    public string DisplayName => nameof(EnvironmentVariablesConfigurationSource);

    public string Description => "Environment variable based configuration source.";

    public Task<bool> IsEnabledAsync() => Task.FromResult(true);

    public IConfigurationProvider Build()
        => new EnvironmentVariablesConfigurationProvider(_environmentVariables);
}
