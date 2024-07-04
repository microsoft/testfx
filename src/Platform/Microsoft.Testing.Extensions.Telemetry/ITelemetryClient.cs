// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Extensions.Telemetry;

internal interface ITelemetryClient
{
    void TrackEvent(string eventName, Dictionary<string, string> properties, Dictionary<string, double> metrics);
}
