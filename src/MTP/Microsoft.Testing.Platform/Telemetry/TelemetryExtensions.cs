// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.Telemetry;

internal static class TelemetryExtensions
{
    internal static string AsTelemetryBool(this bool value) => value ? TelemetryProperties.True : TelemetryProperties.False;
}
