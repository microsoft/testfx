// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.Builder;

/// <summary>
/// Represents the configuration options for the builder.
/// </summary>
public sealed class ConfigurationOptions
{
    /// <summary>
    /// Gets the configuration sources options.
    /// </summary>
    public ConfigurationSourcesOptions ConfigurationSources { get; } = new();
}
