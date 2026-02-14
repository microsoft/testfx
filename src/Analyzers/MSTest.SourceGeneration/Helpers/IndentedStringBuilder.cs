// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under dual-license. See LICENSE.PLATFORMTOOLS.txt file in the project root for full license information.

namespace Microsoft.Testing.Framework.SourceGeneration.Helpers;

internal sealed class IndentedStringBuilder
{
    private readonly StringBuilder _builder = new();
    private bool _needsIndent = true;

    public IndentedStringBuilder(int indentationLevel = 0) => IndentationLevel = indentationLevel;

    public int IndentationLevel { get; internal set; }

    public void Append(char value)
    {
        MaybeAppendIndent();
        _builder.Append(value);
        _needsIndent = false;
    }

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

    public void AppendLine(char value)
    {
        MaybeAppendIndent().Append(value).Append(Constants.NewLine);
        _needsIndent = true;
    }

    public void AppendLine(string value)
    {
        MaybeAppendIndent().Append(value).Append(Constants.NewLine);
        _needsIndent = true;
    }

    public void AppendUnindentedLine(string value)
        => _builder.Append(value).Append(Constants.NewLine);

    public IDisposable AppendBlock(string? value = null, char? closingBraceSuffixChar = null)
    {
        if (value is not null)
        {
            AppendLine(value);
        }

        AppendLine('{');
        IndentationLevel++;

        return new DisposableAction(() =>
        {
            IndentationLevel--;
            Append('}');
            if (closingBraceSuffixChar is not null)
            {
                Append(closingBraceSuffixChar.Value);
            }

            AppendLine();
        });
    }

    public override string ToString() => _builder.ToString();

    private StringBuilder MaybeAppendIndent()
    {
        if (_needsIndent)
        {
            _builder.Append(' ', IndentationLevel * 4);
        }

        return _builder;
    }
}
