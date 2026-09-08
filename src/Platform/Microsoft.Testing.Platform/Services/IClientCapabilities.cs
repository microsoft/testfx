// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.Services;

/// <summary>
/// Represents the capabilities declared by the client that is driving the test host.
/// </summary>
/// <remarks>
/// Capabilities are opt-in: unless a client explicitly declares a capability, the platform assumes the
/// most conservative (default) behavior. Use <see cref="ClientCapabilitiesExtensions.GetIsStateful"/>
/// when the distinction between an explicitly stateless client and an undeclared capability is required.
/// <para>
/// This API is experimental. It may change, break, or be removed at any time without notice.
/// </para>
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
    /// This capability is independent of connection lifetime and multi-request support.
    /// </remarks>
    bool IsStateful { get; }
}

/// <summary>
/// Provides extension methods for <see cref="IClientCapabilities"/>.
/// </summary>
/// <remarks>
/// This API is experimental. It may change, break, or be removed at any time without notice.
/// </remarks>
[Experimental("TPEXP", UrlFormat = "https://aka.ms/testingplatform/diagnostics#{0}")]
public static class ClientCapabilitiesExtensions
{
    /// <summary>
    /// Gets a value indicating whether the client is stateful, or <see langword="null"/> when the client
    /// did not declare the capability.
    /// </summary>
    /// <param name="capabilities">The client capabilities.</param>
    /// <returns>
    /// <see langword="true"/> for a stateful client, <see langword="false"/> for a client that explicitly declares
    /// itself stateless, or <see langword="null"/> when the capability was not declared.
    /// </returns>
    public static bool? GetIsStateful(this IClientCapabilities capabilities)
        => capabilities is ClientCapabilitiesService clientCapabilities
            ? clientCapabilities.DeclaredIsStateful
            : capabilities.IsStateful;
}
