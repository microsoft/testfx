// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.Services;

/// <summary>
/// Represents the capabilities declared by the client that is driving the test host.
/// </summary>
/// <remarks>
/// Capabilities are opt-in. The platform preserves whether a client omitted a capability so a test framework
/// can distinguish an explicit value from a legacy client that does not support the capability.
/// <para>
/// This API is experimental. It may change, break, or be removed at any time without notice.
/// </para>
/// </remarks>
[Experimental("TPEXP", UrlFormat = "https://aka.ms/testingplatform/diagnostics#{0}")]
public interface IClientCapabilities
{
    /// <summary>
    /// Gets a value indicating whether the client is stateful, or <see langword="null"/> when the client
    /// did not declare the capability.
    /// </summary>
    /// <remarks>
    /// A stateful client persists an addressable set of test nodes for the whole session and keeps each node in
    /// its last-known state until it is explicitly updated (for example, an IDE test explorer). A stateless client
    /// consumes updates as a stream and does not retain node state after the run (for example, <c>dotnet test</c>).
    /// <see langword="null"/> means that the client does not support or did not declare this capability. Consumers
    /// can use compatibility behavior for known legacy clients only in that case.
    /// This capability is independent of connection lifetime and multi-request support.
    /// </remarks>
    bool? IsStateful { get; }
}
