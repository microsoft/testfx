// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Text;

namespace Microsoft.Testing.Platform.IPC.SourceGeneration.Helpers;

internal sealed class IndentedStringBuilder
{
    private readonly StringBuilder _builder = new();
    private int _indentationLevel;

    public int IndentationLevel
    {
        get => _indentationLevel;
        set => _indentationLevel = value;
    }

    public void AppendLine(string line = "")
    {
        if (!string.IsNullOrEmpty(line))
        {
            _builder.Append(' ', _indentationLevel * 4);
        }

        _builder.AppendLine(line);
    }

    public IDisposable AppendBlock(string line)
    {
        AppendLine(line);
        AppendLine("{");
        _indentationLevel++;
        return new DisposeAction(() =>
        {
            _indentationLevel--;
            AppendLine("}");
        });
    }

    public override string ToString() => _builder.ToString();

    private sealed class DisposeAction : IDisposable
    {
        private readonly Action _action;

        public DisposeAction(Action action) => _action = action;

        public void Dispose() => _action();
    }
}
