// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under dual-license. See LICENSE.PLATFORMTOOLS.txt file in the project root for full license information.

using PlatformIConfiguration = Microsoft.Testing.Platform.Configurations.IConfiguration;

namespace Microsoft.Testing.Framework.Configurations;

/// <summary>
/// Wraps a platform IConfiguration instance. This is preferable as it allows us to avoid being dependent
/// on Platform if we need some specific API change.
/// </summary>
internal sealed class ConfigurationWrapper : IConfiguration
{
    public ConfigurationWrapper(PlatformIConfiguration configuration)
        => WrappedConfiguration = configuration;

    internal PlatformIConfiguration WrappedConfiguration { get; }

    public string? this[string key] => WrappedConfiguration[key];
}
