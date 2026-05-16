// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.OutputDevice;

/// <summary>
/// Minimal pretty-printing JSON writer used to serialize the <c>--list-tests json</c> document.
/// Avoids depending on either <c>System.Text.Json</c> (only available on .NET) or <c>Jsonite</c>
/// (only compiled on netstandard2.0) so the same code can run on every supported target.
/// </summary>
internal sealed class JsonStringWriter
{
    private readonly StringBuilder _builder = new();
    private readonly Stack<ContainerState> _stack = new();

    public void WriteStartObject(string? name = null) => WriteStartContainer(name, ContainerKind.Object);

    public void WriteEndObject() => WriteEndContainer(ContainerKind.Object);

    public void WriteStartArray(string name) => WriteStartContainer(name, ContainerKind.Array);

    public void WriteEndArray() => WriteEndContainer(ContainerKind.Array);

    public void WriteString(string name, string? value)
    {
        WritePropertyHead(name);
        WriteEscapedString(value);
    }

    public void WriteStringValue(string? value)
    {
        WriteValueHead();
        WriteEscapedString(value);
    }

    public void WriteNumber(string name, int value)
    {
        WritePropertyHead(name);
        _builder.Append(value.ToString(CultureInfo.InvariantCulture));
    }

    public override string ToString() => _builder.ToString();

    private void WriteStartContainer(string? name, ContainerKind kind)
    {
        if (name is null)
        {
            WriteValueHead();
        }
        else
        {
            WritePropertyHead(name);
        }

        _builder.Append(kind == ContainerKind.Object ? '{' : '[');
        _stack.Push(new ContainerState(kind));
    }

    private void WriteEndContainer(ContainerKind kind)
    {
        ContainerState state = _stack.Pop();
        if (state.Kind != kind)
        {
            throw new InvalidOperationException(string.Format(CultureInfo.InvariantCulture, "JSON writer mismatch: expected to close {0} but got {1}.", state.Kind, kind));
        }

        if (state.HasMembers)
        {
            _builder.Append('\n');
            AppendIndent();
        }

        _builder.Append(kind == ContainerKind.Object ? '}' : ']');
    }

    private void WritePropertyHead(string name)
    {
        WriteSeparatorAndIndent();
        WriteEscapedString(name);
        _builder.Append(": ");
    }

    private void WriteValueHead() => WriteSeparatorAndIndent();

    private void WriteSeparatorAndIndent()
    {
        if (_stack.Count > 0)
        {
            ContainerState top = _stack.Peek();
            if (top.HasMembers)
            {
                _builder.Append(',');
            }

            top.HasMembers = true;
        }

        _builder.Append('\n');
        AppendIndent();
    }

    private void AppendIndent() => _builder.Append(' ', _stack.Count * 2);

    private void WriteEscapedString(string? value)
    {
        if (value is null)
        {
            _builder.Append("null");
            return;
        }

        _builder.Append('"');
        for (int i = 0; i < value.Length; i++)
        {
            char c = value[i];
            switch (c)
            {
                case '\\':
                    _builder.Append("\\\\");
                    break;
                case '"':
                    _builder.Append("\\\"");
                    break;
                case '\b':
                    _builder.Append("\\b");
                    break;
                case '\f':
                    _builder.Append("\\f");
                    break;
                case '\n':
                    _builder.Append("\\n");
                    break;
                case '\r':
                    _builder.Append("\\r");
                    break;
                case '\t':
                    _builder.Append("\\t");
                    break;
                case '\u2028':
                case '\u2029':
                    // Valid JSON but invalid in JavaScript string literals (legacy eval() concern);
                    // System.Text.Json escapes these by default so we mirror that.
                    AppendUnicodeEscape(c);
                    break;
                default:
                    if (c < 0x20)
                    {
                        AppendUnicodeEscape(c);
                    }
                    else if (char.IsHighSurrogate(c))
                    {
                        if (i + 1 < value.Length && char.IsLowSurrogate(value[i + 1]))
                        {
                            _builder.Append(c);
                            _builder.Append(value[++i]);
                        }
                        else
                        {
                            _builder.Append('\uFFFD');
                        }
                    }
                    else if (char.IsLowSurrogate(c))
                    {
                        // Unpaired low surrogate.
                        _builder.Append('\uFFFD');
                    }
                    else
                    {
                        _builder.Append(c);
                    }

                    break;
            }
        }

        _builder.Append('"');
    }

    private void AppendUnicodeEscape(char c)
        => _builder.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));

    private enum ContainerKind
    {
        Object,
        Array,
    }

    private sealed class ContainerState(ContainerKind kind)
    {
        public ContainerKind Kind { get; } = kind;

        public bool HasMembers { get; set; }
    }
}
