// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.Builder;

/// <summary>
/// Represents the options for configuration sources.
/// </summary>
public sealed class ConfigurationSourcesOptions
{
    /// <summary>
    /// Gets or sets a value indicating whether to register the environment variables configuration source.
    /// </summary>
    public bool RegisterEnvironmentVariablesConfigurationSource { get; set; } = true;
}
