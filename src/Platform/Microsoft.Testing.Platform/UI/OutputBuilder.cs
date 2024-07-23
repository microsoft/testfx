// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.UI;

internal class OutputBuilder : IDisposable
{
    private readonly ITerminal _terminal;

    public OutputBuilder(ITerminal terminal)
    {
        _terminal = terminal;
        _terminal.StartUpdate();
    }

    public void Append(char value)
        => _terminal.Append(value);

    public void Append(string value)
        => _terminal.Append(value);

    public void AppendLine(string value)
        => _terminal.AppendLine(value);

    public void AppendLine()
        => _terminal.AppendLine();

    public void SetColor(TerminalColor color)
        => _terminal.SetColor(color);

    public void ResetColor()
        => _terminal.ResetColor();

    public void Dispose()
        => _terminal.StopUpdate();

    internal void AppendLink(string path, int? lineNumber)
        => _terminal.AppendLink(path, lineNumber);
}
