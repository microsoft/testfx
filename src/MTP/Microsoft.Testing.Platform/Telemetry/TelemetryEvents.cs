// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.Telemetry;

internal static class TelemetryEvents
{
    // Keep these names lowercase. The cluster will lowercase them, so this makes it easier to copy paste to your telemetry query.
    public const string TestHostBuiltEventName = "dotnet/testingplatform/host/testhostbuilt";
    public const string ConsoleTestHostExitEventName = "dotnet/testingplatform/host/consoletesthostexit";
    public const string TestHostControllersTestHostExitEventName = "dotnet/testingplatform/host/testhostcontrollerstesthostexit";

    public const string TestsDiscoveryEventName = "dotnet/testingplatform/execution/testsdiscovery";
    public const string TestsRunEventName = "dotnet/testingplatform/execution/testsrun";
}
