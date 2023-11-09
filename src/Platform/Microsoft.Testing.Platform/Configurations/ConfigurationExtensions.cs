// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Helpers;

namespace Microsoft.Testing.Platform.Configurations;

public static class ConfigurationExtensions
{
    public static string GetTestResultDirectory(this IConfiguration configuration)
    {
        string? resultDirectory = configuration[PlatformConfigurationConstants.PlatformResultDirectory];
        ArgumentGuard.IsNotNull(resultDirectory, "Unexpected null result directory");
        return resultDirectory;
    }

    public static string GeCurrentWorkingDirectory(this IConfiguration configuration)
    {
        string? workingDirectory = configuration[PlatformConfigurationConstants.PlatformCurrentWorkingDirectory];
        ArgumentGuard.IsNotNull(workingDirectory, "Unexpected null working directory");
        return workingDirectory;
    }

    public static string GeTestHostWorkingDirectory(this IConfiguration configuration)
    {
        string? workingDirectory = configuration[PlatformConfigurationConstants.PlatformTestHostWorkingDirectory];
        ArgumentGuard.IsNotNull(workingDirectory, "Unexpected null working directory");
        return workingDirectory;
    }
}
