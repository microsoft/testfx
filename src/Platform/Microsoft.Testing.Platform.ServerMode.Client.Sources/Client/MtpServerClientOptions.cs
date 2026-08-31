// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.ServerMode.Client;

/// <summary>
/// Configuration for an <c>MtpServerClient</c>: how it identifies itself in the initialize handshake, which
/// optional capabilities it advertises, how long it waits for the test app to dial back, and where its own
/// diagnostics go.
/// </summary>
internal sealed class MtpServerClientOptions
{
    /// <summary>
    /// Gets or sets the client name reported to the server in the initialize handshake
    /// (<c>clientInfo.name</c>). Defaults to the package's own identity.
    /// </summary>
    public string ClientName { get; set; } = "Microsoft.Testing.Platform.ServerMode.Client";

    /// <summary>
    /// Gets or sets the client compatibility version reported to the server (<c>clientInfo.version</c>).
    /// This is separate from <see cref="SupportedProtocolVersions"/>.
    /// </summary>
    public string ClientVersion { get; set; } = "1.0.0";

    /// <summary>
    /// Gets or sets the server-mode protocol versions supported by the client. The server selects its most
    /// preferred mutually supported version.
    /// </summary>
    public IReadOnlyCollection<string> SupportedProtocolVersions { get; set; } = JsonRpcProtocolVersions.Supported;

    /// <summary>
    /// Gets or sets a value indicating whether the client advertises the reserved debugger-provider
    /// capability (<c>capabilities.testing.debuggerProvider</c>). Microsoft.Testing.Platform protocol 1.0
    /// accepts this field for compatibility but does not currently send debugger requests.
    /// </summary>
    public bool DebuggerProvider { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the client persists an addressable set of test nodes for the
    /// whole session and keeps each node in its last-known state until explicitly updated
    /// (<c>capabilities.testing.isStateful</c>). Defaults to <see langword="false"/>.
    /// </summary>
    /// <remarks>
    /// This capability describes how the client consumes test-node updates. It is independent of connection
    /// lifetime and the server's <c>ServerCapabilities.MultiRequestSupport</c> capability.
    /// </remarks>
    public bool IsStateful { get; set; }

    /// <summary>
    /// Gets or sets how long to wait for the launched test app to connect back to the client's loopback
    /// listener. Overridable by callers per the <c>VSTEST_CONNECTION_TIMEOUT</c> convention (seconds).
    /// Defaults to 90 seconds.
    /// </summary>
    public TimeSpan ConnectionTimeout { get; set; } = TimeSpan.FromSeconds(90);

    /// <summary>
    /// Gets the environment variables injected into the launched test-app process (in addition to the
    /// inherited environment). Useful for passing configuration such as diagnostics switches.
    /// </summary>
    public IDictionary<string, string?> EnvironmentVariables { get; } = new Dictionary<string, string?>(StringComparer.Ordinal);

    /// <summary>
    /// Gets or sets the sink for the client's own diagnostic messages. When <see langword="null"/> the client
    /// uses <see cref="NullMtpClientLogger.Instance"/> (diagnostics are discarded).
    /// </summary>
    public IMtpClientLogger? Logger { get; set; }
}
