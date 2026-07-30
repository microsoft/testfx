// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.Telemetry;

/// <summary>
/// Mirrors <c>System.Diagnostics.ActivityKind</c> without taking a dependency on it.
/// </summary>
/// <remarks>
/// The platform targets netstandard2.0 and must stay dependency free, so the instrumentation
/// abstractions cannot expose <c>System.Diagnostics.DiagnosticSource</c> types directly.
/// </remarks>
internal enum PlatformActivityKind
{
    /// <summary>
    /// Internal operation, the default for everything the platform does in-process.
    /// </summary>
    Internal,

    /// <summary>
    /// Handling of an inbound request (for example a server-mode JSON-RPC call).
    /// </summary>
    Server,

    /// <summary>
    /// Outbound request to a remote peer (for example a call to the test host controller).
    /// </summary>
    Client,

    /// <summary>
    /// Message published to a broker/bus, where the consumer runs independently.
    /// </summary>
    Producer,

    /// <summary>
    /// Message consumed from a broker/bus.
    /// </summary>
    Consumer,
}
