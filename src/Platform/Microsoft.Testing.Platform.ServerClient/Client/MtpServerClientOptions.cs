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
    public string ClientName { get; set; } = "Microsoft.Testing.Platform.ServerClient";

    /// <summary>
    /// Gets or sets the client protocol/tool version reported to the server (<c>clientInfo.version</c>).
    /// </summary>
    public string ClientVersion { get; set; } = "1.0.0";

    /// <summary>
    /// Gets or sets a value indicating whether the client advertises that it can provide a debugger
    /// (<c>capabilities.testing.debuggerProvider</c>). When <see langword="true"/> the server may send
    /// <c>client/attachDebugger</c> / <c>client/launchDebugger</c> requests, which the caller must answer via
    /// a debugger callback. Defaults to <see langword="false"/>.
    /// </summary>
    public bool DebuggerProvider { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the client keeps the connection alive for multiple requests
    /// (<c>capabilities.testing.isStateful</c> / <c>experimental_multiRequestSupport</c>). When
    /// <see langword="false"/> the client performs a single discover or run and then exits. Defaults to
    /// <see langword="false"/>.
    /// </summary>
    /// <remarks>
    /// This flag only advertises the client's willingness to the server during the handshake; it does not by
    /// itself guarantee multi-request behavior. Real keep-alive additionally requires the server to negotiate
    /// it back via <c>ServerCapabilities.MultiRequestSupport</c>. Setting this to <see langword="true"/>
    /// against a server that does not support it has no effect.
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
