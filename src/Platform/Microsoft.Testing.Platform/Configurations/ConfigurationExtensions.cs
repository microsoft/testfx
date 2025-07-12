// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

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
        return Guard.NotNull(resultDirectory);
    }

    /// <summary>
    /// Gets the current working directory from the configuration.
    /// </summary>
    /// <param name="configuration">The configuration.</param>
    /// <returns>The current working directory.</returns>
    public static string GetCurrentWorkingDirectory(this IConfiguration configuration)
    {
        string? workingDirectory = configuration[PlatformConfigurationConstants.PlatformCurrentWorkingDirectory];
        return Guard.NotNull(workingDirectory);
    }

    /// <summary>
    /// Gets the test host working directory from the configuration.
    /// </summary>
    /// <param name="configuration">The configuration.</param>
    /// <returns>The test host working directory.</returns>
    [Obsolete("This API is obsolete and will be removed in v2. Use GetCurrentWorkingDirectory instead.", error: true)]
    // TODO: This is only used by CC so far. It can be removed once we update CC in this repo to a version where the API is no longer used.
    public static string GetTestHostWorkingDirectory(this IConfiguration configuration)
    {
        string? workingDirectory = configuration[PlatformConfigurationConstants.PlatformTestHostWorkingDirectory];
        return Guard.NotNull(workingDirectory);
    }
}
