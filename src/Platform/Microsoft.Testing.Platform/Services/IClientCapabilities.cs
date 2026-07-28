// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.Services;

/// <summary>
/// Represents the capabilities declared by the client that is driving the test host.
/// </summary>
/// <remarks>
/// Capabilities are opt-in: unless a client explicitly declares a capability, the platform assumes the
/// most conservative (default) behavior. This lets a test framework tailor its behavior to how the client
/// intends to consume the results without having to guess based on the environment or transport.
/// </remarks>
[Experimental("TPEXP", UrlFormat = "https://aka.ms/testingplatform/diagnostics#{0}")]
public interface IClientCapabilities
{
    /// <summary>
    /// Gets a value indicating whether the client is stateful.
    /// </summary>
    /// <remarks>
    /// A stateful client persists an addressable set of test nodes for the whole session and keeps each node in
    /// its last-known state until it is explicitly updated (for example, an IDE test explorer). A stateless client
    /// consumes updates as a stream and does not retain node state after the run (for example, <c>dotnet test</c>).
    /// The default is <see langword="false"/> (stateless); a client opts into stateful behavior.
    /// </remarks>
    bool IsStateful { get; }
}
