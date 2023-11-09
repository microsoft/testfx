// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.Configurations;

internal static class PlatformConfigurationConstants
{
    public const string KeyDelimiter = ":";
    public const string PlatformTestHostControllersManagerSingleConnectionNamedPipeServerWaitConnectionTimeoutSeconds = "testingPlatform:testHostControllersManager:singleConnectionNamedPipeServer:waitConnectionTimeoutSeconds";
    public const string PlatformTestHostControllersManagerNamedPipeClientConnectTimeoutSeconds = "testingPlatform:testHostControllersManager:namedPipeClient:connectTimeoutSeconds";
    public const string PlatformResultDirectory = "testingPlatform:resultDirectory";
    public const string PlatformCurrentWorkingDirectory = "testingPlatform:currentWorkingDirectory";
    public const string PlatformTestHostWorkingDirectory = "testingPlatform:testHostWorkingDirectory";
    public const string PlatformExitProcessOnUnhandledException = "testingPlatform:exitProcessOnUnhandledException";
    public const string PlatformTelemetryIsDevelopmentRepository = "testingPlatform:telemetry:isDevelopmentRepository";
    public const string PlatformConfigSuffixFileName = ".testingplatformconfig.json";
}
