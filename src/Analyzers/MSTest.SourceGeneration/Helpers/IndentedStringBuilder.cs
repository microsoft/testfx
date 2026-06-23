// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.SourceGeneration.Helpers;

/// <summary>
/// Canonical source-generator indentation helper. This file is linked by
/// MSTest.AotReflection.SourceGeneration so both generators use deterministic newlines.
/// </summary>
internal sealed class IndentedStringBuilder
{
    private readonly StringBuilder _builder = new();
    private bool _needsIndent = true;

    public IndentedStringBuilder(int indentationLevel = 0) => IndentationLevel = indentationLevel;

    public int IndentationLevel { get; internal set; }

    public IndentedStringBuilder Append(string value)
    {
        MaybeAppendIndent();
        _builder.Append(value);
        _needsIndent = false;

        return this;
    }

    public IndentedStringBuilder AppendLine()
    {
        _builder.Append(Constants.NewLine);
        _needsIndent = true;

        return this;
    }

    public IndentedStringBuilder AppendLine(string value)
    {
        MaybeAppendIndent();
        _builder.Append(value);
        _builder.Append(Constants.NewLine);
        _needsIndent = true;

        return this;
    }

    public IDisposable Block(string? header = null)
    {
        if (header is not null)
        {
            AppendLine(header);
        }

        AppendLine("{");
        IndentationLevel++;

        return new DisposableAction(() =>
        {
            IndentationLevel--;
            AppendLine("}");
        });
    }

    public override string ToString() => _builder.ToString();

    private void MaybeAppendIndent()
    {
        if (_needsIndent)
        {
            _builder.Append(' ', IndentationLevel * 4);
            _needsIndent = false;
        }
    }

    private sealed class DisposableAction : IDisposable
    {
        private readonly Action _action;
        private bool _disposed;

        public DisposableAction(Action action) => _action = action;

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _action();
        }
    }
}
