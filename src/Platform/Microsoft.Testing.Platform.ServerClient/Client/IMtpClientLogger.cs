// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.ServerMode.Client;

/// <summary>
/// Severity of a diagnostic message emitted by the MTP server client itself (transport, handshake, process
/// lifetime). This is the client's own trace channel and is intentionally decoupled from the platform's
/// <c>LogLevel</c> and from the <c>client/log</c> notifications the server forwards.
/// </summary>
internal enum MtpClientLogLevel
{
    /// <summary>Extremely verbose tracing (raw frames, per-message correlation).</summary>
    Trace,

    /// <summary>Diagnostic detail useful when debugging the client.</summary>
    Debug,

    /// <summary>Informational lifecycle messages (connected, initialized, exited).</summary>
    Information,

    /// <summary>A recoverable problem the caller may want to know about.</summary>
    Warning,

    /// <summary>A failure in the client transport or process handling.</summary>
    Error,
}

/// <summary>
/// Sink for the client's own diagnostic messages. Consumers inject their host logger (vstest's tracing, VS
/// output, C# Dev Kit logging) so the package carries no logging dependency of its own — in particular NO
/// <c>EqtTrace</c> and NO Visual Studio logger.
/// </summary>
internal interface IMtpClientLogger
{
    /// <summary>
    /// Writes a diagnostic message. Implementations must be thread-safe: the client calls this from its
    /// background read loop as well as from caller threads.
    /// </summary>
    /// <param name="level">Severity of the message.</param>
    /// <param name="message">The already-formatted message text.</param>
    void Log(MtpClientLogLevel level, string message);
}

/// <summary>
/// An <see cref="IMtpClientLogger"/> that forwards to a delegate, for callers that prefer a lambda over a type.
/// </summary>
/// <param name="log">The delegate invoked for each message.</param>
internal sealed class DelegateMtpClientLogger(Action<MtpClientLogLevel, string> log) : IMtpClientLogger
{
    private readonly Action<MtpClientLogLevel, string> _log = log ?? throw new ArgumentNullException(nameof(log));

    /// <inheritdoc />
    public void Log(MtpClientLogLevel level, string message)
        => _log(level, message);
}

/// <summary>
/// An <see cref="IMtpClientLogger"/> that discards everything. Used when the caller supplies no logger.
/// </summary>
internal sealed class NullMtpClientLogger : IMtpClientLogger
{
    /// <summary>Gets the shared instance.</summary>
    public static NullMtpClientLogger Instance { get; } = new();

    private NullMtpClientLogger()
    {
    }

    /// <inheritdoc />
    public void Log(MtpClientLogLevel level, string message)
    {
        // Intentionally empty.
    }
}
