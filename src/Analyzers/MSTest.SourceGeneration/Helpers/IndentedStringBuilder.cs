// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.SourceGeneration.Helpers;

/// <summary>
/// Small helper that produces indented source text. Mirrors the helper used by the existing
/// MSTest.SourceGeneration project so that the generated output is consistent with the rest of
/// the MSTest source generators.
/// </summary>
internal sealed class IndentedStringBuilder
{
    private readonly StringBuilder _builder = new();
    private bool _needsIndent = true;

    public IndentedStringBuilder(int indentationLevel = 0) => IndentationLevel = indentationLevel;

    public int IndentationLevel { get; internal set; }

    public void Append(string value)
    {
        MaybeAppendIndent();
        _builder.Append(value);
        _needsIndent = false;
    }

    public void AppendLine()
    {
        _builder.Append(Constants.NewLine);
        _needsIndent = true;
    }

    public void AppendLine(string value)
    {
        MaybeAppendIndent();
        _builder.Append(value);
        _builder.Append(Constants.NewLine);
        _needsIndent = true;
    }

    public IDisposable AppendBlock(string header)
    {
        AppendLine(header);
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
