// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.CommandLine;

namespace Microsoft.Testing.Platform.Configurations;

/// <summary>
/// <see cref="IConfigurationSource"/> that exposes the parsed CLI options through the unified
/// <see cref="IConfiguration"/> read model (issue #6349).
/// </summary>
/// <remarks>
/// <para>
/// Registered with <see cref="Order"/> 0 so that CLI-provided values always win against values
/// from environment variables (<see cref="EnvironmentVariablesConfigurationSource"/>, Order 1) and
/// from <c>testconfig.json</c> (<see cref="JsonConfigurationSource"/>, Order 3). See
/// <see cref="AggregatedConfiguration"/> for the first-hit-wins lookup semantics.
/// </para>
/// <para>
/// The provider built by this source mirrors the JSON-flattened layout, allowing both the CLI and
/// <c>commandLineOptions</c> in <c>testconfig.json</c> to be merged transparently. See
/// <see cref="CommandLineConfigurationProvider"/> for the key shape.
/// </para>
/// </remarks>
internal sealed class CommandLineConfigurationSource : ConfigurationSourceBase
{
    // Lowest Order so the CLI wins against env vars (1) and testconfig.json (3).
    public override int Order => 0;

    public override Task<IConfigurationProvider> BuildAsync(CommandLineParseResult commandLineParseResult)
        => Task.FromResult<IConfigurationProvider>(new CommandLineConfigurationProvider(commandLineParseResult));
}
