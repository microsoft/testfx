// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.Telemetry;

/// <summary>
/// This service allows to collect usage information.
/// For instance how long different operations run for, what kinds of capabilities
/// are being enabled, disabled.
/// </summary>
internal interface ITelemetryCollector
{
    /// <summary>
    /// Logs a telemetry event.
    /// </summary>
    Task LogEventAsync(string eventName, IDictionary<string, object> paramsMap, CancellationToken cancellationToken);
}
