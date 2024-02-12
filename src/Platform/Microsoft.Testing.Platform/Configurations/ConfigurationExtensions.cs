// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Helpers;

namespace Microsoft.Testing.Platform.Configurations;

/// <summary>
/// Provides extension methods for the IConfiguration interface.
/// </summary>
public static class ConfigurationExtensions
{
    /// <summary>
    /// Gets the test result directory from the configuration.
    /// </summary>
    /// <param name="configuration">The configuration.</param>
    /// <returns>The test result directory.</returns>
    public static string GetTestResultDirectory(this IConfiguration configuration)
    {
        string? resultDirectory = configuration[PlatformConfigurationConstants.PlatformResultDirectory];
        ArgumentGuard.IsNotNull(resultDirectory);
        return resultDirectory;
    }

    /// <summary>
    /// Gets the current working directory from the configuration.
    /// </summary>
    /// <param name="configuration">The configuration.</param>
    /// <returns>The current working directory.</returns>
    public static string GetCurrentWorkingDirectory(this IConfiguration configuration)
    {
        string? workingDirectory = configuration[PlatformConfigurationConstants.PlatformCurrentWorkingDirectory];
        ArgumentGuard.IsNotNull(workingDirectory);
        return workingDirectory;
    }

    /// <summary>
    /// Gets the test host working directory from the configuration.
    /// </summary>
    /// <param name="configuration">The configuration.</param>
    /// <returns>The test host working directory.</returns>
    public static string GetTestHostWorkingDirectory(this IConfiguration configuration)
    {
        string? workingDirectory = configuration[PlatformConfigurationConstants.PlatformTestHostWorkingDirectory];
        ArgumentGuard.IsNotNull(workingDirectory);
        return workingDirectory;
    }
}
