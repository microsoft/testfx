// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Helpers;

namespace Microsoft.Testing.Platform.OutputDevice.Terminal;

/// <summary>
/// Simple terminal that uses 4-bit ANSI for colors but does not move cursor and does not do other fancy stuff to stay compatible with CI systems like AzDo.
/// </summary>
internal sealed class SimpleAnsiTerminal : SimpleTerminal
{
    public SimpleAnsiTerminal(IConsole console)
        : base(console)
    {
    }

    public override void SetColor(TerminalColor color)
    {
        string setColor = $"{AnsiCodes.CSI}{(int)color}{AnsiCodes.SetColor}";
        Console.Write(setColor);
    }

    public override void ResetColor()
        => Console.Write(AnsiCodes.SetDefaultColor);
}
