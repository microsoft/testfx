// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Extensions.Telemetry;

internal sealed class AppInsightTelemetryClientFactory : ITelemetryClientFactory
{
    public ITelemetryClient Create(string? currentSessionId, string osVersion)
        => new AppInsightTelemetryClient(currentSessionId, osVersion);
}
