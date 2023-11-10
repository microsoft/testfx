// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.Telemetry;

internal sealed class TelemetryInformation(bool isEnabled, string version) : ITelemetryInformation
{
    public bool IsEnabled { get; } = isEnabled;

    public string Version { get; } = version;
}
