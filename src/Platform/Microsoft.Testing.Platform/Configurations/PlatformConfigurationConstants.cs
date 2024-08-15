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
}
