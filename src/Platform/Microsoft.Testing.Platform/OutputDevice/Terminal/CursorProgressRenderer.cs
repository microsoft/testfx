// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.OutputDevice.Terminal;

/// <summary>
/// The classic renderer that redraws the whole progress block in place on every refresh tick.
/// Used for cursor-capable ANSI terminals (<see cref="AnsiMode.AnsiIfPossible"/> resolved to ANSI,
/// or <see cref="AnsiMode.ForceAnsi"/>).
/// </summary>
[UnsupportedOSPlatform("browser")]
internal sealed class CursorProgressRenderer : IProgressRenderer
{
    public void OnStart()
    {
        // Nothing to do, the busy indicator and refresher are managed by TestProgressStateAwareTerminal.
    }

    public void OnTick(ITerminal terminal, TestProgressState?[] progressItems)
        => terminal.RenderProgress(progressItems);

    public void OnWrite(ITerminal terminal, TestProgressState?[] progressItems, Action<ITerminal> write)
    {
        terminal.EraseProgress();
        write(terminal);
        terminal.RenderProgress(progressItems);
    }

    public void OnTestCompleted()
    {
        // The cursor renderer reflects completion via the next in-place redraw, no silence tracking needed.
    }
}
