// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under dual-license. See LICENSE.PLATFORMTOOLS.txt file in the project root for full license information.

using Microsoft.Testing.Platform.Configurations;

namespace Microsoft.Testing.Framework.Configurations;

public static class ConfigurationExtensions
{
    public static string GetTestResultDirectory(this IConfiguration configuration)
        => GetConfigurationWrapper(configuration).WrappedConfiguration.GetTestResultDirectory();

    private static ConfigurationWrapper GetConfigurationWrapper(IConfiguration configuration)
        => configuration as ConfigurationWrapper
           ?? throw new ArgumentException("Current configuration is not of expected type", nameof(configuration));
}
