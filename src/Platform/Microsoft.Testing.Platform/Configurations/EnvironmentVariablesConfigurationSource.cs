// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Helpers;

namespace Microsoft.Testing.Platform.Configurations;

internal sealed class EnvironmentVariablesConfigurationSource(IEnvironment environmentVariables) : ConfigurationSourceBase
{
    private readonly IEnvironment _environmentVariables = environmentVariables;

    public override int Order => 1;

    public override Task<IConfigurationProvider> BuildAsync(CommandLineParseResult commandLineParseResult)
        => Task.FromResult<IConfigurationProvider>(new EnvironmentVariablesConfigurationProvider(_environmentVariables));
}
