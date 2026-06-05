// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.Configurations;

internal static class PlatformConfigurationConstants
{
    public const string KeyDelimiter = ":";
    public const string PlatformTestHostControllersManagerSingleConnectionNamedPipeServerWaitConnectionTimeoutSeconds = "platformOptions:testHostControllersManager:singleConnectionNamedPipeServer:waitConnectionTimeoutSeconds";
    public const string PlatformTestHostControllersManagerNamedPipeClientConnectTimeoutSeconds = "platformOptions:testHostControllersManager:namedPipeClient:connectTimeoutSeconds";
    public const string PlatformResultDirectory = "platformOptions:resultDirectory";
    public const string PlatformCurrentWorkingDirectory = "platformOptions:currentWorkingDirectory";
    public const string PlatformTestHostWorkingDirectory = "platformOptions:testHostWorkingDirectory";
    public const string PlatformExitProcessOnUnhandledException = "platformOptions:exitProcessOnUnhandledException";
    public const string PlatformTelemetryIsDevelopmentRepository = "platformOptions:telemetry:isDevelopmentRepository";
    public const string PlatformConfigSuffixFileName = ".testconfig.json";

    /// <summary>
    /// Root section name used by the unified configuration model to expose command-line options
    /// through <see cref="IConfiguration"/>. Both the in-memory CLI-backed provider
    /// (<see cref="CommandLineConfigurationProvider"/>) and the JSON-backed provider flatten
    /// individual options under <c>commandLineOptions:&lt;name&gt;</c> (single value or boolean
    /// presence marker) and <c>commandLineOptions:&lt;name&gt;:&lt;index&gt;</c> (one entry per
    /// argument for multi-value options).
    /// </summary>
    public const string CommandLineOptionsSectionName = "commandLineOptions";
}
