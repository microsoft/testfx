// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.OutputDevice.Terminal;

/// <summary>
/// Strategy that decides how in-progress information is rendered to the terminal.
/// </summary>
/// <remarks>
/// Two implementations exist:
/// <list type="bullet">
/// <item><description><see cref="CursorProgressRenderer"/> redraws the progress block in place on every
/// refresh tick (used for cursor-capable ANSI terminals).</description></item>
/// <item><description><see cref="SilenceDrivenHeartbeatRenderer"/> emits single durable lines only when the
/// run goes silent or a test runs slowly (used for <see cref="AnsiMode.SimpleAnsi"/> and
/// <see cref="AnsiMode.NoAnsi"/>, e.g. CI / piped / redirected output).</description></item>
/// </list>
/// All members are invoked by <see cref="TestProgressStateAwareTerminal"/> while it holds the terminal lock,
/// except <see cref="OnTestCompleted"/> which may be called from the message pump and MUST be thread-safe.
/// </remarks>
[UnsupportedOSPlatform("browser")]
internal interface IProgressRenderer
{
    /// <summary>
    /// Gets the desired cadence between <see cref="OnTick"/> invocations. Cursor renderers want a responsive
    /// sub-second redraw, whereas the silence-driven heartbeat only needs second-level granularity, so a
    /// slower cadence avoids unnecessary scan/sort work on every tick in CI / redirected runs.
    /// </summary>
    TimeSpan TickInterval { get; }

    /// <summary>
    /// Called once when progress reporting starts, before the first tick.
    /// </summary>
    void OnStart();

    /// <summary>
    /// Called on every refresh tick of the rendering thread.
    /// </summary>
    void OnTick(ITerminal terminal, TestProgressState?[] progressItems);

    /// <summary>
    /// Called to write durable output to the terminal, giving the renderer a chance to wrap it
    /// (e.g. erase and re-render in-place progress around it).
    /// </summary>
    void OnWrite(ITerminal terminal, TestProgressState?[] progressItems, Action<ITerminal> write);

    /// <summary>
    /// Notifies the renderer that a test completed. Used by the heartbeat renderer to reset its silence timer.
    /// MUST be thread-safe.
    /// </summary>
    void OnTestCompleted();
}
